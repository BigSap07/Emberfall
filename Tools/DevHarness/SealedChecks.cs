using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// SEALED (SealedState): anything a purchase grants is marked in the save, and a summon, a
    /// Conversion or a forge gamble refuses it, while a deterministic sink takes it. No purchase
    /// path exists, so every Sealed good here comes from SealedGrants, the debug grant.
    ///
    /// One check per refusal, each proving the refusal changed nothing: the save is byte for byte
    /// what it was. Then the deterministic sinks that must still accept Sealed goods, the save
    /// (version 24 only when a mark is held, damaged blocks, clamps), and the rule that nothing in
    /// the game calls the debug grant.
    /// </summary>
    internal static partial class Program
    {
        private const long SealedBase = 1760000000L;

        private static void CheckSealed(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("SEALED (the Belgian firewall: a purchased good never enters a summon, a conversion or a forge gamble)");
            CheckSealedSummonRefuses(db);
            CheckSealedConversionRefuses(db);
            CheckSealedForgeRefuses(db);
            CheckSealedDeterministicSinksAccept(db);
            CheckSealedSave(db);
            CheckSealedNothingCallsTheGrant(contentDir);
        }

        /// <summary>The cloud and save audit alone (CloudChecks.Run), for a pass that moved the save
        /// version: --check CloudChecksAlone. The full run calls it from Main; this is the aid.</summary>
        private static void CloudChecksAlone(ContentDatabase db, string contentDir)
        {
            _failures += CloudChecks.Run(db, contentDir);
        }

        // ------------------------------------------------------------------ helpers

        private static GameSession SealedSession(ContentDatabase db)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, SealedBase);
            return new GameSession(db, p);
        }

        private static string SealedSnapshot(GameSession s) { return SaveSerializer.Serialize(s.Profile); }

        private static ArmorPiece SealedPiece(ContentDatabase db, GameSession s, ArmorSlot slot, ArmorTemper temper, int seed, bool sealedPiece)
        {
            ArmorPiece piece = ArmorRoller.Roll(db.Armor, slot, temper, Aspect.Ember, seed);
            if (sealedPiece) return SealedGrants.GrantArmor(s, piece);
            piece.Uid = s.Profile.AllocateArmorUid();
            s.Profile.OwnedArmor.Add(piece);
            return piece;
        }

        // ------------------------------------------------------------------ 1. summon

        private static void CheckSealedSummonRefuses(ContentDatabase db)
        {
            int cost = db.Balance.SummonCost;

            // Only Sealed Shards: the purse is full and the summon still refuses.
            GameSession a = SealedSession(db);
            a.Profile.Shards = 0;
            SealedGrants.GrantShards(a, cost * 3L);
            string before = SealedSnapshot(a);
            SummonOutcome refused = a.Summon(12345, SealedBase);
            Check(!refused.Success && !a.CanSummon && a.Profile.Shards == cost * 3L && a.Profile.TotalSummons == 0 && SealedSnapshot(a) == before,
                  "SUMMON REFUSES SEALED SHARDS: " + (cost * 3) + " Sealed Shards buy nothing (\"" + refused.Message + "\"), and the save is byte for byte as it was");

            // Mixed purse: the Sealed part does not count toward the price.
            GameSession b = SealedSession(db);
            b.Profile.Shards = 0;
            b.Profile.Shards += cost - 10;
            SealedGrants.GrantShards(b, cost * 2L);
            before = SealedSnapshot(b);
            SummonOutcome shy = b.Summon(777, SealedBase);
            Check(!shy.Success && SealedSnapshot(b) == before && b.Profile.FreeShards == cost - 10,
                  "and a purse of " + (cost - 10) + " free plus " + (cost * 2) + " Sealed is refused: ten short of a summon, and the Sealed part is not borrowed");

            // Enough free Shards: it summons, spends only the free part, and the Sealed part is untouched.
            GameSession c = SealedSession(db);
            c.Profile.Shards = 0;
            c.Profile.Shards += cost;
            SealedGrants.GrantShards(c, cost * 2L);
            long sealedBefore = c.Profile.Sealed.Shards;
            SummonOutcome ok = c.Summon(777, SealedBase);
            Check(ok.Success && c.Profile.Sealed.Shards == sealedBefore && c.Profile.Shards >= sealedBefore && c.Profile.FreeShards < cost,
                  "enough FREE Shards still summons, pays from the free part only, and leaves the Sealed " + sealedBefore + " untouched");
        }

        // ------------------------------------------------------------------ 2. conversion

        private static void CheckSealedConversionRefuses(ContentDatabase db)
        {
            Aspect target = Aspect.Ember;
            int required = db.Balance.ConversionPointsRequired;

            GameSession s = SealedSession(db);
            HeroDef fodder = null;
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                HeroDef h = db.Heroes[i];
                if (h.Stars >= 5 || h.Aspect != target || s.Profile.Owns(h.Id)) continue;
                if (fodder == null || s.FodderPoints(h) > s.FodderPoints(fodder)) fodder = h;
            }
            if (fodder == null) { Check(false, "SEALED CONVERSION: no unowned low-star Ember hero to use as fodder"); return; }
            int points = s.FodderPoints(fodder);
            int n = (required + points - 1) / points;

            HeroInstance body = s.GrantHeroDirect(fodder, SealedBase);
            body.Copies = n + 6;
            List<HeroInstance> list = new List<HeroInstance>();
            for (int i = 0; i < n; i++) list.Add(body);

            Check(s.ConversionBlocker(target, list) == null && s.ConversionSpare(body) == body.Copies - (s.Profile.IsDeployed(body.Uid) ? 1 : 0),
                  "baseline: " + n + " bodies of one earned hero convert (spare " + s.ConversionSpare(body) + ")");

            // One Sealed copy too many: the conversion needs n and only n - 1 are free.
            body.SealedCopies = body.Copies - (n - 1);
            string before = SealedSnapshot(s);
            string blocker = s.ConversionBlocker(target, list);
            ConversionResult refused = s.Convert(target, list, 99, SealedBase);
            Check(blocker != null && !refused.Success && s.ConversionSpare(body) == n - 1 && SealedSnapshot(s) == before,
                  "CONVERSION REFUSES SEALED COPIES: with " + body.SealedCopiesHeld + " of " + body.Copies + " copies Sealed, only " + (n - 1)
                  + " are free and a conversion of " + n + " is refused (\"" + blocker + "\"), the save byte for byte as it was");

            // Exactly enough free copies: it converts, spends free copies only.
            body.SealedCopies = body.Copies - n;
            int sealedHeld = body.SealedCopiesHeld;
            ConversionResult ok = s.Convert(target, list, 99, SealedBase);
            Check(ok.Success && body.SealedCopiesHeld == sealedHeld && body.Copies == sealedHeld && s.Profile.Heroes.Contains(body),
                  "exactly enough free copies converts, spends none of the " + sealedHeld + " Sealed ones, and the hero stays in the collection");

            // Every copy Sealed (a purchased hero): nothing to give.
            GameSession t = SealedSession(db);
            HeroInstance bought = SealedGrants.GrantHeroCopy(t, fodder, SealedBase);
            bought.Copies = n + 2;
            bought.SealedCopies = bought.Copies;
            List<HeroInstance> all = new List<HeroInstance>();
            for (int i = 0; i < n; i++) all.Add(bought);
            before = SealedSnapshot(t);
            ConversionResult none = t.Convert(target, all, 5, SealedBase);
            Check(!none.Success && t.ConversionSpare(bought) == 0 && SealedSnapshot(t) == before,
                  "a hero whose every copy is Sealed gives no body to a conversion, and the save is byte for byte as it was");

            // The deployed body and a Sealed body can be the same body.
            GameSession u = SealedSession(db);
            HeroInstance line = u.GrantHeroDirect(fodder, SealedBase);
            u.Profile.AssignToTeam(0, line.Uid);
            line.Copies = 3;
            line.SealedCopies = 2;
            Check(u.Profile.IsDeployed(line.Uid) && u.ConversionSpare(line) == 1,
                  "a deployed hero with 3 copies, 2 Sealed, has 1 free to give: the body that stays can be a Sealed one");
        }

        // ------------------------------------------------------------------ 3. forge

        private static void CheckSealedForgeRefuses(ContentDatabase db)
        {
            GameSession s = SealedSession(db);
            s.Profile.Slag = 0;
            s.Profile.Slag += 100000;

            ArmorPiece sealedPiece = SealedPiece(db, s, ArmorSlot.Weapon, ArmorTemper.Rough, 11, true);
            ArmorPiece earned = SealedPiece(db, s, ArmorSlot.Weapon, ArmorTemper.Rough, 12, false);
            ArmorPiece earned2 = SealedPiece(db, s, ArmorSlot.Weapon, ArmorTemper.Rough, 13, false);

            string before = SealedSnapshot(s);
            bool reroll = s.RerollArmor(sealedPiece.Uid, SealedBase);
            Check(!reroll && SealedSnapshot(s) == before,
                  "REROLL REFUSES A SEALED PIECE: refused with Slag to spare, the piece and the purse byte for byte as they were");

            bool combine = s.CombineArmor(sealedPiece.Uid, earned.Uid, earned2.Uid, SealedBase);
            Check(!combine && SealedSnapshot(s) == before && s.Profile.ArmorCombineCandidates(sealedPiece).Count == 0,
                  "COMBINE REFUSES A SEALED INPUT: one Sealed piece among three is refused, nothing is spent or removed, and the Forge offers no partners to it");

            Check(s.Profile.ArmorCombineCandidates(earned).Count == 1,
                  "and an earned piece is offered only the other earned piece (the Sealed one is not a partner)");

            // Sealed Slag pays for nothing random.
            GameSession t = SealedSession(db);
            t.Profile.Slag = 0;
            SealedGrants.GrantSlag(t, 100000);
            ArmorPiece e1 = SealedPiece(db, t, ArmorSlot.Weapon, ArmorTemper.Rough, 21, false);
            ArmorPiece e2 = SealedPiece(db, t, ArmorSlot.Weapon, ArmorTemper.Rough, 22, false);
            ArmorPiece e3 = SealedPiece(db, t, ArmorSlot.Weapon, ArmorTemper.Rough, 23, false);
            before = SealedSnapshot(t);
            bool rr = t.RerollArmor(e1.Uid, SealedBase);
            bool cc = t.CombineArmor(e1.Uid, e2.Uid, e3.Uid, SealedBase);
            Check(!rr && !cc && t.Profile.FreeSlag == 0 && SealedSnapshot(t) == before,
                  "REROLL AND COMBINE REFUSE SEALED SLAG: 100000 Sealed Slag pays for neither, and the save is byte for byte as it was");

            // The same forge with free Slag works, and spends the free part only.
            t.Profile.Slag += 100000;
            long sealedSlag = t.Profile.Sealed.Slag;
            bool rrOk = t.RerollArmor(e1.Uid, SealedBase);
            Check(rrOk && t.Profile.Sealed.Slag == sealedSlag && t.Profile.Slag < 200000,
                  "free Slag still rerolls an earned piece and leaves the Sealed " + sealedSlag + " untouched");
        }

        // ------------------------------------------------------------------ 4. deterministic sinks accept

        private static void CheckSealedDeterministicSinksAccept(ContentDatabase db)
        {
            // Level-up takes Sealed Gold, Sealed first.
            GameSession s = SealedSession(db);
            HeroInstance hero = s.Profile.Heroes[0];
            s.Profile.Gold = 0;
            SealedGrants.GrantGold(s, 1000000);
            s.Profile.Gold += 500;
            int cost = s.LevelUpCost(hero);
            long goldBefore = s.Profile.Gold;
            LevelUpResult lv = s.LevelUpHero(hero, 1, SealedBase);
            Check(lv.Success && s.Profile.Gold == goldBefore - cost && s.Profile.Sealed.Gold == 1000000 - cost && s.Profile.FreeGold == 500,
                  "LEVEL-UP TAKES SEALED GOLD, Sealed part first: it paid " + cost + " from the Sealed part and left the 500 free Gold whole");

            // Ascension of the same hero takes Sealed copies, Sealed first.
            GameSession a = SealedSession(db);
            HeroDef five = null;
            for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Stars == 5) { five = db.Heroes[i]; break; }
            HeroInstance h5 = SealedGrants.GrantHeroCopy(a, five, SealedBase);
            int need = StatMath.AscensionCopiesFor(db.Balance, 0);
            h5.Copies = 1 + need + 2;
            h5.SealedCopies = need + 2;
            AscensionResult asc = a.Ascend(h5);
            Check(asc.Success && h5.Copies == 3 && h5.SealedCopiesHeld == 2,
                  "ASCENSION TAKES SEALED COPIES of the same hero, Sealed first: spent " + need + ", left " + h5.Copies + " copies of which " + h5.SealedCopiesHeld + " Sealed");

            // A Sealed armor piece can be worn and salvaged; salvage pays Sealed Slag, never free.
            GameSession w = SealedSession(db);
            w.Profile.Slag = 0;
            ArmorPiece piece = SealedPiece(db, w, ArmorSlot.Weapon, ArmorTemper.Rough, 31, true);
            HeroInstance wearer = w.Profile.Heroes[0];
            Check(w.EquipArmor(wearer, piece.Uid) && w.Profile.HeroWearingArmor(piece.Uid) == wearer,
                  "A SEALED PIECE CAN BE WORN: equipping is deterministic");
            w.UnequipArmor(wearer, piece.Slot);
            int pay = w.SalvageValue(piece);
            bool salvaged = w.SalvageArmor(piece.Uid);
            Check(salvaged && w.Profile.Slag == pay && w.Profile.Sealed.Slag == pay && w.Profile.FreeSlag == 0,
                  "and salvaged: it pays " + pay + " Slag, all of it Sealed, so a purchased piece cannot be washed into Slag a reroll accepts");

            // An earned piece salvages to free Slag, as before.
            ArmorPiece plain = SealedPiece(db, w, ArmorSlot.Helm, ArmorTemper.Rough, 32, false);
            int plainPay = w.SalvageValue(plain);
            w.SalvageArmor(plain.Uid);
            Check(w.Profile.FreeSlag == plainPay,
                  "while an earned piece still salvages to free Slag (" + plainPay + ")");
        }

        // ------------------------------------------------------------------ 5. the save

        private static void CheckSealedSave(ContentDatabase db)
        {
            string error;

            // No mark: the document is the one the build before wrote, with no sealed key anywhere.
            GameSession clean = SealedSession(db);
            string cleanJson = SaveSerializer.Serialize(clean.Profile);
            Check(JsonValue.Parse(cleanJson).GetInt("version", 0) == SaveSerializer.BaseVersion && !cleanJson.Contains("sealed"),
                  "A SAVE WITH NO SEALED GOOD says version " + SaveSerializer.BaseVersion + " and carries no sealed key: the document the build before wrote");

            // Every mark at once, round trip, and the document claims 24.
            GameSession m = SealedSession(db);
            SealedGrants.GrantGold(m, 4000);
            SealedGrants.GrantShards(m, 300);
            SealedGrants.GrantSlag(m, 50);
            HeroDef some = null;
            for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Stars == 4 && !m.Profile.Owns(db.Heroes[i].Id)) { some = db.Heroes[i]; break; }
            SealedGrants.GrantHeroCopy(m, some, SealedBase);
            SealedGrants.GrantHeroCopy(m, some, SealedBase);
            SealedGrants.GrantArmor(m, ArmorRoller.Roll(db.Armor, ArmorSlot.Boots, ArmorTemper.Honed, Aspect.Tide, 5));
            string json = SaveSerializer.Serialize(m.Profile);
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            HeroInstance backHero = back != null ? back.FindHeroByDefId(some.Id) : null;
            Check(back != null && error == null && SaveSerializer.Serialize(back) == json
                  && back.Sealed.Gold == 4000 && back.Sealed.Shards == 300 && back.Sealed.Slag == 50
                  && backHero != null && backHero.SealedCopies == 2 && backHero.Copies == 2
                  && back.OwnedArmor.Count == 1 && back.OwnedArmor[0].Sealed,
                  "SEALED SAVE ROUND TRIP: three purse parts, two Sealed copies of a hero and a Sealed piece save, load back identical and save again byte for byte");
            Check(JsonValue.Parse(json).GetInt("version", 0) == SaveSerializer.CoreGapsVersion && SaveSerializer.VersionOfJson(json) == SaveSerializer.CoreGapsVersion
                  && SaveSerializer.CoreGapsVersion == 24,
                  "and the document claims version " + SaveSerializer.CoreGapsVersion + ", the version that holds Sealed data");

            // Each mark alone is enough to claim 24.
            GameSession g1 = SealedSession(db); SealedGrants.GrantGold(g1, 1);
            GameSession g2 = SealedSession(db); SealedGrants.GrantHeroCopy(g2, some, SealedBase);
            GameSession g3 = SealedSession(db); SealedGrants.GrantArmor(g3, ArmorRoller.Roll(db.Armor, ArmorSlot.Helm, ArmorTemper.Rough, Aspect.Ember, 6));
            Check(SaveSerializer.VersionOf(g1.Profile) == 24 && SaveSerializer.VersionOf(g2.Profile) == 24 && SaveSerializer.VersionOf(g3.Profile) == 24,
                  "any ONE mark (a purse part, a hero copy, a piece) is enough to claim version 24");

            // A version 22 document loads with nothing Sealed, and saves back unchanged.
            PlayerProfile old = SaveSerializer.Deserialize(cleanJson, db, out error);
            Check(old != null && old.Sealed.IsEmpty && !SealedState.ProfileHoldsAny(old) && SaveSerializer.Serialize(old) == cleanJson,
                  "A VERSION 22 SAVE LOADS with nothing Sealed and saves again as exactly the same document at 22");

            // Damaged blocks read as empty and the save still loads.
            string[] damaged = { "\"sealed\": \"x\"", "\"sealed\": 7", "\"sealed\": []", "\"sealed\": { \"gold\": \"x\", \"shards\": [], \"slag\": { } }" };
            int fine = 0;
            for (int i = 0; i < damaged.Length; i++)
            {
                string doc = cleanJson.TrimEnd();
                int last = doc.LastIndexOf('}');
                string withBlock = doc.Substring(0, last).TrimEnd() + ",\n  " + damaged[i] + "\n}";
                PlayerProfile p = SaveSerializer.Deserialize(withBlock, db, out error);
                if (p != null && error == null && p.Sealed.IsEmpty) fine++;
            }
            Check(fine == damaged.Length, "damaged blocks (a string, a number, an array, wrong types inside) read as empty and the save still loads (" + fine + " of " + damaged.Length + ")");

            JsonValue block = JsonValue.NewObject();
            block.Set("gold", -50L);
            block.Set("shards", 99999999999999999L);
            block.Set("slag", 12L);
            SealedState clamped = SealedState.Read(block);
            Check(clamped.Gold == 0 && clamped.Shards == SealedState.PartMax && clamped.Slag == 12,
                  "and the numbers are clamped to what the game could have written: a negative part to zero, an absurd one to " + SealedState.PartMax);

            // A hand-edited sealedCopies above the copies held is clamped to the copies.
            string tampered = json.Replace("\"sealedCopies\": 2", "\"sealedCopies\": 999");
            PlayerProfile clampedHero = SaveSerializer.Deserialize(tampered, db, out error);
            HeroInstance ch = clampedHero != null ? clampedHero.FindHeroByDefId(some.Id) : null;
            Check(ch != null && ch.SealedCopies == ch.Copies,
                  "and a Sealed count above the copies held is clamped to the copies");

            // The purse part never exceeds the purse when read: the free part is the purse less the part.
            PlayerProfile pp = new PlayerProfile();
            pp.Gold = 100; pp.Sealed.Gold = 500;
            pp.Shards = 0; pp.Sealed.Shards = 10;
            Check(pp.FreeGold == 0 && pp.FreeShards == 0 && pp.FreeSlag == 0,
                  "a Sealed part larger than its purse reads as the whole purse: a spend that forgot to lower it errs toward Sealed");

            // Cloud: the push claims the document's own version.
            Check(SaveSerializer.VersionOfJson(cleanJson) == SaveSerializer.BaseVersion,
                  "a save with no Sealed good still pushes as version " + SaveSerializer.BaseVersion + " (VersionOfJson)");
        }

        // ------------------------------------------------------------------ 6. nothing calls the grant

        private static void CheckSealedNothingCallsTheGrant(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "SEALED: the repository root was not found from " + contentDir); return; }
            List<string> hits = new List<string>();
            string[] files = Directory.GetFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                // R19: PurchaseGrants.cs is the ONE game file that grants Sealed goods (CheckStoreNoRandomPath
                // proves the rest of what it may and may not call).
                if (Path.GetFileName(files[i]) == "SealedGrants.cs" || Path.GetFileName(files[i]) == "PurchaseGrants.cs") continue;
                if (File.ReadAllText(files[i]).Contains("SealedGrants.Grant")) hits.Add(Path.GetFileName(files[i]));
            }
            Check(hits.Count == 0,
                  "ONE GRANT PATH: nothing under Assets calls SealedGrants but PurchaseGrants, the one place a purchase becomes Sealed goods" + (hits.Count > 0 ? " (" + string.Join(", ", hits.ToArray()) + ")" : ""));
        }

        private static string SealedRepoRoot(string contentDir)
        {
            string dir = contentDir;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "Assets")) && Directory.Exists(Path.Combine(dir, "Tools"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
