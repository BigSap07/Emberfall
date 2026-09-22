using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Summon
{
    /// <summary>
    /// THE SUMMON, AS A PLAYER FOR A RESULT THAT IS ALREADY DECIDED.
    ///
    /// By the time this runs, GameSession.Summon has rolled, charged and granted. The sequence is
    /// handed the outcomes and does nothing but show them, the way the battle screen is a player
    /// for a finished BattleEvent list. It never rolls, never picks a hero and cannot change what
    /// came out.
    ///
    /// THE TELL IS THE WHOLE PAYLOAD. The light that builds up commits to the rolled tier's
    /// colour a beat before the hero is on screen: blue, purple, gold, read off the same ramp the
    /// rarity frame is drawn in (Emberforge.SummonTell). The colour always comes from the real
    /// outcome, never from the batch's best pull and never from a guess, because a gold light
    /// that reveals a 3-star is worse than no light at all.
    ///
    /// EVERY FRAME IS A FUNCTION OF (phase, time). Nothing about a beat is accumulated from frame
    /// to frame - not the particles, not the shake, not the rings - so a skip is setting the time,
    /// and the capture harness can photograph any exact instant of any beat by freezing the clock
    /// there. That is also why the particles are computed rather than simulated: each mote's
    /// position is a hash of its index and the time.
    ///
    /// Lives on its own nested Canvas, so the motes and rings moving every frame rebuild this
    /// overlay's mesh and not the whole screen's.
    /// </summary>
    public sealed class SummonSequence : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Phase { Idle, Press, Buildup, Flash, Reveal, Summary }

        private const int MoteCount = 24;
        private const float LightSize = 560f;
        private const float PortraitSize = 360f;
        private const float RevealStarSize = 38f;
        private const float SummaryPortrait = 150f;

        // ---------------------------------------------------------------- built parts

        private RectTransform _root;
        private CanvasGroup _group;
        private Image _dim;
        private RectTransform _stage;
        private Image _lightOuter;
        private Image _lightCore;
        private Image _lightHot;
        private RectTransform _rays;
        private Image[] _rayStreaks;
        private Image _ringOne;
        private Image _ringTwo;
        private readonly Image[] _motes = new Image[MoteCount];
        private Image _flash;

        private RectTransform _reveal;
        private Image _revealBack;
        private RectTransform _revealRays;
        private Image[] _revealStreaks;
        private RectTransform _portraitHost;
        private RectTransform _nameBlock;
        private Text _name;
        private Text _title;
        private readonly Image[] _stars = new Image[5];
        private Text _note;

        private Text _counter;
        private Text _hint;
        private Image _holdRing;
        private UIButton _skipAll;
        private Text _previewTag;

        private RectTransform _summary;
        private CanvasGroup _summaryGroup;
        private RectTransform _summaryGrid;
        private Text _summaryTitle;
        private Text _summaryLine;

        // ---------------------------------------------------------------- run state

        private List<SummonOutcome> _outcomes = new List<SummonOutcome>();
        private SummonTiming _timing = SummonTiming.Full;
        private Action _onDone;
        private EmberforgeButtonSkin _pressedSkin;
        private Phase _phase = Phase.Idle;
        private float _time;
        private int _index;
        private int _starsPinged;
        private bool _revealFired;
        private bool _tellFired;
        private bool _flashFired;
        private bool _pointerDown;
        private float _pointerDownAt;
        private bool _holdConsumed;
        private bool _frozen;

        public Phase Current { get { return _phase; } }
        public int Index { get { return _index; } }
        public bool Running { get { return _phase != Phase.Idle; } }

        // ================================================================ build

        public static SummonSequence Build(RectTransform parent)
        {
            RectTransform root = UIBuilder.Node("SummonSequence", parent);
            UIBuilder.FillParent(root, 0f);

            // Its own canvas, so a moving mote dirties this mesh and nothing else. A nested canvas
            // needs its own raycaster or nothing on it can be tapped.
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<GraphicRaycaster>();

            SummonSequence sequence = root.gameObject.AddComponent<SummonSequence>();
            sequence.BuildParts(root);
            root.gameObject.SetActive(false);
            return sequence;
        }

        private void BuildParts(RectTransform root)
        {
            _root = root;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            // The dim is also the tap surface: it covers the screen and is the raycast target
            // every tap and hold lands on.
            _dim = root.gameObject.AddComponent<Image>();
            _dim.sprite = TextureLab.White;
            _dim.color = new Color(0f, 0f, 0f, 0f);

            _stage = UIBuilder.Node("Stage", root);
            UIBuilder.Box(_stage, 0f, 0f, LightSize, LightSize, new Vector2(0.5f, 0.5f));

            _rayStreaks = Streaks("Rays", _stage, LightSize * 2.6f, 34f, out _rays);
            _lightOuter = Decor("LightOuter", _stage, TextureLab.Glow(256, 1.4f), LightSize * 1.6f);
            _ringOne = Decor("RingOne", _stage, TextureLab.Ring(256, 6), LightSize * 0.5f);
            _ringTwo = Decor("RingTwo", _stage, TextureLab.Ring(256, 10), LightSize * 0.5f);

            Sprite mote = TextureLab.Glow(32, 1.6f);
            for (int i = 0; i < MoteCount; i++) _motes[i] = Decor("Mote" + i, _stage, mote, 20f);

            _lightCore = Decor("LightCore", _stage, TextureLab.Glow(256, 2.2f), LightSize * 0.7f);
            _lightHot = Decor("LightHot", _stage, TextureLab.Glow(128, 3.4f), LightSize * 0.34f);

            BuildReveal(root);
            BuildChrome(root);
            BuildSummary(root);

            // Last, so it covers the reveal it is hiding the cut to.
            _flash = UIBuilder.Img("Flash", root, TextureLab.White, new Color(1f, 1f, 1f, 0f));
            UIBuilder.FillParent(_flash.rectTransform, 0f);
            _flash.raycastTarget = false;

            // The hold ring and the skip control draw over the flash, so they never blink out.
            _holdRing.transform.SetAsLastSibling();
            _skipAll.Root.SetAsLastSibling();
            _previewTag.transform.SetAsLastSibling();
        }

        private static Image Decor(string name, Transform parent, Sprite sprite, float size)
        {
            Image image = UIBuilder.Img(name, parent, sprite, new Color(1f, 1f, 1f, 0f));
            UIBuilder.Box(image.rectTransform, 0f, 0f, size, size, new Vector2(0.5f, 0.5f));
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Soft light rays: four long, thin glows crossed through one centre, turned as a group.
        ///
        /// Built from the Glow falloff stretched thin rather than from a star shape, because the
        /// first pass used TextureLab.Spark and a hard-edged four-point star the size of the screen
        /// read as a cut-out shape laid over the light, not as light.
        /// </summary>
        private static Image[] Streaks(string name, Transform parent, float length, float width,
                                       out RectTransform group)
        {
            group = UIBuilder.Node(name, parent);
            UIBuilder.Box(group, 0f, 0f, 10f, 10f, new Vector2(0.5f, 0.5f));
            Sprite glow = TextureLab.Glow(128, 1.1f);
            Image[] streaks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                streaks[i] = Decor("Streak" + i, group, glow, 10f);
                // Alternate lengths, so the four read as rays rather than as a plus and an x.
                float l = i % 2 == 0 ? length : length * 0.7f;
                streaks[i].rectTransform.sizeDelta = new Vector2(l, width);
                streaks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            }
            return streaks;
        }

        private static void PoseStreaks(RectTransform group, Image[] streaks, Color color, float alpha,
                                        float scale, float degrees)
        {
            group.localScale = new Vector3(scale, scale, 1f);
            group.localRotation = Quaternion.Euler(0f, 0f, degrees);
            for (int i = 0; i < streaks.Length; i++) streaks[i].color = UITheme.WithAlpha(color, alpha);
        }

        private void BuildReveal(RectTransform root)
        {
            _reveal = UIBuilder.Node("Reveal", root);
            UIBuilder.FillParent(_reveal, 0f);

            _revealStreaks = Streaks("Rays", _reveal, 1300f, 40f, out _revealRays);
            _revealRays.anchoredPosition = new Vector2(0f, 120f);
            _revealBack = Decor("Backlight", _reveal, TextureLab.Glow(256, 1.5f), 820f);
            _revealBack.rectTransform.anchoredPosition = new Vector2(0f, 120f);

            _portraitHost = UIBuilder.Node("PortraitHost", _reveal);
            UIBuilder.Box(_portraitHost, 0f, 120f, PortraitSize, PortraitSize, new Vector2(0.5f, 0.5f));

            _nameBlock = UIBuilder.Node("NameBlock", _reveal);
            UIBuilder.Box(_nameBlock, 0f, -130f, 1100f, 70f, new Vector2(0.5f, 0.5f));
            _name = EmberforgeUI.Title("Name", _nameBlock, "", UITheme.FontDisplay, Emberforge.TextGold,
                                       TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_name.rectTransform, 0f);
            UIBuilder.Outlined(_name, Emberforge.Outline, Emberforge.StrokeThick);

            _title = EmberforgeUI.Label("Title", _reveal, "", UITheme.FontSmall, Emberforge.TextQuiet,
                                        TextAnchor.MiddleCenter);
            UIBuilder.Box(_title.rectTransform, 0f, -186f, 1100f, 34f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_title, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform starRow = UIBuilder.Node("Stars", _reveal);
            float gap = Emberforge.Space8;
            float rowWidth = RevealStarSize * 5f + gap * 4f;
            UIBuilder.Box(starRow, 0f, -236f, rowWidth, RevealStarSize, new Vector2(0.5f, 0.5f));
            Sprite star = TextureLab.Star(64);
            for (int i = 0; i < 5; i++)
            {
                _stars[i] = UIBuilder.Img("Star" + i, starRow, star, Color.white);
                UIBuilder.Box(_stars[i].rectTransform, -rowWidth * 0.5f + RevealStarSize * 0.5f + i * (RevealStarSize + gap),
                              0f, RevealStarSize, RevealStarSize, new Vector2(0.5f, 0.5f));
                _stars[i].raycastTarget = false;
                UnityEngine.UI.Outline edge = _stars[i].gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = Emberforge.Outline;
                edge.effectDistance = new Vector2(Emberforge.StrokeThin, -Emberforge.StrokeThin);
            }

            _note = EmberforgeUI.Label("Note", _reveal, "", UITheme.FontSmall, Emberforge.TextBody,
                                       TextAnchor.MiddleCenter);
            UIBuilder.Box(_note.rectTransform, 0f, -290f, 1200f, 34f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_note, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>The counter, the hint, the hold ring and SKIP ALL: everything that talks about
        /// the sequence rather than being part of it.</summary>
        private void BuildChrome(RectTransform root)
        {
            _counter = EmberforgeUI.Title("Counter", root, "", UITheme.FontHeading, Emberforge.TextGold,
                                          TextAnchor.MiddleCenter);
            UIBuilder.Box(_counter.rectTransform, 0f, -56f, 400f, 50f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_counter, Emberforge.Outline, Emberforge.StrokeThin);

            _hint = EmberforgeUI.Label("Hint", root, "", UITheme.FontSmall, Emberforge.TextBody,
                                       TextAnchor.MiddleCenter);
            UIBuilder.Box(_hint.rectTransform, 0f, 56f, 1200f, 34f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_hint, Emberforge.Outline, Emberforge.StrokeThin);

            // Fills while a finger is held, so a hold is a thing the player can see coming rather
            // than a gesture they have to already know about.
            _holdRing = UIBuilder.Img("HoldRing", root, TextureLab.Ring(128, 12), Emberforge.TextGold);
            UIBuilder.Box(_holdRing.rectTransform, 0f, 112f, 56f, 56f, new Vector2(0.5f, 0f));
            _holdRing.type = Image.Type.Filled;
            _holdRing.fillMethod = Image.FillMethod.Radial360;
            _holdRing.fillClockwise = true;
            _holdRing.fillOrigin = 2;
            _holdRing.fillAmount = 0f;
            _holdRing.raycastTarget = false;

            _skipAll = EmberforgeUI.Button("SkipAll", root, "SKIP ALL", UITheme.FontSmall,
                                           ForgeButtonKind.Secondary);
            UIBuilder.Box(_skipAll.Root, -Emberforge.Space32 - 100f, -Emberforge.Space32 - 36f, 200f, 72f,
                          new Vector2(1f, 1f));
            _skipAll.OnClick(SkipAll);

            _previewTag = EmberforgeUI.Label("PreviewTag", root, "EDITOR PREVIEW  -  NOTHING WAS ROLLED OR GRANTED",
                                             UITheme.FontTiny, UITheme.Danger, TextAnchor.MiddleLeft);
            UIBuilder.Box(_previewTag.rectTransform, Emberforge.Space32 + 350f, -Emberforge.Space32 - 12f, 700f, 26f,
                          new Vector2(0f, 1f));
            UIBuilder.Outlined(_previewTag, Emberforge.Outline, Emberforge.StrokeThin);
            _previewTag.raycastTarget = false;
        }

        private void BuildSummary(RectTransform root)
        {
            _summary = UIBuilder.Node("Summary", root);
            UIBuilder.FillParent(_summary, 0f);
            _summaryGroup = _summary.gameObject.AddComponent<CanvasGroup>();

            RectTransform panel = UIBuilder.Node("Panel", _summary);
            UIBuilder.Box(panel, 0f, 0f, 1340f, 820f, new Vector2(0.5f, 0.5f));
            EmberforgeUI.PanelOn(panel, true);

            _summaryTitle = EmberforgeUI.Title("Title", panel, "", UITheme.FontHeading, Emberforge.TextGold,
                                               TextAnchor.UpperCenter);
            UIBuilder.Box(_summaryTitle.rectTransform, 0f, -26f, 1200f, 46f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_summaryTitle, Emberforge.Outline, Emberforge.StrokeThin);

            _summaryLine = EmberforgeUI.Label("Line", panel, "", UITheme.FontSmall, Emberforge.TextQuiet,
                                              TextAnchor.UpperCenter);
            UIBuilder.Box(_summaryLine.rectTransform, 0f, -76f, 1200f, 32f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_summaryLine, Emberforge.Outline, Emberforge.StrokeThin);

            _summaryGrid = UIBuilder.Node("Grid", panel);
            UIBuilder.Box(_summaryGrid, 0f, 12f, 1240f, 540f, new Vector2(0.5f, 0.5f));

            UIButton close = EmberforgeUI.Button("Continue", panel, "CONTINUE", UITheme.FontHeading,
                                                 ForgeButtonKind.Primary);
            UIBuilder.Box(close.Root, 0f, 28f, 420f, 88f, new Vector2(0.5f, 0f));
            close.OnClick(Finish);

            _summary.gameObject.SetActive(false);
        }

        // ================================================================ play

        /// <summary>
        /// Plays a finished batch. <paramref name="pressed"/> is the button that started it, held
        /// sunk through the press beat; null when there is none. <paramref name="preview"/> marks
        /// an editor preview, whose outcomes were never rolled, so the screen says so.
        /// </summary>
        public void Play(List<SummonOutcome> outcomes, SummonTiming timing, UIButton pressed,
                         bool preview, Action onDone)
        {
            _outcomes = outcomes;
            _timing = timing;
            _onDone = onDone;
            _pressedSkin = pressed != null ? pressed.Root.GetComponent<EmberforgeButtonSkin>() : null;
            if (_pressedSkin != null) _pressedSkin.HoldPressed(true);

            _root.gameObject.SetActive(true);
            _root.SetAsFirstSibling();
            _group.alpha = 1f;
            _previewTag.gameObject.SetActive(preview);
            _summary.gameObject.SetActive(false);
            _pointerDown = false;
            _holdConsumed = false;
            _frozen = false;
            _flashFired = false;

            _index = 0;
            LoadHero(0);
            Enter(Phase.Press);
            SummonAudio.Fire(SummonBeat.Press, Stars, 0);
            Pose();
        }

        /// <summary>Stops everything and hides, without calling back. For the screen going away
        /// mid-summon; the result is already banked, so nothing is lost by not finishing.</summary>
        public void Abort()
        {
            ReleaseButton();
            _phase = Phase.Idle;
            _onDone = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private SummonOutcome CurrentOutcome { get { return _outcomes[_index]; } }
        private int Stars { get { return CurrentOutcome.Hero.Stars; } }
        private bool IsBatch { get { return _outcomes.Count > 1; } }

        /// <summary>The later pulls of a batch get the mini tell rather than the full buildup.</summary>
        private bool Mini { get { return _index > 0; } }

        private void Enter(Phase phase)
        {
            _phase = phase;
            _time = 0f;
            if (phase == Phase.Buildup)
            {
                _tellFired = false;
                SummonAudio.Fire(SummonBeat.BuildupStart, Stars, _index);
                // The mini tell commits on its first frame; fire now so the beat is not a frame late.
                if (Mini) FireTell();
            }
            if (phase == Phase.Flash)
            {
                _flashFired = true;
                SummonAudio.Fire(SummonBeat.Flash, Stars, _index);
            }
            if (phase == Phase.Reveal)
            {
                _starsPinged = 0;
                _revealFired = false;
            }
        }

        private void FireTell()
        {
            if (_tellFired) return;
            _tellFired = true;
            SummonAudio.Fire(SummonBeat.Tell, Stars, _index);
        }

        private void ReleaseButton()
        {
            if (_pressedSkin != null) _pressedSkin.HoldPressed(false);
            _pressedSkin = null;
        }

        // ================================================================ clock

        private void Update()
        {
            if (_phase == Phase.Idle) return;

            if (!_frozen) _time += Time.unscaledDeltaTime;
            TickHold();
            if (!_frozen) Advance();
            Pose();
        }

        /// <summary>Moves to the next phase once this one's time is up. The reveal and the
        /// summary never end on their own: they wait for the player.</summary>
        private void Advance()
        {
            switch (_phase)
            {
                case Phase.Press:
                    if (_time >= _timing.Dim)
                    {
                        ReleaseButton();
                        Enter(Phase.Buildup);
                    }
                    break;
                case Phase.Buildup:
                    if (!Mini && _time >= _timing.Tell) FireTell();
                    if (_time >= BuildupLength) Enter(Phase.Flash);
                    break;
                case Phase.Flash:
                    if (_time >= _timing.FlashIn) Enter(Phase.Reveal);
                    break;
                case Phase.Reveal:
                    TickRevealAudio();
                    break;
            }
        }

        private float BuildupLength
        {
            get { return Mini ? _timing.MiniFor(Stars) : _timing.BuildupFor(Stars); }
        }

        private float TellAt
        {
            get { return Mini ? 0f : _timing.Tell; }
        }

        private void TickRevealAudio()
        {
            if (!_revealFired)
            {
                _revealFired = true;
                SummonAudio.Fire(SummonBeat.Reveal, Stars, _index);
            }
            int due = StarsDue(_time);
            while (_starsPinged < due)
            {
                SummonAudio.Fire(SummonBeat.StarPing, Stars, _starsPinged);
                // Every tier but 5 stamps its row as one beat, so it pings once for the row.
                _starsPinged = Stars >= 5 ? _starsPinged + 1 : 5;
            }
        }

        /// <summary>How many star pings are due by reveal time t: one per star for a 5-star, one
        /// for the whole row otherwise.</summary>
        private int StarsDue(float t)
        {
            if (t < _timing.StarsAt) return 0;
            if (Stars < 5) return 5;
            return Mathf.Min(5, 1 + Mathf.FloorToInt((t - _timing.StarsAt) / _timing.StarGap));
        }

        // ================================================================ input

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_phase == Phase.Idle || _phase == Phase.Summary) return;
            _pointerDown = true;
            _pointerDownAt = Time.unscaledTime;
            _holdConsumed = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool wasDown = _pointerDown;
            _pointerDown = false;
            _holdRing.fillAmount = 0f;
            if (!wasDown || _holdConsumed) return;
            Tap();
        }

        private void TickHold()
        {
            if (!_pointerDown || _holdConsumed)
            {
                _holdRing.fillAmount = 0f;
                return;
            }
            float held = Time.unscaledTime - _pointerDownAt;
            // Nothing shows for the length of an ordinary tap, so tapping does not flicker it.
            const float ShowAfter = 0.12f;
            _holdRing.fillAmount = held < ShowAfter ? 0f
                : Mathf.Clamp01((held - ShowAfter) / (SummonTiming.HoldToSkipAll - ShowAfter));
            if (held >= SummonTiming.HoldToSkipAll)
            {
                _holdConsumed = true;
                _holdRing.fillAmount = 0f;
                SkipAll();
            }
        }

        /// <summary>
        /// A TAP: past the buildup to the reveal, then through the reveal, then on.
        ///
        /// A tap on a reveal still stamping in completes it rather than leaving it, so a player
        /// tapping fast to skip buildups still sees every hero land for at least one tap.
        /// </summary>
        public void Tap()
        {
            switch (_phase)
            {
                case Phase.Press:
                case Phase.Buildup:
                case Phase.Flash:
                    SummonAudio.Fire(SummonBeat.Skip, Stars, _index);
                    ReleaseButton();
                    // The tell always happens, even skipped: a sound set may key off it, and the
                    // reveal it leads into is in the tier colour either way.
                    FireTell();
                    if (!_flashFired) SummonAudio.Fire(SummonBeat.Flash, Stars, _index);
                    Enter(Phase.Reveal);
                    break;
                case Phase.Reveal:
                    if (_time < _timing.Settled) { _time = _timing.Settled; TickRevealAudio(); }
                    else Next();
                    break;
            }
            Pose();
        }

        /// <summary>HOLD, or SKIP ALL: straight to the end of the whole summon. For a batch that is
        /// the summary grid; for a single pull it is the hero, settled, because the end of a
        /// summon is the result and a skip that hid the result would be a skip past the point.</summary>
        public void SkipAll()
        {
            if (_phase == Phase.Idle || _phase == Phase.Summary) return;
            SummonAudio.Fire(SummonBeat.Skip, Stars, _index);
            ReleaseButton();
            if (IsBatch)
            {
                ShowSummary();
                return;
            }
            FireTell();
            Enter(Phase.Reveal);
            _time = _timing.Settled;
            _revealFired = true;
            _starsPinged = 5;
            Pose();
        }

        private void Next()
        {
            if (_index + 1 < _outcomes.Count)
            {
                _index++;
                LoadHero(_index);
                _flashFired = false;
                Enter(Phase.Buildup);
                return;
            }
            if (IsBatch) ShowSummary();
            else Finish();
        }

        private void Finish()
        {
            Action done = _onDone;
            Abort();
            if (done != null) done();
        }

        // ================================================================ content

        private void LoadHero(int index)
        {
            SummonOutcome outcome = _outcomes[index];
            HeroDef def = outcome.Hero;

            for (int i = _portraitHost.childCount - 1; i >= 0; i--)
                Destroy(_portraitHost.GetChild(i).gameObject);
            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", _portraitHost, def, def.Stars, PortraitSize);
            UIBuilder.FillParent(portrait, 0f);

            _name.text = def.Name;
            _name.color = Emberforge.RarityLit(def.Stars);
            // A hero with no title gets no leading separator: the first capture printed
            // "-  MARKSMAN  -  EMBER" for exactly that case.
            string line = UITheme.RoleName(def.Role) + "   -   " + AspectChart.DisplayName(def.Aspect).ToUpperInvariant();
            _title.text = string.IsNullOrEmpty(def.Title) ? line : def.Title.ToUpperInvariant() + "   -   " + line;

            if (outcome.IsNew)
            {
                _note.text = "NEW ALLY";
                _note.color = UITheme.Success;
            }
            else
            {
                _note.text = "DUPLICATE  -  CONVERTED TO +" + outcome.GoldRefund + " GOLD, +"
                           + outcome.ShardRefund + " SHARDS";
                _note.color = Emberforge.TextGold;
            }

            _counter.text = IsBatch ? (index + 1) + " / " + _outcomes.Count : "";
        }

        // ================================================================ summary

        private void ShowSummary()
        {
            ReleaseButton();
            for (int i = _summaryGrid.childCount - 1; i >= 0; i--)
                Destroy(_summaryGrid.GetChild(i).gameObject);

            int best = BestPull(_outcomes);
            int newCount = 0, dupeCount = 0, gold = 0, shards = 0;
            const int Columns = 5;
            const float CellW = 232f, CellH = 262f;
            int rows = (_outcomes.Count + Columns - 1) / Columns;
            for (int i = 0; i < _outcomes.Count; i++)
            {
                SummonOutcome o = _outcomes[i];
                if (o.IsNew) newCount++;
                else { dupeCount++; gold += o.GoldRefund; shards += o.ShardRefund; }

                int col = i % Columns;
                int row = i / Columns;
                int inRow = Mathf.Min(Columns, _outcomes.Count - row * Columns);
                float x = (col - (inRow - 1) * 0.5f) * (CellW + Emberforge.Space12);
                float y = ((rows - 1) * 0.5f - row) * (CellH + Emberforge.Space12);
                SummaryCell(o, i == best, x, y, CellW, CellH);
            }

            _summaryTitle.text = _outcomes.Count + " PULLS";
            string line = newCount + (newCount == 1 ? " NEW ALLY" : " NEW ALLIES")
                        + "   -   " + dupeCount + (dupeCount == 1 ? " DUPLICATE" : " DUPLICATES");
            if (dupeCount > 0) line += "   -   CONVERTED TO +" + gold + " GOLD, +" + shards + " SHARDS";
            _summaryLine.text = line;

            _phase = Phase.Summary;
            _time = 0f;
            _summary.gameObject.SetActive(true);
            SummonAudio.Fire(SummonBeat.Summary, 0, 0);
            Pose();
        }

        /// <summary>The best pull of a batch: highest tier, then a new ally over a duplicate, then
        /// the earliest. Deterministic, so the same batch always highlights the same cell.</summary>
        public static int BestPull(List<SummonOutcome> outcomes)
        {
            int best = 0;
            for (int i = 1; i < outcomes.Count; i++)
            {
                SummonOutcome a = outcomes[i];
                SummonOutcome b = outcomes[best];
                if (a.Hero.Stars > b.Hero.Stars) best = i;
                else if (a.Hero.Stars == b.Hero.Stars && a.IsNew && !b.IsNew) best = i;
            }
            return best;
        }

        private void SummaryCell(SummonOutcome o, bool best, float x, float y, float w, float h)
        {
            HeroDef def = o.Hero;
            RectTransform cell = UIBuilder.Node("Pull_" + def.Id, _summaryGrid);
            UIBuilder.Box(cell, x, y, w, h, new Vector2(0.5f, 0.5f));

            if (best)
            {
                Image glow = UIBuilder.Img("BestGlow", cell, TextureLab.Glow(128, 1.3f),
                                           UITheme.WithAlpha(Emberforge.SummonTell(def.Stars), 0.85f));
                UIBuilder.FillParent(glow.rectTransform, -46f);
                glow.raycastTarget = false;
            }

            // New allies stand proud of the sheet, duplicates sit in it: the same depth sort the
            // old results list used, so a player counts new allies without reading a word.
            if (o.IsNew) EmberforgeUI.PlainPanelOn(cell);
            else EmberforgeUI.SunkenOn(cell);

            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", cell, def, def.Stars, SummaryPortrait);
            UIBuilder.Box(portrait, 0f, -18f, SummaryPortrait, SummaryPortrait, new Vector2(0.5f, 1f));

            Text name = EmberforgeUI.Label("Name", cell, def.Name, UITheme.FontTiny, Emberforge.RarityLit(def.Stars),
                                           TextAnchor.MiddleCenter);
            UIBuilder.Box(name.rectTransform, 0f, 64f, w - 16f, 26f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform stars = StarRow(cell, def.Stars, 18f);
            UIBuilder.Box(stars, 0f, 40f, stars.sizeDelta.x, 18f, new Vector2(0.5f, 0f));

            string status = o.IsNew ? "NEW" : "+" + o.ShardRefund + " SHARDS";
            Text tag = EmberforgeUI.Label("Status", cell, status, UITheme.FontTiny - 2,
                                          o.IsNew ? UITheme.Success : Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(tag.rectTransform, 0f, 10f, w - 16f, 24f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(tag, Emberforge.Outline, Emberforge.StrokeThin);

            if (best)
            {
                RectTransform chip = EmberforgeUI.Chip("Best", cell, "BEST PULL", Emberforge.SummonTell(def.Stars),
                                                       UITheme.FontTiny);
                UIBuilder.Box(chip, 0f, 14f, 150f, 34f, new Vector2(0.5f, 1f));
                cell.localScale = new Vector3(1.06f, 1.06f, 1f);
                cell.SetAsLastSibling();
            }
        }

        /// <summary>A star row in the rarity ramp rather than UITheme.StarColor, so the stars
        /// under a hero agree with the frame round them and the light that announced them.</summary>
        private static RectTransform StarRow(Transform parent, int stars, float size)
        {
            RectTransform row = UIBuilder.Node("Stars", parent);
            float gap = 3f;
            UIBuilder.Size(row, size * 5f + gap * 4f, size);
            UIBuilder.HStack(row, gap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            Sprite sprite = TextureLab.Star(64);
            for (int i = 0; i < 5; i++)
            {
                Image img = UIBuilder.Img("Star" + i, row, sprite,
                                          i < stars ? Emberforge.RarityLit(stars) : UITheme.WithAlpha(UITheme.TextDim, 0.55f));
                UIBuilder.Sized(img.rectTransform, size, size);
                img.raycastTarget = false;
            }
            return row;
        }

        // ================================================================ pose

        /// <summary>Draws the current (phase, time). The only method that writes to a visual.</summary>
        private void Pose()
        {
            if (_phase == Phase.Idle) return;

            bool summary = _phase == Phase.Summary;
            bool reveal = _phase == Phase.Reveal;
            bool lit = _phase == Phase.Buildup || _phase == Phase.Flash;

            // ---- the room.
            float dim = _phase == Phase.Press ? Emberforge.SummonDim * Ease.Out(_time / _timing.Dim)
                                              : Emberforge.SummonDim;
            _dim.color = new Color(0f, 0f, 0f, dim);

            // ---- the light.
            _stage.gameObject.SetActive(lit);
            if (lit) PoseLight();

            // ---- the flash.
            float flash = 0f;
            if (_phase == Phase.Flash) flash = Mathf.Clamp01(_time / _timing.FlashIn);
            else if (reveal) flash = 1f - Mathf.Clamp01(_time / _timing.FlashOut);
            _flash.color = new Color(1f, 1f, 1f, flash);

            // ---- the hero.
            _reveal.gameObject.SetActive(reveal);
            if (reveal) PoseReveal(_time);

            // ---- the chrome.
            _counter.gameObject.SetActive(IsBatch && !summary);
            _skipAll.Root.gameObject.SetActive(IsBatch && !summary);
            _hint.gameObject.SetActive(!summary);
            if (!summary) PoseHint();

            _summary.gameObject.SetActive(summary);
            if (summary) _summaryGroup.alpha = Ease.Out(_time / 0.2f);
        }

        private void PoseLight()
        {
            int stars = Stars;
            Color tell = Emberforge.SummonTell(stars);
            Color hot = Emberforge.SummonTellHot(stars);
            float t = _phase == Phase.Flash ? BuildupLength + _time : _time;
            float tellAt = TellAt;
            float length = BuildupLength;
            float since = t - tellAt;
            bool committed = since >= 0f;

            // 0 to 1 over the approach to the tell; the full buildup grows through it, the mini
            // tell arrives already grown.
            float grow = tellAt > 0f ? Mathf.Clamp01(t / tellAt) : 1f;
            // The final charge before the flash.
            float charge = Mathf.Clamp01((t - (length - 0.14f)) / 0.14f);

            // Colour commits over a few frames rather than snapping, so the change reads as the
            // light CHOOSING rather than as a cut.
            float commit = committed ? Mathf.Clamp01(since / 0.08f) : 0f;
            Color body = Color.Lerp(Emberforge.SummonNeutral, tell, commit);
            Color centre = Color.Lerp(Color.white, hot, commit * 0.6f);

            // Scale: grows in, punches on the tell, swells into the flash. It starts at 0.3, not
            // near zero: from 0.12 the timing strip showed the first half second of the buildup as
            // an empty screen, which reads as a stall rather than as something gathering.
            float punch = committed ? Ease.Punch(Mathf.Clamp01(since / 0.28f)) * 0.22f : 0f;
            float scale = (Mini ? Mathf.Lerp(0.4f, 1f, Ease.Out(Mathf.Clamp01(t / 0.12f)))
                                : Mathf.Lerp(0.3f, 0.9f, Ease.InOut(grow)))
                          + punch + charge * 0.7f;
            float appear = Mini ? 1f : Mathf.Clamp01(t / 0.15f);

            SetImage(_lightOuter, body, 0.55f * appear, scale);
            SetImage(_lightCore, body, 0.95f * appear, scale);
            SetImage(_lightHot, centre, appear, scale * (1f + 0.06f * Mathf.Sin(t * 30f)));

            // Rays: gold only, and faintly on purple. Turning slowly, so a 5-star's light has
            // structure the lower tiers' lights do not.
            float raysAlpha = stars >= 5 ? 0.7f : stars == 4 ? 0.25f : 0f;
            PoseStreaks(_rays, _rayStreaks, hot, raysAlpha * commit, scale * 0.9f + 0.1f, t * 24f);

            // Ring one on every tier at the tell. Ring two on gold only, a beat later and wider.
            PoseRing(_ringOne, since, 0f, 0.55f, 3.2f, tell);
            if (stars >= 5) PoseRing(_ringTwo, since, 0.12f, 0.7f, 4.6f, hot);
            else SetImage(_ringTwo, hot, 0f, 1f);

            // Motes: pulled inward through the approach, tier-coloured after the tell, gone in
            // the flash.
            float motesAlpha = (Mini ? 0.5f : appear) * (1f - charge);
            Color moteColor = Color.Lerp(Emberforge.SummonNeutral, hot, commit);
            for (int i = 0; i < MoteCount; i++) PoseMote(i, t, motesAlpha, moteColor);

            // Rumble ramps with the approach; gold adds a shake on the tell that decays.
            float rumble = Mini ? 2f : Mathf.Lerp(0f, 6f, grow) + charge * 4f;
            float shake = 0f;
            if (stars >= 5 && committed) shake = 22f * Mathf.Clamp01(1f - since / 0.45f);
            float amplitude = rumble + shake;
            _stage.anchoredPosition = new Vector2(Noise(t * 38f, 0.3f) * amplitude,
                                                  Noise(t * 41f, 7.1f) * amplitude);
        }

        private static void PoseRing(Image ring, float since, float delay, float life, float reach, Color color)
        {
            float k = (since - delay) / life;
            if (k < 0f || k > 1f) { SetImage(ring, color, 0f, 1f); return; }
            SetImage(ring, color, 0.9f * (1f - k), Mathf.Lerp(0.6f, reach, Ease.Out(k)));
        }

        private void PoseMote(int i, float t, float alpha, Color color)
        {
            Image mote = _motes[i];
            float angle = Hash(i, 1) * Mathf.PI * 2f;
            float radius = Mathf.Lerp(420f, 760f, Hash(i, 2));
            float period = Mathf.Lerp(0.55f, 0.95f, Hash(i, 3));
            float local = Mathf.Repeat(t / period + Hash(i, 4), 1f);
            float pull = local * local;
            angle += local * 0.7f;
            float r = radius * (1f - pull);
            mote.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            float size = Mathf.Lerp(26f, 8f, pull);
            mote.rectTransform.sizeDelta = new Vector2(size, size);
            mote.color = UITheme.WithAlpha(color, alpha * Mathf.Sin(local * Mathf.PI));
        }

        private void PoseReveal(float t)
        {
            SummonTiming timing = _timing;
            int stars = Stars;
            Color tell = Emberforge.SummonTell(stars);
            Color hot = Emberforge.SummonTellHot(stars);

            // Backlight: the committed light, left burning behind the hero.
            float back = Ease.Out(Mathf.Clamp01(t / 0.2f));
            SetImage(_revealBack, tell, 0.75f * back, 1f + 0.03f * Mathf.Sin(t * 2.4f));
            PoseStreaks(_revealRays, _revealStreaks, hot, (stars >= 5 ? 0.5f : stars == 4 ? 0.2f : 0f) * back,
                        1f, t * 10f);

            // Portrait: 1.15 down to 1.0, in over its first tenth of a second.
            float k = Mathf.Clamp01(t / timing.PortraitIn);
            float scale = Mathf.Lerp(1.15f, 1f, Ease.Out(k));
            _portraitHost.localScale = new Vector3(scale, scale, 1f);
            SetGroupAlpha(_portraitHost, Mathf.Clamp01(t / 0.1f));

            // Name and title stamp: big to size, overshooting slightly.
            float n = Mathf.Clamp01((t - timing.NameAt) / timing.NameIn);
            float nameScale = t < timing.NameAt ? 0f : Mathf.Lerp(1.6f, 1f, Ease.OutBack(n));
            _nameBlock.localScale = new Vector3(nameScale, nameScale, 1f);
            _name.color = UITheme.WithAlpha(_name.color, Mathf.Clamp01(n * 2f));
            _title.color = UITheme.WithAlpha(Emberforge.TextQuiet, Mathf.Clamp01(n));

            // Stars: all at once below 5, one at a time at 5.
            Color filled = Emberforge.RarityLit(stars);
            Color empty = UITheme.WithAlpha(UITheme.TextDim, 0.55f);
            for (int i = 0; i < 5; i++)
            {
                float at = timing.StarsAt + (stars >= 5 ? i * timing.StarGap : 0f);
                float s = Mathf.Clamp01((t - at) / timing.StarIn);
                bool on = i < stars;
                float starScale = t < at ? 0f : Mathf.Lerp(on ? 2f : 1.3f, 1f, Ease.OutBack(s));
                _stars[i].rectTransform.localScale = new Vector3(starScale, starScale, 1f);
                _stars[i].color = UITheme.WithAlpha(on ? filled : empty, (on ? 1f : empty.a) * Mathf.Clamp01(s * 2f));
            }

            float noteAlpha = Mathf.Clamp01((t - (timing.Settled - 0.12f)) / 0.12f);
            _note.color = UITheme.WithAlpha(_note.color, noteAlpha);
        }

        private void PoseHint()
        {
            bool settled = _phase == Phase.Reveal && _time >= _timing.Settled;
            if (settled)
            {
                bool last = _index + 1 >= _outcomes.Count;
                _hint.text = last ? (IsBatch ? "TAP TO SEE ALL PULLS" : "TAP TO CONTINUE") : "TAP FOR THE NEXT PULL";
                float pulse = 0.65f + 0.35f * Mathf.Sin((_time - _timing.Settled) * 4f);
                _hint.color = UITheme.WithAlpha(Emberforge.TextBody, Mathf.Clamp01((_time - _timing.Settled) / 0.15f) * pulse);
                return;
            }
            // Before the hero lands the hint says how to get past the wait, quietly.
            _hint.text = "TAP TO SKIP   -   HOLD TO SKIP " + (IsBatch ? "ALL" : "TO THE HERO");
            _hint.color = UITheme.WithAlpha(Emberforge.TextQuiet, _phase == Phase.Reveal ? 0f : 0.7f);
        }

        // ================================================================ helpers

        private static void SetImage(Image image, Color color, float alpha, float scale)
        {
            image.color = UITheme.WithAlpha(color, alpha);
            image.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private static void SetGroupAlpha(RectTransform rect, float alpha)
        {
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
        }

        /// <summary>A stable 0..1 value per (mote, channel). Integer hashing, not Random, so the
        /// same mote is in the same place at the same time on every run and in every capture.</summary>
        private static float Hash(int i, int channel)
        {
            uint h = (uint)(i * 374761393 + channel * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        /// <summary>Smooth-ish noise in -1..1 from two sines, enough for a rumble.</summary>
        private static float Noise(float x, float seed)
        {
            return Mathf.Sin(x + seed) * 0.6f + Mathf.Sin(x * 1.73f + seed * 2.3f) * 0.4f;
        }

        private static class Ease
        {
            public static float Out(float k) { k = Mathf.Clamp01(k); float i = 1f - k; return 1f - i * i * i; }
            public static float InOut(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
            public static float OutBack(float k)
            {
                k = Mathf.Clamp01(k);
                const float c = 1.9f;
                float m = k - 1f;
                return 1f + (c + 1f) * m * m * m + c * m * m;
            }
            /// <summary>0 at both ends, 1 early: the shape of a thing hit and settling.</summary>
            public static float Punch(float k) { k = Mathf.Clamp01(k); return Mathf.Sin(k * Mathf.PI) * (1f - k); }
        }

        // ================================================================ capture

        /// <summary>
        /// Holds the sequence at an exact instant: this pull, this phase, this many seconds in.
        /// For the capture harness, which photographs beats rather than hoping to catch them.
        /// Unfreeze with Freeze(false).
        /// </summary>
        public void FreezeAt(int index, Phase phase, float time)
        {
            _frozen = true;
            if (index != _index)
            {
                _index = index;
                LoadHero(index);
            }
            if (phase == Phase.Summary)
            {
                ShowSummary();
                _time = time;
                Pose();
                return;
            }
            _phase = phase;
            _time = time;
            _summary.gameObject.SetActive(false);
            Pose();
        }

        public void Freeze(bool frozen)
        {
            _frozen = frozen;
        }
    }
}
