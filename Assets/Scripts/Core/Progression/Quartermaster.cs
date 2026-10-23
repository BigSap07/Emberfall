using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Why a Quartermaster purchase was refused. None is a sale.</summary>
    public enum QuartermasterRefusal
    {
        None,
        /// <summary>QuartermasterSwitch is off, or quartermaster.json or armor.json holds nothing.</summary>
        Off,
        /// <summary>The shop is not open yet: 3-12 is not cleared (Feature.Quartermaster, FeatureEntryPoints "screen:Quartermaster").</summary>
        Locked,
        /// <summary>Today's stock has not been rolled yet: the shop was not opened (Quartermaster.Open).</summary>
        Stale,
        /// <summary>No such shelf.</summary>
        NoSuchPiece,
        /// <summary>That shelf was bought today.</summary>
        Sold,
        /// <summary>The Gold is there but part of it is Sealed (SealedState), and rolled armor is a roll.</summary>
        SealedGold,
        /// <summary>The Gold is short of the price.</summary>
        NotEnough
    }

    /// <summary>The outcome of one Quartermaster.Buy.</summary>
    public sealed class QuartermasterResult
    {
        public bool Success;
        public QuartermasterRefusal Reason = QuartermasterRefusal.None;
        /// <summary>What it cost, or would cost (0 when nothing was priced).</summary>
        public long Price;
        /// <summary>The piece that was added to the bag.</summary>
        public ArmorPiece Piece;
    }

    /// <summary>One shelf as a screen shows it. Read only: a screen never works any of this out.</summary>
    public sealed class QuartermasterSlotView
    {
        public int Index;
        public ArmorPiece Piece;
        public long Price;
        /// <summary>The price in hours of idle Gold at the frontier, for the card to state.</summary>
        public double Hours;
        public bool Sold;
        /// <summary>What buying it now would be refused for, or None when it is for sale.</summary>
        public QuartermasterRefusal Blocked = QuartermasterRefusal.None;
    }

    /// <summary>The whole shop as a screen shows it.</summary>
    public sealed class QuartermasterView
    {
        /// <summary>False when the switch is off or the content sells nothing: draw nothing.</summary>
        public bool Active;
        /// <summary>False until 3-12 is cleared.</summary>
        public bool Open;
        /// <summary>True when today's stock has not been rolled, until Quartermaster.Open is called.</summary>
        public bool Stale;
        public long Gold;
        public long FreeGold;
        /// <summary>The player's idle Gold per hour at their frontier, which every price is a multiple of.</summary>
        public long HourGold;
        /// <summary>When the shelf is restocked (unix seconds): the next reward day.</summary>
        public long ResetUnix;
        public readonly List<QuartermasterSlotView> Slots = new List<QuartermasterSlotView>();
    }

    /// <summary>
    /// THE QUARTERMASTER (Docs/DESIGN-QUARTERMASTER.md): a daily stock of rolled armor, one piece per
    /// slot, sold for Gold. Engine free.
    ///
    /// THE GOLD IS EARNED. Rolled armor is a roll, so it is paid for in FREE Gold only
    /// (PlayerProfile.FreeGold): a Sealed part of the purse never buys a piece (SealedState). Nothing
    /// else is read or written but the Gold purse, the bag and the shelf.
    ///
    /// PRICES ARE HOURS OF THE PLAYER'S OWN IDLE GOLD AT THEIR FRONTIER (PriceOf), worked out at
    /// display and at purchase from one function and never stored, so a retune of quartermaster.json
    /// or of the economy moves every price with it. The stock is the opposite: stored as resolved
    /// pieces (QuartermasterState), so a retune of armor.json never rerolls the shelf a player is
    /// looking at.
    ///
    /// The stock is rolled once per reward day, when the shop is opened (Open), from a seed made of
    /// the day and the shelf. The stamp only moves forward, so a clock wound back finds the later
    /// day's stock still standing and sells it again to nobody: sold stays sold.
    ///
    /// Buy checks everything before it writes anything, then writes once, so a refusal leaves the
    /// profile byte for byte as it was.
    /// </summary>
    public static class Quartermaster
    {
        /// <summary>True when the shop exists at all: the switch is on and both files sell something.</summary>
        public static bool Active(GameSession session)
        {
            return QuartermasterSwitch.Enabled && session != null
                && session.Content.Quartermaster.IsUsable
                && session.Content.Armor != null && session.Content.Armor.IsUsable;
        }

        /// <summary>True when 3-12 is cleared, asked through FeatureEntryPoints.</summary>
        public static bool IsOpen(GameSession session)
        {
            return FeatureEntryPoints.Allows(session.Content, session.Profile, "screen:Quartermaster");
        }

        /// <summary>The idle Gold one hour pays at the player's frontier: the one formula behind the idle
        /// chest, the daily orders and every price here (GameSession.IdleYield).</summary>
        public static long HourGold(GameSession session)
        {
            return session.IdleYield(3600L).Gold;
        }

        /// <summary>
        /// What a piece costs: its temper's hours of idle Gold at the frontier, rounded UP to
        /// priceRound, never below it. Zero for a temper the file prices at nothing (not for sale).
        /// </summary>
        public static long PriceOf(GameSession session, ArmorPiece piece)
        {
            if (session == null || piece == null) return 0;
            QuartermasterConfig cfg = session.Content.Quartermaster;
            double hours = cfg.HoursOf(piece.Temper);
            if (hours <= 0.0) return 0;
            double gold = Math.Ceiling(hours * HourGold(session));
            long round = Math.Max(1, cfg.PriceRound);
            long price = (long)(Math.Ceiling(gold / round) * round);
            return Math.Max(round, price);
        }

        /// <summary>The frontier's global stage index, which tilts the temper roll as depth tilts a drop's.</summary>
        private static int FrontierIndex(GameSession session)
        {
            StageDef s = session.FurthestClearedStage;
            return s != null ? Math.Max(0, s.GlobalIndex) : 0;
        }

        // ------------------------------------------------------------------ the stock

        /// <summary>
        /// Rolls today's stock if the shelf is from an earlier reward day (or was never rolled), and
        /// answers whether it did. Idempotent within a day. Does nothing when the shop is off or the
        /// shop is locked. A screen calls this when it opens; Buy never does, so a purchase can
        /// only ever be of a shelf the player was shown.
        /// </summary>
        public static bool Open(GameSession session, long nowUnix)
        {
            if (!Active(session) || !IsOpen(session)) return false;
            QuartermasterState state = session.Profile.Quartermaster;
            long dayStart = session.DayStartUnix(nowUnix);
            if (dayStart <= state.DayUnix) return false;

            Roll(session, state, dayStart);
            return true;
        }

        private static void Roll(GameSession session, QuartermasterState state, long dayStart)
        {
            ArmorConfig armor = session.Content.Armor;
            QuartermasterConfig cfg = session.Content.Quartermaster;
            int frontier = FrontierIndex(session);

            state.Slots.Clear();
            state.DayUnix = dayStart;
            for (int i = 0; i < cfg.Slots.Count; i++)
            {
                int seed = StockSeed(dayStart, i);
                Aspect aspect = ArmorRoller.RollAspect(seed + 2);
                ArmorTemper temper = ArmorRoller.RollTemper(armor, frontier, seed + 3);
                if (cfg.HoursOf(temper) <= 0.0) temper = LowestPriced(cfg);
                ArmorPiece piece = ArmorRoller.Roll(armor, cfg.Slots[i], temper, aspect, seed + 4);
                if (piece == null) continue;
                piece.Uid = "";
                QuartermasterSlot slot = new QuartermasterSlot();
                slot.Piece = piece;
                state.Slots.Add(slot);
            }
        }

        private static ArmorTemper LowestPriced(QuartermasterConfig cfg)
        {
            foreach (ArmorTemper t in Enum.GetValues(typeof(ArmorTemper)))
                if (cfg.HoursOf(t) > 0.0) return t;
            return ArmorTemper.Rough;
        }

        /// <summary>A stable mix of the reward day and the shelf. Not cryptographic; it exists so a
        /// check can reproduce a day's stock exactly and two days never share one.</summary>
        public static int StockSeed(long dayStartUnix, int shelf)
        {
            unchecked
            {
                return (int)(dayStartUnix / 3600L) * 7919 + shelf * 104729 + 31337;
            }
        }

        // ------------------------------------------------------------------ the view

        /// <summary>The shelf for a screen to draw. Reads only, and answers Active false with nothing on
        /// it when the shop is off.</summary>
        public static QuartermasterView View(GameSession session, long nowUnix)
        {
            QuartermasterView view = new QuartermasterView();
            if (!Active(session)) return view;
            view.Active = true;
            view.Open = IsOpen(session);
            view.Gold = session.Profile.Gold;
            view.FreeGold = session.Profile.FreeGold;
            view.HourGold = HourGold(session);

            long dayStart = session.DayStartUnix(nowUnix);
            QuartermasterState state = session.Profile.Quartermaster;
            view.ResetUnix = Math.Max(dayStart, state.DayUnix) + 86400L;
            view.Stale = dayStart > state.DayUnix || state.Slots.Count == 0;
            if (!view.Open || view.Stale) return view;

            for (int i = 0; i < state.Slots.Count; i++)
            {
                QuartermasterSlotView v = new QuartermasterSlotView();
                v.Index = i;
                v.Piece = state.Slots[i].Piece;
                v.Price = PriceOf(session, v.Piece);
                v.Hours = session.Content.Quartermaster.HoursOf(v.Piece.Temper);
                v.Sold = state.Slots[i].Sold;
                v.Blocked = Refusal(session, state, i, nowUnix, v.Price);
                view.Slots.Add(v);
            }
            return view;
        }

        /// <summary>The first thing standing between the player and a shelf, in the order Buy checks
        /// them, or None. Reads only.</summary>
        private static QuartermasterRefusal Refusal(GameSession session, QuartermasterState state, int index, long nowUnix, long price)
        {
            if (!Active(session)) return QuartermasterRefusal.Off;
            if (!IsOpen(session)) return QuartermasterRefusal.Locked;
            if (session.DayStartUnix(nowUnix) > state.DayUnix || state.Slots.Count == 0) return QuartermasterRefusal.Stale;
            if (index < 0 || index >= state.Slots.Count) return QuartermasterRefusal.NoSuchPiece;
            if (state.Slots[index].Sold) return QuartermasterRefusal.Sold;
            PlayerProfile p = session.Profile;
            if (p.FreeGold < price) return p.Gold >= price ? QuartermasterRefusal.SealedGold : QuartermasterRefusal.NotEnough;
            return QuartermasterRefusal.None;
        }

        // ------------------------------------------------------------------ the door

        /// <summary>
        /// Buys the piece on one shelf. Every refusal is decided before anything is written; the sale
        /// itself is one block, in this order: take the price from the Gold purse, add a copy of the
        /// piece to the bag under a fresh uid, mark the shelf sold.
        /// </summary>
        public static QuartermasterResult Buy(GameSession session, int index, long nowUnix)
        {
            QuartermasterResult result = new QuartermasterResult();
            if (!Active(session)) { result.Reason = QuartermasterRefusal.Off; return result; }

            QuartermasterState state = session.Profile.Quartermaster;
            ArmorPiece shown = index >= 0 && index < state.Slots.Count ? state.Slots[index].Piece : null;
            result.Price = PriceOf(session, shown);
            QuartermasterRefusal why = Refusal(session, state, index, nowUnix, result.Price);
            if (why != QuartermasterRefusal.None) { result.Reason = why; return result; }

            // ---- the one write block
            PlayerProfile profile = session.Profile;
            profile.Gold -= result.Price;
            ArmorPiece bought = shown.Clone();
            bought.Sealed = false;
            bought.Uid = profile.AllocateArmorUid();
            profile.OwnedArmor.Add(bought);
            state.Slots[index].Sold = true;
            // The spend-Gold order counts this sink too, so a roster whose every hero is at its cap can still finish it.
            if (DailyLoopSwitch.Enabled) DailyLoop.Record(session, OrderKind.Gold, (int)Math.Min(int.MaxValue, result.Price), nowUnix);

            result.Piece = bought;
            result.Success = true;
            return result;
        }
    }
}
