using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// THE FIRST SESSION, DRAWN (DESIGN-ONBOARDING.md). Onboarding decides every step and saves
    /// it; this only shows the step the profile is on and notices when the player has done it.
    ///
    /// One overlay over everything, rebuilt from PlayerProfile.TutorialStep every frame, so there
    /// is no second copy of the flow's state to drift: quitting and relaunching shows exactly the
    /// step after the last completed one because that is the only thing this reads.
    ///
    ///   Wake       a dark screen over the dimmed Hub with the three lines; one tap continues
    ///   Recall     the Hub dimmed except the Summon obelisk, ringed and lit, and one RECALL card
    ///   result     the recalled hero, then CONTINUE
    ///   Prologue   the existing prologue battle, with one line over it
    ///   Guided     six steps: everything dimmed and inert except one control, an arrow at it,
    ///              one line under it and six pips along the top
    ///   free play  one tip the first time each screen opens, gone on any tap
    ///
    /// SKIP sits top right from the wake to the last guided step and ends the flow through
    /// Onboarding.Skip, which still gives Kael.
    ///
    /// LAYOUT AND LOGIC ONLY. The look is a placeholder for the designer's art pass: every colour
    /// comes from UITheme or Emberforge and every word from onboarding.json, so the pass restyles
    /// this file's Build methods without touching what the flow does.
    /// </summary>
    public sealed class OnboardingCoach : MonoBehaviour
    {
        // ------------------------------------------------------------------ metrics

        private const float HolePad = 14f;
        private const float CaptionWidth = 860f;
        private const float CaptionHeight = 78f;
        private const float ArrowSize = 76f;
        private const float RingPad = 22f;
        private const float PipSize = 18f;
        private const float PipCurrentWidth = 44f;
        private const float PipGap = 14f;
        private const float SkipWidth = 150f;
        private const float SkipHeight = 64f;
        private const float EdgeMargin = 28f;

        private enum Mode { None, Wake, Recall, RecallResult, Prologue, Guided }

        public static OnboardingCoach Instance { get; private set; }

        private GameApp _app;
        private RectTransform _root;
        private Mode _mode = Mode.None;

        // The guided layer: four blockers around the target, four dims around the light, the
        // spotlight itself, the ring, the arrow and the line.
        private RectTransform _guided;
        private readonly RectTransform[] _blockers = new RectTransform[4];
        private readonly RectTransform[] _dims = new RectTransform[4];
        private RectTransform _spot;
        private RectTransform _fullBlock;
        private RectTransform _litTap;
        private RectTransform _ring;
        private RectTransform _arrow;
        private RectTransform _captionPlate;
        private Text _caption;
        private RectTransform _pipRow;
        private readonly List<Image> _pips = new List<Image>();
        private UIButton _skip;

        private RectTransform _wake;
        private Text _wakeLines;
        private RectTransform _recallCard;
        private RectTransform _result;
        private RectTransform _resultPortraitHost;
        private Text _resultName;
        private Text _resultLine;
        private RectTransform _battleLine;
        private Text _battleLineText;

        private RectTransform _tip;
        private Text _tipText;
        private UiLayerStack.Layer _tipLayer;

        private bool _resultOpen;
        private bool _prologueStarted;
        private List<HeroInstance> _squad;
        private int _snapshotStep = -1;
        private int _levelSnapshot;
        private long _idleSnapshot;
        private ScreenId _lastScreen = ScreenId.MainMenu;

        /// <summary>True while the forced flow owns input, so the back key does nothing rather
        /// than walking the player out of a step.</summary>
        public static bool BlocksBack
        {
            get { return Instance != null && Instance._mode != Mode.None && Instance._mode != Mode.Prologue; }
        }

        // ------------------------------------------------------------------ install

        /// <summary>Builds the coach once, over every other overlay, and hooks the router and the
        /// panel host. Called at boot for every player: the tips need it after the flow is over.</summary>
        public static OnboardingCoach Install(GameApp app)
        {
            if (!OnboardingSwitch.Enabled) return null;
            if (Instance != null) return Instance;
            RectTransform root = UIBuilder.Node("Onboarding", app.OverlayLayer);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = Vector2.zero;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            OnboardingCoach coach = root.gameObject.AddComponent<OnboardingCoach>();
            coach._app = app;
            coach._root = root;
            coach.Build();
            Instance = coach;
            app.Router.Shown += coach.OnShown;
            if (app.Panels != null) app.Panels.Shown += coach.OnShown;
            return coach;
        }

        private OnboardingConfig Config
        {
            get { return _app.Session.Content.Onboarding; }
        }

        private PlayerProfile Profile
        {
            get { return _app.Session.Profile; }
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            BuildGuided();
            BuildWake();
            BuildRecallCard();
            BuildResult();
            BuildBattleLine();
            BuildTip();

            // SKIP last, so it sits over every layer above.
            _skip = EmberforgeUI.Button("Skip", _root, "SKIP", UITheme.FontTiny, ForgeButtonKind.Secondary);
            UIBuilder.Box(_skip.Root, -EdgeMargin, -EdgeMargin, SkipWidth, SkipHeight, new Vector2(1f, 1f));
            _skip.OnClick(OnSkip);
            ShowOnly(Mode.None);
        }

        private RectTransform Rect(string name, Transform parent, Color color, bool raycast)
        {
            Image image = UIBuilder.Img(name, parent, TextureLab.White, color);
            image.raycastTarget = raycast;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            return rt;
        }

        private void BuildGuided()
        {
            _guided = UIBuilder.Node("Guided", _root);
            UIBuilder.FillParent(_guided, 0f);

            Color dim = UITheme.ScrimReveal;
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                _dims[i] = Rect("Dim" + i, _guided, dim, false);
                _blockers[i] = Rect("Block" + i, _guided, clear, true);
            }

            Image spot = UIBuilder.Img("Spot", _guided, TextureLab.Spotlight(256), dim);
            spot.raycastTarget = false;
            _spot = spot.rectTransform;
            _spot.anchorMin = Vector2.zero;
            _spot.anchorMax = Vector2.zero;
            _spot.pivot = new Vector2(0.5f, 0.5f);

            // Over the whole screen, on for a step whose lit control must NOT take the tap (the
            // Cinder's obelisk is lit but the card's RECALL is what is pressed).
            Image full = UIBuilder.Img("FullBlock", _guided, TextureLab.White, clear);
            _fullBlock = full.rectTransform;
            UIBuilder.FillParent(_fullBlock, 0f);

            // The Cinder step's lit obelisk: a tap on it presses RECALL, the same as the card's
            // button. Sits over the full block, so it is the only thing under the overlay that
            // takes input, and only in the recall step (ShowOnly, ShowRecall).
            Image litTap = UIBuilder.Img("LitTap", _guided, TextureLab.White, clear);
            _litTap = litTap.rectTransform;
            _litTap.anchorMin = Vector2.zero;
            _litTap.anchorMax = Vector2.zero;
            _litTap.pivot = Vector2.zero;
            Button litButton = litTap.gameObject.AddComponent<Button>();
            litButton.transition = Selectable.Transition.None;
            litButton.targetGraphic = litTap;
            litButton.onClick.AddListener(OnRecall);
            _litTap.gameObject.SetActive(false);

            Image ring = UIBuilder.Img("Ring", _guided, TextureLab.Ring(128, 6), UITheme.Gold);
            ring.raycastTarget = false;
            _ring = ring.rectTransform;
            _ring.anchorMin = Vector2.zero;
            _ring.anchorMax = Vector2.zero;
            _ring.pivot = new Vector2(0.5f, 0.5f);

            Image arrow = UIBuilder.Img("Arrow", _guided, TextureLab.Arrow(128), UITheme.Gold);
            arrow.raycastTarget = false;
            _arrow = arrow.rectTransform;
            _arrow.anchorMin = Vector2.zero;
            _arrow.anchorMax = Vector2.zero;
            _arrow.pivot = new Vector2(0.5f, 0f);
            _arrow.sizeDelta = new Vector2(ArrowSize, ArrowSize);

            _captionPlate = UIBuilder.Node("Caption", _guided);
            _captionPlate.anchorMin = Vector2.zero;
            _captionPlate.anchorMax = Vector2.zero;
            _captionPlate.pivot = new Vector2(0.5f, 0.5f);
            _captionPlate.sizeDelta = new Vector2(CaptionWidth, CaptionHeight);
            EmberforgeUI.PanelOn(_captionPlate, false);
            _caption = EmberforgeUI.Label("Text", _captionPlate, "", UITheme.FontBody,
                                          Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_caption.rectTransform, Emberforge.Space16);
            UIBuilder.BestFit(_caption, UITheme.FontTiny, UITheme.FontBody);
            UIBuilder.Outlined(_caption, Emberforge.Outline, Emberforge.StrokeThin);
            _caption.raycastTarget = false;

            _pipRow = UIBuilder.Node("Pips", _guided);
            float rowWidth = PipCurrentWidth + (Onboarding.GuidedSteps - 1) * PipSize + (Onboarding.GuidedSteps - 1) * PipGap;
            UIBuilder.Box(_pipRow, 0f, -EdgeMargin - 18f, rowWidth, PipSize, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_pipRow, PipGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            for (int i = 0; i < Onboarding.GuidedSteps; i++)
            {
                Image pip = UIBuilder.Img("Pip" + i, _pipRow, TextureLab.RoundedRect(9), Emberforge.TextQuiet);
                pip.type = Image.Type.Sliced;
                pip.raycastTarget = false;
                UIBuilder.Sized(pip.rectTransform, PipSize, PipSize);
                _pips.Add(pip);
            }
        }

        private void BuildWake()
        {
            _wake = UIBuilder.Node("Wake", _root);
            UIBuilder.FillParent(_wake, 0f);
            Image dark = _wake.gameObject.AddComponent<Image>();
            dark.sprite = TextureLab.White;
            dark.color = UITheme.WithAlpha(UITheme.BackdropDeep, 0.9f);
            Button tap = _wake.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.targetGraphic = dark;
            tap.onClick.AddListener(OnWakeTapped);

            _wakeLines = EmberforgeUI.Title("Lines", _wake, "", UITheme.FontTitle, Emberforge.TextBody,
                                            TextAnchor.MiddleCenter);
            UIBuilder.Box(_wakeLines.rectTransform, 0f, 30f, 1400f, 420f, new Vector2(0.5f, 0.5f));
            _wakeLines.lineSpacing = 1.6f;
            UIBuilder.Outlined(_wakeLines, Emberforge.Outline, Emberforge.StrokeThin);
            _wakeLines.raycastTarget = false;

            Text hint = EmberforgeUI.Label("Hint", _wake, "TAP TO CONTINUE", UITheme.FontTiny,
                                           Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(hint.rectTransform, 0f, 90f, 600f, 34f, new Vector2(0.5f, 0f));
            hint.raycastTarget = false;
        }

        private void BuildRecallCard()
        {
            _recallCard = UIBuilder.Node("RecallCard", _root);
            UIBuilder.Box(_recallCard, 140f, 0f, 640f, 380f, new Vector2(0f, 0.5f));
            EmberforgeUI.PanelOn(_recallCard, true);
        }

        /// <summary>Fills the recall card from onboarding.json. Rebuilt on show, so a reworded
        /// line reads correctly without a restart in the editor.</summary>
        private void FillRecallCard()
        {
            for (int i = _recallCard.childCount - 1; i >= 0; i--)
            {
                Transform child = _recallCard.GetChild(i);
                if (child.name == "Title" || child.name == "Body" || child.name == "Recall") Destroy(child.gameObject);
            }
            Text title = EmberforgeUI.Title("Title", _recallCard, Config.RecallTitle, UITheme.FontTitle,
                                            Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(title.rectTransform, 0f, -56f, 560f, 70f, new Vector2(0.5f, 1f));
            UIBuilder.BestFit(title, UITheme.FontHeading, UITheme.FontTitle);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text body = EmberforgeUI.Body("Body", _recallCard, Config.RecallBody, UITheme.FontBody,
                                          Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(body.rectTransform, 0f, -146f, 540f, 100f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(body, Emberforge.Outline, Emberforge.StrokeThin);

            UIButton recall = EmberforgeUI.Button("Recall", _recallCard, Config.RecallButton, UITheme.FontHeading,
                                                  ForgeButtonKind.Primary);
            UIBuilder.Box(recall.Root, 0f, 48f, 380f, 96f, new Vector2(0.5f, 0f));
            recall.OnClick(OnRecall);
        }

        private void BuildResult()
        {
            _result = UIBuilder.Node("RecallResult", _root);
            UIBuilder.FillParent(_result, 0f);
            Image dark = _result.gameObject.AddComponent<Image>();
            dark.sprite = TextureLab.White;
            dark.color = UITheme.ScrimReveal;

            Image glow = UIBuilder.Img("Glow", _result, TextureLab.Glow(256, 2f), UITheme.WithAlpha(UITheme.Gold, 0.45f));
            UIBuilder.Box(glow.rectTransform, 0f, 70f, 760f, 760f, new Vector2(0.5f, 0.5f));
            glow.raycastTarget = false;

            _resultPortraitHost = UIBuilder.Node("Portrait", _result);
            UIBuilder.Box(_resultPortraitHost, 0f, 90f, 340f, 340f, new Vector2(0.5f, 0.5f));

            _resultName = EmberforgeUI.Title("Name", _result, "", UITheme.FontDisplay, Emberforge.TextGold,
                                             TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultName.rectTransform, 0f, -140f, 1200f, 80f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_resultName, Emberforge.Outline, Emberforge.StrokeThick);

            _resultLine = EmberforgeUI.Label("Line", _result, "", UITheme.FontBody, Emberforge.TextBody,
                                             TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultLine.rectTransform, 0f, -210f, 1200f, 44f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_resultLine, Emberforge.Outline, Emberforge.StrokeThin);

            UIButton next = EmberforgeUI.Button("Continue", _result, "CONTINUE", UITheme.FontHeading,
                                                ForgeButtonKind.Primary);
            UIBuilder.Box(next.Root, 0f, 120f, 420f, 96f, new Vector2(0.5f, 0f));
            next.OnClick(OnResultContinue);
            _resultContinue = next;
        }

        private UIButton _resultContinue;

        private void BuildBattleLine()
        {
            _battleLine = UIBuilder.Node("BattleLine", _root);
            UIBuilder.Box(_battleLine, 0f, 150f, CaptionWidth, CaptionHeight, new Vector2(0.5f, 0f));
            EmberforgeUI.PanelOn(_battleLine, false);
            Image plate = _battleLine.GetComponent<Image>();
            if (plate != null) plate.raycastTarget = false;
            _battleLineText = EmberforgeUI.Label("Text", _battleLine, "", UITheme.FontBody,
                                                 Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_battleLineText.rectTransform, Emberforge.Space16);
            UIBuilder.Outlined(_battleLineText, Emberforge.Outline, Emberforge.StrokeThin);
            _battleLineText.raycastTarget = false;
        }

        private void BuildTip()
        {
            _tip = UIBuilder.Node("Tip", _root);
            UIBuilder.FillParent(_tip, 0f);
            Image catcher = _tip.gameObject.AddComponent<Image>();
            catcher.sprite = TextureLab.White;
            catcher.color = UITheme.ScrimModal;
            Button tap = _tip.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.targetGraphic = catcher;
            tap.onClick.AddListener(DismissTip);

            RectTransform plate = UIBuilder.Node("Plate", _tip);
            UIBuilder.Box(plate, 0f, 200f, 1100f, 96f, new Vector2(0.5f, 0f));
            EmberforgeUI.PanelOn(plate, true);
            Image plateImage = plate.GetComponent<Image>();
            if (plateImage != null) plateImage.raycastTarget = false;
            _tipText = EmberforgeUI.Label("Text", plate, "", UITheme.FontBody, Emberforge.TextBody,
                                          TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_tipText.rectTransform, Emberforge.Space24);
            UIBuilder.BestFit(_tipText, UITheme.FontTiny, UITheme.FontBody);
            UIBuilder.Outlined(_tipText, Emberforge.Outline, Emberforge.StrokeThin);
            _tipText.raycastTarget = false;
            _tip.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the frame

        private void LateUpdate()
        {
            if (_app == null || _app.Session == null || _app.Router == null) return;
            PlayerProfile profile = Profile;
            if (Onboarding.IsComplete(profile))
            {
                if (_mode != Mode.None) ShowOnly(Mode.None);
                return;
            }

            int step = profile.TutorialStep;
            if (step != _snapshotStep) TakeSnapshot(step);

            switch (step)
            {
                case Onboarding.StepWake: ShowWake(); break;
                case Onboarding.StepRecall: ShowRecall(); break;
                case Onboarding.StepPrologue:
                    if (_resultOpen) ShowOnly(Mode.RecallResult);
                    else ShowPrologue();
                    break;
                default: ShowGuided(step); break;
            }
        }

        private void TakeSnapshot(int step)
        {
            _snapshotStep = step;
            _levelSnapshot = LevelSum();
            _idleSnapshot = Profile.LastIdleClaimUnix;
        }

        private int LevelSum()
        {
            int sum = 0;
            for (int i = 0; i < Profile.Heroes.Count; i++) sum += Profile.Heroes[i].Level;
            return sum;
        }

        private void ShowOnly(Mode mode)
        {
            bool changed = _mode != mode;
            _mode = mode;
            _wake.gameObject.SetActive(mode == Mode.Wake);
            _guided.gameObject.SetActive(mode == Mode.Guided || mode == Mode.Recall);
            _recallCard.gameObject.SetActive(mode == Mode.Recall);
            _litTap.gameObject.SetActive(mode == Mode.Recall);
            _result.gameObject.SetActive(mode == Mode.RecallResult);
            _battleLine.gameObject.SetActive(mode == Mode.Prologue);
            _skip.Root.gameObject.SetActive(mode != Mode.None);
            _pipRow.gameObject.SetActive(mode == Mode.Guided);
            // Top right, under the top bar's currency pills rather than over them, and on the
            // battle a row lower again, under its own SPEED and SKIP strip.
            float skipTop = UITheme.TopBarHeight + Emberforge.Space12;
            UIBuilder.Box(_skip.Root, -EdgeMargin, mode == Mode.Prologue ? -(skipTop + 30f) : -skipTop,
                          SkipWidth, SkipHeight, new Vector2(1f, 1f));
            if (changed && mode != Mode.None) _root.SetAsLastSibling();
            if (changed && _skip != null) _skip.SetText(Config != null && Config.Skip.Length > 0 ? Config.Skip : "SKIP");
        }

        // ------------------------------------------------------------------ 1. wake

        private void ShowWake()
        {
            if (_mode != Mode.Wake)
            {
                _wakeLines.text = string.Join("\n", Config.WakeLines.ToArray());
                ShowOnly(Mode.Wake);
            }
        }

        private void OnWakeTapped()
        {
            if (!Onboarding.CompleteStep(Profile, Onboarding.StepWake)) return;
            _app.Audio.Tap();
            _app.SaveNow();
        }

        // ------------------------------------------------------------------ 2. the Cinder

        private void ShowRecall()
        {
            if (_app.Router.Current == null || _app.Router.Current.Id != ScreenId.MainMenu)
                _app.Router.GoHome();
            if (_mode != Mode.Recall)
            {
                FillRecallCard();
                ShowOnly(Mode.Recall);
            }

            // The obelisk is painted under its marker: the light takes the marker and reaches
            // down the marker's lift to the stone. A tap there presses RECALL (_litTap); the
            // marker itself and the rest of the hub stay inert under the full block.
            RectTransform node = FindDeep(_app.Router.MainMenu.Root, "NodeSUMMON");
            Rect lit;
            if (node != null && TryRectOf(node, out lit))
            {
                float drop = HubStyle.LabelLift + 170f;
                lit = new Rect(lit.x - 40f, lit.y - drop, lit.width + 80f, lit.height + drop);
                Light(lit, false);
                _litTap.anchoredPosition = new Vector2(lit.x, lit.y);
                _litTap.sizeDelta = new Vector2(lit.width, lit.height);
            }
            else
            {
                Light(new Rect(0f, 0f, 0f, 0f), false);
                _litTap.sizeDelta = Vector2.zero;
            }
            HideCoachText();
        }

        private void OnRecall()
        {
            RecallOutcome outcome = Onboarding.Recall(_app.Session, TimeUtil.NowUnix());
            _app.Audio.Tap();
            _app.SaveNow();
            _app.RefreshHud();

            HeroInstance hero = outcome.Hero;
            for (int i = _resultPortraitHost.childCount - 1; i >= 0; i--) Destroy(_resultPortraitHost.GetChild(i).gameObject);
            if (hero != null && hero.Def != null)
            {
                RectTransform portrait = EmberforgeUI.PortraitFrame("Hero", _resultPortraitHost, hero.Def, hero.Def.Stars, 340f);
                UIBuilder.FillParent(portrait, 0f);
                _resultName.text = hero.Def.Name.ToUpperInvariant();
                _resultLine.text = hero.Def.Name + " " + Config.RecallResult;
            }
            else
            {
                _resultName.text = "";
                _resultLine.text = "";
            }
            _resultContinue.SetText(Config.RecallContinue);
            _resultOpen = true;
            ShowOnly(Mode.RecallResult);
        }

        private void OnResultContinue()
        {
            _app.Audio.Tap();
            _resultOpen = false;
        }

        // ------------------------------------------------------------------ 3. the prologue

        private void ShowPrologue()
        {
            if (_mode != Mode.Prologue)
            {
                _battleLineText.text = Config.PrologueLine;
                ShowOnly(Mode.Prologue);
            }
            if (_prologueStarted) return;
            _prologueStarted = true;

            _squad = PrologueScript.BuildSquad(_app.Session.Content);
            if (_squad == null || _squad.Count == 0)
            {
                // No cast in this content: the battle is skipped, never fought on an empty field.
                FinishPrologue();
                return;
            }
            _app.Router.Battle.SetPrologueStep(Onboarding.PrologueStep(_app.Session.Content), _squad,
                                               delegate (bool victory) { FinishPrologue(); });
            _app.Router.Show(ScreenId.Battle, false);
        }

        private void FinishPrologue()
        {
            _squad = null;
            _prologueStarted = false;
            if (Onboarding.CompleteStep(Profile, Onboarding.StepPrologue)) _app.SaveNow();
            _app.Router.GoHome();
        }

        // ------------------------------------------------------------------ 4. the guided loop

        private void ShowGuided(int step)
        {
            if (_mode != Mode.Guided) ShowOnly(Mode.Guided);
            if (CheckDone(step)) return;

            int index = step - Onboarding.StepTeam;
            for (int i = 0; i < _pips.Count; i++)
            {
                bool current = i == index;
                _pips[i].color = i < index ? Emberforge.GoldMid : current ? Emberforge.GoldLit : Emberforge.TextQuiet;
                UIBuilder.Sized(_pips[i].rectTransform, current ? PipCurrentWidth : PipSize, PipSize);
            }

            RectTransform target = TargetFor(step);
            string line = Config.StepLine(index);
            Rect rect;
            if (target != null && TryRectOf(target, out rect))
            {
                Light(rect, true);
                PlaceCoachText(rect, line);
            }
            else
            {
                // Nothing to point at on this screen: the flow says its line and lets the screen
                // be used rather than lock a player out of a step it cannot reach.
                Unlight();
                PlaceCoachText(new Rect(_root.rect.width * 0.5f, _root.rect.height * 0.5f, 0f, 0f), line);
                _ring.gameObject.SetActive(false);
                _arrow.gameObject.SetActive(false);
            }
        }

        /// <summary>The completions read by polling the profile, which is the truth of them: a
        /// level bought, an idle claim made, stage 1-1 or 1-2 cleared.</summary>
        private bool CheckDone(int step)
        {
            bool done = false;
            switch (step)
            {
                case Onboarding.StepWaveResult:
                    StageDef first = Onboarding.StageFor(_app.Session.Content, step);
                    done = first != null && Profile.HasClearedStage(first.Id);
                    break;
                case Onboarding.StepSecondStage:
                    StageDef second = Onboarding.StageFor(_app.Session.Content, step);
                    done = second == null || Profile.HasClearedStage(second.Id);
                    break;
                case Onboarding.StepLevelUp:
                    done = LevelSum() > _levelSnapshot;
                    break;
                case Onboarding.StepIdle:
                    done = Profile.LastIdleClaimUnix != _idleSnapshot;
                    break;
            }
            if (done) Complete(step);
            return done;
        }

        /// <summary>The completions that are a screen change, from the router and the panel host.</summary>
        private void OnShown(ScreenId id)
        {
            if (_app == null || _app.Session == null) return;
            PlayerProfile profile = Profile;
            ScreenId last = _lastScreen;
            _lastScreen = id;

            if (!Onboarding.IsComplete(profile))
            {
                if (profile.TutorialStep == Onboarding.StepTeam && last == ScreenId.Formation && id != ScreenId.Formation)
                {
                    Onboarding.EnsureRecallHeroDeployed(_app.Session);
                    Complete(Onboarding.StepTeam);
                }
                else if (profile.TutorialStep == Onboarding.StepCampaign && id == ScreenId.WaveResult)
                {
                    Complete(Onboarding.StepCampaign);
                }
                // Screens met inside the flow are taught by it, so their tips are spent here.
                Onboarding.MarkSeen(profile, id.ToString());
                return;
            }

            string tip = Config != null ? Config.TipFor(id.ToString()) : null;
            if (tip == null) return;
            if (!Onboarding.MarkSeen(profile, id.ToString())) return;
            _app.RequestSave();
            ShowTip(tip);
        }

        private void Complete(int step)
        {
            if (!Onboarding.CompleteStep(Profile, step)) return;
            _app.SaveNow();
            if (Onboarding.IsComplete(Profile)) ShowOnly(Mode.None);
        }

        /// <summary>
        /// THE ROUTE: for the step being taught and the screen showing, the one control to light.
        /// Paths are node names under the screen, found depth-first; "a/b" finds b under a. Null
        /// is "nothing to light here", and the step then lets the screen be used.
        /// </summary>
        private RectTransform TargetFor(int step)
        {
            UIScreen screen = _app.Router.Current;
            if (screen == null) return null;
            ScreenId id = screen.Id;
            Transform root = screen.Root;

            switch (step)
            {
                case Onboarding.StepTeam:
                    if (id == ScreenId.MainMenu) return FindDeep(root, "NodeHEROES");
                    if (id == ScreenId.Formation) return FindDeep(root, "Header/Back");
                    break;
                case Onboarding.StepCampaign:
                    if (id == ScreenId.MainMenu) return FindDeep(root, "Campaign/Continue");
                    if (id == ScreenId.Campaign) return FindDeep(root, "Tray/EnterBattle");
                    break;
                case Onboarding.StepWaveResult:
                    if (id == ScreenId.WaveResult) return FindDeep(root, "NextWave") ?? FindDeep(root, "Retry");
                    break;
                case Onboarding.StepSecondStage:
                    if (id == ScreenId.WaveResult)
                        return FindDeep(root, "Continue") ?? FindDeep(root, "NextWave") ?? FindDeep(root, "Retry");
                    if (id == ScreenId.MainMenu) return FindDeep(root, "Campaign/Continue");
                    if (id == ScreenId.Campaign) return FindDeep(root, "Tray/EnterBattle");
                    return FindDeep(root, "Header/Back") ?? FindDeep(root, "Header/Home");
                case Onboarding.StepLevelUp:
                    if (id == ScreenId.WaveResult) return FindDeep(root, "Home");
                    if (id == ScreenId.MainMenu) return FindDeep(root, "NodeHEROES");
                    if (id == ScreenId.Formation) return FindDeep(root, "ViewAll");
                    if (id == ScreenId.Heroes) return FindDeep(root, "OpenHero") ?? FindDeep(root, "Card0");
                    if (id == ScreenId.HeroDetail) return FindDeep(root, "LevelUp");
                    break;
                case Onboarding.StepIdle:
                    if (id == ScreenId.MainMenu)
                    {
                        RectTransform claim = FindDeep(_app.OverlayLayer, "IdleClaim/Claim");
                        return claim != null ? claim : FindDeep(root, "IdleChest");
                    }
                    if (id == ScreenId.WaveResult) return FindDeep(root, "Home");
                    return FindDeep(root, "Header/Back") ?? FindDeep(root, "Header/Home");
            }
            return null;
        }

        /// <summary>Depth-first, active nodes only. "a/b" finds a, then b under it.</summary>
        public static RectTransform FindDeep(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform at = root;
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                at = FindNamed(at, parts[i]);
                if (at == null) return null;
            }
            return at as RectTransform;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.name == name) return child;
                Transform deeper = FindNamed(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>The target's rect in this overlay's own space, origin bottom left.</summary>
        private bool TryRectOf(RectTransform target, out Rect rect)
        {
            rect = new Rect(0f, 0f, 0f, 0f);
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 a = _root.InverseTransformPoint(corners[0]);
            Vector3 b = _root.InverseTransformPoint(corners[2]);
            rect = new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            return rect.width > 1f && rect.height > 1f;
        }

        /// <summary>
        /// Dims everything but <paramref name="lit"/>. With <paramref name="passInput"/> the lit
        /// control takes taps and nothing else does; without it nothing under the overlay does.
        /// </summary>
        private void Light(Rect lit, bool passInput)
        {
            float w = _root.rect.width, h = _root.rect.height;
            Rect hole = new Rect(lit.x - HolePad, lit.y - HolePad, lit.width + HolePad * 2f, lit.height + HolePad * 2f);

            // The input blockers hug the hole exactly.
            Place(_blockers[0], 0f, hole.yMax, w, h - hole.yMax);
            Place(_blockers[1], 0f, 0f, w, hole.y);
            Place(_blockers[2], 0f, hole.y, hole.x, hole.height);
            Place(_blockers[3], hole.xMax, hole.y, w - hole.xMax, hole.height);
            _fullBlock.gameObject.SetActive(!passInput);

            // The light is a soft ellipse a little larger than the hole; the dims fill around it.
            float sw = hole.width * 1.55f + 60f, sh = hole.height * 1.55f + 60f;
            Rect spot = new Rect(hole.center.x - sw * 0.5f, hole.center.y - sh * 0.5f, sw, sh);
            _spot.gameObject.SetActive(lit.width > 0f);
            _spot.anchoredPosition = spot.center;
            _spot.sizeDelta = new Vector2(sw, sh);
            if (lit.width <= 0f) spot = new Rect(0f, 0f, 0f, 0f);
            Place(_dims[0], 0f, spot.yMax, w, h - spot.yMax);
            Place(_dims[1], 0f, 0f, w, spot.y);
            Place(_dims[2], 0f, spot.y, spot.x, spot.height);
            Place(_dims[3], spot.xMax, spot.y, w - spot.xMax, spot.height);
            for (int i = 0; i < 4; i++)
            {
                _dims[i].gameObject.SetActive(true);
                _blockers[i].gameObject.SetActive(true);
            }

            bool any = lit.width > 0f;
            _ring.gameObject.SetActive(any);
            _ring.anchoredPosition = hole.center;
            float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f);
            _ring.sizeDelta = new Vector2(hole.width + RingPad * 2f, hole.height + RingPad * 2f) * pulse;

            _arrow.gameObject.SetActive(any && passInput);
            if (any && passInput)
            {
                // Above the target pointing down, unless the target is at the top of the screen.
                bool below = hole.yMax + ArrowSize + 40f > h - 120f;
                float bob = 10f * Mathf.Sin(Time.unscaledTime * 5f);
                _arrow.localEulerAngles = new Vector3(0f, 0f, below ? 180f : 0f);
                _arrow.anchoredPosition = below
                    ? new Vector2(hole.center.x, hole.y - RingPad - 8f - bob)
                    : new Vector2(hole.center.x, hole.yMax + RingPad + 8f + bob);
            }
        }

        private void Unlight()
        {
            for (int i = 0; i < 4; i++)
            {
                _dims[i].gameObject.SetActive(false);
                _blockers[i].gameObject.SetActive(false);
            }
            _spot.gameObject.SetActive(false);
            _fullBlock.gameObject.SetActive(false);
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        /// <summary>The step's line, on the side of the target with more room.</summary>
        private void PlaceCoachText(Rect target, string line)
        {
            _captionPlate.gameObject.SetActive(!string.IsNullOrEmpty(line));
            _caption.text = line;
            float w = _root.rect.width, h = _root.rect.height;
            bool above = target.center.y < h * 0.5f;
            float y = above ? target.yMax + ArrowSize + RingPad + 40f + CaptionHeight * 0.5f
                            : target.y - RingPad - 40f - CaptionHeight * 0.5f;
            if (above && y + CaptionHeight * 0.5f > h - 90f) y = target.y - RingPad - 40f - CaptionHeight * 0.5f;
            float x = Mathf.Clamp(target.center.x, CaptionWidth * 0.5f + EdgeMargin, w - CaptionWidth * 0.5f - EdgeMargin);
            _captionPlate.anchoredPosition = new Vector2(x, Mathf.Clamp(y, CaptionHeight, h - CaptionHeight));
        }

        private void HideCoachText()
        {
            _captionPlate.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ skip

        private void OnSkip()
        {
            _app.Audio.Tap();
            bool inPrologue = _mode == Mode.Prologue;
            _resultOpen = false;
            _prologueStarted = false;
            _squad = null;
            Onboarding.Skip(_app.Session, TimeUtil.NowUnix());
            _app.SaveNow();
            _app.RefreshHud();
            ShowOnly(Mode.None);
            if (inPrologue || (_app.Router.Current != null && _app.Router.Current.Id == ScreenId.MainMenu))
                _app.Router.GoHome();
        }

        // ------------------------------------------------------------------ free-play tips

        private void ShowTip(string line)
        {
            _tipText.text = line;
            _tip.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            if (_tipLayer == null && _app.Layers != null) _tipLayer = _app.Layers.Push("popup:tip", CloseTip);
        }

        private void DismissTip()
        {
            if (_tipLayer != null && _app.Layers != null)
            {
                _app.Layers.Dismiss(_tipLayer);
                return;
            }
            CloseTip();
        }

        private void CloseTip()
        {
            _tipLayer = null;
            if (_tip != null) _tip.gameObject.SetActive(false);
        }

        public bool TipShowing
        {
            get { return _tip != null && _tip.gameObject.activeSelf; }
        }

#if UNITY_EDITOR
        /// <summary>Capture diagnostics: where the dims and the light are.</summary>
        public string DescribeForCapture()
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder("root " + _root.rect + " mode " + _mode);
            for (int i = 0; i < 4; i++)
            {
                Image img = _dims[i].GetComponent<Image>();
                b.Append(" | dim" + i + " active " + _dims[i].gameObject.activeInHierarchy + " pos " + _dims[i].anchoredPosition
                         + " size " + _dims[i].sizeDelta + " color " + (img != null ? img.color.ToString() : "none"));
            }
            b.Append(" | spot " + _spot.gameObject.activeInHierarchy + " " + _spot.anchoredPosition + " " + _spot.sizeDelta);
            return b.ToString();
        }

        /// <summary>Capture door: the result card for the recall the profile already has.</summary>
        public void ShowRecallResultForCapture()
        {
            OnRecall();
        }
#endif
    }
}
