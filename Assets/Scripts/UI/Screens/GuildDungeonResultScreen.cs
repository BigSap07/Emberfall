using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// What an attempt came to: the turns, the heroes lost, EVERY board with where the attempt put
    /// you on it or what kept it off, the attempts left, and - after a handover - the fight you
    /// watched set beside your own.
    ///
    /// EVERY BOARD IS LISTED, NOT ONLY THE ONES ENTERED. A missed board says what kept it out
    /// ("fielded Kael, a 5-star", "2 heroes fell") because that sentence is the next attempt's plan.
    ///
    /// POST TO GUILD CHAT is the gold button, win or lose. A loss is the most useful thing a
    /// guild can be shown: here is what beat me, here is the line, watch it and do better.
    /// </summary>
    public sealed class GuildDungeonResultScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildDungeonResult; } }

        private const float Top = UITheme.TopBarHeight + 118f;

        private Text _headerSubtitle;
        private Text _outcome;
        private Text _facts;
        private RectTransform _lineHost;
        private Text _compare;
        private RectTransform _boardRows;
        private UIButton _share;
        private UIButton _watch;
        private UIButton _boards;
        private UIButton _back;
        private bool _shared;
        private bool _sharing;
        private bool _loading;

        private GuildDungeonState State { get { return Router.GuildDungeon.State; } }

        public bool Shared { get { return _shared; } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "RESULT", Router, out _headerSubtitle);

            RectTransform left = UIBuilder.Node("Summary", Root);
            UIBuilder.Box(left, -460f, -Top, 820f, 640f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(left, true);

            _outcome = EmberforgeUI.Title("Outcome", left, "", UITheme.FontTitle, Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(_outcome.rectTransform, 0f, -30f, 760f, 60f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_outcome, Emberforge.Outline, Emberforge.StrokeThick);

            _facts = EmberforgeUI.Body("Facts", left, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(_facts.rectTransform, 0f, -96f, 760f, 70f, new Vector2(0.5f, 1f));

            _lineHost = UIBuilder.Node("Line", left);
            UIBuilder.Box(_lineHost, 0f, -176f, 5 * 96f + 4 * 10f, 96f, new Vector2(0.5f, 1f));

            _compare = EmberforgeUI.Body("Compare", left, "", UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(_compare.rectTransform, 0f, -290f, 740f, 90f, new Vector2(0.5f, 1f));

            _share = EmberforgeUI.Button("Share", left, "POST TO GUILD CHAT", UITheme.FontBody, ForgeButtonKind.Primary);
            UIBuilder.Box(_share.Root, 0f, 150f, 560f, 90f, new Vector2(0.5f, 0f));
            _share.OnClick(OnShare);

            _watch = EmberforgeUI.Button("WatchAgain", left, "WATCH IT AGAIN", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_watch.Root, -140f, 50f, 270f, 76f, new Vector2(0.5f, 0f));
            _watch.OnClick(OnWatch);

            _back = EmberforgeUI.Button("BackToFloor", left, "BACK TO THE FLOOR", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_back.Root, 140f, 50f, 270f, 76f, new Vector2(0.5f, 0f));
            _back.OnClick(OnBack);

            RectTransform right = UIBuilder.Node("Boards", Root);
            UIBuilder.Box(right, 460f, -Top, 820f, 640f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(right, true);
            EmberforgeUI.Heading("Heading", right, "THE BOARDS", 36f, -20f);

            _boards = EmberforgeUI.Button("OpenBoards", right, "SEE THE BOARDS", UITheme.FontTiny, ForgeButtonKind.Secondary);
            UIBuilder.Box(_boards.Root, -Emberforge.OrnamentClearance, -20f, 230f, 54f, new Vector2(1f, 1f));
            _boards.OnClick(OnBoards);

            _boardRows = UIBuilder.Node("Rows", right);
            UIBuilder.FillParent(_boardRows, 24f, 78f, 24f, 24f);
        }

        public override void OnShow()
        {
            App.RefreshHud();
            _shared = false;
            _sharing = false;
            Render();
            // The boards move under this screen: others are attempting too. One re-read on arrival
            // puts THIS attempt against the guild as it stands now rather than as it stood when
            // the floor view last loaded.
            if (!_loading) { _loading = true; StartCoroutine(RefreshRoutine()); }
        }

        public override void OnHide()
        {
            _loading = false;
        }

        private IEnumerator RefreshRoutine()
        {
            GuildDungeonAttempt mine = State.LastAttempt;
            yield return State.Load(GuildDungeonBackends.For(App));
            _loading = false;
            // The reload replaces the list; point LastAttempt at the stored row so ranks line up.
            if (mine != null)
                for (int i = 0; i < State.Attempts.Count; i++)
                    if (State.Attempts[i].Id == mine.Id && mine.Id != 0) State.LastAttempt = State.Attempts[i];
            Render();
        }

        private void Render()
        {
            GuildDungeonAttempt a = State.LastAttempt;
            for (int i = _boardRows.childCount - 1; i >= 0; i--) Destroy(_boardRows.GetChild(i).gameObject);
            for (int i = _lineHost.childCount - 1; i >= 0; i--) Destroy(_lineHost.GetChild(i).gameObject);
            if (a == null)
            {
                _headerSubtitle.text = "NO ATTEMPT YET";
                _outcome.text = "";
                _facts.text = "";
                _compare.text = "";
                _share.Interactable = false;
                _watch.Interactable = false;
                return;
            }

            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            string self = backend != null ? backend.SelfUserId : a.UserId;
            int left = State.AttemptsLeft(self, a.Floor);

            _headerSubtitle.text = "FLOOR " + a.Floor + "   -   ATTEMPT " + a.Attempt + " OF " + State.Cap;
            _outcome.text = a.Victory ? "CLEARED IN " + a.Turns + " TURNS" : "FELL AFTER " + a.Turns + " TURNS";
            _outcome.color = a.Victory ? Emberforge.TextGold : UITheme.Danger;
            _facts.text = Mathf.Max(0, a.Fallen) + " of " + a.Team.Units.Count + " heroes lost.   "
                        + left + " of " + State.Cap + " attempts left on this floor.";
            GuildDungeonFlow.DrawLine(_lineHost, App.Content, a.Team, 0f, 0f, 96f, 10f);

            GuildDungeonAttempt watched = State.Watched;
            if (watched != null && watched.Floor == a.Floor && watched.Cycle == a.Cycle)
            {
                _compare.text = "YOU WATCHED " + GuildDungeonFlow.NameOf(watched).ToUpperInvariant() + ": "
                              + GuildDungeonFlow.OutcomeLine(watched) + " with " + watched.Team.Units.Count + ".\n"
                              + "YOU: " + GuildDungeonFlow.OutcomeLine(a) + " with " + a.Team.Units.Count + "."
                              + (a.Victory && (!watched.Victory || a.Turns < watched.Turns) ? "   You did better." : "");
            }
            else
            {
                _compare.text = a.Victory
                    ? "A clear enters every board its line satisfies. Nobody declares anything."
                    : "A loss enters no board, but it is still worth sharing: it shows the guild what this floor does.";
            }

            _share.SetText(_shared ? "POSTED TO GUILD CHAT" : _sharing ? "POSTING" : "POST TO GUILD CHAT");
            _share.Interactable = !_shared && !_sharing && a.Id != 0;
            _watch.Interactable = true;
            _back.SetText(left > 0 ? "BACK TO THE FLOOR" : "BACK TO THE DUNGEON");

            List<GuildDungeonStanding> standings = GuildDungeon.StandingsOf(App.Content, State.Attempts, a);
            for (int b = 0; b < GuildDungeon.Boards.Length; b++)
            {
                GuildDungeonBoard board = GuildDungeon.Boards[b];
                GuildDungeonStanding s = standings.Find(x => x.Board == board);
                AddBoardRow(b, board, s, GuildDungeon.Unmet(App.Content, a, board));
            }
        }

        private void AddBoardRow(int index, GuildDungeonBoard board, GuildDungeonStanding standing, string unmet)
        {
            bool met = standing != null;
            RectTransform row = UIBuilder.Node("Board" + index, _boardRows);
            UIBuilder.Box(row, 0f, -index * 88f, 772f, 80f, new Vector2(0.5f, 1f));
            if (met) EmberforgeUI.PlainPanelOn(row); else EmberforgeUI.SunkenOn(row);

            Text title = EmberforgeUI.Label("Title", row, GuildDungeon.BoardTitle(board), UITheme.FontSmall,
                                            met ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 22f, -10f, 420f, 30f, new Vector2(0f, 1f));

            Text rule = EmberforgeUI.Label("Rule", row, met ? GuildDungeon.BoardRule(board) : "Not entered: " + unmet + ".",
                                           UITheme.FontTiny - 4, met ? Emberforge.TextBody : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(rule.rectTransform, 22f, -44f, 500f, 24f, new Vector2(0f, 1f));

            if (!met) return;
            Text rank = EmberforgeUI.Title("Rank", row, GuildDungeonFlow.Ordinal(standing.Rank) + " OF " + standing.Of,
                                           UITheme.FontHeading, Emberforge.TextBody, TextAnchor.MiddleRight);
            UIBuilder.Box(rank.rectTransform, -22f, 6f, 230f, 44f, new Vector2(1f, 0.5f));
            UIBuilder.Outlined(rank, Emberforge.Outline, Emberforge.StrokeThin);
            if (!standing.ThisAttempt)
            {
                Text held = EmberforgeUI.Label("Held", row, "held by an earlier attempt of yours", UITheme.FontTiny - 6,
                                               Emberforge.TextQuiet, TextAnchor.LowerRight);
                UIBuilder.Box(held.rectTransform, -22f, 6f, 300f, 20f, new Vector2(1f, 0f));
            }
        }

        // ================================================================== actions

        private void OnShare()
        {
            if (_shared || _sharing || State.LastAttempt == null) return;
            App.Audio.Tap();
            StartCoroutine(ShareRoutine(State.LastAttempt));
        }

        private IEnumerator ShareRoutine(GuildDungeonAttempt attempt)
        {
            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            if (backend == null || !backend.Ready) { App.Notify(backend == null ? "No guild." : backend.NotReadyReason); yield break; }
            _sharing = true;
            Render();
            bool ok = false;
            string error = "";
            yield return backend.Share(attempt, delegate (bool success, string message) { ok = success; error = message; });
            _sharing = false;
            _shared = ok;
            App.Notify(ok ? "Posted to guild chat. Anyone in the guild can watch it and run the floor themselves."
                          : (string.IsNullOrEmpty(error) ? "That did not post." : error));
            Render();
        }

        private void OnWatch()
        {
            App.Audio.Tap();
            GuildDungeonFlow.Watch(App, Router, State.LastAttempt, ScreenId.GuildDungeonResult, "BACK TO THE RESULT");
        }

        private void OnBoards()
        {
            App.Audio.Tap();
            if (State.LastAttempt != null) Router.GuildDungeonBoards.SetFloor(State.LastAttempt.Floor);
            Router.Show(ScreenId.GuildDungeonBoards);
        }

        private void OnBack()
        {
            App.Audio.Tap();
            GuildDungeonAttempt a = State.LastAttempt;
            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            bool attemptsLeft = a != null && backend != null && State.AttemptsLeft(backend.SelfUserId, a.Floor) > 0;
            Router.Show(attemptsLeft ? ScreenId.GuildFloor : ScreenId.GuildDungeon, false);
        }
    }
}
