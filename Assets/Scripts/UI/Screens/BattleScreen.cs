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
            public Image EnergyFill;
            public Image ShieldFill;
            public Text Status;
            public Image TurnPip;
            public CanvasGroup Group;
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
        private UIButton _resultPrimary;
        private UIButton _resultSecondary;

        private readonly Dictionary<int, UnitBar> _bars = new Dictionary<int, UnitBar>();
        private readonly List<FloatText> _floatPool = new List<FloatText>();

        private RectTransform _turnStrip;
        private readonly List<TurnStripIcon> _turnStripIcons = new List<TurnStripIcon>();
        private UnitBar _activeTurnBar;

        public void SetStage(StageDef stage)
        {
            _stage = stage;
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
            BuildResultPanel();
        }

        private void BuildTurnStrip()
        {
            _turnStrip = UIBuilder.Node("TurnStrip", Root);
            UIBuilder.Box(_turnStrip, 0f, -146f, 900f, 36f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_turnStrip, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        }

        private void BuildTopHud()
        {
            RectTransform bar = UIBuilder.Node("TopHud", Root);
            UIBuilder.TopStrip(bar, 112f, 24f, UITheme.ScreenPadding);

            Image backing = bar.gameObject.AddComponent<Image>();
            backing.sprite = TextureLab.RoundedRect(20);
            backing.type = Image.Type.Sliced;
            backing.color = new Color(0f, 0f, 0f, 0.55f);

            _stageLabel = UIBuilder.Label("Stage", bar, "", UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_stageLabel.rectTransform, 24f, -14f, 560f, 34f, new Vector2(0f, 1f));
            UIBuilder.Shadowed(_stageLabel, new Color(0f, 0f, 0f, 0.8f), 2f);

            _roundLabel = UIBuilder.Label("Round", bar, "", UITheme.FontTiny, UITheme.TextMuted, TextAnchor.LowerLeft);
            UIBuilder.Box(_roundLabel.rectTransform, 24f, 12f, 560f, 28f, new Vector2(0f, 0f));

            _speedButton = UIBuilder.SecondaryButton("Speed", bar, "1x", UITheme.FontSmall);
            UIBuilder.Box(_speedButton.Root, -24f, 0f, 128f, 76f, new Vector2(1f, 0.5f));
            _speedButton.OnClick(CycleSpeed);

            _skipButton = UIBuilder.SecondaryButton("Skip", bar, "SKIP", UITheme.FontSmall);
            UIBuilder.Box(_skipButton.Root, -164f, 0f, 150f, 76f, new Vector2(1f, 0.5f));
            _skipButton.OnClick(SkipToEnd);
        }

        private void BuildBanner()
        {
            _banner = UIBuilder.Node("Banner", Root);
            UIBuilder.Box(_banner, 0f, 250f, 980f, 132f, new Vector2(0.5f, 0.5f));
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;

            Image backing = _banner.gameObject.AddComponent<Image>();
            backing.sprite = TextureLab.RoundedRect(18);
            backing.type = Image.Type.Sliced;
            backing.color = new Color(0f, 0f, 0f, 0.62f);
            backing.raycastTarget = false;

            _bannerTitle = UIBuilder.Label("Title", _banner, "", UITheme.FontTitle, UITheme.Gold, TextAnchor.UpperCenter);
            UIBuilder.Box(_bannerTitle.rectTransform, 0f, -14f, 940f, 56f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_bannerTitle, new Color(0f, 0f, 0f, 0.9f), 2.5f);

            _bannerSub = UIBuilder.Label("Sub", _banner, "", UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.LowerCenter);
            UIBuilder.Box(_bannerSub.rectTransform, 0f, 14f, 940f, 36f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_bannerSub, new Color(0f, 0f, 0f, 0.9f), 2f);

            _banner.gameObject.SetActive(false);
        }

        private void BuildResultPanel()
        {
            _resultPanel = UIBuilder.Node("Result", Root);
            UIBuilder.FillParent(_resultPanel, 0f);
            _resultGroup = _resultPanel.gameObject.AddComponent<CanvasGroup>();

            Image dim = _resultPanel.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = new Color(0f, 0f, 0f, 0.78f);

            _resultTitle = UIBuilder.Label("Title", _resultPanel, "", UITheme.FontDisplay + 16, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultTitle.rectTransform, 0f, 420f, 980f, 110f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_resultTitle, new Color(0.2f, 0.05f, 0f, 0.95f), 4f);

            _resultSub = UIBuilder.Label("Sub", _resultPanel, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(_resultSub.rectTransform, 0f, 340f, 940f, 40f, new Vector2(0.5f, 0.5f));

            RectTransform card = UIBuilder.Node("Rewards", _resultPanel);
            UIBuilder.Box(card, 0f, 50f, 860f, 462f, new Vector2(0.5f, 0.5f));
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = TextureLab.RoundedRect(24);
            cardImage.type = Image.Type.Sliced;
            cardImage.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.Border, 24, 2);

            Text heading = UIBuilder.Label("Heading", card, "SPOILS", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(heading.rectTransform, 0f, -18f, 400f, 26f, new Vector2(0.5f, 1f));

            _rewardList = UIBuilder.Node("List", card);
            UIBuilder.FillParent(_rewardList, 28f, 58f, 28f, 24f);
            UIBuilder.VStack(_rewardList, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            _resultSecondary = UIBuilder.SecondaryButton("Again", _resultPanel, "FIGHT AGAIN", UITheme.FontBody);
            UIBuilder.Box(_resultSecondary.Root, 0f, 300f, 860f, 118f, new Vector2(0.5f, 0f));
            _resultSecondary.OnClick(Replay);

            _resultPrimary = UIBuilder.GoldButton("Continue", _resultPanel, "CONTINUE", UITheme.FontHeading);
            UIBuilder.Box(_resultPrimary.Root, 0f, 160f, 860f, 126f, new Vector2(0.5f, 0f));
            _resultPrimary.OnClick(LeaveBattle);

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

            if (_stage == null) _stage = App.Session.NextStage;
            if (_stage == null)
            {
                // no campaign content: bail out next frame rather than re-entering the router
                App.Notify("No campaign stages are loaded.");
                StartCoroutine(LeaveNextFrame());
                return;
            }

            _stageLabel.text = _stage.DisplayCode + "   " + _stage.Name.ToUpperInvariant();
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
            UIBuilder.Size(bar.Root, 210f, 110f);
            bar.Group = bar.Root.gameObject.AddComponent<CanvasGroup>();

            Color tint = snapshot.IsPlayer ? UITheme.HealthAlly : UITheme.HealthEnemy;

            // starts fully transparent; TurnCue lights it for the duration of this unit's turn
            bar.TurnPip = UIBuilder.Panel("TurnPip", bar.Root, new Color(1f, 1f, 1f, 0f), 6);
            UIBuilder.Box(bar.TurnPip.rectTransform, 0f, 108f, 22f, 22f, new Vector2(0.5f, 0f));

            // stacked upward from the unit's head: status, name, health, ward, energy
            bar.Status = UIBuilder.Label("Status", bar.Root, "", UITheme.FontTiny - 4, UITheme.Warning, TextAnchor.LowerCenter);
            UIBuilder.Box(bar.Status.rectTransform, 0f, 84f, 260f, 22f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(bar.Status, new Color(0f, 0f, 0f, 0.9f), 1.4f);

            bar.Name = UIBuilder.Label("Name", bar.Root, snapshot.Name + "  " + snapshot.Level,
                                       UITheme.FontTiny - 2, UITheme.TextPrimary, TextAnchor.LowerCenter);
            UIBuilder.Box(bar.Name.rectTransform, 0f, 58f, 300f, 24f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(bar.Name, new Color(0f, 0f, 0f, 0.95f), 1.6f);

            // ghost bar drains a moment after the real one, so a big hit is legible
            bar.HpGhost = UIBuilder.Bar("HpGhost", bar.Root, new Color(0f, 0f, 0f, 0.8f), new Color(1f, 1f, 1f, 0.5f));
            UIBuilder.Box(bar.HpGhost.rectTransform.parent as RectTransform, 0f, 36f, 200f, 18f, new Vector2(0.5f, 0f));

            bar.HpFill = UIBuilder.Bar("Hp", bar.Root, new Color(0f, 0f, 0f, 0f), tint);
            UIBuilder.Box(bar.HpFill.rectTransform.parent as RectTransform, 0f, 36f, 200f, 18f, new Vector2(0.5f, 0f));

            bar.ShieldFill = UIBuilder.Bar("Shield", bar.Root, new Color(0f, 0f, 0f, 0.6f), UITheme.WithAlpha(UITheme.Shield, 0.95f));
            UIBuilder.Box(bar.ShieldFill.rectTransform.parent as RectTransform, 0f, 27f, 200f, 7f, new Vector2(0.5f, 0f));
            UIBuilder.SetBar(bar.ShieldFill, 0f);

            bar.EnergyFill = UIBuilder.Bar("Energy", bar.Root, new Color(0f, 0f, 0f, 0.7f), UITheme.Energy);
            UIBuilder.Box(bar.EnergyFill.rectTransform.parent as RectTransform, 0f, 16f, 200f, 9f, new Vector2(0.5f, 0f));
            UIBuilder.SetBar(bar.EnergyFill, snapshot.EnergyMax > 0 ? snapshot.StartEnergy / (float)snapshot.EnergyMax : 0f);

            return bar;
        }

        private void LateUpdate()
        {
            if (_world == null || _world.Camera == null || Root == null || !Root.gameObject.activeInHierarchy) return;
            float scale = App.RootCanvas != null ? App.RootCanvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor actor = _world.Actors[i];
                UnitBar bar;
                if (!_bars.TryGetValue(actor.Snapshot.Id, out bar)) continue;

                Vector3 screen = _world.Camera.WorldToScreenPoint(actor.HeadPosition + Vector3.up * 0.28f);
                bool visible = screen.z > 0f && actor.Alive;
                bar.Group.alpha = visible ? 1f : Mathf.Max(0f, bar.Group.alpha - Time.deltaTime * 4f);
                if (!visible) continue;

                bar.Root.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale + bar.TurnLift);
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
                    if (_activeTurnBar != null && _activeTurnBar != bar) SettleTurnBar(_activeTurnBar);
                    _activeTurnBar = bar;
                    AdvanceTurnStrip(e.SourceId);
                    if (bar != null) yield return TurnCue(bar);
                    break;
                }

                case BattleEventType.BasicAttack:
                {
                    UnitActor source = ActorOf(e.SourceId);
                    List<int> targetIds = LookaheadTargets(index);
                    if (source != null)
                    {
                        // intent + motion: lunge toward the real target now, land the strike vfx at arrival
                        UnitActor focus = targetIds.Count > 0 ? ActorOf(targetIds[0]) : null;
                        source.PlayAttackLunge(focus != null ? focus.transform.position : source.transform.position, _speed);
                    }
                    yield return Wait(0.16f);
                    if (source != null)
                    {
                        for (int t = 0; t < targetIds.Count; t++)
                        {
                            UnitActor hit = ActorOf(targetIds[t]);
                            if (hit != null) PlayAbilityVfx(e.Vfx, source, hit, AspectTint(source));
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
                    yield return Wait(0.36f);
                    if (source != null) PlayTargetedVfx(e.Vfx, source, targetIds, AspectTint(source));
                    yield return Wait(0.12f);
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
                    if (bar != null && !bar.Statuses.Contains(e.Status))
                    {
                        bar.Statuses.Add(e.Status);
                        RefreshStatusText(bar);
                    }
                    if (target != null)
                    {
                        Color tint = StatusInfo.IsHarmful(e.Status) ? UITheme.Danger : UITheme.Success;
                        _world.Vfx.Aura(target.transform.position, tint, 1.8f);
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

                case BattleEventType.EnergyChanged:
                {
                    UnitActor actor = ActorOf(e.SourceId);
                    UnitBar bar = BarOf(e.SourceId);
                    if (actor != null && bar != null)
                    {
                        actor.Energy = e.Value;
                        float max = actor.Snapshot.EnergyMax > 0 ? actor.Snapshot.EnergyMax : 100f;
                        UIBuilder.SetBar(bar.EnergyFill, e.Value / max);
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
                bar.TurnPip.color = new Color(lit.r, lit.g, lit.b, k);
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
            bar.TurnPip.color = new Color(c.r, c.g, c.b, 0f);
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

        /// <summary>Full-alpha on the acting unit's icon, dimmed on everyone else in the strip.</summary>
        private void AdvanceTurnStrip(int unitId)
        {
            for (int i = 0; i < _turnStripIcons.Count; i++)
            {
                TurnStripIcon icon = _turnStripIcons[i];
                bool active = icon.UnitId == unitId;
                Color c = icon.Fill.color;
                icon.Fill.color = new Color(c.r, c.g, c.b, active ? 1f : 0.4f);
            }
        }

        private void ClearTurnStrip()
        {
            for (int i = 0; i < _turnStripIcons.Count; i++)
                if (_turnStripIcons[i].Root != null) Destroy(_turnStripIcons[i].Root.gameObject);
            _turnStripIcons.Clear();
        }

        private Color AspectTint(UnitActor actor)
        {
            return actor != null ? UITheme.AspectColor(actor.Snapshot.Aspect) : UITheme.TextPrimary;
        }

        /// <summary>WEAK when the target's aspect took bonus damage, RESIST when it took less - the ×1.20/×0.85 swing that decides fights, made readable.</summary>
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

        private static void RefreshStatusText(UnitBar bar)
        {
            string text = "";
            for (int i = 0; i < bar.Statuses.Count && i < 4; i++)
            {
                if (text.Length > 0) text += " ";
                text += StatusInfo.ShortTag(bar.Statuses[i]);
            }
            bar.Status.text = text;
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
            UIBuilder.Outlined(item.Label, new Color(0f, 0f, 0f, 0.95f), 2.4f);
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
            start.x += Random.Range(-34f, 34f);

            float duration = 0.75f / _speed;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                item.Root.anchoredPosition = start + new Vector2(0f, 120f * Mathf.Sqrt(k));
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

            BattleRewards rewards = App.Session.GrantBattleResult(_stage, _result.Victory, TimeUtil.NowUnix());
            App.RefreshHud();
            App.RequestSave();

            _resultTitle.text = _result.Victory ? "VICTORY" : "DEFEAT";
            _resultTitle.color = _result.Victory ? UITheme.Gold : UITheme.Danger;
            _resultSub.text = _result.Rounds + " ROUNDS"
                              + (_result.TimedOut ? "  -  TIME LIMIT REACHED" : "")
                              + "  -  BANNER AT " + Mathf.RoundToInt(_result.PlayerHpRatio * 100f) + "%";

            for (int i = _rewardList.childCount - 1; i >= 0; i--) Destroy(_rewardList.GetChild(i).gameObject);

            if (_result.Victory)
            {
                if (rewards.FirstClear) AddRewardRow("FIRST CLEAR BONUS", "INCLUDED", UITheme.Success);
                AddRewardRow("GOLD", "+" + rewards.Gold, UITheme.Gold);
                AddRewardRow("EMBER SHARDS", "+" + rewards.Shards, UITheme.Shard);
                AddRewardRow("WAYFARER XP", "+" + rewards.AccountXp, UITheme.Shard);
                if (rewards.PlayerLevelsGained > 0)
                    AddRewardRow("RANK UP", "+" + rewards.PlayerLevelsGained + "  (hero cap " + App.Session.HeroLevelCap + ")", UITheme.Success);
                if (rewards.UnlockedNextStage) AddRewardRow("NEW STAGE UNLOCKED", "AHEAD", UITheme.Ember);

                _resultPrimary.SetText("SPEND REWARDS");
                _resultSecondary.SetText("FIGHT AGAIN");
                App.Audio.Victory();
            }
            else
            {
                AddRewardRow("NOTHING RECOVERED", "-", UITheme.TextDim);
                AddRewardRow("LEVEL YOUR HEROES", "HERO SCREEN", UITheme.TextMuted);
                AddRewardRow("OR COLLECT THE CACHE", "MAIN SCREEN", UITheme.TextMuted);
                _resultPrimary.SetText("IMPROVE HEROES");
                _resultSecondary.SetText("TRY AGAIN");
                App.Audio.Defeat();
            }

            _resultPanel.gameObject.SetActive(true);
            _resultPanel.SetAsLastSibling();
            _resultGroup.alpha = 1f;
        }

        private void AddRewardRow(string label, string value, Color tint)
        {
            Widgets.StatRow(label, _rewardList, label, value, tint, 56f);
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

        private void LeaveBattle()
        {
            App.Audio.Tap();
            if (_result != null && _result.Victory) Router.Show(ScreenId.Campaign, false);
            else Router.Show(ScreenId.Heroes, false);
        }
    }
}
