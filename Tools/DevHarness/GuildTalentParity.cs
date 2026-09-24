using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Emberfall.Core.Data;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// EVERY GUILD TALENT, READ BY THE INSTRUMENT THAT READS HERO TALENTS (G8).
    ///
    /// The brief for the tree was explicit that this must not be a new instrument: the parity study
    /// already grades a hero talent at 100% of recommended power against a 1%-of-Attack floor, and
    /// a guild talent is a PassiveDef, so the study can be pointed at it unchanged. Everything here
    /// is a caller of ParityStudy.cs - ParDuel, ParBuildLadder, ParLadderArm, ParCompare, ParClass
    /// and ParMaterial do the work, with the same t >= 2 rule and the same ParMaterialAtkPct floor.
    ///
    /// HOW A GUILD TALENT IS INJECTED. A guild talent applies to every hero of its family the
    /// player owns, but the instrument is per-subject: it puts one hero in a pinned line and reads
    /// that line's margin, that fight's result and that hero's own output. So the talent is
    /// measured the way a hero talent is - clone the subject's def, append the guild talent as one
    /// more talent slot, select it - and the reading is per hero carrying it. ParPlacebo already
    /// does exactly this with an empty passive, and this is the same trick with a real one.
    ///
    /// Read that way the number is directly comparable to every hero talent in the game and to the
    /// material floor, which is the comparison G8 asks for. It is also a LOWER BOUND on what a
    /// guild talent is worth in play, because in play it is on every family member in the line at
    /// once; the study says so rather than quietly claiming the per-subject figure is the whole
    /// effect.
    ///
    /// SUBJECTS ARE SAMPLED, not exhaustive. A family has up to 28 heroes and the gate runs on every
    /// build-check; the sample is deterministic (sorted by id, evenly spaced) so the reading is
    /// reproducible, and it spans the family's roles rather than taking the first few alphabetically.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>
        /// Heroes per family the talents are read on. Deterministic and evenly spaced through the
        /// family's roles.
        ///
        /// THE GATE AND THE STUDY READ THE SAME HEROES. The gate used to read three and the study
        /// six, and the two then disagreed about the content in front of them: at three subjects
        /// Grace got one Mender, that Mender's kit does not heal in the pinned line, and Steady
        /// Hands - a clause that fires on a heal - read INERT while the study read it +13.0% duel
        /// and +29.3% ladder on the same file. A gate that fails what its own study passes is not a
        /// stricter gate, it is a different question. The sample sizes below the subject count
        /// still differ, because those buy precision rather than deciding which heroes are looked
        /// at, and a wider interval only ever makes this gate more forgiving.
        /// </summary>
        private const int GtSubjectsPerFamily = 6;

        private static readonly FieldInfo _gtTalentsField = typeof(HeroDef).GetField("Talents");

        private sealed class GtTalentRow
        {
            public string Family = "", Id = "", Name = "", Description = "";
            public ParDelta Duel, Ladder;
            public string ByMargin = "flat", ByWin = "flat", Material = "flat";
            public double EqDuel, EqLadder;
            public int Changed, N;
            public double Best { get { return Math.Max(Math.Abs(EqDuel), Math.Abs(EqLadder)); } }
        }

        /// <summary>
        /// The subject's def with one guild talent appended as an extra slot, and the 1-based index
        /// that selects it. ParPlacebo's trick; see this file's header for why it is the right one.
        /// </summary>
        private static HeroDef GtWithGuildTalent(HeroDef d, PassiveDef guildTalent, out int slot)
        {
            HeroDef c = TempoClone(d);
            List<PassiveDef> t = new List<PassiveDef>(d.Talents);
            t.Add(guildTalent);
            _gtTalentsField.SetValue(c, t);
            slot = t.Count;
            return c;
        }

        /// <summary>
        /// The family's subjects: every role it holds is represented before any role is
        /// represented twice, and within a role the heroes are taken evenly spaced through the
        /// id-sorted list. Deterministic, so two runs read the same talents on the same heroes.
        /// </summary>
        private static List<HeroDef> GtSubjects(ContentDatabase db, GuildTechFamilyDef family, int want)
        {
            List<HeroDef> pool = new List<HeroDef>();
            for (int i = 0; i < db.Heroes.Count; i++)
                if (family.Roles.Contains(db.Heroes[i].Role)) pool.Add(db.Heroes[i]);
            pool.Sort(delegate (HeroDef a, HeroDef b) { return string.CompareOrdinal(a.Id, b.Id); });

            List<HeroDef> picked = new List<HeroDef>();
            int round = 0;
            while (picked.Count < want && round < 16)
            {
                for (int r = 0; r < family.Roles.Count && picked.Count < want; r++)
                {
                    List<HeroDef> ofRole = pool.FindAll(h => h.Role == family.Roles[r]);
                    if (ofRole.Count == 0) continue;
                    int step = Math.Max(1, ofRole.Count / Math.Max(1, want / Math.Max(1, family.Roles.Count) + 1));
                    int index = (round * step) % ofRole.Count;
                    if (!picked.Contains(ofRole[index])) picked.Add(ofRole[index]);
                }
                round++;
            }
            return picked;
        }

        /// <summary>
        /// Reads every guild talent and, in the gate, fails the build for one that cannot be felt.
        ///
        /// THE GATE'S RULE IS THE STUDY'S RULE: a talent is kept when some parity reading classes
        /// it non-flat AND that reading is worth at least ParMaterialAtkPct of Attack in the same
        /// direction - ParMaterial, unchanged. "A guild talent that cannot be felt is the failure
        /// the four-node Path already made once" (G8), and twelve of them would repeat it at scale.
        /// </summary>
        private static void GuildTalentParity(ContentDatabase db, bool study)
        {
            GuildTechConfig tree = db.GuildTech;
            int talents = 0;
            for (int f = 0; f < tree.Families.Count; f++) talents += tree.Families[f].Talents.Count;
            if (talents == 0)
            {
                Check(false, "G8 - guild-tech.json authors no guild talent at all; the tree is rungs and nothing to remember");
                return;
            }

            int subjects = GtSubjectsPerFamily;
            int nDuel = study ? 500 * Math.Max(1, _parScale) : 240;
            int perStage = study ? 15 * Math.Max(1, _parScale) : 3;

            if (study)
            {
                Line("  EVERY GUILD TALENT AT PARITY (G8) - the instrument that grades hero talents, pointed at these");
                Line("    duel   : the pinned line with the subject in its role's place against the SAME line with no guild talent,");
                Line("             level 40, n=" + nDuel + " per arm. Matched power by construction");
                Line("    ladder : that line at each stage's parity power against its CEIL, " + perStage + " seeds a stage");
                Line("    kept   : non-flat by the t >= 2 rule AND worth >= " + F(ParMaterialAtkPct, "0") + "% Attack in the same direction");
                Line("    " + subjects + " subject heroes per family, spanning its roles; the reading is PER HERO CARRYING IT, so it is a");
                Line("    lower bound on play, where every family member in the line carries it at once");
                Line("");
            }
            if (_parLadderPowers == null) ParCalibrateLadder(db);

            List<GtTalentRow> rows = new List<GtTalentRow>();
            double yardDuel = 0, yardLadder = 0;
            int yardN = 0;

            for (int f = 0; f < tree.Families.Count; f++)
            {
                GuildTechFamilyDef family = tree.Families[f];
                List<HeroDef> pool = GtSubjects(db, family, subjects);
                if (pool.Count == 0) continue;

                // The yardstick, per family: Attack x1.05 on each subject, in both arenas. Worth in
                // "% Attack" is the reading over this, times five - ParTalents' own conversion.
                for (int s = 0; s < pool.Count; s++)
                {
                    HeroDef subject = pool[s];
                    string how;
                    string[] line = NormalPlayLineFor(db, PinnedLine, subject, out how);
                    int seed = ParSeedBase(subject.Id);
                    ParLadder ladder = ParBuildLadder(db, line, 0f);
                    ParObs[] duelC = ParDuel(db, line, subject.Id, null, 0, line, 40, nDuel, seed);
                    ParObs[] ladC = ParLadderArm(db, ladder, subject.Id, null, 0, perStage);
                    HeroDef atk = ParStat(subject, 1.05f, 1f, 1f, 1f);
                    yardDuel += ParCompare(duelC, ParDuel(db, line, subject.Id, atk, 0, line, 40, nDuel, seed)).Mm;
                    yardLadder += ParCompare(ladC, ParLadderArm(db, ladder, subject.Id, atk, 0, perStage)).Mm;
                    yardN++;
                }
            }
            double cDuel = yardN > 0 ? yardDuel / yardN : 0, cLad = yardN > 0 ? yardLadder / yardN : 0;

            for (int f = 0; f < tree.Families.Count; f++)
            {
                GuildTechFamilyDef family = tree.Families[f];
                List<HeroDef> pool = GtSubjects(db, family, subjects);
                if (pool.Count == 0) continue;

                for (int t = 0; t < family.Talents.Count; t++)
                {
                    PassiveDef talent = family.Talents[t];
                    GtTalentRow row = new GtTalentRow();
                    row.Family = family.Name;
                    row.Id = talent.Id;
                    row.Name = talent.Name;
                    row.Description = talent.Description;

                    // Pooled over the family's subjects: one talent, read on every hero sampled,
                    // and the arms concatenated so the t statistic is over all of them.
                    List<ParObs[]> dC = new List<ParObs[]>(), dT = new List<ParObs[]>();
                    List<ParObs[]> lC = new List<ParObs[]>(), lT = new List<ParObs[]>();
                    for (int s = 0; s < pool.Count; s++)
                    {
                        HeroDef subject = pool[s];
                        string how;
                        string[] line = NormalPlayLineFor(db, PinnedLine, subject, out how);
                        int seed = ParSeedBase(subject.Id);
                        int slot;
                        HeroDef withIt = GtWithGuildTalent(subject, talent, out slot);
                        ParLadder ladder = ParBuildLadder(db, line, 0f);

                        dC.Add(ParDuel(db, line, subject.Id, null, 0, line, 40, nDuel, seed));
                        dT.Add(ParDuel(db, line, subject.Id, withIt, slot, line, 40, nDuel, seed));
                        lC.Add(ParLadderArm(db, ladder, subject.Id, null, 0, perStage));
                        lT.Add(ParLadderArm(db, ladder, subject.Id, withIt, slot, perStage));
                    }
                    row.Duel = ParCompare(Concat(dC), Concat(dT));
                    row.Ladder = ParCompare(Concat(lC), Concat(lT));
                    row.Changed = row.Duel.Changed + row.Ladder.Changed;
                    row.N = row.Duel.N + row.Ladder.N;
                    row.ByMargin = ParClass(new List<double> { row.Duel.Tm, row.Ladder.Tm },
                                            new List<double> { row.Duel.Mm, row.Ladder.Mm });
                    row.ByWin = ParClass(new List<double> { row.Duel.Tw, row.Ladder.Tw },
                                         new List<double> { row.Duel.Wm, row.Ladder.Wm });
                    row.EqDuel = cDuel != 0 ? row.Duel.Mm / cDuel * 5 : 0;
                    row.EqLadder = cLad != 0 ? row.Ladder.Mm / cLad * 5 : 0;
                    row.Material = ParMaterial(row.ByMargin, row.Duel.Tm, row.EqDuel, row.Ladder.Tm, row.EqLadder);
                    rows.Add(row);

                    if (study)
                    {
                        Line("  " + family.Name + " / " + row.Name + "   [" + row.Id + "]");
                        Line("      " + row.Description);
                        Line("      duel   " + ParShow(row.Duel));
                        Line("      ladder " + ParShow(row.Ladder));
                        Line("      class " + row.ByMargin.ToUpperInvariant() + " by margin, " + row.ByWin.ToUpperInvariant()
                             + " by win rate; worth " + GtEq(row.EqDuel) + " / " + GtEq(row.EqLadder) + " Attack; material "
                             + row.Material.ToUpperInvariant());
                    }
                }
            }

            // ---- the split, the way the parity study reports one
            int gain = 0, flat = 0, lose = 0, material = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].ByMargin == "gain") gain++; else if (rows[i].ByMargin == "lose") lose++; else flat++;
                if (rows[i].Material != "flat") material++;
            }
            Line("  guild talents at parity, by margin: " + gain + " gain / " + flat + " flat / " + lose + " lose;  "
                 + material + " of " + rows.Count + " clear the " + F(ParMaterialAtkPct, "0") + "% Attack material floor");

            List<string> dead = new List<string>();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Material == "flat") dead.Add(rows[i].Family + "/" + rows[i].Name + " (" + GtEq(rows[i].Best) + ")");
            Check(dead.Count == 0,
                  "G8 - EVERY GUILD TALENT CAN BE FELT: all " + rows.Count + " clear the " + F(ParMaterialAtkPct, "0")
                  + "% Attack floor the hero talents are graded against"
                  + (dead.Count > 0 ? " - these cannot: " + string.Join(", ", dead.ToArray()) : ""));

            // A talent that changes no fight at all is a different failure from one that changes
            // fights and is worth nothing, and it is worth naming separately.
            List<string> inert = new List<string>();
            for (int i = 0; i < rows.Count; i++) if (rows[i].Changed == 0) inert.Add(rows[i].Family + "/" + rows[i].Name);
            Check(inert.Count == 0,
                  "every guild talent changes a fight in at least one arena"
                  + (inert.Count > 0 ? " - INERT: " + string.Join(", ", inert.ToArray()) : ""));

            // G4: two guilds at maximum tech should field noticeably different sides. Three options
            // that measure the same are one option with three names.
            List<string> twins = new List<string>();
            for (int f = 0; f < tree.Families.Count; f++)
            {
                List<GtTalentRow> mine = rows.FindAll(r => r.Family == tree.Families[f].Name);
                for (int a = 0; a < mine.Count; a++)
                    for (int b = a + 1; b < mine.Count; b++)
                    {
                        double sep = Math.Abs(mine[a].EqDuel - mine[b].EqDuel) + Math.Abs(mine[a].EqLadder - mine[b].EqLadder);
                        if (sep < 0.5 && mine[a].Duel.Changed > 0 && mine[b].Duel.Changed > 0
                            && GtSameShape(tree.Families[f], mine[a].Id, mine[b].Id))
                            twins.Add(mine[a].Name + " / " + mine[b].Name);
                    }
            }
            Check(twins.Count == 0,
                  "G4 - no family offers two options that are the same clause twice: a guild choosing between near-identical "
                  + "talents has not been given a choice"
                  + (twins.Count > 0 ? " - " + string.Join(", ", twins.ToArray()) : ""));

            // THE OTHER WAY A CHOICE CAN BE FAKE - three options where one is plainly the best -
            // IS NOT ASKED HERE ANY MORE, and the rule that used to ask it is gone.
            //
            // It capped a family's strongest option at three times its weakest on this ladder. It
            // passed every family while Line read Dig In +39.1, Hold the Wall +25.0 and Answer in
            // Kind +17.6 - inside x3, and Dig In still the pick every time. A ratio cannot tell a
            // real choice from a dominated one, because it is a reading of SIZE and the question is
            // about WHEN. One arena has only one answer to give.
            //
            // GuildTalentThemes.cs (G10) asks it in the dimension that can answer: every talent
            // across every floor theme and condition, in the guild dungeon, read by the dungeon's
            // own board order. A family passes when at least two of its talents are the best pick
            // on some theme. See that file's header for why the bar it uses is derived rather than
            // authored.

            if (study)
            {
                Line("");
                Line("  EVERY GUILD TALENT'S WORTH IN % ATTACK (duel | ladder), class, material");
                for (int i = 0; i < rows.Count; i++)
                {
                    GtTalentRow r = rows[i];
                    Line("    " + (r.Family + "/" + r.Name).PadRight(34) + GtEq(r.EqDuel).PadLeft(8) + " t"
                         + F(r.Duel.Tm, "0.0").PadRight(7) + " | " + GtEq(r.EqLadder).PadLeft(8) + " t"
                         + F(r.Ladder.Tm, "0.0").PadRight(7) + "  " + r.ByMargin + ", material " + r.Material
                         + "   dWR duel " + Pct((float)r.Duel.Wm) + " ladder " + Pct((float)r.Ladder.Wm));
                }
                Line("");
            }
        }

        /// <summary>
        /// Whether two talents on one family are the same CLAUSE - same trigger and the same kinds
        /// of effect in the same order. Two options can legitimately measure alike while doing
        /// different things (a guild picks for how a fight feels as well as for what it is worth),
        /// so the near-identical check above only fires when the shapes match too.
        /// </summary>
        private static bool GtSameShape(GuildTechFamilyDef family, string a, string b)
        {
            PassiveDef x = family.Talent(a), y = family.Talent(b);
            if (x == null || y == null) return false;
            if (x.Trigger != y.Trigger) return false;
            if (x.Effects.Count != y.Effects.Count) return false;
            for (int i = 0; i < x.Effects.Count; i++)
                if (x.Effects[i].Kind != y.Effects[i].Kind) return false;
            return x.Appends.Count == y.Appends.Count && x.Overrides.Count == y.Overrides.Count;
        }

        private static string GtEq(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.0", CultureInfo.InvariantCulture) + "%"; }
    }
}
