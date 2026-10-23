using System;
using System.Collections.Generic;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Screens
{
    public enum GuildFloorState
    {
        /// <summary>Nobody in the guild has cleared the floor before it this cycle.</summary>
        Locked,
        /// <summary>Open, and nobody in the guild has cleared it yet.</summary>
        Open,
        /// <summary>Somebody in the guild has cleared it this cycle. Still attemptable while attempts remain.</summary>
        Cleared
    }

    /// <summary>One floor row of the ladder.</summary>
    public sealed class GuildFloorRowView
    {
        public int Floor;
        public string Title = "";
        /// <summary>The last floor of a block (5 and 10): drawn with the Champion treatment.</summary>
        public bool Champion;
        /// <summary>True on the first floor of the second block (6): a thin divider is drawn above the row.</summary>
        public bool DividerBefore;
        public string DividerCaption = "";
        public GuildFloorState State;
        /// <summary>"LOCKED", "OPEN", "CLEARED".</summary>
        public string StateText = "";
        public int AttemptsLeft;
        public int AttemptsPerFloor;
        /// <summary>"2 of 3 left". GuildDungeonScale.AttemptsLeft, which already takes the lesser of the floor's and the cycle's.</summary>
        public string AttemptsText = "";
        /// <summary>True when this member has a victory on the floor this cycle.</summary>
        public bool ClearedByMe;
        /// <summary>"First cleared by Ana". Empty when nobody has.</summary>
        public string FirstClearText = "";
        /// <summary>ATTEMPT. A disabled one carries GuildDungeonScale.CanAttempt's sentence verbatim as Reason.</summary>
        public ButtonModel Attempt = ButtonModel.Hidden();
        /// <summary>What the first clear pays. "Reward not decided yet" while GuildDungeonRewards is a placeholder.</summary>
        public string RewardText = "";
        /// <summary>Draw RewardText in the muted text colour (the placeholder), never a 0 with a currency icon.</summary>
        public bool RewardMuted;
    }

    /// <summary>Everything the guild dungeon's ladder screen draws.</summary>
    public sealed class GuildDungeonLadderView
    {
        public ScreenPhase Phase;
        public string Title = "GUILD DUNGEON";
        public string GuildName = "";
        public int FloorCount;
        /// <summary>"10 FLOORS THIS CYCLE - set at 31 members when the cycle opened." The member half is left out
        /// when the server predates 0016 (LadderMembers -1).</summary>
        public string LadderLine = "";
        /// <summary>"A guild of 15 gets 7 floors from next cycle." Empty at the longest ladder or with no live member count.</summary>
        public string NextCycleLine = "";
        public int AttemptsLeftThisCycle;
        public int CycleAttemptCap;
        /// <summary>"ATTEMPTS 9 / 15 THIS CYCLE": attempts left of the cycle cap.</summary>
        public string AttemptsLine = "";
        public long CycleEndsInSeconds;
        /// <summary>"Cycle ends in 2d 4h", or "A new cycle is opening" once it has ended.</summary>
        public string CycleLine = "";
        public int PipCount;
        public int PipsCleared;
        public readonly List<GuildFloorRowView> Rows = new List<GuildFloorRowView>();
        /// <summary>The whole-screen sentence for Offline, Error and Unavailable.</summary>
        public string Status = "";
        /// <summary>A one-line notice over a ladder that is still shown: a refused attempt, a failed refresh.</summary>
        public string Banner = "";
        public bool ShowSpinner;
        public ButtonModel Boards = ButtonModel.Hidden();
        public ButtonModel Refresh = ButtonModel.Hidden();
        public ButtonModel Retry = ButtonModel.Hidden();
        /// <summary>Drawn only in the no-guild state: opens the guild finder.</summary>
        public ButtonModel FindGuild = ButtonModel.Hidden();
    }

    /// <summary>
    /// THE GUILD DUNGEON LADDER'S STATE, with the size-scaled ladder and the cycle attempt cap
    /// (Docs/ui-specs/GUILD-DUNGEON-SIZE.md, Docs/ui-specs/R28-SCREENS.md).
    ///
    /// Every number is read, never recomputed: the ladder and the attempts from GuildDungeonStatus (the server's
    /// answer), the fallbacks and every refusal sentence from GuildDungeonScale, the reward from
    /// GuildDungeonRewards. Engine-free and stateful like LeaderboardsPresenter; GuildDungeonLadderController
    /// in Game makes the calls.
    ///
    /// NOTHING IS SOLD HERE. No button buys an attempt, refills one or plays an ad for one: attempts cannot be
    /// bought, and the R28 checks fail the build if a label on this screen says otherwise.
    /// </summary>
    public sealed class GuildDungeonLadderPresenter
    {
        public const string SecondBlockCaption = "SECOND BLOCK - the climb starts again";
        public const string PlaceholderRewardText = "Reward not decided yet";

        private bool _offline;
        private string _offlineReason = "";
        private bool _offlineCanRetry;
        private bool _noGuild;
        private bool _loading;
        private string _error = "";
        private string _banner = "";
        private GuildDungeonStatus _status;
        private List<GuildDungeonAttempt> _attempts = new List<GuildDungeonAttempt>();
        private string _guildName = "";
        private int _liveMembers = -1;
        private int _serverFloorsForLive = -1;
        private int _recordingFloor;

        public GuildDungeonStatus Status { get { return _status; } }
        public bool Loading { get { return _loading; } }

        // ------------------------------------------------------------------ what the controller reports

        public void SetOffline(string reason, bool canRetry)
        {
            _offline = true;
            _offlineReason = reason ?? "";
            _offlineCanRetry = canRetry;
            _loading = false;
            _recordingFloor = 0;
        }

        public void SetNoGuild()
        {
            _noGuild = true;
            _offline = false;
            _loading = false;
            _status = null;
            _attempts = new List<GuildDungeonAttempt>();
        }

        public void BeginLoad()
        {
            _offline = false;
            _noGuild = false;
            _loading = true;
            _error = "";
        }

        /// <summary>The status and this cycle's attempts arrived. liveMembers is GuildService's current count, -1 when unknown.</summary>
        public void Loaded(GuildDungeonStatus status, List<GuildDungeonAttempt> attempts, string guildName, int liveMembers)
        {
            _status = status;
            _attempts = attempts ?? new List<GuildDungeonAttempt>();
            _guildName = guildName ?? "";
            if (liveMembers != _liveMembers) _serverFloorsForLive = -1;
            _liveMembers = liveMembers;
            _loading = false;
            _error = "";
        }

        /// <summary>guild_dungeon_floors_for answered for the live member count. The server's copy wins over the client's prediction.</summary>
        public void ServerLadderFor(int members, int floors)
        {
            if (members == _liveMembers) _serverFloorsForLive = floors;
        }

        public void Failed(string sentence)
        {
            _loading = false;
            _error = string.IsNullOrEmpty(sentence) ? "The guild dungeon could not be reached." : sentence;
        }

        /// <summary>ATTEMPT was tapped and the fight is being recorded: every ATTEMPT is disabled until it lands.</summary>
        public void AttemptStarted(int floor)
        {
            _recordingFloor = floor;
            _banner = "";
        }

        /// <summary>The attempt was stored. The controller reloads the attempts after this.</summary>
        public void AttemptRecorded()
        {
            _recordingFloor = 0;
        }

        /// <summary>The server refused the attempt. Its sentence is shown as it came (0016's raise messages match
        /// GuildDungeonScale.CanAttempt's wording), and the controller reloads, since the refusal means this copy was stale.</summary>
        public void AttemptRefused(string serverSentence)
        {
            _recordingFloor = 0;
            _banner = string.IsNullOrEmpty(serverSentence) ? "The attempt was not recorded." : "Not recorded: " + serverSentence;
        }

        public void ClearBanner()
        {
            _banner = "";
        }

        // ------------------------------------------------------------------ what the screen draws

        public GuildDungeonLadderView Build(ContentDatabase content, string selfUserId, long nowUnix)
        {
            GuildDungeonLadderView v = new GuildDungeonLadderView();
            v.GuildName = _guildName;

            if (_noGuild)
            {
                v.Phase = ScreenPhase.Unavailable;
                v.Status = "You are not in a guild. Join one to climb its dungeon together.";
                v.FindGuild = ButtonModel.On("FIND A GUILD");
                return v;
            }
            if (_status == null)
            {
                if (_offline)
                {
                    v.Phase = ScreenPhase.Offline;
                    v.Status = _offlineReason.Length > 0 ? _offlineReason : "You are offline, so the guild cannot be reached.";
                    v.Retry = _offlineCanRetry ? ButtonModel.On("TRY AGAIN") : ButtonModel.Hidden();
                    return v;
                }
                if (_error.Length > 0)
                {
                    v.Phase = ScreenPhase.Error;
                    v.Status = _error;
                    v.Retry = ButtonModel.On("TRY AGAIN");
                    return v;
                }
                v.Phase = ScreenPhase.Loading;
                v.ShowSpinner = true;
                return v;
            }

            BalanceConfig cfg = content.Balance;
            GuildDungeonStatus s = _status;
            int ladder = Math.Max(1, s.FloorCount);
            int cap = s.CycleAttemptCap > 0 ? s.CycleAttemptCap : GuildDungeonScale.CycleAttemptCap(cfg);
            int used = Math.Max(s.AttemptsUsed, GuildDungeonScale.CycleAttemptsUsed(_attempts, selfUserId, s.Cycle));
            int serverUsed = s.AttemptsUsed;
            bool ended = s.EndsUnix > 0 && s.EndsUnix <= nowUnix;

            v.Phase = ScreenPhase.Ready;
            v.FloorCount = ladder;
            v.LadderLine = ladder + " FLOORS THIS CYCLE"
                         + (s.LadderMembers > 0 ? " - set at " + s.LadderMembers + " members when the cycle opened." : "");
            v.NextCycleLine = NextCycleLine(content, ladder);
            v.CycleAttemptCap = cap;
            v.AttemptsLeftThisCycle = Math.Max(0, cap - used);
            v.AttemptsLine = "ATTEMPTS " + v.AttemptsLeftThisCycle + " / " + cap + " THIS CYCLE";
            v.CycleEndsInSeconds = s.EndsUnix > 0 ? Math.Max(0, s.EndsUnix - nowUnix) : 0;
            v.CycleLine = ended ? "A new cycle is opening" : (s.EndsUnix > 0 ? "Cycle ends in " + ScreenText.Countdown(v.CycleEndsInSeconds) : "");
            v.PipCount = ladder;
            v.PipsCleared = Math.Min(ladder, GuildDungeon.HighestCleared(_attempts, null, s.Cycle));

            int perFloor = Math.Max(1, cfg.GuildDungeonAttemptsPerFloor);
            for (int floor = 1; floor <= ladder; floor++)
            {
                GuildFloorRowView r = new GuildFloorRowView();
                r.Floor = floor;
                r.Title = "FLOOR " + floor;
                r.Champion = GuildDungeonGenerator.PositionInBlock(floor) == GuildDungeonGenerator.BlockFloors;
                r.DividerBefore = floor > 1 && GuildDungeonGenerator.PositionInBlock(floor) == 1;
                r.DividerCaption = r.DividerBefore ? (GuildDungeonGenerator.BlockOf(floor) == 1 ? SecondBlockCaption : "BLOCK " + (GuildDungeonGenerator.BlockOf(floor) + 1) + " - the climb starts again") : "";

                GuildDungeonAttempt first = GuildDungeon.FirstGuildClear(_attempts, s.Cycle, floor);
                bool open = GuildDungeon.FloorOpen(_attempts, s.Cycle, floor);
                r.State = first != null ? GuildFloorState.Cleared : open ? GuildFloorState.Open : GuildFloorState.Locked;
                r.StateText = r.State == GuildFloorState.Cleared ? "CLEARED" : r.State == GuildFloorState.Open ? "OPEN" : "LOCKED";
                r.FirstClearText = first != null ? "First cleared by " + (first.DisplayName.Length > 0 ? first.DisplayName : "a guildmate") : "";
                r.ClearedByMe = ClearedBy(selfUserId, s.Cycle, floor);
                r.AttemptsPerFloor = perFloor;
                r.AttemptsLeft = GuildDungeonScale.AttemptsLeft(cfg, _attempts, selfUserId, s.Cycle, floor, serverUsed);
                r.AttemptsText = r.AttemptsLeft + " of " + perFloor + " left";

                string reason;
                bool can = GuildDungeonScale.CanAttempt(content, _attempts, selfUserId, s.Cycle, floor, ladder, serverUsed, out reason);
                if (ended) r.Attempt = ButtonModel.Off("ATTEMPT", "this cycle has ended; refresh for the new one");
                else if (_recordingFloor > 0) r.Attempt = ButtonModel.Off(_recordingFloor == floor ? "RECORDING" : "ATTEMPT", "an attempt is being recorded");
                else if (_offline) r.Attempt = ButtonModel.Off("ATTEMPT", _offlineReason.Length > 0 ? _offlineReason : "you are offline");
                else r.Attempt = can ? ButtonModel.On("ATTEMPT") : ButtonModel.Off("ATTEMPT", reason);

                GuildDungeonFloorReward reward = GuildDungeonRewards.ForFirstClear(cfg, floor, ladder);
                if (reward.Placeholder || reward.Amount <= 0 || string.IsNullOrEmpty(reward.Currency))
                {
                    r.RewardText = PlaceholderRewardText;
                    r.RewardMuted = true;
                }
                else
                {
                    r.RewardText = "First clear: " + ScreenText.Group(reward.Amount) + " " + reward.Currency;
                }
                v.Rows.Add(r);
            }

            bool anyAttempt = false;
            for (int i = 0; i < _attempts.Count && !anyAttempt; i++) anyAttempt = _attempts[i] != null && _attempts[i].Cycle == s.Cycle;
            v.Boards = anyAttempt ? ButtonModel.On("BOARDS") : ButtonModel.Off("BOARDS", "Nobody in your guild has made an attempt this cycle yet.");

            if (_loading)
            {
                v.ShowSpinner = true;
                v.Refresh = ButtonModel.Off("REFRESHING", "Loading the dungeon.");
            }
            else if (_offline && !_offlineCanRetry)
            {
                v.Refresh = ButtonModel.Off("REFRESH", _offlineReason);
            }
            else
            {
                v.Refresh = ButtonModel.On("REFRESH");
            }

            if (_banner.Length > 0) v.Banner = _banner;
            else if (_offline) v.Banner = "Offline. Showing the dungeon as it was when it last loaded.";
            else if (_error.Length > 0) v.Banner = "Could not refresh: " + _error;
            else if (ended) v.Banner = "This cycle has ended. Refresh to open the new one.";
            return v;
        }

        /// <summary>
        /// The quiet line under the ladder: what the guild's ladder will be from next cycle. Never a promise about
        /// this cycle, which is fixed until it rolls.
        /// </summary>
        private string NextCycleLine(ContentDatabase content, int ladderNow)
        {
            if (_liveMembers <= 0) return "";
            int live = Math.Min(_liveMembers, GuildRules.MemberCap);
            int liveLadder = _serverFloorsForLive > 0 ? _serverFloorsForLive : GuildDungeonScale.LadderFor(content, live);
            if (liveLadder != ladderNow)
                return "Your guild of " + live + " gets " + liveLadder + " floors from next cycle.";
            int max = GuildDungeonScale.MaxLadder(content);
            if (liveLadder >= max) return "";
            for (int m = live + 1; m <= GuildRules.MemberCap; m++)
            {
                int floors = GuildDungeonScale.LadderFor(content, m);
                if (floors > liveLadder) return "A guild of " + m + " gets " + floors + " floors from next cycle.";
            }
            return "";
        }

        private bool ClearedBy(string userId, int cycle, int floor)
        {
            for (int i = 0; i < _attempts.Count; i++)
            {
                GuildDungeonAttempt a = _attempts[i];
                if (a != null && a.Victory && a.UserId == userId && a.Cycle == cycle && a.Floor == floor) return true;
            }
            return false;
        }
    }
}
