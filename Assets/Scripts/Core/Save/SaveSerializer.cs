using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;

namespace Emberfall.Core.Save
{
    /// <summary>
    /// Turns a PlayerProfile into JSON and back. Unknown hero ids are dropped rather than
    /// throwing, so removing a hero from content never bricks an existing save.
    /// </summary>
    public static class SaveSerializer
    {
        /// <summary>
        /// 2: team array grew from 3 to 5 slots. The upgrade path (a 3-entry team read into 5
        /// slots) needs no migration - the read loop is length-tolerant and NormaliseTeam fills
        /// the rest. The bump exists for the downgrade path: a 5-entry team read by 3-slot code
        /// truncates silently, dropping two heroes with no error. Older builds never checked the
        /// version at all, so this build is where the guard starts existing; from here on, a save
        /// from a newer build is refused instead of quietly mangled.
        ///
        /// 3: HeroInstance.RelicId, written beside uid/defId. DESIGN-RELICS allows this to ride
        /// the team-size bump only if relics ship in the same pass; they did not, so 2 -> 3 as
        /// that document specifies. Upgrade needs no migration - a version 2 save has no relicId
        /// key and reads as empty, which is an empty relic slot, which is the default. The bump is
        /// again for the downgrade direction: a version 3 save read by version 2 code silently
        /// drops every equipped relic, and the guard below turns that into a refusal.
        ///
        /// 4: the repeat cap's dailyResetUnix and stageClearsToday map (DESIGN-REPLAY-ECONOMY).
        /// Upgrade needs no migration: a version 3 save has neither key, reads as stamp 0 with an
        /// empty map, and the first clear afterwards rolls the stamp to the current reward day -
        /// which is exactly a player who has spent none of today's paid repeats, the correct and
        /// generous default. The bump is once more for the downgrade direction: version 3 code
        /// drops both keys, so a capped-out player who rolled back would silently get their whole
        /// day of paid repeats handed back, and the guard below refuses instead.
        ///
        /// 5: the relic inventory (DESIGN-RELIC-PROGRESSION section 3) plus the relicDust purse.
        /// The profile grows an ownedRelics array of { relicId, tier }, and the per-hero relicId
        /// key becomes equippedRelicId - a REFERENCE into that array rather than the relic's only
        /// existence. The doc calls this bump 3 -> 4; 3 was the version when it was written, and
        /// the repeat cap has since taken 4, so it is 4 -> 5.
        ///
        /// Upgrade DOES need a migration this time, and it is the reason this is not a free bump:
        /// a version 4 save has no ownedRelics array at all, so a relic that exists only as a
        /// hero's relicId would simply cease to exist under the new model. The read below
        /// synthesises an ownedRelics entry at tier 1 for every hero-level relic it finds, from
        /// either key, so a player who had one equipped keeps it equipped and now also owns it.
        /// A player with none gets an empty collection, which is the correct default.
        ///
        /// Downgrade is guarded the same way 2, 3 and 4 are, by the version check below: version 4
        /// code reads no equippedRelicId and no ownedRelics, so it would silently unequip every
        /// relic AND drop the player's whole relic collection and dust purse. Refusing is the only
        /// safe answer, and refusing is what the existing guard already does once this constant moves.
        ///
        /// 6: the events engine's per-event progress (PlayerProfile.EventProgress) — points earned,
        /// which milestone thresholds are claimed, and an event's own currency balance. Upgrade
        /// needs no migration, unlike the version 4 -> 5 bump: a version 5 save simply has no
        /// eventProgress array, which reads as an empty list, which is the correct state for a
        /// player who has never seen an event - there is no prior format for event progress to
        /// translate out of, the way a hero-level relicId had to become an owned-relics entry.
        /// Downgrade is guarded the same way every bump above is: version 5 code reads no
        /// eventProgress key at all, so it would silently drop every claimed milestone and currency
        /// balance a player earned under version 6, and the version check below refuses that instead.
        ///
        /// 7: The Deep Recital's ladder position (RecitalCurrentFloor, RecitalHighestFloor) and
        /// Echo bank (RecitalEchoesBanked, RecitalEchoesClockUnix). Same shape as the version 5 to 6
        /// bump, and for the same reason: a version 6 save simply has none of these four keys, which
        /// read as PlayerProfile's own field defaults (floor 1, highest 0) rather than a synthesised
        /// value - except the Echo bank, which the read below deliberately defaults to
        /// content.Balance.RecitalEchoesMax rather than to PlayerProfile's bare-field default of 0,
        /// so an upgrading player starts able to attempt a floor immediately rather than waiting out
        /// a full recharge for a resource their save never had an opinion on. Downgrade is guarded
        /// the same way every bump above is.
        ///
        /// 8: armor (DESIGN-ARMOR section 10). The profile grows a `slag` purse and an `ownedArmor`
        /// array, and each hero grows an `equippedArmor` array of uids, one slot per entry in
        /// ArmorInfo.AllSlots.
        ///
        /// Upgrade needs no migration, unlike the version 4 -> 5 relic bump: a version 7 save simply
        /// has none of these keys, which read as zero Slag, an empty armor collection and six empty
        /// slots per hero - exactly a player who has never seen the system. There is no prior format
        /// for armor to translate out of, the way a hero-level relicId had to become an owned-relics
        /// entry.
        ///
        /// An armor piece is written with its RESOLVED rolls, not with a roll seed. A seed would be
        /// smaller and would make a reroll a single integer bump, but armor.json is tuning data that
        /// is expected to move, and a seed would mean every retune silently rerolled every piece
        /// every player owns. This is the same instinct that makes a save hold hero ids rather than
        /// hero stats, applied to a thing that has no id to hold.
        ///
        /// Downgrade is guarded the same way every bump above is: version 7 code reads neither key,
        /// so it would silently drop a player's whole armor collection and Slag purse, and the
        /// version check below refuses that instead.
        /// </summary>
        public const int CurrentVersion = 8;

        /// <summary>
        /// The pre-version-5 per-hero relic key. Read on upgrade and never written again; see the
        /// migration in Deserialize. Named here rather than inlined so the one place that still
        /// understands the old format is obvious.
        /// </summary>
        private const string LegacyHeroRelicKey = "relicId";

        /// <summary>Whether a hero has anything in any armor slot - the gate on writing the
        /// equippedArmor key at all.</summary>
        private static bool HasAnyArmor(HeroInstance hero)
        {
            for (int i = 0; i < hero.EquippedArmor.Length; i++)
                if (!string.IsNullOrEmpty(hero.EquippedArmor[i])) return true;
            return false;
        }

        /// <summary>
        /// One armor stat line. Mode is written rather than re-derived on read: a saved piece keeps
        /// the arithmetic it was rolled with even if ArmorInfo.ModeFor is ever changed under it,
        /// which is the same reason a piece stores resolved values instead of a seed.
        /// </summary>
        private static JsonValue WriteRoll(ArmorStatRoll roll)
        {
            JsonValue rj = JsonValue.NewObject();
            rj.Set("stat", roll.Stat.ToString());
            rj.Set("mode", roll.Mode.ToString());
            rj.Set("value", roll.Value);
            return rj;
        }

        /// <summary>Reads one armor stat line back. An unparseable stat or mode name falls back to
        /// the enum default rather than dropping the line, which keeps a piece's line COUNT stable
        /// across a content rename - a piece that silently lost a substat would look like a
        /// downgrade to the player.</summary>
        private static ArmorStatRoll ReadRoll(JsonValue rj)
        {
            if (rj == null || rj.IsNull) return null;
            ArmorStatRoll roll = new ArmorStatRoll();
            roll.Stat = EnumUtil.Parse(rj.GetString("stat", "Attack"), ArmorStat.Attack);
            roll.Mode = EnumUtil.Parse(rj.GetString("mode", "Multiply"), ArmorStatMode.Multiply);
            roll.Value = rj.GetFloat("value", 0f);
            return roll;
        }

        public static string Serialize(PlayerProfile profile)
        {
            JsonValue root = JsonValue.NewObject();
            root.Set("version", CurrentVersion);
            root.Set("playerName", profile.PlayerName);
            root.Set("playerLevel", profile.PlayerLevel);
            root.Set("playerXp", profile.PlayerXp);
            root.Set("gold", profile.Gold);
            root.Set("shards", profile.Shards);
            root.Set("relicDust", profile.RelicDust);
            root.Set("slag", profile.Slag);
            root.Set("stagesCleared", profile.StagesCleared);
            root.Set("lastIdleClaimUnix", profile.LastIdleClaimUnix);
            root.Set("lastSaveUnix", profile.LastSaveUnix);
            root.Set("dailyResetUnix", profile.DailyResetUnix);
            root.Set("nextHeroUid", profile.NextHeroUid);
            root.Set("nextArmorUid", profile.NextArmorUid);
            root.Set("totalSummons", profile.TotalSummons);
            root.Set("totalBattlesWon", profile.TotalBattlesWon);
            root.Set("recitalCurrentFloor", profile.RecitalCurrentFloor);
            root.Set("recitalHighestFloor", profile.RecitalHighestFloor);
            root.Set("recitalEchoesBanked", profile.RecitalEchoesBanked);
            root.Set("recitalEchoesClockUnix", profile.RecitalEchoesClockUnix);

            JsonValue cleared = JsonValue.NewArray();
            for (int i = 0; i < profile.ClearedStageIds.Count; i++) cleared.Add(profile.ClearedStageIds[i]);
            root.Set("clearedStages", cleared);

            JsonValue heroes = JsonValue.NewArray();
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                JsonValue hj = JsonValue.NewObject();
                hj.Set("uid", h.Uid);
                hj.Set("defId", h.DefId);
                hj.Set("level", h.Level);
                hj.Set("copies", h.Copies);
                hj.Set("acquired", h.AcquiredUnix);
                // Written only when set, so a profile with no relics equipped serialises byte for
                // byte the way it did before relics existed apart from the version number. The
                // legacy "relicId" key is never written again - only read, on upgrade.
                if (!string.IsNullOrEmpty(h.EquippedRelicId)) hj.Set("equippedRelicId", h.EquippedRelicId);

                // Written only when the hero wears something, same reasoning as equippedRelicId
                // above: a profile with no armor equipped serialises byte for byte the way it did
                // before armor existed, apart from the version number and the two profile-level
                // keys. Positional - index i is ArmorInfo.AllSlots[i] - and written in full
                // (empty strings included) once anything is worn, because a compacted array would
                // lose which slot each uid belonged to.
                if (HasAnyArmor(h))
                {
                    JsonValue worn = JsonValue.NewArray();
                    for (int s = 0; s < h.EquippedArmor.Length; s++)
                        worn.Add(new JsonValue(h.EquippedArmor[s] != null ? h.EquippedArmor[s] : ""));
                    hj.Set("equippedArmor", worn);
                }
                heroes.Add(hj);
            }
            root.Set("heroes", heroes);

            // Written even when empty, same reasoning as ownedRelics below: absent means "predates
            // armor", empty means "has armor, owns none", and both must read back as the same
            // thing without needing to tell them apart.
            JsonValue ownedArmor = JsonValue.NewArray();
            for (int i = 0; i < profile.OwnedArmor.Count; i++)
            {
                ArmorPiece piece = profile.OwnedArmor[i];
                if (piece == null || string.IsNullOrEmpty(piece.Uid)) continue;
                JsonValue aj = JsonValue.NewObject();
                aj.Set("uid", piece.Uid);
                aj.Set("name", piece.Name);
                aj.Set("slot", piece.Slot.ToString());
                aj.Set("aspect", piece.Aspect.ToString());
                aj.Set("temper", piece.Temper.ToString());
                if (piece.Primary != null) aj.Set("primary", WriteRoll(piece.Primary));
                JsonValue subs = JsonValue.NewArray();
                for (int s = 0; s < piece.SubStats.Count; s++)
                {
                    if (piece.SubStats[s] == null) continue;
                    subs.Add(WriteRoll(piece.SubStats[s]));
                }
                aj.Set("subStats", subs);
                ownedArmor.Add(aj);
            }
            root.Set("ownedArmor", ownedArmor);

            // The relic collection is written even when empty, unlike the per-hero key above: an
            // empty array and an absent array mean different things here now. Absent is a save
            // from before version 5, which the read below migrates; empty is a version 5 player
            // who genuinely owns no relics and needs nothing synthesised.
            JsonValue ownedRelics = JsonValue.NewArray();
            for (int i = 0; i < profile.OwnedRelics.Count; i++)
            {
                OwnedRelic owned = profile.OwnedRelics[i];
                if (string.IsNullOrEmpty(owned.RelicId)) continue;
                JsonValue oj = JsonValue.NewObject();
                oj.Set("relicId", owned.RelicId);
                oj.Set("tier", owned.Tier);
                ownedRelics.Add(oj);
            }
            root.Set("ownedRelics", ownedRelics);

            // Written even when empty, same reasoning as ownedRelics above: absent means "predates
            // the events engine", empty means "has the engine, has touched nothing yet", and the
            // two must read as the same thing on the way back in without needing to tell them apart.
            JsonValue eventProgress = JsonValue.NewArray();
            for (int i = 0; i < profile.EventProgress.Count; i++)
            {
                EventProgressEntry p = profile.EventProgress[i];
                if (string.IsNullOrEmpty(p.EventId)) continue;
                JsonValue pj = JsonValue.NewObject();
                pj.Set("eventId", p.EventId);
                pj.Set("points", p.Points);
                pj.Set("currency", p.Currency);
                JsonValue claimed = JsonValue.NewArray();
                for (int c = 0; c < p.ClaimedMilestoneThresholds.Count; c++)
                    claimed.Add(new JsonValue((double)p.ClaimedMilestoneThresholds[c]));
                pj.Set("claimedMilestones", claimed);
                eventProgress.Add(pj);
            }
            root.Set("eventProgress", eventProgress);

            // Zero counts are dropped rather than written: absent already means zero on the way
            // back in, so a player who has farmed nothing today serialises no map at all.
            JsonValue clearsToday = JsonValue.NewObject();
            foreach (KeyValuePair<string, int> pair in profile.StageClearsToday)
            {
                if (pair.Value > 0) clearsToday.Set(pair.Key, pair.Value);
            }
            root.Set("stageClearsToday", clearsToday);

            JsonValue team = JsonValue.NewArray();
            for (int i = 0; i < profile.Team.Length; i++) team.Add(new JsonValue(profile.Team[i]));
            root.Set("team", team);

            return root.ToJson(true);
        }

        public static PlayerProfile Deserialize(string json, ContentDatabase content, out string error)
        {
            error = null;
            JsonValue root;
            if (!JsonValue.TryParse(json, out root, out error)) return null;

            // Saves never carried a version below 1, and version was written from the start,
            // so a missing field just means "1". Anything above CurrentVersion comes from a
            // newer build; reading it here would silently drop whatever the newer format added
            // (a 5-slot team truncated to 3 was the first real casualty), so refuse instead.
            int version = root.GetInt("version", 1);
            if (version > CurrentVersion)
            {
                error = "save is from a newer build (version " + version + ", this build reads up to " + CurrentVersion + ")";
                return null;
            }

            PlayerProfile profile = new PlayerProfile();
            profile.PlayerName = root.GetString("playerName", "Wayfarer");
            profile.PlayerLevel = Math.Max(1, root.GetInt("playerLevel", 1));
            profile.PlayerXp = Math.Max(0, root.GetInt("playerXp", 0));
            profile.Gold = Math.Max(0, root.GetLong("gold", 0));
            profile.Shards = Math.Max(0, root.GetLong("shards", 0));
            profile.RelicDust = Math.Max(0, root.GetLong("relicDust", 0));
            profile.Slag = Math.Max(0, root.GetLong("slag", 0));
            profile.StagesCleared = Math.Max(0, root.GetInt("stagesCleared", 0));
            profile.LastIdleClaimUnix = root.GetLong("lastIdleClaimUnix", 0);
            profile.LastSaveUnix = root.GetLong("lastSaveUnix", 0);
            profile.DailyResetUnix = Math.Max(0, root.GetLong("dailyResetUnix", 0));
            profile.NextHeroUid = Math.Max(1, root.GetInt("nextHeroUid", 1));
            profile.NextArmorUid = Math.Max(1, root.GetInt("nextArmorUid", 1));
            profile.TotalSummons = root.GetInt("totalSummons", 0);
            profile.TotalBattlesWon = root.GetInt("totalBattlesWon", 0);

            // Absent on any save before version 7 (or simply never touched) reads as PlayerProfile's
            // own field defaults for the floor position - floor 1, highest 0 - which is exactly a
            // player who has never attempted the Recital. The Echo bank is the one field here that
            // deliberately does NOT default to PlayerProfile's bare 0: an upgrading player should be
            // able to attempt a floor immediately, not stare at a multi-hour recharge for a resource
            // their save format never had an opinion on, so it defaults to a full bank instead.
            int defaultEchoes = content != null ? content.Balance.RecitalEchoesMax : 5;
            profile.RecitalCurrentFloor = Math.Max(1, root.GetInt("recitalCurrentFloor", 1));
            profile.RecitalHighestFloor = Math.Max(0, root.GetInt("recitalHighestFloor", 0));
            profile.RecitalEchoesBanked = Math.Max(0, root.GetInt("recitalEchoesBanked", defaultEchoes));
            profile.RecitalEchoesClockUnix = root.GetLong("recitalEchoesClockUnix", 0);

            List<JsonValue> cleared = root.GetList("clearedStages");
            for (int i = 0; i < cleared.Count; i++)
            {
                string id = cleared[i].AsString(null);
                if (!string.IsNullOrEmpty(id) && !profile.ClearedStageIds.Contains(id)) profile.ClearedStageIds.Add(id);
            }

            List<JsonValue> heroes = root.GetList("heroes");
            for (int i = 0; i < heroes.Count; i++)
            {
                JsonValue hj = heroes[i];
                string defId = hj.GetString("defId", "");
                HeroDef def = content != null ? content.GetHero(defId) : null;
                if (def == null) continue; // content changed under the save - skip quietly

                HeroInstance h = new HeroInstance();
                h.Uid = hj.GetString("uid", profile.AllocateUid());
                h.DefId = defId;
                h.Def = def;
                h.Level = Math.Max(1, hj.GetInt("level", 1));
                h.Copies = Math.Max(1, hj.GetInt("copies", 1));
                h.AcquiredUnix = hj.GetLong("acquired", 0);
                // Missing reads as empty per DESIGN-RELICS. A relic id whose def no longer exists
                // is deliberately NOT dropped here: the simulation resolves ids to defs and treats
                // an unresolvable one as no relic, so keeping the string means re-adding the relic
                // to content restores the equip rather than silently having wiped it.
                // Version 5 writes equippedRelicId; anything older wrote relicId. Reading both
                // here is what makes the migration below a no-op for an already-current save.
                h.EquippedRelicId = hj.GetString("equippedRelicId", hj.GetString(LegacyHeroRelicKey, ""));

                // Positional and length-tolerant, the same way the team array's read loop is: a
                // save written before a seventh slot existed simply fills the first six and leaves
                // the rest empty, and one written after is truncated to what this build has rather
                // than throwing. NormaliseArmor below is what then checks each uid actually belongs
                // in the slot it landed in.
                List<JsonValue> worn = hj.GetList("equippedArmor");
                for (int s = 0; s < worn.Count && s < h.EquippedArmor.Length; s++)
                    h.EquippedArmor[s] = worn[s].AsString("");

                profile.Heroes.Add(h);
            }

            // No migration to perform (see CurrentVersion's doc comment): a pre-armor save has no
            // "ownedArmor" key, GetList returns an empty list, and profile.OwnedArmor stays the
            // empty list it was constructed with - which is exactly "this player has never seen
            // armor". Read BEFORE NormaliseArmor runs, since that pass resolves hero slots against
            // this collection.
            List<JsonValue> ownedArmor = root.GetList("ownedArmor");
            for (int i = 0; i < ownedArmor.Count; i++)
            {
                JsonValue aj = ownedArmor[i];
                string uid = aj.GetString("uid", "");
                if (string.IsNullOrEmpty(uid) || profile.FindArmor(uid) != null) continue;

                ArmorPiece piece = new ArmorPiece();
                piece.Uid = uid;
                piece.Name = aj.GetString("name", "Armor");
                piece.Slot = EnumUtil.Parse(aj.GetString("slot", "Weapon"), ArmorSlot.Weapon);
                piece.Aspect = EnumUtil.Parse(aj.GetString("aspect", "Ember"), Aspect.Ember);
                piece.Temper = EnumUtil.Parse(aj.GetString("temper", "Rough"), ArmorTemper.Rough);

                ArmorStatRoll primary = ReadRoll(aj["primary"]);
                // A piece with no readable primary keeps its slot's guaranteed stat at zero rather
                // than a null Primary, so AllRolls and every UI path stay total. Inert, not absent.
                if (primary == null)
                    primary = new ArmorStatRoll(ArmorInfo.PrimaryStat(piece.Slot), ArmorInfo.ModeFor(ArmorInfo.PrimaryStat(piece.Slot)), 0f);
                piece.Primary = primary;

                List<JsonValue> subs = aj.GetList("subStats");
                for (int s = 0; s < subs.Count; s++)
                {
                    ArmorStatRoll roll = ReadRoll(subs[s]);
                    if (roll != null) piece.SubStats.Add(roll);
                }

                profile.OwnedArmor.Add(piece);
            }

            List<JsonValue> ownedRelics = root.GetList("ownedRelics");
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                string relicId = ownedRelics[i].GetString("relicId", "");
                if (string.IsNullOrEmpty(relicId) || profile.OwnsRelic(relicId)) continue;
                OwnedRelic owned = new OwnedRelic();
                owned.RelicId = relicId;
                owned.Tier = Math.Max(1, ownedRelics[i].GetInt("tier", 1));
                profile.OwnedRelics.Add(owned);
            }

            // THE VERSION 4 -> 5 MIGRATION. Before version 5 a relic existed only as a hero's
            // relicId, so every one of those has to become an owned relic or it vanishes with the
            // format. Synthesised at tier 1, which is the only tier that has ever existed.
            //
            // Deliberately not conditioned on the version number: it is written as "every equipped
            // relic must be owned", which is true of a version 5 save as well and simply finds
            // nothing to do there. A version check would make the invariant hold only on the one
            // read path that happened to be tested.
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                string relicId = profile.Heroes[i].EquippedRelicId;
                if (string.IsNullOrEmpty(relicId) || profile.OwnsRelic(relicId)) continue;
                OwnedRelic synthesised = new OwnedRelic();
                synthesised.RelicId = relicId;
                synthesised.Tier = 1;
                profile.OwnedRelics.Add(synthesised);
            }

            // No migration to perform here (see CurrentVersion's doc comment): a pre-events save
            // simply has no "eventProgress" key, GetList returns an empty list for it, and the loop
            // below adds nothing - profile.EventProgress stays the empty list it was constructed
            // with, which is exactly "this player has never touched an event".
            List<JsonValue> eventProgress = root.GetList("eventProgress");
            for (int i = 0; i < eventProgress.Count; i++)
            {
                string eventId = eventProgress[i].GetString("eventId", "");
                if (string.IsNullOrEmpty(eventId) || profile.FindEventProgress(eventId) != null) continue;
                EventProgressEntry p = new EventProgressEntry();
                p.EventId = eventId;
                p.Points = Math.Max(0, eventProgress[i].GetLong("points", 0));
                p.Currency = Math.Max(0, eventProgress[i].GetLong("currency", 0));
                List<JsonValue> claimed = eventProgress[i].GetList("claimedMilestones");
                for (int c = 0; c < claimed.Count; c++) p.ClaimedMilestoneThresholds.Add(claimed[c].AsLong(0));
                profile.EventProgress.Add(p);
            }

            // Stage ids are not validated against content here for the same reason relic ids are
            // not: a stage that vanished and came back should not have had its counter wiped in
            // between, and a counter against an id nothing can launch is inert either way.
            JsonValue clearsToday = root["stageClearsToday"];
            foreach (string stageId in clearsToday.Keys)
            {
                int count = clearsToday[stageId].AsInt(0);
                if (count > 0) profile.StageClearsToday[stageId] = count;
            }

            List<JsonValue> team = root.GetList("team");
            for (int i = 0; i < team.Count && i < profile.Team.Length; i++)
            {
                profile.Team[i] = team[i].AsString(null);
            }

            profile.NormaliseTeam();
            profile.NormaliseRelics();
            profile.NormaliseArmor();
            profile.NormaliseRecital();
            return profile;
        }
    }
}
