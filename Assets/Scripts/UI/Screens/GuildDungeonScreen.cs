using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The guild dungeon's front door: this cycle, how far the guild has pushed, how far YOU have,
    /// what you have left, how long is left, and the five floors.
    ///
    /// A FLOOR ROW SHOWS ITS ENEMIES BEFORE IT IS OPENED. The whole mode is a fixed fight that a
    /// guild solves with thirty rosters, and a puzzle is only a puzzle if it can be looked at before
    /// anything is spent on it. The row is a preview; the floor view is the inspection.
    ///
    /// Floors open for the whole guild together (GuildDungeon.FloorOpen): a locked row says who
    /// has to clear what, rather than just being grey.
    /// </summary>
    public sealed class GuildDungeonScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildDungeon; } }

        private const float CardWidth = 1180f;
        private const float RowHeight = 178f;

        /// <summary>Shared with the floor, result and boards screens through the router.</summary>
        public readonly GuildDungeonState State = new GuildDungeonState();

        private Text _headerSubtitle;
        private ScrollRect _scroll;
        private RectTransform _content;
        private RectTransform _statusCard;
        private Text _cycle;
        private Text _timeLeft;
        private Text _guildPush;
        private Text _mine;
        private RectTransform _pushPips;
        private UIButton _boardsButton;
        private RectTransform _rows;
        private Text _note;
        private bool _loading;

        public bool Loading { get { return _loading; } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "GUILD DUNGEON", Router, out _headerSubtitle);

            _content = UIBuilder.ScrollList("Content", Root, out _scroll, 14f, new RectOffset(0, 0, 8, 40));
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(), UITheme.ScreenPadding,
                                 UITheme.TopBarHeight + 118f, UITheme.ScreenPadding, 20f);

            BuildStatusCard();

            _rows = UIBuilder.Node("Floors", _content);
            UIBuilder.Sized(_rows, CardWidth, 0f);
            UIBuilder.VStack(_rows, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            _rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _note = EmberforgeUI.Body("Note", _content, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Sized(_note.rectTransform, CardWidth - 120f, 96f);
        }

        private void BuildStatusCard()
        {
            _statusCard = UIBuilder.Node("Status", _content);
            UIBuilder.Sized(_statusCard, CardWidth, 200f);
            EmberforgeUI.PanelOn(_statusCard, true);

            _cycle = EmberforgeUI.Title("Cycle", _statusCard, "", UITheme.FontTitle, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_cycle.rectTransform, 44f, -26f, 320f, 56f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_cycle, Emberforge.Outline, Emberforge.StrokeThin);

            _timeLeft = EmberforgeUI.Label("TimeLeft", _statusCard, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_timeLeft.rectTransform, 44f, -90f, 320f, 34f, new Vector2(0f, 1f));

            Text pushLabel = EmberforgeUI.Label("PushLabel", _statusCard, "THE GUILD HAS REACHED", UITheme.FontTiny,
                                                Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(pushLabel.rectTransform, 380f, -28f, 360f, 28f, new Vector2(0f, 1f));

            _guildPush = EmberforgeUI.Title("Push", _statusCard, "", UITheme.FontBody, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_guildPush.rectTransform, 380f, -60f, 460f, 40f, new Vector2(0f, 1f));

            _pushPips = UIBuilder.Node("Pips", _statusCard);
            UIBuilder.Box(_pushPips, 380f, -112f, 360f, 30f, new Vector2(0f, 1f));

            // Your own line runs along the bottom of the card, under the cycle and the guild's
            // push: beside them it collided with the push title and with BOARDS.
            _mine = EmberforgeUI.Label("Mine", _statusCard, "", UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_mine.rectTransform, 44f, -154f, 800f, 30f, new Vector2(0f, 1f));

            // BOARDS over GUILD PATH in the right-hand column. Side by side, the second button ran
            // into the guild's push line and its pips, which own the middle of the card.
            _boardsButton = EmberforgeUI.Button("Boards", _statusCard, "BOARDS", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_boardsButton.Root, -44f, 42f, 260f, 66f, new Vector2(1f, 0.5f));
            _boardsButton.OnClick(OnBoards);

            // What the dungeon pays is spent on the Guild Path, so its door sits under the boards
            // that pay the bonus.
            UIButton path = EmberforgeUI.Button("Path", _statusCard, "GUILD PATH", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(path.Root, -44f, -42f, 260f, 66f, new Vector2(1f, 0.5f));
            path.OnClick(delegate { App.Audio.Tap(); Router.Show(ScreenId.GuildPath); });
        }

        // ================================================================== lifecycle

        public override void OnShow()
        {
            App.RefreshHud();
            Render();
            Reload();
        }

        public override void OnHide()
        {
            // A coroutine dies with its screen's GameObject; forget that one was running, or the
            // next visit sees _loading still true and never loads. GuildScreen learnt this first.
            _loading = false;
        }

        public void Reload()
        {
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            if (!State.Loaded) _headerSubtitle.text = "LOADING";
            yield return State.Load(GuildDungeonBackends.For(App));
            _loading = false;
            Render();
        }

        // ================================================================== render

        private void Render()
        {
            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            bool usable = backend != null && backend.Ready && State.Loaded && string.IsNullOrEmpty(State.Error);
            _statusCard.gameObject.SetActive(usable);
            ClearRows();

            if (!usable)
            {
                _headerSubtitle.text = backend == null ? "LOCAL ONLY" : (State.Loaded ? "OFFLINE" : "LOADING");
                _note.text = !string.IsNullOrEmpty(State.Error) ? State.Error
                           : backend == null ? "This build has no cloud configured, so there is no guild dungeon."
                           : "";
                return;
            }

            string self = backend.SelfUserId;
            int cycle = State.Cycle;
            int floors = State.FloorCount;
            int guildBest = GuildDungeon.HighestCleared(State.Attempts, null, cycle);
            int myBest = GuildDungeon.HighestCleared(State.Attempts, self, cycle);

            _headerSubtitle.text = backend.GuildName.ToUpperInvariant() + "   -   CYCLE " + cycle;
            _cycle.text = "CYCLE " + cycle;
            _timeLeft.text = GuildDungeonFlow.TimeLeft(State.Status.EndsUnix - TimeUtil.NowUnix());
            _guildPush.text = guildBest == 0 ? "NOTHING CLEARED YET" : "FLOOR " + guildBest + " OF " + floors + " CLEARED";
            DrawPips(guildBest, floors);

            int spent = 0, open = 0;
            for (int f = 1; f <= floors; f++)
            {
                spent += GuildDungeon.AttemptsUsed(State.Attempts, self, cycle, f);
                if (GuildDungeon.FloorOpen(State.Attempts, cycle, f)) open++;
            }
            _mine.text = "YOU: " + (myBest == 0 ? "no floor cleared yet" : "cleared up to floor " + myBest)
                       + "   -   " + (open * State.Cap - spent) + " attempts left across the "
                       + open + (open == 1 ? " open floor" : " open floors");

            for (int f = 1; f <= floors; f++) AddFloorRow(f, self);

            _note.text = "Every hero fights here at level " + App.Content.Balance.GuildDungeonHeroLevel
                       + ", with no relics and no armor. Talents still count. The same line always fights the "
                       + "same battle, so each of your " + State.Cap + " attempts on a floor is a decision, not a reroll.";
        }

        private void DrawPips(int cleared, int floors)
        {
            for (int i = _pushPips.childCount - 1; i >= 0; i--) Destroy(_pushPips.GetChild(i).gameObject);
            for (int f = 1; f <= floors; f++)
            {
                // One bar per floor, full when the guild has cleared it: five short bars read as
                // "three of five" at a glance where one long bar reads as a percentage.
                Image fill = EmberforgeUI.ProgressBar("Pip" + f, _pushPips);
                UIBuilder.Box((RectTransform)fill.rectTransform.parent, (f - 1) * 66f, 0f, 58f, 26f, new Vector2(0f, 0.5f));
                EmberforgeUI.SetProgress(fill, f <= cleared ? 1f : 0f);
            }
        }

        private void ClearRows()
        {
            for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);
        }

        /// <summary>
        /// One floor. Its number and shape on the left, its five enemies in the middle, where the
        /// guild and you stand on it, and INSPECT on the right - or, locked, what opens it.
        /// </summary>
        private void AddFloorRow(int floor, string self)
        {
            int cycle = State.Cycle;
            bool open = GuildDungeon.FloorOpen(State.Attempts, cycle, floor);
            StageDef stage = GuildDungeonGenerator.BuildFloor(App.Content, State.Status.Seed, floor);
            GuildDungeonTheme theme = GuildDungeonGenerator.ThemesFor(State.FloorCount, State.Status.Seed)[floor - 1];

            RectTransform row = UIBuilder.Node("Floor" + floor, _rows);
            UIBuilder.Sized(row, CardWidth, RowHeight);
            if (open) EmberforgeUI.PlainPanelOn(row); else EmberforgeUI.SunkenOn(row);

            Text name = EmberforgeUI.Title("Name", row, "FLOOR " + floor, UITheme.FontHeading,
                                           open ? Emberforge.TextBody : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 28f, -18f, 220f, 44f, new Vector2(0f, 1f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform chip = EmberforgeUI.Chip("Theme", row, GuildDungeonFlow.ThemeLabel(theme),
                                                   theme == GuildDungeonTheme.Champion ? UITheme.Danger : Emberforge.TextGold,
                                                   UITheme.FontTiny - 4);
            UIBuilder.Box(chip, 28f, -70f, 150f, 30f, new Vector2(0f, 1f));

            Text subtitle = EmberforgeUI.Label("Subtitle", row, stage.Subtitle, UITheme.FontTiny - 2,
                                               Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(subtitle.rectTransform, 28f, -104f, 250f, 26f, new Vector2(0f, 1f));

            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                EnemyDef def = App.Content.GetEnemy(stage.Enemies[i].EnemyId);
                if (def == null) continue;
                RectTransform face = EmberforgeUI.PortraitFrame("Enemy" + i, row, def, 3, 76f);
                UIBuilder.Box(face, 280f + i * 84f, 18f, 76f, 76f, new Vector2(0f, 0.5f));
            }

            // THE FLOOR'S CONDITION, ON THE ROW (DESIGN-GUILD-FLOOR-CONDITIONS C3): readable from
            // here, before the floor is even opened, let alone a line committed. A board it
            // suspends (C7) is named with it.
            FloorConditionDef condition = GuildDungeon.ConditionOf(App.Content, State.Status.Seed, floor);
            RectTransform strip = UIBuilder.Node("Condition", row);
            UIBuilder.Box(strip, 280f, 10f, 700f, 36f, new Vector2(0f, 0f));
            EmberforgeUI.SunkenOn(strip);
            string suspended = "";
            for (int b = 0; b < GuildDungeon.Boards.Length; b++)
                if (GuildDungeon.Grants(condition, GuildDungeon.Boards[b]))
                    suspended += "   -   " + GuildDungeon.BoardTitle(GuildDungeon.Boards[b]) + " SUSPENDED";
            Text conditionText = EmberforgeUI.Label("Text", strip,
                condition == null ? "NO CONDITION" : condition.Name.ToUpperInvariant() + ":  " + condition.Text + suspended,
                UITheme.FontTiny - 4, condition == null ? Emberforge.TextQuiet : Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(conditionText.rectTransform, 14f, 0f, 10f, 0f);
            Text level = EmberforgeUI.Label("Level", row, "LV " + stage.Enemies[stage.Enemies.Count - 1].Level,
                                            UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(level.rectTransform, 280f, -10f, 200f, 22f, new Vector2(0f, 1f));

            int clearedBy = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < State.Attempts.Count; i++)
            {
                GuildDungeonAttempt a = State.Attempts[i];
                if (a.Cycle == cycle && a.Floor == floor && a.Victory && seen.Add(a.UserId)) clearedBy++;
            }
            GuildDungeonAttempt first = GuildDungeon.FirstGuildClear(State.Attempts, cycle, floor);
            int used = GuildDungeon.AttemptsUsed(State.Attempts, self, cycle, floor);
            GuildDungeonAttempt best = BestOf(self, floor);

            string guildLine = clearedBy == 0 ? (open ? "NOT CLEARED YET" : "LOCKED")
                             : "CLEARED BY " + clearedBy + "   -   FIRST: " + GuildDungeonFlow.NameOf(first).ToUpperInvariant();
            Text guild = EmberforgeUI.Label("Guild", row, guildLine, UITheme.FontTiny - 2,
                                            clearedBy > 0 ? UITheme.Success : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(guild.rectTransform, 710f, -22f, 330f, 26f, new Vector2(0f, 1f));

            string mineLine = !open
                ? "Opens when anyone clears floor " + (floor - 1) + "."
                : "YOUR ATTEMPTS " + used + " / " + State.Cap
                  + (best != null ? "\nBEST: " + GuildDungeonFlow.OutcomeLine(best) : used > 0 ? "\nNo clear yet." : "");
            Text mine = EmberforgeUI.Body("Mine", row, mineLine, UITheme.FontTiny - 2,
                                          Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(mine.rectTransform, 710f, -54f, 250f, 64f, new Vector2(0f, 1f));

            UIButton inspect = EmberforgeUI.Button("Inspect", row, open ? "INSPECT" : "LOCKED", UITheme.FontSmall,
                                                   open ? ForgeButtonKind.Primary : ForgeButtonKind.Secondary);
            UIBuilder.Box(inspect.Root, -28f, 0f, 200f, 76f, new Vector2(1f, 0.5f));
            inspect.Interactable = open;
            int captured = floor;
            inspect.OnClick(delegate { OpenFloor(captured); });
        }

        private GuildDungeonAttempt BestOf(string self, int floor)
        {
            GuildDungeonAttempt best = null;
            for (int i = 0; i < State.Attempts.Count; i++)
            {
                GuildDungeonAttempt a = State.Attempts[i];
                if (a.UserId != self || a.Cycle != State.Cycle || a.Floor != floor || !a.Victory) continue;
                if (best == null || a.Turns < best.Turns) best = a;
            }
            return best;
        }

        // ================================================================== actions

        /// <summary>Opens a floor for inspection. Not from a replay, so the handover's memory of
        /// something watched is dropped: the floor view should not claim you just watched a fight
        /// you walked away from two screens ago.</summary>
        public void OpenFloor(int floor)
        {
            App.Audio.Tap();
            State.Floor = floor;
            State.Watched = null;
            Router.GuildFloor.ResetOnNextShow();
            Router.Show(ScreenId.GuildFloor);
        }

        private void OnBoards()
        {
            App.Audio.Tap();
            Router.GuildDungeonBoards.SetFloor(Mathf.Max(1, GuildDungeon.HighestCleared(State.Attempts, null, State.Cycle)));
            Router.Show(ScreenId.GuildDungeonBoards);
        }
    }
}
