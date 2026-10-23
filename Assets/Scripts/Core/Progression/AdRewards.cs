using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Why an ad placement cannot pay now.</summary>
    public enum AdRefusal
    {
        None,
        /// <summary>AdsSwitch is off, or ads.json is absent: no button exists.</summary>
        SwitchOff,
        /// <summary>ads.json has no placement of this kind, or it is disabled there.</summary>
        NoPlacement,
        /// <summary>The feature it belongs to is not open yet (the Proving before 2-4).</summary>
        NotOpen,
        /// <summary>There is nothing for it to pay right now: no claim, a full Brand bank, paid repeats left.</summary>
        NothingToPay,
        /// <summary>It has paid PerDay times this reward day.</summary>
        CappedToday,
        /// <summary>This impression id has already been paid.</summary>
        AlreadyGranted,
        /// <summary>The offer was shown, but what it paid for stopped being true while the ad played (the Brand bank
        /// filled). Nothing is granted and the day's count is not spent.</summary>
        Lapsed
    }

    /// <summary>What an ad placement is about, from the screen it is drawn on.</summary>
    public sealed class AdContext
    {
        /// <summary>IdleGold: the Gold the idle claim just paid. 0 when there was no claim.</summary>
        public long ClaimGold;
        /// <summary>RepeatClear: the stage the player just cleared.</summary>
        public StageDef Stage;
    }

    /// <summary>
    /// What one placement would pay if watched now: worked out BEFORE the ad starts, shown in full on the button,
    /// and granted exactly as shown (AdRewards.Grant pays the offer, never a recomputation).
    /// </summary>
    public sealed class AdOffer
    {
        public string PlacementId = "";
        public AdPlacementKind Kind;
        public string AdPlacement = "";
        public bool Available;
        public AdRefusal Refusal = AdRefusal.None;
        /// <summary>Why not, in one sentence. Empty when Available.</summary>
        public string Reason = "";
        public long Gold;
        public long Dust;
        public int Brands;
        /// <summary>The full line: "Watch an ad: +2 hours of idle Gold = 41,200 Gold".</summary>
        public string Line = "";
        /// <summary>The reward alone: "+41,200 Gold".</summary>
        public string RewardText = "";
        public int PerDay;
        public int LeftToday;
        /// <summary>The reward day this offer counts against.</summary>
        public long Day;
        /// <summary>When the next reward day starts, unix seconds.</summary>
        public long NextDayUnix;
        /// <summary>RepeatClear: the stage it pays for.</summary>
        public string StageId = "";
    }

    public sealed class AdGrantResult
    {
        public bool Success;
        public AdRefusal Refusal = AdRefusal.None;
        public string Reason = "";
        public long Gold;
        public long Dust;
        public int Brands;
        public readonly List<string> Lines = new List<string>();
    }

    /// <summary>
    /// REWARDED ADS, THE RULES (R28, Docs/DESIGN-REWARDED-ADS.md). Engine-free; the ad network is the Game layer's
    /// business (Assets/Scripts/Game/Ads), and this never knows which one, or whether one exists.
    ///
    /// THE BELGIAN RULE. Nothing a player is paid for watching depends on luck: each placement pays one fixed line
    /// computed from the player's own state before the ad starts, and Grant pays that line and nothing else. The
    /// Gold goes through PurchaseGrants.Apply as a fixed Gold line, so it is Sealed and can only buy level-ups; the
    /// Relic Dust is a fixed Dust line; a Brand goes through GameSession.AddProvingBrand. No Shards, no hero, no
    /// relic, no skin, no event points, nothing a Store offer sells as its headline.
    ///
    /// NEVER IN PLACE OF A PURCHASE. Three placements, each at most PerDay a reward day, on the idle claim, the
    /// Proving and a capped stage. None of them is drawn on the Store, the Events screen or the pass, and none of
    /// them opens, extends or discounts an offer.
    ///
    /// ONE CLOCK, FORWARD ONLY. The day is the later of the device's reward day and AdsState.ClockDay, so a clock
    /// wound back reopens nothing.
    /// </summary>
    public static class AdRewards
    {
        public const string CappedReason = "Used today. Back after the daily reset.";

        /// <summary>True when ads can exist at all in this build: the switch is on and ads.json loaded.</summary>
        public static bool Active(GameSession session)
        {
            return AdsSwitch.Enabled && session != null && session.Content != null && session.Content.Ads.IsUsable;
        }

        /// <summary>The reward day ads count against: the device's, or the latest one an ad was granted on if later.</summary>
        public static long EffectiveDay(GameSession session, long nowUnix)
        {
            return Math.Max(session.DayStartUnix(nowUnix), session.Profile.Ads.ClockDay);
        }

        /// <summary>How many times this placement has paid on the effective reward day.</summary>
        public static int UsedToday(GameSession session, AdPlacementDef def, long nowUnix)
        {
            AdPlacementRecord r = session.Profile.Ads.Find(def.Id);
            if (r == null) return 0;
            return r.Day == EffectiveDay(session, nowUnix) ? r.Count : 0;
        }

        /// <summary>What a placement would pay now. READ-ONLY: it writes nothing to the profile.</summary>
        public static AdOffer View(GameSession session, AdPlacementKind kind, AdContext context, long nowUnix)
        {
            AdOffer o = new AdOffer();
            o.Kind = kind;
            if (!Active(session)) return Refuse(o, AdRefusal.SwitchOff, "");
            AdPlacementDef def = session.Content.Ads.OfKind(kind);
            if (def == null || !def.Enabled || def.PerDay <= 0) return Refuse(o, AdRefusal.NoPlacement, "");
            o.PlacementId = def.Id;
            o.AdPlacement = def.AdPlacement;
            o.PerDay = def.PerDay;
            o.Day = EffectiveDay(session, nowUnix);
            o.NextDayUnix = o.Day + 86400L;
            o.LeftToday = Math.Max(0, def.PerDay - UsedToday(session, def, nowUnix));
            if (context == null) context = new AdContext();

            switch (kind)
            {
                case AdPlacementKind.IdleGold:
                {
                    if (context.ClaimGold <= 0) return Refuse(o, AdRefusal.NothingToPay, "Collect the idle chest first.");
                    long capGold = session.IdleYield((long)Math.Round(def.CapHours * 3600.0)).Gold;
                    o.Gold = Math.Max(0L, Math.Min(context.ClaimGold, capGold));
                    if (o.Gold <= 0) return Refuse(o, AdRefusal.NothingToPay, "Collect the idle chest first.");
                    bool capped = context.ClaimGold > capGold;
                    o.RewardText = "+" + Group(o.Gold) + " Gold";
                    o.Line = capped
                        ? "Watch an ad: +" + Hours(def.CapHours) + " of idle Gold = " + Group(o.Gold) + " Gold"
                        : "Watch an ad: your claim again = " + Group(o.Gold) + " Gold";
                    break;
                }
                case AdPlacementKind.Brand:
                {
                    // Asked through the Proving's own door, the way every screen asks (OnboardingChecks holds it).
                    List<FeatureEntryPoint> doors = FeatureEntryPoints.Of(Feature.Proving);
                    if (doors.Count > 0 && !FeatureEntryPoints.Allows(session.Content, session.Profile, doors[0].Id))
                        return Refuse(o, AdRefusal.NotOpen, FeatureGate.LockLine(session.Content, Feature.Proving));
                    ProvingBrandsStatus brands = session.PeekProvingBrands(nowUnix);
                    if (brands.Banked >= brands.Max) return Refuse(o, AdRefusal.NothingToPay, "Your Brands are full.");
                    o.Brands = 1;
                    o.RewardText = "+1 Brand";
                    o.Line = "Watch an ad: +1 Brand for the Proving";
                    break;
                }
                default:
                {
                    StageDef stage = context.Stage;
                    if (stage == null || !session.Profile.HasClearedStage(stage.Id))
                        return Refuse(o, AdRefusal.NothingToPay, "Clear the stage first.");
                    if (RepeatsLeftReadOnly(session, stage, nowUnix) > 0)
                        return Refuse(o, AdRefusal.NothingToPay, "This stage still has paid clears today.");
                    o.StageId = stage.Id;
                    o.Gold = Math.Max(0, stage.RewardGold);
                    o.Dust = Math.Max(0, session.RelicDustForRepeatClear(stage));
                    if (o.Gold <= 0 && o.Dust <= 0) return Refuse(o, AdRefusal.NothingToPay, "This stage pays nothing to repeat.");
                    string pay = o.Gold > 0 && o.Dust > 0 ? Group(o.Gold) + " Gold and " + Group(o.Dust) + " Relic Dust"
                               : o.Gold > 0 ? Group(o.Gold) + " Gold" : Group(o.Dust) + " Relic Dust";
                    o.RewardText = "+" + pay;
                    o.Line = "Watch an ad: one more full clear of " + stage.DisplayCode + " = " + pay;
                    break;
                }
            }

            if (o.LeftToday <= 0) return Refuse(o, AdRefusal.CappedToday, CappedReason);
            o.Available = true;
            return o;
        }

        /// <summary>
        /// Pays a watched ad: exactly the offer that was shown before it started, once per impression id, at most
        /// PerDay a reward day. All or nothing: a refusal leaves the profile untouched.
        /// </summary>
        public static AdGrantResult Grant(GameSession session, AdOffer offer, string impressionId, long nowUnix)
        {
            AdGrantResult r = new AdGrantResult();
            if (!Active(session)) return Refuse(r, AdRefusal.SwitchOff, "Ads are off in this build.");
            if (offer == null || !offer.Available) return Refuse(r, AdRefusal.NothingToPay, "Nothing was offered.");
            AdPlacementDef def = session.Content.Ads.Find(offer.PlacementId);
            if (def == null || !def.Enabled || def.Kind != offer.Kind) return Refuse(r, AdRefusal.NoPlacement, "That ad is not offered.");
            if (string.IsNullOrEmpty(impressionId)) return Refuse(r, AdRefusal.NothingToPay, "The ad did not say it was finished.");
            AdsState ads = session.Profile.Ads;
            if (ads.Seen(impressionId)) return Refuse(r, AdRefusal.AlreadyGranted, "That ad has already been paid.");
            if (UsedToday(session, def, nowUnix) >= def.PerDay) return Refuse(r, AdRefusal.CappedToday, CappedReason);

            if (offer.Kind == AdPlacementKind.Brand)
            {
                if (!session.AddProvingBrand(nowUnix))
                    return Refuse(r, AdRefusal.Lapsed, "Your Brands filled up while the ad played, so nothing was added.");
                r.Brands = 1;
                r.Lines.Add("1 Brand");
            }
            else
            {
                PurchaseGrant grant = new PurchaseGrant();
                if (offer.Gold > 0) grant.Add(new GrantItem(GrantKind.Gold, offer.Gold));
                if (offer.Dust > 0) grant.Add(new GrantItem(GrantKind.Dust, offer.Dust));
                if (grant.Items.Count == 0) return Refuse(r, AdRefusal.NothingToPay, "Nothing was offered.");
                GrantResult g = PurchaseGrants.Apply(session, grant, null, nowUnix);
                if (!g.Success) return Refuse(r, AdRefusal.NothingToPay, "The reward could not be paid.");
                r.Gold = g.Gold;
                r.Dust = g.Dust;
                r.Lines.AddRange(g.Lines);
            }

            long day = EffectiveDay(session, nowUnix);
            AdPlacementRecord rec = ads.Record(def.Id);
            if (rec.Day != day) { rec.Day = day; rec.Count = 0; }
            rec.Count++;
            if (day > ads.ClockDay) ads.ClockDay = day;
            ads.Remember(impressionId);
            r.Success = true;
            return r;
        }

        /// <summary>GameSession.RepeatClearsRemaining without its RollDailyReset write: a later reward day reads as the
        /// full allowance, exactly what the daily reset would leave.</summary>
        private static int RepeatsLeftReadOnly(GameSession session, StageDef stage, long nowUnix)
        {
            if (session.DayStartUnix(nowUnix) > session.Profile.DailyResetUnix) return session.RepeatFullClearsPerDay;
            return Math.Max(0, session.RepeatFullClearsPerDay - session.Profile.ClearsTodayFor(stage.Id));
        }

        private static AdOffer Refuse(AdOffer o, AdRefusal why, string reason)
        {
            o.Available = false;
            o.Refusal = why;
            o.Reason = reason ?? "";
            return o;
        }

        private static AdGrantResult Refuse(AdGrantResult r, AdRefusal why, string reason)
        {
            r.Success = false;
            r.Refusal = why;
            r.Reason = reason;
            return r;
        }

        private static string Group(long v)
        {
            return v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Hours(double h)
        {
            string n = h.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return n + (h == 1.0 ? " hour" : " hours");
        }
    }
}
