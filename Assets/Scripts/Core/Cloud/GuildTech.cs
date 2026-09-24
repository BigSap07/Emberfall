using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// What the guild tech tree is, at one moment, for one account: the guild's level and XP, the
    /// rungs THIS MEMBER has bought, and the talent THE GUILD has picked on each family.
    ///
    /// TWO OWNERS, DELIBERATELY, and that split is the whole design:
    ///
    ///   * RUNGS ARE THE MEMBER'S. Bought with the member's own Waymarks, keyed to the account in
    ///     0012 exactly as the Path's unlocks were in 0011, and kept through leaving, being
    ///     removed or watching the guild disband. That is what stops the tree becoming lock-in.
    ///   * THE TALENT IS THE GUILD'S (G4). One active per family, set by the leadership and
    ///     changeable at cycle roll, so two guilds at maximum tech field noticeably different
    ///     sides and the argument about which to run is the point. A member who leaves loses the
    ///     talent, because it was never theirs.
    ///   * THE LEVEL IS THE GUILD'S (G6). It raises ceilings and opens slots and nothing else
    ///     reads it.
    /// </summary>
    public sealed class GuildTechState
    {
        /// <summary>The guild's level, 0 to GuildTechConfig.LevelCeiling. Zero for a player in no
        /// guild, which opens nothing and is not an error.</summary>
        public int Level;

        /// <summary>Accumulated guild XP, for the screen's progress line. The server owns it.</summary>
        public long Xp;

        /// <summary>Members the server counted when it answered, so the client can predict the
        /// next level's cost with the same square root the server charged.</summary>
        public int Members;

        /// <summary>Rungs this member has bought, by family id. Absent reads as none.</summary>
        public readonly Dictionary<string, int> Rungs = new Dictionary<string, int>();

        /// <summary>The guild's active talent id, by family id. Absent reads as no talent, which
        /// is what a family whose slot is not open yet must look like.</summary>
        public readonly Dictionary<string, string> Talents = new Dictionary<string, string>();

        public int RungsIn(string familyId)
        {
            int n;
            return familyId != null && Rungs.TryGetValue(familyId, out n) ? Math.Max(0, n) : 0;
        }

        public string TalentIn(string familyId)
        {
            string id;
            return familyId != null && Talents.TryGetValue(familyId, out id) ? (id ?? "") : "";
        }

        /// <summary>True when nothing here can change a fight: no rung anywhere and no talent
        /// anywhere. THE REFERENCE PLAYER IS THIS (G5), and GuildTechChecks asserts it rather than
        /// changing how the reference player is built.</summary>
        public bool IsEmpty
        {
            get
            {
                foreach (KeyValuePair<string, int> kv in Rungs) if (kv.Value > 0) return false;
                foreach (KeyValuePair<string, string> kv in Talents) if (!string.IsNullOrEmpty(kv.Value)) return false;
                return true;
            }
        }

        public GuildTechState Clone()
        {
            GuildTechState c = new GuildTechState();
            c.Level = Level; c.Xp = Xp; c.Members = Members;
            foreach (KeyValuePair<string, int> kv in Rungs) c.Rungs[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, string> kv in Talents) c.Talents[kv.Key] = kv.Value;
            return c;
        }

        /// <summary>What guild_tech_state returns (0012). A malformed row reads as an empty tree
        /// rather than throwing: no guild tech is always a legal state.</summary>
        public static GuildTechState Read(JsonValue row)
        {
            GuildTechState s = new GuildTechState();
            if (row == null) return s;
            if (row.IsArray) row = row.AsList().Count > 0 ? row.AsList()[0] : new JsonValue();
            s.Level = Math.Max(0, row.GetInt("level", 0));
            s.Xp = Math.Max(0, row.GetLong("xp", 0));
            s.Members = Math.Max(0, row.GetInt("members", 0));

            List<JsonValue> rungs = row.GetList("rungs");
            for (int i = 0; i < rungs.Count; i++)
            {
                string family = rungs[i].GetString("family", "");
                int n = Math.Max(0, rungs[i].GetInt("rungs", 0));
                if (family.Length > 0) s.Rungs[family] = n;
            }
            List<JsonValue> talents = row.GetList("talents");
            for (int i = 0; i < talents.Count; i++)
            {
                string family = talents[i].GetString("family", "");
                string id = talents[i].GetString("talent", "");
                if (family.Length > 0) s.Talents[family] = id;
            }
            return s;
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("level", Level);
            o.Set("xp", Xp);
            o.Set("members", Members);
            JsonValue rungs = JsonValue.NewArray();
            foreach (KeyValuePair<string, int> kv in Rungs)
            {
                if (kv.Value <= 0) continue;
                JsonValue r = JsonValue.NewObject();
                r.Set("family", kv.Key);
                r.Set("rungs", kv.Value);
                rungs.Add(r);
            }
            o.Set("rungs", rungs);
            JsonValue talents = JsonValue.NewArray();
            foreach (KeyValuePair<string, string> kv in Talents)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                JsonValue t = JsonValue.NewObject();
                t.Set("family", kv.Key);
                t.Set("talent", kv.Value);
                talents.Add(t);
            }
            o.Set("talents", talents);
            return o;
        }
    }

    /// <summary>
    /// THE ONE PLACE THAT TURNS GUILD TECH INTO NUMBERS. Engine-free and pure, like everything
    /// under Core: given the tree, the account's state and a hero's role, it answers what the stat
    /// rungs add and which guild talent the hero carries. CombatSimulation, StatMath's caller and
    /// the hero detail preview all come through here, so the number a player reads on a card is by
    /// construction the number the simulation fights with.
    ///
    /// G1 IS ENFORCED HERE AND NOWHERE ELSE: every answer below is a function of the hero's ROLE.
    /// Nothing in this file reads an aspect, a star tier, a level or who else is in the line.
    /// </summary>
    public static class GuildTech
    {
        /// <summary>
        /// Rungs that actually count on a family: what the member bought, held down to what the
        /// guild's level has opened and to what the tree authors.
        ///
        /// CLAMPED RATHER THAN TRUSTED, for the reason GameSession.OwnsGuildPathNode is: the state
        /// is the server's last answer, the tree is this build's content, and a rung the build does
        /// not open must grant nothing rather than grant something nobody can see the price of. A
        /// guild that drops a level keeps its rungs, because 0012 never lowers a level it granted.
        /// </summary>
        public static int RungsOn(GuildTechConfig config, GuildTechState state, string familyId)
        {
            if (config == null || state == null || string.IsNullOrEmpty(familyId)) return 0;
            if (config.Family(familyId) == null) return 0;
            int bought = state.RungsIn(familyId);
            int allowed = 0;
            for (int r = 1; r <= config.RungsPerFamily; r++)
            {
                int level = config.LevelForRung(familyId, r);
                if (level > 0 && level <= state.Level) allowed = r;
            }
            return Math.Max(0, Math.Min(bought, allowed));
        }

        /// <summary>
        /// The guild talent a hero of this role carries, or null. Null unless the family's slot is
        /// open at the guild's level AND the guild has picked one AND this build's content still
        /// names it - the same three-way tolerance a hero's selected talent gets.
        /// </summary>
        public static PassiveDef TalentFor(GuildTechConfig config, GuildTechState state, HeroRole role)
        {
            if (config == null || state == null) return null;
            GuildTechFamilyDef family = config.FamilyOfRole(role);
            if (family == null) return null;
            int slot = config.LevelForTalentSlot(family.Id);
            if (slot <= 0 || state.Level < slot) return null;
            return family.Talent(state.TalentIn(family.Id));
        }

        /// <summary>
        /// What one hero's rungs are worth, as a share of its own power score, or zero.
        ///
        /// ONE NUMBER FOR EVERY FAMILY, which is the point: a rung costs the same Waymarks whatever
        /// track it sits on, so it has to buy the same thing. What differs between families is
        /// WHICH stats carry that worth, never how much of it there is.
        /// </summary>
        public static float PowerShareFor(GuildTechConfig config, GuildTechState state, HeroRole role)
        {
            if (config == null) return 0f;
            GuildTechFamilyDef family = config.FamilyOfRole(role);
            if (family == null) return 0f;
            return RungsOn(config, state, family.Id) * family.RungScale;
        }

        /// <summary>
        /// Folds a hero's guild tech rungs into an already-built UnitStats, in place. StatMath's
        /// ApplyRelic and ApplyArmor are the siblings this is modelled on: same in-place signature,
        /// same "an absent argument is an exact no-op" contract, and it runs LAST of the three so
        /// the order of the whole stack is fixed and reproducible.
        ///
        /// THE SPLIT IS EQUAL BY POWER, not by stat. A rung is worth `share` of the hero's power
        /// score; each stat the family names carries an equal part of that, converted through the
        /// same weights StatMath.PowerScore uses. So Line's rung on Max HP and Defense and Blade's
        /// on Attack and Crit Damage are worth exactly the same, and neither family's second stat
        /// is a rounding error the way a flat percentage of a ratio stat would be.
        ///
        /// NOT SEED-SAFE, and for ApplyArmor's reason rather than a new one: a rung on Crit Chance
        /// moves a DeterministicRng.Chance draw. That is what it means for guild tech to be real
        /// power (G5 retires the rule that it changes no fight), and it is why the guild dungeon
        /// keeps handing CombatSimulation an empty profile - see GuildTechConfig's class comment.
        /// </summary>
        public static void ApplyRungs(UnitStats stats, GuildTechConfig config, GuildTechState state, HeroRole role)
        {
            if (stats == null || config == null) return;
            GuildTechFamilyDef family = config.FamilyOfRole(role);
            if (family == null || family.RungStats.Count == 0) return;
            int rungs = RungsOn(config, state, family.Id);
            if (rungs <= 0 || family.RungScale <= 0f) return;
            ApplyShare(stats, family, rungs * family.RungScale);
        }

        /// <summary>
        /// ApplyRungs with the share named by the caller rather than read off a state. The harness
        /// solves the rung scale with this, and the tree screen previews a rung with it, so neither
        /// has to reimplement the split and get it subtly different.
        /// </summary>
        public static void ApplyShare(UnitStats stats, GuildTechFamilyDef family, float share)
        {
            if (stats == null || family == null || family.RungStats.Count == 0 || share <= 0f) return;
            int power = StatMath.PowerScore(stats);
            if (power <= 0) return;
            float each = power * share / family.RungStats.Count;
            // StatMath owns the conversion, because StatMath owns the weights it inverts.
            for (int i = 0; i < family.RungStats.Count; i++) StatMath.AddPower(stats, family.RungStats[i], each);
        }
    }
}
