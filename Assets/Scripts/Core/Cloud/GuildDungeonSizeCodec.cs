using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>One row of guild_dungeon_cycles (0016_guild_dungeon_size.sql): the member count a guild's
    /// ladder was set from when its cycle opened, and the ladder that set.</summary>
    public sealed class GuildDungeonCycleSnapshot
    {
        public int Cycle;
        public int MemberCount = -1;
        public int FloorCount = -1;
    }

    /// <summary>
    /// The bodies of the guild dungeon size functions: guild_dungeon_floors_for, guild_dungeon_floor_max and
    /// guild_dungeon_cycle_attempt_cap answer a bare integer, and guild_dungeon_cycles is read as rows.
    /// Engine-free so R28ScreenChecks runs it; Assets/Scripts/Game/Cloud/GuildDungeonSizeService.cs sends.
    /// </summary>
    public static class GuildDungeonSizeCodec
    {
        public const string CycleColumns = "cycle,member_count,floor_count";

        /// <summary>The JSON body of a guild_dungeon_floors_for call.</summary>
        public static string FloorsForArgs(int members)
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("p_members", Math.Max(0, members));
            return o.ToJson(false);
        }

        /// <summary>A scalar function's answer: PostgREST sends a bare number ("6"), and some proxies wrap it in a
        /// one-element list. -1 when the body is neither.</summary>
        public static int ReadScalarInt(string body)
        {
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null) return -1;
            if (root.IsArray) root = root.Count > 0 ? root[0] : null;
            if (root == null || root.Type != JsonType.Number) return -1;
            return root.AsInt(-1);
        }

        /// <summary>The first guild_dungeon_cycles row of the body, or null when there is none.</summary>
        public static GuildDungeonCycleSnapshot ReadSnapshot(string body)
        {
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsArray || root.Count == 0) return null;
            JsonValue row = root[0];
            if (row == null || !row.IsObject) return null;
            GuildDungeonCycleSnapshot s = new GuildDungeonCycleSnapshot();
            s.Cycle = row.GetInt("cycle", 0);
            s.MemberCount = row.GetInt("member_count", -1);
            s.FloorCount = row.GetInt("floor_count", -1);
            return s;
        }
    }
}
