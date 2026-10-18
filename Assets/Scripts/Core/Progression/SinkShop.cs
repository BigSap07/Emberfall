using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Why a purchase was refused. None is a sale.</summary>
    public enum SinkRefusal
    {
        None,
        /// <summary>SinksSwitch is off, or sinks.json holds nothing.</summary>
        Off,
        /// <summary>No such item.</summary>
        Unknown,
        /// <summary>A feature the item needs (the Proving, the Recital, armor) is not open yet.</summary>
        Locked,
        /// <summary>A hero item without a hero from its list.</summary>
        BadChoice,
        /// <summary>The day's or the month's allowance of this item is spent.</summary>
        CapReached,
        /// <summary>An attempt bank that cannot take another attempt.</summary>
        BankFull,
        /// <summary>The purse is short of the price.</summary>
        NotEnough
    }

    /// <summary>The outcome of one SinkShop.Buy.</summary>
    public sealed class SinkResult
    {
        public bool Success;
        public SinkRefusal Reason = SinkRefusal.None;
        /// <summary>What it cost, or would cost (0 when the item is unknown or the shop is off).</summary>
        public long Price;
        /// <summary>The hero that gained a copy, for a hero item.</summary>
        public HeroInstance Hero;
        /// <summary>The piece that was added, for an armor item.</summary>
        public ArmorPiece Armor;
    }

    /// <summary>One item as a screen shows it. Read only: a screen never works any of this out.</summary>
    public sealed class SinkItemView
    {
        public SinkItemDef Item;
        public long Price;
        /// <summary>What buying it now would be refused for, or None when it is for sale.</summary>
        public SinkRefusal Blocked = SinkRefusal.None;
        /// <summary>Units still buyable in the current period, and the period's whole allowance.</summary>
        public int CapLeft;
        public int CapLimit;
        /// <summary>When the allowance comes back (unix seconds), for a spent one.</summary>
        public long ResetUnix;
        /// <summary>Attempts banked and the bank's size, for a Brand or an Echo. Zero otherwise.</summary>
        public int Banked;
        public int BankMax;
    }

    /// <summary>One shop as a screen shows it.</summary>
    public sealed class SinkShopView
    {
        public bool Active;
        public SinkShopId Shop;
        /// <summary>The shop's purse: Valor for the Armory, Coda for the Archive.</summary>
        public long Purse;
        /// <summary>One day of this player's income in that currency, which every price is a multiple of.</summary>
        public long Day;
        public readonly List<SinkItemView> Items = new List<SinkItemView>();
    }

    /// <summary>
    /// The Armory (Valor) and the Archive (Coda): what they show and the one door that sells
    /// (DESIGN-SINKS.md). Engine free, and NOTHING IN HERE IS A DRAW: an item is a fixed armor
    /// preset, one attempt back in a bank, or one copy of a hero the player chose. Earned currency
    /// only, one purse per shop.
    ///
    /// Buy checks everything before it writes anything, then writes once, in a fixed order, so a
    /// refusal leaves the profile byte for byte as it was. Only the shop's own purse falls;
    /// no other currency is read or written here.
    ///
    /// The caps count in reward days (GameSession.DayStartUnix) and in calendar months of the
    /// reward day, both UTC. The stamps only move forward (SinksState), so a clock wound back finds
    /// the later period's counters still standing and reopens nothing.
    /// </summary>
    public static class SinkShop
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>The latest unix time a DateTime can be built from, minus a day of room.</summary>
        private const long UnixLimit = 253402214400L;

        /// <summary>True when the shops are open at all: the switch is on and sinks.json sells something.</summary>
        public static bool Active(GameSession session)
        {
            return SinksSwitch.Enabled && session != null && session.Content.Sinks.IsUsable;
        }

        // ------------------------------------------------------------------ the calendar

        /// <summary>The month a reward day belongs to: year x 12 + month - 1, UTC.</summary>
        public static long MonthKeyOf(long dayStartUnix)
        {
            DateTime d = Epoch.AddSeconds(Math.Max(0L, Math.Min(UnixLimit, dayStartUnix)));
            return d.Year * 12L + d.Month - 1;
        }

        /// <summary>When a monthly allowance comes back: the first reward day of the next month.</summary>
        public static long NextMonthUnix(GameSession session, long dayStartUnix)
        {
            DateTime d = Epoch.AddSeconds(Math.Max(0L, Math.Min(UnixLimit, dayStartUnix)));
            DateTime first = new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)
                                 .AddHours(Math.Max(0, Math.Min(23, session.Balance.DailyResetHourUtc)));
            return (long)(first - Epoch).TotalSeconds;
        }

        /// <summary>Units of a cap key bought in the period nowUnix is in, read without writing: a
        /// counter stamped for an earlier period reads as zero, one stamped for a later period (a
        /// wound-back clock) still counts.</summary>
        public static int Bought(GameSession session, SinkItemDef item, long nowUnix)
        {
            SinksState state = session.Profile.Sinks;
            long dayStart = session.DayStartUnix(nowUnix);
            if (item.CapPer == SinkCapPer.Day)
                return dayStart > state.DayUnix ? 0 : SinksState.CountOf(state.Daily, item.CapKey);
            return MonthKeyOf(dayStart) > state.MonthKey ? 0 : SinksState.CountOf(state.Monthly, item.CapKey);
        }

        // ------------------------------------------------------------------ rules

        /// <summary>The entry point whose feature a shop belongs to: the Armory opens with the
        /// Proving, the Archive with the Recital (FeatureEntryPoints).</summary>
        private static string ShopEntry(SinkShopId shop)
        {
            return shop == SinkShopId.Armory ? "screen:Armory" : "screen:Archive";
        }

        /// <summary>True when every feature the item needs is open: its shop's, the bank's it fills
        /// and the armor bag's it feeds. Asked through FeatureEntryPoints, the one table that
        /// decides an unlock.</summary>
        private static bool IsOpen(GameSession session, SinkItemDef item)
        {
            ContentDatabase content = session.Content;
            PlayerProfile profile = session.Profile;
            if (!FeatureEntryPoints.Allows(content, profile, ShopEntry(item.Shop))) return false;
            if (item.Kind == SinkKind.Brand && !FeatureEntryPoints.Allows(content, profile, "screen:Proving")) return false;
            if (item.Kind == SinkKind.Echo && !FeatureEntryPoints.Allows(content, profile, "screen:Recital")) return false;
            if (item.Kind == SinkKind.Armor && !FeatureEntryPoints.Allows(content, profile, "screen:Inventory")) return false;
            return true;
        }

        private static long PurseOf(GameSession session, SinkShopId shop)
        {
            return shop == SinkShopId.Armory ? session.Profile.Valor : session.Profile.Coda;
        }

        /// <summary>The bank an item fills, as attempts banked now and the bank's size.</summary>
        private static void BankOf(GameSession session, SinkItemDef item, long nowUnix, out int banked, out int max)
        {
            banked = 0;
            max = 0;
            if (item.Kind == SinkKind.Brand)
            {
                ProvingBrandsStatus s = session.PeekProvingBrands(nowUnix);
                banked = s.Banked;
                max = s.Max;
            }
            else if (item.Kind == SinkKind.Echo)
            {
                RecitalEchoesStatus s = session.PeekRecitalEchoes(nowUnix);
                banked = s.Banked;
                max = s.Max;
            }
        }

        /// <summary>The first thing standing between the player and an item, in the order Buy
        /// checks them, or None. Reads only.</summary>
        private static SinkRefusal Refusal(GameSession session, SinkItemDef item, string choice, long nowUnix, long price)
        {
            if (!IsOpen(session, item)) return SinkRefusal.Locked;
            if (Bought(session, item, nowUnix) >= item.CapLimit) return SinkRefusal.CapReached;
            if (item.Kind == SinkKind.HeroCopy && (string.IsNullOrEmpty(choice) || !item.Heroes.Contains(choice)
                                                   || session.Content.GetHero(choice) == null))
                return SinkRefusal.BadChoice;
            if (item.Kind == SinkKind.Brand || item.Kind == SinkKind.Echo)
            {
                int banked, max;
                BankOf(session, item, nowUnix, out banked, out max);
                if (max <= 0 || banked >= max) return SinkRefusal.BankFull;
            }
            if (PurseOf(session, item.Shop) < price) return SinkRefusal.NotEnough;
            return SinkRefusal.None;
        }

        // ------------------------------------------------------------------ the view

        /// <summary>One shop's shelf, for a screen to draw. Reads only, and answers Active false
        /// with an empty shelf when the shops are off.</summary>
        public static SinkShopView View(GameSession session, SinkShopId shop, long nowUnix)
        {
            SinkShopView view = new SinkShopView();
            view.Shop = shop;
            if (!Active(session)) return view;
            view.Active = true;
            view.Purse = PurseOf(session, shop);
            view.Day = SinkPricing.DayOf(session, shop);

            long dayStart = session.DayStartUnix(nowUnix);
            List<SinkItemDef> items = session.Content.Sinks.ItemsOf(shop);
            for (int i = 0; i < items.Count; i++)
            {
                SinkItemDef item = items[i];
                SinkItemView v = new SinkItemView();
                v.Item = item;
                v.Price = SinkPricing.Price(session, item);
                v.CapLimit = item.CapLimit;
                v.CapLeft = Math.Max(0, item.CapLimit - Bought(session, item, nowUnix));
                v.ResetUnix = item.CapPer == SinkCapPer.Day ? dayStart + 86400L : NextMonthUnix(session, dayStart);
                BankOf(session, item, nowUnix, out v.Banked, out v.BankMax);

                // A hero item has no choice yet; the screen asks for one, so it is judged on the
                // first hero it lists and a BadChoice never shows on a shelf.
                string probe = item.Kind == SinkKind.HeroCopy && item.Heroes.Count > 0 ? item.Heroes[0] : null;
                v.Blocked = Refusal(session, item, probe, nowUnix, v.Price);
                view.Items.Add(v);
            }
            return view;
        }

        // ------------------------------------------------------------------ the door

        /// <summary>
        /// Buys one unit of an item. choice is the hero id for a hero item and ignored otherwise.
        /// Every refusal is decided before anything is written; the sale itself is one block, in
        /// this order: roll the stamps forward, take the price from the shop's purse, grant, count.
        /// </summary>
        public static SinkResult Buy(GameSession session, string itemId, string choice, long nowUnix)
        {
            SinkResult result = new SinkResult();
            if (!Active(session)) { result.Reason = SinkRefusal.Off; return result; }
            SinkItemDef item = session.Content.Sinks.Item(itemId);
            if (item == null) { result.Reason = SinkRefusal.Unknown; return result; }

            result.Price = SinkPricing.Price(session, item);
            SinkRefusal why = Refusal(session, item, choice, nowUnix, result.Price);
            if (why != SinkRefusal.None) { result.Reason = why; return result; }

            // ---- the one write block
            PlayerProfile profile = session.Profile;
            SinksState state = profile.Sinks;
            long dayStart = session.DayStartUnix(nowUnix);
            if (dayStart > state.DayUnix)
            {
                state.DayUnix = dayStart;
                state.Daily.Clear();
            }
            long month = MonthKeyOf(dayStart);
            if (month > state.MonthKey)
            {
                state.MonthKey = month;
                state.Monthly.Clear();
            }

            if (item.Shop == SinkShopId.Armory) profile.Valor -= result.Price;
            else profile.Coda -= result.Price;

            if (item.Kind == SinkKind.Armor)
            {
                result.Armor = BuildArmor(profile, item.Armor);
                profile.OwnedArmor.Add(result.Armor);
            }
            else if (item.Kind == SinkKind.HeroCopy)
            {
                result.Hero = session.GrantHeroDirect(session.Content.GetHero(choice), nowUnix);
            }
            else if (item.Kind == SinkKind.Brand)
            {
                session.AddProvingBrand(nowUnix);
            }
            else if (item.Kind == SinkKind.Echo)
            {
                session.AddRecitalEcho(nowUnix);
            }

            SinksState.Bump(item.CapPer == SinkCapPer.Day ? state.Daily : state.Monthly, item.CapKey);
            result.Success = true;
            return result;
        }

        /// <summary>A preset as an owned piece: the stats exactly as sinks.json fixes them, a fresh uid.</summary>
        private static ArmorPiece BuildArmor(PlayerProfile profile, SinkArmorSpec spec)
        {
            ArmorPiece piece = new ArmorPiece();
            piece.Uid = profile.AllocateArmorUid();
            piece.Name = spec.Name;
            piece.Slot = spec.Slot;
            piece.Aspect = spec.Aspect;
            piece.Temper = spec.Temper;
            piece.Primary = new ArmorStatRoll(spec.Primary.Stat, spec.Primary.Mode, spec.Primary.Value);
            for (int i = 0; i < spec.Subs.Count; i++)
                piece.SubStats.Add(new ArmorStatRoll(spec.Subs[i].Stat, spec.Subs[i].Mode, spec.Subs[i].Value));
            return piece;
        }
    }
}
