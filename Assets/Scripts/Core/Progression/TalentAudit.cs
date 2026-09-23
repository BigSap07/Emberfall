namespace Emberfall.Core.Progression
{
    /// <summary>A talent the audit found costing its own hero output, and what it buys for it.</summary>
    public sealed class AuditedTalent
    {
        public readonly string HeroId;
        public readonly int Selection;

        /// <summary>True for a talent that buys nothing back: a trap, waiting on a kit change.
        /// False for a trade, which buys something the output total cannot see.</summary>
        public readonly bool Trap;

        /// <summary>One player-facing sentence: what it costs and what, if anything, it buys.</summary>
        public readonly string Note;

        public AuditedTalent(string heroId, int selection, bool trap, string note)
        {
            HeroId = heroId;
            Selection = selection;
            Trap = trap;
            Note = note;
        }
    }

    /// <summary>
    /// THE AUDITED LIST, as the game can read it: the talents Docs/PINOY-LIST.md section 2e and the
    /// harness's KnownCostlyTalents record as costing their own hero output across every arena that
    /// reaches them.
    ///
    /// WHY THE SCREEN NEEDS IT AS WELL AS ITS OWN MEASUREMENT. TalentTrial measures one line, and
    /// in some lines a costly talent reads inside the noise - the harness reads each of these
    /// across every room it builds, and one player's line is one room. A screen that let the
    /// reading in a quiet line stand in for the audit would hide a cost exactly where it was
    /// quietest.
    ///
    /// EVERY ENTRY IS A TRADE NOW, AND THERE ARE TWO. The two traps this list carried - Kael III
    /// and Mireth I - were rewritten rather than relabelled: both bought nothing, and both now buy
    /// something the fight can see. Three of the seven trades went the same way, for the same
    /// reason read one step further in: Rheia I paid two Energy off every Applied Revision for a
    /// rider on one clause, Rheia III's own note said her casts refill the Revision it was buying
    /// back, and Kiva III had two clauses that no rate her kit banks at could ever reach. What is
    /// left is ONE, and the parity study is why it is the one: Nysera I is the only entry whose
    /// purchase pays when the two sides are matched. It reads -9.4% of Attack in the sweep arena
    /// and +1.6% in a duel at parity, because the Provoke it strips off the body aiming at her
    /// frees the whole line's aim, and the sweep arena has no line to free. Marrow I was the
    /// other, and the same study said its purchase does not pay either - a third ally under
    /// Promise for a smaller Promise heal read -5.6pp of win rate on the ladder - so the price
    /// went rather than the entry staying. Trap stays on AuditedTalent because the next one will
    /// need it.
    ///
    /// A COPY, and it can drift. The harness's KnownCostlyTalents is the list the build proves; this
    /// is what the game is allowed to see, because the harness is not compiled into it. The harness
    /// checks the two equal, traps and trades alike, in CheckTalentAuditMatchesHarness.
    /// </summary>
    public static class TalentAudit
    {
        public static readonly AuditedTalent[] Costly =
        {
            new AuditedTalent("nysera_the_missing_arrow", 1, false,
                "Audited as a trade: Nysera deals less, and strips a buff off whoever targets her.")
        };

        public static AuditedTalent For(string heroId, int selection)
        {
            for (int i = 0; i < Costly.Length; i++)
                if (Costly[i].HeroId == heroId && Costly[i].Selection == selection) return Costly[i];
            return null;
        }
    }
}
