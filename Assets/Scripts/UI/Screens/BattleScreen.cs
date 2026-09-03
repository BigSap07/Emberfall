using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Watches a battle. The fight is already fully resolved by CombatSimulation before the
    /// first frame plays - this screen only animates the event list and reports the outcome.
    /// </summary>
    public sealed class BattleScreen : UIScreen
    {
        public override bool HidesTopBar { get { return true; } }
        public override ScreenId Id { get { return ScreenId.Battle; } }

        private sealed class UnitBar
        {
            public RectTransform Root;
            public Text Name;
            public Image HpFill;
            public Image HpGhost;
            public Image ShieldFill;
            public Image EnergyFill;
            public Text Status;
            public Image TurnPip;
            public CanvasGroup Group;
            /// <summary>
            /// One entry per entry the simulation holds, duplicates included, in the order they
            /// first landed. It used to be a set, which is what made Ember's stacking invisible:
            /// three Burn entries collapsed into one BRN tag on the way in and no later code could
            /// tell them apart. Kept in sync from the count StatusApplied carries, never tallied
            /// from how many times the event was seen - see SyncStatusCount.
            /// </summary>
            public readonly List<StatusKind> Statuses = new List<StatusKind>();

            /// <summary>Added to the bar's tracked screen position by LateUpdate while the turn cue plays.</summary>
            public float TurnLift;
        }

        private sealed class FloatText
        {
            public RectTransform Root;
            public Text Label;
            public CanvasGroup Group;
            public bool Busy;
        }

        private sealed class TurnStripIcon
        {
            public RectTransform Root;
            public Image Fill;
            public int UnitId;
        }

        private StageDef _stage;
        private BattleStage _world;
        private BattleResult _result;
        private Coroutine _playback;
        private Coroutine _bannerRoutine;
        private float _speed = 1f;
        private bool _finished;

        /// <summary>True once ShowResult has run - the fight is fully resolved and the result
        /// panel is up. Exists for external observers (editor automation, tests driving a real
        /// Play mode session) that need to know when to stop waiting; nothing in-game reads it.</summary>
        public bool Finished { get { return _finished; } }

        /// <summary>
        /// Nominal (pre-_speed) seconds owed before the NEXT event may render, drained at the top
        /// of Handle regardless of what event that turns out to be. Exists for a SpriteAnimator
        /// unit's basic/skill choreography: the event that TRIGGERED the clip only holds long
        /// enough for the outbound half (so the following Damage event fires exactly when the
        /// strike connects, not before and not after); the follow-through/return half is deferred
        /// here so it still finishes before anything else advances, without delaying Damage itself.
        /// Always 0 for a unit with no SpriteAnimator, so nothing changes for the other six heroes.
        /// </summary>
        private float _pendingAnimationTail;

        private Text _stageLabel;
        private Text _roundLabel;
        private UIButton _speedButton;
        private UIButton _skipButton;
        private RectTransform _barLayer;
        private RectTransform _textLayer;

        private RectTransform _banner;
        private CanvasGroup _bannerGroup;
        private Text _bannerTitle;
        private Text _bannerSub;

        private RectTransform _resultPanel;
        private CanvasGroup _resultGroup;
        private Text _resultTitle;
        private Text _resultSub;
        private RectTransform _rewardList;
        private Text _debriefText;
        private UIButton _resultPrimary;
        private UIButton _resultSecondary;

        private UIButton[] _statTabs;
        private int _statTabActive;
        private RectTransform _statsPanel;
        private readonly Dictionary<int, int> _statsDealt = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _statsHealed = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _statsTaken = new Dictionary<int, int>();

        private readonly Dictionary<int, UnitBar> _bars = new Dictionary<int, UnitBar>();
        private readonly List<FloatText> _floatPool = new List<FloatText>();

        private RectTransform _turnStrip;
        private readonly List<TurnStripIcon> _turnStripIcons = new List<TurnStripIcon>();

        /// <summary>Index of the strip icon whose turn is being played. See AdvanceTurnStrip.</summary>
        private int _turnStripCursor;
        private UnitBar _activeTurnBar;

        /// <summary>-1 when this battle is a campaign fight (the only mode that existed before The
        /// Deep Recital); a floor number when it is a Recital attempt. Drives ShowResult's branch
        /// between GameSession.GrantBattleResult and GrantRecitalResult - the two reward models are
        /// different enough (repeat cap and stage unlocks vs. a one-way floor ladder and Echoes)
        /// that sharing one result path would mean one of them lying about what happened.</summary>
        private int _recitalFloor = -1;

        public void SetStage(StageDef stage)
        {
            _stage = stage;
            _recitalFloor = -1;
        }

        /// <summary>
        /// Launches this screen as a Recital attempt rather than a campaign fight. Spends nothing
        /// itself - OnShow is the one place an Echo is actually deducted (see the
        /// TryStartRecitalAttempt call there), because OnShow runs for both this initial entry AND
        /// every Rematch, and an attempt has to cost an Echo either way. RecitalScreen's FIGHT
        /// button only peeks affordability before calling this, it does not spend.
        /// </summary>
        public void SetRecitalFloor(int floor)
        {
            _recitalFloor = floor;
            _stage = App.Session.BuildRecitalStage(floor);
        }

        // ------------------------------------------------------------------ construction

        protected override void Build()
        {
            _barLayer = UIBuilder.Node("Bars", Root);
            UIBuilder.FillParent(_barLayer, 0f);

            _textLayer = UIBuilder.Node("FloatingText", Root);
            UIBuilder.FillParent(_textLayer, 0f);

            BuildTopHud();
            BuildTurnStrip();
            BuildBanner();

            // Floating numbers were being created before the banner and so drawing UNDERNEATH it,
            // which is the worse half of the collision the banner causes: a number that overlaps
            // the banner is survivable, a number hidden behind it is a lost event. uGUI paints in
            // sibling order, so lifting the text layer here puts every number on top of the
            // banner and the HUD while still leaving the result panel above everything.
            _textLayer.SetAsLastSibling();

            BuildResultPanel();
        }

        private void BuildTurnStrip()
        {
            _turnStrip = UIBuilder.Node("TurnStrip", Root);
            UIBuilder.Box(_turnStrip, 0f, -146f, 900f, 36f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_turnStrip, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        }

        // The heavy element here was never the text, it was the full-width black band behind it.
        // That band existed to make two short labels legible, which a text outline does for free,
        // and it cost a 112 tall opaque stripe across the top of the battlefield to do it. Panel
        // dropped, strip 112 -> 64, labels down a size each with the round counter dimmed to
        // TextDim, buttons 128x76 and 150x76 -> 84x44 and 100x44. Nothing is removed: the stage
        // name, the round counter, speed and skip are all still there, they just stop being the
        // brightest thing on screen.
        private void BuildTopHud()
        {
            RectTransform bar = UIBuilder.Node("TopHud", Root);
            UIBuilder.TopStrip(bar, 64f, 16f, UITheme.ScreenPadding);

            _stageLabel = UIBuilder.Label("Stage", bar, "", UITheme.FontTiny, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_stageLabel.rectTransform, 4f, -2f, 520f, 26f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_stageLabel, UITheme.InkArt, 1.8f);

            _roundLabel = UIBuilder.Label("Round", bar, "", UITheme.FontTiny - 4, UITheme.TextDim, TextAnchor.LowerLeft);
            UIBuilder.Box(_roundLabel.rectTransform, 4f, 4f, 520f, 22f, new Vector2(0f, 0f));
            UIBuilder.Outlined(_roundLabel, UITheme.InkArt, 1.5f);

            _speedButton = UIBuilder.SecondaryButton("Speed", bar, "1x", UITheme.FontTiny);
            UIBuilder.Box(_speedButton.Root, -4f, 0f, 84f, 44f, new Vector2(1f, 0.5f));
            _speedButton.OnClick(CycleSpeed);

            _skipButton = UIBuilder.SecondaryButton("Skip", bar, "SKIP", UITheme.FontTiny);
            UIBuilder.Box(_skipButton.Root, -96f, 0f, 100f, 44f, new Vector2(1f, 0.5f));
            _skipButton.OnClick(SkipToEnd);
        }

        // 980 x 132 at 0.62 alpha was close to a third of the screen's width in solid black, put
        // over the enemy line every time anything cast. 520 x 74 at 0.34, with the title down from
        // FontTitle to FontBody, keeps the same job (name the ability, say who cast it) at about a
        // quarter of the area and half the opacity.
        //
        // Also raised, 250 -> 300 above centre. That is not styling: at 250 the band sat at screen
        // y 724 to 856, which is exactly where a damage number from a front-rank unit climbs, and
        // the Sundered Colossus's numbers were measured crossing it. At 300 the band is 803 to 877
        // and no unit's float text reaches it, while still clearing the turn strip at 916.
        private void BuildBanner()
        {
            _banner = UIBuilder.Node("Banner", Root);
            UIBuilder.Box(_banner, 0f, 300f, 520f, 74f, new Vector2(0.5f, 0.5f));
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;

            Image backing = _banner.gameObject.AddComponent<Image>();
            backing.sprite = TextureLab.RoundedRect(14);
            backing.type = Image.Type.Sliced;
            backing.color = UITheme.ScrimBacking;
            backing.raycastTarget = false;

            _bannerTitle = UIBuilder.Label("Title", _banner, "", UITheme.FontBody, UITheme.Gold, TextAnchor.UpperCenter);
            UIBuilder.Box(_bannerTitle.rectTransform, 0f, -8f, 500f, 34f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_bannerTitle, UITheme.InkArt, 1.8f);

            _bannerSub = UIBuilder.Label("Sub", _banner, "", UITheme.FontTiny - 2, UITheme.TextMuted, TextAnchor.LowerCenter);
            UIBuilder.Box(_bannerSub.rectTransform, 0f, 8f, 500f, 24f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_bannerSub, UITheme.InkArt, 1.5f);

            _banner.gameObject.SetActive(false);
        }

        private void BuildResultPanel()
        {
            _resultPanel = UIBuilder.Node("Result", Root);
            UIBuilder.FillParent(_resultPanel, 0f);
            _resultGroup = _resultPanel.gameObject.AddComponent<CanvasGroup>();

            Image dim = _resultPanel.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimResult;

            // Landscape splits the panel into two panes: outcome, cause and actions on the left,
            // the spoils card on the right. The portrait layout stacked these down the middle and
            // mixed centre-anchored and bottom-anchored elements, which collide at 1080 of height.
            _resultTitle = UIBuilder.Label("Title", _resultPanel, "", UITheme.FontDisplay + 16, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultTitle.rectTransform, -460f, 310f, 900f, 110f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_resultTitle, new Color(0.2f, 0.05f, 0f, 0.95f), 4f);

            _resultSub = UIBuilder.Label("Sub", _resultPanel, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultSub.rectTransform, -460f, 232f, 860f, 40f, new Vector2(0.5f, 0.5f));

            RectTransform card = UIBuilder.Node("Rewards", _resultPanel);
            UIBuilder.Box(card, 460f, 0f, 820f, 560f, new Vector2(0.5f, 0.5f));
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = TextureLab.RoundedRect(24);
            cardImage.type = Image.Type.Sliced;
            cardImage.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.Border, 24, 2);

            // Rewards — always visible, top section of card.
            Text spoilsHeading = UIBuilder.Label("Heading", card, "SPOILS", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(spoilsHeading.rectTransform, 0f, -6f, 400f, 26f, new Vector2(0.5f, 1f));

            _rewardList = UIBuilder.Node("List", card);
            UIBuilder.Box(_rewardList, 0f, -38f, 764f, 208f, new Vector2(0.5f, 1f));
            UIBuilder.VStack(_rewardList, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            // Divider between rewards and stat breakdown.
            RectTransform divider = UIBuilder.Node("Divider", card);
            UIBuilder.Box(divider, 0f, -252f, 760f, 1f, new Vector2(0.5f, 1f));
            Image dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.sprite = TextureLab.White;
            dividerImage.color = UITheme.Border;

            // Stat tab strip — 3 tabs: DEALT / HEALED / TAKEN. Rewards are never behind a tab.
            RectTransform tabStrip = UIBuilder.Node("Tabs", card);
            UIBuilder.Box(tabStrip, 0f, -261f, 780f, 34f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(tabStrip, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            string[] tabNames = { "DEALT", "HEALED", "TAKEN" };
            _statTabs = new UIButton[tabNames.Length];
            for (int ti = 0; ti < tabNames.Length; ti++)
            {
                int idx = ti;
                UIButton tab = UIBuilder.SecondaryButton("Tab_" + tabNames[ti], tabStrip, tabNames[ti], UITheme.FontTiny - 2);
                UIBuilder.Sized(tab.Root, 256f, 30f);
                tab.OnClick(() => SelectStatTab(idx));
                _statTabs[ti] = tab;
            }

            _statsPanel = UIBuilder.Node("Stats", card);
            UIBuilder.Box(_statsPanel, 0f, -303f, 764f, 229f, new Vector2(0.5f, 1f));
            UIBuilder.VStack(_statsPanel, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            // the Debrief line - one sentence, the single biggest reason the fight went this way -
            // sits directly under the outcome it explains, in the left pane's reading order:
            // verdict, cause, then what to do about it (the two buttons below)
            _debriefText = UIBuilder.WrappedLabel("Debrief", _resultPanel, "", UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(_debriefText.rectTransform, -460f, 40f, 820f, 220f, new Vector2(0.5f, 0.5f));

            _resultSecondary = UIBuilder.SecondaryButton("AdjustLine", _resultPanel, "ADJUST LINE", UITheme.FontBody);
            UIBuilder.Box(_resultSecondary.Root, -460f, -220f, 620f, 112f, new Vector2(0.5f, 0.5f));
            _resultSecondary.OnClick(AdjustLine);

            _resultPrimary = UIBuilder.GoldButton("Rematch", _resultPanel, "REMATCH", UITheme.FontHeading);
            UIBuilder.Box(_resultPrimary.Root, -460f, -370f, 620f, 126f, new Vector2(0.5f, 0.5f));
            _resultPrimary.OnClick(Replay);

            _resultPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnShow()
        {
            App.SetBackgroundVisible(false);
            _finished = false;
            _resultPanel.gameObject.SetActive(false);
            _banner.gameObject.SetActive(false);
            _speed = 1f;
            _speedButton.SetText("1x");
            _skipButton.Interactable = true;

            if (_recitalFloor < 0 && _stage == null) _stage = App.Session.NextStage;
            if (_stage == null)
            {
                // no campaign content: bail out next frame rather than re-entering the router
                App.Notify("No campaign stages are loaded.");
                StartCoroutine(LeaveNextFrame());
                return;
            }

            if (_recitalFloor >= 0)
            {
                // The one and only spend point for a Recital attempt - covers both the first entry
                // (RecitalScreen's FIGHT button only peeked affordability, it did not spend) and
                // every Rematch (which calls OnShow again with no gate of its own otherwise). If
                // Echoes ran out between then and now, bail back to the Recital screen instead of
                // fighting a floor that was not paid for.
                if (!App.Session.TryStartRecitalAttempt(TimeUtil.NowUnix()))
                {
                    App.Notify("Out of Echoes.");
                    Router.Show(ScreenId.Recital, false);
                    return;
                }
                _stageLabel.text = "FLOOR " + _recitalFloor + "   " + _stage.Name.ToUpperInvariant();
            }
            else
            {
                _stageLabel.text = _stage.DisplayCode + "   " + _stage.Name.ToUpperInvariant();
            }
            _roundLabel.text = "PREPARING";

            BuildWorld();
            RunSimulation();

            if (_playback != null) StopCoroutine(_playback);
            _playback = StartCoroutine(Playback());
        }

        public override void OnHide()
        {
            // stops playback plus every death, drain and floating-text routine, so nothing
            // touches the world objects that are about to be destroyed
            StopAllCoroutines();
            _playback = null;
            _bannerRoutine = null;
            ResetFloatingText();

            App.SetBackgroundVisible(true);
            ClearBars();
            ClearTurnStrip();
            _activeTurnBar = null;
            if (_world != null)
            {
                _world.Teardown();
                Destroy(_world.gameObject);
                _world = null;
            }
        }

        private IEnumerator LeaveNextFrame()
        {
            yield return null;
            Router.Show(ScreenId.MainMenu, false);
        }

        private void BuildWorld()
        {
            if (_world == null)
            {
                GameObject host = new GameObject("BattleWorld");
                _world = host.AddComponent<BattleStage>();
                _world.Initialise();
            }
            _world.BuildWorld(_stage.Environment);
        }

        private void RunSimulation()
        {
            int seed = Mathf.Abs(_stage.Id.GetHashCode() * 31
                                 + App.Session.Profile.TotalBattlesWon * 7919
                                 + Mathf.RoundToInt(Time.unscaledTime * 997f));
            if (seed == 0) seed = 1;

            BattleRequest request = App.Session.BuildBattleRequest(_stage, seed);
            _result = CombatSimulation.Run(request);
            _world.Populate(_result);
            BuildBars();
        }

        // ------------------------------------------------------------------ unit bars

        private void ClearBars()
        {
            foreach (KeyValuePair<int, UnitBar> pair in _bars)
                if (pair.Value.Root != null) Destroy(pair.Value.Root.gameObject);
            _bars.Clear();
        }

        private void BuildBars()
        {
            ClearBars();
            for (int i = 0; i < _result.Units.Count; i++)
            {
                UnitSnapshot snapshot = _result.Units[i];
                _bars[snapshot.Id] = CreateBar(snapshot);
            }
        }

        private UnitBar CreateBar(UnitSnapshot snapshot)
        {
            UnitBar bar = new UnitBar();
            bar.Root = UIBuilder.Node("Bar_" + snapshot.Name, _barLayer);
            bar.Root.anchorMin = Vector2.zero;
            bar.Root.anchorMax = Vector2.zero;
            bar.Root.pivot = new Vector2(0.5f, 0f);
            UIBuilder.Size(bar.Root, BarWidth + 40f, BarStackHeight);
            bar.Group = bar.Root.gameObject.AddComponent<CanvasGroup>();

            Color tint = snapshot.IsPlayer ? UITheme.HealthAlly : UITheme.HealthEnemy;

            // Rebuilt as a small tag rather than a slab, and rebuilt bottom-up rather than
            // re-tuned. The root's pivot is its BOTTOM edge and LateUpdate places that bottom edge
            // BarWorldRise above the unit's head, so whatever sits at local y 0 is what the player
            // reads as "the gap". Previously that was empty space: the stack ran energy at y 16,
            // ward at 27, health at 36, name at 58, status at 84, turn pip at 108, so the health
            // bar was 36 canvas units above an anchor that was itself 0.28 world units up, and the
            // gap that actually rendered was 0.28 + 36/pixelsPerWorldUnit, more than double the
            // number anyone was reading. The health bar now sits AT y 0, so the rise means what it
            // says and no constant had to be fudged to get there.
            //
            // 200 x 18 -> 120 x 7, and the label fonts drop with it.
            bar.HpGhost = UIBuilder.Bar("HpGhost", bar.Root, new Color(0f, 0f, 0f, 0.8f), new Color(1f, 1f, 1f, 0.5f));
            UIBuilder.Box(bar.HpGhost.rectTransform.parent as RectTransform, 0f, 0f, BarWidth, BarHeight, new Vector2(0.5f, 0f));

            bar.HpFill = UIBuilder.Bar("Hp", bar.Root, new Color(0f, 0f, 0f, 0f), tint);
            UIBuilder.Box(bar.HpFill.rectTransform.parent as RectTransform, 0f, 0f, BarWidth, BarHeight, new Vector2(0.5f, 0f));

            // Ward is no longer its own strip. It occupies the SAME rectangle as health and draws
            // over it, so the unit still reads as one bar while a warded unit is still obviously
            // warded. Kept rather than cut because a ward changes whether an incoming number
            // matters at all, which is the one thing on this tag the player cannot infer from the
            // health fill alone.
            bar.ShieldFill = UIBuilder.Bar("Shield", bar.Root, new Color(0f, 0f, 0f, 0f), UITheme.WithAlpha(UITheme.Shield, 0.95f));
            UIBuilder.Box(bar.ShieldFill.rectTransform.parent as RectTransform, 0f, 0f, BarWidth, BarHeight, new Vector2(0.5f, 0f));
            UIBuilder.SetBar(bar.ShieldFill, 0f);

            // Energy sits directly on top of health, thinner and the same width, so the tag still
            // reads as one object. Above rather than below because below would put it between the
            // head and the health bar, spending the deliberately small BarWorldRise gap on the
            // subordinate readout.
            bar.EnergyFill = UIBuilder.Bar("Energy", bar.Root, new Color(0f, 0f, 0f, 0.7f), UITheme.Energy);
            UIBuilder.Box(bar.EnergyFill.rectTransform.parent as RectTransform,
                          0f, BarHeight + 1f, BarWidth, EnergyHeight, new Vector2(0.5f, 0f));
            SetEnergy(bar, snapshot.EnergyMax > 0 ? snapshot.StartEnergy / (float)snapshot.EnergyMax : 0f);

            // Name only. The level number that used to trail it is gone: it never changes during a
            // battle, so it was permanent clutter on a tag that has to be readable in a glance.
            bar.Name = UIBuilder.Label("Name", bar.Root, snapshot.Name,
                                       UITheme.FontTiny - 5, UITheme.TextPrimary, TextAnchor.LowerCenter);
            UIBuilder.Box(bar.Name.rectTransform, 0f, BarHeight + EnergyHeight + 3f, 240f, 18f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(bar.Name, UITheme.InkArt, 1.4f);

            // Status sits above the name and is empty unless the unit actually carries one, so it
            // costs nothing visually at rest. Kept against a literal reading of "nothing else"
            // because DESIGN-BATTLE-READABILITY.md names it as load-bearing: without it there is
            // no way to see that a unit is burning, taunting or warded down.
            bar.Status = UIBuilder.Label("Status", bar.Root, "", UITheme.FontTiny - 7, UITheme.Warning, TextAnchor.LowerCenter);
            UIBuilder.Box(bar.Status.rectTransform, 0f, BarHeight + EnergyHeight + 21f, 240f, 15f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(bar.Status, UITheme.InkArt, 1.2f);

            // Turn pip moved from above the whole stack to a dot beside the bar, which is what let
            // the stack lose 60 units of height. Starts transparent; TurnCue lights it.
            bar.TurnPip = UIBuilder.Panel("TurnPip", bar.Root, new Color(1f, 1f, 1f, 0f), 5);
            UIBuilder.Box(bar.TurnPip.rectTransform, -(BarWidth * 0.5f + 9f), -1f, 10f, 10f, new Vector2(0.5f, 0f));

            return bar;
        }

        /// <summary>
        /// Fills the energy meter and lights it gold at full. The colour change is the point of
        /// the meter, not decoration on it: in a fight the player cannot influence, "this unit
        /// casts on its next turn" is the only thing worth anticipating, and a fill level read
        /// across five tags at once is much harder to judge than a colour that either is or is
        /// not gold.
        /// </summary>
        private static void SetEnergy(UnitBar bar, float fraction)
        {
            if (bar == null || bar.EnergyFill == null) return;
            float k = Mathf.Clamp01(fraction);
            UIBuilder.SetBar(bar.EnergyFill, k);
            bar.EnergyFill.color = k >= 0.999f ? UITheme.Gold : UITheme.Energy;
        }

        // How far above a unit's head its bar floats, in screen pixels rather than world units.
        // A world offset (the old approach: HeadPosition + up * 0.28) is fragile - the same
        // world gap projects to a different pixel gap whenever the camera's pitch or distance
        // changes, so it needs retuning every time BattleStage's camera does. Projecting the
        // head first and adding the offset after, in pixels, makes the bar's on-screen rise
        // constant regardless of how the battle camera is framed. This mechanism itself did not
        // need to change for the perspective-to-orthographic switch: WorldToScreenPoint resolves
        // correctly under either projection mode, and a pixel offset added after projection has
        // no notion of perspective to begin with.
        //
        // What the gap should be, in world units. The pixel figure is computed per frame below
        // instead of being baked in, because baking it in is exactly how it went wrong: the
        // constant here used to be 58 px, derived - correctly, at the time - against an
        // orthographicSize of 2.6, giving 1080 / 5.2 = 207.7 px per world unit. BattleStage has
        // since reframed three times and is now at 4.25, which is 1080 / 8.5 = 127.1 px per world
        // unit, so those 58 px had quietly become 0.46 world units, two thirds again the gap
        // intended. Reading orthographicSize off the live camera cannot go stale that way.
        // 0.28 -> 0.12. The anchor bug that made this render as roughly double its value is fixed,
        // so this is now just a preference, set to taste rather than to correct anything: 0.12
        // world units is about 18 px at 1080p and orthographicSize 3.65. Checked against putting
        // tags on the heads themselves before choosing it, and there is a lot of room left. The
        // tag's own bottom edge is what lands here, and the nearest thing above any unit is the
        // next rank's feet, which for the tightest pair (back rank slot 3 under slot 4) sits about
        // 120 px above the head. So anything up to roughly 0.5 world would still clear; 0.12 is a
        // deliberate choice well inside that, not a limit.
        private const float BarWorldRise = 0.12f;

        private const float BarWidth = 120f;
        private const float BarHeight = 7f;

        /// <summary>Energy meter height. Deliberately thinner than the health bar it sits on.</summary>
        private const float EnergyHeight = 4f;

        /// <summary>How far a damage or heal number climbs over its life, in canvas units.</summary>
        private const float FloatRise = 120f;

        // Full height of the tag above its own anchor, in canvas units: bar 7, name to 27, status
        // to 44. Was 110. Used to size the root and to keep the tag on screen, see the clamp in
        // LateUpdate.
        //
        // The energy meter is back, and the reasoning that removed it was wrong. The argument was
        // that nothing the player DOES depends on it, since the fight is resolved before the first
        // frame and the screen offers only speed and skip. That is true and beside the point:
        // being unable to act is exactly what makes knowing what is coming valuable. Energy is the
        // only forward-looking quantity on screen, the one thing that says an ultimate is one turn
        // away rather than reporting damage already dealt. DESIGN-BATTLE-READABILITY.md step 7
        // had it right.
        //
        // Restored at the new stack's scale rather than at its old proportions: 200 x 9 under a
        // 200 x 18 health bar became 120 x 4 over a 120 x 7 one, so it reads as a subordinate
        // strip on the same tag rather than a second slab. It also lights to gold at full, since
        // "can cast now" is the state worth seeing across the field, not the fill level.
        private const float BarStackHeight = 48f;

        private void LateUpdate()
        {
            if (_world == null || _world.Camera == null || Root == null || !Root.gameObject.activeInHierarchy) return;
            float scale = App.RootCanvas != null ? App.RootCanvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            // Orthographic gives one pixel scale for the whole field, independent of depth, so
            // this is exact rather than an average across near and far units.
            float pixelsPerWorldUnit = Screen.height / (2f * Mathf.Max(0.01f, _world.Camera.orthographicSize));
            float rise = BarWorldRise * pixelsPerWorldUnit / scale;

            // The back rank's far-z slot projects a head to about 97% of screen height, so there
            // is genuinely no room above it for a 110-unit stack; without this the whole bar left
            // the screen and read as a stray bar stuck to the top edge belonging to nobody.
            // Clamped it overlaps its own unit instead, which is legible and still attached.
            float ceiling = Screen.height / scale - BarStackHeight - 6f;

            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor actor = _world.Actors[i];
                UnitBar bar;
                if (!_bars.TryGetValue(actor.Snapshot.Id, out bar)) continue;

                Vector3 screen = _world.Camera.WorldToScreenPoint(actor.HeadPosition);
                bool visible = screen.z > 0f && actor.Alive;
                bar.Group.alpha = visible ? 1f : Mathf.Max(0f, bar.Group.alpha - Time.deltaTime * 4f);
                if (!visible) continue;

                float y = screen.y / scale + rise + bar.TurnLift;
                bar.Root.anchoredPosition = new Vector2(screen.x / scale, Mathf.Min(y, ceiling));
            }
        }

        // ------------------------------------------------------------------ playback

        private IEnumerator Playback()
        {
            yield return Wait(0.35f);

            for (int i = 0; i < _result.Events.Count; i++)
            {
                yield return Handle(i);
            }

            yield return Wait(0.45f);
            ShowResult();
        }

        private IEnumerator Handle(int index)
        {
            if (_pendingAnimationTail > 0f)
            {
                float tail = _pendingAnimationTail;
                _pendingAnimationTail = 0f;
                yield return Wait(tail);
            }

            BattleEvent e = _result.Events[index];
            switch (e.Type)
            {
                case BattleEventType.RoundStart:
                    _roundLabel.text = "ROUND " + e.Value + " / " + App.Session.Balance.MaxRounds;
                    RebuildTurnStrip(index);
                    yield return Wait(0.10f);
                    break;

                case BattleEventType.TurnStart:
                {
                    UnitBar bar = BarOf(e.SourceId);
                    // TurnPip is checked, not just the wrapper: ClearBars destroys the bar's
                    // GameObject but leaves this UnitBar instance alive, so a non-null wrapper can
                    // still hold a destroyed Image. SettleTurnBar dereferences TurnPip directly.
                    if (_activeTurnBar != null && _activeTurnBar != bar && _activeTurnBar.TurnPip != null)
                        SettleTurnBar(_activeTurnBar);
                    _activeTurnBar = bar;
                    AdvanceTurnStrip(e.SourceId);
                    if (bar != null) yield return TurnCue(bar);
                    break;
                }

                case BattleEventType.BasicAttack:
                {
                    UnitActor source = ActorOf(e.SourceId);
                    List<int> targetIds = LookaheadTargets(index);
                    UnitActor focus = targetIds.Count > 0 ? ActorOf(targetIds[0]) : null;
                    Vector3 towards = focus != null ? focus.transform.position : (source != null ? source.transform.position : Vector3.zero);

                    // How far this unit travels to make the attack. Melee closes on its target;
                    // a bow, spellbook, orb or staff attacks from where it stands, which is the
                    // whole point of carrying one. Applies to both timing branches below and to
                    // enemies as much as heroes - a staff-carrying caster should not walk its
                    // spell over either. See UnitRig.AttacksAtRange for why this reads the weapon
                    // rather than the role.
                    float approach = source != null && UnitRig.AttacksAtRange(source.Snapshot.Visual.Weapon)
                        ? 0f : UnitActor.ApproachDistance;

                    // A unit with a "basic" clip syncs the lunge and the following Damage event to
                    // ImpactFraction instead of the fixed 0.16s snap below: out to reach the target
                    // when the clip says the strike connects, landing the hit VFX and (by returning
                    // from Handle right there) the very next BattleEvent - Damage, resolving the
                    // actual hit - at that same moment. The return trip is deferred to
                    // _pendingAnimationTail so it still finishes before anything else advances,
                    // without delaying Damage itself. Zero for a unit with no such clip: falls
                    // through to the exact original fixed-timing lunge, unchanged.
                    float clipDuration = source != null && source.Rig != null && source.Rig.Animation != null
                        ? source.Rig.Animation.ClipDuration("basic") : 0f;
                    if (source != null && clipDuration > 0f)
                    {
                        float impact = clipDuration * source.Rig.Animation.ImpactFraction("basic");
                        float outSeconds = Mathf.Clamp(impact, 0.02f, clipDuration);
                        float backSeconds = clipDuration - outSeconds;
                        source.PlayAttackLunge(towards, outSeconds, backSeconds, _speed, approach);
                        yield return Wait(outSeconds);
                        for (int t = 0; t < targetIds.Count; t++)
                        {
                            UnitActor hit = ActorOf(targetIds[t]);
                            if (hit != null) PlayAbilityVfx(e.Vfx, source, hit, AspectTint(source));
                        }
                        _pendingAnimationTail = backSeconds;
                    }
                    else
                    {
                        if (source != null) source.PlayAttackLunge(towards, _speed, approach);
                        yield return Wait(0.16f);
                        if (source != null)
                        {
                            for (int t = 0; t < targetIds.Count; t++)
                            {
                                UnitActor hit = ActorOf(targetIds[t]);
                                if (hit != null) PlayAbilityVfx(e.Vfx, source, hit, AspectTint(source));
                            }
                        }
                    }
                    break;
                }

                case BattleEventType.AbilityCast:
                {
                    UnitActor source = ActorOf(e.SourceId);
                    List<int> targetIds = LookaheadTargets(index);
                    if (source != null)
                    {
                        source.PlayCast(_speed);
                        App.Audio.Cast();
                        ShowBanner(e.AbilityName, source.Snapshot.Name);
                        _world.Vfx.Aura(source.transform.position, AspectTint(source), 2.2f);
                    }

                    // Kael's Breaking Point hits AllEnemies, so there is no single target to lunge
                    // toward - only the "when does it land" half of the choreography applies here.
                    // A unit with a "skill" clip syncs the strike VFX (and, by returning from
                    // Handle right there, the following Damage events) to ImpactFraction instead of
                    // the fixed 0.36s/0.12s below; the clip's remaining follow-through is deferred
                    // to _pendingAnimationTail. Zero for a unit with no such clip: falls through to
                    // the exact original fixed timing, unchanged.
                    float clipDuration = source != null && source.Rig != null && source.Rig.Animation != null
                        ? source.Rig.Animation.ClipDuration("skill") : 0f;
                    if (source != null && clipDuration > 0f)
                    {
                        float impact = clipDuration * source.Rig.Animation.ImpactFraction("skill");
                        float impactSeconds = Mathf.Clamp(impact, 0.02f, clipDuration);
                        yield return Wait(impactSeconds);
                        PlayTargetedVfx(e.Vfx, source, targetIds, AspectTint(source));
                        _pendingAnimationTail = clipDuration - impactSeconds;
                    }
                    else
                    {
                        yield return Wait(0.36f);
                        if (source != null) PlayTargetedVfx(e.Vfx, source, targetIds, AspectTint(source));
                        yield return Wait(0.12f);
                    }
                    break;
                }

                case BattleEventType.PassiveTrigger:
                {
                    UnitActor source = ActorOf(e.SourceId);
                    if (source != null)
                    {
                        _world.Vfx.Aura(source.transform.position, UITheme.Gold, 2f);
                        ShowBanner(e.AbilityName, source.Snapshot.Name + "  -  PASSIVE");
                    }
                    yield return Wait(0.18f);
                    break;
                }

                case BattleEventType.Damage:
                {
                    UnitActor target = ActorOf(e.TargetId);
                    UnitActor source = ActorOf(e.SourceId);
                    if (target != null)
                    {
                        // impact: flash, shake, sound - immediate
                        target.PlayHit(e.Critical);
                        if (source != null && e.Status == StatusKind.None) _world.Vfx.Impact(target.CenterPosition, AspectTint(source), e.Critical ? 1.3f : 0.9f);
                        if (e.Critical) { App.Audio.Crit(); _world.Shake(0.14f, 0.2f); }
                        else App.Audio.Hit();

                        yield return null; // number floats one frame after impact, never simultaneously

                        // number: value, crit prefix, aspect tag
                        Color colour = e.Critical ? UITheme.Warning : Color.white;
                        if (e.Status == StatusKind.Burn) colour = UITheme.Ember;
                        string text = (e.Critical ? "CRIT " : "") + e.Value + AspectTag(e.AspectFactor);
                        SpawnFloatingText(target.HeadPosition, text, colour, e.Critical ? 1.5f : 1f);

                        // effect, last: bar drains to the new value, ghost bar trails behind
                        target.DisplayHp = Mathf.Max(0f, target.DisplayHp - e.Value);
                        UpdateHp(target, e.Critical);
                    }
                    yield return Wait(e.Critical ? 0.14f : 0.10f);
                    break;
                }

                case BattleEventType.Heal:
                {
                    UnitActor target = ActorOf(e.TargetId);
                    if (target != null)
                    {
                        target.DisplayHp = Mathf.Min(target.MaxHp, target.DisplayHp + e.Value);
                        UpdateHp(target, false);
                        SpawnFloatingText(target.HeadPosition, "+" + e.Value, UITheme.Success, 1f);
                        _world.Vfx.Motes(target.transform.position, UITheme.Success, 6, 2.2f);
                        App.Audio.Heal();
                    }
                    yield return Wait(0.08f);
                    break;
                }

                case BattleEventType.ShieldGain:
                {
                    UnitActor target = ActorOf(e.TargetId);
                    if (target != null)
                    {
                        target.DisplayShield += e.Value;
                        UpdateHp(target, false);
                        SpawnFloatingText(target.HeadPosition, "+" + e.Value + " ward", UITheme.Shield, 0.9f);
                        _world.Vfx.Aura(target.transform.position, UITheme.Shield, 2f);
                    }
                    yield return Wait(0.06f);
                    break;
                }

                case BattleEventType.ShieldAbsorb:
                {
                    UnitActor target = ActorOf(e.TargetId);
                    if (target != null)
                    {
                        target.DisplayShield = Mathf.Max(0f, target.DisplayShield - e.Value);
                        UpdateHp(target, false);
                        SpawnFloatingText(target.HeadPosition, e.Value.ToString(), UITheme.Shield, 0.85f);
                    }
                    yield return Wait(0.05f);
                    break;
                }

                case BattleEventType.StatusApplied:
                {
                    UnitBar bar = BarOf(e.TargetId);
                    UnitActor target = ActorOf(e.TargetId);
                    if (bar != null) SyncStatusCount(bar, e.Status, e.Value);
                    if (target != null)
                    {
                        Color tint = StatusInfo.IsHarmful(e.Status) ? UITheme.Danger : UITheme.Success;
                        _world.Vfx.Aura(target.transform.position, tint, 1.8f);
                    }
                    yield return Wait(0.05f);
                    break;
                }

                case BattleEventType.StatusResisted:
                {
                    // The whole reason this event exists: without a visible beat here, a resisted
                    // debuff is indistinguishable from an ability that did nothing, and the player
                    // has no way to learn that a target is resistant. Named, so the record is
                    // readable rather than merely non-empty - "RESIST" alone would not say what
                    // failed. Shield blue rather than Danger red, because nothing bad happened to
                    // the unit this text sits over; the affliction is what failed.
                    UnitActor target = ActorOf(e.TargetId);
                    if (target != null)
                    {
                        SpawnFloatingText(target.HeadPosition,
                                          StatusInfo.DisplayName(e.Status) + " resisted",
                                          UITheme.Shield, 0.8f);
                    }
                    yield return Wait(0.05f);
                    break;
                }

                case BattleEventType.StatusExpired:
                {
                    UnitBar bar = BarOf(e.TargetId);
                    if (bar != null && bar.Statuses.Remove(e.Status)) RefreshStatusText(bar);
                    break;
                }

                case BattleEventType.StatusSpread:
                {
                    // Umbra (DESIGN-ASPECTS section 8). Named in the float text rather than shown as
                    // a bare tag appearing: a debuff arriving on a unit nobody attacked, during
                    // someone else's death animation, is exactly the kind of thing the player reads
                    // as a bug unless the screen says out loud that it moved.
                    UnitBar bar = BarOf(e.TargetId);
                    UnitActor heir = ActorOf(e.TargetId);
                    if (bar != null) SyncStatusCount(bar, e.Status, 0);
                    if (heir != null)
                    {
                        SpawnFloatingText(heir.HeadPosition,
                                          StatusInfo.DisplayName(e.Status) + " spreads",
                                          UITheme.Danger, 0.8f);
                        _world.Vfx.Aura(heir.transform.position, UITheme.Danger, 1.8f);
                    }
                    yield return Wait(0.12f);
                    break;
                }

                case BattleEventType.EnergyChanged:
                {
                    UnitActor actor = ActorOf(e.SourceId);
                    UnitBar bar = BarOf(e.SourceId);
                    if (actor != null)
                    {
                        actor.Energy = e.Value;
                        float max = actor.Snapshot.EnergyMax > 0 ? actor.Snapshot.EnergyMax : 100f;
                        SetEnergy(bar, e.Value / max);
                    }
                    break;
                }

                case BattleEventType.Death:
                {
                    UnitActor target = ActorOf(e.TargetId);
                    if (target != null)
                    {
                        target.DisplayHp = 0f;
                        UpdateHp(target, false);
                        App.Audio.Death();
                        _world.Vfx.Burst(target.transform.position + Vector3.up * 0.4f, AspectTint(target) * 0.8f, 0.9f);
                        // run on the actor so the routine dies with it
                        target.StartCoroutine(target.PlayDeath(_speed));
                    }
                    yield return Wait(0.28f);
                    // A unit with a death clip (UnitActor.DeathHoldNominalSeconds) needs the timeline
                    // to hold past the existing 0.28s or the next BattleEvent starts while it is still
                    // mid-clip - the exact bug this exists to fix. Zero, and this Wait is skipped
                    // entirely, for every unit with no clip - the other six heroes and every enemy.
                    if (target != null)
                    {
                        float extra = Mathf.Max(0f, target.DeathHoldNominalSeconds - 0.28f);
                        if (extra > 0f) yield return Wait(extra);
                    }
                    break;
                }

                case BattleEventType.BattleEnd:
                    yield return Wait(0.25f);
                    break;
            }
        }

        private IEnumerator Wait(float seconds)
        {
            float remaining = seconds / _speed;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ presentation helpers

        private UnitActor ActorOf(int id)
        {
            return _world != null ? _world.Find(id) : null;
        }

        private UnitBar BarOf(int id)
        {
            UnitBar bar;
            return _bars.TryGetValue(id, out bar) ? bar : null;
        }

        // ------------------------------------------------------------------ turn cue / strip

        /// <summary>Turn pip lights and the bar rises over 0.12s - the "cause" beat before intent/motion/impact.</summary>
        private IEnumerator TurnCue(UnitBar bar)
        {
            float duration = Mathf.Max(0.05f, 0.12f / _speed);
            Color lit = UITheme.Gold;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                bar.TurnPip.color = UITheme.WithAlpha(lit, k);
                bar.TurnLift = Mathf.Lerp(0f, 14f, k);
                yield return null;
            }
            bar.TurnPip.color = lit;
            bar.TurnLift = 14f;
        }

        /// <summary>Drops a bar out of "acting now" state - called on the previous actor once a new TurnStart fires.</summary>
        private void SettleTurnBar(UnitBar bar)
        {
            bar.TurnLift = 0f;
            Color c = bar.TurnPip.color;
            bar.TurnPip.color = UITheme.WithAlpha(c, 0f);
        }

        /// <summary>Repopulates the turn strip from the TurnStart events between this RoundStart and the next.</summary>
        private void RebuildTurnStrip(int roundStartIndex)
        {
            ClearTurnStrip();
            for (int j = roundStartIndex + 1; j < _result.Events.Count; j++)
            {
                BattleEvent e = _result.Events[j];
                if (e.Type == BattleEventType.RoundStart) break;
                if (e.Type != BattleEventType.TurnStart) continue;

                UnitSnapshot snap = _result.FindUnit(e.SourceId);
                Color tint = snap != null && snap.IsPlayer ? UITheme.HealthAlly : UITheme.HealthEnemy;

                TurnStripIcon icon = new TurnStripIcon();
                icon.UnitId = e.SourceId;
                icon.Fill = UIBuilder.Panel("Turn_" + e.SourceId, _turnStrip, UITheme.WithAlpha(tint, 0.4f), 8);
                UIBuilder.Sized(icon.Fill.rectTransform, 30f, 30f);
                icon.Root = icon.Fill.rectTransform;
                _turnStripIcons.Add(icon);
            }
        }

        /// <summary>
        /// Full-alpha on the acting unit's icon, dimmed on everyone else in the strip.
        ///
        /// Walks a cursor rather than matching every icon with this unit id, because Gale's kill
        /// re-entry (DESIGN-ASPECTS section 1) puts a unit into one round's order more than once, so
        /// the strip legitimately holds two icons for the same unit. Matching on id alone lit both
        /// of them from the first turn and made the extra turn look like a duplicated frame instead
        /// of the second entry it is. The cursor advances to the next icon at or after its current
        /// position, which is exactly the sequence the strip was built from.
        /// </summary>
        private void AdvanceTurnStrip(int unitId)
        {
            for (int i = _turnStripCursor; i < _turnStripIcons.Count; i++)
            {
                if (_turnStripIcons[i].UnitId != unitId) continue;
                _turnStripCursor = i;
                break;
            }

            for (int i = 0; i < _turnStripIcons.Count; i++)
            {
                TurnStripIcon icon = _turnStripIcons[i];
                bool active = i == _turnStripCursor && icon.UnitId == unitId;
                Color c = icon.Fill.color;
                icon.Fill.color = UITheme.WithAlpha(c, active ? 1f : 0.4f);
            }
        }

        private void ClearTurnStrip()
        {
            for (int i = 0; i < _turnStripIcons.Count; i++)
                if (_turnStripIcons[i].Root != null) Destroy(_turnStripIcons[i].Root.gameObject);
            _turnStripIcons.Clear();
            _turnStripCursor = 0;
        }

        private Color AspectTint(UnitActor actor)
        {
            return actor != null ? UITheme.AspectColor(actor.Snapshot.Aspect) : UITheme.TextPrimary;
        }

        /// <summary>WEAK when the target's aspect took bonus damage, RESIST when it took less - the
        /// AspectChart.Advantage/Disadvantage swing, made readable. The bounds test either side of
        /// neutral rather than the chart's exact values, so retuning the wheel does not silence the
        /// tags; Debrief.AspectFactorThreshold matches this deliberately.</summary>
        private static string AspectTag(float aspectFactor)
        {
            if (aspectFactor > 1.01f) return "  WEAK";
            if (aspectFactor < 0.99f) return "  RESIST";
            return "";
        }

        /// <summary>
        /// The true target set for the BasicAttack/AbilityCast at events[index]: every distinct
        /// TargetId hit by a Damage, Heal or ShieldGain event before the next TurnStart. This is
        /// the actual resolved target (taunt redirects included), not a geometry guess - a unit
        /// using LowestHpEnemy, HighestAttackEnemy or RandomEnemy targeting shows the real pick,
        /// and an AllEnemies effect naturally yields every unit it actually hit.
        /// </summary>
        private List<int> LookaheadTargets(int index)
        {
            List<int> targets = new List<int>();
            for (int j = index + 1; j < _result.Events.Count; j++)
            {
                BattleEvent e = _result.Events[j];
                if (e.Type == BattleEventType.TurnStart) break;
                if (e.Type != BattleEventType.Damage && e.Type != BattleEventType.Heal && e.Type != BattleEventType.ShieldGain) continue;
                if (!targets.Contains(e.TargetId)) targets.Add(e.TargetId);
            }
            return targets;
        }

        private void PlayAbilityVfx(string vfx, UnitActor source, UnitActor target, Color colour)
        {
            switch (vfx)
            {
                case "bolt":
                    StartCoroutine(_world.Vfx.Bolt(source.CenterPosition, target.CenterPosition, colour));
                    break;
                case "shadow":
                    _world.Vfx.Implode(target.CenterPosition, colour);
                    break;
                case "slash":
                    _world.Vfx.Slash(source.CenterPosition, target.CenterPosition, colour);
                    break;
                default:
                    _world.Vfx.Slash(source.CenterPosition, target.CenterPosition, colour);
                    break;
            }
        }

        /// <summary>
        /// Plays the cast-impact vfx on the ability's real target set. Falls back to the old
        /// same-side sweep only when lookahead found nothing to key off - a pure buff/status
        /// ability (e.g. Maeve's Tidal Cadence: AttackUp + Focus + EnergyGain, no Damage/Heal/
        /// Shield effect) has no Damage/Heal/ShieldGain event to reveal its target set from.
        /// </summary>
        private void PlayTargetedVfx(string vfx, UnitActor source, List<int> targetIds, Color colour)
        {
            if (targetIds.Count > 0)
            {
                for (int i = 0; i < targetIds.Count; i++)
                {
                    UnitActor actor = ActorOf(targetIds[i]);
                    if (actor == null || !actor.Alive) continue;
                    PlayAreaVfxOn(vfx, actor, colour);
                }
            }
            else
            {
                bool againstEnemies = vfx != "heal" && vfx != "buff";
                for (int i = 0; i < _world.Actors.Count; i++)
                {
                    UnitActor actor = _world.Actors[i];
                    if (!actor.Alive) continue;
                    bool opposing = actor.Snapshot.IsPlayer != source.Snapshot.IsPlayer;
                    if (opposing != againstEnemies) continue;
                    PlayAreaVfxOn(vfx, actor, colour);
                }
            }

            if (vfx == "burst" || vfx == "shadow") _world.Shake(0.18f, 0.26f);
        }

        private void PlayAreaVfxOn(string vfx, UnitActor actor, Color colour)
        {
            switch (vfx)
            {
                case "burst": _world.Vfx.Burst(actor.transform.position, colour, 0.9f); break;
                case "shadow": _world.Vfx.Implode(actor.CenterPosition, colour); break;
                case "heal": _world.Vfx.Motes(actor.transform.position, UITheme.Success, 6, 2.2f); break;
                case "buff": _world.Vfx.Aura(actor.transform.position, colour, 2.2f); break;
                default: _world.Vfx.Impact(actor.CenterPosition, colour, 1f); break;
            }
        }

        private void UpdateHp(UnitActor actor, bool critical)
        {
            UnitBar bar = BarOf(actor.Snapshot.Id);
            if (bar == null) return;
            float ratio = actor.MaxHp > 0f ? actor.DisplayHp / actor.MaxHp : 0f;
            UIBuilder.SetBar(bar.HpFill, ratio);
            UIBuilder.SetBar(bar.ShieldFill, actor.MaxHp > 0f ? Mathf.Clamp01(actor.DisplayShield / actor.MaxHp) : 0f);
            StartCoroutine(DrainGhost(bar, ratio));
        }

        private IEnumerator DrainGhost(UnitBar bar, float target)
        {
            float current = bar.HpGhost.fillAmount;
            if (current <= target) { UIBuilder.SetBar(bar.HpGhost, target); yield break; }
            yield return Wait(0.16f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3.2f * _speed;
                UIBuilder.SetBar(bar.HpGhost, Mathf.Lerp(current, target, t));
                yield return null;
            }
            UIBuilder.SetBar(bar.HpGhost, target);
        }

        /// <summary>
        /// Ember (DESIGN-ASPECTS section 6): the bar states how deep a stack is. Four distinct
        /// kinds is still the cap on what the row shows, but the cap now counts kinds rather than
        /// entries - with stacking, four entries could be one affliction, and a row that spent its
        /// whole width saying BRN four times would be less readable than the one this replaces.
        ///
        /// The suffix is only written for a real stack. A lone status reads BRN exactly as it
        /// always did, so the count is a mark of the thing Ember does rather than noise on every
        /// tag in the game.
        /// </summary>
        private static void RefreshStatusText(UnitBar bar)
        {
            string text = "";
            int shown = 0;
            for (int i = 0; i < bar.Statuses.Count && shown < 4; i++)
            {
                StatusKind kind = bar.Statuses[i];

                bool alreadyShown = false;
                for (int j = 0; j < i; j++) if (bar.Statuses[j] == kind) { alreadyShown = true; break; }
                if (alreadyShown) continue;

                int depth = 0;
                for (int j = i; j < bar.Statuses.Count; j++) if (bar.Statuses[j] == kind) depth++;

                if (text.Length > 0) text += " ";
                text += StatusInfo.ShortTag(kind);
                if (depth > 1) text += "x" + depth;
                shown++;
            }
            bar.Status.text = text;
        }

        /// <summary>
        /// Set the bar's entry count for one status to what the simulation says it is.
        /// DESIGN-BATTLE-READABILITY's rule, applied to a number rather than to a cause: the depth
        /// is read off the event, never derived here. Deriving it is the trap - AddOrRefresh folds
        /// an ordinary re-application into its existing entry and emits StatusApplied regardless, so
        /// a bar counting events would show Nyx's Guard Down growing one deeper every basic attack
        /// while the simulation held exactly one entry the whole fight.
        ///
        /// A count of zero means an emit site that carries no depth - StatusSpread, which uses
        /// AddOrRefresh and so can never produce a stack. Treated as "at least one present", which
        /// leaves an existing stack alone rather than flattening it to a single tag.
        /// </summary>
        private static void SyncStatusCount(UnitBar bar, StatusKind kind, int count)
        {
            if (kind == StatusKind.None) return;

            int have = 0;
            for (int i = 0; i < bar.Statuses.Count; i++) if (bar.Statuses[i] == kind) have++;

            int want = count > 0 ? count : Mathf.Max(1, have);
            if (want == have) return;

            while (have < want) { bar.Statuses.Add(kind); have++; }
            for (int i = bar.Statuses.Count - 1; i >= 0 && have > want; i--)
            {
                if (bar.Statuses[i] != kind) continue;
                bar.Statuses.RemoveAt(i);
                have--;
            }

            RefreshStatusText(bar);
        }

        private void ShowBanner(string title, string subtitle)
        {
            _bannerTitle.text = title != null ? title.ToUpperInvariant() : "";
            _bannerSub.text = subtitle != null ? subtitle.ToUpperInvariant() : "";
            _banner.gameObject.SetActive(true);
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(BannerRoutine());
        }

        private IEnumerator BannerRoutine()
        {
            _bannerGroup.alpha = 0f;
            float t = 0f;
            while (t < 0.12f) { t += Time.deltaTime * _speed; _bannerGroup.alpha = t / 0.12f; yield return null; }
            _bannerGroup.alpha = 1f;
            yield return Wait(0.55f);
            t = 0f;
            while (t < 0.22f) { t += Time.deltaTime * _speed; _bannerGroup.alpha = 1f - t / 0.22f; yield return null; }
            _bannerGroup.alpha = 0f;
        }

        // ------------------------------------------------------------------ floating text

        private void ResetFloatingText()
        {
            for (int i = 0; i < _floatPool.Count; i++)
            {
                _floatPool[i].Busy = false;
                _floatPool[i].Group.alpha = 0f;
                _floatPool[i].Root.gameObject.SetActive(false);
            }
        }

        private FloatText TakeFloatText()
        {
            for (int i = 0; i < _floatPool.Count; i++) if (!_floatPool[i].Busy) return _floatPool[i];

            FloatText item = new FloatText();
            item.Root = UIBuilder.Node("Float", _textLayer);
            item.Root.anchorMin = Vector2.zero;
            item.Root.anchorMax = Vector2.zero;
            item.Root.pivot = new Vector2(0.5f, 0.5f);
            UIBuilder.Size(item.Root, 320f, 60f);
            item.Group = item.Root.gameObject.AddComponent<CanvasGroup>();
            item.Label = UIBuilder.Label("Text", item.Root, "", UITheme.FontHeading, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(item.Label.rectTransform, 0f);
            UIBuilder.Outlined(item.Label, UITheme.InkArt, 2.4f);
            _floatPool.Add(item);
            return item;
        }

        private void SpawnFloatingText(Vector3 worldPosition, string text, Color colour, float scale)
        {
            FloatText item = TakeFloatText();
            item.Busy = true;
            item.Label.text = text;
            item.Label.color = colour;
            item.Root.localScale = new Vector3(scale, scale, 1f);
            item.Root.gameObject.SetActive(true);
            item.Root.SetAsLastSibling();
            StartCoroutine(FloatRoutine(item, worldPosition));
        }

        private IEnumerator FloatRoutine(FloatText item, Vector3 worldPosition)
        {
            float canvasScale = App.RootCanvas != null ? App.RootCanvas.scaleFactor : 1f;
            if (canvasScale <= 0f) canvasScale = 1f;

            Vector3 screen = _world.Camera.WorldToScreenPoint(worldPosition);
            Vector2 start = new Vector2(screen.x / canvasScale, screen.y / canvasScale);

            // Callers pass HeadPosition, which is exactly where that unit's tag is anchored, so
            // a number spawned here started underneath the name and rose straight through it.
            // That is the "58 on top of the Sundered Colossus's name" case, and it was not bad
            // luck: for a head-targeted number it was certain. Clearing the tag's own height puts
            // the number above it and it rises away rather than across.
            float rise = BarWorldRise * (Screen.height / (2f * Mathf.Max(0.01f, _world.Camera.orthographicSize)));
            start.y += (rise / canvasScale) + BarStackHeight + 6f;
            start.x += Random.Range(-34f, 34f);

            // Clearing the tag pushes the back rank's far-z units past the top of the screen, so
            // the whole 120-unit climb is clamped to fit rather than just the spawn point. A
            // number that lands slightly below its own unit is still readable; one that rises off
            // the screen is not there at all.
            start.y = Mathf.Min(start.y, Screen.height / canvasScale - FloatRise - 30f);

            float duration = 0.75f / _speed;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                item.Root.anchoredPosition = start + new Vector2(0f, FloatRise * Mathf.Sqrt(k));
                item.Group.alpha = 1f - k * k;
                yield return null;
            }

            item.Group.alpha = 0f;
            item.Root.gameObject.SetActive(false);
            item.Busy = false;
        }

        // ------------------------------------------------------------------ result

        private void ShowResult()
        {
            if (_finished) return;
            _finished = true;
            _skipButton.Interactable = false;

            App.RefreshHud();
            App.RequestSave();

            _resultTitle.text = _result.Victory ? "VICTORY" : "DEFEAT";
            _resultTitle.color = _result.Victory ? UITheme.Gold : UITheme.Danger;
            _resultSub.text = _result.Rounds + " ROUNDS"
                              + (_result.TimedOut ? "  -  TIME LIMIT REACHED" : "")
                              + "  -  BANNER AT " + Mathf.RoundToInt(_result.PlayerHpRatio * 100f) + "%";

            // one plain sentence naming the single biggest reason this fight went the way it did -
            // win or lose, per DESIGN-FIRST-TEN-MINUTES: "a one-line cause... win or lose"
            DebriefResult debrief = Debrief.Summarize(_result);
            _debriefText.text = debrief.Text;

            ComputeStats();
            SelectStatTab(0);

            for (int i = _rewardList.childCount - 1; i >= 0; i--) Destroy(_rewardList.GetChild(i).gameObject);

            if (_recitalFloor >= 0) ShowRecitalResult();
            else ShowCampaignResult();

            _resultPanel.gameObject.SetActive(true);
            _resultPanel.SetAsLastSibling();
            _resultGroup.alpha = 1f;
        }

        private void ShowCampaignResult()
        {
            BattleRewards rewards = App.Session.GrantBattleResult(_stage, _result.Victory, TimeUtil.NowUnix());

            if (_result.Victory)
            {
                if (rewards.FirstClear) AddRewardRow("FIRST CLEAR BONUS", "INCLUDED", UITheme.Success);
                // Why the purse below reads zero, said before the player reads it as a bug.
                else if (rewards.RepeatCapped) AddRewardRow("DAILY REPEAT CAP", "REACHED", UITheme.Warning);
                else AddRewardRow("PAID REPEATS LEFT TODAY", rewards.RepeatClearsRemaining.ToString(), UITheme.TextMuted);
                AddRewardRow("GOLD", "+" + rewards.Gold, UITheme.Gold);
                AddRewardRow("EMBER SHARDS", "+" + rewards.Shards, UITheme.Shard);
                AddRewardRow("WAYFARER XP", "+" + rewards.AccountXp, UITheme.Shard);
                if (rewards.PlayerLevelsGained > 0)
                    AddRewardRow("RANK UP", "+" + rewards.PlayerLevelsGained + "  (hero cap " + App.Session.HeroLevelCap + ")", UITheme.Success);
                if (rewards.UnlockedNextStage) AddRewardRow("NEW STAGE UNLOCKED", "AHEAD", UITheme.Ember);
                // Named by its temper rather than just "ARMOR", because the temper is the only part
                // of the drop a player can judge before opening the inventory.
                if (rewards.ArmorDrop != null)
                    AddRewardRow(ArmorInfo.TemperLabel(rewards.ArmorDrop.Temper) + " " + ArmorInfo.SlotLabel(rewards.ArmorDrop.Slot).ToUpperInvariant(),
                                 rewards.ArmorDrop.Name, UITheme.ArmorTemperColor(rewards.ArmorDrop.Temper));
                App.Audio.Victory();
            }
            else
            {
                AddRewardRow("NOTHING RECOVERED", "-", UITheme.TextDim);
                AddRewardRow("LEVEL YOUR HEROES", "HERO SCREEN", UITheme.TextMuted);
                AddRewardRow("OR COLLECT THE CACHE", "MAIN SCREEN", UITheme.TextMuted);
                App.Audio.Defeat();
            }
        }

        /// <summary>Echoes are never shown here as a reward line - one was already spent to reach
        /// this screen, win or lose, by TryStartRecitalAttempt before the fight ran, so there is
        /// nothing about them to report on the way out.</summary>
        private void ShowRecitalResult()
        {
            RecitalRewards rewards = App.Session.GrantRecitalResult(_recitalFloor, _result.Victory, TimeUtil.NowUnix());

            if (_result.Victory)
            {
                if (rewards.AdvancedFloor)
                    AddRewardRow("FLOOR CLEARED", App.Session.Profile.RecitalHighestFloor.ToString(), UITheme.Success);
                AddRewardRow("GOLD", "+" + rewards.Gold, UITheme.Gold);
                AddRewardRow("EMBER SHARDS", "+" + rewards.Shards, UITheme.Shard);
                if (!string.IsNullOrEmpty(rewards.GrantedRelicId))
                {
                    RelicDef relic = App.Session.Content.GetRelic(rewards.GrantedRelicId);
                    AddRewardRow("RELIC RECOVERED", relic != null ? relic.Name : rewards.GrantedRelicId, UITheme.Mythic);
                }
                else if (rewards.RelicDust > 0)
                {
                    AddRewardRow(rewards.IsDeepMilestone ? "DEEP MILESTONE DUST" : "MILESTONE DUST",
                                 "+" + rewards.RelicDust, UITheme.Ember);
                }
                App.Audio.Victory();
            }
            else
            {
                AddRewardRow("NOTHING RECOVERED", "-", UITheme.TextDim);
                AddRewardRow("THE LADDER HOLDS", "FLOOR " + App.Session.Profile.RecitalCurrentFloor + " STILL AHEAD", UITheme.TextMuted);
                App.Audio.Defeat();
            }
        }

        private void AddRewardRow(string label, string value, Color tint)
        {
            Widgets.StatRow(label, _rewardList, label, value, tint, 56f);
        }

        private void ComputeStats()
        {
            _statsDealt.Clear();
            _statsHealed.Clear();
            _statsTaken.Clear();
            for (int i = 0; i < _result.Units.Count; i++)
            {
                UnitSnapshot u = _result.Units[i];
                _statsDealt[u.Id] = 0;
                _statsHealed[u.Id] = 0;
                _statsTaken[u.Id] = 0;
            }
            for (int i = 0; i < _result.Events.Count; i++)
            {
                BattleEvent e = _result.Events[i];
                if (e.Type == BattleEventType.Damage && e.Value > 0)
                {
                    if (_statsDealt.ContainsKey(e.SourceId)) _statsDealt[e.SourceId] += e.Value;
                    if (_statsTaken.ContainsKey(e.TargetId)) _statsTaken[e.TargetId] += e.Value;
                }
                else if (e.Type == BattleEventType.Heal && e.Value > 0)
                {
                    if (_statsHealed.ContainsKey(e.SourceId)) _statsHealed[e.SourceId] += e.Value;
                }
            }
        }

        private void SelectStatTab(int index)
        {
            _statTabActive = index;
            Dictionary<int, int> map = index == 1 ? _statsHealed : index == 2 ? _statsTaken : _statsDealt;
            ShowStats(map);

            for (int i = 0; i < _statTabs.Length; i++)
            {
                if (_statTabs[i] == null) continue;
                Text lbl = _statTabs[i].Root.GetComponentInChildren<Text>();
                if (lbl != null) lbl.color = i == index ? UITheme.Gold : UITheme.TextDim;
            }
        }

        private void ShowStats(Dictionary<int, int> statMap)
        {
            for (int i = _statsPanel.childCount - 1; i >= 0; i--)
                Destroy(_statsPanel.GetChild(i).gameObject);

            List<UnitSnapshot> heroes = new List<UnitSnapshot>();
            List<UnitSnapshot> enemies = new List<UnitSnapshot>();
            for (int i = 0; i < _result.Units.Count; i++)
            {
                if (_result.Units[i].IsPlayer) heroes.Add(_result.Units[i]);
                else enemies.Add(_result.Units[i]);
            }

            AddStatSection("YOUR TEAM", heroes, statMap);
            AddStatSection("ENEMIES", enemies, statMap);
        }

        private void AddStatSection(string header, List<UnitSnapshot> units, Dictionary<int, int> statMap)
        {
            units.Sort((a, b) =>
            {
                int va; statMap.TryGetValue(a.Id, out va);
                int vb; statMap.TryGetValue(b.Id, out vb);
                return vb.CompareTo(va);
            });

            RectTransform hdrNode = UIBuilder.Node("Hdr_" + header, _statsPanel);
            UIBuilder.Sized(hdrNode, 0f, 22f);
            Text hdrText = UIBuilder.Label("Text", hdrNode, header, UITheme.FontTiny - 2, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(hdrText.rectTransform, 12f, 0f, 0f, 0f);

            for (int i = 0; i < units.Count; i++)
            {
                int val;
                statMap.TryGetValue(units[i].Id, out val);
                Color tint = i == 0 ? UITheme.Gold : UITheme.TextPrimary;
                Widgets.StatRow(units[i].Name, _statsPanel, units[i].Name, val.ToString(), tint, 44f);
            }
        }

        // ------------------------------------------------------------------ actions

        private void CycleSpeed()
        {
            if (_speed < 1.5f) _speed = 2f;
            else if (_speed < 3f) _speed = 4f;
            else _speed = 1f;
            _speedButton.SetText(Mathf.RoundToInt(_speed) + "x");
            if (_world != null && _world.Vfx != null) _world.Vfx.SpeedScale = _speed;
            App.Audio.Tap();
        }

        private void SkipToEnd()
        {
            if (_finished) return;
            App.Audio.Tap();
            if (_playback != null) { StopCoroutine(_playback); _playback = null; }
            if (_world == null) { ShowResult(); return; }
            if (_world.Vfx != null) _world.Vfx.StopAll();

            // settle every actor to its final state so the board matches the outcome
            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor actor = _world.Actors[i];
                bool survived = SurvivedBattle(actor.Snapshot.Id);
                if (!survived && actor.Alive)
                {
                    actor.Alive = false;
                    if (actor.Rig != null) actor.Rig.Root.gameObject.SetActive(false);
                }
            }
            ShowResult();
        }

        private bool SurvivedBattle(int unitId)
        {
            for (int i = 0; i < _result.Events.Count; i++)
            {
                BattleEvent e = _result.Events[i];
                if (e.Type == BattleEventType.Death && e.TargetId == unitId) return false;
            }
            return true;
        }

        private void Replay()
        {
            App.Audio.Tap();
            OnHide();
            OnShow();
        }

        /// <summary>Straight to Heroes to change the team - the stage stays remembered on this
        /// screen (_stage isn't cleared), so Campaign still opens on the same fight afterward.</summary>
        private void AdjustLine()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Heroes, false);
        }
    }
}
