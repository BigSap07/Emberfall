using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>What a rewarded ad placement pays. Closed: a file naming another kind is dropped with a warning.
    /// Every kind is one fixed line (Docs/DESIGN-REWARDED-ADS.md, "The placements").</summary>
    public enum AdPlacementKind
    {
        /// <summary>A1. The idle claim's Gold again, Sealed, capped at CapHours of the player's own idle Gold.</summary>
        IdleGold,
        /// <summary>A2. One Proving Brand, while the bank is below its maximum.</summary>
        Brand,
        /// <summary>A3. One more full-pay repeat clear of a stage whose paid repeats are used: its Gold, Sealed, and its Relic Dust.</summary>
        RepeatClear
    }

    public sealed class AdPlacementDef
    {
        public string Id = "";
        public AdPlacementKind Kind;
        public int PerDay = 1;
        /// <summary>IdleGold only: the most the ad adds, in hours of the player's own idle Gold.</summary>
        public double CapHours;
        /// <summary>The placement name in the ad network's dashboard.</summary>
        public string AdPlacement = "";
        public bool Enabled = true;
    }

    /// <summary>
    /// ads.json: the rewarded ad placements, data only. Loaded after ContentDatabase.Load and NOT part of
    /// ContentVersion: no fight reads it. Empty (IsUsable false) on a build that never loaded it, which every
    /// screen reads as "no ads".
    /// </summary>
    public sealed class AdsConfig
    {
        /// <summary>The ceiling on any placement's PerDay, whatever the file says. The record proposes 1 each.</summary>
        public const int MaxPerDay = 3;
        /// <summary>The ceiling on IdleGold's CapHours, whatever the file says: the lean variant is 2, and more than 4
        /// moved the chapter 2 curve past the band (Docs/DESIGN-REWARDED-ADS.md, "The measurement").</summary>
        public const double MaxCapHours = 4.0;

        public readonly List<AdPlacementDef> Placements = new List<AdPlacementDef>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable { get { return Placements.Count > 0; } }

        public AdPlacementDef Find(string id)
        {
            for (int i = 0; i < Placements.Count; i++) if (Placements[i].Id == id) return Placements[i];
            return null;
        }

        public AdPlacementDef OfKind(AdPlacementKind kind)
        {
            for (int i = 0; i < Placements.Count; i++) if (Placements[i].Kind == kind) return Placements[i];
            return null;
        }

        public static AdsConfig FromJson(string text)
        {
            AdsConfig c = new AdsConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("ads.json: " + err); return c; }
            List<JsonValue> rows = root.GetList("placements");
            for (int i = 0; i < rows.Count; i++)
            {
                JsonValue r = rows[i];
                AdPlacementDef d = new AdPlacementDef();
                d.Id = r.GetString("id", "");
                string kind = r.GetString("kind", "");
                if (kind == "idleGold") d.Kind = AdPlacementKind.IdleGold;
                else if (kind == "brand") d.Kind = AdPlacementKind.Brand;
                else if (kind == "repeatClear") d.Kind = AdPlacementKind.RepeatClear;
                else { c.Warnings.Add("ads.json: placement " + i + " has an unknown kind '" + kind + "' - dropped"); continue; }
                if (d.Id.Length == 0 || c.Find(d.Id) != null || c.OfKind(d.Kind) != null)
                {
                    c.Warnings.Add("ads.json: placement " + i + " needs a unique id and a kind no other placement has - dropped");
                    continue;
                }
                d.PerDay = Math.Max(0, Math.Min(MaxPerDay, r.GetInt("perDay", 1)));
                d.CapHours = d.Kind == AdPlacementKind.IdleGold ? Math.Max(0.0, Math.Min(MaxCapHours, r["capHours"].AsDouble(2.0))) : 0.0;
                if (d.Kind == AdPlacementKind.IdleGold && d.CapHours <= 0.0)
                {
                    c.Warnings.Add("ads.json: " + d.Id + " needs capHours above 0 - dropped");
                    continue;
                }
                d.AdPlacement = r.GetString("adPlacement", d.Id);
                d.Enabled = r.GetBool("enabled", true);
                c.Placements.Add(d);
            }
            return c;
        }
    }
}
