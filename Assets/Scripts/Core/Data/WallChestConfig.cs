using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>What one kind of wall chest holds: hours of idle income at the frontier it is opened
    /// at, and Slag in proportion to how deep the chapter is.</summary>
    public sealed class WallChestTier
    {
        public double Hours;
        public int SlagPerChapter;
    }

    /// <summary>
    /// wall-chests.json: the one-time chest on a chapter's wall stage and on its finale (R17 task 5).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here
    /// is read by a fight, so retuning a chest must not retire every guild's replays. Loaded through
    /// ContentDatabase.LoadWallChests after Load. Every reward is hours of idle income, paid through
    /// the one idle formula (GameSession.IdleYield), plus Slag; there is no currency amount in this
    /// file and so no second formula to drift from the first.
    ///
    /// THE SWITCH. "enabled" is the rollback: false makes every chest absent, and a first clear then
    /// pays exactly what it paid before this file existed. Empty (IsUsable false) on a database that
    /// never loaded the file, which reads the same way.
    /// </summary>
    public sealed class WallChestConfig
    {
        public bool Enabled;

        /// <summary>The stage index within its chapter that holds the wall chest (6).</summary>
        public int WallIndex = 6;

        /// <summary>The stage index within its chapter that holds the finale chest (12).</summary>
        public int FinaleIndex = 12;

        public WallChestTier Wall = new WallChestTier();
        public WallChestTier Finale = new WallChestTier();

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable { get { return Enabled && (Wall.Hours > 0.0 || Finale.Hours > 0.0); } }

        public static WallChestConfig FromJson(string text)
        {
            WallChestConfig c = new WallChestConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("wall-chests.json: " + err); return c; }

            c.Enabled = root.GetBool("enabled", false);
            c.WallIndex = root.GetInt("wallStageIndex", c.WallIndex);
            c.FinaleIndex = root.GetInt("finaleStageIndex", c.FinaleIndex);
            ReadTier(root["wall"], c.Wall);
            ReadTier(root["finale"], c.Finale);

            if (c.WallIndex == c.FinaleIndex)
                c.Warnings.Add("wall-chests.json: the wall and the finale name the same stage index");
            if (c.Finale.Hours < c.Wall.Hours || c.Finale.SlagPerChapter < c.Wall.SlagPerChapter)
                c.Warnings.Add("wall-chests.json: the finale chest must be larger than the wall chest");
            return c;
        }

        private static void ReadTier(JsonValue j, WallChestTier tier)
        {
            tier.Hours = Math.Max(0.0, j["hours"].AsDouble(0.0));
            tier.SlagPerChapter = Math.Max(0, j.GetInt("slagPerChapter", 0));
        }
    }
}
