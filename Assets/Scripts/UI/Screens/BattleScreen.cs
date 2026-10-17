using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.UI.Battle;

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

        /// <summary>
        /// One unit's plate and what this screen remembers about it. The plate (UnitPlate) draws;
        /// the mirror of the unit's statuses and the turn cue's lift are this screen's, because
        /// they are read off the record here and handed to the plate, never worked out by it.
        /// </summary>
        private sealed class UnitBar
        {
            public UnitPlate Plate;

            /// <summary>
            /// One entry per entry the simulation holds, duplicates included, in the order they
            /// first landed. It used to be a set, which is what made Ember's stacking invisible:
            /// three Burn entries collapsed into one BRN tag on the way in and no later code could
            /// tell them apart. Kept in sync from the count StatusApplied carries, never tallied
            /// from how many times the event was seen - see SyncStatusCount.
            /// </summary>
            public readonly List<StatusKind> Statuses = new List<StatusKind>();

            /// <summary>Added to the plate's tracked screen position by LateUpdate while the turn cue plays.</summary>
            public float TurnLift;
        }

        private sealed class FloatText
        {
            public RectTransform Root;
            public Text Label;
            public CanvasGroup Group;
            public bool Busy;
        }

        private StageDef _stage;
        private BattleStage _world;
        private BattleResult _result;
        private Coroutine _playback;
        private Coroutine _calloutRoutine;
        private float _speed = 1f;

        /// <summary>
        /// Chain mode, per DESIGN-CAMPAIGN-STRUCTURE.md's "Auto-advance through a fresh stage".
        /// While on, a won battle starts the next one without a tap. A first clear is 144 battles
        /// and NEXT BATTLE is one tap each, so this is the difference between the 144x the design
        /// accepted being idle clock-time and it being active tap-time.
        ///
        /// A UI loop over the existing FightNextBattle path, NOT a second way to resolve a battle:
        /// every fight still runs through the same OnShow/RunSimulation the manual button uses, so
        /// there is no auto-only code path that could drift from the played one.
        /// </summary>
        private bool _auto;
        private UIButton _autoButton;

        /// <summary>True only for the instant FightNextBattle spends between two battles of the
        /// same chain. OnHide cannot otherwise tell "the player left" from "the chain hopped", and
        /// FightNextBattle goes through OnHide every time.</summary>
        private bool _chaining;

        /// <summary>The top bar: its ground and its safe-area content. Held so it can be raised
        /// back above the result panel - see the note where that happens.</summary>
        private RectTransform _topHud;
        private Image _barGround;
        private ForgeDisc _home;

        /// <summary>The bar's second line is WAVE / BATTLE, then ROUND. The wave and battle part
        /// is set once per fight and the round part on every RoundStart, so they are held apart
        /// and joined in RefreshStatusLine.</summary>
        private string _progressText = "";
        private string _roundText = "";

        /// <summary>Device-level UI preferences, deliberately in PlayerPrefs rather than
        /// PlayerProfile: how fast someone likes their battles and whether they chain is a property
        /// of the handset, not of the account, and putting it in the save would have cost a
        /// SaveSerializer version bump for something no other device should inherit.</summary>
        private const string AutoPrefKey = "Emberfall.Battle.Auto";
        private const string SpeedPrefKey = "Emberfall.Battle.Speed";
        private bool _finished;

        /// <summary>
        /// True from a skill's AbilityCast until the next BasicAttack or TurnStart: the Damage
        /// events in between are the skill landing, which sounds different from a basic hit. Read
        /// off the record in the order it plays; nothing is recomputed.
        /// </summary>
        private bool _skillActive;

        /// <summary>True once ShowResult has run - the fight is fully resolved and the result
        /// panel is up. Exists for external observers (editor automation, tests driving a real
        /// Play mode session) that need to know when to stop waiting; nothing in-game reads it.</summary>
        public bool Finished { get { return _finished; } }

        /// <summary>
        /// Nominal (pre-_speed) seconds of an attack or skill clip's follow-through still owed.
        /// The event that TRIGGERED the clip holds only for the outbound half, so the Damage events
        /// that follow render on the strike frame; the follow-through is owed here and drained
        /// before the next ACTION opens (OpensBeat), never before the action's own consequences.
        /// Every Wait while it is owed pays it down, so the damage numbers, hit reactions and
        /// status tags of the strike play out during the follow-through rather than after it.
        ///
        /// It used to drain at the top of the very next Handle, which is the strike's own Damage
        /// event, so every drawn hero's hit landed a whole follow-through - 1.0 to 1.7 seconds -
        /// after the frame the clip draws it on. Always 0 for a unit with no SpriteAnimator, so
        /// placeholder mannequins time exactly as they did.
        /// </summary>
        private float _pendingAnimationTail;

        private Text _stageLabel;
        private Text _roundLabel;
        private UIButton _speedButton;
        private UIButton _skipButton;
        private RectTransform _barLayer;
        private RectTransform _textLayer;

        private RectTransform _callout;
        private CanvasGroup _calloutGroup;
        private Text _calloutText;

        /// <summary>The callout's text width at the size it is drawn, kept so SyncFrame can
        /// re-place the pill when the screen changes shape.</summary>
        private float _calloutTextWidth;

        private RectTransform _resultPanel;
        private CanvasGroup _resultGroup;
        private Text _resultTitle;

        /// <summary>
        /// REMATCH, but only while a CONTINUE is also on offer. Built as a SECONDARY so the panel
        /// never shows two gold buttons at once - see ShowResult for why the primary changes
        /// identity instead of a third prominent button being added.
        /// </summary>
        private UIButton _resultTertiary;

        /// <summary>
        /// What the gold button means for THIS result. Was a bool ("continues, or replays"), which
        /// only had room for the two answers a win needs; a loss needs a third.
        /// </summary>
        private enum PrimaryAction { Replay, Continue, Exit, Prologue, NextBattle, Guild }

        private PrimaryAction _primaryAction = PrimaryAction.Replay;

        /// <summary>Where Exit goes: the screen this fight was launched from. Resolved per result
        /// so the button leads back where the player actually came from.</summary>
        private ScreenId _exitScreen = ScreenId.Campaign;
        private Text _resultSub;
        private RectTransform _rewardList;
        private Text _debriefText;
        private UIButton _resultPrimary;
        private UIButton _resultSecondary;

        private ForgeTab[] _statTabs;
        private int _statTabActive;
        private RectTransform _statsPanel;
        private readonly Dictionary<int, int> _statsDealt = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _statsHealed = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _statsTaken = new Dictionary<int, int>();

        private readonly Dictionary<int, UnitBar> _bars = new Dictionary<int, UnitBar>();
        private readonly List<FloatText> _floatPool = new List<FloatText>();

        private TurnOrderPill _turnPill;

        /// <summary>The round's turn order: one unit id per TurnStart between this RoundStart and
        /// the next, read off the record. A unit can appear twice (Gale's kill re-entry).</summary>
        private readonly List<int> _turnOrder = new List<int>();
        private readonly List<UnitSnapshot> _upcomingScratch = new List<UnitSnapshot>();

        /// <summary>
        /// Whether a round has begun and the turn order is populated - which is to say, whether
        /// this screen is showing the battle HUD or still showing its opening.
        ///
        /// Read-only and for the capture tool, which needs to photograph a HUD rather than a
        /// preamble. It is exposed rather than guessed at with a frame count because a frame count
        /// is not a fact about this screen: the number of BattleStart passives that banner before
        /// the first round is a property of the five heroes deployed, so the "safe" delay changes
        /// with the team, and a capture taken on the old guess photographed a battlefield with no
        /// turn order on it and reported that as the HUD.
        ///
        /// It asserts nothing and changes nothing - it reads the count of a list the playback
        /// already maintains, so it cannot affect what the screen does or when.
        /// </summary>
        public bool RoundUnderway { get { return _turnOrder.Count > 0; } }

#if UNITY_EDITOR
        /// <summary>
        /// Presses SKIP from outside, so a capture can photograph the RESULT PANEL without
        /// sitting through a fight at playback speed.
        ///
        /// Editor-only, and it drives the same private SkipToEnd the button drives rather than
        /// reaching past it - a capture that assembled the result panel its own way would be a
        /// photograph of something no tap can produce. It is the same reasoning as RoundUnderway
        /// above: the tool asks the screen a question, or asks it to do a thing it already does,
        /// and never does the thing itself.
        /// </summary>
        public void SkipToEndForCapture() { SkipToEnd(); }

        /// <summary>True while the event last played is a blow rather than a tick, so a capture
        /// can freeze on a hit effect and not on a status ring. Read only; set in Handle.</summary>
        public bool HitJustPlayed { get; private set; }

        /// <summary>True for a quarter second after a Heal plays, so a capture can freeze on the heal effect.</summary>
        public bool HealJustPlayed { get { return Time.unscaledTime - _lastHealAt < 0.25f; } }
        private float _lastHealAt = -10f;
#endif

        /// <summary>Index into _turnOrder of the turn being played, and -1 before the round's
        /// first. See AdvanceTurnStrip.</summary>
        private int _turnStripCursor;
        private int _turnActing = -1;
        private UnitBar _activeTurnBar;

        /// <summary>-1 when this battle is a campaign fight (the only mode that existed before The
        /// Deep Recital); a floor number when it is a Recital attempt. Drives ShowResult's branch
        /// between GameSession.GrantBattleResult and GrantRecitalResult - the two reward models are
        /// different enough (repeat cap and stage unlocks vs. a one-way floor ladder and Echoes)
        /// that sharing one result path would mean one of them lying about what happened.</summary>
        private int _recitalFloor = -1;

        /// <summary>
        /// The prologue's fixed squad while this battle is a prologue step, or null for every other
        /// mode. The fourth discriminator on this screen, alongside _stage, _recitalFloor and
        /// _provingOpponent - see SetPrologueStep for why it is a team rather than a bool.
        /// </summary>
        private List<HeroInstance> _prologueSquad;

        /// <summary>
        /// Position inside the campaign stage being fought, 1-based, per
        /// DESIGN-CAMPAIGN-STRUCTURE.md. Meaningful only when _stage carries a wave curve; a
        /// synthetic stage is a single fight and sits at (1,1) forever.
        ///
        /// Held on the screen rather than read from the profile each frame because the profile's
        /// cursor advances the moment a battle is WON, and this screen still has to render the
        /// result panel for the battle just fought. Reading the profile would relabel the panel
        /// with the next battle's number while the player is still looking at the last one's.
        /// </summary>
        private int _campaignWave = 1;
        private int _campaignBattle = 1;

        /// <summary>True when the win just shown advanced the stage but did not finish it, so the
        /// panel is a checkpoint rather than a clear: no spoils, and the gold button fights on
        /// instead of leaving.</summary>
        private bool _midStageAdvance;

        /// <summary>What OnboardingCoach wants told when a prologue fight is done with, true on a win.
        /// Null in every other mode.</summary>
        private System.Action<bool> _prologueOnResolved;

        /// <summary>
        /// The snapshot being fought when this battle is a Proving attempt, or null for every other
        /// mode. Set by SetProvingOpponent and cleared by SetStage/SetRecitalFloor, so the three
        /// modes are mutually exclusive by construction rather than by remembering to reset.
        /// </summary>
        private BattleLoadout _provingOpponent;

        /// <summary>
        /// A friend's published line while this battle is a friendly duel, or null for every other
        /// mode. The FIFTH discriminator, and the only one that costs nothing and pays nothing.
        ///
        /// Kept separate from _provingOpponent rather than reusing it with a flag, even though the
        /// two carry the same type and reach the same BuildProvingRequest. What differs is not how
        /// the fight resolves - it resolves identically, which is the point - but everything
        /// around it: a Proving attempt spends a Brand on entry and grants Valor and standing on
        /// the way out, and a duel does neither. One field with a "don't charge me" flag would put
        /// that distinction inside two branches that each have to remember it; two fields put it
        /// in the type system, where forgetting is not available.
        /// </summary>
        private BattleLoadout _duelOpponent;

        /// <summary>The friend's name, for the header and the result panel. Held beside the
        /// loadout rather than taken from BattleLoadout.Label, because the label is whatever that
        /// player's account was called when they last published and the friends list may know them
        /// by a newer one.</summary>
        private string _duelOpponentName = "";

        /// <summary>
        /// The seed the duel was resolved at, kept so the result panel can show it.
        ///
        /// Shown because a duel is the one fight in this game whose outcome somebody might want to
        /// argue about. The engine is a pure function of its request, so the seed plus the two
        /// lines IS the fight - printing it is what makes "we can both check" a true statement
        /// rather than a claim about an architecture nobody outside the code can see.
        /// </summary>
        private int _duelSeed;

        /// <summary>
        /// A guild dungeon fight, or a replay of one, while this battle is either; null for every
        /// other mode. The SIXTH discriminator, and the only one whose result is HANDED IN rather
        /// than computed here: GuildDungeon.Fight or GuildDungeon.Replay produced it in Core, and
        /// this screen plays it back. A replay that recomputed would be a second fight that looked
        /// like the first, and the whole point of the handover is that it is the first.
        ///
        /// It also carries what the result panel says and where its buttons lead, because the
        /// dungeon's flow (the result screen, the floor, the chat) lives on screens this one knows
        /// nothing about.
        /// </summary>
        private GuildBattleRun _guildRun;

        /// <summary>
        /// Plays a guild dungeon attempt or a replay. Spends nothing and grants nothing: an attempt
        /// was stored before it got here (GuildDungeonFlow.Attempt), and a replay is a record.
        /// REMATCH is WATCH AGAIN in this mode, and it replays the same result - there is no second
        /// roll to be had, which is the rule the dungeon is built on.
        /// </summary>
        public void SetGuildRun(GuildBattleRun run)
        {
            _recitalFloor = -1;
            _provingOpponent = null;
            _duelOpponent = null;
            _prologueSquad = null;
            _guildRun = run;
            _stage = run != null ? run.Stage : null;
        }

        /// <summary>
        /// Enters a campaign stage, resuming wherever the player left it.
        ///
        /// The resume point comes from the session rather than defaulting to (1,1), which is what
        /// makes a stage survive being walked away from: 90 battles in, quit to the Hub, come back
        /// a week later, and this starts battle 91. See GameSession.ResumePointFor for why the
        /// saved stage id has to match before the position is honoured.
        /// </summary>
        public void SetStage(StageDef stage)
        {
            _stage = stage;
            _recitalFloor = -1;
            _provingOpponent = null;
            _duelOpponent = null;
            _prologueSquad = null;
            _guildRun = null;

            _campaignWave = 1;
            _campaignBattle = 1;
            if (stage != null && App != null && App.Session != null)
                App.Session.ResumePointFor(stage, out _campaignWave, out _campaignBattle);
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
            _provingOpponent = null;
            _duelOpponent = null;
            _prologueSquad = null;
            _guildRun = null;
            _stage = App.Session.BuildRecitalStage(floor);
        }

        /// <summary>
        /// Launches this screen as a Proving attempt. Spends nothing itself, for exactly the reason
        /// SetRecitalFloor does not: OnShow is the one spend point, because it runs for both this
        /// entry AND every Rematch, and an attempt costs a Brand either way.
        ///
        /// The stage is the mode's own (ProvingGenerator.BuildStage), not a borrowed campaign one.
        /// It used to borrow Chapters[0].Stages[0], which meant every Proving match rendered against
        /// ch1_1's painted "ashen" backdrop - not a fallback, just chapter 1's art in a mode it has
        /// nothing to do with. Its own stage carries its own environment id instead.
        /// </summary>
        public void SetProvingOpponent(BattleLoadout opponent)
        {
            _recitalFloor = -1;
            _provingOpponent = opponent;
            _duelOpponent = null;
            _prologueSquad = null;
            _guildRun = null;
            _stage = ProvingGenerator.BuildStage();
        }

        /// <summary>
        /// Launches this screen as a friendly duel against a friend's published line.
        ///
        /// NOTHING IS SPENT HERE AND NOTHING IS SPENT IN OnShow, which is the whole difference
        /// between this and SetProvingOpponent one method up. A duel costs no Brand, pays no
        /// Valor, moves no standing, records no attempt and writes nothing to the save - see
        /// ShowDuelResult, which grants nothing at all. Rematch is therefore free and unlimited,
        /// which is correct: there is no economy here to protect.
        ///
        /// The stage is the Proving's, deliberately and with one caveat worth writing down. A duel
        /// wants its own name, but it does not want its own ENVIRONMENT id: BattleAssets only
        /// carries palettes for "ember", "hollow" and "proving", and anything else falls through
        /// to ashen's - so inventing "duel" would have bought a name and cost the warm gatehouse
        /// dusk that the only other line-against-line mode in the game already fights in. The id
        /// is the Proving's; the stage id is not, because it is not that mode's match.
        /// </summary>
        public void SetDuelOpponent(BattleLoadout opponent, string friendName)
        {
            _recitalFloor = -1;
            _provingOpponent = null;
            _prologueSquad = null;
            _guildRun = null;
            _duelOpponent = opponent;
            _duelOpponentName = string.IsNullOrEmpty(friendName)
                ? (opponent != null ? opponent.Label : "") : friendName;

            StageDef stage = ProvingGenerator.BuildStage();
            stage.Id = "friendly_duel";
            stage.Name = "Friendly Duel";
            stage.Subtitle = "nothing is at stake";
            _stage = stage;
        }

        /// <summary>
        /// Runs one prologue step. The squad is handed in rather than read from the save, and the
        /// result is handed BACK rather than acted on here.
        ///
        /// The callback is the whole reason this mode exists as a fourth discriminator instead of
        /// as a campaign stage with a flag. Every other mode owns what happens after its result -
        /// Campaign continues, Recital returns to its tower, the Proving returns to its ladder.
        /// The prologue does not: what follows it is the first session's guided loop, which belongs
        /// to OnboardingCoach, not to a battle screen, and it has to fire on a WIN as well as a loss.
        /// Passing the outcome back leaves that decision in the one place that knows the script.
        ///
        /// onResolved is invoked from the gold button, not from the moment the sim ends, so the
        /// player still reads the result panel of the fight they just lost before the story moves.
        /// </summary>
        public void SetPrologueStep(PrologueScript.Step step, List<HeroInstance> squad,
                                    System.Action<bool> onResolved)
        {
            _recitalFloor = -1;
            _provingOpponent = null;
            _duelOpponent = null;
            _guildRun = null;
            _prologueSquad = squad;
            _prologueOnResolved = onResolved;
            _stage = PrologueScript.BuildStage(step);
        }

        // ------------------------------------------------------------------ construction

        protected override void Build()
        {
            _barLayer = UIBuilder.Node("Bars", Root);
            UIBuilder.FillParent(_barLayer, 0f);

            _textLayer = UIBuilder.Node("FloatingText", Root);
            UIBuilder.FillParent(_textLayer, 0f);

            BuildTopHud();
            BuildTurnOrder();
            BuildCallout();

            // Floating numbers were being created before the ability callout and so drawing
            // UNDERNEATH it: a number that overlaps the callout is survivable, a number hidden
            // behind it is a lost event. uGUI paints in sibling order, so lifting the text layer
            // here puts every number on top of the callout and the HUD while still leaving the
            // result panel above everything.
            _textLayer.SetAsLastSibling();

            BuildResultPanel();
        }

        // THE TURN ORDER, as a pill at the bottom centre (TurnOrderPill): the acting unit's disc,
        // enlarged, then NEXT and up to six units still to act this round. It was a row of
        // 30-unit chips across the top of the field, where it competed with the back rank's tags
        // and the ability banner for the same strip of sky. The bottom centre is the one stretch
        // of the frame no formation slot's plate reaches, which BattleHudChecks proves at every
        // aspect, so the field's viewport is NOT shortened at the bottom to make room for it.
        private void BuildTurnOrder()
        {
            _turnPill = TurnOrderPill.Create(Root);
        }

        // THE TOP BAR, opaque, BattleHudMetrics.BarHeight under the top of the safe area with a
        // thin rule along its lower edge. It used to be a transparent 64-unit strip of outlined
        // labels over the painting, and units and their tags stood behind it. Now the battle
        // camera's viewport stops at the bar's lower edge (see BuildWorld and SyncFrame), so the
        // bar covers nothing: no unit, no plate and no number is ever drawn under it, and it can
        // be a solid surface that reads at a glance instead of text fighting a painting.
        //
        // The ground runs up to the very top of the screen, so a top inset shows bar rather than
        // field; the content sits in a SafeAreaRect child, so HOME and the buttons never land
        // under a notch. HOME on the left, then two lines, the stage and then WAVE / BATTLE /
        // ROUND, and AUTO, SKIP and the speed button on the right. Sizes are BattleHudMetrics',
        // colours BattleHudStyle's.
        private void BuildTopHud()
        {
            // A full-screen holder with no graphic of its own, so the bar's ground and its
            // safe-area content rise above the result panel together (see ShowResult) without
            // the holder catching a tap anywhere.
            _topHud = UIBuilder.Node("TopHud", Root);
            UIBuilder.FillParent(_topHud, 0f);

            _barGround = UIBuilder.Img("Bar", _topHud, TextureLab.White, BattleHudStyle.BarFill);
            RectTransform ground = _barGround.rectTransform;
            ground.anchorMin = new Vector2(0f, 1f);
            ground.anchorMax = new Vector2(1f, 1f);
            ground.pivot = new Vector2(0.5f, 1f);
            ground.anchoredPosition = Vector2.zero;
            ground.sizeDelta = new Vector2(0f, BattleHudMetrics.BarHeight);

            Image rule = UIBuilder.Img("Rule", ground, TextureLab.White, BattleHudStyle.BarRule);
            rule.rectTransform.anchorMin = Vector2.zero;
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.anchoredPosition = Vector2.zero;
            rule.rectTransform.sizeDelta = new Vector2(0f, BattleHudMetrics.BarRuleHeight);

            RectTransform safe = UIBuilder.Node("Safe", _topHud);
            SafeAreaRect.Attach(safe);
            RectTransform bar = UIBuilder.Node("Content", safe);
            UIBuilder.TopStrip(bar, BattleHudMetrics.BarHeight, 0f, UITheme.ScreenPadding);

            _stageLabel = EmberforgeUI.Label("Stage", bar, "", BattleHudMetrics.BarStageLineFont,
                                             BattleHudStyle.StageLine, TextAnchor.MiddleLeft);
            UIBuilder.Box(_stageLabel.rectTransform, BattleHudMetrics.BarLabelInset, -BattleHudMetrics.BarStageLineTop,
                          BattleHudMetrics.BarLabelWidth, BattleHudMetrics.BarStageLineHeight, new Vector2(0f, 1f));
            UIBuilder.Outlined(_stageLabel, BattleHudStyle.TextOutline, Emberforge.StrokeThin);

            _roundLabel = EmberforgeUI.Label("Round", bar, "", BattleHudMetrics.BarStatusLineFont,
                                             BattleHudStyle.StatusLine, TextAnchor.MiddleLeft);
            UIBuilder.Box(_roundLabel.rectTransform, BattleHudMetrics.BarLabelInset, -BattleHudMetrics.BarStatusLineTop,
                          BattleHudMetrics.BarLabelWidth, BattleHudMetrics.BarStatusLineHeight, new Vector2(0f, 1f));
            UIBuilder.Outlined(_roundLabel, BattleHudStyle.TextOutline, Emberforge.StrokeThin);

            // TEAL, all three. These are the controls that change how the fight in front of you
            // plays out, and none of them is the thing the screen wants pressed - the battle is
            // what the screen wants watched. The one ember button in this whole screen is the
            // primary on the result panel, which is the only place there IS a next tap.
            float x = 0f;
            _speedButton = EmberforgeUI.Button("Speed", bar, "1x", BattleHudMetrics.BarButtonFont, ForgeButtonKind.Secondary);
            UIBuilder.Box(_speedButton.Root, x, 0f, BattleHudMetrics.BarSpeedWidth, BattleHudMetrics.BarButtonHeight,
                          new Vector2(1f, 0.5f));
            _speedButton.OnClick(CycleSpeed);
            x -= BattleHudMetrics.BarSpeedWidth + BattleHudMetrics.BarButtonGap;

            _skipButton = EmberforgeUI.Button("Skip", bar, "SKIP", BattleHudMetrics.BarButtonFont, ForgeButtonKind.Secondary);
            UIBuilder.Box(_skipButton.Root, x, 0f, BattleHudMetrics.BarSkipWidth, BattleHudMetrics.BarButtonHeight,
                          new Vector2(1f, 0.5f));
            _skipButton.OnClick(SkipToEnd);
            x -= BattleHudMetrics.BarSkipWidth + BattleHudMetrics.BarButtonGap;

            // Same row as SPEED and SKIP, to the left of both: the three controls that change how
            // the fight in front of you plays out belong together, and AUTO is the one you set once
            // per stage rather than per battle, so it sits furthest from the thumb's resting edge.
            _autoButton = EmberforgeUI.Button("Auto", bar, "AUTO", BattleHudMetrics.BarButtonFont, ForgeButtonKind.Secondary);
            UIBuilder.Box(_autoButton.Root, x, 0f, BattleHudMetrics.BarAutoWidth, BattleHudMetrics.BarButtonHeight,
                          new Vector2(1f, 0.5f));
            _autoButton.OnClick(ToggleAuto);

            // Top-left, where every campaign screen keeps it. Shown only for a campaign fight (see
            // RefreshHome), and inside the bar so the result panel, which re-raises the bar above
            // itself, never covers it.
            _home = ScreenChrome.HomeDisc(bar, Router, 0f, BattleHudMetrics.BarHomeTop);
        }

        /// <summary>
        /// HOME belongs to the campaign flow: a campaign stage's own fight and a campaign wave's
        /// replay. A Recital floor, a Proving match, a duel, the prologue and a guild dungeon run
        /// each have their own way out and are left exactly as they were. The stage and round
        /// labels step right of the disc while it is up, because the corner is otherwise theirs.
        /// </summary>
        private void RefreshHome()
        {
            bool show = _guildRun != null
                ? _guildRun.Home
                : _recitalFloor < 0 && _provingOpponent == null && _duelOpponent == null
                  && _prologueSquad == null;
            _home.Root.gameObject.SetActive(show);

            float x = show ? BattleHudMetrics.BarLabelInsetWithHome : BattleHudMetrics.BarLabelInset;
            _stageLabel.rectTransform.anchoredPosition = new Vector2(x, _stageLabel.rectTransform.anchoredPosition.y);
            _roundLabel.rectTransform.anchoredPosition = new Vector2(x, _roundLabel.rectTransform.anchoredPosition.y);
        }

        /// <summary>The back key leaves a campaign replay for the wave it came from, the same
        /// step its BACK button takes. A live fight stays unleavable by key, as it always was.</summary>
        public override bool TryStepBack()
        {
            if (_guildRun == null || !_guildRun.Home || _guildRun.OnPrimary == null) return false;
            _guildRun.OnPrimary();
            return true;
        }

        // THE CALLOUT: a slim pill centred just under the top bar, "Unusable Cover · KAEL" for a
        // cast and "Unusable Cover · KAEL · PASSIVE" for a passive. It replaces a 520 x 74 banner
        // that sat 300 above the middle of the screen, over the enemy line and in the climb of
        // the front rank's numbers (and before that a 980 x 132 slab). The bar is the one edge of
        // the frame with no unit under it, so a pill hung from it covers the least of the fight
        // and is still where the eye goes for "what just happened". One line, the ability's own
        // name in gold, the caster after it, PASSIVE quieter still.
        private void BuildCallout()
        {
            _callout = UIBuilder.Node("Callout", Root);
            _callout.anchorMin = Vector2.zero;
            _callout.anchorMax = Vector2.zero;
            _callout.pivot = Vector2.zero;
            _calloutGroup = _callout.gameObject.AddComponent<CanvasGroup>();
            _calloutGroup.alpha = 0f;
            _calloutGroup.blocksRaycasts = false;

            Image plate = _callout.gameObject.AddComponent<Image>();
            plate.sprite = BattleHudStyle.PillPlate;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            plate.raycastTarget = false;

            _calloutText = EmberforgeUI.Label("Text", _callout, "", BattleHudMetrics.CalloutFont,
                                              BattleHudStyle.CalloutCaster, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_calloutText.rectTransform, 0f);
            UIBuilder.Outlined(_calloutText, BattleHudStyle.TextOutline, Emberforge.StrokeThin);

            _callout.gameObject.SetActive(false);
        }

        /// <summary>Puts the callout where BattleHudLayout says, for the text it now holds.</summary>
        private void PlaceCallout(float scale)
        {
            BattleFraming.CanvasRect r = BattleHudLayout.Callout(Screen.width / scale, SafeTopCanvas(scale), _calloutTextWidth);
            _callout.anchoredPosition = new Vector2(r.X, r.Y);
            _callout.sizeDelta = new Vector2(r.Width, r.Height);
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
            // VICTORY or DEFEAT in Cinzel, at display size, with the kit's thick stroke. The one
            // word on this panel that is read from across a room.
            _resultTitle = EmberforgeUI.Title("Title", _resultPanel, "", UITheme.FontDisplay + 16, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultTitle.rectTransform, -460f, 310f, 900f, 110f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_resultTitle, Emberforge.Outline, Emberforge.StrokeThick);

            _resultSub = EmberforgeUI.Label("Sub", _resultPanel, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultSub.rectTransform, -460f, 232f, 860f, 40f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_resultSub, Emberforge.Outline, Emberforge.StrokeThin);

            // 700 tall, not 560, and BOTH LISTS SCROLL. Neither is styling.
            //
            // The spoils list is four rows after an ordinary battle and up to eight after a stage
            // clear - first-clear bonus, gold, shards, Wayfarer XP, a rank up, the next stage
            // unlocking, an armour drop - and the stat list is one row per unit that fought, which
            // on a wave is five heroes and as many enemies as the wave carried. Both were laid out
            // in a fixed box with a VerticalLayoutGroup, which gives every child its preferred
            // height and lets the total run straight out of the bottom of the card. The capture
            // that found it shows the damage table printed through the card's own lower edge.
            //
            // A ScrollRect is the honest answer to a list whose length is a property of the fight:
            // one that fits shows no scrollbar and behaves exactly as before, and one that does
            // not can be reached instead of being drawn through the frame.
            //
            // 700 rather than taller because this card shares the screen with the battle HUD: at
            // 20:9 the canvas is 966, so a 700-tall card centred on it starts at y 133 and clears
            // the HUD's top bar, which ends at 100. An 880-tall card would have run into SKIP.
            RectTransform card = UIBuilder.Node("Rewards", _resultPanel);
            UIBuilder.Box(card, 460f, 0f, 820f, 700f, new Vector2(0.5f, 0.5f));
            EmberforgeUI.PanelOn(card, true);

            // Rewards - always visible, top section of card. A ribbon, because the card has one
            // subject and the tab strip further down is what divides the rest of it.
            EmberforgeUI.TitleRibbon("Heading", card, "SPOILS", 820f - Emberforge.RibbonOverhang * 2f);

            ScrollRect rewardScroll;
            _rewardList = UIBuilder.ScrollList("List", card, out rewardScroll, 4f,
                                               new RectOffset(0, 0, 0, 0));
            UIBuilder.Box(rewardScroll.GetComponent<RectTransform>(), 0f, -44f, 764f, 250f,
                          new Vector2(0.5f, 1f));

            // Divider between rewards and stat breakdown.
            Image dividerImage = EmberforgeUI.Rule("Divider", card);
            UIBuilder.Box(dividerImage.rectTransform, 0f, -302f, 760f, Emberforge.RuleHeight,
                          new Vector2(0.5f, 1f));

            // Stat tab strip — 3 tabs: DEALT / HEALED / TAKEN. Rewards are never behind a tab.
            RectTransform tabStrip = UIBuilder.Node("Tabs", card);
            UIBuilder.Box(tabStrip, 0f, -314f, 780f, 34f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(tabStrip, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            // REAL TABS, not three teal buttons. Three buttons in a row is three things to press
            // and no statement about which one you are looking at; a tab bar says both, and the
            // selected state is a different plate rather than a tint - see EmberforgeUI.Tab.
            string[] tabNames = { "DEALT", "HEALED", "TAKEN" };
            _statTabs = new ForgeTab[tabNames.Length];
            for (int ti = 0; ti < tabNames.Length; ti++)
            {
                int idx = ti;
                ForgeTab tab = EmberforgeUI.Tab(tabNames[ti], tabStrip, 256f, 34f,
                                                delegate { SelectStatTab(idx); });
                _statTabs[ti] = tab;
            }

            ScrollRect statScroll;
            _statsPanel = UIBuilder.ScrollList("Stats", card, out statScroll, 2f,
                                               new RectOffset(0, 0, 0, 0));
            UIBuilder.Box(statScroll.GetComponent<RectTransform>(), 0f, -356f, 764f, 328f,
                          new Vector2(0.5f, 1f));

            // the Debrief line - one sentence, the single biggest reason the fight went this way -
            // sits directly under the outcome it explains, in the left pane's reading order:
            // verdict, cause, then what to do about it (the two buttons below)
            _debriefText = EmberforgeUI.Body("Debrief", _resultPanel, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(_debriefText.rectTransform, -460f, 40f, 820f, 220f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_debriefText, Emberforge.Outline, Emberforge.StrokeThin);

            // Three actions, one of which is gold. Positions and the primary's label are set per
            // result in ShowResult rather than here, because what a player should do next after a
            // win is not what they should do after a loss, and this panel is built once.
            _resultSecondary = EmberforgeUI.Button("AdjustLine", _resultPanel, "ADJUST LINE",
                                                   UITheme.FontBody, ForgeButtonKind.Secondary);
            _resultSecondary.OnClick(AdjustLine);

            _resultTertiary = EmberforgeUI.Button("Rematch", _resultPanel, "REMATCH",
                                                  UITheme.FontBody, ForgeButtonKind.Secondary);
            _resultTertiary.OnClick(Replay);

            // Wired to OnPrimary, not to Replay or Continue directly: UIButton.OnClick APPENDS a
            // listener, so rebinding this per result would stack a handler every fight and one tap
            // would eventually start several. The dispatch flag changes; the wiring never does.
            _resultPrimary = EmberforgeUI.Button("Rematch", _resultPanel, "REMATCH",
                                                 UITheme.FontHeading, ForgeButtonKind.Primary);
            _resultPrimary.OnClick(OnPrimary);

            _resultPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnShow()
        {
            App.SetBackgroundVisible(false);
            _finished = false;
            _progressText = "";
            _resultPanel.gameObject.SetActive(false);
            _callout.gameObject.SetActive(false);
            // RESTORED, not reset. This used to force 1x on every OnShow, which is invisible when a
            // battle is a one-off and actively wrong now a stage is 144 of them: chaining through a
            // wave would have dropped the player back to 1x on every single battle.
            // Stored as an int because the only values the cycle produces are 1, 2 and 4, and
            // because the harness's Unity shim carries PlayerPrefs.GetInt but not GetFloat -
            // widening the shim for a value that was never fractional would be the wrong fix.
            int storedSpeed = PlayerPrefs.GetInt(SpeedPrefKey, 1);
            _speed = (storedSpeed == 2 || storedSpeed == 4) ? storedSpeed : 1;
            _speedButton.SetText(Mathf.RoundToInt(_speed) + "x");
            _skipButton.Interactable = true;

            // Auto only ever applies to a campaign stage with a curve. A Recital floor, a Proving
            // match and a prologue step are each a single fight with nothing to chain into, so the
            // control is hidden rather than shown-and-inert.
            bool chainable = _stage != null && _stage.HasWaveCurve
                             && _recitalFloor < 0 && _provingOpponent == null && _duelOpponent == null
                             && _prologueSquad == null && _guildRun == null;
            _autoButton.Root.gameObject.SetActive(chainable);
            if (!chainable) _auto = false;
            else _auto = PlayerPrefs.GetInt(AutoPrefKey, 0) == 1;
            RefreshAutoButton();
            RefreshHome();

            if (_recitalFloor < 0 && _stage == null) _stage = App.Session.NextStage;
            if (_stage == null)
            {
                // no campaign content: bail out next frame rather than re-entering the router
                App.Notify("No campaign stages are loaded.");
                StartCoroutine(LeaveNextFrame());
                return;
            }

            if (_guildRun != null)
            {
                // No spend point here either: an attempt was stored before it was handed in, and a
                // replay is a record. See SetGuildRun.
                _stageLabel.text = _guildRun.Label;
                _progressText = _guildRun.Progress ?? "";
            }
            else if (_duelOpponent != null)
            {
                // No spend point at all, and that absence is the mode. Checked BEFORE the Proving
                // branch because both carry a BattleLoadout and falling into that one would charge
                // a Brand for a fight that pays nothing - the same ordering rule the prologue
                // branch in ShowResult keeps, for the same reason.
                _stageLabel.text = "FRIENDLY DUEL   " + _duelOpponentName.ToUpperInvariant();
            }
            else if (_provingOpponent != null)
            {
                // The one and only spend point for a Proving attempt, mirroring the Recital branch
                // below: RecitalScreen and ProvingScreen both only PEEK affordability, and a
                // Rematch re-enters here with no gate of its own.
                if (!App.Session.TryStartProvingAttempt(TimeUtil.NowUnix()))
                {
                    App.Notify("Out of Brands.");
                    Router.Show(ScreenId.Proving, false);
                    return;
                }
                _stageLabel.text = "THE PROVING   " + _provingOpponent.Label.ToUpperInvariant();
            }
            else if (_recitalFloor >= 0)
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
                // "1-3 · THE SMOKING ORCHARD" over "WAVE 4 / 12 · BATTLE 7 / 12 · ROUND 2 / 30".
                // The stage on the first line, and on the second everything that counts up while
                // it is fought, outermost first. A stage with no curve has no wave or battle, and
                // its second line is the round alone.
                _stageLabel.text = _stage.DisplayCode + BattleHudStyle.Separator + _stage.Name.ToUpperInvariant();
                if (_stage.HasWaveCurve)
                    _progressText = "WAVE " + _campaignWave + " / " + _stage.WaveCount
                                    + BattleHudStyle.Separator + "BATTLE " + _campaignBattle + " / " + _stage.BattleCount;
            }
            _roundText = "PREPARING";
            RefreshStatusLine();

            BuildWorld();
            RunSimulation();
            App.Audio.EnterBattle(BattleMusicKind());

            if (_playback != null) StopCoroutine(_playback);
            _playback = StartCoroutine(Playback());
        }

        public override void OnHide()
        {
            // ANY EXIT FROM THIS SCREEN ENDS THE CHAIN - the exit button, the back gesture,
            // CONTINUE into the next stage, the prologue hand-off. Auto is a mode you are in while
            // watching a stage run, and leaving says plainly that you are not watching it.
            //
            // The _chaining guard is what makes that possible at all: FightNextBattle goes through
            // OnHide on every hop between two battles of the same chain, so without it the first
            // auto-advance would immediately turn auto off and the chain would stop after one
            // battle. A hop clears the flag and leaves the preference alone; anything else is a
            // real exit and writes it off.
            if (_chaining) _chaining = false;
            else if (_auto) SetAuto(false);

            // stops playback plus every death, drain and floating-text routine, so nothing
            // touches the world objects that are about to be destroyed
            StopAllCoroutines();
            _playback = null;
            _calloutRoutine = null;
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
                // This screen has every event in hand as it plays it and feeds them to the
                // director in Handle, which silences the taps UnitActor would otherwise fire for
                // the same blow. Set once here because the director is built once per stage.
                _world.Director.ExactFeed = true;
            }
            // The viewport BEFORE the world: BuildWorld fits the backdrop to the camera's aspect,
            // and the field has to stop at the top bar's lower edge from its first frame. Setting it
            // afterwards would mean rebuilding, and BattleStage.BuildWorld clears the actors.
            _world.Camera.rect = FieldViewport();
            _world.BuildWorld(_stage.Environment);
#if UNITY_EDITOR
            _framingChecked = false;
#endif
        }

        private float CanvasScale()
        {
            float scale = App.RootCanvas != null ? App.RootCanvas.scaleFactor : 1f;
            return scale > 0f ? scale : 1f;
        }

        /// <summary>The battle camera's viewport for the screen as it is now: under the top bar,
        /// above the safe area's bottom edge, the full width. See BattleHudLayout.Viewport.</summary>
        private Rect FieldViewport()
        {
            Rect safe = SafeAreaRect.Current;
            BattleFraming.CanvasRect v = BattleHudLayout.Viewport(Screen.width, Screen.height,
                                                                  safe.y, safe.y + safe.height, CanvasScale());
            return new Rect(v.X, v.Y, v.Width, v.Height);
        }

        /// <summary>The top of the safe area, in canvas units from the bottom of the screen.</summary>
        private float SafeTopCanvas(float scale)
        {
            Rect safe = SafeAreaRect.Current;
            return (safe.y + safe.height) / scale;
        }

        /// <summary>
        /// Keeps the frame matched to the screen. The safe area and the canvas scale can change
        /// mid-fight (a rotation, a resized Game view), and Unity raises no event for either, so
        /// this polls like SafeAreaRect does. A new viewport only re-fits the backdrop: rebuilding
        /// the world would clear the actors in the middle of their fight. The bar's ground grows
        /// to cover any top inset, and the two pills are placed again for the new shape.
        /// </summary>
        private void SyncFrame(float scale)
        {
            Rect want = FieldViewport();
            if (_world.Camera.rect != want)
            {
                _world.Camera.rect = want;
                _world.FitBackdrop();
            }

            float topInset = Mathf.Max(0f, Screen.height / scale - SafeTopCanvas(scale));
            _barGround.rectTransform.sizeDelta = new Vector2(0f, BattleHudMetrics.BarHeight + topInset);

            if (_callout.gameObject.activeSelf) PlaceCallout(scale);
            PlaceTurnPill(scale);
        }

        /// <summary>
        /// Which battle track this fight asks for, as a key into audio.json's musicByScreen
        /// ("Battle.kind"). Only reads which mode the screen was entered in; the track itself, and
        /// whether one exists, is the cue map's business.
        /// </summary>
        private string BattleMusicKind()
        {
            if (_prologueSquad != null) return "prologue";
            if (_guildRun != null) return "guild";
            if (_duelOpponent != null || _provingOpponent != null) return "proving";
            if (_recitalFloor >= 0) return "recital";
            if (_stage != null && (_stage.IsBoss || _stage.IsMiniboss)) return "boss";
            return "campaign";
        }

        private void RunSimulation()
        {
            int seed = Mathf.Abs(_stage.Id.GetHashCode() * 31
                                 + App.Session.Profile.TotalBattlesWon * 7919
                                 + Mathf.RoundToInt(Time.unscaledTime * 997f));
            if (seed == 0) seed = 1;

            // Three request shapes, one per mode that does not use the player's own line as-is.
            // Checked most-specific first: a prologue fight has no profile at all, so it must not
            // fall through to BuildBattleRequest, which reads Profile.TeamHeroes().
            // A guild dungeon fight arrives already resolved - see SetGuildRun. Nothing is built
            // and nothing is run: the screen populates from the result it was given.
            if (_guildRun != null)
            {
                _result = _guildRun.Result;
                _world.Populate(_result);
                BuildBars();
                return;
            }

            BattleRequest request;
            if (_prologueSquad != null) request = App.Session.BuildPrologueRequest(_stage, _prologueSquad, seed);
            else if (_duelOpponent != null)
            {
                // The SAME request builder the Proving uses, and that is the claim a duel rests
                // on: a line fetched from a friend's snapshot is a BattleLoadout, and nothing in
                // the combat path can tell it from a generated one. There is no duel code in the
                // engine because there is nothing for it to do.
                _duelSeed = seed;
                request = App.Session.BuildProvingRequest(_stage, _duelOpponent, seed);
            }
            else if (_provingOpponent != null) request = App.Session.BuildProvingRequest(_stage, _provingOpponent, seed);
            else
            {
                // The stage as it stands at THIS wave and battle, through the one function every
                // campaign battle in the game takes (the wave resolve and the sweep use it too).
                // BuildBattle returns the stage unchanged when it has no curve, so Recital floors
                // and every synthetic stage resolve exactly as before without a branch of their own.
                request = null;
                _result = App.Session.ResolveLadderBattle(_stage, _campaignWave, _campaignBattle, seed);
            }
            if (request != null) _result = CombatSimulation.Run(request);
            _world.Populate(_result);
            BuildBars();
        }

        // ------------------------------------------------------------------ unit bars

        private void ClearBars()
        {
            foreach (KeyValuePair<int, UnitBar> pair in _bars)
                if (pair.Value.Plate != null && pair.Value.Plate.Root != null) Destroy(pair.Value.Plate.Root.gameObject);
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

            // ONE NAME PER UNIT. A placeholder mannequin wears its name at its feet in the world
            // (UnitRig.BuildNameplate), and the plate now hangs its name in the same place, so a
            // pending hero or an undrawn enemy read its name twice, one over the other. The plate
            // is the one this screen keeps; the rig's label is switched off rather than deleted,
            // because the wave result screen and the Dev Arena show the rig with no plate under it.
            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor actor = _world.Actors[i];
                if (actor.Rig != null && actor.Rig.Nameplate != null) actor.Rig.Nameplate.gameObject.SetActive(false);
            }
        }

        // THE PLATE HANGS UNDER THE FEET. It used to float above the head, which put the back
        // rank's tags against the top of the screen (clamped there, over their own units) and
        // every tag in the path of the damage numbers rising off the same heads. Under the feet it
        // sits on the ground the unit stands on, the rank in front is lower on screen and so is
        // its plate, and the head is left to the numbers. BattleHudMetrics has the sizes and
        // Tools/DevHarness (BattleHudChecks) proves no two plates meet at any formation slot.
        private UnitBar CreateBar(UnitSnapshot snapshot)
        {
            UnitBar bar = new UnitBar();
            bar.Plate = UnitPlate.Create(_barLayer, snapshot.Name, snapshot.IsPlayer);
            bar.Plate.SetEnergy(snapshot.EnergyMax > 0 ? snapshot.StartEnergy / (float)snapshot.EnergyMax : 0f);
            return bar;
        }

        /// <summary>
        /// Fills the energy meter and lights it gold at full. The colour change is the point of
        /// the meter, not decoration on it: in a fight the player cannot influence, "this unit
        /// casts on its next turn" is the only thing worth anticipating, and a fill level read
        /// across five plates at once is much harder to judge than a colour that either is or is
        /// not gold.
        /// </summary>
        private static void SetEnergy(UnitBar bar, float fraction)
        {
            if (bar == null || bar.Plate == null) return;
            bar.Plate.SetEnergy(fraction);
        }

        /// <summary>How far a damage or heal number climbs over its life, in canvas units.</summary>
        private const float FloatRise = 120f;

        private void LateUpdate()
        {
            if (_world == null || _world.Camera == null || Root == null || !Root.gameObject.activeInHierarchy) return;
            float scale = CanvasScale();
            SyncFrame(scale);
#if UNITY_EDITOR
            CheckFramingAgainstCamera(scale);
#endif

            // Each plate is pinned to its unit's feet as the camera sees them this frame, so it
            // follows a lunge and the camera's shake. A unit's feet are its own position: every
            // rig stands on y 0. The plate's offsets from that point are BattleHudLayout's.
            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor actor = _world.Actors[i];
                UnitBar bar;
                if (!_bars.TryGetValue(actor.Snapshot.Id, out bar)) continue;

                Vector3 screen = _world.Camera.WorldToScreenPoint(actor.transform.position);
                bool visible = screen.z > 0f && actor.Alive;
                CanvasGroup group = bar.Plate.Group;
                group.alpha = visible ? 1f : Mathf.Max(0f, group.alpha - Time.deltaTime * 4f);
                if (!visible) continue;

                BattleFraming.CanvasPoint at = BattleHudLayout.PlateOrigin(
                    new BattleFraming.CanvasPoint(screen.x / scale, screen.y / scale), bar.TurnLift);
                bar.Plate.Root.anchoredPosition = new Vector2(at.X, at.Y);
            }
        }

#if UNITY_EDITOR
        private bool _framingChecked;

        /// <summary>
        /// TIES THE HARNESS'S MODEL TO THE REAL CAMERA. Tools/DevHarness proves the plates never
        /// overlap using BattleFraming.Project as the camera; this compares that projection with
        /// Camera.WorldToScreenPoint for every actor's feet and head, once per battle, on the first
        /// frame the camera stands at rest with the actors placed, and warns if any point differs
        /// by more than a pixel. Editor only: it asserts, it never moves anything.
        /// </summary>
        private void CheckFramingAgainstCamera(float scale)
        {
            if (_framingChecked || _world.Actors.Count == 0) return;
            Vector3 home = new Vector3(BattleFraming.CameraX, BattleFraming.CameraY, BattleFraming.CameraZ);
            if ((_world.Camera.transform.localPosition - home).sqrMagnitude > 1e-8f) return;
            _framingChecked = true;

            Rect r = _world.Camera.rect;
            BattleFraming.CanvasRect viewport = BattleHudLayout.ToCanvas(
                new BattleFraming.CanvasRect(r.x, r.y, r.width, r.height), Screen.width / scale, Screen.height / scale);
            float worst = 0f;
            string worstAt = "";
            int points = 0;
            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor actor = _world.Actors[i];
                Vector3[] probes = { actor.transform.position, actor.HeadPosition };
                for (int p = 0; p < probes.Length; p++)
                {
                    Vector3 world = probes[p];
                    BattleFraming.CanvasPoint model = BattleFraming.Project(world.x, world.y, world.z, viewport);
                    Vector3 real = _world.Camera.WorldToScreenPoint(world);
                    float error = Mathf.Max(Mathf.Abs(model.X * scale - real.x), Mathf.Abs(model.Y * scale - real.y));
                    points++;
                    if (error > worst) { worst = error; worstAt = actor.Snapshot.Name + (p == 0 ? " feet" : " head"); }
                }
            }

            string summary = "BattleHud: BattleFraming.Project against Camera.WorldToScreenPoint at " + Screen.width + "x"
                             + Screen.height + ", " + points + " points on " + _world.Actors.Count + " units, worst "
                             + worst.ToString("0.000") + " px" + (worstAt.Length > 0 ? " (" + worstAt + ")" : "");
            if (worst > 1f) Debug.LogWarning(summary + " - the harness's layout model no longer matches the camera");
            else Debug.Log(summary);
        }
#endif

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
            BattleEvent e = _result.Events[index];
            if (_pendingAnimationTail > 0f && OpensBeat(e.Type))
            {
                float tail = _pendingAnimationTail;
                _pendingAnimationTail = 0f;
                yield return Wait(tail);
            }

            // Hit feedback, fed the event itself. Before the switch on purpose: the director reads a
            // shield break off DisplayShield against the amount absorbed and a kill off a unit that
            // is still standing, and the cases below are what subtract the one and end the other.
            // It plays what the record says and decides nothing, so a skip that never reaches
            // this line leaves the fight and its result exactly as they were.
            _world.Director.Play(e);
#if UNITY_EDITOR
            HitJustPlayed = e.Type == BattleEventType.Damage && e.Status == StatusKind.None;
            if (e.Type == BattleEventType.Heal && e.Value > 0) _lastHealAt = Time.unscaledTime;
#endif

            switch (e.Type)
            {
                case BattleEventType.RoundStart:
                    _roundText = RoundText(e.Value);
                    RefreshStatusLine();
                    RebuildTurnStrip(index);
                    yield return Wait(0.10f);
                    break;

                case BattleEventType.TurnStart:
                {
                    UnitBar bar = BarOf(e.SourceId);
                    // TurnPip is checked, not just the wrapper: ClearBars destroys the plate's
                    // GameObject but leaves this UnitBar instance alive, so a non-null wrapper can
                    // still hold a destroyed Image. SettleTurnBar reaches TurnPip through it.
                    if (_activeTurnBar != null && _activeTurnBar != bar && _activeTurnBar.Plate.TurnPip != null)
                        SettleTurnBar(_activeTurnBar);
                    _activeTurnBar = bar;
                    AdvanceTurnStrip(e.SourceId);
                    _skillActive = false;
                    if (bar != null) yield return TurnCue(bar);
                    break;
                }

                case BattleEventType.BasicAttack:
                {
                    _skillActive = false;
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
                    //
                    // Which basic clip: BattleClips.BasicClipFor alternates basic and basic2 by how
                    // many basics this unit has made in the record, resolved to what the unit
                    // actually ships, and the beat is timed off that same clip.
                    string basicClip = source != null && source.Rig != null && source.Rig.Animation != null
                        ? source.Rig.Animation.Resolve(BattleClips.BasicClipFor(_result.Events, index)) : null;
                    float clipDuration = basicClip != null ? source.Rig.Animation.ClipDuration(basicClip) : 0f;
                    if (source != null && clipDuration > 0f)
                    {
                        float impact = clipDuration * source.Rig.Animation.ImpactFraction(basicClip);
                        float outSeconds = Mathf.Clamp(impact, 0.02f, clipDuration);
                        float backSeconds = clipDuration - outSeconds;
                        source.PlayAttackLunge(basicClip, towards, outSeconds, backSeconds, _speed, approach);
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
                        _skillActive = true;
                        App.Audio.Cast(source.Snapshot.Aspect);
                        ShowCallout(e.AbilityName, source.Snapshot.Name, false);
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
                        ? source.Rig.Animation.ClipDuration(BattleClips.Skill) : 0f;
                    if (source != null && clipDuration > 0f)
                    {
                        float impact = clipDuration * source.Rig.Animation.ImpactFraction(BattleClips.Skill);
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
                        ShowCallout(e.AbilityName, source.Snapshot.Name, true);
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
                        if (e.Critical) _world.Shake(0.14f, 0.2f);
                        PlayDamageSound(e, source);

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
                        App.Audio.Shield();
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
                    RefreshControlled(e.TargetId);
                    if (target != null)
                    {
                        Color tint = StatusInfo.IsHarmful(e.Status) ? UITheme.Danger : UITheme.Success;
                        _world.Vfx.Aura(target.transform.position, tint, 1.8f);
                        // A ward has its own sound, from ShieldGain; every other status is a buff or a debuff.
                        if (e.Status != StatusKind.Shield)
                        {
                            if (StatusInfo.IsHarmful(e.Status)) App.Audio.Debuff();
                            else App.Audio.Buff();
                        }
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
                    RefreshControlled(e.TargetId);
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
                    RefreshControlled(e.TargetId);
                    RefreshControlled(e.SourceId);
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

        /// <summary>
        /// The events that open a new action, before which an owed clip follow-through is paid in
        /// full. Everything else in the record is a consequence of the action already on screen
        /// and plays inside its follow-through. See _pendingAnimationTail.
        /// </summary>
        private static bool OpensBeat(BattleEventType type)
        {
            return type == BattleEventType.TurnStart || type == BattleEventType.RoundStart
                   || type == BattleEventType.BasicAttack || type == BattleEventType.AbilityCast
                   || type == BattleEventType.BattleEnd;
        }

        /// <summary>
        /// Puts a unit into or out of its `controlled` rest loop from the status mirror this screen
        /// already keeps off the record. Reads the bar, never the simulation.
        /// </summary>
        private void RefreshControlled(int unitId)
        {
            UnitActor actor = ActorOf(unitId);
            UnitBar bar = BarOf(unitId);
            if (actor == null || bar == null) return;
            actor.SetControlled(bar.Statuses.Exists(BattleClips.Controls));
        }

        private IEnumerator Wait(float seconds)
        {
            // Time spent on anything pays down an owed follow-through: it is on screen either way.
            if (_pendingAnimationTail > 0f) _pendingAnimationTail = Mathf.Max(0f, _pendingAnimationTail - seconds);
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

        /// <summary>Turn pip lights and the plate rises over 0.12s - the "cause" beat before intent/motion/impact.</summary>
        private IEnumerator TurnCue(UnitBar bar)
        {
            float duration = Mathf.Max(0.05f, 0.12f / _speed);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                bar.Plate.SetTurnPip(k);
                bar.TurnLift = Mathf.Lerp(0f, BattleHudMetrics.PlateTurnLift, k);
                yield return null;
            }
            bar.Plate.SetTurnPip(1f);
            bar.TurnLift = BattleHudMetrics.PlateTurnLift;
        }

        /// <summary>Drops a plate out of "acting now" state - called on the previous actor once a new TurnStart fires.</summary>
        private void SettleTurnBar(UnitBar bar)
        {
            bar.TurnLift = 0f;
            bar.Plate.SetTurnPip(0f);
        }

        /// <summary>Repopulates the round's turn order from the TurnStart events between this RoundStart and the next.</summary>
        private void RebuildTurnStrip(int roundStartIndex)
        {
            ClearTurnStrip();
            for (int j = roundStartIndex + 1; j < _result.Events.Count; j++)
            {
                BattleEvent e = _result.Events[j];
                if (e.Type == BattleEventType.RoundStart) break;
                if (e.Type == BattleEventType.TurnStart) _turnOrder.Add(e.SourceId);
            }
            RefreshTurnPill();
        }

        /// <summary>
        /// Moves the turn order on to this unit's turn.
        ///
        /// Walks a cursor rather than matching every entry with this unit id, because Gale's kill
        /// re-entry (DESIGN-ASPECTS section 1) puts a unit into one round's order more than once.
        /// Matching on id alone lit both entries from the first turn and made the extra turn look
        /// like a duplicated frame instead of the second entry it is. The search starts just past
        /// the turn being played (at the start before the round's first), so the n-th TurnStart of
        /// a round is the n-th entry, which is exactly the sequence the order was built from.
        /// </summary>
        private void AdvanceTurnStrip(int unitId)
        {
            int from = _turnActing >= 0 ? _turnActing + 1 : _turnStripCursor;
            for (int i = from; i < _turnOrder.Count; i++)
            {
                if (_turnOrder[i] != unitId) continue;
                _turnStripCursor = i;
                break;
            }
            _turnActing = _turnStripCursor < _turnOrder.Count && _turnOrder[_turnStripCursor] == unitId
                ? _turnStripCursor : -1;
            RefreshTurnPill();
        }

        /// <summary>Hands the pill the acting unit and the ones still to come this round. The
        /// pill draws them and decides nothing.</summary>
        private void RefreshTurnPill()
        {
            if (_turnPill == null) return;
            UnitSnapshot acting = _turnActing >= 0 ? _result.FindUnit(_turnOrder[_turnActing]) : null;
            _upcomingScratch.Clear();
            int from = _turnActing >= 0 ? _turnActing + 1 : _turnStripCursor;
            for (int i = from; i < _turnOrder.Count && _upcomingScratch.Count < BattleHudMetrics.NextMaxUpcoming; i++)
            {
                UnitSnapshot snap = _result.FindUnit(_turnOrder[i]);
                if (snap != null) _upcomingScratch.Add(snap);
            }
            _turnPill.Show(acting, _upcomingScratch);
            PlaceTurnPill(CanvasScale());
        }

        private void PlaceTurnPill(float scale)
        {
            if (_turnPill == null) return;
            Rect safe = SafeAreaRect.Current;
            _turnPill.Place(Screen.width / scale, safe.y / scale);
        }

        private void ClearTurnStrip()
        {
            _turnOrder.Clear();
            _turnStripCursor = 0;
            _turnActing = -1;
            if (_turnPill != null) _turnPill.Hide();
        }

        /// <summary>
        /// The sound of a Damage event, chosen from what the record says about it and played the
        /// moment the event lands, which is the strike: the wait for ImpactFraction happens before
        /// the Damage event is handled, not after. A skill's damage is the skill's aspect landing
        /// (and a critical one adds the critical thump); a basic hit is a hit or a crit; a tick
        /// from a status is a quiet dot.
        /// </summary>
        private void PlayDamageSound(BattleEvent e, UnitActor source)
        {
            // The original tones (audio.json's realAudio off) had one sound per hit and crit and
            // knew nothing of skills or ticks: keep exactly that.
            if (!App.Audio.RealAudio)
            {
                if (e.Critical) App.Audio.Crit();
                else App.Audio.Hit();
                return;
            }
            if (e.Status != StatusKind.None) { App.Audio.Play("battle.dot"); return; }
            if (_skillActive && source != null)
            {
                App.Audio.SkillImpact(source.Snapshot.Aspect);
                if (e.Critical) App.Audio.Crit();
                return;
            }
            if (e.Critical) App.Audio.Crit();
            else App.Audio.Hit();
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
            bar.Plate.SetHp(ratio, actor.MaxHp > 0f ? Mathf.Clamp01(actor.DisplayShield / actor.MaxHp) : 0f);
            StartCoroutine(DrainGhost(bar, ratio));
        }

        private IEnumerator DrainGhost(UnitBar bar, float target)
        {
            Image ghost = bar.Plate.HpGhost;
            float current = ghost.fillAmount;
            if (current <= target) { UIBuilder.SetBar(ghost, target); yield break; }
            yield return Wait(0.16f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3.2f * _speed;
                if (ghost == null) yield break;
                UIBuilder.SetBar(ghost, Mathf.Lerp(current, target, t));
                yield return null;
            }
            if (ghost != null) UIBuilder.SetBar(ghost, target);
        }

        /// <summary>
        /// The plate's status icons, from this screen's mirror of the record. One icon per kind
        /// with its depth as a small digit, so Ember's stacking (DESIGN-ASPECTS section 6) still
        /// shows: three Burn entries are one Burn icon marked 3, never three icons. Harmful kinds
        /// first, then Shield, then buffs, then the inert bookkeeping kinds, three at most and a
        /// "+N" for the rest - StatusIcons.Compose decides, and the harness checks it. The row was
        /// a line of short text tags (BRN x3 GRD) in one warning colour, which said a unit carried
        /// something and left the player to decode whether it was good or bad.
        /// </summary>
        private void RefreshStatusText(UnitBar bar)
        {
            int overflow = StatusIcons.Compose(bar.Statuses, _iconScratch);
            bar.Plate.ShowStatuses(_iconScratch, overflow);
        }

        private readonly List<StatusIcon> _iconScratch = new List<StatusIcon>();

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
        private void SyncStatusCount(UnitBar bar, StatusKind kind, int count)
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

        /// <summary>
        /// "Unusable Cover · KAEL", and " · PASSIVE" after a passive. The pill is as wide as the
        /// line plus its padding, up to CalloutMaxWidth; a line longer than that is set smaller
        /// rather than run out of the pill.
        /// </summary>
        private void ShowCallout(string ability, string caster, bool passive)
        {
            string text = "<color=#" + ColorUtility.ToHtmlStringRGB(BattleHudStyle.CalloutAbility) + ">"
                          + (ability ?? "") + "</color>" + BattleHudStyle.Separator
                          + (caster ?? "").ToUpperInvariant();
            if (passive)
                text += BattleHudStyle.Separator + "<color=#" + ColorUtility.ToHtmlStringRGB(BattleHudStyle.CalloutPassive)
                        + ">PASSIVE</color>";
            _calloutText.text = text;
            _calloutText.fontSize = BattleHudMetrics.CalloutFont;

            float room = BattleHudMetrics.CalloutMaxWidth - BattleHudMetrics.CalloutPadding * 2f;
            float width = _calloutText.preferredWidth;
            if (width > room)
            {
                _calloutText.fontSize = Mathf.Max(12, Mathf.FloorToInt(BattleHudMetrics.CalloutFont * room / width));
                width = Mathf.Min(room, _calloutText.preferredWidth);
            }
            _calloutTextWidth = width;
            PlaceCallout(CanvasScale());

            _callout.gameObject.SetActive(true);
            if (_calloutRoutine != null) StopCoroutine(_calloutRoutine);
            _calloutRoutine = StartCoroutine(CalloutRoutine());
        }

        private IEnumerator CalloutRoutine()
        {
            _calloutGroup.alpha = 0f;
            float t = 0f;
            while (t < 0.12f) { t += Time.deltaTime * _speed; _calloutGroup.alpha = t / 0.12f; yield return null; }
            _calloutGroup.alpha = 1f;
            yield return Wait(0.55f);
            t = 0f;
            while (t < 0.22f) { t += Time.deltaTime * _speed; _calloutGroup.alpha = 1f - t / 0.22f; yield return null; }
            _calloutGroup.alpha = 0f;
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
            item.Label = EmberforgeUI.Label("Text", item.Root, "", UITheme.FontHeading, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(item.Label.rectTransform, 0f);
            UIBuilder.Outlined(item.Label, Emberforge.Outline, 2.4f);
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

            // Callers pass HeadPosition. The number used to have to clear a tag anchored on that
            // same head (the "58 on top of the Sundered Colossus's name" case); the plate now hangs
            // under the feet, so the head is the number's alone and it starts just above it.
            start.y += BattleHudMetrics.FloatSpawnAboveHead;

            // Jitter was +/-34, which was more than half the old 120-unit tag width, so a number
            // could start closer to a NEIGHBOUR's unit than to the one that produced it. On a five-unit
            // line that is the whole attribution problem: the number was correct and unreadable as
            // to whose it was. +/-9 still separates two hits landing on the same unit in the same
            // frame, which is all the jitter was ever for.
            start.x += Random.Range(-9f, 9f);

            // Headroom is taken out of the CLIMB, not out of the spawn point. The old clamp moved
            // start.y down when the unit sat near the top of the screen, which detached the number
            // from its unit and parked several of them on one arbitrary line - numbers hanging in
            // open sky belonging to nobody. A number that starts on its owner and rises a shorter
            // distance is still attributable; one that starts somewhere else never was.
            // The climb stops with the number's centre FloatStopBelowBar, plus half a line, under
            // the top bar, so a number off the back rank's tallest unit never runs into the bar.
            float ceiling = BattleHudLayout.BarBottom(SafeTopCanvas(canvasScale))
                            - BattleHudMetrics.FloatStopBelowBar - BattleHudMetrics.FloatHalfLine;
            float climb = Mathf.Clamp(ceiling - start.y, 24f, FloatRise);

            float duration = 0.75f / _speed;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                item.Root.anchoredPosition = start + new Vector2(0f, climb * Mathf.Sqrt(k));
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
            // A replay holds a result somebody already has. It reaches no grant below, and it does
            // not ask for a save either, so watching one cannot touch the save file.
            if (_guildRun == null || !_guildRun.IsReplay) App.RequestSave();

            _resultTitle.text = _result.Victory ? "VICTORY" : "DEFEAT";
            _resultTitle.color = _result.Victory ? UITheme.Gold : UITheme.Danger;
            _resultSub.text = _result.Rounds + " ROUNDS"
                              + (_result.TimedOut ? "  -  TIME LIMIT REACHED" : "")
                              + "  -  BANNER AT " + Mathf.RoundToInt(_result.PlayerHpRatio * 100f) + "%";

            // one plain sentence naming the single biggest reason this fight went the way it did -
            // win or lose, per DESIGN-FIRST-TEN-MINUTES: "a one-line cause... win or lose"
            // The Debrief already writes a real sentence rather than a number dump - that part was
            // never missing. What it did not do was let a player SEE the cause without reading the
            // whole line, so the clause it already computed is surfaced as a short lead-in tag.
            //
            // Built from DebriefResult.Clause rather than by string-matching the sentence, which is
            // exactly why that enum exists ("lets a caller (or a test) check the cause without
            // string-matching"). A clause with no obvious short name falls through to no tag at all
            // rather than to a vague one - "OUTCOME" in front of a sentence explains nothing and
            // costs a line of width.
            DebriefResult debrief = Debrief.Summarize(_result);
            string cause = CauseTag(debrief.Clause);
            _debriefText.text = string.IsNullOrEmpty(cause)
                ? debrief.Text
                : "<color=#" + ColorUtility.ToHtmlStringRGB(CauseTint(debrief.Clause)) + ">"
                  + cause + "</color>   " + debrief.Text;

            ComputeStats();
            SelectStatTab(0);

            for (int i = _rewardList.childCount - 1; i >= 0; i--) Destroy(_rewardList.GetChild(i).gameObject);

            // The prologue is checked FIRST and this ordering is load-bearing, not stylistic.
            // ShowCampaignResult is the else of this chain, and it calls GrantBattleResult, which
            // on a win writes the stage id into ClearedStageIds, adds gold, shards and account XP,
            // and refreshes the derived StagesCleared count - permanently, into the save, for a tutorial fight against a
            // synthetic stage id. A prologue step that reached that branch would not merely show
            // the wrong panel; it would corrupt the campaign progress of an account that has not
            // started the campaign. ShowPrologueResult grants nothing at all.
            if (_prologueSquad != null) ShowPrologueResult();
            else if (_guildRun != null) ShowGuildResult();
            else if (_duelOpponent != null) ShowDuelResult();
            else if (_provingOpponent != null) ShowProvingResult();
            else if (_recitalFloor >= 0) ShowRecitalResult();
            else ShowCampaignResult();

            // ---------------------------------------------------------------- what to do next
            // Before this, a win offered ADJUST LINE (which leaves for Heroes) and REMATCH (which
            // replays the stage just cleared), and the gold one - the button a thumb goes to - was
            // the one that made no progress. The stage the win had just unlocked was announced in
            // the spoils and then not reachable from the screen announcing it.
            //
            // So the gold slot goes to CONTINUE whenever there is somewhere to continue TO. The
            // panel keeps exactly one gold button by swapping the primary's identity rather than
            // adding a third: two gold buttons side by side would restate the same "which of these
            // is the next tap?" question this exists to answer.
            //
            // ORDER MATTERS, and it is why this sits here rather than up with the title and the
            // Debrief line where it reads more naturally. NextStage is derived from
            // Profile.ClearedStageIds (the first stage whose id is not in it), and the clear is
            // not recorded until GrantBattleResult, which ShowCampaignResult calls. Computed any earlier, NextStage still returns the stage
            // just fought, the id comparison below matches, and CONTINUE never appears on the one
            // screen it exists for. Measured, not assumed: the first build of this fix put the
            // block above and a live win still showed REMATCH.
            //
            // The id comparison is what keeps this honest once the campaign is finished, since
            // NextStage returns the LAST stage rather than null in that case, so a bare null check
            // would offer CONTINUE back into the fight just completed. A Recital run is excluded
            // outright - it has floors, not campaign stages, and NextStage means nothing there.
            // So is a Proving attempt, for the same reason and one the merge surfaced: it
            // clears _recitalFloor, so the Recital check alone stopped being exhaustive the
            // moment a third battle mode existed, and a Proving win would have offered
            // CONTINUE into a campaign stage that had nothing to do with it.
            // A prologue step is excluded here for the same reason the other two modes are, and
            // more so: NextStage is the first UNCLEARED campaign stage, which for a brand-new
            // account is ch1_1 - so a prologue win would offer CONTINUE straight into the real
            // campaign, skipping the entire scripted ending this feature exists to deliver.
            StageDef nextStage = (_result.Victory && _recitalFloor < 0
                                  && _provingOpponent == null && _duelOpponent == null
                                  && _prologueSquad == null && _guildRun == null)
                ? App.Session.NextStage : null;
            bool canContinue = nextStage != null && (_stage == null || nextStage.Id != _stage.Id);

            // WHEN THERE IS NOWHERE TO CONTINUE TO, THE GOLD BUTTON LEAVES.
            //
            // A loss used to offer ADJUST LINE and REMATCH and nothing else, so the only ways off
            // the screen were to refight the thing that just beat you or to walk to the banner -
            // and neither is "I have read the Debrief, take me back to the map". The Debrief's own
            // cause line ("OUTSPED", "WRONG ASPECTS") is an instruction to go and change
            // something, which means leaving, and there was no button for it.
            //
            // The rule is deliberately not "on defeat" but "whenever CONTINUE is not offered",
            // because the identical dead end existed on every result that has no next stage: a win
            // that finished the campaign, a Recital floor, a Proving match. One rule closes all of
            // them, and a special case for defeat would have left the other three exactly as they
            // were. It leads back to the screen the fight was STARTED from rather than to Campaign
            // unconditionally - a Proving loss belongs back at the Proving, not on a stage list.
            //
            // REMATCH keeps existing, demoted to the middle slot. It is still the right answer
            // sometimes; it is no longer the only one.
            // THE GUILD DUNGEON'S BUTTONS ARE ITS FLOW'S. The gold one goes forward - to the
            // result screen after an attempt, into the floor after a replay (RUN IT YOURSELF, the
            // handover) - the teal one goes back to wherever the fight was opened from, and the
            // middle one is WATCH AGAIN, which replays the same result because there is no other.
            if (_guildRun != null)
            {
                _primaryAction = PrimaryAction.Guild;
                _resultPrimary.SetText(_guildRun.PrimaryLabel);
                _resultPrimary.Root.name = "GuildPrimary";
                _resultSecondary.SetText(_guildRun.SecondaryLabel);
                _resultSecondary.Root.gameObject.SetActive(!string.IsNullOrEmpty(_guildRun.SecondaryLabel));
                _resultTertiary.SetText("WATCH AGAIN");
                _resultTertiary.Root.gameObject.SetActive(true);
                UIBuilder.Box(_resultSecondary.Root, -460f, -170f, 620f, 96f, new Vector2(0.5f, 0.5f));
                UIBuilder.Box(_resultTertiary.Root, -460f, -276f, 620f, 96f, new Vector2(0.5f, 0.5f));
                UIBuilder.Box(_resultPrimary.Root, -460f, -400f, 620f, 132f, new Vector2(0.5f, 0.5f));
                _resultPanel.gameObject.SetActive(true);
                _resultPanel.SetAsLastSibling();
                if (_topHud != null) _topHud.SetAsLastSibling();
                _resultGroup.alpha = 1f;
                return;
            }

            // Every other mode speaks the panel's own words, which the branch above can have
            // replaced on a previous fight: this screen is built once and reused.
            _resultSecondary.SetText("ADJUST LINE");
            _resultTertiary.SetText("REMATCH");

            // THE PROLOGUE OWNS ITS OWN EXIT, AND IT IS THE ONLY ONE.
            //
            // Every other mode ends on three buttons: ADJUST LINE, REMATCH, and a forward action.
            // All three are wrong here. ADJUST LINE walks to the Heroes screen mid-story, on an
            // account whose roster is not yet the squad on screen. REMATCH re-runs a fight whose
            // outcome the script does not depend on but whose PLACE in the script it does - and on
            // step 8 it offers to refight the loss the story is built around, which is an offer to
            // grind against a wall that cannot be beaten by design. So both are hidden and the
            // gold button is the only way off this panel, which is exactly the shape a scripted
            // sequence wants: one forward.
            if (_prologueSquad != null)
            {
                _primaryAction = PrimaryAction.Prologue;
                _resultPrimary.SetText("CONTINUE");
                _resultPrimary.Root.name = "PrologueContinue";
                _resultSecondary.Root.gameObject.SetActive(false);
                _resultTertiary.Root.gameObject.SetActive(false);
                UIBuilder.Box(_resultPrimary.Root, -460f, -400f, 620f, 132f, new Vector2(0.5f, 0.5f));

                _resultPanel.gameObject.SetActive(true);
                _resultPanel.SetAsLastSibling();
                // AND THEN THE CONTROL STRIP BACK ON TOP OF IT.
                //
                // The result panel is a full-screen overlay, so it swallowed taps on AUTO - which
                // is precisely the moment a player decides they want it. Seeing "battle 3 of 144"
                // is what makes someone reach for a chain mode, and until this they had to dismiss
                // the panel, start the next fight and hit it mid-battle instead. Found by tapping
                // it in Play mode and watching the raycast land on Result.
                //
                // Raising the whole strip rather than just AUTO keeps SPEED reachable there too,
                // which has the same argument, and costs nothing: the bar is 100 units at the very
                // top and the panel's own content starts below it.
                if (_topHud != null) _topHud.SetAsLastSibling();
                _resultGroup.alpha = 1f;
                return;
            }

            // Restored for every other mode, since the branch above can have switched them off on
            // a previous fight and this screen is built once and reused.
            _resultSecondary.Root.gameObject.SetActive(true);

            // MID-STAGE, THE FORWARD ACTION IS THE NEXT BATTLE, NOT THE NEXT STAGE.
            //
            // CONTINUE means "the stage you just cleared unlocked another one", which is false
            // 143 times out of 144 here. Offering it would walk the player out of a stage they are
            // 40 battles into and quietly abandon that position. REMATCH is dropped for the same
            // reason it is dropped in the prologue: re-fighting a battle already won cannot
            // advance anything, and the one button that matters should be unmissable.
            if (_midStageAdvance)
            {
                _primaryAction = PrimaryAction.NextBattle;
                _resultPrimary.SetText("NEXT BATTLE");
                _resultPrimary.Root.name = "NextBattle";
                _resultTertiary.Root.gameObject.SetActive(false);
                UIBuilder.Box(_resultSecondary.Root, -460f, -170f, 620f, 96f, new Vector2(0.5f, 0.5f));
                UIBuilder.Box(_resultPrimary.Root, -460f, -400f, 620f, 132f, new Vector2(0.5f, 0.5f));

                _resultPanel.gameObject.SetActive(true);
                _resultPanel.SetAsLastSibling();
                // AND THEN THE CONTROL STRIP BACK ON TOP OF IT.
                //
                // The result panel is a full-screen overlay, so it swallowed taps on AUTO - which
                // is precisely the moment a player decides they want it. Seeing "battle 3 of 144"
                // is what makes someone reach for a chain mode, and until this they had to dismiss
                // the panel, start the next fight and hit it mid-battle instead. Found by tapping
                // it in Play mode and watching the raycast land on Result.
                //
                // Raising the whole strip rather than just AUTO keeps SPEED reachable there too,
                // which has the same argument, and costs nothing: the bar is 100 units at the very
                // top and the panel's own content starts below it.
                if (_topHud != null) _topHud.SetAsLastSibling();
                _resultGroup.alpha = 1f;
                return;
            }

            if (canContinue)
            {
                _primaryAction = PrimaryAction.Continue;
                _resultPrimary.SetText("CONTINUE");
                _resultPrimary.Root.name = "Continue";
            }
            else
            {
                _primaryAction = PrimaryAction.Exit;
                _exitScreen = OriginScreen();
                _resultPrimary.SetText(ExitLabel());
                _resultPrimary.Root.name = "Exit";
            }

            // Three buttons on every result now, in one layout: the two replay-shaped options
            // above, the forward action gold and taller below them. The win branch already read
            // this way and there is no longer a reason for a loss to read differently.
            _resultTertiary.Root.gameObject.SetActive(true);
            UIBuilder.Box(_resultSecondary.Root, -460f, -170f, 620f, 96f, new Vector2(0.5f, 0.5f));
            UIBuilder.Box(_resultTertiary.Root, -460f, -276f, 620f, 96f, new Vector2(0.5f, 0.5f));
            UIBuilder.Box(_resultPrimary.Root, -460f, -400f, 620f, 132f, new Vector2(0.5f, 0.5f));

            _resultPanel.gameObject.SetActive(true);
            _resultPanel.SetAsLastSibling();
                // AND THEN THE CONTROL STRIP BACK ON TOP OF IT.
                //
                // The result panel is a full-screen overlay, so it swallowed taps on AUTO - which
                // is precisely the moment a player decides they want it. Seeing "battle 3 of 144"
                // is what makes someone reach for a chain mode, and until this they had to dismiss
                // the panel, start the next fight and hit it mid-battle instead. Found by tapping
                // it in Play mode and watching the raycast land on Result.
                //
                // Raising the whole strip rather than just AUTO keeps SPEED reachable there too,
                // which has the same argument, and costs nothing: the bar is 100 units at the very
                // top and the panel's own content starts below it.
                if (_topHud != null) _topHud.SetAsLastSibling();
            _resultGroup.alpha = 1f;
        }

        private void ShowCampaignResult()
        {
            _midStageAdvance = false;

            // A WAVE-CURVE STAGE PAYS ONCE, ON ITS LAST BATTLE.
            //
            // DESIGN-CAMPAIGN-STRUCTURE.md keeps the repeat cap and idle income at Stage
            // granularity, so 143 of a stage's 144 wins must move the cursor and pay nothing.
            // GrantBattleResult is what pays, records a first clear and spends a daily repeat, so
            // it cannot be reached until the stage is actually finished - calling it per battle
            // would multiply every stage's income by 144 and hand out 144 first clears.
            //
            // A LOST battle deliberately changes nothing: RecordBattleLost changes no state (it only reports the loss to the play-stats tap),
            // so the cursor still points at the battle that was just lost and the next attempt
            // re-fights exactly that one. Not the wave, not the stage.
            BattleRewards rewards = null;
            if (_stage != null && _stage.HasWaveCurve)
            {
                if (!_result.Victory)
                {
                    App.Session.RecordBattleLost(_stage, _campaignWave, _campaignBattle);

                    // A LOSS ALWAYS STOPS THE CHAIN, and turns the toggle off rather than pausing
                    // it. Defeat retries the SAME battle, so a chain that kept going would re-fight
                    // an encounter it has just been shown it cannot win, forever, burning the
                    // player's evening on a wall. Turning it off rather than leaving it armed makes
                    // continuing a decision: the player has to look at what stopped them and opt
                    // back in.
                    if (_auto)
                    {
                        SetAuto(false);
                        App.Notify("Auto stopped - wave " + _campaignWave + ", battle " + _campaignBattle + " was lost.");
                    }
                }
                else
                {
                    // Through RecordLadderWin, the win the wave resolve takes, so a wave's drip
                    // share and the stage clear net of it are paid here exactly as they are there.
                    LadderWin win = App.Session.RecordLadderWin(_stage, _campaignWave, _campaignBattle, TimeUtil.NowUnix());
                    if (win.StageCleared) rewards = win.Rewards;
                }
                if (_result.Victory && rewards == null)
                {
                    _midStageAdvance = true;
                    ShowWaveProgressResult();

                    // The chain. Deliberately routed through the SAME FightNextBattle the gold
                    // button calls rather than a private fast path, so auto and manual cannot drift
                    // apart - if one is ever broken, both are.
                    //
                    // One frame of delay, not zero: FightNextBattle runs OnHide/OnShow, and calling
                    // that from inside the result-building code would tear down the panel currently
                    // being built. The wait also leaves the checkpoint readable rather than
                    // strobing 144 panels past unread.
                    if (_auto) StartCoroutine(AutoAdvanceNext());
                    return;
                }
            }

            if (rewards == null) rewards = App.Session.GrantBattleResult(_stage, _result.Victory, TimeUtil.NowUnix());

            if (_result.Victory)
            {
                AddClearRewardRows(_rewardList, rewards);
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
        /// <summary>
        /// The spoils box for a battle that advanced a stage without finishing it. Says where the
        /// player is and what is left, and pays nothing, because nothing was earned yet - the
        /// stage's gold, shards and XP all land on its last battle.
        /// </summary>
        private void ShowWaveProgressResult()
        {
            int total = _stage.TotalBattles;
            int done = CampaignWaveGenerator.BattleOrdinal(_stage, _campaignWave, _campaignBattle);

            AddRewardRow("WAVE", _campaignWave + " OF " + _stage.WaveCount, UITheme.TextPrimary);
            AddRewardRow("BATTLE", _campaignBattle + " OF " + _stage.BattleCount, UITheme.TextPrimary);
            AddRewardRow("STAGE PROGRESS", done + " / " + total, UITheme.Gold);
            // "PAID" rather than "SPOILS": the box this sits in is already headed SPOILS, and the
            // duplicate read as a stutter on screen. Caught by looking at the panel, not the code.
            AddRewardRow("PAID", "ON STAGE CLEAR", UITheme.TextMuted);
            App.Audio.Victory();
        }

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

        /// <summary>
        /// The Proving's result. Pays Valor and nothing else - see ProvingRewards for why the mode
        /// deliberately touches no existing economy.
        ///
        /// A loss still shows a Valor row rather than "nothing recovered": the Brand was spent on
        /// entry either way, and a result screen that reported zero after charging for the attempt
        /// would read as a bug. The standing line is shown on both branches because standing moving
        /// down is as much a result as it moving up.
        /// </summary>
        /// <summary>
        /// A duel's result: the outcome, the seed, and a sentence saying that is all there is.
        ///
        /// GRANTS NOTHING, and the method is short because that is the feature rather than an
        /// omission. There is no rewards call, no standing, no attempt counter, no write to the
        /// save - the only thing this mode leaves behind is whatever the player now thinks about
        /// their line. ShowResult's App.RequestSave() above still runs, because it runs for every
        /// mode, and it writes a profile that this branch has not touched.
        ///
        /// The seed is on the panel deliberately. A duel is the one fight here that two people
        /// might disagree about, and the engine is a pure function of its request - so the seed
        /// plus the two lines IS the fight, and showing it is what turns "we would both get the
        /// same result" from an architectural claim into something either of them can check.
        /// </summary>
        private void ShowDuelResult()
        {
            // The sentence goes in the SUBTITLE, directly under VICTORY or DEFEAT, rather than
            // among the reward rows. A player who has just won reads the big word and stops; if
            // "nothing is at stake" is three rows down among things that look like spoils, the one
            // message this mode has to deliver is the one they skip.
            _resultSub.text = "FRIENDLY DUEL, NOTHING IS AT STAKE   -   " + _result.Rounds + " ROUNDS"
                            + (_result.TimedOut ? "   -   TIME LIMIT REACHED" : "");

            AddRewardRow("AGAINST", _duelOpponentName, UITheme.TextPrimary);
            AddRewardRow("SEED", _duelSeed.ToString(), UITheme.TextMuted);
            AddRewardRow("REWARDS", "NONE", UITheme.TextMuted);

            if (_result.Victory) App.Audio.Victory(); else App.Audio.Defeat();
        }

        /// <summary>
        /// A guild dungeon attempt or replay: the rows its flow wrote, and nothing granted. The
        /// subtitle says which of the two this is, under VICTORY or DEFEAT, for the reason the duel
        /// puts "nothing is at stake" there - it is the line that gets read.
        /// </summary>
        private void ShowGuildResult()
        {
            _resultSub.text = _guildRun.Subtitle;
            for (int i = 0; i < _guildRun.Rows.Count; i++)
                AddRewardRow(_guildRun.Rows[i].Key, _guildRun.Rows[i].Value,
                             i == 0 ? UITheme.TextPrimary : UITheme.TextMuted);
            if (_result.Victory) App.Audio.Victory(); else App.Audio.Defeat();
        }

        private void ShowProvingResult()
        {
            ProvingRewards rewards = App.Session.GrantProvingResult(_result.Victory, TimeUtil.NowUnix());

            AddRewardRow("VALOR", "+" + rewards.Valor, UITheme.Gold);
            if (_result.Victory)
            {
                if (rewards.NewBest) AddRewardRow("NEW BEST STANDING", rewards.NewStanding.ToString(), UITheme.Success);
                else AddRewardRow("STANDING", rewards.Standing + "  ->  " + rewards.NewStanding, UITheme.Success);
                App.Audio.Victory();
            }
            else
            {
                AddRewardRow("STANDING", rewards.Standing + "  ->  " + rewards.NewStanding, UITheme.TextMuted);
                App.Audio.Defeat();
            }
        }

        /// <summary>
        /// A two-or-three word name for why the fight went this way, from the clause the Debrief
        /// already resolved. Deliberately not a sentence - the sentence is already there, and this
        /// exists so the cause survives being glanced at rather than read.
        ///
        /// Fallback returns empty on purpose. It is the clause that fires when nothing else did,
        /// so there is no cause to name, and labelling it anyway would be inventing a reason.
        /// </summary>
        internal static string CauseTag(DebriefClause clause)
        {
            // Every clause Debrief can produce is about what happened TO the player's line -
            // AbilityDominant only totals damage where target.IsPlayer, Resisted counts the
            // player's own afflictions being shrugged off - so each tag can say which way it went
            // instead of naming a bare noun. "ASPECT", "SPEED" and "UNSPENT" named a topic and
            // left the direction to the sentence beside them, which is the opposite of what a
            // glance-first label is for: on a VICTORY, "ONE ABILITY" over a line about an enemy's
            // flail read as praise for one of your own.
            switch (clause)
            {
                case DebriefClause.Timeout: return "RAN OUT OF ROUNDS";
                case DebriefClause.AbilityDominant: return "ONE ENEMY ABILITY";
                case DebriefClause.Aspect: return "WRONG ASPECTS";
                case DebriefClause.Resisted: return "AFFLICTIONS RESISTED";
                case DebriefClause.Speed: return "OUTSPED";
                case DebriefClause.UnspentActive: return "ABILITY UNUSED";
                default: return "";
            }
        }

        /// <summary>
        /// Colour for the cause tag, reusing tones this screen and UITheme already carry rather
        /// than introducing a palette for one label. Warning for the two clauses that name
        /// something the player could have done differently, Energy for the aspect wheel because
        /// that is the colour the aspect chips already use, TextMuted for the rest.
        /// </summary>
        internal static Color CauseTint(DebriefClause clause)
        {
            switch (clause)
            {
                case DebriefClause.Timeout:
                case DebriefClause.UnspentActive: return UITheme.Warning;
                case DebriefClause.Aspect: return UITheme.Energy;
                default: return UITheme.TextMuted;
            }
        }

        private void AddRewardRow(string label, string value, Color tint)
        {
            AddRewardRow(_rewardList, label, value, tint);
        }

        private static void AddRewardRow(Transform list, string label, string value, Color tint)
        {
            Widgets.StatRow(label, list, label, value, tint, 44f);
        }

        /// <summary>
        /// The stage-clear reward rows, built into any list so this screen and the wave result
        /// screen print the same rows for the same BattleRewards.
        /// </summary>
        internal static void AddClearRewardRows(Transform list, BattleRewards rewards)
        {
            if (rewards.FirstClear) AddRewardRow(list, "FIRST CLEAR BONUS", "INCLUDED", UITheme.Success);
            // Why the purse below reads zero, said before the player reads it as a bug.
            else if (rewards.RepeatCapped) AddRewardRow(list, "DAILY REPEAT CAP", "REACHED", UITheme.Warning);
            else AddRewardRow(list, "PAID REPEATS LEFT TODAY", rewards.RepeatClearsRemaining.ToString(), UITheme.TextMuted);
            AddRewardRow(list, "GOLD", "+" + rewards.Gold, UITheme.Gold);
            AddRewardRow(list, "EMBER SHARDS", "+" + rewards.Shards, UITheme.Shard);
            AddRewardRow(list, "WAYFARER XP", "+" + rewards.AccountXp, UITheme.Shard);
            if (rewards.PlayerLevelsGained > 0)
                AddRewardRow(list, "RANK UP", "+" + rewards.PlayerLevelsGained, UITheme.Success);
            if (rewards.UnlockedNextStage) AddRewardRow(list, "NEW STAGE UNLOCKED", "AHEAD", UITheme.Ember);
            // Named by its temper rather than just "ARMOR", because the temper is the only part
            // of the drop a player can judge before opening the inventory.
            // The row used to put "HONED BOOTS" in the label and "Roadworn Boots" in the
            // value, which reads as two names for one object. Only one of them ever was a
            // name: "Roadworn Boots" is the item, from armor.json's Boots list, and "Honed" is
            // its TEMPER - the Rough/Honed/Gilded/Radiant rung, a rarity. Every other screen
            // already gets this right, showing the name with the temper as a chip beside it,
            // so the Debrief was the only place inventing a second title. Label names the kind
            // of reward, value carries the item and its rung in the order the Bag shows them.
            if (rewards.ArmorDrop != null)
                AddRewardRow(list, "ARMOR DROP",
                             rewards.ArmorDrop.Name + "  -  " + ArmorInfo.TemperLabel(rewards.ArmorDrop.Temper).ToUpperInvariant(),
                             UITheme.ArmorTemperColor(rewards.ArmorDrop.Temper));
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
                EmberforgeUI.SetTabSelected(_statTabs[i], i == index);
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
            Text hdrText = EmberforgeUI.Label("Text", hdrNode, header, UITheme.FontTiny - 2,
                                              Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(hdrText.rectTransform, 12f, 0f, 0f, 0f);
            UIBuilder.Outlined(hdrText, Emberforge.Outline, Emberforge.StrokeThin);

            for (int i = 0; i < units.Count; i++)
            {
                int val;
                statMap.TryGetValue(units[i].Id, out val);
                Color tint = i == 0 ? UITheme.Gold : UITheme.TextPrimary;
                Widgets.StatRow(units[i].Name, _statsPanel, units[i].Name, val.ToString(), tint, 36f);
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
            PlayerPrefs.SetInt(SpeedPrefKey, Mathf.RoundToInt(_speed));
            App.Audio.Tap();
        }

        private void ToggleAuto()
        {
            App.Audio.Tap();
            SetAuto(!_auto);
        }

        /// <summary>
        /// One place the flag moves, so every reason auto turns off - a defeat, leaving the screen,
        /// ADJUST LINE - writes the preference the same way. A player who is stopped by a loss and
        /// comes back should find the toggle off, because that is what they were last told.
        /// </summary>
        private void SetAuto(bool on)
        {
            _auto = on;
            PlayerPrefs.SetInt(AutoPrefKey, on ? 1 : 0);
            RefreshAutoButton();
        }

        private void RefreshAutoButton()
        {
            if (_autoButton == null) return;
            _autoButton.SetText(_auto ? "AUTO ON" : "AUTO");
            _autoButton.Label.color = _auto ? UITheme.Gold : UITheme.TextDim;
        }

        private void SkipToEnd()
        {
            if (_finished) return;
            App.Audio.Tap();
            if (_playback != null) { StopCoroutine(_playback); _playback = null; }
            // An ability callout caught mid-fade by the skip has nobody left to fade it out, so it
            // used to stay on screen, half drawn, behind the result.
            if (_calloutRoutine != null) { StopCoroutine(_calloutRoutine); _calloutRoutine = null; }
            _calloutGroup.alpha = 0f;
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
            SettleHeaderOnFinalRound();
            ShowResult();
        }

        private string RoundText(int round)
        {
            return "ROUND " + round + " / " + App.Session.Balance.MaxRounds;
        }

        /// <summary>The bar's second line: the wave and battle, where the stage has a curve, then
        /// the round.</summary>
        private void RefreshStatusLine()
        {
            _roundLabel.text = string.IsNullOrEmpty(_progressText)
                ? _roundText : _progressText + BattleHudStyle.Separator + _roundText;
        }

        /// <summary>
        /// The header as a fight played to the end leaves it: the last round's number, and that
        /// round's turn order with the last turn taken lit. A skip stops playback before the rest
        /// of the RoundStart events play, and they are the only thing that moves the header, so it
        /// used to keep the round it was skipped from ("ROUND 2 / 30" over a 4-turn result). Read
        /// from the event list the fight already produced, the same events playback reads, so the
        /// header cannot disagree with the result it sits above.
        /// </summary>
        private void SettleHeaderOnFinalRound()
        {
            int lastRound = -1;
            for (int i = 0; i < _result.Events.Count; i++)
                if (_result.Events[i].Type == BattleEventType.RoundStart) lastRound = i;
            if (lastRound < 0) return;

            _roundText = RoundText(_result.Events[lastRound].Value);
            RefreshStatusLine();
            RebuildTurnStrip(lastRound);
            for (int i = lastRound + 1; i < _result.Events.Count; i++)
                if (_result.Events[i].Type == BattleEventType.TurnStart) AdvanceTurnStrip(_result.Events[i].SourceId);
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

        /// <summary>
        /// The gold button, whatever it currently means. See the wiring note in Build for why this
        /// dispatches on a flag rather than being rebound per result.
        /// </summary>
        private void OnPrimary()
        {
            switch (_primaryAction)
            {
                case PrimaryAction.Continue: ContinueToNextStage(); break;
                case PrimaryAction.Exit: ExitToOrigin(); break;
                case PrimaryAction.Prologue: ResolvePrologueStep(); break;
                case PrimaryAction.NextBattle: FightNextBattle(); break;
                case PrimaryAction.Guild:
                    App.Audio.Tap();
                    if (_guildRun != null && _guildRun.OnPrimary != null) _guildRun.OnPrimary();
                    break;
                default: Replay(); break;
            }
        }

        /// <summary>
        /// The screen this fight was launched from. The same three-way discriminator ShowResult
        /// already branches its spoils on, so the exit cannot disagree with the result panel about
        /// which mode the player is in.
        /// </summary>
        /// <summary>
        /// Hands the outcome back to OnboardingCoach and gets out of the way.
        ///
        /// The callback is read into a local and the field cleared BEFORE invoking, because what
        /// OnboardingCoach does with it may start another step, which calls SetPrologueStep and
        /// assigns a new callback. Clearing after the call would wipe the one just installed and
        /// strand the sequence on the following step with a dead gold button.
        /// </summary>
        private void ResolvePrologueStep()
        {
            App.Audio.Tap();
            System.Action<bool> done = _prologueOnResolved;
            _prologueOnResolved = null;
            if (done != null) done(_result != null && _result.Victory);
        }

        /// <summary>
        /// The prologue's spoils, which are none.
        ///
        /// This grants NOTHING - no gold, no shards, no account XP, no cleared-stage id, no battle
        /// tally. That is the point of it existing rather than reusing ShowCampaignResult with the
        /// rewards zeroed: there is no reward path left live to accidentally fire. The row is here
        /// so the panel does not render an empty spoils box, and it is a line of script rather
        /// than a number because the prologue is the one fight in the game about neither.
        ///
        /// The audio still plays. The scripted loss is meant to land, and Defeat() is how every
        /// other loss in the game lands.
        /// </summary>
        private void ShowPrologueResult()
        {
            if (_result.Victory)
            {
                AddRewardRow("THE LINE HOLDS", "for now", UITheme.TextMuted);
                App.Audio.Victory();
            }
            else
            {
                AddRewardRow("THE LINE BREAKS", "as it always does", UITheme.TextMuted);
                App.Audio.Defeat();
            }
        }

        /// <summary>
        /// Straight into the next battle of the same stage.
        ///
        /// Reads the cursor back out of the session rather than incrementing the screen's own
        /// copy, so the position the player resumes at after quitting is the same number this
        /// button acts on - one source of truth, and no way for the two to drift apart if a
        /// battle is ever recorded from somewhere other than here. Then reuses Replay's
        /// OnHide/OnShow rebuild, the same path every other way into a fight uses.
        /// </summary>
        private void FightNextBattle()
        {
            App.Audio.Tap();
            App.Session.ResumePointFor(_stage, out _campaignWave, out _campaignBattle);
            // Tells the OnHide below that this is a hop inside a chain rather than the player
            // leaving, so an auto run is not cancelled by its own advance. Harmless when auto is
            // off: the flag is cleared by that same OnHide either way.
            _chaining = _auto;
            OnHide();
            OnShow();
        }

        private ScreenId OriginScreen()
        {
            if (_duelOpponent != null) return ScreenId.FriendTeam;
            if (_provingOpponent != null) return ScreenId.Proving;
            if (_recitalFloor >= 0) return ScreenId.Recital;
            return ScreenId.Campaign;
        }

        private string ExitLabel()
        {
            if (_duelOpponent != null) return "BACK TO THEIR LINE";
            if (_provingOpponent != null) return "BACK TO THE PROVING";
            if (_recitalFloor >= 0) return "BACK TO THE RECITAL";
            return "BACK TO CAMPAIGN";
        }

        /// <summary>
        /// Leaves the fight for wherever it started. Router.Show rather than Back() on purpose:
        /// the history stack behind a battle can hold the screen that launched it OR a rematch
        /// chain several fights deep, and "back" through that is not what the button says.
        /// </summary>
        private void ExitToOrigin()
        {
            App.Audio.Tap();
            Router.Show(_exitScreen, false);
        }

        /// <summary>
        /// Straight into the fight the win just unlocked, with no detour through Campaign.
        ///
        /// Deliberately reuses Replay's path - SetStage, then OnHide/OnShow - so world teardown and
        /// rebuild are identical to every other way a battle starts. A second entry point that
        /// built the world its own way is exactly the kind of thing that drifts apart later.
        ///
        /// Re-resolves NextStage at TAP time rather than trusting what ShowResult saw. Nothing
        /// should change in between, but this is now the most-pressed button in the game and the
        /// cost of being wrong is a null dereference, so it falls back to Campaign instead.
        /// </summary>
        private void ContinueToNextStage()
        {
            App.Audio.Tap();

            StageDef next = App.Session.NextStage;
            if (next == null)
            {
                Router.Show(ScreenId.Campaign, false);
                return;
            }

            // Through the single campaign entry point: a fresh stage with a wave curve is resolved
            // a wave at a time on the wave result screen, not fought here. OnHide runs first so
            // this screen is torn down whichever way Launch goes (Router.Show only hides it when it
            // is leaving for another screen, and a stage without a curve comes straight back here).
            OnHide();
            CampaignFlow.LaunchResult went = CampaignFlow.Launch(App, next);
            if (went == CampaignFlow.LaunchResult.Refused || went == CampaignFlow.LaunchResult.Swept)
                Router.Show(ScreenId.Campaign, false);
        }

        /// <summary>
        /// Straight to the banner to change the line - the stage stays remembered on this screen
        /// (_stage isn't cleared), so Campaign still opens on the same fight afterward.
        ///
        /// Now goes to Formation rather than the collection. The button is called ADJUST LINE and
        /// it used to land on a roster with no deploy control on it, which is the same complaint
        /// the Formation screen exists to answer.
        /// </summary>
        /// <summary>
        /// Waits out a short beat so the checkpoint panel is seen, then presses NEXT BATTLE.
        ///
        /// Scaled by _speed, so a player who has chosen 4x gets a chain that is actually four times
        /// faster rather than one gated by a fixed pause. Re-checks _auto after the wait because
        /// anything can have turned it off in between - a tap on the toggle, ADJUST LINE, leaving
        /// the screen - and a coroutine that fired regardless would restart a chain the player has
        /// just stopped.
        /// </summary>
        private IEnumerator AutoAdvanceNext()
        {
            float wait = 0.85f / Mathf.Max(1f, _speed);
            float t = 0f;
            while (t < wait) { t += Time.unscaledDeltaTime; yield return null; }

            if (!_auto) yield break;
            if (_primaryAction != PrimaryAction.NextBattle) yield break;
            FightNextBattle();
        }

        private void AdjustLine()
        {
            App.Audio.Tap();
            // In the guild dungeon this slot is the way back to wherever the fight was opened
            // from; the line is chosen on the floor view, not on Formation.
            if (_guildRun != null)
            {
                if (_guildRun.OnSecondary != null) _guildRun.OnSecondary();
                return;
            }
            // Going to change the banner is a deliberate stop: the line that comes back is not the
            // line the chain was running with, so continuing on the old decision would be wrong.
            SetAuto(false);
            Router.Show(ScreenId.Formation, false);
        }
    }
}
