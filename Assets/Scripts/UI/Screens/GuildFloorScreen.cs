using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// ONE FLOOR, INSPECTED: the enemy line on the left, your line on the right, your heroes
    /// under it, and ATTEMPT.
    ///
    /// THE ENEMY LINE IS SHOWN WHOLE BEFORE ANYTHING IS SPENT. Name, level, Aspect, what each one
    /// does (it holds the front, it picks its target, it hits everyone - read off its abilities by
    /// GuildDungeonGenerator.Classify, the same reading that built the floor) and its numbers at
    /// the level it fights at. A fixed fight you cannot inspect is a slot machine with extra steps.
    ///
    /// YOUR LINE IS CHOSEN HERE, NOT ON FORMATION. It starts as your formation and nothing done
    /// here changes that: a line built for one floor's puzzle is not the line you want the
    /// campaign to run. Every hero shows at the dungeon's level, because that is what it fights at.
    ///
    /// The chips under the line say which boards it would enter if it clears, before it is
    /// committed - the constraints are nobody's declaration, but they are everybody's to aim for.
    ///
    /// Opened from a replay (RUN IT YOURSELF) the floor view carries what was watched: whose fight,
    /// how it ended, the line they used. That is the handover's second half.
    /// </summary>
    public sealed class GuildFloorScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildFloor; } }

        private const float PanelWidth = 880f;
        private const float Top = UITheme.TopBarHeight + 118f;
        private const float SlotSize = 150f;

        private Text _headerSubtitle;
        private Text _title;
        private RectTransform _enemyPanel;
        private RectTransform _enemyRows;
        private RectTransform _linePanel;
        private RectTransform _slots;
        private RectTransform _chips;
        private Text _chipsLabel;
        private Text _levelNote;
        private RectTransform _watchedHost;
        private Text _attemptsLeft;
        private UIButton _attemptButton;
        private RectTransform _rosterContent;
        private readonly Dictionary<string, ForgeCard> _rosterCards = new Dictionary<string, ForgeCard>();

        /// <summary>The line being built, by hero def id, in slot order.</summary>
        private readonly List<string> _line = new List<string>();
        private bool _reset = true;
        private int _lineFloor = -1;
        private bool _busy;
        private bool _loading;

        private GuildDungeonState State { get { return Router.GuildDungeon.State; } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            RectTransform header = ScreenChrome.ForgedHeader(Root, "FLOOR", Router, out _headerSubtitle);
            // The ribbon's own label, so the title can name the floor: the header is built once
            // and the floor changes every time the screen is opened.
            Transform title = header.Find("Title/Text");
            _title = title != null ? title.GetComponent<Text>() : null;
            BuildEnemyPanel();
            BuildLinePanel();
            BuildRosterPanel();
        }

        private void BuildEnemyPanel()
        {
            _enemyPanel = UIBuilder.Node("EnemyLine", Root);
            UIBuilder.Box(_enemyPanel, -PanelWidth / 2f - 14f, -Top, PanelWidth, 640f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(_enemyPanel, true);
            EmberforgeUI.Heading("Heading", _enemyPanel, "THE LINE YOU FACE", 36f, -20f);

            _enemyRows = UIBuilder.Node("Rows", _enemyPanel);
            UIBuilder.FillParent(_enemyRows, 20f, 62f, 20f, 20f);
        }

        private void BuildLinePanel()
        {
            _linePanel = UIBuilder.Node("YourLine", Root);
            UIBuilder.Box(_linePanel, PanelWidth / 2f + 14f, -Top, PanelWidth, 452f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(_linePanel, true);
            EmberforgeUI.Heading("Heading", _linePanel, "YOUR LINE", 36f, -20f);

            _slots = UIBuilder.Node("Slots", _linePanel);
            UIBuilder.FillParent(_slots, 0f);

            _chipsLabel = EmberforgeUI.Label("ChipsLabel", _linePanel, "IF THIS LINE CLEARS, IT ENTERS", UITheme.FontTiny - 2,
                                             Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_chipsLabel.rectTransform, 36f, -222f, 800f, 26f, new Vector2(0f, 1f));

            _chips = UIBuilder.Node("Chips", _linePanel);
            UIBuilder.FillParent(_chips, 0f);

            _levelNote = EmberforgeUI.Body("LevelNote", _linePanel, "", UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_levelNote.rectTransform, 36f, -326f, 808f, 44f, new Vector2(0f, 1f));

            _watchedHost = UIBuilder.Node("Watched", _linePanel);
            UIBuilder.FillParent(_watchedHost, 0f);

            _attemptsLeft = EmberforgeUI.Label("AttemptsLeft", _linePanel, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.MiddleRight);
            UIBuilder.Box(_attemptsLeft.rectTransform, -300f, 34f, 280f, 60f, new Vector2(1f, 0f));

            _attemptButton = EmberforgeUI.Button("Attempt", _linePanel, "ATTEMPT", UITheme.FontBody, ForgeButtonKind.Primary);
            UIBuilder.Box(_attemptButton.Root, -Emberforge.OrnamentClearance, 28f, 260f, 76f, new Vector2(1f, 0f));
            _attemptButton.OnClick(OnAttempt);
        }

        private void BuildRosterPanel()
        {
            RectTransform panel = UIBuilder.Node("Roster", Root);
            UIBuilder.Box(panel, PanelWidth / 2f + 14f, -(Top + 464f), PanelWidth, 176f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(panel, false);

            Text label = EmberforgeUI.Label("Label", panel, "YOUR HEROES   -   TAP TO ADD OR REMOVE", UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 28f, -12f, 800f, 26f, new Vector2(0f, 1f));

            ScrollRect scroll;
            _rosterContent = UIBuilder.ScrollStrip("Strip", panel, out scroll, 10f, new RectOffset(4, 4, 0, 0));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 22f, 42f, 22f, 12f);
        }

        // ================================================================== lifecycle

        /// <summary>
        /// Asks for the line to start again from the formation the next time this is shown. The
        /// entry screen and the handover both ask; coming BACK to the floor from a fight does not,
        /// because the line just fought is the one a player wants to adjust, not lose.
        /// </summary>
        public void ResetOnNextShow()
        {
            _reset = true;
        }

        public override void OnShow()
        {
            App.RefreshHud();
            _busy = false;
            if (_reset || _line.Count == 0 || _lineFloor != State.Floor) ResetLine();
            _reset = false;
            _lineFloor = State.Floor;
            if (!State.Loaded) { Render(); Load(); return; }
            Render();
        }

        public override void OnHide()
        {
            _loading = false;
        }

        private void Load()
        {
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            yield return State.Load(GuildDungeonBackends.For(App));
            _loading = false;
            Render();
        }

        /// <summary>The line starts as the formation, each time the floor is opened.</summary>
        private void ResetLine()
        {
            _line.Clear();
            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int i = 0; i < team.Count && _line.Count < App.Content.Balance.MaxTeamSize; i++)
                if (team[i] != null && team[i].Def != null && !_line.Contains(team[i].DefId)) _line.Add(team[i].DefId);
        }

        // ================================================================== render

        private void Render()
        {
            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            bool usable = backend != null && backend.Ready && State.Loaded && State.Status != null;
            _enemyPanel.gameObject.SetActive(usable);
            _linePanel.gameObject.SetActive(usable);
            _rosterContent.parent.parent.gameObject.SetActive(usable);
            if (!usable)
            {
                _headerSubtitle.text = !string.IsNullOrEmpty(State.Error) ? State.Error.ToUpperInvariant() : "LOADING";
                return;
            }

            int floor = State.Floor;
            GuildDungeonTheme theme = GuildDungeonGenerator.ThemesFor(State.FloorCount, State.Status.Seed)[floor - 1];
            StageDef stage = GuildDungeonGenerator.BuildFloor(App.Content, State.Status.Seed, floor);

            if (_title != null) _title.text = "FLOOR " + floor;
            GuildDungeonAttempt watched = State.Watched;
            _headerSubtitle.text = watched != null
                ? "YOU WATCHED " + GuildDungeonFlow.NameOf(watched).ToUpperInvariant() + " "
                  + (watched.Victory ? "CLEAR IT IN " + watched.Turns + " TURNS" : "FALL AFTER " + watched.Turns + " TURNS")
                  + ".   NOW YOUR LINE."
                : GuildDungeonFlow.ThemeLabel(theme) + "   -   " + stage.Subtitle.ToUpperInvariant();

            RenderEnemies(stage);
            RenderLine(backend.SelfUserId);
            RenderRoster();
        }

        private void RenderEnemies(StageDef stage)
        {
            for (int i = _enemyRows.childCount - 1; i >= 0; i--) Destroy(_enemyRows.GetChild(i).gameObject);
            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                EnemyDef def = App.Content.GetEnemy(stage.Enemies[i].EnemyId);
                if (def == null) continue;
                int level = stage.Enemies[i].Level;

                RectTransform row = UIBuilder.Node("Enemy" + i, _enemyRows);
                UIBuilder.Box(row, 0f, -i * 112f, PanelWidth - 40f, 104f, new Vector2(0.5f, 1f));
                EmberforgeUI.SunkenOn(row);

                RectTransform face = EmberforgeUI.PortraitFrame("Face", row, def, 3, 84f);
                UIBuilder.Box(face, 12f, 0f, 84f, 84f, new Vector2(0f, 0.5f));

                Text name = EmberforgeUI.Label("Name", row, def.Name.ToUpperInvariant(), UITheme.FontSmall,
                                               Emberforge.TextBody, TextAnchor.UpperLeft);
                UIBuilder.Box(name.rectTransform, 110f, -10f, 360f, 32f, new Vector2(0f, 1f));

                Text sub = EmberforgeUI.Label("Sub", row, "LV " + level + "   " + AspectChart.DisplayName(def.Aspect).ToUpperInvariant(),
                                              UITheme.FontTiny - 4, Emberforge.Accent(def.Aspect), TextAnchor.UpperLeft);
                UIBuilder.Box(sub.rectTransform, 110f, -42f, 360f, 22f, new Vector2(0f, 1f));

                // What it DOES, read off its abilities - the same reading that built the floor.
                float cx = 110f;
                GuildDungeonGenerator.Behaviour b = GuildDungeonGenerator.Classify(def);
                if (stage.IsBoss && i == 0) cx = Chip(row, "CHAMPION", UITheme.Danger, cx);
                if ((b & GuildDungeonGenerator.Behaviour.Wall) != 0) cx = Chip(row, "HOLDS THE FRONT", Emberforge.TextGold, cx);
                if ((b & GuildDungeonGenerator.Behaviour.Hunter) != 0) cx = Chip(row, "PICKS ITS TARGET", UITheme.Warning, cx);
                if ((b & GuildDungeonGenerator.Behaviour.Spreader) != 0) cx = Chip(row, "HITS EVERYONE", UITheme.Ember, cx);
                if (b == GuildDungeonGenerator.Behaviour.None) Chip(row, "FIGHTS IN THE OPEN", Emberforge.TextQuiet, cx);

                UnitStats s = StatMath.ForEnemy(def, level);
                Text stats = EmberforgeUI.Body("Stats", row,
                    "HP " + Mathf.RoundToInt(s.MaxHp).ToString("N0") + "   ATK " + Mathf.RoundToInt(s.Attack)
                    + "\nDEF " + Mathf.RoundToInt(s.Defense) + "   SPD " + Mathf.RoundToInt(s.Speed),
                    UITheme.FontTiny - 4, Emberforge.TextBody, TextAnchor.UpperRight);
                UIBuilder.Box(stats.rectTransform, -16f, -10f, 300f, 48f, new Vector2(1f, 1f));

                string active = def.Active != null && !string.IsNullOrEmpty(def.Active.Name) ? def.Active.Name : "";
                if (active.Length > 0)
                {
                    Text act = EmberforgeUI.Label("Active", row, "ACTIVE  " + active.ToUpperInvariant(), UITheme.FontTiny - 4,
                                                  Emberforge.TextGold, TextAnchor.LowerRight);
                    UIBuilder.Box(act.rectTransform, -16f, 10f, 330f, 24f, new Vector2(1f, 0f));
                }
            }
        }

        private float Chip(RectTransform row, string text, Color tint, float x)
        {
            float width = 26f + text.Length * 10.5f;
            RectTransform chip = EmberforgeUI.Chip("Does", row, text, tint, UITheme.FontTiny - 6);
            UIBuilder.Box(chip, x, 8f, width, 28f, new Vector2(0f, 0f));
            return x + width + 6f;
        }

        private void RenderLine(string self)
        {
            for (int i = _slots.childCount - 1; i >= 0; i--) Destroy(_slots.GetChild(i).gameObject);
            for (int i = _chips.childCount - 1; i >= 0; i--) Destroy(_chips.GetChild(i).gameObject);
            for (int i = _watchedHost.childCount - 1; i >= 0; i--) Destroy(_watchedHost.GetChild(i).gameObject);

            int size = App.Content.Balance.MaxTeamSize;
            int level = App.Content.Balance.GuildDungeonHeroLevel;
            for (int slot = 0; slot < size; slot++)
            {
                int captured = slot;
                string defId = slot < _line.Count ? _line[slot] : null;
                HeroDef def = defId != null ? App.Content.GetHero(defId) : null;
                ForgeCard card = EmberforgeUI.SelectableCard("Slot" + slot, _slots, delegate { OnSlot(captured); });
                UIBuilder.Box(card.Root, 36f + slot * (SlotSize + 12f), -58f, SlotSize, SlotSize, new Vector2(0f, 1f));
                if (def == null)
                {
                    card.Plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Sunken);
                    Text empty = EmberforgeUI.Label("Empty", card.Body, "EMPTY", UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                    UIBuilder.FillParent(empty.rectTransform, 0f);
                    continue;
                }
                EmberforgeUI.SetCardSelected(card, true);
                RectTransform face = EmberforgeUI.PortraitFrame("Face", card.Body, def, def.Stars, 96f);
                UIBuilder.Box(face, 0f, -10f, 96f, 96f, new Vector2(0.5f, 1f));
                Text name = EmberforgeUI.Label("Name", card.Body, ShortName(def), UITheme.FontTiny - 4, Emberforge.TextBody, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -110f, SlotSize - 12f, 22f, new Vector2(0.5f, 1f));
                Text lv = EmberforgeUI.Label("Level", card.Body, "LV " + level, UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(lv.rectTransform, 0f, -128f, SlotSize - 12f, 18f, new Vector2(0.5f, 1f));
            }

            // Which boards this line would enter on a clear. The same rule the boards apply, asked
            // of a clear that has not happened: attempt number next, nobody lost unknown.
            int floor = State.Floor;
            int used = GuildDungeon.AttemptsUsed(State.Attempts, self, State.Cycle, floor);
            GuildDungeonAttempt probe = new GuildDungeonAttempt();
            probe.Victory = true;
            probe.Attempt = used + 1;
            probe.Fallen = -1;
            probe.Team = GuildDungeon.LineOf(App.Content, Heroes(), "");
            for (int b = 0; b < GuildDungeon.Boards.Length; b++)
            {
                GuildDungeonBoard board = GuildDungeon.Boards[b];
                bool met = board == GuildDungeonBoard.Flawless || GuildDungeon.Satisfies(App.Content, probe, board);
                string text = board == GuildDungeonBoard.Flawless ? "NOBODY FELL, IF NOBODY FALLS" : GuildDungeon.BoardTitle(board);
                RectTransform chip = EmberforgeUI.Chip(board.ToString(), _chips, text,
                                                       met ? Emberforge.TextGold : Emberforge.TextQuiet, UITheme.FontTiny - 6);
                UIBuilder.Box(chip, 36f + (b % 3) * 272f, -252f - (b / 3) * 38f, 262f, 32f, new Vector2(0f, 1f));
                if (!met) chip.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);
            }

            // Hidden while a watched fight is shown: the watched line sits in the same band.
            _levelNote.text = "Every hero fights at level " + level + ", no relics, no armor. Talents and stars count. "
                            + "Your formation is untouched.";
            _levelNote.gameObject.SetActive(State.Watched == null);

            GuildDungeonAttempt watched = State.Watched;
            if (watched != null)
            {
                bool sameFight = watched.Cycle == State.Cycle && watched.Floor == floor;
                Text who = EmberforgeUI.Label("Who", _watchedHost,
                    sameFight ? GuildDungeonFlow.NameOf(watched).ToUpperInvariant() + "'S LINE  -  "
                                + (watched.Victory ? "CLEARED IN " + watched.Turns : "FELL AFTER " + watched.Turns)
                              : "THAT FIGHT WAS AN EARLIER CYCLE'S FLOOR",
                    UITheme.FontTiny - 4, watched.Victory ? UITheme.Success : UITheme.Danger, TextAnchor.LowerLeft);
                // Beside the portraits and above the attempts count: under the portraits it was hidden, and
                // above them it met the second row of board chips.
                UIBuilder.Box(who.rectTransform, 300f, 98f, 290f, 22f, new Vector2(0f, 0f));
                RectTransform faces = UIBuilder.Node("Faces", _watchedHost);
                UIBuilder.Box(faces, 36f, 72f, 300f, 50f, new Vector2(0f, 0f));
                GuildDungeonFlow.DrawLine(faces, App.Content, watched.Team, 0f, 0f, 48f, 4f);
            }

            int left = State.AttemptsLeft(self, floor);
            bool open = GuildDungeon.FloorOpen(State.Attempts, State.Cycle, floor);
            _attemptsLeft.text = !open ? "LOCKED" : left + " OF " + State.Cap + " ATTEMPTS LEFT";
            _attemptsLeft.color = left > 0 ? Emberforge.TextBody : UITheme.Danger;
            _attemptButton.SetText(_busy ? "RECORDING" : left > 0 ? "ATTEMPT" : "NONE LEFT");
            _attemptButton.Interactable = open && left > 0 && _line.Count > 0 && !_busy;
        }

        private static string ShortName(HeroDef def)
        {
            string n = def.Name;
            int cut = n.IndexOf(" the ", System.StringComparison.OrdinalIgnoreCase);
            if (cut > 0) n = n.Substring(0, cut);
            return n.ToUpperInvariant();
        }

        private List<HeroInstance> Heroes()
        {
            List<HeroInstance> heroes = new List<HeroInstance>();
            for (int i = 0; i < _line.Count; i++)
            {
                HeroInstance h = App.Session.Profile.FindHeroByDefId(_line[i]);
                if (h != null) heroes.Add(h);
            }
            return heroes;
        }

        private void RenderRoster()
        {
            List<HeroInstance> owned = new List<HeroInstance>();
            foreach (HeroInstance h in App.Session.Profile.Heroes) if (h != null && h.Def != null) owned.Add(h);
            owned.Sort(delegate (HeroInstance a, HeroInstance b)
            {
                int c = b.Def.Stars.CompareTo(a.Def.Stars);
                return c != 0 ? c : string.CompareOrdinal(a.Def.Name, b.Def.Name);
            });

            if (_rosterCards.Count != owned.Count)
            {
                for (int i = _rosterContent.childCount - 1; i >= 0; i--) Destroy(_rosterContent.GetChild(i).gameObject);
                _rosterCards.Clear();
                for (int i = 0; i < owned.Count; i++)
                {
                    HeroDef def = owned[i].Def;
                    string id = def.Id;
                    ForgeCard card = EmberforgeUI.SelectableCard("Pick_" + id, _rosterContent, delegate { OnPick(id); });
                    UIBuilder.Sized(card.Root, 104f, 120f);
                    RectTransform face = EmberforgeUI.PortraitFrame("Face", card.Body, def, def.Stars, 78f);
                    UIBuilder.Box(face, 0f, -8f, 78f, 78f, new Vector2(0.5f, 1f));
                    Text name = EmberforgeUI.Label("Name", card.Body, ShortName(def), UITheme.FontTiny - 7, Emberforge.TextBody, TextAnchor.UpperCenter);
                    UIBuilder.Box(name.rectTransform, 0f, -90f, 100f, 22f, new Vector2(0.5f, 1f));
                    _rosterCards[id] = card;
                }
            }
            foreach (KeyValuePair<string, ForgeCard> kv in _rosterCards)
                EmberforgeUI.SetCardSelected(kv.Value, _line.Contains(kv.Key));
        }

        // ================================================================== actions

        private void OnSlot(int slot)
        {
            if (slot >= _line.Count || _busy) return;
            App.Audio.Tap();
            _line.RemoveAt(slot);
            Render();
        }

        private void OnPick(string defId)
        {
            if (_busy) return;
            App.Audio.Tap();
            if (_line.Contains(defId)) _line.Remove(defId);
            else if (_line.Count < App.Content.Balance.MaxTeamSize) _line.Add(defId);
            else { App.Notify("A line is five heroes. Tap one in the line to take it out."); return; }
            Render();
        }

        private void OnAttempt()
        {
            if (_busy || _line.Count == 0) return;
            App.Audio.Tap();
            IGuildDungeonBackend backend = GuildDungeonBackends.For(App);
            int floor = State.Floor;
            int attempt = State.Cap - State.AttemptsLeft(backend != null ? backend.SelfUserId : "", floor) + 1;
            App.Modal.ShowConfirm("COMMIT THIS LINE?",
                "This is attempt " + attempt + " of " + State.Cap + " on floor " + floor + ". The fight is fixed: "
                + "this line will fight this floor the same way every time.",
                "ATTEMPT", "NOT YET",
                delegate { StartCoroutine(AttemptRoutine(floor)); }, null);
        }

        private IEnumerator AttemptRoutine(int floor)
        {
            _busy = true;
            Render();
            TeamSnapshot line = GuildDungeon.LineOf(App.Content, Heroes(), App.Session.Profile.PlayerName);
            string failure = null;
            yield return GuildDungeonFlow.Attempt(App, Router, State, floor, line, delegate (string why) { failure = why; });
            _busy = false;
            if (failure != null)
            {
                App.Notify(failure);
                Render();
            }
        }
    }
}
