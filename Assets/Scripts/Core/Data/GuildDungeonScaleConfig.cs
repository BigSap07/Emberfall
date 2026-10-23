using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>One row of the ladder table: a guild of at least Members members gets Floors floors.</summary>
    public sealed class GuildDungeonLadderBand
    {
        public int Members;
        public int Floors;
    }

    /// <summary>
    /// guild-dungeon-scale.json: how many floors a guild's dungeon has, by how many members the
    /// guild had when its cycle opened (claude/DESIGN-GUILDS-PHASE-TWO.md, "Size": scale the CONTENT
    /// to the guild, not the payout per head).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason. The table
    /// decides how many floors exist for a guild, never what any floor is: a floor is a function of
    /// (seed, floor, hashed content) alone (GuildDungeonGenerator), so moving a band changes no
    /// fight and must not retire a week of every guild's replays.
    ///
    /// THE SERVER HAS THE REAL COPY. guild_dungeon_floors_for in
    /// Server/supabase/migrations/0022_guild_dungeon_size.sql decides a guild's ladder and refuses an
    /// attempt past it; this file is what the client predicts and prints. GuildDungeonChecks fails
    /// the build-check when the two disagree on any band.
    /// </summary>
    public sealed class GuildDungeonScaleConfig
    {
        /// <summary>Ascending by Members, the first at 1 member. Empty when the file is absent, and
        /// then every guild reads as the base ladder (FloorsFor).</summary>
        public readonly List<GuildDungeonLadderBand> Bands = new List<GuildDungeonLadderBand>();

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable { get { return Bands.Count > 0; } }

        /// <summary>
        /// Floors for a guild of this many members: the last band whose Members it reaches. Below
        /// the first band, or with no table, the base ladder. Total: a nonsense count reads as a
        /// guild of one.
        /// </summary>
        public int FloorsFor(int members, int baseLadder)
        {
            int floors = Math.Max(1, baseLadder);
            int m = Math.Max(1, members);
            for (int i = 0; i < Bands.Count; i++)
                if (m >= Bands[i].Members) floors = Bands[i].Floors;
            return floors;
        }

        /// <summary>The longest ladder any guild can have.</summary>
        public int MaxFloors(int baseLadder)
        {
            int max = Math.Max(1, baseLadder);
            for (int i = 0; i < Bands.Count; i++) max = Math.Max(max, Bands[i].Floors);
            return max;
        }

        /// <summary>
        /// Parses the file. A malformed band is dropped with a warning rather than guessed at; bands
        /// that do not ascend in Members, or that shrink the ladder as a guild grows, are refused as a
        /// whole, because a table out of order would give a growing guild fewer floors.
        /// </summary>
        public static GuildDungeonScaleConfig FromJson(string text)
        {
            GuildDungeonScaleConfig c = new GuildDungeonScaleConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("guild-dungeon-scale.json: " + err); return c; }

            List<JsonValue> rows = root.GetList("ladders");
            for (int i = 0; i < rows.Count; i++)
            {
                GuildDungeonLadderBand b = new GuildDungeonLadderBand();
                b.Members = rows[i].GetInt("members", 0);
                b.Floors = rows[i].GetInt("floors", 0);
                if (b.Members < 1 || b.Floors < 1)
                {
                    c.Warnings.Add("guild-dungeon-scale.json: ladder row " + i + " needs members and floors of at least 1");
                    continue;
                }
                c.Bands.Add(b);
            }
            for (int i = 1; i < c.Bands.Count; i++)
            {
                if (c.Bands[i].Members <= c.Bands[i - 1].Members || c.Bands[i].Floors < c.Bands[i - 1].Floors)
                {
                    c.Warnings.Add("guild-dungeon-scale.json: ladders must ascend in members and never shrink in floors - table ignored");
                    c.Bands.Clear();
                    break;
                }
            }
            return c;
        }
    }
}
