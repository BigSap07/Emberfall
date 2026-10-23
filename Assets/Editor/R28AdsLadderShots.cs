#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;
using Emberfall.Game;
using Emberfall.Game.Ads;
using Emberfall.Game.Cloud;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// THE STAND-INS BEHIND THE R28 LADDER AND AD CAPTURES (Docs/ui-specs/R28-SCREENS.md; the targets are in
    /// PlayModeScreenshot under "R28 screens (builder b)"). Nothing here is reachable from a player build and nothing
    /// here touches a network.
    ///
    /// R28LadderBackend is an IGuildDungeonBackend that answers with a status and a list of attempts it was given, so
    /// the REAL controller and presenter draw every state the spec lists, offline. The ad backends rehearse the
    /// endings the simulated backend cannot reach by itself: an ad that is still up (Playing), and an ad that finishes
    /// for an impression the save has already paid (Refused).
    /// </summary>
    public sealed class R28LadderBackend : IGuildDungeonBackend
    {
        public enum Mode
        {
            /// <summary>Status and the attempts arrive.</summary>
            Normal,
            /// <summary>Status never arrives: the screen sits in Loading, and the capture overlays the state it wants.</summary>
            Never,
            /// <summary>Status fails with ErrorText.</summary>
            Error
        }

        public Mode How = Mode.Normal;
        public string ErrorText = "The guild dungeon could not be reached.";
        public GuildDungeonStatus StatusRow;
        public List<GuildDungeonAttempt> Rows = new List<GuildDungeonAttempt>();
        public string Self = "u-me";
        public string Guild = "Ashen Oath";
        /// <summary>The live member count the screen's next-cycle line reads (GuildService's, which offline there is none of).</summary>
        public int LiveMembers = -1;

        public bool Ready { get { return true; } }
        public string NotReadyReason { get { return ""; } }
        public string SelfUserId { get { return Self; } }
        public string GuildName { get { return Guild; } }

        public IEnumerator Status(Action<GuildDungeonStatus, string> done)
        {
            if (How == Mode.Never) { while (true) yield return null; }
            if (How == Mode.Error) { done(null, ErrorText); yield break; }
            done(StatusRow, "");
        }

        public IEnumerator ListAttempts(int cycle, Action<List<GuildDungeonAttempt>, string> done)
        {
            done(new List<GuildDungeonAttempt>(Rows), "");
            yield break;
        }

        public IEnumerator Record(GuildDungeonAttempt attempt, Action<GuildDungeonAttempt, string> done)
        {
            done(null, "This stand-in stores nothing.");
            yield break;
        }

        public IEnumerator Share(GuildDungeonAttempt attempt, Action<bool, string> done)
        {
            done(false, "This stand-in shares nothing.");
            yield break;
        }

        public IEnumerator FetchChat(Action<List<GuildMessageEntry>, string> done)
        {
            done(new List<GuildMessageEntry>(), "");
            yield break;
        }

        public IEnumerator PostMessage(string typed, Action<bool, string> done)
        {
            done(false, "This stand-in posts nothing.");
            yield break;
        }
    }

    /// <summary>An ad that is still up: it never reports back, so the block stays in Playing.</summary>
    public sealed class R28HoldingAdBackend : IAdBackend
    {
        public bool IsReady(string adPlacement) { return true; }
        public void Show(string adPlacement, Action<AdShowResult> done) { }
    }

    /// <summary>An ad that finishes for an impression the save has already paid: AdRewards refuses it, the Refused ending.</summary>
    public sealed class R28RefusedAdBackend : IAdBackend
    {
        private const string Impression = "r28-already-paid";
        private readonly GameSession _session;

        public R28RefusedAdBackend(GameSession session) { _session = session; }

        public bool IsReady(string adPlacement) { return true; }

        public void Show(string adPlacement, Action<AdShowResult> done)
        {
            _session.Profile.Ads.Remember(Impression);
            AdShowResult r = new AdShowResult();
            r.Rewarded = true;
            r.ImpressionId = Impression;
            if (done != null) done(r);
        }
    }

    /// <summary>The ladder states, as data.</summary>
    public static class R28LadderStates
    {
        public const string Me = "u-me";
        public const int Cycle = 42;

        /// <summary>Every payload Screen:GuildLadder=&lt;state&gt; takes, in the order the spec lists them.</summary>
        public static readonly string[] All =
        {
            "offline-nocloud", "offline-signedout", "unavailable", "loading", "error",
            "g4-open", "g30-cap", "g12-grew", "changed-guild", "after-floor8",
            "banner-refresh", "banner-offline", "banner-refused", "banner-cycleover"
        };

        public static GuildDungeonStatus Status(int floors, int ladderMembers, int cap, int used, long now)
        {
            GuildDungeonStatus s = new GuildDungeonStatus();
            s.GuildId = "g-r28";
            s.Cycle = Cycle;
            s.StartsUnix = now - 3L * 86400L;
            s.EndsUnix = now + 2L * 86400L + 4L * 3600L + 30L;
            s.AttemptCap = 3;
            s.FloorCount = floors;
            s.CycleAttemptCap = cap;
            s.AttemptsUsed = used;
            s.LadderMembers = ladderMembers;
            return s;
        }

        public static GuildDungeonAttempt Attempt(string user, string name, int floor, bool victory)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            a.UserId = user;
            a.DisplayName = name;
            a.Cycle = Cycle;
            a.Floor = floor;
            a.Victory = victory;
            return a;
        }

        /// <summary>
        /// The backend for a state, and (through `overlay`) what to do to the presenter once the controller has loaded
        /// it. The five states GUILD-DUNGEON-SIZE.md asks for come out of Normal backends; the status-only states
        /// (Offline, Unavailable, Loading, Error) come out of a backend that never answers, or one that fails, with the
        /// presenter's own calls laid over it. The banner states lay a failed refresh, an offline connection or a
        /// refused attempt over a loaded ladder.
        /// </summary>
        public static R28LadderBackend For(string state, ContentDatabase content, long now, out Action<GuildDungeonLadderPresenter> overlay)
        {
            R28LadderBackend b = new R28LadderBackend();
            int cap = GuildDungeonScale.CycleAttemptCap(content.Balance);
            overlay = null;

            switch (state)
            {
                case "offline-nocloud":
                    b.How = R28LadderBackend.Mode.Never;
                    overlay = delegate (GuildDungeonLadderPresenter p) { p.SetOffline("This build has no online features, so there is no guild dungeon.", false); };
                    return b;
                case "offline-signedout":
                    b.How = R28LadderBackend.Mode.Never;
                    overlay = delegate (GuildDungeonLadderPresenter p) { p.SetOffline("You are offline, so the guild cannot be reached.", true); };
                    return b;
                case "unavailable":
                    b.How = R28LadderBackend.Mode.Never;
                    overlay = delegate (GuildDungeonLadderPresenter p) { p.SetNoGuild(); };
                    return b;
                case "loading":
                    b.How = R28LadderBackend.Mode.Never;
                    return b;
                case "error":
                    b.How = R28LadderBackend.Mode.Error;
                    return b;
            }

            // Everything below is a loaded ladder.
            switch (state)
            {
                case "g30-cap":
                {
                    // Fifteen of this member's own, two on each of floors 1 to 7 and one on 8, so no single floor is spent
                    // and the cycle cap is what stops every open floor; a guildmate cleared floor 7.
                    for (int f = 1; f <= 6; f++) b.Rows.Add(Attempt("u-mate", "Ana", f, true));
                    b.Rows.Add(Attempt("u-mate2", "Bo", 7, true));
                    for (int f = 1; f <= 7; f++) for (int k = 0; k < 2; k++) b.Rows.Add(Attempt(Me, "Me", f, f == 3 && k == 1));
                    b.Rows.Add(Attempt(Me, "Me", 8, false));
                    b.StatusRow = Status(10, 30, cap, cap, now);
                    b.LiveMembers = 30;
                    break;
                }
                case "g12-grew":
                    b.StatusRow = Status(5, 9, cap, 0, now);
                    b.LiveMembers = 12;
                    break;
                case "changed-guild":
                    // Fifteen attempts spent in another guild: the server's count wins over rows this guild cannot show.
                    b.Rows.Add(Attempt("u-mate", "Ana", 1, true));
                    b.StatusRow = Status(5, 6, cap, cap, now);
                    b.LiveMembers = 6;
                    break;
                case "after-floor8":
                {
                    // The member has just cleared floor 8: every floor to 8 cleared, 9 open, 10 locked.
                    for (int f = 1; f <= 8; f++) b.Rows.Add(Attempt(f % 3 == 0 ? "u-mate" : Me, f % 3 == 0 ? "Ana" : "Me", f, true));
                    b.Rows.Add(Attempt(Me, "Me", 8, true));
                    b.StatusRow = Status(10, 30, cap, 9, now);
                    b.LiveMembers = 30;
                    break;
                }
                case "banner-cycleover":
                    b.StatusRow = Status(5, 4, cap, 0, now);
                    b.StatusRow.EndsUnix = now - 5L;
                    b.LiveMembers = 4;
                    break;
                default:
                    // "g4-open", and the base of the banner states: a guild of 4 on cycle open.
                    b.StatusRow = Status(5, 4, cap, 0, now);
                    b.LiveMembers = 4;
                    break;
            }

            // The controller has no cloud offline, so it cannot know the guild's live size: the capture tells the
            // presenter, the way GuildService would.
            GuildDungeonStatus status = b.StatusRow;
            List<GuildDungeonAttempt> rows = b.Rows;
            string guild = b.Guild;
            int live = b.LiveMembers;
            switch (state)
            {
                case "banner-refresh":
                    overlay = delegate (GuildDungeonLadderPresenter p)
                    {
                        p.Loaded(status, rows, guild, live);
                        p.BeginLoad();
                        p.Failed("timed out");
                    };
                    break;
                case "banner-offline":
                    overlay = delegate (GuildDungeonLadderPresenter p)
                    {
                        p.Loaded(status, rows, guild, live);
                        p.SetOffline("You are offline, so the guild cannot be reached.", true);
                    };
                    break;
                case "banner-refused":
                    overlay = delegate (GuildDungeonLadderPresenter p)
                    {
                        p.Loaded(status, rows, guild, live);
                        p.AttemptStarted(1);
                        p.AttemptRefused("no attempts left on floor 1 this cycle (3 of 3)");
                    };
                    break;
                default:
                    overlay = delegate (GuildDungeonLadderPresenter p) { p.Loaded(status, rows, guild, live); };
                    break;
            }
            return b;
        }
    }
}
#endif
