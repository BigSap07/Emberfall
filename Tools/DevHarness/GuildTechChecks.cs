using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE GUILD TECH TREE'S HARNESS (claude/DESIGN-GUILD-TECH.md): the derivation every number in
    /// guild-tech.json comes out of, printed by `DevHarness.exe --guild-tech`, and the gate that
    /// re-derives it on every build-check and fails when the file has drifted.
    ///
    /// A PART OF Program, not a class of its own like GuildPathChecks, for one reason: the guild
    /// talents are graded by the instrument that grades hero talents, and that instrument
    /// (ParityStudy.cs - ParDuel, ParLadderArm, ParCompare, ParClass, ParMaterial) is a private part
    /// of Program. Pointing the existing instrument at the new talents was the brief; reimplementing
    /// it next door would have been a second instrument that slowly disagreed with the first.
    ///
    /// WHAT IS CHOSEN AND WHAT IS MEASURED, stated up front the way GuildPathChecks states it.
    /// Three numbers are design inputs, each one named below with the sentence it implements.
    /// Everything else - the rung's size, the rung's price, the XP a level costs, the ceiling, the
    /// rate a guild of thirty levels against a guild of two - is read off the content or falls out
    /// of the tree's own shape.
    /// </summary>
    internal static partial class Program
    {
        // ---------------------------------------------------------------- the design inputs

        /// <summary>
        /// G2: "Stat rungs are the floor and guild talents are the reward... They are meant to be
        /// unremarkable." A WHOLE TRACK - all three rungs on one family - is worth exactly ONE HERO
        /// LEVEL to a hero of that family.
        ///
        /// A level rather than a percentage, because a level is the game's own smallest unit of
        /// progression and it is already measured: StatMath decides what one costs and what one
        /// buys, so this input names a thing that exists instead of inventing a number. It also
        /// sizes the tree against G5 without a second knob - one level is far inside every band the
        /// campaign is tuned to, so guild tech cannot walk a player out of the band the content was
        /// written for, and "accelerates a wall, never gates one" is a claim the walk can check
        /// rather than a hope.
        /// </summary>
        private const double GtTrackWorthInHeroLevels = 1.0;

        /// <summary>
        /// THE ONE AUTHORED PACE NUMBER, and it is authored because nothing in the game implies it:
        /// how long the long game is. A guild of GtReferenceGuildSize members opens the whole tree
        /// in this many cycles. Everything else about the curve - what a level costs, what a rung
        /// costs, how much faster a big guild climbs - is derived from it and from G7's exponent.
        ///
        /// Fifty-two cycles is a year of weekly dungeons. The retired four-node Path was eight, and
        /// being finished in two months with a currency that then bought nothing is the failure
        /// this tree exists to replace.
        /// </summary>
        private const int GtCyclesToCeiling = 52;

        /// <summary>The guild size the pace above is quoted at. Ten: inside the sizes the guild
        /// dungeon study already measures (1, 2, 5, 10, 20, 30) and near the middle of them.</summary>
        private const int GtReferenceGuildSize = 10;

        // ---------------------------------------------------------------- what is read, not chosen

        /// <summary>
        /// A member's mean cycle in Waymarks, and so the guild XP a member contributes in one.
        /// NOT A NEW NUMBER: it is guild-path.json's unit, the one every pay number there is solved
        /// backwards from, and GuildPathChecks already asserts that every guild size from two to
        /// thirty earns within ten percent of it. Guild XP is the Waymarks the guild's members
        /// earned that cycle, so the tree needs no second faucet and no second measurement.
        /// </summary>
        private const int GtMeanCycle = 100;

        /// <summary>A written number may sit this far from the derived one before the gate fails.
        /// GuildPathChecks' tolerance, for its reason: the derivation moves with content, and a
        /// price the server charges should not have to move with every edit. Past this, it should.</summary>
        private const double GtTolerance = 0.10;

        /// <summary>The level the rung's worth is measured at. balance.json's guild dungeon level:
        /// the one level in this game that already means "a hero, neutrally, with no history".</summary>
        private static int GtReferenceLevel(ContentDatabase db) { return Math.Max(1, db.Balance.GuildDungeonHeroLevel); }

        private sealed class GuildTechDerived
        {
            public double LevelPowerShare;    // what one hero level is worth, as a share of power
            public double LevelShareLow, LevelShareHigh;
            public double TrackShare;         // what a whole track is worth
            public double RungShareRaw;       // what one rung is worth
            public float RungScale;           // rounded as the file carries it
            public int Ceiling;
            public int TotalRungDepth;
            public double RungUnitRaw;
            public int RungUnit;
            public double XpPerLevelRaw;
            public int XpPerLevel;
            public int Heroes;
        }

        /// <summary>
        /// THE WHOLE DERIVATION, in one place and with no fights in it. Every line below is either
        /// read off the content, read off the tree's own shape, or one of the three inputs above.
        /// </summary>
        private static GuildTechDerived DeriveGuildTech(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            GuildTechDerived d = new GuildTechDerived();

            // 1. WHAT ONE HERO LEVEL IS WORTH, measured over every hero in the roster at the
            //    reference level. A share of the hero's own power, so a 5-star and a 1-star
            //    contribute the same kind of number rather than the big one drowning the small.
            int level = GtReferenceLevel(db);
            double total = 0;
            d.LevelShareLow = double.MaxValue;
            d.LevelShareHigh = double.MinValue;
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                HeroDef h = db.Heroes[i];
                int at = StatMath.PowerScore(StatMath.ForHero(h, level));
                int up = StatMath.PowerScore(StatMath.ForHero(h, level + 1));
                if (at <= 0) continue;
                double share = (up - at) / (double)at;
                total += share;
                if (share < d.LevelShareLow) d.LevelShareLow = share;
                if (share > d.LevelShareHigh) d.LevelShareHigh = share;
                d.Heroes++;
            }
            d.LevelPowerShare = d.Heroes > 0 ? total / d.Heroes : 0;

            // 2. THE TRACK AND THE RUNG. A track is worth GtTrackWorthInHeroLevels of that; the
            //    rungs on it split that worth evenly, because they cost 1, 2 and 3 units and a
            //    player buying the cheap one first should not be buying the good one first too.
            d.TrackShare = d.LevelPowerShare * GtTrackWorthInHeroLevels;
            d.RungShareRaw = d.TrackShare / Math.Max(1, tree.RungsPerFamily);
            d.RungScale = (float)Math.Round(d.RungShareRaw, 5);

            // 3. THE CEILING, from the tree's shape and nothing else: every family's rungs, plus
            //    every family's talent slot, one per level.
            d.Ceiling = Math.Max(1, tree.Families.Count * (tree.RungsPerFamily + 1));

            // 4. THE RUNG'S PRICE. A member finishes paying for the tree at about the moment their
            //    guild finishes opening it, so the two clocks are one clock. Rung r costs r units,
            //    so the whole tree is families x (1 + 2 + ... + rungs) units.
            d.TotalRungDepth = 0;
            for (int f = 0; f < tree.Families.Count; f++)
                for (int r = 1; r <= tree.RungsPerFamily; r++) d.TotalRungDepth += r;
            d.RungUnitRaw = d.TotalRungDepth > 0 ? (double)GtCyclesToCeiling * GtMeanCycle / d.TotalRungDepth : 0;
            d.RungUnit = (int)Math.Round(d.RungUnitRaw, MidpointRounding.AwayFromZero);

            // 5. WHAT A LEVEL COSTS IN GUILD XP. A guild of m members earns m x GtMeanCycle a cycle
            //    and pays XpPerLevel x m^exponent for a level, so it climbs at
            //    m^(1 - exponent) x GtMeanCycle / XpPerLevel levels a cycle. Set that so the
            //    reference size reaches the ceiling in the authored number of cycles:
            //
            //        XpPerLevel = GtMeanCycle x refSize^(1 - exponent) x cycles / ceiling
            //
            //    At G7's exponent of one half that is refSize's square root, which is the form the
            //    file carries and the form 0012 charges.
            double refRate = Math.Pow(GtReferenceGuildSize, 1.0 - tree.XpExponent);
            d.XpPerLevelRaw = d.Ceiling > 0 ? GtMeanCycle * refRate * GtCyclesToCeiling / d.Ceiling : 0;
            d.XpPerLevel = (int)Math.Round(d.XpPerLevelRaw, MidpointRounding.AwayFromZero);
            return d;
        }

        // ---------------------------------------------------------------- the level curve, simulated

        /// <summary>
        /// Cycles a guild of `members` needs to reach the ceiling, walked one cycle at a time
        /// through the same expressions the client and 0012 use - not the closed form. Walked so
        /// the reading includes the integer rounding in XpForLevel, which is what a guild actually
        /// meets, and so the ratio the gate asserts is a ratio of things that happened rather than
        /// of an algebraic identity that cannot fail.
        /// </summary>
        private static int GuildCyclesToCeiling(GuildTechConfig tree, int members, out int reached)
        {
            long xp = 0;
            reached = 0;
            for (int cycle = 1; cycle <= 20000; cycle++)
            {
                xp += (long)members * GtMeanCycle;
                reached = tree.LevelForXp(xp, members);
                if (reached >= tree.LevelCeiling) return cycle;
            }
            return int.MaxValue;
        }

        // ---------------------------------------------------------------- the study

        /// <summary>
        /// DevHarness.exe --guild-tech. Prints the whole working and asserts nothing: the
        /// derivation, the level curve at every guild size, the family table, and every guild
        /// talent read at parity by the instrument that reads hero talents. Writes nothing and
        /// changes no content.
        /// </summary>
        internal static int RunGuildTechStudy(ContentDatabase db, string contentDir)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Line("GUILD TECH - the study guild-tech.json's numbers are derived from");
            Line("claude/DESIGN-GUILD-TECH.md. Three numbers are chosen; every other is read off the content or the tree's shape.");
            Line("");
            if (!db.GuildTech.IsUsable) { Line("  guild-tech.json did not load: " + string.Join("; ", db.Warnings.ToArray())); return 1; }

            GuildTechDerived d = DeriveGuildTech(db);
            ReportGuildTechDerivation(db, d);
            ReportGuildTechFamilies(db);
            ReportGuildTechLevelCurve(db);
            ReportGuildTechUnlocks(db);
            GuildTalentParity(db, true);
            return 0;
        }

        private static void ReportGuildTechDerivation(ContentDatabase db, GuildTechDerived d)
        {
            GuildTechConfig tree = db.GuildTech;
            Line("  CHOSEN - three inputs, each a sentence of the design record");
            Line("    a whole track is worth " + F(GtTrackWorthInHeroLevels, "0.0") + " hero level(s)   (G2: the rungs are the floor, and a level is the game's own unit)");
            Line("    a guild of " + GtReferenceGuildSize + " opens the whole tree in " + GtCyclesToCeiling + " cycles   (the one authored pace number: how long the long game is)");
            Line("    G7's exponent " + F(tree.XpExponent, "0.00") + "   (authored by the design doc itself, not by this harness)");
            Line("");
            Line("  READ, NOT CHOSEN");
            Line("    a member's mean cycle is " + GtMeanCycle + " Waymarks - guild-path.json's own unit, asserted there across sizes 2 to 30");
            Line("    guild XP a member contributes in a cycle IS that, so the tree adds no second faucet");
            Line("    one hero level at level " + GtReferenceLevel(db) + " is worth " + Pct3(d.LevelPowerShare) + " of a hero's power, over "
                 + d.Heroes + " heroes (spread " + Pct3(d.LevelShareLow) + " to " + Pct3(d.LevelShareHigh) + ")");
            Line("");
            Line("  DERIVED");
            Line("    track  = " + Pct3(d.LevelPowerShare) + " x " + F(GtTrackWorthInHeroLevels, "0.0") + " = " + Pct3(d.TrackShare) + " of a hero's power");
            Line("    rung   = track / " + tree.RungsPerFamily + " = " + Pct3(d.RungShareRaw) + "  ->  rungScale " + F(d.RungScale, "0.00000"));
            Line("    ceiling = families " + tree.Families.Count + " x (rungs " + tree.RungsPerFamily + " + 1 talent slot) = " + d.Ceiling);
            Line("    rungUnit = " + GtCyclesToCeiling + " cycles x " + GtMeanCycle + " / " + d.TotalRungDepth + " rung-units = "
                 + F(d.RungUnitRaw, "0.00") + "  ->  " + d.RungUnit);
            Line("    xpPerLevel = " + GtMeanCycle + " x " + GtReferenceGuildSize + "^(1 - " + F(tree.XpExponent, "0.00") + ") x "
                 + GtCyclesToCeiling + " / " + d.Ceiling + " = " + F(d.XpPerLevelRaw, "0.00") + "  ->  " + d.XpPerLevel);
            Line("");
            Line("  guild-tech.json today: rungScale " + F(tree.Families.Count > 0 ? tree.Families[0].RungScale : 0f, "0.00000")
                 + ", rungUnit " + tree.RungUnit + ", xpPerLevel " + tree.XpPerLevel + ", ceiling " + tree.LevelCeiling);
            Line("    the whole tree costs " + tree.WholeTreeCost + " Waymarks, "
                 + F(tree.WholeTreeCost / (double)GtMeanCycle, "0.0") + " mean cycles");
            Line("");
        }

        private static void ReportGuildTechFamilies(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            Line("  THE FAMILIES - a hero belongs by ROLE and by nothing else (G1)");
            Line("    " + "family".PadRight(8) + "heroes".PadRight(8) + "rung stats".PadRight(24) + "roles");
            for (int f = 0; f < tree.Families.Count; f++)
            {
                GuildTechFamilyDef fam = tree.Families[f];
                int n = 0;
                for (int h = 0; h < db.Heroes.Count; h++) if (fam.Roles.Contains(db.Heroes[h].Role)) n++;
                List<string> roles = new List<string>();
                for (int r = 0; r < fam.Roles.Count; r++) roles.Add(fam.Roles[r].ToString());
                Line("    " + fam.Name.PadRight(8) + n.ToString().PadRight(8)
                     + string.Join("+", fam.RungStats.ConvertAll(s => s.ToString()).ToArray()).PadRight(24)
                     + string.Join(", ", roles.ToArray()));
            }
            Line("");
        }

        private static void ReportGuildTechLevelCurve(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            Line("  THE LEVEL CURVE - XP per level scales with member count ^ " + F(tree.XpExponent, "0.00") + " (G7)");
            Line("    every size reaches the SAME ceiling (" + tree.LevelCeiling + "); a large guild arrives sooner, not somewhere else");
            Line("    " + "members".PadRight(9) + "xp/level".PadRight(10) + "levels/cycle".PadRight(14) + "cycles to ceiling".PadRight(19) + "vs a guild of 2");
            int[] sizes = { 1, 2, 5, 10, 20, 30 };
            int baseline = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                int reached;
                int cycles = GuildCyclesToCeiling(tree, sizes[i], out reached);
                if (sizes[i] == 2) baseline = cycles;
            }
            for (int i = 0; i < sizes.Length; i++)
            {
                int m = sizes[i];
                int reached;
                int cycles = GuildCyclesToCeiling(tree, m, out reached);
                double perCycle = m * (double)GtMeanCycle / tree.XpForLevel(m);
                Line("    " + m.ToString().PadRight(9) + tree.XpForLevel(m).ToString().PadRight(10)
                     + F(perCycle, "0.000").PadRight(14)
                     + (cycles == int.MaxValue ? "never" : cycles.ToString()).PadRight(19)
                     + (baseline > 0 && cycles > 0 && cycles != int.MaxValue ? "x" + F(baseline / (double)cycles, "0.00") : "-")
                     + "   (reaches level " + reached + ")");
            }
            double predicted = Math.Pow(30.0 / 2.0, 1.0 - tree.XpExponent);
            Line("    the square root predicts a guild of 30 climbs x" + F(predicted, "0.000") + " as fast per person as a guild of 2");
            Line("");
        }

        private static void ReportGuildTechUnlocks(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            Line("  WHAT EACH LEVEL OPENS - a rule, not a table (GuildTechConfig.BuildUnlocks)");
            for (int i = 0; i < tree.Unlocks.Count; i++)
            {
                GuildTechUnlock u = tree.Unlocks[i];
                GuildTechFamilyDef fam = tree.Family(u.FamilyId);
                string what = u.TalentSlot ? "talent slot" : "rung " + u.Rung + " (" + tree.RungCost(u.Rung) + " Waymarks)";
                Line("    level " + u.Level.ToString().PadLeft(2) + "   " + (fam != null ? fam.Name : u.FamilyId).PadRight(8) + what
                     + (u.GrantsNode.Length > 0 ? "   + grants " + u.GrantsNode + " (G9)" : ""));
            }
            Line("");
        }

        private static string Pct3(double v) { return (v * 100).ToString("0.000", CultureInfo.InvariantCulture) + "%"; }

        // ---------------------------------------------------------------- the gate

        public static int GuildTechGate(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("GUILD TECH");
            GuildTechConfig tree = db.GuildTech;
            Check(tree.IsUsable, "guild-tech.json loads with " + tree.Families.Count + " families and a ceiling of " + tree.LevelCeiling);
            if (!tree.IsUsable) return 0;

            CheckGuildTechShape(db);
            CheckGuildTechDerivation(db);
            CheckGuildTechLevelCurve(db);
            CheckGuildTechGates(db);
            CheckReferencePlayerHasNoGuildTech(db);
            CheckGuildDungeonStripsGuildTech(db);
            CheckGuildTechPathNodesMoved(db);
            CheckGuildTechServerAgrees(db, contentDir);
            GuildTalentParity(db, false);
            return 0;
        }

        /// <summary>G1 as a build failure: every role in exactly one family, every family with at
        /// least one role, and the hero counts the design record's table claims.</summary>
        private static void CheckGuildTechShape(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            List<string> orphans = new List<string>(), doubled = new List<string>();
            foreach (HeroRole role in Enum.GetValues(typeof(HeroRole)))
            {
                int claims = 0;
                for (int f = 0; f < tree.Families.Count; f++) if (tree.Families[f].Roles.Contains(role)) claims++;
                if (claims == 0) orphans.Add(role.ToString());
                if (claims > 1) doubled.Add(role.ToString());
            }
            Check(orphans.Count == 0 && doubled.Count == 0,
                  "G1 - every hero role belongs to exactly one family"
                  + (orphans.Count > 0 ? "; NO FAMILY CLAIMS " + string.Join(", ", orphans.ToArray()) : "")
                  + (doubled.Count > 0 ? "; TWO FAMILIES CLAIM " + string.Join(", ", doubled.ToArray()) : ""));

            List<string> counts = new List<string>();
            bool everyFamilyHasHeroes = true;
            for (int f = 0; f < tree.Families.Count; f++)
            {
                int n = 0;
                for (int h = 0; h < db.Heroes.Count; h++) if (tree.Families[f].Roles.Contains(db.Heroes[h].Role)) n++;
                counts.Add(tree.Families[f].Name + " " + n);
                if (n == 0) everyFamilyHasHeroes = false;
            }
            Check(everyFamilyHasHeroes, "every family has heroes in the roster to give its tech to (" + string.Join(", ", counts.ToArray()) + ")");

            // A rung stat the power score cannot see would be a rung the game reads as free.
            bool visible = true;
            for (int f = 0; f < tree.Families.Count; f++)
                for (int s = 0; s < tree.Families[f].RungStats.Count; s++)
                    if (StatMath.PowerWeightOf(tree.Families[f].RungStats[s]) <= 0f) visible = false;
            Check(visible, "every rung stat carries power - a rung the power score cannot see would understate the line that bought it");

            // Distinguishable tracks: two families whose rungs move the same stats are one family
            // with two names and two prices.
            List<string> same = new List<string>();
            for (int a = 0; a < tree.Families.Count; a++)
                for (int b = a + 1; b < tree.Families.Count; b++)
                {
                    string sa = string.Join("+", tree.Families[a].RungStats.ConvertAll(x => x.ToString()).ToArray());
                    string sb = string.Join("+", tree.Families[b].RungStats.ConvertAll(x => x.ToString()).ToArray());
                    if (sa == sb) same.Add(tree.Families[a].Id + "/" + tree.Families[b].Id);
                }
            Check(same.Count == 0, "no two families buy the same stats with their rungs"
                  + (same.Count > 0 ? " - " + string.Join(", ", same.ToArray()) + " are one track with two names" : ""));
        }

        /// <summary>The numbers in the file are the numbers the tree derives. CheckDerivation's
        /// shape, and its tolerance, for its reasons.</summary>
        private static void CheckGuildTechDerivation(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            GuildTechDerived d = DeriveGuildTech(db);
            Line("  derived: one level is " + Pct3(d.LevelPowerShare) + " of a hero's power over " + d.Heroes
                 + " heroes, so a track is " + Pct3(d.TrackShare) + " and a rung " + Pct3(d.RungShareRaw)
                 + "; rungUnit " + F(d.RungUnitRaw, "0.0") + ", xpPerLevel " + F(d.XpPerLevelRaw, "0.0") + ", ceiling " + d.Ceiling);

            List<string> off = new List<string>();
            for (int f = 0; f < tree.Families.Count; f++)
                if (!GtWithin(tree.Families[f].RungScale, d.RungShareRaw))
                    off.Add(tree.Families[f].Id + " " + F(tree.Families[f].RungScale, "0.00000"));
            Check(off.Count == 0,
                  "every family's rungScale is the derived " + F(d.RungShareRaw, "0.00000") + " within " + GtPct(GtTolerance)
                  + (off.Count > 0 ? " - " + string.Join(", ", off.ToArray()) + "; run --guild-tech and write what it derives" : ""));

            Check(GtWithin(tree.RungUnit, d.RungUnitRaw),
                  "rungUnit " + tree.RungUnit + " is the derived " + F(d.RungUnitRaw, "0.0") + " within " + GtPct(GtTolerance));
            Check(GtWithin(tree.XpPerLevel, d.XpPerLevelRaw),
                  "xpPerLevel " + tree.XpPerLevel + " is the derived " + F(d.XpPerLevelRaw, "0.0") + " within " + GtPct(GtTolerance));
            Check(tree.LevelCeiling == d.Ceiling,
                  "the ceiling is the tree's own shape: " + tree.Families.Count + " families x (" + tree.RungsPerFamily
                  + " rungs + 1 talent slot) = " + d.Ceiling);

            // The two clocks are one clock: the tree's bill and the ceiling's pace.
            int reached;
            int cycles = GuildCyclesToCeiling(tree, GtReferenceGuildSize, out reached);
            double bill = tree.WholeTreeCost / (double)GtMeanCycle;
            Check(cycles != int.MaxValue && Math.Abs(bill - cycles) <= 0.15 * cycles,
                  "a member finishes PAYING for the tree (" + F(bill, "0.0") + " cycles) about when a guild of "
                  + GtReferenceGuildSize + " finishes OPENING it (" + cycles + " cycles) - one clock, not two");
        }

        /// <summary>
        /// G7, both halves. The rate ratio is asserted against WHAT THE SQUARE ROOT PREDICTS and
        /// never against a target somebody typed: the design doc's estimate of the advantage is an
        /// estimate, the exponent is the authored thing, and if the two disagree the exponent wins
        /// and this prints what the curve actually does.
        /// </summary>
        private static void CheckGuildTechLevelCurve(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            int[] sizes = { 1, 2, 5, 10, 20, 30 };
            List<string> reachedAll = new List<string>();
            bool sameCeiling = true;
            Dictionary<int, int> cyclesBySize = new Dictionary<int, int>();
            for (int i = 0; i < sizes.Length; i++)
            {
                int reached;
                int cycles = GuildCyclesToCeiling(tree, sizes[i], out reached);
                cyclesBySize[sizes[i]] = cycles;
                reachedAll.Add(sizes[i] + ": " + (cycles == int.MaxValue ? "never" : cycles + " cycles"));
                if (reached != tree.LevelCeiling) sameCeiling = false;
            }
            Check(sameCeiling,
                  "G7 - THE CEILING IS THE SAME FOR EVERY SIZE: a guild of 1 and a guild of 30 both reach level "
                  + tree.LevelCeiling + " (" + string.Join(", ", reachedAll.ToArray()) + ")");

            double predicted = Math.Pow(30.0 / 2.0, 1.0 - tree.XpExponent);
            double measured = cyclesBySize[30] > 0 && cyclesBySize[30] != int.MaxValue && cyclesBySize[2] != int.MaxValue
                ? cyclesBySize[2] / (double)cyclesBySize[30] : 0;
            Check(measured > 0 && Math.Abs(measured - predicted) <= 0.05 * predicted,
                  "G7 - the measured rate ratio is the SQUARE ROOT'S OWN PREDICTION, not a tuned target: a guild of 30 levels x"
                  + F(measured, "0.000") + " as fast per person as a guild of 2, and (30/2)^(1-" + F(tree.XpExponent, "0.00")
                  + ") predicts x" + F(predicted, "0.000"));

            // A guild of one is not a failure state, it is just slow. Said out loud so nobody
            // reads the ceiling check above as proof that every size is equally served.
            Line("    a guild of 1 reaches the ceiling in " + (cyclesBySize[1] == int.MaxValue ? "never" : cyclesBySize[1].ToString())
                 + " cycles, a guild of 30 in " + cyclesBySize[30] + " - the same ceiling, a different road");
        }

        /// <summary>
        /// G6, and the rung ceiling per level. No rung may be bought below the level the tree opens
        /// it at, and GuildTech.RungsOn is what enforces it, so the check goes through the real
        /// call rather than re-reading the table.
        /// </summary>
        private static void CheckGuildTechGates(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            List<string> wrong = new List<string>();
            for (int f = 0; f < tree.Families.Count; f++)
            {
                GuildTechFamilyDef fam = tree.Families[f];
                int previous = 0;
                for (int r = 1; r <= tree.RungsPerFamily; r++)
                {
                    int at = tree.LevelForRung(fam.Id, r);
                    if (at <= 0) { wrong.Add(fam.Id + " rung " + r + " opens at no level"); continue; }
                    if (at <= previous) wrong.Add(fam.Id + " rung " + r + " opens at " + at + ", not after rung " + (r - 1));
                    previous = at;

                    // A member who has bought every rung still holds only what the level opened.
                    GuildTechState greedy = new GuildTechState();
                    greedy.Rungs[fam.Id] = tree.RungsPerFamily;
                    greedy.Level = at - 1;
                    if (GuildTech.RungsOn(tree, greedy, fam.Id) >= r)
                        wrong.Add(fam.Id + " rung " + r + " counts at guild level " + (at - 1));
                    greedy.Level = at;
                    if (GuildTech.RungsOn(tree, greedy, fam.Id) != r)
                        wrong.Add(fam.Id + " rung " + r + " does not count at guild level " + at);
                }
                int slot = tree.LevelForTalentSlot(fam.Id);
                if (slot <= 0) wrong.Add(fam.Id + " has no talent slot level");
            }
            Check(wrong.Count == 0,
                  "G6 - no rung is reachable below the guild level the tree opens it at, and the ceiling per level is the tree's"
                  + (wrong.Count > 0 ? " - " + string.Join("; ", wrong.ToArray()) : ""));

            // A talent nobody picked, and a talent this build does not name, both grant nothing.
            if (tree.Families.Count > 0)
            {
                GuildTechFamilyDef fam = tree.Families[0];
                HeroRole role = fam.Roles[0];
                GuildTechState st = new GuildTechState();
                st.Level = tree.LevelCeiling;
                bool none = GuildTech.TalentFor(tree, st, role) == null;
                st.Talents[fam.Id] = "a-talent-this-build-does-not-have";
                bool unknown = GuildTech.TalentFor(tree, st, role) == null;
                st.Talents[fam.Id] = fam.Talents.Count > 0 ? fam.Talents[0].Id : "";
                st.Level = tree.LevelForTalentSlot(fam.Id) - 1;
                bool tooEarly = GuildTech.TalentFor(tree, st, role) == null;
                st.Level = tree.LevelForTalentSlot(fam.Id);
                bool open = fam.Talents.Count == 0 || GuildTech.TalentFor(tree, st, role) != null;
                Check(none && unknown && tooEarly && open,
                      "a family with no pick, a pick this build does not name, and a pick made before the slot opens all grant nothing");
            }
        }

        /// <summary>
        /// G5's premise, ASSERTED RATHER THAN ARRANGED. The reference player recommendedPower is
        /// derived for has no guild tech - not because the harness switches it off, but because a
        /// fresh profile has none and nothing in the economy walk ever gives it any. If that stops
        /// being true, the campaign's whole calibration moves and this is what says so.
        /// </summary>
        private static void CheckReferencePlayerHasNoGuildTech(ContentDatabase db)
        {
            PlayerProfile fresh = GameSession.CreateNewProfile(db, 0);
            bool empty = fresh.GuildTech != null && fresh.GuildTech.IsEmpty && fresh.GuildTech.Level == 0;

            // And it stays empty through a walk: the economy walk is the reference player.
            EconomyRun walk = SimulateEconomyUnder(db, 1, false, false);
            bool stillEmpty = walk.Profile.GuildTech != null && walk.Profile.GuildTech.IsEmpty && walk.Profile.GuildTech.Level == 0;
            Check(empty && stillEmpty,
                  "G5 - the reference player recommendedPower is derived for has NO guild tech, at creation and after a whole campaign walk");

            // And the tree is worth nothing to a profile that has none: same stat line, same power.
            HeroDef sample = db.Heroes.Count > 0 ? db.Heroes[0] : null;
            if (sample != null)
            {
                int bare = StatMath.PowerScore(StatMath.ForHero(sample, 40));
                int through = StatMath.PowerScore(StatMath.ForHero(sample, 40, null, null, null, db.GuildTech, fresh.GuildTech));
                Check(bare == through, "and an empty tree is an exact no-op on a stat line (" + bare + " either way)");
            }
        }

        /// <summary>
        /// WHY guild-tech.json IS NOT IN ContentVersion, proved rather than argued. The guild
        /// dungeon hands CombatSimulation an empty profile, so no floor can read the tree - which
        /// is what lets a price or a talent change without retiring every guild's stored replays.
        /// </summary>
        private static void CheckGuildDungeonStripsGuildTech(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            int seed = GuildDungeon.SeedFor("guild-tech-strip", 1);
            List<HeroInstance> line = new List<HeroInstance>();
            for (int i = 0; i < db.Heroes.Count && line.Count < db.Balance.MaxTeamSize; i++)
            {
                HeroInstance h = new HeroInstance();
                h.Uid = "gt" + i; h.DefId = db.Heroes[i].Id; h.Def = db.Heroes[i]; h.Level = 40;
                line.Add(h);
            }
            TeamSnapshot team = GuildDungeon.LineOf(db, line, "guild tech");

            // THE DUNGEON'S OWN PATH. GuildDungeon.Fight takes no profile and builds an empty one,
            // so there is no parameter by which guild tech could arrive.
            string dungeon = GtFingerprint(GuildDungeon.Fight(db, team, seed, 1));

            // The same fight rebuilt by hand, so the profile CAN be named: once empty, once at
            // maximum tech. Empty must equal the dungeon's own path, or this check is measuring
            // something else. Maximum must DIFFER from it, or the check proves nothing at all -
            // a tree worth nothing on this floor would pass a "the dungeon strips it" test by
            // being inert rather than by being stripped.
            PlayerProfile none = GameSession.CreateNewProfile(db, 0);
            PlayerProfile max = GameSession.CreateNewProfile(db, 0);
            max.GuildTech = MaxGuildTech(tree);
            string bare = GtFingerprint(GtDungeonFightAs(db, team, seed, 1, none));
            string teched = GtFingerprint(GtDungeonFightAs(db, team, seed, 1, max));

            Check(dungeon == bare && dungeon != teched,
                  "the guild dungeon strips guild tech at the door: its own path (" + dungeon + ") is the no-tech fight, and the same "
                  + "floor WOULD read differently at maximum tech (" + teched + ") if the tree could reach it - which is why "
                  + "guild-tech.json is not hashed into ContentVersion");
        }

        /// <summary>
        /// GuildDungeon.FightUnder's request, rebuilt with the profile named by the caller. The one
        /// thing the real call does not let anybody do, and the only way to show that what the
        /// dungeon fights is the no-tech fight rather than merely A fight.
        /// </summary>
        private static BattleResult GtDungeonFightAs(ContentDatabase db, TeamSnapshot team, int dungeonSeed,
                                                     int floor, PlayerProfile profile)
        {
            TeamSnapshot fought = GuildDungeon.NormaliseTeam(db, team);
            BattleRequest request = new BattleRequest();
            request.Content = db;
            request.Stage = GuildDungeonGenerator.BuildFloor(db, dungeonSeed, floor);
            request.Seed = GuildDungeon.FightSeedFor(dungeonSeed, floor);
            FloorConditionDef condition = GuildDungeon.ConditionOf(db, dungeonSeed, floor);
            request.Rules = condition != null ? condition.Fight : null;
            for (int i = 0; i < fought.Units.Count; i++)
            {
                SnapshotUnit u = fought.Units[i];
                HeroInstance hero = new HeroInstance();
                hero.Uid = "guild_dungeon_" + i;
                hero.DefId = u.DefId;
                hero.Def = db.GetHero(u.DefId);
                hero.Level = u.Level;
                hero.SelectedTalent = u.SelectedTalent;
                request.Team.Add(hero);
            }
            request.Profile = profile;
            return CombatSimulation.Run(request);
        }

        /// <summary>G9: the Path's two built nodes are early guild level rewards now, and a member
        /// who already bought one keeps it.</summary>
        private static void CheckGuildTechPathNodesMoved(ContentDatabase db)
        {
            GuildTechConfig tree = db.GuildTech;
            List<string> granted = tree.NodesGrantedAt(tree.LevelCeiling);
            Check(granted.Contains("second-line") && granted.Contains("line-talents"),
                  "G9 - the saved line and the line that keeps its talents are guild level rewards now ("
                  + string.Join(", ", granted.ToArray()) + ")");

            PlayerProfile p = GameSession.CreateNewProfile(db, 0);
            GameSession s = new GameSession(db, p);
            bool noneAtZero = s.LinesAllowed == 1 && !s.OwnsGuildPathNode("second-line");
            p.GuildTech.Level = 1;
            bool grantedAtOne = s.OwnsGuildPathNode("second-line") && s.LinesAllowed == 2;
            p.GuildTech.Level = 0;
            p.GuildPathNodes.Add("second-line");
            bool boughtStillCounts = s.OwnsGuildPathNode("second-line") && s.LinesAllowed == 2;
            Check(noneAtZero && grantedAtOne && boughtStillCounts,
                  "a guild at level 0 grants no line, level 1 grants the second, and a node BOUGHT under 0011 still counts - "
                  + "so the tree replaced the Path with nobody's purchase migrated or lost");
        }

        /// <summary>The server charges and gates; the file predicts. Every constant in 0012 must be
        /// the file's. CheckServerAgrees' shape, for its reason.</summary>
        private static void CheckGuildTechServerAgrees(ContentDatabase db, string contentDir)
        {
            GuildTechConfig tree = db.GuildTech;
            string sql = GtReadMigration(contentDir);
            if (sql.Length == 0) { Check(false, "0012_guild_tech.sql is in Server/supabase/migrations"); return; }

            int unit = GtSqlConstant(sql, "guild_tech_rung_unit");
            int xp = GtSqlConstant(sql, "guild_tech_xp_per_level");
            int ceiling = GtSqlConstant(sql, "guild_tech_level_ceiling");
            int rungs = GtSqlConstant(sql, "guild_tech_rungs_per_family");
            Check(unit == tree.RungUnit && xp == tree.XpPerLevel && ceiling == tree.LevelCeiling && rungs == tree.RungsPerFamily,
                  "0012 charges what guild-tech.json says: rung unit " + unit + ", xp per level " + xp
                  + ", ceiling " + ceiling + ", rungs per family " + rungs);

            // The square root is written twice - here and in 0012 - so the gate reads it in both.
            Check(Regex.IsMatch(sql, @"sqrt\(\s*greatest\(\s*1"),
                  "0012 scales the XP requirement by the SQUARE ROOT of member count (G7), the same expression the client predicts with");

            // Every family and every talent the file names has a row, so a guild cannot pick a
            // talent the server has never heard of.
            List<string> missing = new List<string>();
            for (int f = 0; f < tree.Families.Count; f++)
            {
                if (!sql.Contains("('" + tree.Families[f].Id + "'")) missing.Add(tree.Families[f].Id);
                for (int t = 0; t < tree.Families[f].Talents.Count; t++)
                    if (!sql.Contains("'" + tree.Families[f].Talents[t].Id + "'")) missing.Add(tree.Families[f].Talents[t].Id);
            }
            Check(missing.Count == 0, "0012 knows every family and every guild talent guild-tech.json names"
                  + (missing.Count > 0 ? " - missing " + string.Join(", ", missing.ToArray()) : ""));

            Check(sql.Contains("guild_tech_dev_set"),
                  "0012 carries the dev override that sets a guild's level and XP directly, so a two-person guild can sit at maximum");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A state with every rung on every family and every talent slot filled with its
        /// first option: the strongest tree this content can express.</summary>
        private static GuildTechState MaxGuildTech(GuildTechConfig tree)
        {
            GuildTechState s = new GuildTechState();
            s.Level = tree.LevelCeiling;
            s.Members = GtReferenceGuildSize;
            for (int f = 0; f < tree.Families.Count; f++)
            {
                s.Rungs[tree.Families[f].Id] = tree.RungsPerFamily;
                if (tree.Families[f].Talents.Count > 0) s.Talents[tree.Families[f].Id] = tree.Families[f].Talents[0].Id;
            }
            return s;
        }

        /// <summary>A state with every rung and no talent: the tree's stat half alone.</summary>
        private static GuildTechState MaxGuildRungs(GuildTechConfig tree)
        {
            GuildTechState s = MaxGuildTech(tree);
            s.Talents.Clear();
            return s;
        }

        private static string GtFingerprint(BattleResult r)
        {
            if (r == null) return "NULL";
            return r.Victory + "/" + r.Rounds + "/" + r.Events.Count + "/"
                 + r.PlayerHpRatio.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static bool GtWithin(double written, double derived)
        {
            return Math.Abs(written - derived) <= GtTolerance * Math.Max(1e-9, Math.Abs(derived));
        }

        private static string GtPct(double v) { return (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%"; }

        private static string GtReadMigration(string contentDir)
        {
            string dir = contentDir;
            for (int up = 0; up < 6 && dir != null; up++)
            {
                string path = Path.Combine(dir, Path.Combine("Server", Path.Combine("supabase",
                                  Path.Combine("migrations", "0012_guild_tech.sql"))));
                if (File.Exists(path)) return File.ReadAllText(path);
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            return "";
        }

        private static int GtSqlConstant(string sql, string function)
        {
            Match m = Regex.Match(sql, "function public\\." + function + "\\(\\)[^$]*\\$\\$ select (\\d+); \\$\\$");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }
    }
}
