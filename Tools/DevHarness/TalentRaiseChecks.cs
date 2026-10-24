using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE TALENTS THE SWEEP ARENA CANNOT RAISE, RAISED ON PURPOSE, inside the talent sweep's own
    /// check rather than only in a room of THE ARENA MATRIX.
    ///
    /// WHY THERE ARE TWO NUMBERS. AUTHORED-TALENT SWEEP prints that the shared arena leaves 19 of 90
    /// talents with a fingerprint identical to leaving them unpicked, and its coverage line says
    /// 21 talents are observed only in a named matrix room (69 in the sweep arena). They measure
    /// two different things and neither is wrong. The first reads the sweep's FINGERPRINT, announcements
    /// included, and counts a talent dark when no seed's fight changed. The second is the matrix's
    /// stricter reading: the fight must change with the talent's own announcement taken out, and a
    /// talent with a trigger of its own must have fired. The 19 are all inside the 21; the two
    /// extra are talents (Isolde I and Nysera III today) that move the sweep fight picked while the
    /// trigger clause they carry never fires there, which the matrix does not count as the talent
    /// being seen. The union, 21, is the set this file raises, and it is computed on every run rather than named.
    ///
    /// WHAT RAISING MEANS HERE. Each talent in the union has a scenario in TalentRaiseScenarios: a
    /// stage, a squad a player could field, a seed and a clock that provide what the talent waits
    /// on (the text TalentGate prints is the brief), and the ONE tally the talent's own sentence
    /// promises with the direction it moves. The scenario is run under the house pattern the
    /// matrix rooms use (EvaluateMatrixRoom): the talent selected twice on the same seed must be
    /// byte-identical, the control with the talent unpicked must fight a different fight, and the
    /// named tally must move the way the sentence says. A talent is not counted on "something
    /// moved"; a scenario with no named tally is refused.
    ///
    /// WHAT IS NOT FORCED. A talent no legal lineup can raise (no hero fielded twice, no more than
    /// PlayerProfile.TeamSize) gets ONE entry in TalentRaiseBlockers: a reason, and a precondition
    /// measured on this build rather than remembered. A reason whose talent is raised after all, or
    /// whose precondition no longer holds, fails until it is taken out. The list is empty today
    /// because every talent here is raised; BlockerProblems is proved against synthetic entries on
    /// every run so that the first real entry arrives under a comparison that can fail.
    ///
    /// NOTHING HERE WRITES A FINGERPRINT. The existing sweep, its squads, seeds and baseline files
    /// are untouched, and the readings below are asserted as properties, not pinned.
    /// </summary>
    internal static partial class Program
    {
        private sealed class TalentRaise
        {
            public readonly MatrixRoom Room;
            /// <summary>What moves, in the talent's own words.</summary>
            public readonly string Moves;
            public TalentRaise(string pair, string stage, string allies, int seed, bool slow,
                               string measure, int direction, string provides, string moves)
            {
                Room = new MatrixRoom(pair, stage, allies, seed, slow, measure, direction, provides);
                Moves = moves;
            }
        }

        /// <summary>
        /// One scenario per talent the sweep arena cannot raise. Provides says what the scenario
        /// has that the sweep arena does not; Moves says what changed against the control, as the
        /// talent's own sentence puts it. The stages and bodies are the matrix arena's.
        /// </summary>
        private static readonly TalentRaise[] TalentRaiseScenarios = {
            new TalentRaise("aeron_the_unbroken_stride II", "s_caster_tail", DarkFive, 1, false, "shift.ally", 1,
                "an enemy that casts, so Momentum Breaker has a debt to call in beside four allies to advance",
                "the lowest-Energy ally advances its turn once the debt is called in"),
            new TalentRaise("dahlia_the_wayward_hymn II", "s_caster_bruiser_tail", DarkFive, 1, false, "heal.others", 1,
                "allies hurt enough for Broken Rhythm to heal, and a second-lowest ally to receive the extra",
                "Dahlia heals more HP onto allies"),
            new TalentRaise("edrin_the_distant_bell III", "s_buffer", DarkFive, 1, false, "applied:Resolve", 1,
                "a marked enemy that lives long enough for Resolve to bank past its old ceiling",
                "Edrin banks more Resolve than the control can"),
            new TalentRaise("elyra_the_patient_warden III", "s_buffer", DarkFive, 1, false, "shield.others", 1,
                "Resolve banked to full, so Stillness Pays spends it and the lowest-HP ally is there to be warded",
                "an ally gains a Ward from Elyra where the control grants none"),
            new TalentRaise("hale_the_relentless_hunter II", "s_buffer", "", 1, false, "hits", 1,
                "Hale alone, so no ally ever strikes Quarry and only the talent can open the missing-HP blow",
                "Hale lands extra blows with Closing Radius"),
            new TalentRaise("isolde_the_slow_fading I", "m_hexer", "", 1, false, "payments.subject:+10", 1,
                "a Cinder of hers running its full course on a body that lives",
                "Isolde gains the 10 Energy the sentence names each time a Cinder she applied expires"),
            new TalentRaise("isolde_the_slow_fading III", "s_buffer", "", 1, false, "lost:Resolve", 1,
                "Revelation expiring while she holds a full set of Traces",
                "Isolde spends her Traces to strike where the control keeps them"),
            new TalentRaise("kael_the_unyielding II", "s_triple", "", 1, false, "applied:Vulnerable", 1,
                "enemies carrying buffs for Take Down the Wall to strip",
                "each enemy stripped of a buff is left Vulnerable to Kael"),
            new TalentRaise("merek_the_sequence_thief I", "s_caster_tail", "", 1, false, "applied:Vulnerable", 1,
                "an enemy that casts, so Anticipated has a turn to delay",
                "the enemy Anticipated delays is left Vulnerable to Merek's blows"),
            new TalentRaise("mireth_the_erased_name I", "s_buffer", "", 1, false, "applied:Resolve", 1,
                "a marked enemy gaining buffs for The Missing Page to take",
                "Mireth carries more Absence for what she takes away"),
            new TalentRaise("mireth_the_erased_name II", "s_buffer", "", 1, false, "energy.enemy-", 1,
                "a full set of Pages for The Missing Page to spend on a marked enemy",
                "the marked enemy loses Energy where the control takes none"),
            new TalentRaise("nysera_the_missing_arrow III", "s_soft_big", "", 1, false, "applied:ArmorBreakUp", 1,
                "a kill made while Vanishing Mark's Armor Break Up is still up",
                "Nysera gains Armor Break Up when Vanishing Mark fires, and Stealth again on the kill"),
            new TalentRaise("rheia_the_revision_scribe I", "s_selfdown", DarkStrikers, 1, false, "damage", 1,
                "an enemy whose Attack Down Rewrite the Outcome has rewritten, struck again",
                "Rheia's blow on a rewritten enemy lands harder"),
            new TalentRaise("rheia_the_revision_scribe II", "s_selfdown", DarkStrikers, 1, false, "applied:Vulnerable", 1,
                "Second Draft firing at full Revision on an enemy that holds a buff",
                "the target is left Vulnerable to Rheia"),
            new TalentRaise("rheia_the_revision_scribe III", "s_selfdown", DarkStrikers, 1, false, "lost:Resolve", -1,
                "Second Draft firing at full Revision, so there is a Revision to keep or spend",
                "Rheia loses less Revision because the strike no longer spends it"),
            new TalentRaise("rovan_the_adaptive_commander II", "s_bruiser", "isara_the_second_chance", 1, false,
                "payments.ally:+8", 1,
                "a large Ward for Pass the Guard to move onto a hurt ally",
                "the ally who receives the Ward gains the 8 Energy the clause pays"),
            new TalentRaise("seraphine_the_closed_volume II", "s_buffer", DarkStrikers, 1, true, "removed:Ledger", 1,
                "the slow clock, so the highest-Energy enemy outlives the Footnote Contradiction Filed writes on it",
                "the Footnote starts with a record in it, so more record is paid out"),
            new TalentRaise("seraphine_the_closed_volume III", "s_buffer_tail", DarkStrikers, 1, false, "shift.by.subject", 1,
                "Volume Closed firing on an enemy whose turn can still be moved",
                "the enemy loses turn progress where the control moves none"),
            new TalentRaise("sylven_the_unfading_blade III", "s_triple", "", 1, false, "applied:HealingReduction", 1,
                "an enemy that gains the buff Wounds Remember waits for",
                "the enemy's mending is shut down with Healing Reduction"),
            new TalentRaise("vaelis_the_unfixed_blade I", "s_soft_soft_big", "isara_the_second_chance", 2, false,
                "applied:EffectResistDown", 1,
                "a Departure target that an ally then strikes, so the cut has a target to lower",
                "Leaving Cut lowers the Departure target's Effect Resist"),
            new TalentRaise("vaelis_the_unfixed_blade III", "s_soft_soft_big", "isara_the_second_chance", 2, false,
                "payments.subject:+12", 1,
                "a Mender setting off Departure, which the sweep's fillers never do",
                "Vaelis gains the 12 Energy when a Mender or Warden sets off Departure"),
        };

        private static TalentRaise TalentRaiseFor(string pair)
        {
            for (int i = 0; i < TalentRaiseScenarios.Length; i++)
                if (TalentRaiseScenarios[i].Room.Rule == pair) return TalentRaiseScenarios[i];
            return null;
        }

        /// <summary>
        /// A talent no legal lineup can raise, and why, in one sentence. Precondition is measured on
        /// every run and returns null while the reason still holds on this build, or a sentence
        /// saying what is different now.
        /// </summary>
        private sealed class TalentRaiseBlocker
        {
            public readonly string Pair, Why;
            public readonly Func<string> Precondition;
            public TalentRaiseBlocker(string pair, string why, Func<string> precondition)
            { Pair = pair; Why = why; Precondition = precondition; }
        }

        // EMPTY, AND THAT IS THE READING: every talent the sweep arena leaves dark is raised by a
        // scenario above in a lineup a player can field. The comparison that would hold a reason
        // honest is BlockerProblems, proved on synthetic entries by CheckTalentRaise.
        private static readonly TalentRaiseBlocker[] TalentRaiseBlockers = new TalentRaiseBlocker[0];

        /// <summary>
        /// What is wrong with a list of stated reasons: a reason for a talent that does not exist
        /// or is not dark, a reason for a talent a scenario raises after all, a reason with no
        /// precondition to measure, and a precondition that no longer holds.
        /// </summary>
        private static List<string> BlockerProblems(TalentRaiseBlocker[] list, Dictionary<string, bool> dark,
                                                    Dictionary<string, bool> raised)
        {
            List<string> problems = new List<string>();
            for (int i = 0; i < list.Length; i++)
            {
                TalentRaiseBlocker b = list[i];
                if (!dark.ContainsKey(b.Pair)) { problems.Add(b.Pair + ": not a talent the sweep arena leaves dark"); continue; }
                if (raised.ContainsKey(b.Pair) && raised[b.Pair]) { problems.Add(b.Pair + ": raised after all, take the reason out"); continue; }
                if (b.Precondition == null) { problems.Add(b.Pair + ": no precondition to measure, so the reason is only remembered"); continue; }
                string now = b.Precondition();
                if (now != null) problems.Add(b.Pair + ": the reason no longer holds (" + now + ")");
            }
            return problems;
        }

        private static string TalentRaiseSetLine(string label, List<string> names)
        {
            return "  [note] " + label + " (" + names.Count + "): " + string.Join(", ", names.ToArray());
        }

        /// <summary>
        /// Called by CheckAuthoredTalentSweep with the talents whose fingerprint did not move in the
        /// sweep arena and the matrix it already measured.
        /// </summary>
        private static void CheckTalentRaise(List<MatrixRule> matrix, List<string> sweepDark, int talentCount)
        {
            if (matrix == null) { Check(false, "the arena matrix was built for the talent raise pass"); return; }

            Dictionary<string, bool> identical = new Dictionary<string, bool>();
            for (int i = 0; i < sweepDark.Count; i++) identical[sweepDark[i]] = true;

            // The two sets and how they relate, computed rather than remembered.
            List<string> setIdentical = new List<string>();
            List<string> setRoomOnly = new List<string>();
            List<string> dark = new List<string>();
            Dictionary<string, bool> darkSet = new Dictionary<string, bool>();
            Dictionary<string, MatrixRule> ruleOf = new Dictionary<string, MatrixRule>();
            int inSweep = 0;
            for (int i = 0; i < matrix.Count; i++)
            {
                MatrixRule r = matrix[i];
                if (r.Kind != MatrixRuleKind.Talent) continue;
                bool idle = identical.ContainsKey(r.Name);
                bool roomOnly = !r.InSweep;
                if (r.InSweep) inSweep++;
                if (idle) setIdentical.Add(r.Name);
                if (roomOnly) setRoomOnly.Add(r.Name);
                if (idle || roomOnly) { dark.Add(r.Name); darkSet[r.Name] = true; ruleOf[r.Name] = r; }
            }
            List<string> extra = new List<string>();
            for (int i = 0; i < setRoomOnly.Count; i++)
                if (!identical.ContainsKey(setRoomOnly[i])) extra.Add(setRoomOnly[i]);

            Line("  [note] two counts, reconciled: " + setIdentical.Count + " of " + talentCount
                 + " talents leave the sweep fingerprint identical to leaving them unpicked, and " + setRoomOnly.Count
                 + " are observed only in a named room (" + inSweep + " in the sweep arena); " + dark.Count
                 + " are in either set, and this pass raises each of them.");
            Line(TalentRaiseSetLine("fingerprint identical to unpicked", setIdentical));
            Line(TalentRaiseSetLine("observed only in a named matrix room", setRoomOnly));
            Line("  [note] in the second set and not the first (" + extra.Count + "): "
                 + (extra.Count == 0 ? "none" : string.Join(", ", extra.ToArray()))
                 + " - the sweep fight moves with them picked, but the trigger clause"
                 + " they carry never fires there");

            // Every dark talent raised, each under the house pattern with its own named tally.
            Dictionary<string, bool> raised = new Dictionary<string, bool>();
            List<string> unraised = new List<string>();
            List<string> unnamed = new List<string>();
            List<string> orphans = new List<string>();
            for (int i = 0; i < dark.Count; i++)
            {
                string pair = dark[i];
                raised[pair] = false;
                TalentRaise sc = TalentRaiseFor(pair);
                if (sc == null)
                {
                    bool blocked = false;
                    for (int b = 0; b < TalentRaiseBlockers.Length; b++) if (TalentRaiseBlockers[b].Pair == pair) blocked = true;
                    if (!blocked) unraised.Add(pair + " waits on " + TalentGate(ruleOf[pair].Passive));
                    continue;
                }
                if (string.IsNullOrEmpty(sc.Room.Measure)) { unnamed.Add(pair); continue; }
                string reading;
                bool ok = EvaluateMatrixRoom(ruleOf[pair], sc.Room, out reading);
                raised[pair] = ok;
                Line("  [raise] " + pair + " (waits on " + TalentGate(ruleOf[pair].Passive) + ") - " + sc.Room.Stage
                     + (sc.Room.Allies.Length > 0 ? " beside " + sc.Room.Allies : " alone")
                     + ", seed " + sc.Room.Seed + (sc.Room.Slow ? ", slow clock" : "") + ": " + sc.Moves + " [" + reading + "]");
                if (!ok) unraised.Add(pair + " is not raised by its scenario: " + reading);
            }
            for (int i = 0; i < TalentRaiseScenarios.Length; i++)
                if (!TalentRaiseRuleExists(matrix, TalentRaiseScenarios[i].Room.Rule)) orphans.Add(TalentRaiseScenarios[i].Room.Rule);

            int raisedCount = 0;
            foreach (KeyValuePair<string, bool> kv in raised) if (kv.Value) raisedCount++;

            Check(unnamed.Count == 0 && orphans.Count == 0,
                  unnamed.Count == 0 && orphans.Count == 0
                    ? "every raise scenario names a talent the roster has and the one tally its sentence promises"
                    : unnamed.Count + " scenario(s) name no tally (" + string.Join(", ", unnamed.ToArray()) + ") and "
                      + orphans.Count + " name a talent the roster does not have (" + string.Join(", ", orphans.ToArray()) + ")");
            Check(unraised.Count == 0,
                  unraised.Count == 0
                    ? "every one of the " + dark.Count + " talents the sweep arena leaves dark is RAISED on purpose and observed"
                      + " changing a fight in its own scenario: same seed twice identical, the unpicked control differs,"
                      + " and the tally its sentence names moves the way it says (" + raisedCount + " raised; the other "
                      + (talentCount - dark.Count) + " move the sweep arena itself)"
                    : unraised.Count + " of " + dark.Count + " dark talents are not raised in a lineup a player can field,"
                      + " and none has a stated reason: " + string.Join("; ", unraised.ToArray()));

            // Stated reasons: re-measured, and the comparison itself proved able to fail.
            List<string> live = BlockerProblems(TalentRaiseBlockers, darkSet, raised);
            Check(live.Count == 0,
                  live.Count == 0
                    ? TalentRaiseBlockers.Length + " stated reason(s) for a talent no legal lineup raises, each with its"
                      + " precondition re-measured on this build"
                    : live.Count + " stated reason(s) have outlived what they were written about: "
                      + string.Join("; ", live.ToArray()));

            string firstDark = dark.Count > 0 ? dark[0] : "x";
            Dictionary<string, bool> fakeDark = new Dictionary<string, bool>();
            fakeDark[firstDark] = true;
            Dictionary<string, bool> fakeRaised = new Dictionary<string, bool>();
            fakeRaised[firstDark] = true;
            Dictionary<string, bool> fakeNotRaised = new Dictionary<string, bool>();
            fakeNotRaised[firstDark] = false;
            TalentRaiseBlocker holds = new TalentRaiseBlocker(firstDark, "synthetic", delegate () { return null; });
            TalentRaiseBlocker broken = new TalentRaiseBlocker(firstDark, "synthetic", delegate () { return "now different"; });
            TalentRaiseBlocker unmeasured = new TalentRaiseBlocker(firstDark, "synthetic", null);
            TalentRaiseBlocker unknown = new TalentRaiseBlocker("nobody_at_all I", "synthetic", delegate () { return null; });
            bool proved = BlockerProblems(new TalentRaiseBlocker[] { holds }, fakeDark, fakeNotRaised).Count == 0
                          && BlockerProblems(new TalentRaiseBlocker[] { holds }, fakeDark, fakeRaised).Count == 1
                          && BlockerProblems(new TalentRaiseBlocker[] { broken }, fakeDark, fakeNotRaised).Count == 1
                          && BlockerProblems(new TalentRaiseBlocker[] { unmeasured }, fakeDark, fakeNotRaised).Count == 1
                          && BlockerProblems(new TalentRaiseBlocker[] { unknown }, fakeDark, fakeNotRaised).Count == 1;
            Check(proved,
                  "a stated reason is held to its precondition: one that holds passes, and one for a talent that is raised"
                  + " after all, whose precondition is gone, that has none, or that names no dark talent fails");
        }

        private static bool TalentRaiseRuleExists(List<MatrixRule> matrix, string name)
        {
            for (int i = 0; i < matrix.Count; i++)
                if (matrix[i].Kind == MatrixRuleKind.Talent && matrix[i].Name == name) return true;
            return false;
        }
    }
}
