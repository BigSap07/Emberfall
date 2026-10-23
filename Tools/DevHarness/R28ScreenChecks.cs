using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE R28 SCREENS' STATE (Docs/ui-specs/R28-SCREENS.md): the presenters behind the leaderboards, the guild dungeon's
    /// size-scaled ladder, the notification settings and the rewarded ad buttons, driven through every state each
    /// screen can be in; the leaderboard and ladder codecs against the exact shapes the two 0016 migrations return; the
    /// transport's function names against those migrations; and the rewarded ad rules (Docs/DESIGN-REWARDED-ADS.md):
    /// off equals before, fixed amounts paid as shown, the caps, the forward-only clock, once per impression, and no
    /// path that leaves a reward to luck.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR28Screens(ContentDatabase shipped, string contentDir)
        {
            Line("");
            Line("R28 SCREENS (Docs/ui-specs/R28-SCREENS.md: leaderboards, guild dungeon ladder, notifications, rewarded ads)");
            bool adsWas = AdsSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, liveWas = LiveEventsSwitch.Enabled;
            LeaderboardBoard weeklyWas = LeaderboardsPresenter.WeeklyBoard;
            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));
            try
            {
                CheckR28ScreenText();
                CheckR28LeaderboardCodec(root);
                CheckR28LeaderboardStates(shipped);
                CheckR28GuildCodec(root);
                CheckR28GuildLadder(shipped);
                CheckR28Notifications(shipped);
                CheckR28AdsConfig(shipped);
                CheckR28AdsOff(shipped);
                CheckR28AdRules(shipped);
                CheckR28AdPresenter(shipped);
                CheckR28AdsNothingLeftToLuck(root);
            }
            finally
            {
                AdsSwitch.Enabled = adsWas;
                DailyLoopSwitch.Enabled = loopWas;
                LiveEventsSwitch.Enabled = liveWas;
                LeaderboardsPresenter.WeeklyBoard = weeklyWas;
            }
            Check(AdsSwitch.Enabled == adsWas && DailyLoopSwitch.Enabled == loopWas && LiveEventsSwitch.Enabled == liveWas
                  && LeaderboardsPresenter.WeeklyBoard == weeklyWas, "R28: the switches are put back as the run found them");
        }

        // ------------------------------------------------------------------ shared

        /// <summary>Noon UTC on Thursday 2026-10-22: a weekday well clear of any reset.</summary>
        private static readonly long R28Now = (long)(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        private static GameSession R28Cleared(ContentDatabase d, string throughStageId)
        {
            PlayerProfile p = GameSession.CreateNewProfile(d, R28Now - 30L * 86400L);
            if (throughStageId != null)
            {
                for (int i = 0; i < d.AllStages.Count; i++)
                {
                    p.ClearedStageIds.Add(d.AllStages[i].Id);
                    if (d.AllStages[i].Id == throughStageId) break;
                }
            }
            p.StagesCleared = CampaignProgress.CountCleared(p, d);
            return new GameSession(d, p);
        }

        /// <summary>Words a screen that sells nothing must never show. Word-bounded, any case.</summary>
        private static readonly Regex R28SellWords = new Regex(@"\b(buy|bought|purchase|price|prices|offer|offers|pack|packs|store|shop|sale|discount|refill|euro|gems?)\b|\u20AC",
                                                               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static List<string> R28Texts(LeaderboardsView v)
        {
            List<string> t = new List<string> { v.Title, v.Caption, v.ValueHeader, v.MyRankLine, v.Status, v.Banner, v.ResetLine, v.UpdatedLine, v.SubmitLine, v.LockLine };
            R28Button(t, v.Refresh); R28Button(t, v.Retry);
            for (int i = 0; i < v.Tabs.Count; i++) t.Add(v.Tabs[i].Label);
            for (int i = 0; i < v.Rows.Count; i++) { t.Add(v.Rows[i].RankText); t.Add(v.Rows[i].Name); t.Add(v.Rows[i].ValueText); }
            if (v.PinnedSelf != null) { t.Add(v.PinnedSelf.RankText); t.Add(v.PinnedSelf.ValueText); }
            return t;
        }

        private static List<string> R28Texts(GuildDungeonLadderView v)
        {
            List<string> t = new List<string> { v.Title, v.GuildName, v.LadderLine, v.NextCycleLine, v.AttemptsLine, v.CycleLine, v.Status, v.Banner };
            R28Button(t, v.Boards); R28Button(t, v.Refresh); R28Button(t, v.Retry); R28Button(t, v.FindGuild);
            for (int i = 0; i < v.Rows.Count; i++)
            {
                GuildFloorRowView r = v.Rows[i];
                t.Add(r.Title); t.Add(r.DividerCaption); t.Add(r.StateText); t.Add(r.AttemptsText); t.Add(r.FirstClearText); t.Add(r.RewardText);
                R28Button(t, r.Attempt);
            }
            return t;
        }

        private static List<string> R28Texts(NotificationSettingsView v)
        {
            List<string> t = new List<string> { v.Title, v.Status, v.PermissionLine, v.UpcomingStatus };
            R28Button(t, v.PermissionButton);
            ToggleModel[] all = { v.Master, v.IdleChest, v.Orders, v.Events };
            for (int i = 0; i < all.Length; i++) { t.Add(all[i].Label); t.Add(all[i].Description); t.Add(all[i].Reason); t.Add(all[i].Note); }
            t.AddRange(v.RuleLines);
            for (int i = 0; i < v.Upcoming.Count; i++) { t.Add(v.Upcoming[i].Title); t.Add(v.Upcoming[i].Body); t.Add(v.Upcoming[i].WhenText); }
            return t;
        }

        private static List<string> R28Texts(RewardedAdView v)
        {
            List<string> t = new List<string> { v.RewardLine, v.LeftTodayText, v.ResultLine, v.NextResetText };
            R28Button(t, v.Watch); R28Button(t, v.Dismiss);
            return t;
        }

        private static void R28Button(List<string> into, ButtonModel b)
        {
            if (b == null) return;
            into.Add(b.Label);
            into.Add(b.Reason);
        }

        /// <summary>Collects every string the views of one screen produced, for the dash and selling scans at the end of each block.</summary>
        private static string R28Dashes(List<string> texts)
        {
            for (int i = 0; i < texts.Count; i++) if (ScreenText.HasLongDash(texts[i])) return texts[i];
            return null;
        }

        private static string R28Selling(List<string> texts)
        {
            for (int i = 0; i < texts.Count; i++) if (texts[i] != null && R28SellWords.IsMatch(texts[i])) return texts[i];
            return null;
        }

        private static long R28Unix(int y, int mo, int d, int h, int mi)
        {
            return (long)(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        // ------------------------------------------------------------------ text helpers

        private static void CheckR28ScreenText()
        {
            Check(ScreenText.Countdown(0) == "now" && ScreenText.Countdown(45) == "45s" && ScreenText.Countdown(12 * 60 + 5) == "12m 5s"
                  && ScreenText.Countdown(3 * 3600 + 12 * 60) == "3h 12m" && ScreenText.Countdown(2 * 86400 + 4 * 3600 + 59) == "2d 4h",
                  "R28 countdowns read in their two largest units: now, 45s, 12m 5s, 3h 12m, 2d 4h");
            Check(ScreenText.Group(41200) == "41,200" && ScreenText.Group(0) == "0" && ScreenText.Group(1234567) == "1,234,567",
                  "R28 numbers carry thousands separators (41,200)");
            long thu2145 = R28Unix(2026, 10, 22, 21, 45);
            Check(ScreenText.LocalDayAndClock(thu2145, R28Now, 0) == "today 21:45"
                  && ScreenText.LocalDayAndClock(R28Unix(2026, 10, 23, 9, 0), R28Now, 0) == "tomorrow 09:00"
                  && ScreenText.LocalDayAndClock(R28Unix(2026, 10, 24, 9, 0), R28Now, 0) == "Sat 09:00"
                  && ScreenText.LocalDayAndClock(R28Unix(2026, 10, 22, 22, 30), R28Now, 120) == "tomorrow 00:30",
                  "R28 reminder times read on the device's calendar: today 21:45, tomorrow 09:00, Sat 09:00, and UTC+2 moves 22:30 UTC to tomorrow 00:30");
            Check(ScreenText.HasLongDash("a \u2014 b") && ScreenText.HasLongDash("1\u20132") && !ScreenText.HasLongDash("SECOND BLOCK - the climb"),
                  "R28 the dash scan finds an em dash and an en dash and passes a hyphen-minus");
        }

        // ------------------------------------------------------------------ leaderboards

        private static void CheckR28LeaderboardCodec(string root)
        {
            string top = "[{\"rank\":1,\"display_name\":\"Ana\",\"value\":41,\"is_me\":false},"
                       + "{\"rank\":1,\"display_name\":\"Bo\",\"value\":41,\"is_me\":false},"
                       + "{\"rank\":3,\"display_name\":\"Me\",\"value\":30,\"is_me\":true},"
                       + "{\"rank\":0,\"display_name\":\"bad\",\"value\":1,\"is_me\":false}]";
            List<LeaderboardRow> rows = LeaderboardCodec.ReadTop(top);
            Check(rows != null && rows.Count == 3 && rows[0].DisplayName == "Ana" && rows[1].Rank == 1 && rows[2].IsMe && rows[2].Value == 30,
                  "leaderboard_top's rows read as rank, name, value and is_me; a tie shares its rank; a row without a rank is dropped");
            Check(LeaderboardCodec.ReadTop("{\"message\":\"x\"}") == null && LeaderboardCodec.ReadTop("[]").Count == 0,
                  "a body that is not a list reads as no answer, an empty list as an empty board");

            string submit = "{\"server_time\":\"2026-10-22T12:00:30+00:00\",\"week_start\":\"2026-10-19T04:00:00+00:00\",\"save_revision\":12,"
                          + "\"recital\":{\"status\":\"raised\",\"value\":41},\"proving\":{\"status\":\"kept\",\"value\":30},"
                          + "\"campaign\":{\"status\":\"refused\",\"value\":30,\"allowed\":22,\"reason\":\"faster than play\"}}";
            LeaderboardSubmitResult s = LeaderboardCodec.ReadSubmit(submit);
            Check(s != null && s.ServerTimeUnix == R28Now + 30 && s.WeekStartUnix == R28Unix(2026, 10, 19, 4, 0) && s.SaveRevision == 12
                  && s.Recital.Verdict == LeaderboardVerdict.Raised && s.Recital.Value == 41
                  && s.Proving.Verdict == LeaderboardVerdict.Kept && s.Campaign.Verdict == LeaderboardVerdict.Refused
                  && s.Campaign.Allowed == 22 && s.Campaign.Reason == "faster than play",
                  "leaderboard_submit's answer reads the server's clock, the board week, the revision and each board's raised, kept or refused");

            Check(LeaderboardCodec.FailureOf(200, "{}") == LeaderboardFailure.None
                  && LeaderboardCodec.FailureOf(0, "") == LeaderboardFailure.Offline
                  && LeaderboardCodec.FailureOf(401, "{\"code\":\"PT401\"}") == LeaderboardFailure.NotSignedIn
                  && LeaderboardCodec.FailureOf(404, "{\"code\":\"PT404\",\"message\":\"no cloud save\"}") == LeaderboardFailure.NoCloudSave
                  && LeaderboardCodec.FailureOf(404, "{\"code\":\"PGRST202\",\"message\":\"Could not find the function\"}") == LeaderboardFailure.NotDeployed
                  && LeaderboardCodec.FailureOf(400, "{\"code\":\"PT400\"}") == LeaderboardFailure.SaveTooOld
                  && LeaderboardCodec.FailureOf(429, "{\"code\":\"PT429\"}") == LeaderboardFailure.TooSoon
                  && LeaderboardCodec.FailureOf(500, "") == LeaderboardFailure.Server,
                  "each refusal 0016 raises reads as its own failure, and a 404 is told apart by its code: PT404 is no cloud save, PGRST202 is 0016 not applied");

            JsonValue argsHigh = JsonValue.Parse(LeaderboardCodec.TopArgs(LeaderboardBoard.RecitalWeekClimb, 500));
            JsonValue argsLow = JsonValue.Parse(LeaderboardCodec.TopArgs(LeaderboardBoard.Campaign, 0));
            Check(argsHigh.GetString("p_board", "") == "recital_week_climb" && argsHigh.GetInt("p_limit", 0) == 100 && argsHigh.Count == 2
                  && argsLow.GetString("p_board", "") == "campaign" && argsLow.GetInt("p_limit", 0) == 1,
                  "leaderboard_top's arguments name the board as 0016 spells it and clamp the limit to 1..100 as the server does");

            Check(LeaderboardCodec.NextWeekResetUnix(R28Now) == R28Unix(2026, 10, 26, 4, 0)
                  && LeaderboardCodec.NextWeekResetUnix(R28Unix(2026, 10, 26, 3, 59)) == R28Unix(2026, 10, 26, 4, 0)
                  && LeaderboardCodec.NextWeekResetUnix(R28Unix(2026, 10, 26, 4, 0)) == R28Unix(2026, 11, 2, 4, 0)
                  && LeaderboardCodec.NextWeekResetUnix(R28Unix(2026, 10, 25, 23, 0)) == R28Unix(2026, 10, 26, 4, 0),
                  "the weekly board turns on Monday at 04:00 UTC: Thursday and Sunday night point at the next Monday, 03:59 at the same morning, 04:00 at the week after");

            string sql = R28Read(root, "Server/supabase/migrations/0016_leaderboards.sql");
            string svc = R28Read(root, "Assets/Scripts/Game/Cloud/LeaderboardService.cs");
            if (sql == null || svc == null) { Check(false, "0016_leaderboards.sql and LeaderboardService.cs are found under " + root); return; }
            bool names = true;
            foreach (LeaderboardBoard b in Enum.GetValues(typeof(LeaderboardBoard))) names &= sql.Contains("'" + LeaderboardCodec.IdOf(b) + "'");
            Check(names, "every board the client can ask for is a board 0016's leaderboard_top accepts");
            Check(sql.Contains("function public.leaderboard_submit()") && svc.Contains("rpc/leaderboard_submit\", \"{}\"")
                  && sql.Contains("function public.leaderboard_top(p_board text, p_limit integer") && svc.Contains("rpc/leaderboard_top"),
                  "the transport calls the two functions 0016 defines, and leaderboard_submit with no argument (the client never sends a score)");
            Check(sql.Contains("60, 4, 8)") && LeaderboardCodec.SubmitIntervalSeconds == 60 && LeaderboardCodec.WeekResetHourUtc == 4,
                  "the client's submit interval (60 s) and week reset hour (4) are the values 0016 seeds into leaderboard_limits");
        }

        private static string R28Read(string root, string relative)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static List<LeaderboardRow> R28Rows(int count, int meRank)
        {
            List<LeaderboardRow> rows = new List<LeaderboardRow>();
            for (int i = 1; i <= count; i++)
            {
                LeaderboardRow r = new LeaderboardRow();
                r.Rank = i;
                r.DisplayName = "Player" + i;
                r.Value = 1000 - i;
                r.IsMe = i == meRank;
                rows.Add(r);
            }
            if (meRank > count)
            {
                LeaderboardRow me = new LeaderboardRow();
                me.Rank = meRank;
                me.DisplayName = "Me";
                me.Value = 12;
                me.IsMe = true;
                rows.Add(me);
            }
            return rows;
        }

        private static void CheckR28LeaderboardStates(ContentDatabase d)
        {
            List<string> texts = new List<string>();
            GameSession early = R28Cleared(d, "ch1_3");
            GameSession late = R28Cleared(d, "ch3_1");
            long now = R28Now;

            LeaderboardsPresenter p = new LeaderboardsPresenter();
            p.SetOffline("This build has no online features, so there are no boards.", false);
            LeaderboardsView v = p.Build(d, early.Profile, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Offline && !v.Retry.Visible && !v.Refresh.Visible && v.Status.Length > 0 && v.Tabs.Count == 4 && v.Tabs[0].Selected,
                  "LEADERBOARDS OFFLINE, NO CLOUD: the reason is the status, there is no TRY AGAIN (nothing to retry), and the four tabs still draw");

            p = new LeaderboardsPresenter();
            p.SetOffline("You are offline, so the boards cannot be reached.", true);
            v = p.Build(d, early.Profile, now);
            Check(v.Phase == ScreenPhase.Offline && v.Retry.Visible && v.Retry.Enabled && v.Retry.Label == "TRY AGAIN",
                  "LEADERBOARDS OFFLINE, SIGNED OUT: TRY AGAIN is drawn and tappable");

            p = new LeaderboardsPresenter();
            p.Online();
            p.BeginLoad(LeaderboardBoard.Recital);
            v = p.Build(d, early.Profile, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Loading && v.ShowSpinner && v.Refresh.Visible && !v.Refresh.Enabled && v.Rows.Count == 0,
                  "LEADERBOARDS LOADING: a spinner, REFRESH drawn but not tappable, no rows");

            p.TopLoaded(LeaderboardBoard.Recital, R28Rows(10, 2), now);
            v = p.Build(d, late.Profile, now + 30);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Ready && v.Rows.Count == 10 && v.Rows[1].IsMe && v.PinnedSelf == null && v.MyRankLine == "You are #2"
                  && v.Rows[0].RankText == "#1" && v.ValueHeader == "FLOOR" && v.ResetLine == "All time" && v.ResetInSeconds == -1
                  && v.UpdatedLine == "Updated just now" && v.Refresh.Enabled && !v.ShowSpinner && v.LockLine == "",
                  "LEADERBOARDS READY: ten rows best first, the caller's own row highlighted in place, 'You are #2', FLOOR, All time, REFRESH tappable");

            v = p.Build(d, late.Profile, now + 3 * 60 + 10);
            Check(v.UpdatedLine == "Updated 3m 10s ago", "LEADERBOARDS: the age of the rows reads 'Updated 3m 10s ago' (" + v.UpdatedLine + ")");

            p.TopLoaded(LeaderboardBoard.Recital, R28Rows(LeaderboardsPresenter.Limit, LeaderboardsPresenter.Limit + 262), now);
            v = p.Build(d, late.Profile, now);
            Check(v.Rows.Count == LeaderboardsPresenter.Limit && v.PinnedSelf != null && v.PinnedSelf.IsMe && v.PinnedSelf.RankText == "#312"
                  && v.MyRankLine == "You are #312",
                  "LEADERBOARDS, CALLER BELOW THE TOP: the top " + LeaderboardsPresenter.Limit + " are the rows and the caller's own row is pinned under them (#312)");

            p.TopLoaded(LeaderboardBoard.Recital, R28Rows(5, 0), now);
            v = p.Build(d, late.Profile, now);
            Check(v.MyRankLine == "You are not on this board yet" && v.PinnedSelf == null, "LEADERBOARDS, CALLER ABSENT: 'You are not on this board yet'");

            p.BeginLoad(LeaderboardBoard.Recital);
            v = p.Build(d, late.Profile, now);
            Check(v.Phase == ScreenPhase.Ready && v.ShowSpinner && v.Rows.Count == 5 && !v.Refresh.Enabled && v.Refresh.Label == "REFRESHING",
                  "LEADERBOARDS REFRESHING: the last rows stay, a spinner, REFRESHING not tappable");

            p.TopFailed(LeaderboardBoard.Recital, LeaderboardFailure.Server);
            v = p.Build(d, late.Profile, now + 120);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Ready && v.Rows.Count == 5 && v.Banner.StartsWith("Could not refresh") && v.Refresh.Enabled,
                  "LEADERBOARDS, REFRESH FAILED: the rows stay, a banner says so, REFRESH tappable again");

            p.SetOffline("You are offline, so the boards cannot be reached.", true);
            v = p.Build(d, late.Profile, now + 120);
            Check(v.Phase == ScreenPhase.Ready && v.Rows.Count == 5 && v.Banner.StartsWith("Offline."),
                  "LEADERBOARDS, OFFLINE WITH ROWS: the rows stay under an offline banner");

            p = new LeaderboardsPresenter();
            p.Online();
            p.BeginLoad(LeaderboardBoard.Recital);
            p.TopFailed(LeaderboardBoard.Recital, LeaderboardFailure.Server);
            v = p.Build(d, late.Profile, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Error && v.Retry.Visible && v.Status == LeaderboardsPresenter.FailureSentence(LeaderboardFailure.Server),
                  "LEADERBOARDS ERROR: the failure's sentence and TRY AGAIN");
            p.BeginLoad(LeaderboardBoard.Recital);
            p.TopFailed(LeaderboardBoard.Recital, LeaderboardFailure.NotDeployed);
            v = p.Build(d, late.Profile, now);
            Check(v.Phase == ScreenPhase.Error && !v.Retry.Visible && v.Status == "The leaderboards are not open yet.",
                  "LEADERBOARDS NOT DEPLOYED (0016 not applied): 'The leaderboards are not open yet.' and no TRY AGAIN");

            Check(p.Select(LeaderboardBoard.Campaign), "LEADERBOARDS TABS: selecting a board never loaded asks the controller to load it");
            p.BeginLoad(LeaderboardBoard.Campaign);
            p.TopLoaded(LeaderboardBoard.Campaign, new List<LeaderboardRow>(), now);
            v = p.Build(d, early.Profile, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Empty && v.Status == "Nobody is on this board yet." && v.ValueHeader == "STAGES" && v.Tabs[3].Selected && !v.Tabs[0].Selected,
                  "LEADERBOARDS EMPTY: 'Nobody is on this board yet.', the CAMPAIGN tab selected, STAGES");
            p.Select(LeaderboardBoard.Recital);
            Check(!p.Select(LeaderboardBoard.Campaign), "LEADERBOARDS TABS: going back to a loaded board does not reload it");

            p.Select(LeaderboardsPresenter.WeeklyBoard);
            p.BeginLoad(LeaderboardsPresenter.WeeklyBoard);
            p.TopLoaded(LeaderboardsPresenter.WeeklyBoard, new List<LeaderboardRow>(), now);
            v = p.Build(d, early.Profile, now);
            long toMonday = R28Unix(2026, 10, 26, 4, 0) - now;
            Check(v.Phase == ScreenPhase.Empty && v.Status == "Nobody has climbed this week yet." && v.ResetInSeconds == toMonday
                  && v.ResetLine == "Resets in " + ScreenText.Countdown(toMonday) && v.Tabs[1].Label == "THIS WEEK",
                  "LEADERBOARDS WEEKLY: 'Resets in " + ScreenText.Countdown(toMonday) + "' to Monday 04:00 UTC, and its own empty line");
            Check(v.LockLine == FeatureGate.LockLine(d, Feature.Recital) && (v.LockLine.Length > 0 || !OnboardingSwitch.Enabled),
                  "LEADERBOARDS LOCKED: a player who has not opened the Recital sees its lock line over the board (" + v.LockLine + ")");

            LeaderboardSubmitResult answer = new LeaderboardSubmitResult();
            answer.ServerTimeUnix = now + 600;
            answer.WeekStartUnix = R28Unix(2026, 10, 19, 4, 0);
            answer.Recital.Verdict = LeaderboardVerdict.Raised; answer.Recital.Value = 41;
            answer.Proving.Verdict = LeaderboardVerdict.Kept; answer.Proving.Value = 30;
            answer.Campaign.Verdict = LeaderboardVerdict.Refused; answer.Campaign.Value = 30;
            Check(p.ShouldSubmit(now), "LEADERBOARDS SUBMIT: the first submission of a visit is due");
            p.SubmitStarted(now);
            Check(!p.ShouldSubmit(now + 5), "LEADERBOARDS SUBMIT: none while one is in flight");
            p.SubmitAnswered(answer, now);
            Check(!p.ShouldSubmit(now + 59) && p.ShouldSubmit(now + 60), "LEADERBOARDS SUBMIT: at most one a minute, so the server's flood limit never refuses one");
            v = p.Build(d, late.Profile, now);
            Check(v.ResetInSeconds == toMonday - 600, "LEADERBOARDS WEEKLY: the countdown runs on the server's clock once it is known (the device here is 10 minutes slow)");
            Check(v.SubmitLine == "Your new best, floor 41, is on the board.", "LEADERBOARDS SUBMIT RAISED: '" + v.SubmitLine + "'");
            p.Select(LeaderboardBoard.Proving);
            v = p.Build(d, late.Profile, now);
            Check(v.SubmitLine == "Your best on this board is standing 30.", "LEADERBOARDS SUBMIT KEPT: '" + v.SubmitLine + "'");
            p.Select(LeaderboardBoard.Campaign);
            v = p.Build(d, late.Profile, now);
            texts.AddRange(R28Texts(v));
            Check(v.SubmitLine.StartsWith("Your latest result could not be added yet") && v.SubmitLine.IndexOf("faster", StringComparison.OrdinalIgnoreCase) < 0,
                  "LEADERBOARDS SUBMIT REFUSED: a neutral line, never the server's reason");
            p.SubmitFailed(LeaderboardFailure.NoCloudSave);
            Check(p.Build(d, late.Profile, now).SubmitLine.StartsWith("Sync your save"), "LEADERBOARDS SUBMIT, NO CLOUD SAVE: 'Sync your save from the Account screen...'");
            p.SubmitFailed(LeaderboardFailure.SaveTooOld);
            Check(p.Build(d, late.Profile, now).SubmitLine == "Update the game to appear on the boards.", "LEADERBOARDS SUBMIT, SAVE TOO OLD: 'Update the game...'");
            p.SubmitFailed(LeaderboardFailure.TooSoon);
            Check(p.Build(d, late.Profile, now).SubmitLine.StartsWith("Your latest result"), "LEADERBOARDS SUBMIT, TOO SOON: quiet, the last answer's line stays");
            p.SubmitFailed(LeaderboardFailure.Offline);
            Check(!p.ShouldSubmit(now + 3600), "LEADERBOARDS SUBMIT: none is due while offline");
            p.Online();
            Check(p.ShouldSubmit(now + 3600), "LEADERBOARDS SUBMIT: back online, a submission is due again");

            LeaderboardsPresenter.WeeklyBoard = LeaderboardBoard.RecitalWeekClimb;
            LeaderboardsPresenter climb = new LeaderboardsPresenter();
            climb.Select(LeaderboardBoard.RecitalWeekClimb);
            climb.Online();
            climb.TopLoaded(LeaderboardBoard.RecitalWeekClimb, R28Rows(3, 1), now);
            v = climb.Build(d, late.Profile, now);
            texts.AddRange(R28Texts(v));
            Check(v.Tabs[1].Board == LeaderboardBoard.RecitalWeekClimb && v.ValueHeader == "CLIMBED" && v.Rows[0].ValueText.StartsWith("+"),
                  "LEADERBOARDS, OWNER DECISION 1 FLIPPED: the THIS WEEK tab shows floors climbed (CLIMBED, '+N')");
            LeaderboardsPresenter.WeeklyBoard = LeaderboardBoard.RecitalWeek;

            string dash = R28Dashes(texts);
            Check(dash == null, "LEADERBOARDS: no em or en dash in any string the screen draws" + (dash != null ? " (" + dash + ")" : ""));
            string sell = R28Selling(texts);
            Check(sell == null, "LEADERBOARDS: nothing on the screen sells or prices anything; a rank is never bought" + (sell != null ? " (" + sell + ")" : ""));
        }

        // ------------------------------------------------------------------ guild dungeon ladder

        private static void CheckR28GuildCodec(string root)
        {
            Check(GuildDungeonSizeCodec.ReadScalarInt("6") == 6 && GuildDungeonSizeCodec.ReadScalarInt("[7]") == 7
                  && GuildDungeonSizeCodec.ReadScalarInt("{\"code\":\"PGRST202\"}") == -1 && GuildDungeonSizeCodec.ReadScalarInt("") == -1,
                  "guild_dungeon_floors_for and its siblings answer a bare integer, read as such; anything else reads as -1 (fall back to the client's prediction)");
            GuildDungeonCycleSnapshot snap = GuildDungeonSizeCodec.ReadSnapshot("[{\"cycle\":42,\"member_count\":31,\"floor_count\":10}]");
            Check(snap != null && snap.Cycle == 42 && snap.MemberCount == 31 && snap.FloorCount == 10 && GuildDungeonSizeCodec.ReadSnapshot("[]") == null,
                  "a guild_dungeon_cycles row reads as the cycle, the member count and the ladder; no row reads as none");
            Check(JsonValue.Parse(GuildDungeonSizeCodec.FloorsForArgs(31)).GetInt("p_members", 0) == 31, "guild_dungeon_floors_for is called with p_members, as 0016 names it");

            string sql = R28Read(root, "Server/supabase/migrations/0016_guild_dungeon_size.sql");
            string svc = R28Read(root, "Assets/Scripts/Game/Cloud/GuildDungeonSizeService.cs");
            if (sql == null || svc == null) { Check(false, "0016_guild_dungeon_size.sql and GuildDungeonSizeService.cs are found under " + root); return; }
            string[] fns = { "guild_dungeon_floors_for", "guild_dungeon_floor_max", "guild_dungeon_cycle_attempt_cap" };
            bool all = true;
            for (int i = 0; i < fns.Length; i++)
                all &= svc.Contains("\"" + fns[i] + "\"") && sql.Contains("grant execute on function public." + fns[i]);
            Check(all && sql.Contains("grant select on public.guild_dungeon_cycles to authenticated") && svc.Contains("guild_dungeon_cycles?select="),
                  "the transport calls exactly the three functions 0016 grants to a signed-in client, and reads guild_dungeon_cycles, which it lets members select");
            Check(sql.Contains("cycle_attempt_cap integer") && sql.Contains("attempts_used     integer") && sql.Contains("ladder_members    integer"),
                  "guild_dungeon_status answers the three columns GuildDungeonStatus.Read and the ladder screen read");
        }

        private static GuildDungeonStatus R28Status(int floors, int ladderMembers, int cap, int used, long now)
        {
            GuildDungeonStatus s = new GuildDungeonStatus();
            s.GuildId = "g-r28";
            s.Cycle = 42;
            s.StartsUnix = now - 3L * 86400L;
            s.EndsUnix = now + 2L * 86400L + 4L * 3600L + 30L;
            s.AttemptCap = 3;
            s.FloorCount = floors;
            s.CycleAttemptCap = cap;
            s.AttemptsUsed = used;
            s.LadderMembers = ladderMembers;
            return s;
        }

        private static GuildDungeonAttempt R28Attempt(string user, string name, int floor, bool victory)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            a.UserId = user;
            a.DisplayName = name;
            a.Cycle = 42;
            a.Floor = floor;
            a.Victory = victory;
            return a;
        }

        /// <summary>True when every disabled ATTEMPT carries exactly the sentence GuildDungeonScale.CanAttempt gives.</summary>
        private static bool R28ReasonsVerbatim(ContentDatabase d, GuildDungeonLadderView v, List<GuildDungeonAttempt> attempts, string self, GuildDungeonStatus s)
        {
            for (int i = 0; i < v.Rows.Count; i++)
            {
                string reason;
                bool can = GuildDungeonScale.CanAttempt(d, attempts, self, s.Cycle, v.Rows[i].Floor, s.FloorCount, s.AttemptsUsed, out reason);
                if (can != v.Rows[i].Attempt.Enabled) return false;
                if (!can && v.Rows[i].Attempt.Reason != reason) return false;
            }
            return true;
        }

        /// <summary>Every ATTEMPT disabled: an open floor's with the cycle sentence, a locked floor's with its opening sentence.</summary>
        private static bool R28CycleCapped(GuildDungeonLadderView v, int cap)
        {
            int open = 0;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                GuildFloorRowView r = v.Rows[i];
                if (r.Attempt.Enabled) return false;
                if (r.State == GuildFloorState.Locked)
                {
                    if (r.Attempt.Reason != "floor " + r.Floor + " opens when somebody in your guild clears floor " + (r.Floor - 1)) return false;
                }
                else
                {
                    open++;
                    if (r.Attempt.Reason != "no attempts left this cycle (" + cap + " of " + cap + ")") return false;
                }
            }
            return open > 0;
        }

        private static void CheckR28GuildLadder(ContentDatabase d)
        {
            List<string> texts = new List<string>();
            long now = R28Now;
            const string me = "u-me";
            int cap = GuildDungeonScale.CycleAttemptCap(d.Balance);

            GuildDungeonLadderPresenter p = new GuildDungeonLadderPresenter();
            p.SetOffline("This build has no online features, so there is no guild dungeon.", false);
            GuildDungeonLadderView v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Offline && !v.Retry.Visible && v.Rows.Count == 0, "GUILD LADDER OFFLINE, NO CLOUD: the reason, no TRY AGAIN, no rows");
            p.SetOffline("You are offline, so the guild cannot be reached.", true);
            v = p.Build(d, me, now);
            Check(v.Phase == ScreenPhase.Offline && v.Retry.Visible, "GUILD LADDER OFFLINE, SIGNED OUT: TRY AGAIN");

            p = new GuildDungeonLadderPresenter();
            p.SetNoGuild();
            v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Unavailable && v.FindGuild.Visible && v.FindGuild.Enabled && v.FindGuild.Label == "FIND A GUILD" && v.Rows.Count == 0,
                  "GUILD LADDER, NOT IN A GUILD: the reason and FIND A GUILD");

            p = new GuildDungeonLadderPresenter();
            p.BeginLoad();
            v = p.Build(d, me, now);
            Check(v.Phase == ScreenPhase.Loading && v.ShowSpinner, "GUILD LADDER LOADING: a spinner");
            p.Failed("the dungeon did not answer");
            v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Error && v.Status == "the dungeon did not answer" && v.Retry.Visible, "GUILD LADDER ERROR: the sentence and TRY AGAIN");

            // Spec state 1: a guild of 4 on cycle open.
            List<GuildDungeonAttempt> none = new List<GuildDungeonAttempt>();
            GuildDungeonStatus s1 = R28Status(5, 4, cap, 0, now);
            p = new GuildDungeonLadderPresenter();
            p.BeginLoad();
            p.Loaded(s1, none, "Ashen Oath", 4);
            v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Ready && v.Rows.Count == 5 && v.PipCount == 5 && v.PipsCleared == 0 && v.GuildName == "Ashen Oath"
                  && v.LadderLine == "5 FLOORS THIS CYCLE - set at 4 members when the cycle opened."
                  && v.NextCycleLine == "A guild of 10 gets 6 floors from next cycle."
                  && v.AttemptsLine == "ATTEMPTS " + cap + " / " + cap + " THIS CYCLE",
                  "GUILD LADDER, A GUILD OF 4 ON CYCLE OPEN: 5 floors set at 4 members, 'a guild of 10 gets 6 floors from next cycle', all " + cap + " attempts left");
            Check(v.Rows[0].Attempt.Enabled && v.Rows[0].State == GuildFloorState.Open && !v.Rows[1].Attempt.Enabled
                  && v.Rows[1].Attempt.Reason == "floor 2 opens when somebody in your guild clears floor 1" && v.Rows[1].State == GuildFloorState.Locked
                  && v.Rows[4].Champion && !v.Rows[3].Champion && !v.Rows[4].DividerBefore && R28ReasonsVerbatim(d, v, none, me, s1),
                  "GUILD LADDER: floor 1 open, every other ATTEMPT disabled with GuildDungeonScale.CanAttempt's sentence verbatim, floor 5 a Champion");
            Check(v.Rows[0].AttemptsText == "3 of 3 left" && !v.Boards.Enabled && v.Boards.Reason.Length > 0
                  && v.CycleLine == "Cycle ends in 2d 4h" && v.CycleEndsInSeconds == 2L * 86400L + 4L * 3600L + 30L,
                  "GUILD LADDER: '3 of 3 left' per floor, BOARDS disabled before any attempt, 'Cycle ends in 2d 4h'");
            bool placeholder = true;
            for (int i = 0; i < v.Rows.Count; i++) placeholder &= v.Rows[i].RewardText == GuildDungeonLadderPresenter.PlaceholderRewardText && v.Rows[i].RewardMuted;
            Check(placeholder && GuildDungeonRewards.IsPlaceholder, "GUILD LADDER: every floor's reward reads 'Reward not decided yet', muted, while GuildDungeonRewards is a placeholder (never a 0 with an icon)");

            // Spec state 2: a guild of 30 mid-cycle, the member at the cycle cap, a guildmate's floor 7 clear.
            List<GuildDungeonAttempt> big = new List<GuildDungeonAttempt>();
            for (int f = 1; f <= 6; f++) big.Add(R28Attempt("u-mate", "Ana", f, true));
            big.Add(R28Attempt("u-mate2", "Bo", 7, true));
            // Fifteen of this member's own, two on each of floors 1 to 7 and one on 8, so no single floor is spent and
            // the cycle cap is what stops every open floor.
            for (int f = 1; f <= 7; f++) for (int k = 0; k < 2; k++) big.Add(R28Attempt(me, "Me", f, f == 3 && k == 1));
            big.Add(R28Attempt(me, "Me", 8, false));
            GuildDungeonStatus s2 = R28Status(10, 30, cap, cap, now);
            p.Loaded(s2, big, "Ashen Oath", 30);
            v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            bool allCycle = R28CycleCapped(v, cap);
            Check(v.Rows.Count == 10 && v.NextCycleLine == "" && v.AttemptsLine == "ATTEMPTS 0 / " + cap + " THIS CYCLE" && allCycle
                  && R28ReasonsVerbatim(d, v, big, me, s2),
                  "GUILD LADDER, A GUILD OF 30 AT THE CAP: 10 floors, no next-cycle line, every open floor's ATTEMPT disabled with 'no attempts left this cycle (" + cap + " of " + cap + ")' and every locked one with its opening sentence (CanAttempt's order)");
            Check(v.Rows[5].DividerBefore && v.Rows[5].DividerCaption == GuildDungeonLadderPresenter.SecondBlockCaption && v.Rows[4].Champion && v.Rows[9].Champion
                  && v.Rows[6].State == GuildFloorState.Cleared && v.Rows[6].FirstClearText == "First cleared by Bo" && v.Rows[7].State == GuildFloorState.Open
                  && v.Rows[8].State == GuildFloorState.Locked && v.Rows[2].ClearedByMe && !v.Rows[6].ClearedByMe && v.PipsCleared == 7 && v.Boards.Enabled,
                  "GUILD LADDER: the SECOND BLOCK divider over floor 6, Champions at 5 and 10, floor 7 'First cleared by Bo', 8 open, 9 locked, 7 pips, BOARDS on");

            // Spec state 3: a guild of 12 whose cycle opened at 9 members.
            GuildDungeonStatus s3 = R28Status(5, 9, cap, 0, now);
            p.Loaded(s3, none, "Ashen Oath", 12);
            v = p.Build(d, me, now);
            Check(v.LadderLine == "5 FLOORS THIS CYCLE - set at 9 members when the cycle opened." && v.NextCycleLine == "Your guild of 12 gets 6 floors from next cycle.",
                  "GUILD LADDER, GREW MID-CYCLE: still 5 floors 'set at 9 members', and 'Your guild of 12 gets 6 floors from next cycle' (never this cycle)");
            p.ServerLadderFor(12, 7);
            Check(p.Build(d, me, now).NextCycleLine == "Your guild of 12 gets 7 floors from next cycle.",
                  "GUILD LADDER: the server's guild_dungeon_floors_for answer wins over the client's prediction");
            p.ServerLadderFor(13, 9);
            Check(p.Build(d, me, now).NextCycleLine == "Your guild of 12 gets 7 floors from next cycle.", "GUILD LADDER: an answer for another member count is ignored");

            // Spec state 4: changed guild after spending the cycle's attempts elsewhere.
            GuildDungeonStatus s4 = R28Status(5, 6, cap, cap, now);
            p = new GuildDungeonLadderPresenter();
            List<GuildDungeonAttempt> newGuild = new List<GuildDungeonAttempt> { R28Attempt("u-mate", "Ana", 1, true) };
            p.Loaded(s4, newGuild, "New Guild", 6);
            v = p.Build(d, me, now);
            allCycle = R28CycleCapped(v, cap);
            for (int i = 0; i < v.Rows.Count; i++) allCycle &= v.Rows[i].AttemptsLeft == 0;
            Check(allCycle && v.AttemptsLine == "ATTEMPTS 0 / " + cap + " THIS CYCLE" && R28ReasonsVerbatim(d, v, newGuild, me, s4),
                  "GUILD LADDER, CHANGED GUILD AFTER " + cap + " ATTEMPTS: the server's count wins over rows this guild cannot show; every ATTEMPT disabled with the cycle sentence");

            // A server before 0016: the status columns are -1.
            GuildDungeonStatus old = R28Status(5, -1, -1, -1, now);
            List<GuildDungeonAttempt> mine = new List<GuildDungeonAttempt> { R28Attempt(me, "Me", 1, false), R28Attempt(me, "Me", 1, true) };
            p.Loaded(old, mine, "Old Server", -1);
            v = p.Build(d, me, now);
            Check(v.LadderLine == "5 FLOORS THIS CYCLE" && v.CycleAttemptCap == cap && v.AttemptsLeftThisCycle == cap - 2 && v.NextCycleLine == ""
                  && v.Rows[0].AttemptsText == "1 of 3 left",
                  "GUILD LADDER, SERVER BEFORE 0016: the floor count alone, the cap from GuildDungeonScale.CycleAttemptCap, the count from this member's rows");

            // Recording and refusal.
            p.Loaded(s1, none, "Ashen Oath", 4);
            p.AttemptStarted(1);
            v = p.Build(d, me, now);
            bool recording = v.Rows[0].Attempt.Label == "RECORDING";
            for (int i = 0; i < v.Rows.Count; i++) recording &= !v.Rows[i].Attempt.Enabled;
            Check(recording, "GUILD LADDER RECORDING: floor 1 reads RECORDING and every ATTEMPT is disabled until the attempt lands");
            p.AttemptRefused("no attempts left on floor 1 this cycle (3 of 3)");
            v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            Check(v.Banner == "Not recorded: no attempts left on floor 1 this cycle (3 of 3)" && v.Rows[0].Attempt.Enabled,
                  "GUILD LADDER REFUSED: the server's sentence as it came, in a banner, and the buttons back");
            p.AttemptStarted(1);
            p.AttemptRecorded();
            Check(p.Build(d, me, now).Banner == "" && p.Build(d, me, now).Rows[0].Attempt.Enabled, "GUILD LADDER RECORDED: no banner, buttons back");

            p.BeginLoad();
            v = p.Build(d, me, now);
            Check(v.Phase == ScreenPhase.Ready && v.ShowSpinner && !v.Refresh.Enabled && v.Rows.Count == 5, "GUILD LADDER REFRESHING: the ladder stays, a spinner, REFRESHING not tappable");
            p.Failed("timed out");
            v = p.Build(d, me, now);
            Check(v.Phase == ScreenPhase.Ready && v.Banner == "Could not refresh: timed out" && v.Refresh.Enabled, "GUILD LADDER, REFRESH FAILED: the ladder stays under a banner");
            p.SetOffline("You are offline, so the guild cannot be reached.", true);
            v = p.Build(d, me, now);
            bool offlineButtons = true;
            for (int i = 0; i < v.Rows.Count; i++) offlineButtons &= !v.Rows[i].Attempt.Enabled;
            Check(v.Phase == ScreenPhase.Ready && v.Banner.StartsWith("Offline.") && offlineButtons, "GUILD LADDER, OFFLINE WITH A LADDER: shown under an offline banner, no ATTEMPT tappable");

            GuildDungeonStatus ended = R28Status(5, 4, cap, 0, now);
            ended.EndsUnix = now - 5;
            p = new GuildDungeonLadderPresenter();
            p.Loaded(ended, none, "Ashen Oath", 4);
            v = p.Build(d, me, now);
            texts.AddRange(R28Texts(v));
            Check(v.CycleLine == "A new cycle is opening" && !v.Rows[0].Attempt.Enabled && v.Banner.StartsWith("This cycle has ended"),
                  "GUILD LADDER, CYCLE OVER: 'A new cycle is opening', no ATTEMPT, a banner asking for a refresh");

            string dash = R28Dashes(texts);
            Check(dash == null, "GUILD LADDER: no em or en dash in any string the screen draws" + (dash != null ? " (" + dash + ")" : ""));
            string sell = R28Selling(texts);
            Check(sell == null, "GUILD LADDER: no label buys, refills or prices an attempt (attempts cannot be bought)" + (sell != null ? " (" + sell + ")" : ""));
            bool noAd = true;
            for (int i = 0; i < texts.Count; i++) noAd &= texts[i] == null || !Regex.IsMatch(texts[i], @"\b(ad|ads|watch)\b", RegexOptions.IgnoreCase);
            Check(noAd, "GUILD LADDER: no ad is offered for an attempt");
        }

        // ------------------------------------------------------------------ notifications

        private static void CheckR28Notifications(ContentDatabase d)
        {
            List<string> texts = new List<string>();
            long now = R28Now;
            DailyLoopSwitch.Enabled = false;
            LiveEventsSwitch.Enabled = false;
            GameSession fresh = R28Cleared(d, null);
            GameSession s = R28Cleared(d, "ch1_6");
            s.Profile.LastIdleClaimUnix = now - 3600L;
            NotificationPrefs on = new NotificationPrefs();

            string before = SaveSerializer.Serialize(s.Profile);
            NotificationSettingsView v = NotificationSettingsPresenter.Build(s, on, NotificationPermission.Unsupported, false, now, 0);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Unavailable && !v.Master.Enabled && !v.IdleChest.Enabled && !v.Orders.Enabled && !v.Events.Enabled
                  && !v.PermissionButton.Visible && v.Upcoming.Count == 0 && v.Status.Length > 0,
                  "NOTIFICATIONS UNSUPPORTED (no package): the screen says so, every switch is disabled, nothing is listed");

            v = NotificationSettingsPresenter.Build(fresh, on, NotificationPermission.NotAsked, false, now, 0);
            texts.AddRange(R28Texts(v));
            Check(v.Phase == ScreenPhase.Ready && v.PermissionButton.Visible && !v.PermissionButton.Enabled && v.PermissionButton.Reason == "Clear your first stage first."
                  && v.Upcoming.Count == 0 && v.UpcomingStatus.Length > 0,
                  "NOTIFICATIONS, BEFORE THE FIRST CLEAR: ALLOW REMINDERS is drawn but waits for the first stage (the permission ask comes after the first session)");

            v = NotificationSettingsPresenter.Build(s, on, NotificationPermission.NotAsked, false, now, 0);
            Check(v.PermissionButton.Enabled && v.PermissionButton.Label == "ALLOW REMINDERS" && v.Master.Enabled && v.Upcoming.Count == 0
                  && v.UpcomingStatus == "Nothing is scheduled until reminders are allowed.",
                  "NOTIFICATIONS, NOT ASKED: ALLOW REMINDERS tappable, the switches can be set ahead, nothing is scheduled yet");

            v = NotificationSettingsPresenter.Build(s, on, NotificationPermission.NotAsked, true, now, 0);
            Check(v.PermissionButton.Visible && !v.PermissionButton.Enabled && v.PermissionButton.Label == "ASKING",
                  "NOTIFICATIONS, ASKING: the button reads ASKING and cannot be tapped twice");

            v = NotificationSettingsPresenter.Build(s, on, NotificationPermission.Denied, false, now, 0);
            texts.AddRange(R28Texts(v));
            Check(v.PermissionButton.Enabled && v.PermissionButton.Label == "OPEN PHONE SETTINGS" && !v.Master.Enabled && !v.IdleChest.Enabled
                  && v.Master.Reason.Length > 0 && v.Upcoming.Count == 0,
                  "NOTIFICATIONS DENIED: OPEN PHONE SETTINGS, every switch disabled with the reason, the game never asks again by itself");

            v = NotificationSettingsPresenter.Build(s, on, NotificationPermission.Granted, false, now, 0);
            texts.AddRange(R28Texts(v));
            List<PlannedNotification> plan = NotificationPlanner.Plan(s, now, 0, on);
            bool same = plan.Count == v.Upcoming.Count && plan.Count > 0;
            for (int i = 0; same && i < plan.Count; i++)
                same = plan[i].Kind == v.Upcoming[i].Kind && plan[i].FireUnix == v.Upcoming[i].FireUnix && plan[i].Title == v.Upcoming[i].Title
                       && v.Upcoming[i].WhenText == ScreenText.LocalDayAndClock(plan[i].FireUnix, now, 0);
            Check(v.Phase == ScreenPhase.Ready && !v.PermissionButton.Visible && v.Master.Enabled && v.IdleChest.Enabled && same && v.UpcomingStatus == "",
                  "NOTIFICATIONS GRANTED: the upcoming list is NotificationPlanner.Plan's own answer, time by time (" + plan.Count + " planned)");
            Check(v.Orders.Note == "Daily orders are not open yet." && v.Events.Note == "No events are running yet." && v.IdleChest.Note == "",
                  "NOTIFICATIONS: a topic whose system is off says so under its switch");
            NotificationRules rules = new NotificationRules();
            Check(v.RuleLines.Count == 3 && v.RuleLines[0].StartsWith("Quiet hours 22:00 to 09:00") && v.RuleLines[1] == "At most " + rules.MaxPerLocalDay + " a day, at least 4 hours apart.",
                  "NOTIFICATIONS: the rule lines are NotificationRules' own numbers (quiet hours 22:00 to 09:00, 2 a day, 4 hours apart)");

            NotificationPrefs off = NotificationSettingsPresenter.Toggle(v, on, null);
            Check(!off.Enabled && on.Enabled, "NOTIFICATIONS TOGGLE: the Reminders switch flips, and the prefs handed in are not changed");
            v = NotificationSettingsPresenter.Build(s, off, NotificationPermission.Granted, false, now, 0);
            Check(!v.Master.On && v.Master.Enabled && !v.IdleChest.Enabled && v.IdleChest.Reason == "Turn on Reminders first." && v.Upcoming.Count == 0 && v.UpcomingStatus == "Reminders are off.",
                  "NOTIFICATIONS, REMINDERS OFF: the topics are disabled under it, nothing is listed");
            NotificationPrefs still = NotificationSettingsPresenter.Toggle(v, off, NotificationKind.IdleChestFull);
            Check(still.IdleChest == off.IdleChest, "NOTIFICATIONS TOGGLE: a disabled topic does not flip");
            v = NotificationSettingsPresenter.Build(s, on, NotificationPermission.Granted, false, now, 0);
            NotificationPrefs noIdle = NotificationSettingsPresenter.Toggle(v, on, NotificationKind.IdleChestFull);
            v = NotificationSettingsPresenter.Build(s, noIdle, NotificationPermission.Granted, false, now, 0);
            bool idleGone = !noIdle.IdleChest && noIdle.Enabled;
            for (int i = 0; i < v.Upcoming.Count; i++) idleGone &= v.Upcoming[i].Kind != NotificationKind.IdleChestFull;
            Check(idleGone && v.UpcomingStatus == "No reminders planned right now.",
                  "NOTIFICATIONS TOGGLE: turning the idle chest off removes it from the list, and an empty list says 'No reminders planned right now.'");
            Check(SaveSerializer.Serialize(s.Profile) == before, "NOTIFICATIONS: building the screen writes nothing to the save");

            string dash = R28Dashes(texts);
            Check(dash == null, "NOTIFICATIONS: no em or en dash in any string the screen draws" + (dash != null ? " (" + dash + ")" : ""));
            string sell = R28Selling(texts);
            Check(sell == null, "NOTIFICATIONS: nothing on the screen or in a listed reminder names anything sold" + (sell != null ? " (" + sell + ")" : ""));
        }

        // ------------------------------------------------------------------ rewarded ads

        private static void CheckR28AdsConfig(ContentDatabase d)
        {
            AdsConfig c = d.Ads;
            AdPlacementDef a1 = c.OfKind(AdPlacementKind.IdleGold), a2 = c.OfKind(AdPlacementKind.Brand), a3 = c.OfKind(AdPlacementKind.RepeatClear);
            Check(c.IsUsable && c.Warnings.Count == 0 && c.Placements.Count == 3 && a1 != null && a2 != null && a3 != null
                  && a1.PerDay == 1 && a2.PerDay == 1 && a3.PerDay == 1 && a1.CapHours == 2.0,
                  "ads.json loads the lean variant: three placements, one a reward day each, the idle ad capped at 2 hours (Docs/DESIGN-REWARDED-ADS.md)");
            AdsConfig bad = AdsConfig.FromJson(("{ 'placements': [ { 'id': 'x', 'kind': 'idleGold', 'perDay': 9, 'capHours': 10 },"
                + " { 'id': 'y', 'kind': 'idleGold' }, { 'id': 'z', 'kind': 'mysteryChest' } ] }").Replace('\'', '"'));
            Check(bad.Placements.Count == 1 && bad.Placements[0].PerDay == AdsConfig.MaxPerDay && bad.Placements[0].CapHours == AdsConfig.MaxCapHours
                  && bad.Warnings.Count == 2,
                  "ads.json: a cap above the ceiling is clamped (" + AdsConfig.MaxPerDay + " a day, " + AdsConfig.MaxCapHours + " hours), a second placement of a kind and an unknown kind are dropped with a warning");
        }

        private static void CheckR28AdsOff(ContentDatabase d)
        {
            AdsSwitch.Enabled = false;
            GameSession s = R28Cleared(d, "ch3_1");
            StageDef stage = d.GetStage("ch1_6");
            s.Profile.StageClearsToday[stage.Id] = s.RepeatFullClearsPerDay;
            s.Profile.DailyResetUnix = s.DayStartUnix(R28Now);
            AdContext ctx = new AdContext();
            ctx.ClaimGold = 5000;
            ctx.Stage = stage;
            string before = SaveSerializer.Serialize(s.Profile);
            bool hidden = true;
            foreach (AdPlacementKind k in Enum.GetValues(typeof(AdPlacementKind)))
            {
                AdOffer o = AdRewards.View(s, k, ctx, R28Now);
                hidden &= !o.Available && o.Refusal == AdRefusal.SwitchOff;
                hidden &= !new RewardedAdPresenter(k).Build(s, ctx, true, R28Now).Visible;
                AdOffer forged = new AdOffer();
                forged.Kind = k; forged.Available = true; forged.PlacementId = "idle_double"; forged.Gold = 5000;
                hidden &= !AdRewards.Grant(s, forged, "imp-off", R28Now).Success;
            }
            Check(hidden, "ADS OFF (the default): no placement offers, no button is drawn even with an ad loaded, and a grant is refused");
            string after = SaveSerializer.Serialize(s.Profile);
            Check(after == before && after.IndexOf("\"ads\"", StringComparison.Ordinal) < 0 && SaveSerializer.VersionOf(s.Profile) < SaveSerializer.CurrentVersion,
                  "ADS OFF: the save is byte for byte what it was, carries no ads key, and is not stamped version " + SaveSerializer.CurrentVersion);
        }

        private static void CheckR28AdRules(ContentDatabase d)
        {
            AdsSwitch.Enabled = true;
            long now = R28Now;

            // A1: the idle claim's Gold again, capped at 2 hours.
            GameSession s = R28Cleared(d, "ch3_1");
            AdContext ctx = new AdContext();
            Check(AdRewards.View(s, AdPlacementKind.IdleGold, ctx, now).Refusal == AdRefusal.NothingToPay, "A1: with no claim there is nothing to double");
            long twoHours = s.IdleYield(2L * 3600L).Gold;
            ctx.ClaimGold = twoHours * 5;
            AdOffer big = AdRewards.View(s, AdPlacementKind.IdleGold, ctx, now);
            Check(big.Available && big.Gold == twoHours && big.Line == "Watch an ad: +2 hours of idle Gold = " + ScreenText.Group(twoHours) + " Gold" && big.LeftToday == 1,
                  "A1: a ten hour claim is doubled only up to 2 hours of the player's own idle Gold, and the line says the exact amount (" + big.Line + ")");
            ctx.ClaimGold = twoHours / 4;
            AdOffer small = AdRewards.View(s, AdPlacementKind.IdleGold, ctx, now);
            Check(small.Available && small.Gold == twoHours / 4 && small.Line == "Watch an ad: your claim again = " + ScreenText.Group(twoHours / 4) + " Gold",
                  "A1: a claim under the cap is paid again in full (" + small.Line + ")");

            string viewed = SaveSerializer.Serialize(s.Profile);
            AdRewards.View(s, AdPlacementKind.IdleGold, ctx, now);
            Check(SaveSerializer.Serialize(s.Profile) == viewed, "ADS: looking at an offer writes nothing to the save");

            long gold0 = s.Profile.Gold, sealed0 = s.Profile.Sealed.Gold, shards0 = s.Profile.Shards;
            AdGrantResult g = AdRewards.Grant(s, big, "imp-1", now + 30);
            Check(g.Success && s.Profile.Gold - gold0 == big.Gold && s.Profile.Sealed.Gold - sealed0 == big.Gold && s.Profile.Shards == shards0 && g.Gold == big.Gold,
                  "A1: the grant is exactly the amount on the button, all of it Sealed Gold, and no Shards move");
            Check(SaveSerializer.VersionOf(s.Profile) == SaveSerializer.CurrentVersion && SaveSerializer.CurrentVersion == 27
                  && SaveSerializer.Serialize(s.Profile).Contains("\"ads\""),
                  "ADS: a profile that was granted an ad is a version 27 document carrying the ads key");
            Check(!AdRewards.Grant(s, big, "imp-2", now + 60).Success && AdRewards.View(s, AdPlacementKind.IdleGold, ctx, now + 60).Refusal == AdRefusal.CappedToday,
                  "A1: once a reward day; a second ad the same day is refused and the offer reads 'used today'");
            long tomorrow = s.DayStartUnix(now) + 86400L + 60L;
            AdOffer next = AdRewards.View(s, AdPlacementKind.IdleGold, ctx, tomorrow);
            Check(next.Available, "A1: the next reward day (04:00 UTC) opens it again");
            Check(AdRewards.Grant(s, next, "imp-1", tomorrow).Refusal == AdRefusal.AlreadyGranted, "ADS: an impression id already paid is never paid again, even on another day");
            Check(AdRewards.Grant(s, next, "imp-3", tomorrow).Success, "A1: a fresh impression on the next day is paid");
            bool shut = true;
            long[] back = { tomorrow - 86400L, tomorrow - 7L * 86400L, tomorrow - 365L * 86400L, 0L };
            for (int i = 0; i < back.Length; i++) shut &= !AdRewards.View(s, AdPlacementKind.IdleGold, ctx, back[i]).Available;
            Check(shut, "ADS, ONE CLOCK FORWARD ONLY: a day, a week, a year and 1970 back reopen nothing");

            AdsState rt = AdsState.Read(JsonValue.Parse(s.Profile.Ads.Write().ToJson(false)));
            Check(rt.ClockDay == s.Profile.Ads.ClockDay && rt.Placements.Count == s.Profile.Ads.Placements.Count && rt.Impressions.Count == 2,
                  "ADS: the ads block round-trips through the save");
            AdsState many = new AdsState();
            for (int i = 0; i < 80; i++) many.Remember("i" + i);
            Check(many.Impressions.Count == AdsState.ImpressionKeep && many.Impressions[0] == "i30" && many.Seen("i79") && !many.Seen("i0"),
                  "ADS: the last " + AdsState.ImpressionKeep + " impression ids are kept, oldest dropped first");

            // A2: one Brand.
            GameSession early = R28Cleared(d, "ch1_3");
            AdOffer locked = AdRewards.View(early, AdPlacementKind.Brand, null, now);
            Check(!locked.Available && (locked.Refusal == AdRefusal.NotOpen || !OnboardingSwitch.Enabled),
                  "A2: before the Proving opens there is no Brand ad (" + locked.Reason + ")");
            GameSession pv = R28Cleared(d, "ch3_1");
            ProvingBrandsStatus full = pv.PeekProvingBrands(now);
            pv.Profile.ProvingBrandsBanked = full.Max;
            pv.Profile.ProvingBrandsClockUnix = now;
            Check(AdRewards.View(pv, AdPlacementKind.Brand, null, now).Refusal == AdRefusal.NothingToPay, "A2: with the Brands full there is nothing to add");
            pv.Profile.ProvingBrandsBanked = full.Max - 2;
            AdOffer brand = AdRewards.View(pv, AdPlacementKind.Brand, null, now);
            Check(brand.Available && brand.Brands == 1 && brand.Line == "Watch an ad: +1 Brand for the Proving", "A2: below the maximum the ad offers exactly one Brand");
            int banked0 = pv.PeekProvingBrands(now).Banked;
            Check(AdRewards.Grant(pv, brand, "b-1", now).Success && pv.PeekProvingBrands(now).Banked == banked0 + 1, "A2: the grant adds one Brand");
            GameSession lapse = R28Cleared(d, "ch3_1");
            lapse.Profile.ProvingBrandsBanked = full.Max - 1;
            lapse.Profile.ProvingBrandsClockUnix = now;
            AdOffer lapsing = AdRewards.View(lapse, AdPlacementKind.Brand, null, now);
            lapse.Profile.ProvingBrandsBanked = full.Max;
            AdGrantResult lapsed = AdRewards.Grant(lapse, lapsing, "b-2", now);
            Check(!lapsed.Success && lapsed.Refusal == AdRefusal.Lapsed && AdRewards.View(lapse, AdPlacementKind.Brand, null, now).Refusal != AdRefusal.CappedToday
                  && lapse.Profile.Ads.IsEmpty,
                  "A2: if the Brands fill while the ad plays, nothing is added, the day's ad is not spent, and nothing is written");

            // A3: one more full-pay repeat clear.
            GameSession rc = R28Cleared(d, "ch3_1");
            StageDef stage = d.GetStage("ch1_6");
            AdContext sc = new AdContext();
            sc.Stage = stage;
            Check(AdRewards.View(rc, AdPlacementKind.RepeatClear, sc, now).Refusal == AdRefusal.NothingToPay, "A3: while the stage still has paid repeat clears there is no ad");
            rc.Profile.StageClearsToday[stage.Id] = rc.RepeatFullClearsPerDay;
            rc.Profile.DailyResetUnix = rc.DayStartUnix(now);
            string pre = SaveSerializer.Serialize(rc.Profile);
            AdOffer clear = AdRewards.View(rc, AdPlacementKind.RepeatClear, sc, now);
            Check(clear.Available && clear.Gold == stage.RewardGold && clear.Dust == rc.RelicDustForRepeatClear(stage) && clear.StageId == stage.Id
                  && clear.Line.StartsWith("Watch an ad: one more full clear of " + stage.DisplayCode + " = ") && SaveSerializer.Serialize(rc.Profile) == pre,
                  "A3: once the paid repeats are used the ad pays what one full repeat clear pays (" + clear.Line + "), read without touching the day's counters");
            Check(AdRewards.View(rc, AdPlacementKind.RepeatClear, sc, rc.DayStartUnix(now) + 86400L + 5L).Refusal == AdRefusal.NothingToPay,
                  "A3: on the next reward day the repeats are back, so the ad is not offered");
            long g0 = rc.Profile.Gold, s0 = rc.Profile.Sealed.Gold, d0 = rc.Profile.RelicDust, sh0 = rc.Profile.Shards;
            Check(AdRewards.Grant(rc, clear, "r-1", now).Success && rc.Profile.Gold - g0 == clear.Gold && rc.Profile.Sealed.Gold - s0 == clear.Gold
                  && rc.Profile.RelicDust - d0 == clear.Dust && rc.Profile.Shards == sh0 && rc.Profile.StageClearsToday[stage.Id] == rc.RepeatFullClearsPerDay,
                  "A3: the grant is exactly the Gold (Sealed) and Relic Dust on the button, no Shards, and the stage's repeat counter is left alone");
        }

        private static void CheckR28AdPresenter(ContentDatabase d)
        {
            AdsSwitch.Enabled = true;
            long now = R28Now;
            List<string> texts = new List<string>();
            GameSession s = R28Cleared(d, "ch3_1");
            AdContext ctx = new AdContext();
            ctx.ClaimGold = s.IdleYield(5L * 3600L).Gold;
            RewardedAdPresenter p = new RewardedAdPresenter(AdPlacementKind.IdleGold);

            RewardedAdView v = p.Build(s, ctx, false, now);
            Check(!v.Visible && v.State == RewardedAdState.Hidden, "AD BUTTON, NO AD LOADED (offline, no fill, no consent): not drawn, nothing waits on an ad");
            v = p.Build(s, new AdContext(), true, now);
            Check(!v.Visible, "AD BUTTON, NOTHING TO PAY: not drawn");
            v = p.Build(s, ctx, true, now);
            texts.AddRange(R28Texts(v));
            AdOffer offer = AdRewards.View(s, AdPlacementKind.IdleGold, ctx, now);
            Check(v.Visible && v.State == RewardedAdState.Ready && v.Watch.Enabled && v.Watch.Label == "WATCH AN AD" && v.RewardLine == offer.Line
                  && v.LeftTodayText == "1 left today",
                  "AD BUTTON READY: WATCH AN AD with the exact fixed line beside it before the tap, and '1 left today'");

            Check(p.Begin(offer) && p.Shown == offer, "AD BUTTON: the tap keeps the offer that was on the button");
            AdContext richer = new AdContext();
            richer.ClaimGold = ctx.ClaimGold * 10;
            v = p.Build(s, richer, true, now);
            texts.AddRange(R28Texts(v));
            Check(v.State == RewardedAdState.Playing && !v.Watch.Enabled && v.Watch.Label == "PLAYING" && v.RewardLine == offer.Line && !p.Begin(offer),
                  "AD BUTTON PLAYING: disabled, still showing the line that was tapped whatever changes meanwhile, and a second tap starts nothing");
            AdGrantResult g = AdRewards.Grant(s, p.Shown, "p-1", now + 20);
            p.Rewarded(g);
            v = p.Build(s, ctx, true, now + 20);
            texts.AddRange(R28Texts(v));
            long toReset = offer.NextDayUnix - (now + 20);
            Check(v.State == RewardedAdState.Granted && v.ResultIsReward && v.ResultLine == "+" + ScreenText.Group(offer.Gold) + " Gold (Sealed)"
                  && v.NextResetText == "Next one in " + ScreenText.Countdown(toReset) && !v.Watch.Visible && v.Dismiss.Enabled,
                  "AD BUTTON GRANTED: '" + v.ResultLine + "', 'Next one in " + ScreenText.Countdown(toReset) + "', no second WATCH, OK to close");
            p.Dismiss();
            Check(!p.Build(s, ctx, true, now + 30).Visible, "AD BUTTON, USED TODAY: after OK the button is gone until the reset");

            GameSession s2 = R28Cleared(d, "ch3_1");
            RewardedAdPresenter q = new RewardedAdPresenter(AdPlacementKind.IdleGold);
            q.Begin(AdRewards.View(s2, AdPlacementKind.IdleGold, ctx, now));
            q.NotFinished();
            v = q.Build(s2, ctx, true, now);
            texts.AddRange(R28Texts(v));
            Check(v.State == RewardedAdState.NotFinished && !v.ResultIsReward && v.ResultLine == "No reward: the ad was not finished." && v.Watch.Enabled && s2.Profile.Ads.IsEmpty,
                  "AD BUTTON NOT FINISHED: 'No reward: the ad was not finished.', nothing spent, WATCH still offered");
            q.Begin(AdRewards.View(s2, AdPlacementKind.IdleGold, ctx, now));
            q.ShowFailed();
            v = q.Build(s2, ctx, true, now);
            texts.AddRange(R28Texts(v));
            Check(v.State == RewardedAdState.Failed && v.ResultLine == "The ad could not be shown. Nothing was spent." && s2.Profile.Ads.IsEmpty,
                  "AD BUTTON FAILED TO SHOW: says nothing was spent");
            AdGrantResult refused = new AdGrantResult();
            refused.Reason = "That ad has already been paid.";
            q.Begin(AdRewards.View(s2, AdPlacementKind.IdleGold, ctx, now));
            q.Rewarded(refused);
            v = q.Build(s2, ctx, true, now);
            Check(v.State == RewardedAdState.Refused && v.ResultLine == "That ad has already been paid." && !v.ResultIsReward, "AD BUTTON REFUSED: the refusal's sentence, muted");
            q.Dismiss();
            Check(q.Build(s2, ctx, true, now).State == RewardedAdState.Ready, "AD BUTTON: after OK on a no-reward line the button is back");

            string dash = R28Dashes(texts);
            Check(dash == null, "AD BUTTON: no em or en dash in any string it draws" + (dash != null ? " (" + dash + ")" : ""));
        }

        /// <summary>Words that mean a reward is left to luck. Whole words only, so "rollback" or "drawn" pass.</summary>
        private static readonly Regex R28LuckWords = new Regex(@"\b(random\w*|rng|roll|rolls|rolled|reroll\w*|summon\w*|pity|chance\w*|gacha|mystery|wheel|lottery|shuffle\w*|odds|lootbox\w*)\b",
                                                               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static void CheckR28AdsNothingLeftToLuck(string root)
        {
            string[] files =
            {
                "Assets/Scripts/Core/Progression/AdRewards.cs", "Assets/Scripts/Core/Data/AdsConfig.cs", "Assets/Scripts/Core/Model/AdsState.cs",
                "Assets/Scripts/Core/Screens/RewardedAdPresenter.cs", "Assets/Scripts/Game/Ads/AdService.cs", "Assets/Resources/Content/ads.json"
            };
            List<string> hits = new List<string>();
            int read = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string text = R28Read(root, files[i]);
                if (text == null) continue;
                read++;
                Match m = R28LuckWords.Match(text);
                if (m.Success) hits.Add(Path.GetFileName(files[i]) + ": " + m.Value);
            }
            Check(read == files.Length && hits.Count == 0,
                  "ADS, THE BELGIAN RULE: no ad file names a random, roll, summon, pity, chance or mystery mechanic (" + read + " files read"
                  + (hits.Count > 0 ? "; " + string.Join(", ", hits.ToArray()) : "") + ")");

            string ui = Path.Combine(Path.Combine(root, "Assets"), Path.Combine("Scripts", "UI"));
            List<string> onSales = new List<string>();
            if (Directory.Exists(ui))
            {
                foreach (string f in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(f);
                    if (!Regex.IsMatch(name, "Store|Event|Pass|Wayfarer|Offer|Shop", RegexOptions.IgnoreCase)) continue;
                    string text = File.ReadAllText(f);
                    if (text.Contains("RewardedAd") || text.Contains("AdRewards")) onSales.Add(name);
                }
            }
            Check(Directory.Exists(ui) && onSales.Count == 0,
                  "ADS, NEVER IN PLACE OF A PURCHASE: no Store, Events, pass, offer or shop screen names an ad" + (onSales.Count > 0 ? " (" + string.Join(", ", onSales.ToArray()) + ")" : ""));
        }
    }
}
