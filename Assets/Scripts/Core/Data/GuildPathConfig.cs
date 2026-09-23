using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// What one Guild Path node gives. EVERY KIND IS BREADTH: a way to hold more options, never a
    /// number a fight reads. The harness pins the list (GuildPathChecks.CheckEveryNodeIsHorizontal)
    /// and fights the same line with every node owned and with none, so a kind added here that
    /// reached a stat line would fail the build rather than ship.
    ///
    /// Appended, never inserted: a node names its kind by string in guild-path.json, but the order
    /// is the order the Path screen groups them in.
    /// </summary>
    public enum GuildPathNodeKind
    {
        /// <summary>One more saved line the formation can switch to (PlayerProfile.Lines).</summary>
        Line,
        /// <summary>A saved line remembers each hero's talent pick and restores it when switched
        /// to. Talents are free to change anyway (GameSession.SelectTalent), so this sells the
        /// convenience of two lines running one hero two ways, not a talent.</summary>
        LineTalents
    }

    /// <summary>One node of the Guild Path, as guild-path.json authors it.</summary>
    public sealed class GuildPathNodeDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The one sentence the Path screen prints under the name.</summary>
        public string Text = "";
        public GuildPathNodeKind Kind = GuildPathNodeKind.Line;
        /// <summary>Waymarks. The server's guild_path_nodes table holds the same number, and it is
        /// the one that is charged; GuildPathChecks fails the build when the two disagree.</summary>
        public int Cost;
        /// <summary>The node that must be owned first, or empty for a root.</summary>
        public string Requires = "";
        /// <summary>Steps from a root, 1 for a root. Derived at load from Requires, never
        /// authored: the cost rule is a function of it (see guild-path.json).</summary>
        public int Depth = 1;
    }

    /// <summary>
    /// guild-path.json: what the guild dungeon pays and what the Guild Path sells
    /// (claude/DESIGN-GUILDS-PHASE-TWO.md, system 2).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, deliberately, and that is why it is not
    /// folded into balance.json. The version exists to retire a guild dungeon replay when a number
    /// the fight reads moves. Nothing here is read by a fight, so a retune of the Path's prices
    /// must not retire a week of every guild's replays.
    ///
    /// Every pay and cost number is DERIVED by the harness (DevHarness.exe --guild-path) and
    /// re-asserted on every build-check; the server's copy in
    /// Server/supabase/migrations/0011_guild_path.sql is the one that is charged and paid.
    /// </summary>
    public sealed class GuildPathConfig
    {
        /// <summary>The currency's player-facing name.</summary>
        public string Currency = "Waymarks";

        /// <summary>Paid once a cycle, on the member's first attempt of it, won or lost.</summary>
        public int PayAttempt;
        /// <summary>Paid once a cycle, on the member's first clear of any floor.</summary>
        public int PayClear;
        /// <summary>Paid per board point after the cycle closes. A board point is a placement's
        /// share of the board beaten: first of n is 1, last is 0 (GuildPath.BoardScores).</summary>
        public int PayBoardPoint;
        /// <summary>The most board scores one cycle pays for, best first. Equal to the number of
        /// boards, so a member who changed guild mid-cycle is paid for one guild's worth.</summary>
        public int BoardsCounted = 6;
        /// <summary>The only floor whose boards pay. See GuildPath.BoardScores for why.</summary>
        public int BoardFloor = 1;

        public readonly List<GuildPathNodeDef> Nodes = new List<GuildPathNodeDef>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable { get { return Nodes.Count > 0; } }

        public GuildPathNodeDef Node(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Nodes.Count; i++) if (Nodes[i].Id == id) return Nodes[i];
            return null;
        }

        /// <summary>The most a member can be paid in one cycle, whatever a client claims: the
        /// base, plus a first place on every board counted.</summary>
        public int MostInOneCycle
        {
            get { return PayAttempt + PayClear + PayBoardPoint * BoardsCounted; }
        }

        public static GuildPathConfig FromJson(string text)
        {
            GuildPathConfig c = new GuildPathConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("guild-path.json: " + err); return c; }

            c.Currency = root.GetString("currency", c.Currency);
            JsonValue pay = root["pay"];
            c.PayAttempt = Math.Max(0, pay.GetInt("attempt", 0));
            c.PayClear = Math.Max(0, pay.GetInt("clear", 0));
            c.PayBoardPoint = Math.Max(0, pay.GetInt("boardPoint", 0));
            c.BoardsCounted = Math.Max(0, pay.GetInt("boardsCounted", c.BoardsCounted));
            c.BoardFloor = Math.Max(1, pay.GetInt("boardFloor", c.BoardFloor));

            List<JsonValue> nodes = root.GetList("nodes");
            for (int i = 0; i < nodes.Count; i++)
            {
                JsonValue n = nodes[i];
                GuildPathNodeDef d = new GuildPathNodeDef();
                d.Id = n.GetString("id", "");
                d.Name = n.GetString("name", d.Id);
                d.Text = n.GetString("text", "");
                d.Cost = n.GetInt("cost", 0);
                d.Requires = n.GetString("requires", "");
                string kind = n.GetString("kind", "");
                GuildPathNodeKind parsed;
                if (string.IsNullOrEmpty(d.Id) || c.Node(d.Id) != null)
                {
                    c.Warnings.Add("guild-path.json: a node has no id or repeats one (" + d.Id + ")");
                    continue;
                }
                if (!TryKind(kind, out parsed))
                {
                    c.Warnings.Add("guild-path.json: node " + d.Id + " names an unknown kind '" + kind + "'");
                    continue;
                }
                if (d.Cost <= 0)
                {
                    c.Warnings.Add("guild-path.json: node " + d.Id + " costs nothing");
                    continue;
                }
                d.Kind = parsed;
                c.Nodes.Add(d);
            }

            // Depth from Requires. A node whose requirement is missing, or that sits in a cycle,
            // is dropped rather than left unreachable on the screen.
            for (int i = 0; i < c.Nodes.Count; i++)
            {
                GuildPathNodeDef d = c.Nodes[i];
                int depth = 1;
                GuildPathNodeDef at = d;
                bool broken = false;
                while (!string.IsNullOrEmpty(at.Requires))
                {
                    at = c.Node(at.Requires);
                    depth++;
                    if (at == null || depth > c.Nodes.Count) { broken = true; break; }
                }
                if (broken) c.Warnings.Add("guild-path.json: node " + d.Id + " requires a node that is missing or circular");
                d.Depth = broken ? 0 : depth;
            }
            c.Nodes.RemoveAll(d => d.Depth == 0);
            return c;
        }

        private static bool TryKind(string text, out GuildPathNodeKind kind)
        {
            switch (text)
            {
                case "line": kind = GuildPathNodeKind.Line; return true;
                case "lineTalents": kind = GuildPathNodeKind.LineTalents; return true;
            }
            kind = GuildPathNodeKind.Line;
            return false;
        }
    }
}
