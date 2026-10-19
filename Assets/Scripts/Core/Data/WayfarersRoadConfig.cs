using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>What a tier of the pass pays. A closed set, appended and never inserted: the file names
    /// a kind by its lower-case string.</summary>
    public enum PassRewardKind
    {
        /// <summary>Hours of idle income at the player's frontier (the one idle formula).</summary>
        Hourglass,
        /// <summary>That many repeat-clear payouts of the frontier stage, in Relic Dust.</summary>
        Dust,
        /// <summary>Slag, a fixed amount.</summary>
        Slag,
        /// <summary>One hero of Stars stars the player picks. A plain earned copy, never Sealed.</summary>
        Selector,
        /// <summary>Paid track only: something to look at.</summary>
        Cosmetic
    }

    /// <summary>One tier's reward, free or paid. Fixed, and shown up front.</summary>
    public sealed class PassRewardDef
    {
        public int Tier;
        public PassRewardKind Kind;
        public double Amount;
        public int Stars;
        public string Label = "";
    }

    /// <summary>
    /// wayfarers-road.json: the season, its points, its tiers and the paid track
    /// (Docs/DESIGN-WAYFARERS-ROAD.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here is
    /// read by a fight. Loaded through ContentDatabase.LoadWayfarersRoad after Load. Empty (IsUsable
    /// false) on a database that never loaded the file, or whose tiers are not exactly 1 to TierCount
    /// in order: the pass reads that as "nothing to run".
    /// </summary>
    public sealed class WayfarersRoadConfig
    {
        public const int TierCount = 30;

        public int SeasonDays = 28;
        /// <summary>Days from 1970-01-01 to the first season's first reward day (a UTC calendar date).</summary>
        public long EpochDay;
        public int TierPoints = 40;
        public int OrderPoints = 8;
        public int DailyChestPoints = 12;
        public int WeeklyChestPoints = 40;
        public double BuyTierDays = 2.0;
        public int BuyTierMax = 3;
        public int PriceRound = 5;

        public readonly List<PassRewardDef> Tiers = new List<PassRewardDef>();
        public readonly List<PassRewardDef> Paid = new List<PassRewardDef>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Tiers.Count == TierCount && SeasonDays > 0 && TierPoints > 0; }
        }

        /// <summary>The free reward of a tier (1 to TierCount), or null.</summary>
        public PassRewardDef Tier(int tier)
        {
            return tier >= 1 && tier <= Tiers.Count ? Tiers[tier - 1] : null;
        }

        public PassRewardDef PaidAt(int tier)
        {
            for (int i = 0; i < Paid.Count; i++) if (Paid[i].Tier == tier) return Paid[i];
            return null;
        }

        public static WayfarersRoadConfig FromJson(string text)
        {
            WayfarersRoadConfig c = new WayfarersRoadConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("wayfarers-road.json: " + err); return c; }

            c.SeasonDays = Math.Max(1, root.GetInt("seasonDays", c.SeasonDays));
            c.TierPoints = Math.Max(1, root.GetInt("tierPoints", c.TierPoints));
            c.PriceRound = Math.Max(1, root.GetInt("priceRound", c.PriceRound));
            DateTime epoch;
            if (DateTime.TryParseExact(root.GetString("epoch", ""), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out epoch))
                c.EpochDay = (long)(epoch - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
            else
                c.Warnings.Add("wayfarers-road.json: epoch is not a yyyy-MM-dd date; the pass has no seasons");

            JsonValue points = root["points"];
            if (points != null && points.IsObject)
            {
                c.OrderPoints = Math.Max(0, points.GetInt("order", c.OrderPoints));
                c.DailyChestPoints = Math.Max(0, points.GetInt("dailyChest", c.DailyChestPoints));
                c.WeeklyChestPoints = Math.Max(0, points.GetInt("weeklyChest", c.WeeklyChestPoints));
            }
            JsonValue buy = root["buyTier"];
            if (buy != null && buy.IsObject)
            {
                c.BuyTierDays = Math.Max(0.0, buy["days"].AsDouble(c.BuyTierDays));
                c.BuyTierMax = Math.Max(0, buy.GetInt("maxPerSeason", c.BuyTierMax));
            }

            ReadRewards(root.GetList("tiers"), c.Tiers, c.Warnings, "tiers", false);
            ReadRewards(root.GetList("paid"), c.Paid, c.Warnings, "paid", true);

            if (c.Tiers.Count != TierCount)
            {
                c.Warnings.Add("wayfarers-road.json: tiers must be exactly " + TierCount + " rewards numbered 1 to " + TierCount + " in order; the pass is off");
                c.Tiers.Clear();
            }
            else
            {
                for (int i = 0; i < c.Tiers.Count; i++)
                    if (c.Tiers[i].Tier != i + 1 || c.Tiers[i].Kind == PassRewardKind.Cosmetic)
                    {
                        c.Warnings.Add("wayfarers-road.json: free tier " + (i + 1) + " is out of order or a cosmetic; the pass is off");
                        c.Tiers.Clear();
                        break;
                    }
            }
            if (c.EpochDay <= 0) c.Tiers.Clear();
            return c;
        }

        private static void ReadRewards(List<JsonValue> source, List<PassRewardDef> into, List<string> warnings, string where, bool paid)
        {
            for (int i = 0; i < source.Count; i++)
            {
                PassRewardDef d = new PassRewardDef();
                d.Tier = source[i].GetInt("tier", 0);
                string kind = source[i].GetString("kind", "");
                switch (kind)
                {
                    case "hourglass": d.Kind = PassRewardKind.Hourglass; break;
                    case "dust": d.Kind = PassRewardKind.Dust; break;
                    case "slag": d.Kind = PassRewardKind.Slag; break;
                    case "selector": d.Kind = PassRewardKind.Selector; break;
                    case "cosmetic": d.Kind = PassRewardKind.Cosmetic; break;
                    default:
                        warnings.Add("wayfarers-road.json: " + where + " entry " + (i + 1) + " has unknown kind '" + kind + "' and is dropped");
                        continue;
                }
                d.Amount = source[i]["amount"].AsDouble(0.0);
                d.Stars = source[i].GetInt("stars", 0);
                d.Label = source[i].GetString("label", "");
                if (d.Kind == PassRewardKind.Selector && (d.Stars < 1 || d.Stars > 5))
                {
                    warnings.Add("wayfarers-road.json: " + where + " tier " + d.Tier + " is a selector without a star count and is dropped");
                    continue;
                }
                if ((d.Kind == PassRewardKind.Hourglass || d.Kind == PassRewardKind.Dust || d.Kind == PassRewardKind.Slag) && d.Amount <= 0.0)
                {
                    warnings.Add("wayfarers-road.json: " + where + " tier " + d.Tier + " pays nothing and is dropped");
                    continue;
                }
                if (d.Tier < 1 || d.Tier > TierCount)
                {
                    warnings.Add("wayfarers-road.json: " + where + " tier " + d.Tier + " is outside 1 to " + TierCount + " and is dropped");
                    continue;
                }
                into.Add(d);
            }
        }
    }
}
