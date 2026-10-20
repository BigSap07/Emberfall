using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Why a grant was not applied. None is success.</summary>
    public enum GrantRefusal
    {
        None,
        /// <summary>The grant is empty, names an unknown hero, or has a line with no amount.</summary>
        BadGrant,
        /// <summary>A line needs a hero from the buyer and the one given is not allowed.</summary>
        BadChoice,
        /// <summary>The pass is off, has no usable file, or no season runs, so a premium cannot be applied.</summary>
        NoSeason,
        /// <summary>An event line while no event runs in that slot, or its track or pack is already bought.</summary>
        NoEvent
    }

    /// <summary>What one applied grant did, for the receipt line and the harness.</summary>
    public sealed class GrantResult
    {
        public bool Success;
        public GrantRefusal Reason = GrantRefusal.None;
        public long Gold;
        public long Slag;
        public long Dust;
        public string HeroId = "";
        public bool PassOpened;
        /// <summary>One plain sentence per line, shown on the receipt: "3,400 Gold (Sealed)".</summary>
        public readonly List<string> Lines = new List<string>();
    }

    /// <summary>
    /// THE ONE PLACE A PURCHASE BECOMES GOODS (R19, Docs/DESIGN-STORE.md). Every grant of every paid offer
    /// goes through Apply: the purchase itself, a Growth Fund milestone, an Ember Ledger day, a paid
    /// Wayfarer's Road tier. It makes its Sealed marks through SealedGrants and nowhere else, so a good that
    /// came from here is always marked.
    ///
    /// THE RULE: nothing in this file reads a random number, rolls, draws or calls a random mechanic. The
    /// grant kinds are a closed set of FIXED lines (GrantKind), the amounts are in the offer's data or a
    /// function of the buyer's own income, and the hero is named by the buyer from a list shown before the
    /// purchase. CheckStoreNoRandomPath scans this file and PurchaseLedger.cs for the words that would break it.
    ///
    /// ALL OR NOTHING. Everything is checked before anything is written; a refusal leaves the profile
    /// untouched, byte for byte.
    /// </summary>
    public static class PurchaseGrants
    {
        /// <summary>Applies a grant to the session. choiceHeroId is the buyer's pick for a HeroChoice or an
        /// OwnedHeroCopy line and is ignored by a grant that has none.</summary>
        public static GrantResult Apply(GameSession session, PurchaseGrant grant, string choiceHeroId, long nowUnix)
        {
            GrantResult r = new GrantResult();
            if (session == null || grant == null) { r.Reason = GrantRefusal.BadGrant; return r; }
            if (grant.Problem(session.Content) != null) { r.Reason = GrantRefusal.BadGrant; return r; }

            // ---- check everything first
            HeroDef chosen = null;
            for (int i = 0; i < grant.Items.Count; i++)
            {
                GrantItem it = grant.Items[i];
                if (it.Kind == GrantKind.HeroChoice)
                {
                    chosen = string.IsNullOrEmpty(choiceHeroId) ? null : session.Content.GetHero(choiceHeroId);
                    if (chosen == null || !it.HeroIds.Contains(choiceHeroId)) { r.Reason = GrantRefusal.BadChoice; return r; }
                }
                else if (it.Kind == GrantKind.OwnedHeroCopy)
                {
                    chosen = string.IsNullOrEmpty(choiceHeroId) ? null : session.Content.GetHero(choiceHeroId);
                    HeroInstance owned = chosen == null ? null : session.Profile.FindHeroByDefId(chosen.Id);
                    if (chosen == null || owned == null || chosen.Stars != it.Stars) { r.Reason = GrantRefusal.BadChoice; return r; }
                }
                else if (it.Kind == GrantKind.PassPremium)
                {
                    if (!WayfarersRoad.Active(session) || WayfarersRoad.SeasonOf(session, nowUnix) == 0)
                    {
                        r.Reason = GrantRefusal.NoSeason;
                        return r;
                    }
                }
                else if (it.Kind == GrantKind.EventTrack || it.Kind == GrantKind.EventPack)
                {
                    LiveEventPart part = it.Kind == GrantKind.EventTrack ? LiveEventPart.Track : LiveEventPart.Pack;
                    if (!LiveEvents.CanGrant(session, SlotOf(it), part, nowUnix))
                    {
                        r.Reason = GrantRefusal.NoEvent;
                        return r;
                    }
                }
            }

            // ---- the one write block
            for (int i = 0; i < grant.Items.Count; i++)
            {
                GrantItem it = grant.Items[i];
                switch (it.Kind)
                {
                    case GrantKind.Gold:
                        SealedGrants.GrantGold(session, (long)it.Amount);
                        r.Gold += (long)it.Amount;
                        r.Lines.Add(Group((long)it.Amount) + " Gold (Sealed)");
                        break;
                    case GrantKind.IdleGoldHours:
                        long gold = session.IdleYield((long)Math.Round(it.Amount * 3600.0)).Gold;
                        SealedGrants.GrantGold(session, gold);
                        r.Gold += gold;
                        r.Lines.Add(Hours(it.Amount) + " of your idle Gold = " + Group(gold) + " Gold (Sealed)");
                        break;
                    case GrantKind.Slag:
                        SealedGrants.GrantSlag(session, (long)it.Amount);
                        r.Slag += (long)it.Amount;
                        r.Lines.Add(Group((long)it.Amount) + " Slag (Sealed)");
                        break;
                    case GrantKind.Dust:
                        session.Profile.RelicDust += (long)it.Amount;
                        r.Dust += (long)it.Amount;
                        r.Lines.Add(Group((long)it.Amount) + " Relic Dust");
                        break;
                    case GrantKind.DustSweeps:
                        long swept = (long)Math.Round(it.Amount * WayfarersRoad.DustPerSweep(session));
                        session.Profile.RelicDust += swept;
                        r.Dust += swept;
                        r.Lines.Add(Sweeps(it.Amount) + " of Relic Dust = " + Group(swept) + " Relic Dust");
                        break;
                    case GrantKind.HeroCopy:
                        HeroDef fixedHero = session.Content.GetHero(it.HeroId);
                        SealedGrants.GrantHeroCopy(session, fixedHero, nowUnix);
                        r.HeroId = fixedHero.Id;
                        r.Lines.Add("One copy of " + fixedHero.Name + " (Sealed)");
                        break;
                    case GrantKind.HeroChoice:
                    case GrantKind.OwnedHeroCopy:
                        SealedGrants.GrantHeroCopy(session, chosen, nowUnix);
                        r.HeroId = chosen.Id;
                        r.Lines.Add("One copy of " + chosen.Name + " (Sealed)");
                        break;
                    case GrantKind.PassPremium:
                        WayfarersRoad.Roll(session, nowUnix);
                        WayfarersRoadState st = session.Profile.WayfarersRoad;
                        st.PaidSeason = st.SeasonIndex;
                        r.PassOpened = true;
                        r.Lines.Add("The paid track of this season");
                        break;
                    case GrantKind.Activate:
                        break;
                    case GrantKind.EventTrack:
                        LiveEvents.MarkTrackBought(session, SlotOf(it), nowUnix);
                        r.Lines.Add("The paid Event Track of this event");
                        break;
                    case GrantKind.EventPack:
                        // The pack's own lines are applied here as the fixed lines they are, so each is marked Sealed
                        // by the branches above; the event then counts its pack as bought.
                        GrantResult inner = Apply(session, LiveEvents.PackGrant(session, SlotOf(it), nowUnix), null, nowUnix);
                        r.Gold += inner.Gold;
                        r.Dust += inner.Dust;
                        r.Lines.AddRange(inner.Lines);
                        LiveEvents.MarkPackBought(session, SlotOf(it), nowUnix);
                        break;
                }
            }
            r.Success = true;
            return r;
        }

        private static LiveEventSlot SlotOf(GrantItem it)
        {
            return it.Amount >= 1.0 ? LiveEventSlot.Monthly : LiveEventSlot.Weekly;
        }

        /// <summary>The one line a Store card shows for a grant item BEFORE purchase: fixed words, with the
        /// real numbers where they are fixed. IdleGoldHours reads the buyer's own income when a session is
        /// given, so the card can say what the hours are worth now.</summary>
        public static string Describe(GrantItem it, GameSession session)
        {
            switch (it.Kind)
            {
                case GrantKind.Gold: return Group((long)it.Amount) + " Gold";
                case GrantKind.Slag: return Group((long)it.Amount) + " Slag";
                case GrantKind.Dust: return Group((long)it.Amount) + " Relic Dust";
                case GrantKind.IdleGoldHours:
                    string text = Hours(it.Amount) + " of idle Gold";
                    if (session != null)
                        text += " (now " + Group(session.IdleYield((long)Math.Round(it.Amount * 3600.0)).Gold) + " Gold)";
                    return text;
                case GrantKind.DustSweeps:
                    string sweepText = Sweeps(it.Amount) + " of Relic Dust";
                    if (session != null) sweepText += " (now " + Group((long)Math.Round(it.Amount * WayfarersRoad.DustPerSweep(session))) + " Relic Dust)";
                    return sweepText;
                case GrantKind.HeroCopy: return "One copy of " + HeroName(session, it.HeroId);
                case GrantKind.HeroChoice:
                    List<string> names = new List<string>();
                    for (int i = 0; i < it.HeroIds.Count; i++) names.Add(HeroName(session, it.HeroIds[i]));
                    return "One copy of the hero you pick: " + string.Join(", ", names.ToArray());
                case GrantKind.OwnedHeroCopy: return "One copy of a " + it.Stars + "-star you already own";
                case GrantKind.PassPremium: return "The paid track of this season";
                case GrantKind.EventTrack: return "The paid Event Track of this event";
                case GrantKind.EventPack: return "The event pack of this event";
                case GrantKind.Activate: return "";
            }
            return "";
        }

        private static string HeroName(GameSession session, string id)
        {
            HeroDef d = session == null ? null : session.Content.GetHero(id);
            return d == null ? id : d.Name;
        }

        private static string Group(long v)
        {
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string Sweeps(double n)
        {
            return n.ToString("0.#", CultureInfo.InvariantCulture) + (n == 1.0 ? " sweep" : " sweeps");
        }

        private static string Hours(double h)
        {
            return h.ToString("0.#", CultureInfo.InvariantCulture) + (h == 1.0 ? " hour" : " hours");
        }
    }
}
