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
    /// THE FLOOR'S CONDITION RUNS ACROSS THE TOP, and the six board tiles under the line say which
    /// boards it would enter if it clears, what each is up against (the guild's lead, nobody yet,
    /// or suspended by the condition), all before anything is committed - the constraints are
    /// nobody's declaration, but they are everybody's to aim for. A line the condition refuses
    /// says why and cannot be attempted.
    ///
    /// Opened from a replay (RUN IT YOURSELF) the floor view carries what was watched: whose fight,
    /// how it ended, the line they used. That is the handover's second half.
    /// </summary>
    public sealed class GuildFloorScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildFloor; } }

        private const float PanelWidth = 880f;
        /// <summary>Where the condition band starts. The panels start under it.</summary>
        private const float BandTop = UITheme.TopBarHeight + 118f;
        private const float BandHeight = 72f;
        private const float Top = BandTop + BandHeight + 8f;
        private const float SlotSize = 150f;

        private Text _headerSubtitle;
        private Text _title;
        private RectTransform _band;
        private Text _conditionName;
        private Text _conditionText;
        private Text _lineVerdict;
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

        /// <summary>The line as it stands, read-only. The capture run reads it to tap a line into
        /// shape the way a player would; nothing else needs it.</summary>
        public IList<string> CurrentLine { get { return _line.AsReadOnly(); } }
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
            BuildConditionBand();
            BuildEnemyPanel();
            BuildLinePanel();
            BuildRosterPanel();
        }

        /// <summary>
        /// THE FLOOR'S CONDITION, ACROSS THE TOP, BEFORE ANYTHING ELSE ON THE SCREEN
        /// (DESIGN-GUILD-FLOOR-CONDITIONS C3). A condition found mid-fight is a trap; one read
        /// before the line is chosen is the reason to choose a different line. The second line
        /// says whether the line being built may be fielded under it, and if not, what to change.
        /// </summary>
        private void BuildConditionBand()
        {
            _band = UIBuilder.Node("Condition", Root);
            UIBuilder.Box(_band, 0f, -BandTop, PanelWidth * 2f + 28f, BandHeight, new Vector2(0.5f, 1f));
            EmberforgeUI.PlainPanelOn(_band);

            Text label = EmberforgeUI.Label("Label", _band, "THIS CYCLE", UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 28f, -8f, 300f, 22f, new Vector2(0f, 1f));

            _conditionName = EmberforgeUI.Title("Name", _band, "", UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_conditionName.rectTransform, 28f, -28f, 330f, 38f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_conditionName, Emberforge.Outline, Emberforge.StrokeThin);

            _conditionText = EmberforgeUI.Label("Text", _band, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_conditionText.rectTransform, 370f, -8f, PanelWidth * 2f + 28f - 400f, 34f, new Vector2(0f, 1f));

            _lineVerdict = EmberforgeUI.Label("Verdict", _band, "", UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_lineVerdict.rectTransform, 370f, -42f, PanelWidth * 2f + 28f - 400f, 28f, new Vector2(0f, 1f));
        }

        private void BuildEnemyPanel()
        {
            _enemyPanel = UIBuilder.Node("EnemyLine", Root);
            UIBuilder.Box(_enemyPanel, -PanelWidth / 2f - 14f, -Top, PanelWidth, 628f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(_enemyPanel, true);
            EmberforgeUI.Heading("Heading", _enemyPanel, "THE LINE YOU FACE", 36f, -20f);

            _enemyRows = UIBuilder.Node("Rows", _enemyPanel);
            UIBuilder.FillParent(_enemyRows, 20f, 62f, 20f, 20f);
        }

        private void BuildLinePanel()
        {
            _linePanel = UIBuilder.Node("YourLine", Root);
            UIBuilder.Box(_linePanel, PanelWidth / 2f + 14f, -Top, PanelWidth, 440f, new Vector2(0.5f, 1f));
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
            // Ends a gap short of the button's left edge (the button is 260 wide, OrnamentClearance in from the right).
            UIBuilder.Box(_attemptsLeft.rectTransform, -(Emberforge.OrnamentClearance + 260f + UITheme.Space16), 26f, 280f, 60f, new Vector2(1f, 0f));

            _attemptButton = EmberforgeUI.Button("Attempt", _linePanel, "ATTEMPT", UITheme.FontBody, ForgeButtonKind.Primary);
            UIBuilder.Box(_attemptButton.Root, -Emberforge.OrnamentClearance, 20f, 260f, 72f, new Vector2(1f, 0f));
            _attemptButton.OnClick(OnAttempt);
        }

        private void BuildRosterPanel()
        {
            RectTransform panel = UIBuilder.Node("Roster", Root);
            UIBuilder.Box(panel, PanelWidth / 2f + 14f, -(Top + 452f), PanelWidth, 184f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(panel, false);

            Text label = EmberforgeUI.Label("Label", panel, "YOUR HEROES   -   TAP TO ADD OR REMOVE", UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.UpperLeft);
            // Hung below the frame's top rail (-12 sat on it and the caps were cut).
            UIBuilder.Box(label.rectTransform, 28f, -20f, 800f, 26f, new Vector2(0f, 1f));
            EmberforgeUI.ClearCornerOrnament(label.rectTransform);

            ScrollRect scroll;
            _rosterContent = UIBuilder.ScrollStrip("Strip", panel, out scroll, 10f, new RectOffset(4, 4, 0, 0));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 22f, 48f, 22f, 10f);
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
            _band.gameObject.SetActive(usable);
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
                UIBuilder.Box(row, 0f, -i * 110f, PanelWidth - 40f, 104f, new Vector2(0.5f, 1f));
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
            //
            // THE SIX BOARDS, BEFORE THE LINE IS COMMITTED (DESIGN-GUILD-FLOOR-CONDITIONS section 5).
            // Each tile says whether this line would enter it and what it is up against: the
            // guild's current lead on this floor, nobody yet, or suspended because the floor's
            // condition hands it to every line (C7). Read from GuildDungeon, never decided here.
            GuildDungeonAttempt probe = new GuildDungeonAttempt();
            probe.Victory = true;
            probe.Attempt = used + 1;
            probe.Fallen = -1;
            probe.Seed = State.Status.Seed;
            probe.Floor = floor;
            probe.Cycle = State.Cycle;
            probe.Team = GuildDungeon.LineOf(App.Content, Heroes(), "");
            FloorConditionDef condition = GuildDungeon.ConditionOf(App.Content, State.Status.Seed, floor);
            string broke;
            bool legal = GuildDungeon.LineLegal(App.Content, condition, probe.Team, out broke);
            for (int b = 0; b < GuildDungeon.Boards.Length; b++)
            {
                GuildDungeonBoard board = GuildDungeon.Boards[b];
                bool suspended = GuildDungeon.Grants(condition, board);
                bool met = !suspended && legal
                           && (board == GuildDungeonBoard.Flawless || GuildDungeon.Satisfies(App.Content, probe, board));
                RectTransform tile = UIBuilder.Node(board.ToString(), _chips);
                UIBuilder.Box(tile, 36f + (b % 3) * 272f, -248f - (b / 3) * 46f, 262f, 42f, new Vector2(0f, 1f));
                if (met) EmberforgeUI.PlainPanelOn(tile); else EmberforgeUI.SunkenOn(tile);

                Text title = EmberforgeUI.Label("Title", tile, GuildDungeon.BoardTitle(board)
                                                + (board == GuildDungeonBoard.Flawless && met ? "  (IF NOBODY FALLS)" : ""),
                                                UITheme.FontTiny - 6, suspended ? Emberforge.TextQuiet : met ? Emberforge.TextGold : Emberforge.TextBody,
                                                TextAnchor.UpperLeft);
                UIBuilder.Box(title.rectTransform, 12f, -4f, 240f, 20f, new Vector2(0f, 1f));
                Text lead = EmberforgeUI.Label("Lead", tile, GuildDungeonFlow.BoardLead(App.Content, State, floor, board),
                                               UITheme.FontTiny - 8, Emberforge.TextQuiet, TextAnchor.UpperLeft);
                UIBuilder.Box(lead.rectTransform, 12f, -22f, 240f, 18f, new Vector2(0f, 1f));
            }

            _levelNote.gameObject.SetActive(false);
            RenderCondition(condition, legal, broke, level);

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
                // Above the portraits, left of the attempts count: beside them it ran into that
                // count, and higher it would meet the second row of board tiles.
                UIBuilder.Box(who.rectTransform, 36f, 74f, 330f, 22f, new Vector2(0f, 0f));
                RectTransform faces = UIBuilder.Node("Faces", _watchedHost);
                UIBuilder.Box(faces, 36f, 20f, 300f, 50f, new Vector2(0f, 0f));
                GuildDungeonFlow.DrawLine(faces, App.Content, watched.Team, 0f, 0f, 48f, 4f);
            }

            int left = State.AttemptsLeft(self, floor);
            bool open = GuildDungeon.FloorOpen(State.Attempts, State.Cycle, floor);
            _attemptsLeft.text = !open ? "LOCKED" : left + " OF " + State.Cap + " ATTEMPTS LEFT";
            _attemptsLeft.color = left > 0 ? Emberforge.TextBody : UITheme.Danger;
            _attemptButton.SetText(_busy ? "RECORDING" : left > 0 ? "ATTEMPT" : "NONE LEFT");
            _attemptButton.Interactable = open && left > 0 && _line.Count > 0 && !_busy && legal;
        }

        /// <summary>The band across the top: the condition, and whether this line may face it.</summary>
        private void RenderCondition(FloorConditionDef condition, bool legal, string broke, int level)
        {
            if (condition == null)
            {
                _conditionName.text = "NO CONDITION";
                _conditionText.text = "This floor plays by the dungeon's standing rules.";
            }
            else
            {
                _conditionName.text = condition.Name.ToUpperInvariant();
                _conditionText.text = condition.Text;
            }
            if (!legal)
            {
                _lineVerdict.text = "YOUR LINE CANNOT ENTER: " + broke.ToUpperInvariant() + ".";
                _lineVerdict.color = UITheme.Danger;
            }
            else
            {
                _lineVerdict.text = GuildDungeonFlow.FamilyLabel(condition) + "Every hero fights at level " + level
                                  + ", no relics, no armor. Talents and stars count. Your formation is untouched.";
                _lineVerdict.color = Emberforge.TextQuiet;
            }
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
            string broke;
            if (!GuildDungeon.LineLegal(App.Content, GuildDungeon.ConditionOf(App.Content, State.Status.Seed, floor),
                                        GuildDungeon.LineOf(App.Content, Heroes(), ""), out broke))
            {
                App.Notify("This line cannot enter: " + broke + ".");
                return;
            }
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
