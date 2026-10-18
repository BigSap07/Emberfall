using System;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// What the Armory and the Archive charge (DESIGN-SINKS.md). A price is DAYS of the shop's
    /// income, turned into a currency amount here and nowhere else, so a retune of Balance moves
    /// every price with it and sinks.json never holds a second copy of an income formula.
    ///
    /// One day of income is a full day of attempts, every one won, at the highest standing or floor
    /// the player has reached. The formulas copy GameSession.GrantProvingResult and
    /// GameSession.GrantRecitalResult, and the harness ties them to the real grants. The highest
    /// standing and floor are used, not the current standing, because the current standing can be
    /// lowered by losing on purpose and a price that follows it can be lowered by throwing fights.
    /// The Recital day ignores the Guardian floor bonus, which prices slightly high: the safe side.
    ///
    /// Computed at display and at purchase from this one function. A price is never stored.
    /// </summary>
    public static class SinkPricing
    {
        /// <summary>Attempts a full day can hold: the bank's size, or as many as regenerate in
        /// 24 hours if that is fewer. The same recharge floor GameSession uses for its banks.</summary>
        public static int AttemptsPerDay(int bankMax, float rechargeHours)
        {
            if (bankMax <= 0) return 0;
            double hours = Math.Max(0.01f, rechargeHours);
            int regenerated = (int)Math.Floor(24.0 / hours + 1e-9);
            return Math.Min(bankMax, Math.Max(0, regenerated));
        }

        /// <summary>One day of Valor at a Proving standing.</summary>
        public static long ValorDay(BalanceConfig balance, int highestStanding)
        {
            long perAttempt = balance.ProvingRewardValorBase
                            + (long)balance.ProvingRewardValorPerStanding * Math.Max(0, highestStanding);
            return AttemptsPerDay(balance.ProvingBrandsMax, balance.ProvingBrandsRechargeHours) * Math.Max(0L, perAttempt);
        }

        /// <summary>One day of Coda at a Recital floor. The floor the next attempt is fought at is
        /// the highest cleared plus one, and that is the floor an attempt pays for.</summary>
        public static long CodaDay(BalanceConfig balance, int highestFloor)
        {
            long perAttempt = balance.RecitalCodaBase
                            + (long)balance.RecitalCodaPerFloor * (Math.Max(0, highestFloor) + 1);
            return AttemptsPerDay(balance.RecitalEchoesMax, balance.RecitalEchoesRechargeHours) * Math.Max(0L, perAttempt);
        }

        /// <summary>One day of income in the shop's own currency, for this player.</summary>
        public static long DayOf(GameSession session, SinkShopId shop)
        {
            if (shop == SinkShopId.Armory) return ValorDay(session.Balance, session.Profile.ProvingHighestStanding);
            return CodaDay(session.Balance, session.Profile.RecitalHighestFloor);
        }

        /// <summary>The price of an item right now: its days of income, rounded UP to the file's
        /// price step, never below one step.</summary>
        public static long Price(GameSession session, SinkItemDef item)
        {
            int step = Math.Max(1, session.Content.Sinks.PriceRound);
            double raw = item.Days * DayOf(session, item.Shop);
            long steps = (long)Math.Ceiling(raw / step - 1e-9);
            return Math.Max(1L, steps) * step;
        }
    }
}
