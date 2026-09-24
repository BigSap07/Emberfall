using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// The six stats a guild tech rung may move. DELIBERATELY EXACTLY StatMath.PowerScore'S SIX,
    /// and that is the whole constraint: a rung is power now (DESIGN-GUILD-TECH retires the rule
    /// that guild progression changes no fight), so the game's own measure of power has to be able
    /// to see it. A rung on Effect Hit would be a stat bonus the power score reads as nothing,
    /// which is how a system starts lying about how strong a player is.
    /// </summary>
    public enum GuildTechStat
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage
    }

    /// <summary>
    /// One family track: which roles belong to it, what its rungs move, and the three guild
    /// talents its slot chooses between (DESIGN-GUILD-TECH, "The shape").
    /// </summary>
    public sealed class GuildTechFamilyDef
    {
        public string Id = "";
        public string Name = "";

        /// <summary>
        /// The roles in this family. G1: A HERO'S FAMILY IS DECIDED BY ITS ROLE AND BY NOTHING
        /// ELSE - not aspect, not star tier, not who is in the line. Every role belongs to exactly
        /// one family and every family has at least one role; GuildTechChecks fails the build
        /// otherwise, because a role in two families would make "every hero of the family"
        /// ambiguous and a role in none would make a hero permanently untouchable by the tree.
        /// </summary>
        public readonly List<HeroRole> Roles = new List<HeroRole>();

        /// <summary>
        /// Which stats a rung on this track moves, its output profile written as numbers. AUTHORED,
        /// because which stats a family cares about is what the family IS. The MAGNITUDE is not
        /// authored - see RungScale.
        /// </summary>
        public readonly List<GuildTechStat> RungStats = new List<GuildTechStat>();

        /// <summary>
        /// DERIVED, never authored: the share of its own base value each named stat gains per rung
        /// bought. Solved by GuildTechChecks so that one rung on this family is worth the same
        /// power as one rung on every other, whatever stats it happens to move - a rung on Max HP
        /// and Defense and a rung on Attack and Crit Damage cost the same Waymarks, so they must
        /// buy the same thing. The gate re-solves it and fails when the file disagrees.
        /// </summary>
        public float RungScale;

        /// <summary>
        /// The three guild talents this family's slot chooses between. A guild talent IS a
        /// PassiveDef (G3): the same triggers, the same effects, the same conditions, overrides and
        /// appends a hero talent is authored in. It is not a new engine and there is no guild-talent
        /// branch anywhere in CombatSimulation - the resolved talent is appended to the unit exactly
        /// where a hero's selected talent is.
        /// </summary>
        public readonly List<PassiveDef> Talents = new List<PassiveDef>();

        public PassiveDef Talent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Talents.Count; i++) if (Talents[i].Id == id) return Talents[i];
            return null;
        }
    }

    /// <summary>
    /// What one guild level opens. DERIVED FROM THE TREE'S SHAPE rather than authored as a table:
    /// see GuildTechConfig.BuildUnlocks for the rule and why there is no table in the file.
    /// </summary>
    public sealed class GuildTechUnlock
    {
        /// <summary>1-based guild level this opens at.</summary>
        public int Level;
        /// <summary>The family this step belongs to.</summary>
        public string FamilyId = "";
        /// <summary>1..RungsPerFamily for a rung step, 0 for the talent slot.</summary>
        public int Rung;
        /// <summary>True when this level opens the family's guild talent slot.</summary>
        public bool TalentSlot { get { return Rung == 0 && FamilyId.Length > 0; } }
        /// <summary>A guild-path.json node id this level grants outright (G9), or empty.</summary>
        public string GrantsNode = "";
    }

    /// <summary>
    /// guild-tech.json: the guild tech tree that replaced the four-node Guild Path
    /// (claude/DESIGN-GUILD-TECH.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason and one more
    /// that had to be checked rather than assumed. The version exists to retire a GUILD DUNGEON
    /// replay when a number that fight reads moves - and a guild dungeon fight cannot read any
    /// number in this file. GuildDungeon.FightUnder hands CombatSimulation a fresh empty
    /// PlayerProfile, and guild tech is resolved from the profile exactly as armor is, so the
    /// dungeon strips it at the door along with the relics and the armor and the levels. The gate
    /// proves that rather than trusting it: a dungeon floor fought at maximum guild tech is
    /// identical, event for event, to the same floor fought with none.
    ///
    /// Every number here except the rung stat shapes is DERIVED. `DevHarness.exe --guild-tech`
    /// prints the working; GuildTechChecks re-derives on every build-check and fails when the file
    /// and the derivation disagree, the same way CheckDerivation holds guild-path.json's pay.
    /// </summary>
    public sealed class GuildTechConfig
    {
        /// <summary>What the tree is bought with. The guild dungeon's currency, unchanged.</summary>
        public string Currency = "Waymarks";

        public readonly List<GuildTechFamilyDef> Families = new List<GuildTechFamilyDef>();

        /// <summary>Rungs on every track. Three, per DESIGN-GUILD-TECH.</summary>
        public int RungsPerFamily = 3;

        /// <summary>
        /// DERIVED: the Waymarks one step of rung costs. Rung r on any family costs r x this, so a
        /// track's three rungs cost 1 + 2 + 3 = 6 units and the whole tree costs 24. Solved from
        /// the mean member cycle and how long the guild level ceiling takes to reach, so a member
        /// finishes paying for the tree at about the moment their guild finishes opening it.
        /// </summary>
        public int RungUnit;

        /// <summary>DERIVED from the tree's shape: families x (rungs + one talent slot).</summary>
        public int LevelCeiling;

        /// <summary>
        /// DERIVED: guild XP one level costs, before the member-count scaling. The requirement for
        /// EVERY level is the same, deliberately - a level a month reads, and a second invented
        /// curve on top of the square root would be one more thing nobody can predict.
        /// </summary>
        public int XpPerLevel;

        /// <summary>
        /// G7, AUTHORED BY THE DESIGN AND BY NOTHING ELSE: XP required per level scales with the
        /// square root of member count. The per-person advantage of thirty over two is not authored
        /// anywhere - it is what this exponent produces, and the gate asserts the measured ratio
        /// against sqrt(30/2) rather than against a target somebody typed.
        /// </summary>
        public float XpExponent = 0.5f;

        /// <summary>DERIVED, in level order. See BuildUnlocks.</summary>
        public readonly List<GuildTechUnlock> Unlocks = new List<GuildTechUnlock>();

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable { get { return Families.Count > 0 && LevelCeiling > 0; } }

        public GuildTechFamilyDef Family(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Families.Count; i++) if (Families[i].Id == id) return Families[i];
            return null;
        }

        /// <summary>
        /// G1: the family a hero belongs to, from its role and nothing else. Null for a role no
        /// family claims, which GuildTechChecks refuses to let ship.
        /// </summary>
        public GuildTechFamilyDef FamilyOfRole(HeroRole role)
        {
            for (int i = 0; i < Families.Count; i++)
                if (Families[i].Roles.Contains(role)) return Families[i];
            return null;
        }

        /// <summary>What rung r on any track costs: the price rule, in one place.</summary>
        public int RungCost(int rung)
        {
            return rung < 1 || rung > RungsPerFamily ? 0 : rung * RungUnit;
        }

        /// <summary>The whole tree's rung bill, what a member pays to finish every track.</summary>
        public int WholeTreeCost
        {
            get
            {
                int total = 0;
                for (int f = 0; f < Families.Count; f++)
                    for (int r = 1; r <= RungsPerFamily; r++) total += RungCost(r);
                return total;
            }
        }

        /// <summary>
        /// The guild level at which a family's rung r may first be bought, or 0 when the tree does
        /// not open it at all.
        /// </summary>
        public int LevelForRung(string familyId, int rung)
        {
            for (int i = 0; i < Unlocks.Count; i++)
                if (Unlocks[i].FamilyId == familyId && Unlocks[i].Rung == rung && rung >= 1) return Unlocks[i].Level;
            return 0;
        }

        /// <summary>The guild level at which a family's talent slot opens, or 0.</summary>
        public int LevelForTalentSlot(string familyId)
        {
            for (int i = 0; i < Unlocks.Count; i++)
                if (Unlocks[i].FamilyId == familyId && Unlocks[i].TalentSlot) return Unlocks[i].Level;
            return 0;
        }

        /// <summary>
        /// The guild XP one more level costs a guild of this many members. G7's square root, and
        /// the only place it is written: the client predicts with this and 0012 charges with the
        /// same expression, and GuildTechChecks fails the build when the two disagree.
        ///
        /// A guild of nought members is read as one, so the expression has no hole in it.
        /// </summary>
        public long XpForLevel(int members)
        {
            double m = Math.Max(1, members);
            return (long)Math.Round(XpPerLevel * Math.Pow(m, XpExponent), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The level a guild's accumulated XP has reached, capped at the ceiling. The SERVER holds
        /// the authority and never lowers a level it has granted (0012 keeps a high-water mark), so
        /// a guild that loses members keeps what it opened; this is what the client predicts with.
        /// </summary>
        public int LevelForXp(long xp, int members)
        {
            long per = Math.Max(1, XpForLevel(members));
            long level = xp / per;
            if (level < 0) level = 0;
            return (int)Math.Min(level, LevelCeiling);
        }

        public static GuildTechConfig FromJson(string text)
        {
            GuildTechConfig c = new GuildTechConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("guild-tech.json: " + err); return c; }

            c.Currency = root.GetString("currency", c.Currency);
            c.RungsPerFamily = Math.Max(1, root.GetInt("rungsPerFamily", c.RungsPerFamily));
            c.RungUnit = Math.Max(0, root.GetInt("rungUnit", 0));

            JsonValue level = root["level"];
            c.XpPerLevel = Math.Max(0, level.GetInt("xpPerLevel", 0));
            c.XpExponent = level.GetFloat("exponent", c.XpExponent);

            List<JsonValue> families = root.GetList("families");
            for (int i = 0; i < families.Count; i++)
            {
                JsonValue f = families[i];
                GuildTechFamilyDef d = new GuildTechFamilyDef();
                d.Id = f.GetString("id", "");
                d.Name = f.GetString("name", d.Id);
                d.RungScale = f.GetFloat("rungScale", 0f);
                if (d.Id.Length == 0 || c.Family(d.Id) != null)
                {
                    c.Warnings.Add("guild-tech.json: a family has no id or repeats one (" + d.Id + ")");
                    continue;
                }

                List<JsonValue> roles = f.GetList("roles");
                for (int r = 0; r < roles.Count; r++)
                {
                    string name = roles[r].AsString("");
                    HeroRole role = EnumUtil.Parse(name, HeroRole.Striker);
                    if (!string.Equals(role.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        c.Warnings.Add("guild-tech.json: " + d.Id + " names an unknown role '" + name + "'");
                        continue;
                    }
                    if (!d.Roles.Contains(role)) d.Roles.Add(role);
                }

                List<JsonValue> stats = f.GetList("rungStats");
                for (int s = 0; s < stats.Count; s++)
                {
                    string name = stats[s].AsString("");
                    GuildTechStat stat = EnumUtil.Parse(name, GuildTechStat.Attack);
                    if (!string.Equals(stat.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        c.Warnings.Add("guild-tech.json: " + d.Id + " names an unknown rung stat '" + name + "'");
                        continue;
                    }
                    if (!d.RungStats.Contains(stat)) d.RungStats.Add(stat);
                }

                List<JsonValue> talents = f.GetList("talents");
                for (int t = 0; t < talents.Count; t++)
                {
                    PassiveDef talent = PassiveDef.FromJson(talents[t]);
                    if (talent == null) continue;
                    if (string.IsNullOrEmpty(talent.Id) || d.Talent(talent.Id) != null)
                    {
                        c.Warnings.Add("guild-tech.json: a talent on " + d.Id + " has no id or repeats one");
                        continue;
                    }
                    d.Talents.Add(talent);
                }

                if (d.Roles.Count == 0) { c.Warnings.Add("guild-tech.json: family " + d.Id + " claims no role, so no hero could ever belong to it"); continue; }
                if (d.RungStats.Count == 0) { c.Warnings.Add("guild-tech.json: family " + d.Id + " names no rung stat"); continue; }
                c.Families.Add(d);
            }

            c.BuildUnlocks(root);
            return c;
        }

        /// <summary>
        /// THE UNLOCK ORDER IS A RULE, NOT A TABLE, so a family cannot be quietly favoured by the
        /// order somebody typed. Passes over the families in file order:
        ///
        ///   rung 1 of every family, then every family's TALENT SLOT, then rung 2, then rung 3.
        ///
        /// Round-robin rather than family by family, because family-by-family would mean a player
        /// whose roster is all Reach waits out three other families before the tree gives them
        /// anything - and G1 already charges a narrow roster once, which is enough.
        ///
        /// The talent slot lands SECOND, not last, because G2 says the talent is the thing a guild
        /// remembers buying and the rungs are the floor around it. Last, the interesting half of
        /// the tree would be invisible for most of a guild's life.
        ///
        /// The ceiling is what the rule produces: families x (rungs + 1). Nothing authors it.
        ///
        /// The only authored part is `grants`, guild-path.json node ids that early levels hand over
        /// outright (G9): the saved line and the line that keeps its talents are built, work, and
        /// are a reasonable level 1 and 2 reward, so they move here rather than being deleted.
        /// </summary>
        private void BuildUnlocks(JsonValue root)
        {
            Unlocks.Clear();
            int level = 0;
            List<int> order = new List<int>();
            order.Add(1);                                            // rung 1
            order.Add(0);                                            // the talent slot
            for (int r = 2; r <= RungsPerFamily; r++) order.Add(r);  // then the remaining rungs

            for (int step = 0; step < order.Count; step++)
            {
                for (int f = 0; f < Families.Count; f++)
                {
                    GuildTechUnlock u = new GuildTechUnlock();
                    u.Level = ++level;
                    u.FamilyId = Families[f].Id;
                    u.Rung = order[step];
                    Unlocks.Add(u);
                }
            }
            LevelCeiling = level;

            List<JsonValue> grants = root.GetList("grants");
            for (int i = 0; i < grants.Count; i++)
            {
                string node = grants[i].GetString("node", "");
                int at = grants[i].GetInt("level", 0);
                if (node.Length == 0 || at < 1 || at > LevelCeiling)
                {
                    Warnings.Add("guild-tech.json: a grant names no node or a level outside 1.." + LevelCeiling);
                    continue;
                }
                for (int u = 0; u < Unlocks.Count; u++)
                    if (Unlocks[u].Level == at) Unlocks[u].GrantsNode = node;
            }
        }

        /// <summary>Every guild-path.json node id this tree hands over at or below a guild level
        /// (G9). Empty at level 0, which is every guild that has not started.</summary>
        public List<string> NodesGrantedAt(int guildLevel)
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < Unlocks.Count; i++)
                if (Unlocks[i].Level <= guildLevel && Unlocks[i].GrantsNode.Length > 0
                    && !ids.Contains(Unlocks[i].GrantsNode)) ids.Add(Unlocks[i].GrantsNode);
            return ids;
        }
    }
}
