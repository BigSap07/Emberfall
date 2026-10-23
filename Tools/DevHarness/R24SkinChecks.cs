using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Cloud;
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
    /// R24 HERO SKINS (Docs/DESIGN-STORE.md): a look for one hero plus a small stat bonus on that hero only. The ceiling at
    /// load, one skin per hero, the bonus the Hero screen shows equal to the bonus the fight uses, replays that cannot read a
    /// skin, the Store offer a skin derives, the grant an event can name, the save, the test skins hidden in a release build,
    /// zero skins, and the rollback.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR24Skins(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("R24 HERO SKINS (Docs/DESIGN-STORE.md: a small bonus on one hero, one skin per hero, never above 5 percent, replays untouched)");
            _offerContentDir = contentDir;
            bool skinsWas = SkinsSwitch.Enabled, visibleWas = SkinsConfig.TestVisible, storeWas = StoreSwitch.Enabled;
            bool roadWas = WayfarersRoadSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, contractWas = HeroContractSwitch.Enabled;
            try
            {
                SkinsSwitch.Enabled = true;
                StoreSwitch.Enabled = true;
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                SkinsConfig.TestVisible = true;
                CheckR24SkinContent(db);
                CheckR24SkinCeiling(db);
                CheckR24SkinZero(db, contentDir);
                CheckR24SkinOnePerHero(db, contentDir);
                CheckR24SkinBuffMatchesSimulation(db);
                CheckR24SkinLook(db, contentDir);
                CheckR24SkinReplaysUntouched(db, contentDir);
                CheckR24SkinNotHashed(db, contentDir);
                CheckR24SkinStore(db, contentDir);
                CheckR24SkinGrant(db);
                CheckR24SkinSave(db);
                CheckR24SkinReleaseHidesTest(db);
                CheckR24SkinWiring(contentDir);
                CheckR24SkinRollback(db);
            }
            finally
            {
                SkinsSwitch.Enabled = skinsWas;
                SkinsConfig.TestVisible = visibleWas;
                StoreSwitch.Enabled = storeWas;
                WayfarersRoadSwitch.Enabled = roadWas;
                DailyLoopSwitch.Enabled = loopWas;
                HeroContractSwitch.Enabled = contractWas;
                StoreOffers.Bind(null);
            }
            Check(SkinsSwitch.Enabled == skinsWas && SkinsConfig.TestVisible == visibleWas, "R24 HERO SKINS: the switches are put back as the run found them");
        }

        // ------------------------------------------------------------------ helpers

        private const string R24Kael = "kael_the_unyielding", R24Kiva = "kiva_the_forsaken_oath";

        /// <summary>A throwaway database from the shipped files with a different skins.json (and the shipped store.json).</summary>
        private static ContentDatabase R24SkinDb(string contentDir, string skinsJson)
        {
            ContentDatabase fresh = LoadContent(contentDir);
            fresh.LoadSkins(skinsJson);
            fresh.LoadStore(File.ReadAllText(Path.Combine(contentDir, "store.json")));
            return fresh;
        }

        private static string R24SkinFile(params string[] skins)
        {
            return "{ \"skins\": [ " + string.Join(", ", skins) + " ] }";
        }

        private static string R24SkinEntry(string id, string hero, string rarity, string extra)
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"" + id + "\", \"hero\": \"" + hero + "\", \"rarity\": \"" + rarity + "\", \"source\": \"store\"" + (extra.Length > 0 ? ", " + extra : "") + " }";
        }

        private static GameSession R24SkinSession(ContentDatabase db)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, SinkBase);
            for (int i = 0; i < p.Heroes.Count; i++) p.Heroes[i].Level = 8;
            return new GameSession(db, p);
        }

        private static void R24Own(GameSession s, string skinId)
        {
            GrantItem g = new GrantItem(GrantKind.Skin, 0);
            g.SkinId = skinId;
            PurchaseGrants.Apply(s, new PurchaseGrant().Add(g), null, SinkBase);
        }

        private static UnitSnapshot R24PlayerUnit(BattleResult r, string defId)
        {
            for (int i = 0; i < r.Units.Count; i++) if (r.Units[i].IsPlayer && r.Units[i].DefId == defId) return r.Units[i];
            return null;
        }

        // ------------------------------------------------------------------ 1. content

        private static void CheckR24SkinContent(ContentDatabase db)
        {
            SkinsConfig c = db.Skins;
            Check(c.Skins.Count == 2 && c.Warnings.Count == 0 && c.IsUsable, "the shipped skins.json loads clean: two TEST skins, no warnings");
            SkinDef a = c.Skin("kael_azure_test"), b = c.Skin("kiva_verdant_test");
            Check(a != null && b != null && a.Test && b.Test && a.HeroId == R24Kael && b.HeroId == R24Kiva && a.Source == SkinSource.Test,
                  "the two test skins are for Kael and Kiva, flagged test, and visible while test skins are visible");
            Check(a.AttackPct == 2.0 && a.HpPct == 3.0 && a.DefensePct == 0.0 && a.BonusLine() == "+2% Attack, +3% HP",
                  "THE DEFAULT BONUS: +2 percent Attack and +3 percent HP when a skin sets nothing (" + a.BonusLine() + ")");
            Check(b.AttackPct == 3.0 && b.HpPct == 4.0, "and a skin sets its own: Kiva's reads +3 percent Attack and +4 percent HP, from data");
            bool allUnder = true;
            for (int i = 0; i < c.Skins.Count; i++)
                if (c.Skins[i].AttackPct > SkinsConfig.HardCapPct || c.Skins[i].HpPct > SkinsConfig.HardCapPct || c.Skins[i].DefensePct > SkinsConfig.HardCapPct) allUnder = false;
            Check(allUnder && SkinsConfig.HardCapPct == 5.0, "every shipped skin is at or under the 5 percent ceiling");
        }

        // ------------------------------------------------------------------ 2. the ceiling, validated at load

        private static void CheckR24SkinCeiling(ContentDatabase db)
        {
            SkinsConfig five = SkinsConfig.FromJson(R24SkinFile(R24SkinEntry("ok5", R24Kael, "rare", "\"attackPct\": 5, \"hpPct\": 5, \"defensePct\": 5")));
            Check(five.Skins.Count == 1 && five.Warnings.Count == 0, "A SKIN AT EXACTLY 5 PERCENT IN EVERY STAT IS ALLOWED");
            SkinsConfig over = SkinsConfig.FromJson(R24SkinFile(
                R24SkinEntry("atk6", R24Kael, "rare", "\"attackPct\": 6"),
                R24SkinEntry("hp50", R24Kael, "rare", "\"hpPct\": 50"),
                R24SkinEntry("def51", R24Kael, "rare", "\"defensePct\": 5.1"),
                R24SkinEntry("neg", R24Kael, "rare", "\"attackPct\": -1"),
                R24SkinEntry("fine", R24Kael, "rare", "")));
            Check(over.Skins.Count == 1 && over.Skins[0].Id == "fine" && over.Warnings.Count == 4,
                  "THE CEILING IS VALIDATED AT LOAD: a skin above 5 percent in Attack, HP or Defense, or below zero, is dropped with a warning, not clamped (" + over.Skins.Count + " kept, " + over.Warnings.Count + " warnings)");
            SkinsConfig greedy = SkinsConfig.FromJson("{ \"maxBonusPct\": 9, \"skins\": [ " + R24SkinEntry("g", R24Kael, "rare", "\"attackPct\": 7") + " ] }");
            Check(greedy.MaxBonusPct == 5.0 && greedy.Skins.Count == 0, "and a file cannot raise the ceiling: maxBonusPct 9 is lowered to 5 and a 7 percent skin is still dropped");
            SkinsConfig lower = SkinsConfig.FromJson("{ \"maxBonusPct\": 2, \"skins\": [ " + R24SkinEntry("l", R24Kael, "rare", "\"hpPct\": 3") + " ] }");
            Check(lower.MaxBonusPct == 2.0 && lower.Skins.Count == 0, "but it can ask for a lower one");
            SkinsConfig bad = SkinsConfig.FromJson(R24SkinFile(
                "{ \"id\": \"nohero\", \"name\": \"x\" }", "{ \"name\": \"noid\", \"hero\": \"" + R24Kael + "\" }",
                R24SkinEntry("odd", R24Kael, "mythic", ""), R24SkinEntry("dup", R24Kael, "rare", ""), R24SkinEntry("dup", R24Kael, "rare", ""),
                "{ \"id\": \"src\", \"hero\": \"" + R24Kael + "\", \"source\": \"gamble\" }"));
            Check(bad.Skins.Count == 1 && bad.Skins[0].Id == "dup", "no hero, no id, an unknown rarity, a repeated id and an unknown source are dropped with warnings");
            ContentDatabase unknownHero = LoadContent(_offerContentDir);
            unknownHero.LoadSkins(R24SkinFile(R24SkinEntry("ghost", "no_such_hero", "rare", "")));
            Check(unknownHero.Skins.Skins.Count == 0 && unknownHero.Skins.Warnings.Count == 1, "a skin for a hero the roster does not have is dropped at load");
        }

        // ------------------------------------------------------------------ 3. zero skins

        private static void CheckR24SkinZero(ContentDatabase db, string contentDir)
        {
            ContentDatabase none = R24SkinDb(contentDir, null);
            ContentDatabase empty = R24SkinDb(contentDir, "{ \"skins\": [ ] }");
            Check(!none.Skins.IsUsable && !empty.Skins.IsUsable && none.Skins.Warnings.Count == 0, "ZERO SKINS: an absent or empty skins.json is a working system with no skin");
            GameSession s = R24SkinSession(none);
            StoreOffers.Bind(s);
            StoreView v = StoreOffers.View(s, OfferMonday(none));
            bool anySkinOffer = false;
            for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Kind == StoreOfferKind.Skin) anySkinOffer = true;
            HeroInstance kael = s.Profile.FindHeroByDefId(R24Kael);
            UnitStats stats = StatMath.ForHero(kael.Def, kael.Level), same = StatMath.ForHero(kael.Def, kael.Level);
            Check(!anySkinOffer && HeroSkins.ApplyWorn(stats, s.Profile, kael, none) == null && stats.MaxHp == same.MaxHp && stats.Attack == same.Attack
                  && !HeroSkins.Equip(s, kael, "kael_azure_test") && HeroSkins.OwnedFor(s.Profile, R24Kael, none).Count == 0,
                  "and the Store lists no skin, nothing is worn, nothing can be equipped and a stat line is untouched");
            string snap = SaveSerializer.Serialize(s.Profile);
            Check(!snap.Contains("ownedSkins") && !snap.Contains("\"skin\""), "the save of a profile with no skin has no skin key");
        }

        // ------------------------------------------------------------------ 4. one skin per hero

        private static void CheckR24SkinOnePerHero(ContentDatabase db, string contentDir)
        {
            ContentDatabase two = R24SkinDb(contentDir, R24SkinFile(
                R24SkinEntry("k_a", R24Kael, "rare", "\"attackPct\": 2, \"hpPct\": 3"),
                R24SkinEntry("k_b", R24Kael, "epic", "\"attackPct\": 4, \"hpPct\": 1"),
                R24SkinEntry("v_a", R24Kiva, "rare", "")));
            GameSession s = R24SkinSession(two);
            HeroInstance kael = s.Profile.FindHeroByDefId(R24Kael), kiva = s.Profile.FindHeroByDefId(R24Kiva);
            Check(!HeroSkins.Equip(s, kael, "k_a"), "A SKIN THE PLAYER DOES NOT OWN CANNOT BE WORN");
            R24Own(s, "k_a"); R24Own(s, "k_b"); R24Own(s, "v_a");
            Check(s.Profile.OwnsSkin("k_a") && s.Profile.OwnedSkins.Count == 3, "owning is separate from wearing: three skins owned, none worn");
            Check(HeroSkins.Worn(s.Profile, kael, two) == null && kael.EquippedSkinId == "", "none is worn until it is equipped");
            Check(!HeroSkins.Equip(s, kael, "v_a") && !HeroSkins.Equip(s, kiva, "k_a") && kael.EquippedSkinId == "" && kiva.EquippedSkinId == "",
                  "A SKIN FITS ITS OWN HERO ONLY: Kiva's skin on Kael and Kael's on Kiva are refused");
            Check(HeroSkins.Equip(s, kael, "k_a") && kael.EquippedSkinId == "k_a", "an owned skin of the hero equips");
            UnitStats one = StatMath.ForHero(kael.Def, kael.Level);
            HeroSkins.ApplyWorn(one, s.Profile, kael, two);
            Check(HeroSkins.Equip(s, kael, "k_b") && kael.EquippedSkinId == "k_b", "equipping a second skin REPLACES the first: one field, nothing to stack");
            UnitStats other = StatMath.ForHero(kael.Def, kael.Level);
            HeroSkins.ApplyWorn(other, s.Profile, kael, two);
            UnitStats basis = StatMath.ForHero(kael.Def, kael.Level);
            float wantAtk = (float)Math.Round(basis.Attack * 1.04f, MidpointRounding.AwayFromZero), wantHp = (float)Math.Round(basis.MaxHp * 1.01f, MidpointRounding.AwayFromZero);
            Check(other.Attack == wantAtk && other.MaxHp == wantHp && one.Attack != other.Attack,
                  "NO STACKING: with k_b worn the bonus is k_b's alone (+4% Attack, +1% HP), not k_a's added to it");
            Check(HeroSkins.Unequip(s, kael) && kael.EquippedSkinId == "" && s.Profile.OwnsSkin("k_a") && !HeroSkins.Unequip(s, kael),
                  "taking it off keeps it owned");
            // the two heroes wear their own, side by side
            HeroSkins.Equip(s, kael, "k_a"); HeroSkins.Equip(s, kiva, "v_a");
            Check(HeroSkins.Worn(s.Profile, kael, two).Id == "k_a" && HeroSkins.Worn(s.Profile, kiva, two).Id == "v_a",
                  "each hero wears its own skin and neither affects the other's");
            Check(HeroSkins.Worn(s.Profile, kael, two) != null && (new HeroInstance()).EquippedSkinId == "", "a new hero wears nothing");
        }

        // ------------------------------------------------------------------ 5. the Hero screen's number is the fight's

        private static void CheckR24SkinBuffMatchesSimulation(ContentDatabase db)
        {
            GameSession s = R24SkinSession(db);
            HeroInstance kael = s.Profile.FindHeroByDefId(R24Kael);
            StageDef stage = db.Chapters[0].Stages[0];
            R24Own(s, "kael_azure_test");

            BattleResult bare = CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 4242));
            UnitSnapshot bareUnit = R24PlayerUnit(bare, R24Kael);
            Check(bareUnit != null && bareUnit.SkinId == "" && bareUnit.Visual == kael.Def.Visual, "WORN NOTHING: an owned but unworn skin changes neither the stats nor the look (the unit's look is the hero's own object)");

            HeroSkins.Equip(s, kael, "kael_azure_test");
            BattleResult dressed = CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 4242));
            UnitSnapshot unit = R24PlayerUnit(dressed, R24Kael);

            // The Hero screen's expression, character for character: ForHero(def, level, relic, armor, armorConfig) then the worn skin.
            RelicDef relic = s.Profile.EquippedRelicOf(kael, db);
            UnitStats screen = StatMath.ForHero(kael.Def, kael.Level, relic, s.Profile.EquippedArmorOf(kael), db.Armor);
            SkinDef worn = HeroSkins.ApplyWorn(screen, s.Profile, kael, db);
            Check(unit != null && worn != null && unit.MaxHp == screen.MaxHp && unit.Attack == screen.Attack && unit.Defense == screen.Defense,
                  "THE HERO SCREEN'S NUMBER IS THE FIGHT'S: the fight's Kael has " + unit.MaxHp + " HP and " + unit.Attack + " Attack, the screen's expression gives " + screen.MaxHp + " and " + screen.Attack);
            float wantHp = (float)Math.Round(bareUnit.MaxHp * 1.03f, MidpointRounding.AwayFromZero), wantAtk = (float)Math.Round(bareUnit.Attack * 1.02f, MidpointRounding.AwayFromZero);
            Check(unit.MaxHp == wantHp && unit.Attack == wantAtk && unit.Defense == bareUnit.Defense && unit.Speed == bareUnit.Speed,
                  "and it is exactly +3 percent HP (" + bareUnit.MaxHp + " to " + unit.MaxHp + ") and +2 percent Attack (" + bareUnit.Attack + " to " + unit.Attack + "), nothing else moved");
            Check(unit.MaxHp <= bareUnit.MaxHp * 1.05f + 0.5f && unit.Attack <= bareUnit.Attack * 1.05f + 0.5f, "never above 5 percent of the stat in play");

            // only Kael's line moved: Kiva (same team) is untouched
            UnitSnapshot kivaBare = R24PlayerUnit(bare, R24Kiva), kivaDressed = R24PlayerUnit(dressed, R24Kiva);
            Check(kivaBare == null || (kivaDressed.MaxHp == kivaBare.MaxHp && kivaDressed.Attack == kivaBare.Attack), "the bonus is on that hero only: the rest of the line has exactly the numbers it had");

            // team power reads the same call
            int powerBare = 0;
            HeroSkins.Unequip(s, kael);
            powerBare = s.Profile.EffectiveTeamPower(db);
            HeroSkins.Equip(s, kael, "kael_azure_test");
            int powerSkin = s.Profile.EffectiveTeamPower(db);
            Check(powerSkin > powerBare, "THE TEAM POWER READS IT TOO: " + powerBare + " with no skin, " + powerSkin + " with the test skin");

            // the battle result also carries the look
            Check(unit.SkinId == "kael_azure_test" && unit.Visual != kael.Def.Visual && kael.Def.Visual.SkinTint == "", "the unit carries the skin id and its own copy of the look; the hero's content look is untouched");
        }

        // ------------------------------------------------------------------ 6. the look: portrait and battle sprite

        private static void CheckR24SkinLook(ContentDatabase db, string contentDir)
        {
            SkinDef test = db.Skins.Skin("kael_azure_test");
            VisualDef own = db.GetHero(R24Kael).Visual;
            VisualDef dressed = HeroSkins.VisualFor(own, test);
            Check(dressed != own && dressed.Sprite == own.Sprite && dressed.SkinTint.Length == 6 && dressed.SkinTint != own.SkinTint && dressed.Primary != own.Primary
                  && own.SkinTint == "" && own.Primary == db.GetHero(R24Kael).Visual.Primary,
                  "THE TEST SKIN IS A HUE SHIFT OF THE HERO'S OWN ART: same sprite, a tint (" + dressed.SkinTint + ") and a shifted palette, on a copy");
            Check(HeroSkins.TintHex(200f) == dressed.SkinTint && HeroSkins.HueShiftHex("FF0000", 120f) == "00FF00" && HeroSkins.HueShiftHex("00FF00", 120f) == "0000FF" && HeroSkins.HueShiftHex("nonsense", 40f) == "nonsense",
                  "the hue arithmetic is exact (red +120 degrees is green) and leaves what it cannot read alone");

            ContentDatabase art = R24SkinDb(contentDir, R24SkinFile(R24SkinEntry("kael_art", R24Kael, "legendary", "\"sprite\": \"kael_skin_sprite\", \"portrait\": \"kael_skin_portrait\"")));
            VisualDef withArt = HeroSkins.VisualFor(own, art.Skins.Skin("kael_art"));
            Check(withArt.Sprite == "kael_skin_sprite" && withArt.Portrait == "kael_skin_portrait" && withArt.SkinTint == "" && withArt.SpriteStatus == "",
                  "A SKIN WITH DELIVERED ART swaps the battle sprite and the portrait by id and adds no tint");
            VisualDef unchanged = HeroSkins.VisualFor(own, null);
            Check(unchanged == own, "no skin is the hero's own look, the same object");
        }

        // ------------------------------------------------------------------ 7. replays cannot read a skin

        private static void CheckR24SkinReplaysUntouched(ContentDatabase db, string contentDir)
        {
            GameSession s = R24SkinSession(db);
            HeroInstance kael = s.Profile.FindHeroByDefId(R24Kael);
            R24Own(s, "kael_azure_test");
            HeroSkins.Equip(s, kael, "kael_azure_test");
            ContentDatabase noSkins = R24SkinDb(_offerContentDir, null);

            // The guild dungeon fights scratch heroes under an empty profile, from a snapshot that records no skin.
            TeamSnapshot team = TeamSnapshotBuilder.From(db, s.Profile, "R24");
            string a = FingerprintOf(GuildDungeon.Fight(db, team, 77, 3));
            TeamSnapshot teamPlain = TeamSnapshotBuilder.From(noSkins, R24SkinSession(noSkins).Profile, "R24");
            string b = FingerprintOf(GuildDungeon.Fight(noSkins, teamPlain, 77, 3));
            Check(team.Units.Count > 0 && a == b, "THE GUILD DUNGEON REPLAY CANNOT READ A SKIN: the same floor and seed fought with a skin worn in the save and with no skins in the content is the same fight (" + a + ")");

            // A fight against a snapshot opponent skips skins, so two identical lines cannot differ by a purchase.
            StageDef stage = db.Chapters[0].Stages[0];
            BattleLoadout loadout = ProvingGenerator.Generate(db, db.Balance, 12, s.ProvingTargetPower(), ProvingGenerator.SeedFor(12, 0));
            BattleRequest dressed = s.BuildBattleRequest(Opening(stage), 4242);
            dressed.Opponent = loadout;
            string withSkin = FingerprintOf(CombatSimulation.Run(dressed));
            HeroSkins.Unequip(s, kael);
            BattleRequest bare = s.BuildBattleRequest(Opening(stage), 4242);
            bare.Opponent = loadout;
            string without = FingerprintOf(CombatSimulation.Run(bare));
            Check(withSkin == without, "THE PROVING AND FRIEND DUELS SKIP SKINS: a fight against a snapshot opponent is the same with the skin worn and not");
            HeroSkins.Equip(s, kael, "kael_azure_test");

            // A request with no profile (every synthetic harness request) wears nothing.
            BattleRequest noProfile = s.BuildBattleRequest(Opening(stage), 4242);
            noProfile.Profile = null;
            BattleResult plain = CombatSimulation.Run(noProfile);
            UnitSnapshot unit = R24PlayerUnit(plain, R24Kael);
            Check(unit != null && unit.SkinId == "" && unit.MaxHp == StatMath.ForHero(kael.Def, kael.Level).MaxHp, "and a request with no profile wears nothing");
        }

        // ------------------------------------------------------------------ 8. the content version

        private static void CheckR24SkinNotHashed(ContentDatabase db, string contentDir)
        {
            ContentDatabase none = R24SkinDb(contentDir, null);
            ContentDatabase many = R24SkinDb(contentDir, R24SkinFile(R24SkinEntry("x", R24Kael, "rare", ""), R24SkinEntry("y", R24Kiva, "epic", "")));
            Check(db.ContentVersion == none.ContentVersion && db.ContentVersion == many.ContentVersion && db.ContentVersion.Length > 0,
                  "SKINS ARE NOT HASHED INTO ContentVersion: it reads " + db.ContentVersion + " with the shipped skins, with none and with others, so no recorded guild replay is retired");
            int loadParams = 0;
            foreach (System.Reflection.MethodInfo m in typeof(ContentDatabase).GetMethods())
                if (m.Name == "Load" && m.GetParameters().Length > loadParams) loadParams = m.GetParameters().Length;
            Check(loadParams == 8, "ContentDatabase.Load still takes the eight hashed files and skins.json is not a ninth (" + loadParams + ")");
            string root = SealedRepoRoot(contentDir);
            string text = root == null ? "" : File.ReadAllText(Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, "Assets"), "Scripts"), "Core"), Path.Combine("Data", "ContentDatabase.cs")));
            Check(text.Length > 0 && text.IndexOf("public void LoadSkins", StringComparison.Ordinal) > 0, "skins load through LoadSkins, after Load, like guild-path.json");
        }

        // ------------------------------------------------------------------ 9. the Store

        private static void CheckR24SkinStore(ContentDatabase db, string contentDir)
        {
            long now = OfferMonday(db);
            ContentDatabase priced = R24SkinDb(contentDir, R24SkinFile(
                R24SkinEntry("p_rare", R24Kael, "rare", ""), R24SkinEntry("p_epic", R24Kiva, "epic", ""), R24SkinEntry("p_leg", R24Kael, "legendary", "")));
            StoreOfferDef r = priced.Store.Offer("skin_p_rare"), e = priced.Store.Offer("skin_p_epic"), l = priced.Store.Offer("skin_p_leg");
            Check(r != null && e != null && l != null && r.PriceEur == 4.99 && e.PriceEur == 7.99 && l.PriceEur == 9.99,
                  "A SKIN IS EUR 4.99 TO 9.99 BY RARITY: rare 4.99, epic 7.99, legendary 9.99, one offer per skin, derived from the data");
            Check(r.Kind == StoreOfferKind.Skin && r.Limit == 1 && r.Reset == StoreReset.Never && r.Built.Grant.Items.Count == 1 && r.Built.Grant.Items[0].Kind == GrantKind.Skin
                  && r.Built.Grant.Items[0].SkinId == "p_rare" && r.Restorable,
                  "the offer is once, never resets, grants one fixed Skin line, and is restorable like the other one-time purchases");

            GameSession s = R24SkinSession(priced);
            string before = OfferSnapshot(s);
            StoreOfferView view = null;
            StoreView v = StoreOffers.View(s, now);
            for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Id == "skin_p_rare") view = v.Offers[i];
            Check(view != null && view.Available && view.PriceLabel == "EUR 4.99" && view.Lines.Count == 3 && view.Lines[1].Contains("+2% Attack, +3% HP") && view.SkinHeroId == R24Kael,
                  "THE CARD SHOWS IT IN FULL before the purchase: the skin, its hero, the look and the bonus, with the price");

            long gold = s.Profile.Gold, shards = s.Profile.Shards, slag = s.Profile.Slag, dust = s.Profile.RelicDust;
            StoreFulfilment f = StoreOffers.Fulfil(s, "skin_p_rare", null, now);
            Check(f.Success && s.Profile.OwnsSkin("p_rare") && f.Grant.SkinId == "p_rare" && s.Profile.Gold == gold && s.Profile.Shards == shards && s.Profile.Slag == slag && s.Profile.RelicDust == dust,
                  "BOUGHT THROUGH PurchaseGrants.Apply: the skin is owned, no purse moved");
            before = OfferSnapshot(s);
            Check(StoreOffers.Fulfil(s, "skin_p_rare", null, now + 86400L).Reason == StoreRefusal.AlreadyOwned && OfferSnapshot(s) == before
                  && StoreOffers.Check(s, "skin_p_rare", null, OfferUnix(2020, 1, 1, 12)) == StoreRefusal.AlreadyOwned,
                  "ONCE: bought again, or with the clock wound back, it is refused and writes nothing");

            // a skin for a hero the buyer does not own cannot be bought
            GameSession poor = new GameSession(priced, GameSession.CreateNewProfile(priced, SinkBase));
            for (int i = poor.Profile.Heroes.Count - 1; i >= 0; i--) if (poor.Profile.Heroes[i].DefId == R24Kiva) poor.Profile.Heroes.RemoveAt(i);
            before = OfferSnapshot(poor);
            Check(StoreOffers.Check(poor, "skin_p_epic", null, now) == StoreRefusal.NotOwned && !StoreOffers.Fulfil(poor, "skin_p_epic", null, now).Success && OfferSnapshot(poor) == before,
                  "a skin for a hero the buyer does not own is refused as such and writes nothing");

            // the purchase path
            StoreOffers.Bind(s);
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
            GameSession t = R24SkinSession(priced);
            StoreOffers.Bind(t);
            PurchaseResult b1 = ledger.Begin(t, "skin_p_epic", null, now);
            PurchaseResult c1 = ledger.Complete(t, "skin_p_epic", "r24-skin-1", now);
            PurchaseResult again = ledger.Complete(t, "skin_p_epic", "r24-skin-1", now);
            Check(b1.Success && c1.Success && t.Profile.OwnsSkin("p_epic") && !again.Success && again.Reason == PurchaseRefusal.AlreadyGranted && t.Profile.OwnedSkins.Count == 1,
                  "THE PURCHASE PATH: Begin and Complete grant the skin once, the same receipt grants nothing more");
            Check(!ledger.Begin(t, "skin_p_epic", null, now).Success, "and a second Begin is refused");

            // refused by every gamble: nothing a random mechanic spends moved
            t.Profile.Shards = 0;
            string snap = OfferSnapshot(t);
            Check(!t.Summon(1, now).Success && OfferSnapshot(t) == snap && t.Profile.Sealed.IsEmpty, "NO RANDOM MECHANIC SEES A SKIN: a summon after buying is refused and no Sealed Gold, Shard or Slag appeared");

            // the shipped test skins' offers: visible while test skins are, gone when not
            GameSession g = R24SkinSession(db);
            StoreOffers.Bind(g);
            Check(StoreOffers.Check(g, "skin_kael_azure_test", null, now) == StoreRefusal.None && StoreOffers.View(g, now).Offers.Count > 7, "the shipped test skins are on sale while test skins are visible");
            SkinsConfig.TestVisible = false;
            try
            {
                bool listed = false;
                StoreView hidden = StoreOffers.View(g, now);
                for (int i = 0; i < hidden.Offers.Count; i++) if (hidden.Offers[i].Kind == StoreOfferKind.Skin) listed = true;
                Check(!listed && StoreOffers.Check(g, "skin_kael_azure_test", null, now) == StoreRefusal.Unknown && StoreRules.Shared.Find("skin_kael_azure_test") == null
                      && StoreRules.Shared.FindByProduct("placeholder.emberfall.store.skin_kael_azure_test") == null,
                      "and in a release build neither the offer nor its product is listed or found");
            }
            finally { SkinsConfig.TestVisible = true; }
        }

        // ------------------------------------------------------------------ 10. the grant an event track can name

        private static void CheckR24SkinGrant(ContentDatabase db)
        {
            GameSession s = R24SkinSession(db);
            GrantItem item = new GrantItem(GrantKind.Skin, 0);
            item.SkinId = "kael_azure_test";
            PurchaseGrant grant = new PurchaseGrant().Add(item);
            Check(grant.Problem(db) == null && !grant.NeedsChoice, "A SKIN GRANT IS WELL FORMED: one fixed line naming the skin, no pick");
            GrantResult r = PurchaseGrants.Apply(s, grant, null, SinkBase);
            Check(r.Success && s.Profile.OwnsSkin("kael_azure_test") && r.SkinId == "kael_azure_test" && r.Lines[0].Contains("Azure Kael"),
                  "AN EVENT TRACK GRANTS A SKIN BY ID through PurchaseGrants.Apply: owned, the receipt line names it");
            string before = OfferSnapshot(s);
            GrantResult twice = PurchaseGrants.Apply(s, grant, null, SinkBase);
            Check(twice.Success && OfferSnapshot(s) == before && s.Profile.OwnedSkins.Count == 1, "and a skin already owned is not owned twice: the grant is a no-op, so a reward claimed after a purchase loses nothing");
            GrantItem ghost = new GrantItem(GrantKind.Skin, 0);
            ghost.SkinId = "no_such_skin";
            PurchaseGrant bad = new PurchaseGrant().Add(ghost);
            GrantResult refused = PurchaseGrants.Apply(s, bad, null, SinkBase);
            Check(!refused.Success && refused.Reason == GrantRefusal.BadGrant && bad.Problem(db) != null && OfferSnapshot(s) == before, "an unknown skin is no grant and writes nothing");
        }

        // ------------------------------------------------------------------ 11. the save

        private static void CheckR24SkinSave(ContentDatabase db)
        {
            GameSession plain = R24SkinSession(db);
            string plainJson = SaveSerializer.Serialize(plain.Profile);
            Check(SaveSerializer.VersionOf(plain.Profile) == SaveSerializer.BaseVersion && !plainJson.Contains("ownedSkins") && !plainJson.Contains("\"skin\""),
                  "A PROFILE WITH NO SKIN IS THE DOCUMENT THE BUILD BEFORE WROTE: no key, and the version is not raised");
            GameSession s = R24SkinSession(db);
            R24Own(s, "kael_azure_test");
            string ownedJson = SaveSerializer.Serialize(s.Profile);
            Check(JsonValue.Parse(ownedJson).GetInt("version", 0) == SaveSerializer.R24Version && SaveSerializer.R24Version == 26 && ownedJson.Contains("ownedSkins") && !ownedJson.Contains("\"skin\""),
                  "AN OWNED SKIN IS A VERSION 26 DOCUMENT: stamped only now, the key `ownedSkins` written, no hero key yet");
            HeroSkins.Equip(s, s.Profile.FindHeroByDefId(R24Kael), "kael_azure_test");
            string worn = SaveSerializer.Serialize(s.Profile);
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(worn, db, out error);
            HeroInstance kael = back == null ? null : back.FindHeroByDefId(R24Kael);
            Check(error == null && back != null && SaveSerializer.Serialize(back) == worn && kael != null && kael.EquippedSkinId == "kael_azure_test" && back.OwnsSkin("kael_azure_test") && worn.Contains("\"skin\""),
                  "THE SAVE ROUND-TRIPS byte for byte, owned skins and the worn one on its hero");
            Check(HeroSkins.Worn(back, kael, db) != null && new GameSession(db, back).Profile.OwnedSkins.Count == 1, "and the reloaded hero wears it");

            // damaged and unknown entries
            string damaged = worn.Replace("\"ownedSkins\": [", "\"ownedSkins\": [ null, { }, \"\", \"kael_azure_test\", ");
            PlayerProfile dmg = SaveSerializer.Deserialize(damaged, db, out error);
            Check(dmg != null && dmg.OwnedSkins.Count == 1 && dmg.OwnedSkins[0] == "kael_azure_test", "a damaged, empty or repeated entry is skipped on load");
            ContentDatabase gone = R24SkinDb(_offerContentDir, null);
            PlayerProfile kept = SaveSerializer.Deserialize(worn, gone, out error);
            GameSession keptSession = new GameSession(gone, kept);
            UnitStats stats = StatMath.ForHero(kept.FindHeroByDefId(R24Kael).Def, 8), baseStats = StatMath.ForHero(kept.FindHeroByDefId(R24Kael).Def, 8);
            Check(kept != null && kept.OwnsSkin("kael_azure_test") && kept.FindHeroByDefId(R24Kael).EquippedSkinId == "kael_azure_test"
                  && HeroSkins.ApplyWorn(stats, kept, kept.FindHeroByDefId(R24Kael), gone) == null && stats.Attack == baseStats.Attack && SaveSerializer.Serialize(kept) == worn,
                  "A SKIN THE CONTENT NO LONGER NAMES is kept in the save, written back untouched, and buffs nothing");
            // a store-only document still says 25, an older one is read
            Check(SaveSerializer.StoreVersion == 25, "a save that holds only Store data still says version 25");
        }

        // ------------------------------------------------------------------ 12. a release build hides the test skins

        private static void CheckR24SkinReleaseHidesTest(ContentDatabase db)
        {
            GameSession s = R24SkinSession(db);
            R24Own(s, "kael_azure_test");
            HeroInstance kael = s.Profile.FindHeroByDefId(R24Kael);
            HeroSkins.Equip(s, kael, "kael_azure_test");
            string saved = SaveSerializer.Serialize(s.Profile);
            StageDef stage = db.Chapters[0].Stages[0];
            BattleResult visible = CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 99));
            SkinsConfig.TestVisible = false;
            try
            {
                BattleResult release = CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 99));
                UnitSnapshot unit = R24PlayerUnit(release, R24Kael), was = R24PlayerUnit(visible, R24Kael);
                Check(db.Skins.Skin("kael_azure_test") == null && db.Skins.SkinsOf(R24Kael).Count == 0 && db.Skins.Sellable().Count == 0
                      && unit.SkinId == "" && unit.MaxHp < was.MaxHp && HeroSkins.Worn(s.Profile, kael, db) == null && !HeroSkins.Equip(s, kael, "kael_azure_test"),
                      "RELEASE HIDES THE TEST SKINS: with test skins hidden none is listed, sold, equipped or read by a fight, even one a development save wears");
                Check(SaveSerializer.Serialize(s.Profile) == saved, "and the save keeps it, byte for byte, for the day the profile is opened in a build that shows it");
                bool grantRefused = !PurchaseGrants.Apply(s, new PurchaseGrant().Add(new GrantItem { Kind = GrantKind.Skin, SkinId = "kiva_verdant_test" }), null, SinkBase).Success;
                Check(grantRefused, "and a grant of a hidden skin is refused");
            }
            finally { SkinsConfig.TestVisible = true; }
            Check(SkinsConfig.TestVisible, "the default of the flag in the editor and a development build is visible, in a release build hidden (it is a compile-time default)");
        }

        // ------------------------------------------------------------------ 13. wiring

        private static void CheckR24SkinWiring(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "R24 HERO SKINS: the repository root was not found from " + contentDir); return; }
            string sim = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Core/Combat/CombatSimulation.cs".Replace('/', Path.DirectorySeparatorChar)));
            Check(sim.Contains("HeroSkins.ApplyWorn(stats, _request.Profile, hero, _request.Content)") && sim.Contains("_request.Opponent == null ? HeroSkins.ApplyWorn"),
                  "THE SIMULATION APPLIES A SKIN IN ONE PLACE, once per player unit, and skips it against a snapshot opponent");
            string detail = File.ReadAllText(Path.Combine(root, "Assets/Scripts/UI/Screens/HeroDetailScreen.cs".Replace('/', Path.DirectorySeparatorChar)));
            Check(detail.Contains("HeroSkins.ApplyWorn("), "THE HERO SCREEN READS THE SAME CALL, so the number it prints is the number the fight uses");
            string rig = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Battle/UnitRig.cs".Replace('/', Path.DirectorySeparatorChar)));
            Check(rig.Contains("SkinTint(visual)") && File.ReadAllText(Path.Combine(root, "Assets/Scripts/Battle/StagePresentation.cs".Replace('/', Path.DirectorySeparatorChar))).Contains("c.SkinTint = v.SkinTint"),
                  "THE BATTLE SPRITE SWAP: the rig multiplies the sprite by the unit's skin tint and the stage look keeps it");
            string[] banned = { "Random", "DeterministicRng", "NextDouble", "NextInt", ".Range(", "Shuffle", "Guid.NewGuid", "Environment.TickCount", ".Summon(", "SummonOutcome", "ArmorRoller", "RerollArmor", "CombineArmor", ".Convert(", "Pity" };
            List<string> hits = new List<string>();
            string[] files = { "Assets/Scripts/Core/Progression/HeroSkins.cs", "Assets/Scripts/Core/Data/SkinsConfig.cs" };
            for (int i = 0; i < files.Length; i++)
            {
                string[] lines = File.ReadAllText(Path.Combine(root, files[i].Replace('/', Path.DirectorySeparatorChar))).Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    string t = lines[l].TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*")) continue;
                    for (int b = 0; b < banned.Length; b++) if (lines[l].Contains(banned[b])) hits.Add(Path.GetFileName(files[i]) + ": " + banned[b]);
                }
            }
            Check(hits.Count == 0, "HeroSkins and SkinsConfig hold no random call, no summon, no roll and no conversion" + (hits.Count > 0 ? " (" + string.Join("; ", hits.ToArray()) + ")" : ""));
        }

        // ------------------------------------------------------------------ 14. the rollback

        private static void CheckR24SkinRollback(ContentDatabase db)
        {
            long now = OfferMonday(db);
            GameSession s = R24SkinSession(db);
            R24Own(s, "kael_azure_test");
            HeroInstance kael = s.Profile.FindHeroByDefId(R24Kael);
            HeroSkins.Equip(s, kael, "kael_azure_test");
            StageDef stage = db.Chapters[0].Stages[0];
            string saved = SaveSerializer.Serialize(s.Profile);
            string withFingerprint = FingerprintOf(CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 7)));
            GameSession fresh = R24SkinSession(db);
            string baselineFingerprint = FingerprintOf(CombatSimulation.Run(fresh.BuildBattleRequest(Opening(stage), 7)));
            UnitSnapshot dressedUnit = R24PlayerUnit(CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 7)), R24Kael);

            SkinsSwitch.Enabled = false;
            try
            {
                BattleResult off = CombatSimulation.Run(s.BuildBattleRequest(Opening(stage), 7));
                UnitSnapshot unit = R24PlayerUnit(off, R24Kael);
                StoreOffers.Bind(s);
                bool listed = false;
                StoreView v = StoreOffers.View(s, now);
                for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Kind == StoreOfferKind.Skin) listed = true;
                Check(FingerprintOf(off) == baselineFingerprint && unit.SkinId == "" && unit.MaxHp < dressedUnit.MaxHp && unit.Visual == kael.Def.Visual,
                      "ROLLBACK: with the skins switch off a fight is exactly the fight of a hero who has no skin, though the save wears one");
                Check(!listed && StoreOffers.Check(s, "skin_kael_azure_test", null, now) == StoreRefusal.Unknown && !HeroSkins.Equip(s, kael, "kael_azure_test") && HeroSkins.Worn(s.Profile, kael, db) == null,
                      "nothing is sold or equipped, and the Hero screen's call returns nothing");
                Check(SaveSerializer.Serialize(s.Profile) == saved, "and the skins already in the save are kept and written back untouched");
                Check(s.Profile.EffectiveTeamPower(db) == fresh.Profile.EffectiveTeamPower(db), "the team power is the one without a skin");
            }
            finally { SkinsSwitch.Enabled = true; }
            Check(withFingerprint != baselineFingerprint || dressedUnit.MaxHp > R24PlayerUnit(CombatSimulation.Run(fresh.BuildBattleRequest(Opening(stage), 7)), R24Kael).MaxHp,
                  "and switching them back on dresses the hero again");
        }
    }
}
