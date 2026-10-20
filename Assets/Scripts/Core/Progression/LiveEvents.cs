using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Why a live event call was refused. None is success.</summary>
    public enum LiveRefusal
    {
        None,
        /// <summary>The switch is off or the file holds nothing.</summary>
        Off,
        /// <summary>No event runs in that slot.</summary>
        NoEvent,
        /// <summary>No such quest or tier.</summary>
        Unknown,
        /// <summary>The quest is not finished, or the tier's points are not reached.</summary>
        NotReached,
        /// <summary>Already claimed.</summary>
        Claimed,
        /// <summary>The tier has no reward on that track.</summary>
        NoReward,
        /// <summary>The paid track is not bought for this event, or the Store is off.</summary>
        PaidLocked
    }

    /// <summary>The outcome of a quest claim or a tier claim.</summary>
    public sealed class LiveResult
    {
        public bool Success;
        public LiveRefusal Reason = LiveRefusal.None;
        public int Points;
        public long Gold, Shards, Dust, Slag;
        /// <summary>The skin a free tier granted, or empty.</summary>
        public string SkinId = "";
    }

    /// <summary>One quest as the screen draws it. Read only.</summary>
    public sealed class LiveQuestView
    {
        public string Id = "";
        public string Line = "";
        public int Progress;
        public int Target = 1;
        public int Points;
        public bool Done;
        public bool Claimed;
        public bool CanClaim;
    }

    /// <summary>One rung of the track as the screen draws it. Read only.</summary>
    public sealed class LiveTierView
    {
        public int Index;
        public int Points;
        public bool HasFree;
        public string FreeLine = "";
        /// <summary>The same reward in few words, for a narrow tile: "3 h idle Gold", "2 sweeps Dust", "5 Slag".</summary>
        public string FreeShort = "";
        public bool HasPaid;
        public string PaidLine = "";
        public string PaidShort = "";
        public bool Reached;
        public bool FreeClaimed;
        public bool FreeCanClaim;
        public bool PaidClaimed;
        public bool PaidCanClaim;
    }

    /// <summary>One slot as the screen draws it. Every number and every sentence is read off here.</summary>
    public sealed class LiveEventView
    {
        public LiveEventSlot Slot;
        public bool Active;
        public string EventId = "";
        public string Name = "";
        public string Blurb = "";
        public Aspect Aspect;
        public string PointsLabel = "";
        public long Period;
        public int DaysLeft;
        public long ResetInSeconds;
        public int Points;
        public int MaxPoints;
        public readonly List<LiveQuestView> Quests = new List<LiveQuestView>();
        public readonly List<LiveTierView> Tiers = new List<LiveTierView>();
        public bool TrackOwned;
        public bool PackOwned;
        /// <summary>The store offers that sell the track and the pack, with their price tags. Empty when the
        /// Store does not list them.</summary>
        public string TrackOfferId = "";
        public string TrackPrice = "";
        public bool TrackCanBuy;
        public string PackOfferId = "";
        public string PackName = "";
        public string PackBlurb = "";
        public string PackPrice = "";
        public bool PackCanBuy;
        /// <summary>The full fixed contents of the pack, in words, before it is bought.</summary>
        public readonly List<string> PackLines = new List<string>();
        /// <summary>What the whole paid track gives, summed, in words, before it is bought. Every tier is listed on the track itself.</summary>
        public readonly List<string> TrackLines = new List<string>();
        public bool AnythingToClaim;
    }

    /// <summary>
    /// THE LIVE EVENTS' RULES (the events section of Docs/DESIGN-STORE.md): a weekly event (seven days, Monday
    /// to Sunday by reward day) and a monthly event (28 days from the file's epoch), each with a quest list,
    /// event points, a free reward track and a paid Event Track, and one fixed event pack. Pure rules over a
    /// GameSession's profile and content; the screen only calls these and reads LiveEventView.
    ///
    /// QUESTS reuse the daily orders' counting: GameSession.OrderEvent hands every order kind here at the call
    /// site that counts it, and the kinds orders do not have (Proving, SpendGold, Ascend, ClaimOrder, AspectWin)
    /// are counted at their own. A quest claim pays event points; points open the tiers.
    ///
    /// ONE CLOCK, FORWARD ONLY. The period is the reward day's week or 28 day block, and LiveEventSlotState.Period
    /// only moves forward: a device clock wound back reads as the period already stored, so it reopens no quest,
    /// no tier, no track and no pack, and a clock jumped forward lands in the period it names and pays nothing
    /// for the ones it skipped. Every read uses the later of the real period and the stored one and writes nothing.
    ///
    /// THE RULE. The free track pays plain earned goods. The PAID track and the PACK are bought, so everything
    /// they give goes through PurchaseGrants.Apply and is Sealed: the track is unlocked by the EventTrack grant,
    /// each paid tier is claimed through Apply, and the pack's contents are applied by it. Every reward is a
    /// fixed number in live-events.json; this file has no chance in it.
    ///
    /// OFF MEANS INERT. With LiveEventsSwitch off, or with no usable live-events.json, every call returns at
    /// once and writes nothing to the profile.
    /// </summary>
    public static class LiveEvents
    {
        public static bool Active(GameSession session)
        {
            return LiveEventsSwitch.Enabled && session != null && session.Content.LiveEvents.IsUsable;
        }

        // ------------------------------------------------------------------ periods

        /// <summary>The period a clock reading falls in: a week index (Monday to Sunday) for the weekly slot, a
        /// 28 day block counted from the epoch for the monthly slot (1 for the first). Both are 0 before the epoch.</summary>
        public static long PeriodOf(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            long dayStart = session.DayStartUnix(nowUnix);
            LiveEventsConfig cfg = session.Content.LiveEvents;
            long rel = DailyLoop.DayNumber(session, dayStart) - cfg.EpochDay;
            if (rel < 0) return 0L;   // no event runs before the file's epoch, in either slot
            return slot == LiveEventSlot.Weekly ? DailyLoop.WeekOf(session, dayStart) : rel / cfg.MonthlyDays + 1L;
        }

        /// <summary>The first reward day (unix seconds) of the period after this one.</summary>
        public static long PeriodEndUnix(GameSession session, LiveEventSlot slot, long period)
        {
            long offset = (long)session.Balance.DailyResetHourUtc * 3600L;
            if (slot == LiveEventSlot.Weekly) return (period * 7L - 3L) * 86400L + offset;
            LiveEventsConfig cfg = session.Content.LiveEvents;
            return (cfg.EpochDay + period * cfg.MonthlyDays) * 86400L + offset;
        }

        /// <summary>The slot's state as of a clock reading, WITHOUT writing: the stored state when it is of this
        /// period or a later one (a wound back clock), else an empty state of the period the clock names.</summary>
        private static LiveEventSlotState Now(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            LiveEventSlotState st = session.Profile.LiveEvents.Of(slot);
            long real = PeriodOf(session, slot, nowUnix);
            if (real <= st.Period) return st;
            LiveEventSlotState fresh = new LiveEventSlotState();
            fresh.Period = real;
            LiveEventDef ev = session.Content.LiveEvents.EventFor(slot, real);
            fresh.EventId = ev != null ? ev.Id : "";
            return fresh;
        }

        /// <summary>The event a slot is running as of a clock reading (null when the slot has none, or its event
        /// has left the file).</summary>
        public static LiveEventDef EventNow(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            if (!Active(session)) return null;
            return session.Content.LiveEvents.Event(Now(session, slot, nowUnix).EventId);
        }

        /// <summary>
        /// Moves a slot to the period nowUnix falls in, and returns true when that changed anything. A later
        /// period starts empty (unclaimed rewards lapse, the track and the pack are for sale again); the same
        /// period or an earlier one (a clock wound back) changes nothing at all.
        /// </summary>
        public static bool Roll(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            if (!Active(session)) return false;
            LiveEventSlotState st = session.Profile.LiveEvents.Of(slot);
            long real = PeriodOf(session, slot, nowUnix);
            if (real <= st.Period) return false;
            LiveEventDef ev = session.Content.LiveEvents.EventFor(slot, real);
            if (ev == null) return false;
            st.Clear();
            st.Period = real;
            st.EventId = ev.Id;
            return true;
        }

        private static void EnsureProgress(LiveEventSlotState st, LiveEventDef ev)
        {
            while (st.Progress.Count < ev.Quests.Count) st.Progress.Add(0);
        }

        // ------------------------------------------------------------------ counting

        /// <summary>Counts an order kind toward the quests of that kind. Called by GameSession.OrderEvent at the
        /// moment the thing happens, never by a screen.</summary>
        public static void RecordOrder(GameSession session, OrderKind kind, int amount, long nowUnix)
        {
            switch (kind)
            {
                case OrderKind.Waves: Record(session, EventQuestKind.Waves, amount, nowUnix); break;
                case OrderKind.Stage: Record(session, EventQuestKind.Stage, amount, nowUnix); break;
                case OrderKind.Level: Record(session, EventQuestKind.Level, amount, nowUnix); break;
                case OrderKind.Summon: Record(session, EventQuestKind.Summon, amount, nowUnix); break;
                case OrderKind.Arena: Record(session, EventQuestKind.Arena, amount, nowUnix); break;
            }
        }

        /// <summary>Counts amount of kind toward the quests of that kind in both slots, capped at each target.
        /// A slot with no quest of that kind is left alone, so an action no quest counts writes nothing.</summary>
        public static void Record(GameSession session, EventQuestKind kind, long amount, long nowUnix)
        {
            if (!Active(session) || amount <= 0) return;
            RecordSlot(session, LiveEventSlot.Weekly, kind, amount, nowUnix, false);
            RecordSlot(session, LiveEventSlot.Monthly, kind, amount, nowUnix, false);
        }

        /// <summary>Counts one stage won toward the AspectWin quests whose event aspect the team holds enough
        /// heroes of. Called by GameSession where a stage win is counted.</summary>
        public static void RecordStageWin(GameSession session, long nowUnix)
        {
            if (!Active(session)) return;
            RecordSlot(session, LiveEventSlot.Weekly, EventQuestKind.AspectWin, 1, nowUnix, true);
            RecordSlot(session, LiveEventSlot.Monthly, EventQuestKind.AspectWin, 1, nowUnix, true);
        }

        private static int HeroesOfAspect(GameSession session, Aspect aspect)
        {
            int n = 0;
            List<HeroInstance> team = session.Profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                HeroDef def = team[i].Def != null ? team[i].Def : session.Content.GetHero(team[i].DefId);
                if (def != null && def.Aspect == aspect) n++;
            }
            return n;
        }

        private static void RecordSlot(GameSession session, LiveEventSlot slot, EventQuestKind kind, long amount, long nowUnix, bool needTeam)
        {
            LiveEventDef ev = session.Content.LiveEvents.Event(Now(session, slot, nowUnix).EventId);
            if (ev == null) return;
            bool any = false;
            for (int i = 0; i < ev.Quests.Count && !any; i++) any = ev.Quests[i].Kind == kind;
            if (!any) return;

            int heldOfAspect = needTeam ? HeroesOfAspect(session, ev.Aspect) : 0;
            Roll(session, slot, nowUnix);
            LiveEventSlotState st = session.Profile.LiveEvents.Of(slot);
            if (st.Period == 0 || st.EventId != ev.Id) return;
            EnsureProgress(st, ev);
            for (int i = 0; i < ev.Quests.Count; i++)
            {
                EventQuestDef q = ev.Quests[i];
                if (q.Kind != kind || LiveEventSlotState.Bit(st.QuestMask, i)) continue;
                if (needTeam && heldOfAspect < q.MinHeroes) continue;
                st.Progress[i] = (int)Math.Min((long)q.Target, (long)st.Progress[i] + amount);
            }
        }

        // ------------------------------------------------------------------ claims

        private static LiveResult Refuse(LiveRefusal why)
        {
            LiveResult r = new LiveResult();
            r.Reason = why;
            return r;
        }

        /// <summary>Claims a finished quest, once: its event points join the slot's total.</summary>
        public static LiveResult ClaimQuest(GameSession session, LiveEventSlot slot, string questId, long nowUnix)
        {
            if (!Active(session)) return Refuse(LiveRefusal.Off);
            LiveEventDef ev = session.Content.LiveEvents.Event(Now(session, slot, nowUnix).EventId);
            if (ev == null) return Refuse(LiveRefusal.NoEvent);
            int index = ev.QuestIndex(questId);
            if (index < 0) return Refuse(LiveRefusal.Unknown);
            Roll(session, slot, nowUnix);
            LiveEventSlotState st = session.Profile.LiveEvents.Of(slot);
            if (st.EventId != ev.Id) return Refuse(LiveRefusal.NoEvent);
            EnsureProgress(st, ev);
            EventQuestDef q = ev.Quests[index];
            if (LiveEventSlotState.Bit(st.QuestMask, index)) return Refuse(LiveRefusal.Claimed);
            if (st.Progress[index] < q.Target) return Refuse(LiveRefusal.NotReached);

            st.QuestMask |= 1 << index;
            st.Points = Math.Min(100000, st.Points + q.Points);
            LiveResult r = new LiveResult();
            r.Success = true;
            r.Points = q.Points;
            return r;
        }

        /// <summary>The skin a Skin reward would grant right now, or null when it cannot (unknown, hidden in this build, or
        /// already owned): the fallback is then what is granted and shown.</summary>
        private static SkinDef GrantableSkin(GameSession session, EventRewardDef d)
        {
            if (d.Kind != EventRewardKind.Skin) return null;
            SkinDef skin = session.Content.Skins.Skin(d.SkinId);
            return skin != null && SkinsConfig.Visible(skin) && !session.Profile.OwnsSkin(skin.Id) ? skin : null;
        }

        private static string RewardLine(GameSession session, EventRewardDef d, bool sealedGood)
        {
            if (d.Kind == EventRewardKind.Skin)
            {
                SkinDef skin = GrantableSkin(session, d);
                return skin != null ? "The skin " + skin.Name + ": " + skin.BonusLine() : RewardLine(session, d.Fallback, sealedGood);
            }
            switch (d.Kind)
            {
                case EventRewardKind.Hourglass:
                    return PurchaseGrants.Describe(new GrantItem(GrantKind.IdleGoldHours, d.Amount), session) + (sealedGood ? " (Sealed)" : "");
                case EventRewardKind.Dust:
                    return PurchaseGrants.Describe(new GrantItem(GrantKind.DustSweeps, d.Amount), session);
                case EventRewardKind.Slag:
                    return PurchaseGrants.Describe(new GrantItem(GrantKind.Slag, d.Amount), session) + (sealedGood ? " (Sealed)" : "");
            }
            return "";
        }

        private static string Num(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string RewardShort(GameSession session, EventRewardDef d)
        {
            if (d.Kind == EventRewardKind.Skin)
            {
                SkinDef skin = GrantableSkin(session, d);
                return skin != null ? "Skin: " + skin.Name : RewardShort(session, d.Fallback);
            }
            switch (d.Kind)
            {
                case EventRewardKind.Hourglass: return Num(d.Amount) + " h idle Gold";
                case EventRewardKind.Dust: return Num(d.Amount) + (d.Amount == 1.0 ? " sweep Dust" : " sweeps Dust");
                case EventRewardKind.Slag: return Num(d.Amount) + " Slag";
            }
            return "";
        }

        /// <summary>Claims the FREE reward of a reached tier (a 1 based index). Plain earned goods, never Sealed.</summary>
        public static LiveResult ClaimFree(GameSession session, LiveEventSlot slot, int tier, long nowUnix)
        {
            if (!Active(session)) return Refuse(LiveRefusal.Off);
            LiveEventDef ev = session.Content.LiveEvents.Event(Now(session, slot, nowUnix).EventId);
            if (ev == null) return Refuse(LiveRefusal.NoEvent);
            if (tier < 1 || tier > ev.Tiers.Count) return Refuse(LiveRefusal.Unknown);
            EventTierDef t = ev.Tiers[tier - 1];
            if (t.Free == null) return Refuse(LiveRefusal.NoReward);
            Roll(session, slot, nowUnix);
            LiveEventSlotState st = session.Profile.LiveEvents.Of(slot);
            if (st.EventId != ev.Id) return Refuse(LiveRefusal.NoEvent);
            if (LiveEventSlotState.Bit(st.FreeMask, tier - 1)) return Refuse(LiveRefusal.Claimed);
            if (st.Points < t.Points) return Refuse(LiveRefusal.NotReached);

            LiveResult r = new LiveResult();
            PlayerProfile p = session.Profile;
            EventRewardDef pay = t.Free;
            if (pay.Kind == EventRewardKind.Skin)
            {
                // A skin goes through the one grant path (idempotent, never random); when it cannot be granted the
                // data's own fallback is paid instead, the same reward the tier showed.
                SkinDef skin = GrantableSkin(session, pay);
                if (skin != null)
                {
                    GrantItem item = new GrantItem(GrantKind.Skin, 0);
                    item.SkinId = skin.Id;
                    PurchaseGrant grant = new PurchaseGrant();
                    grant.Add(item);
                    if (PurchaseGrants.Apply(session, grant, null, nowUnix).Success) r.SkinId = skin.Id;
                }
                pay = r.SkinId.Length > 0 ? null : pay.Fallback;
            }
            if (pay != null)
            {
                switch (pay.Kind)
                {
                    case EventRewardKind.Hourglass:
                        IdleReport y = session.IdleYield((long)Math.Round(pay.Amount * 3600.0));
                        p.Gold += y.Gold;
                        p.Shards += y.Shards;
                        r.Gold = y.Gold;
                        r.Shards = y.Shards;
                        break;
                    case EventRewardKind.Dust:
                        r.Dust = (long)Math.Round(pay.Amount * WayfarersRoad.DustPerSweep(session));
                        p.RelicDust += r.Dust;
                        break;
                    case EventRewardKind.Slag:
                        r.Slag = (long)pay.Amount;
                        p.Slag += r.Slag;
                        break;
                }
            }
            st.FreeMask |= 1 << (tier - 1);
            r.Success = true;
            return r;
        }

        private static PurchaseGrant GrantOf(GameSession session, EventRewardDef d)
        {
            PurchaseGrant g = new PurchaseGrant();
            switch (d.Kind)
            {
                case EventRewardKind.Hourglass: g.Add(new GrantItem(GrantKind.IdleGoldHours, d.Amount)); break;
                case EventRewardKind.Dust: g.Add(new GrantItem(GrantKind.DustSweeps, d.Amount)); break;
                case EventRewardKind.Slag: g.Add(new GrantItem(GrantKind.Slag, Math.Floor(d.Amount))); break;
            }
            return g;
        }

        /// <summary>Claims the PAID reward of a reached tier. Open only when the Store is on and the Event Track of
        /// this period was bought; otherwise it refuses PaidLocked and writes nothing, not even a roll. Paid
        /// through PurchaseGrants.Apply like every purchase, so it is SEALED.</summary>
        public static LiveResult ClaimPaid(GameSession session, LiveEventSlot slot, int tier, long nowUnix)
        {
            if (!Active(session)) return Refuse(LiveRefusal.Off);
            if (!StoreSwitch.Enabled) return Refuse(LiveRefusal.PaidLocked);
            LiveEventSlotState now = Now(session, slot, nowUnix);
            LiveEventDef ev = session.Content.LiveEvents.Event(now.EventId);
            if (ev == null) return Refuse(LiveRefusal.NoEvent);
            if (!now.TrackBought) return Refuse(LiveRefusal.PaidLocked);
            if (tier < 1 || tier > ev.Tiers.Count) return Refuse(LiveRefusal.Unknown);
            EventTierDef t = ev.Tiers[tier - 1];
            if (t.Paid == null) return Refuse(LiveRefusal.NoReward);
            if (LiveEventSlotState.Bit(now.PaidMask, tier - 1)) return Refuse(LiveRefusal.Claimed);
            if (now.Points < t.Points) return Refuse(LiveRefusal.NotReached);

            GrantResult g = PurchaseGrants.Apply(session, GrantOf(session, t.Paid), null, nowUnix);
            if (!g.Success) return Refuse(LiveRefusal.NoReward);
            session.Profile.LiveEvents.Of(slot).PaidMask |= 1 << (tier - 1);
            LiveResult r = new LiveResult();
            r.Success = true;
            r.Gold = g.Gold;
            r.Dust = g.Dust;
            r.Slag = g.Slag;
            return r;
        }

        // ------------------------------------------------------------------ what is sold

        /// <summary>Why the track or the pack of a slot cannot be bought now, in the Store's own words. None means
        /// it can. Reads, writes nothing.</summary>
        public static StoreRefusal PurchaseRefusal(GameSession session, LiveEventSlot slot, LiveEventPart part, long nowUnix)
        {
            if (!Active(session)) return StoreRefusal.Off;
            LiveEventSlotState st = Now(session, slot, nowUnix);
            LiveEventDef ev = session.Content.LiveEvents.Event(st.EventId);
            if (ev == null) return StoreRefusal.NoEvent;
            if (part == LiveEventPart.Pack)
            {
                if (ev.Pack == null) return StoreRefusal.NoEvent;
                return st.PackBought ? StoreRefusal.SoldOut : StoreRefusal.None;
            }
            return st.TrackBought ? StoreRefusal.SoldOut : StoreRefusal.None;
        }

        // ------------------------------------------------------------------ delivering a payment

        /// <summary>The key a purchase in flight is remembered under: its slot and its part.</summary>
        public static int PendingKey(LiveEventSlot slot, LiveEventPart part)
        {
            return (int)slot * 2 + (int)part;
        }

        /// <summary>Called when a purchase of the track or the pack of a slot is BEGUN (StoreOffers.OnBegin, IN MEMORY ONLY):
        /// remembers the event and period it was begun for, so a payment that arrives after the period turned over is
        /// delivered against the event the buyer saw, not the one that runs when it lands.</summary>
        public static void Bind(GameSession session, LiveEventSlot slot, LiveEventPart part, long nowUnix)
        {
            if (!Active(session)) return;
            LiveEventSlotState st = Now(session, slot, nowUnix);
            LivePending p = new LivePending();
            p.EventId = st.EventId;
            p.Period = st.Period;
            session.Profile.LiveEvents.Pending[PendingKey(slot, part)] = p;
        }

        /// <summary>Forgets the remembered purchase once it is delivered.</summary>
        public static void Unbind(GameSession session, LiveEventSlot slot, LiveEventPart part)
        {
            session.Profile.LiveEvents.Pending.Remove(PendingKey(slot, part));
        }

        /// <summary>What a payment for the track or the pack of a slot delivers.</summary>
        public sealed class Delivery
        {
            /// <summary>False only when there is nothing in live-events.json to deliver. A charged payment is otherwise ALWAYS
            /// delivered.</summary>
            public bool Possible;
            /// <summary>True when the payment lands on the event it was begun for, which is running and has not got that part
            /// yet: the part is unlocked or the pack is marked bought. False is a STALE delivery: the goods below are paid out
            /// in full and nothing is marked.</summary>
            public bool Normal;
            /// <summary>The fixed goods to apply: the pack's, or (stale only) the track's paid rewards summed. Null for a normal
            /// track, whose rewards are claimed tier by tier.</summary>
            public PurchaseGrant Goods;
            /// <summary>A sentence for the receipt line of a stale delivery.</summary>
            public string Note = "";
        }

        private static PurchaseGrant PackGoods(LiveEventDef ev)
        {
            if (ev == null || ev.Pack == null) return null;
            PurchaseGrant g = new PurchaseGrant();
            if (ev.Pack.Hours > 0.0) g.Add(new GrantItem(GrantKind.IdleGoldHours, ev.Pack.Hours));
            if (ev.Pack.DustSweeps > 0.0) g.Add(new GrantItem(GrantKind.DustSweeps, ev.Pack.DustSweeps));
            return g.Items.Count == 0 ? null : g;
        }

        /// <summary>The paid rewards of an event summed into fixed lines: exactly the totals the Events screen shows on the
        /// card before the track is bought.</summary>
        private static PurchaseGrant TrackGoods(LiveEventDef ev)
        {
            if (ev == null) return null;
            double hours = 0.0, sweeps = 0.0, slag = 0.0;
            for (int i = 0; i < ev.Tiers.Count; i++)
            {
                EventRewardDef p = ev.Tiers[i].Paid;
                if (p == null) continue;
                if (p.Kind == EventRewardKind.Hourglass) hours += p.Amount;
                else if (p.Kind == EventRewardKind.Dust) sweeps += p.Amount;
                else slag += Math.Floor(p.Amount);
            }
            PurchaseGrant g = new PurchaseGrant();
            if (hours > 0.0) g.Add(new GrantItem(GrantKind.IdleGoldHours, hours));
            if (sweeps > 0.0) g.Add(new GrantItem(GrantKind.DustSweeps, sweeps));
            if (slag > 0.0) g.Add(new GrantItem(GrantKind.Slag, slag));
            return g.Items.Count == 0 ? null : g;
        }

        private static LiveEventDef FirstWith(GameSession session, LiveEventSlot slot, LiveEventPart part)
        {
            List<LiveEventDef> all = session.Content.LiveEvents.Events;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Slot == slot && (part == LiveEventPart.Track ? TrackGoods(all[i]) != null : PackGoods(all[i]) != null)) return all[i];
            return null;
        }

        /// <summary>
        /// WHAT A CHARGED PAYMENT DELIVERS (called by PurchaseGrants.Apply, in its check phase and its write phase). THE RULE:
        /// a payment the store confirmed is always delivered, exactly the contents shown when it was begun, and never twice
        /// (the receipt is kept once, PurchaseLedger).
        ///  - NORMAL: the event it was begun for (or, with no memory of that, the event running now) is running and has not
        ///    got that part: the track is unlocked, or the pack is applied and marked bought.
        ///  - STALE: the period turned over while the payment was in flight, or the part is already the buyer's (two payments
        ///    for one event), or the running event has no pack. The goods the card showed are paid out in full as Sealed
        ///    lines, from the event it was begun for (else the running one, else the first event of the slot that has them),
        ///    and nothing is marked, so the running event's own track and pack stay for sale. A track pays out its paid
        ///    rewards summed, because the quests it would have been claimed against belong to an event that is over.
        /// </summary>
        public static Delivery DeliveryOf(GameSession session, LiveEventSlot slot, LiveEventPart part, long nowUnix)
        {
            Delivery d = new Delivery();
            if (!Active(session)) return d;
            LiveEventSlotState st = Now(session, slot, nowUnix);
            LiveEventDef current = session.Content.LiveEvents.Event(st.EventId);
            LivePending bound;
            session.Profile.LiveEvents.Pending.TryGetValue(PendingKey(slot, part), out bound);
            bool sameEvent = bound == null || (bound.Period == st.Period && bound.EventId == st.EventId);
            bool open = current != null && (part == LiveEventPart.Pack ? current.Pack != null && !st.PackBought : !st.TrackBought);

            if (open && sameEvent)
            {
                d.Goods = part == LiveEventPart.Pack ? PackGoods(current) : null;
                d.Possible = part == LiveEventPart.Track || d.Goods != null;
                d.Normal = d.Possible;
                return d;
            }

            LiveEventDef source = bound != null ? session.Content.LiveEvents.Event(bound.EventId) : current;
            if (source == null || (part == LiveEventPart.Pack ? PackGoods(source) : TrackGoods(source)) == null) source = FirstWith(session, slot, part);
            if (source == null) return d;
            d.Goods = part == LiveEventPart.Pack ? PackGoods(source) : TrackGoods(source);
            d.Possible = d.Goods != null;
            d.Normal = false;
            d.Note = (part == LiveEventPart.Pack ? "The pack" : "The paid track") + " of " + source.Name + ", paid out in full: that event has moved on or is already yours";
            return d;
        }

        /// <summary>Called by PurchaseGrants.Apply for a normal EventTrack delivery: the paid track of the event that runs
        /// now is bought, once.</summary>
        public static void MarkTrackBought(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            Roll(session, slot, nowUnix);
            session.Profile.LiveEvents.Of(slot).TrackBought = true;
        }

        /// <summary>Called by PurchaseGrants.Apply once a normal pack's lines are applied: the pack of this event is
        /// used up.</summary>
        public static void MarkPackBought(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            Roll(session, slot, nowUnix);
            session.Profile.LiveEvents.Of(slot).PackBought = true;
        }

        // ------------------------------------------------------------------ what the screen reads

        private static StoreOfferDef OfferOf(GameSession session, LiveEventSlot slot, LiveEventPart part)
        {
            List<StoreOfferDef> all = session.Content.Store.Offers;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Kind == StoreOfferKind.Event && all[i].EventSlot == slot && all[i].EventPart == part) return all[i];
            return null;
        }

        /// <summary>True when something in either slot can be claimed right now (a finished quest, a reached tier
        /// on the free track, or on the paid track when it is bought). Reads, writes nothing: the Hub asks it for
        /// its dot, and seeing the Hub must never move a save.</summary>
        public static bool AnythingToClaim(GameSession session, long nowUnix)
        {
            if (!Active(session)) return false;
            return SlotHasClaim(session, LiveEventSlot.Weekly, nowUnix) || SlotHasClaim(session, LiveEventSlot.Monthly, nowUnix);
        }

        private static bool SlotHasClaim(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            LiveEventSlotState st = Now(session, slot, nowUnix);
            LiveEventDef ev = session.Content.LiveEvents.Event(st.EventId);
            if (ev == null) return false;
            for (int i = 0; i < ev.Quests.Count; i++)
                if (!LiveEventSlotState.Bit(st.QuestMask, i) && st.ProgressOf(i) >= ev.Quests[i].Target) return true;
            bool paidOpen = st.TrackBought && StoreSwitch.Enabled;
            for (int i = 0; i < ev.Tiers.Count; i++)
            {
                if (st.Points < ev.Tiers[i].Points) break;
                if (ev.Tiers[i].Free != null && !LiveEventSlotState.Bit(st.FreeMask, i)) return true;
                if (paidOpen && ev.Tiers[i].Paid != null && !LiveEventSlotState.Bit(st.PaidMask, i)) return true;
            }
            return false;
        }

        /// <summary>One slot as of nowUnix. Writes nothing.</summary>
        public static LiveEventView View(GameSession session, LiveEventSlot slot, long nowUnix)
        {
            LiveEventView v = new LiveEventView();
            v.Slot = slot;
            if (!Active(session)) return v;
            LiveEventSlotState st = Now(session, slot, nowUnix);
            LiveEventDef ev = session.Content.LiveEvents.Event(st.EventId);
            if (ev == null) return v;

            v.Active = true;
            v.EventId = ev.Id;
            v.Name = ev.Name;
            v.Blurb = ev.Blurb;
            v.Aspect = ev.Aspect;
            v.PointsLabel = ev.PointsLabel;
            v.Period = st.Period;
            long end = PeriodEndUnix(session, slot, st.Period);
            v.ResetInSeconds = Math.Max(0L, end - nowUnix);
            v.DaysLeft = (int)Math.Max(1L, (v.ResetInSeconds + 86399L) / 86400L);
            v.Points = st.Points;
            v.MaxPoints = ev.Tiers[ev.Tiers.Count - 1].Points;
            v.TrackOwned = st.TrackBought;
            v.PackOwned = st.PackBought;

            for (int i = 0; i < ev.Quests.Count; i++)
            {
                EventQuestDef q = ev.Quests[i];
                LiveQuestView qv = new LiveQuestView();
                qv.Id = q.Id;
                qv.Line = q.Line;
                qv.Target = q.Target;
                qv.Progress = Math.Min(q.Target, st.ProgressOf(i));
                qv.Points = q.Points;
                qv.Done = qv.Progress >= q.Target;
                qv.Claimed = LiveEventSlotState.Bit(st.QuestMask, i);
                qv.CanClaim = qv.Done && !qv.Claimed;
                v.Quests.Add(qv);
            }

            bool paidOpen = st.TrackBought && StoreSwitch.Enabled;
            for (int i = 0; i < ev.Tiers.Count; i++)
            {
                EventTierDef t = ev.Tiers[i];
                LiveTierView tv = new LiveTierView();
                tv.Index = i + 1;
                tv.Points = t.Points;
                tv.Reached = st.Points >= t.Points;
                tv.HasFree = t.Free != null;
                tv.HasPaid = t.Paid != null;
                if (tv.HasFree) { tv.FreeLine = RewardLine(session, t.Free, false); tv.FreeShort = RewardShort(session, t.Free); }
                if (tv.HasPaid) { tv.PaidLine = RewardLine(session, t.Paid, true); tv.PaidShort = RewardShort(session, t.Paid); }
                tv.FreeClaimed = LiveEventSlotState.Bit(st.FreeMask, i);
                tv.FreeCanClaim = tv.HasFree && tv.Reached && !tv.FreeClaimed;
                tv.PaidClaimed = LiveEventSlotState.Bit(st.PaidMask, i);
                tv.PaidCanClaim = paidOpen && tv.HasPaid && tv.Reached && !tv.PaidClaimed;
                v.Tiers.Add(tv);
            }

            double hours = 0.0, sweeps = 0.0, slag = 0.0;
            for (int i = 0; i < ev.Tiers.Count; i++)
            {
                EventRewardDef p = ev.Tiers[i].Paid;
                if (p == null) continue;
                if (p.Kind == EventRewardKind.Hourglass) hours += p.Amount;
                else if (p.Kind == EventRewardKind.Dust) sweeps += p.Amount;
                else slag += p.Amount;
            }
            if (hours > 0.0) v.TrackLines.Add(PurchaseGrants.Describe(new GrantItem(GrantKind.IdleGoldHours, hours), session) + " (Sealed)");
            if (sweeps > 0.0) v.TrackLines.Add(PurchaseGrants.Describe(new GrantItem(GrantKind.DustSweeps, sweeps), session));
            if (slag > 0.0) v.TrackLines.Add(PurchaseGrants.Describe(new GrantItem(GrantKind.Slag, slag), session) + " (Sealed)");
            v.TrackLines.Add("Paid out tier by tier as you earn points. Nothing in it is random.");

            StoreOfferDef track = OfferOf(session, slot, LiveEventPart.Track);
            if (track != null && StoreSwitch.Enabled && session.Content.Store.IsUsable)
            {
                v.TrackOfferId = track.Id;
                v.TrackPrice = StoreConfig.Eur(track.PriceEur);
                v.TrackCanBuy = !st.TrackBought;
            }
            StoreOfferDef pack = OfferOf(session, slot, LiveEventPart.Pack);
            if (pack != null && ev.Pack != null && StoreSwitch.Enabled && session.Content.Store.IsUsable)
            {
                v.PackOfferId = pack.Id;
                v.PackName = ev.Pack.Name;
                v.PackBlurb = ev.Pack.Blurb;
                v.PackPrice = StoreConfig.Eur(pack.PriceEur);
                v.PackCanBuy = !st.PackBought;
                if (ev.Pack.Hours > 0.0)
                    v.PackLines.Add(PurchaseGrants.Describe(new GrantItem(GrantKind.IdleGoldHours, ev.Pack.Hours), session) + " (Sealed)");
                if (ev.Pack.DustSweeps > 0.0)
                    v.PackLines.Add(PurchaseGrants.Describe(new GrantItem(GrantKind.DustSweeps, ev.Pack.DustSweeps), session));
                v.PackLines.Add("Once for this event. Nothing in it is random.");
            }
            v.AnythingToClaim = SlotHasClaim(session, slot, nowUnix);
            return v;
        }
    }
}
