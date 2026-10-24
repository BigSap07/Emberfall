using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The boards for one floor: six of them, one per constraint, this guild only.
    ///
    /// SIX SMALL BOARDS, NOT ONE BIG ONE, because each is a different question and a player
    /// with twelve heroes can lead NO FIVE STARS while a player with ninety leads nothing. Every
    /// board ranks fewest turns; a member appears once per board, with their best attempt that
    /// qualifies.
    ///
    /// YOUR ROW STANDS PROUD and everyone else's is set in - the guild roster's convention, which
    /// is depth carrying authorship. EVERY ROW IS PLAYABLE: tap it and watch that attempt, the
    /// same replay a shared card opens.
    /// </summary>
    public sealed class GuildDungeonBoardsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildDungeonBoards; } }

        private const float Top = UITheme.TopBarHeight + 118f;
        private const float CardWidth = 580f;
        private const float CardHeight = 300f;

        private Text _headerSubtitle;
        private RectTransform _tabs;
        private ForgeTab[] _tabButtons = new ForgeTab[0];
        private RectTransform _cards;
        private int _floor = 1;
        private bool _loading;

        private GuildDungeonState State { get { return Router.GuildDungeon.State; } }

        public void SetFloor(int floor) { _floor = Mathf.Max(1, floor); }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "BOARDS", Router, out _headerSubtitle);

            _tabs = UIBuilder.Node("Floors", Root);
            UIBuilder.Box(_tabs, 0f, -Top, 5 * 200f + 4 * 8f, 52f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_tabs, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            _cards = UIBuilder.Node("Cards", Root);
            UIBuilder.FillParent(_cards, 0f);
        }

        public override void OnShow()
        {
            App.RefreshHud();
            Render();
            if (!_loading) { _loading = true; StartCoroutine(LoadRoutine()); }
        }

        public override void OnHide()
        {
            _loading = false;
        }

        private IEnumerator LoadRoutine()
        {
            yield return State.Load(GuildDungeonBackends.For(App));
            _loading = false;
            Render();
        }

        private void BuildTabs(int floors)
        {
            if (_tabButtons.Length == floors) return;
            for (int i = _tabs.childCount - 1; i >= 0; i--) Destroy(_tabs.GetChild(i).gameObject);
            _tabButtons = new ForgeTab[floors];
            for (int f = 1; f <= floors; f++)
            {
                int captured = f;
                _tabButtons[f - 1] = EmberforgeUI.Tab("FLOOR " + f, _tabs, 200f, 52f, delegate { OnFloor(captured); });
            }
        }

        private void Render()
        {
            for (int i = _cards.childCount - 1; i >= 0; i--) Destroy(_cards.GetChild(i).gameObject);
            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            if (backend == null || !backend.Ready || !State.Loaded)
            {
                _headerSubtitle.text = !string.IsNullOrEmpty(State.Error) ? State.Error.ToUpperInvariant() : "LOADING";
                return;
            }

            BuildTabs(State.FloorCount);
            _floor = Mathf.Clamp(_floor, 1, State.FloorCount);
            for (int i = 0; i < _tabButtons.Length; i++) EmberforgeUI.SetTabSelected(_tabButtons[i], i + 1 == _floor);

            GuildDungeonAttempt first = GuildDungeon.FirstGuildClear(State.Attempts, State.Cycle, _floor);
            _headerSubtitle.text = backend.GuildName.ToUpperInvariant() + "   -   FLOOR " + _floor + "   -   "
                                 + (first != null ? "FIRST CLEARED BY " + GuildDungeonFlow.NameOf(first).ToUpperInvariant()
                                                  : "NOT CLEARED YET");

            for (int b = 0; b < GuildDungeon.Boards.Length; b++)
            {
                float x = ((b % 3) - 1) * (CardWidth + 24f);
                float y = -(Top + 68f) - (b / 3) * (CardHeight + 16f);
                AddCard(GuildDungeon.Boards[b], x, y, backend.SelfUserId);
            }
        }

        private void AddCard(GuildDungeonBoard board, float x, float y, string self)
        {
            List<GuildDungeonAttempt> ranked = GuildDungeon.Rank(App.Content, State.Attempts, State.Cycle, _floor, board);
            bool mineOn = ranked.Exists(a => a.UserId == self);

            RectTransform card = UIBuilder.Node(board.ToString(), _cards);
            UIBuilder.Box(card, x, y, CardWidth, CardHeight, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(card, false);

            Text title = EmberforgeUI.Title("Title", card, GuildDungeon.BoardTitle(board), UITheme.FontBody,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 26f, -16f, 380f, 36f, new Vector2(0f, 1f));
            EmberforgeUI.ClearCornerOrnament(title.rectTransform);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text count = EmberforgeUI.Label("Count", card, ranked.Count == 1 ? "1 ENTRY" : ranked.Count + " ENTRIES",
                                            UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.UpperRight);
            UIBuilder.Box(count.rectTransform, -Emberforge.OrnamentClearance, -22f, 160f, 24f, new Vector2(1f, 1f));

            Text rule = EmberforgeUI.Label("Rule", card, GuildDungeon.BoardRule(board), UITheme.FontTiny - 4,
                                           Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(rule.rectTransform, 26f, -54f, CardWidth - 52f, 24f, new Vector2(0f, 1f));

            ScrollRect scroll;
            RectTransform list = UIBuilder.ScrollList("List", card, out scroll, 6f, new RectOffset(0, 0, 0, 0));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 20f, 84f, 20f, 18f);

            // C7: a board the floor's condition hands to every line is suspended this cycle, and
            // says which condition did it rather than sitting empty as though nobody had tried.
            FloorConditionDef condition = GuildDungeon.ConditionOf(App.Content, State.Status.Seed, _floor);
            if (GuildDungeon.Grants(condition, board))
            {
                title.color = Emberforge.TextQuiet;
                Text off = EmberforgeUI.Body("Suspended", list, "SUSPENDED THIS CYCLE. " + condition.Name.ToUpperInvariant()
                                             + " gives it to every line on this floor, so it would rank nothing.",
                                             UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Sized(off.rectTransform, CardWidth - 60f, 120f);
                return;
            }

            if (ranked.Count == 0)
            {
                Text empty = EmberforgeUI.Body("Empty", list, "Nobody has cleared this floor this way yet. The first to do it leads.",
                                               UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, CardWidth - 60f, 120f);
                return;
            }

            for (int i = 0; i < ranked.Count; i++)
            {
                GuildDungeonAttempt a = ranked[i];
                bool mine = a.UserId == self;
                ForgeCard row = EmberforgeUI.SelectableCard("Entry" + i, list, delegate { OnWatch(a); });
                UIBuilder.Sized(row.Root, CardWidth - 44f, 50f);
                if (!mine) row.Plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Sunken);
                else EmberforgeUI.SetCardSelected(row, true);

                Text rank = EmberforgeUI.Title("Rank", row.Body, GuildDungeonFlow.Ordinal(i + 1), UITheme.FontSmall,
                                               i == 0 ? Emberforge.TextGold : Emberforge.TextBody, TextAnchor.MiddleLeft);
                UIBuilder.Box(rank.rectTransform, 16f, 0f, 70f, 40f, new Vector2(0f, 0.5f));

                Text who = EmberforgeUI.Label("Who", row.Body,
                                              GuildDungeonFlow.NameOf(a).ToUpperInvariant() + (mine ? "   (YOU)" : ""),
                                              UITheme.FontTiny, mine ? Emberforge.TextGold : Emberforge.TextBody, TextAnchor.MiddleLeft);
                UIBuilder.Box(who.rectTransform, 90f, 0f, 230f, 40f, new Vector2(0f, 0.5f));

                Text turns = EmberforgeUI.Label("Turns", row.Body, a.Turns + " TURNS", UITheme.FontTiny,
                                                Emberforge.TextBody, TextAnchor.MiddleRight);
                UIBuilder.Box(turns.rectTransform, -150f, 0f, 120f, 40f, new Vector2(1f, 0.5f));

                Text which = EmberforgeUI.Label("Which", row.Body, "TRY " + a.Attempt + "  >", UITheme.FontTiny - 6,
                                                Emberforge.TextQuiet, TextAnchor.MiddleRight);
                UIBuilder.Box(which.rectTransform, -14f, 0f, 120f, 40f, new Vector2(1f, 0.5f));
            }

            if (!mineOn)
            {
                Text none = EmberforgeUI.Label("NotYou", list, "YOU ARE NOT ON THIS BOARD YET", UITheme.FontTiny - 6,
                                               Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Sized(none.rectTransform, CardWidth - 44f, 30f);
            }
        }

        private void OnFloor(int floor)
        {
            App.Audio.Tap();
            _floor = floor;
            Render();
        }

        private void OnWatch(GuildDungeonAttempt attempt)
        {
            App.Audio.Tap();
            GuildDungeonFlow.Watch(App, Router, attempt, ScreenId.GuildDungeonBoards, "BACK TO THE BOARDS");
        }
    }
}
