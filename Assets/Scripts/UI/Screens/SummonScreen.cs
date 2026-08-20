using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>The Rift. Spend Ember Shards, get an ally.</summary>
    public sealed class SummonScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Summon; } }

        private Text _headerSubtitle;
        private RectTransform _portal;
        private Image _portalRingOuter;
        private Image _portalRingInner;
        private Image _portalCore;
        private Text _costLabel;
        private UIButton _summonButton;

        private RectTransform _reveal;
        private CanvasGroup _revealGroup;
        private RectTransform _revealPortraitHost;
        private Image _revealGlow;
        private Text _revealName;
        private Text _revealTitle;
        private Text _revealTag;
        private Text _revealNote;
        private UIButton _revealClose;

        private float _spin;
        private bool _busy;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "THE RIFT", Router, out _headerSubtitle);
            _headerSubtitle.text = "BIND A NEW ALLY TO YOUR BANNER";

            BuildPortal();
            BuildRates();
            BuildActions();
            BuildReveal();
        }

        private void BuildPortal()
        {
            _portal = UIBuilder.Node("Portal", Root);
            UIBuilder.Box(_portal, 0f, 120f, 760f, 760f, new Vector2(0.5f, 0.5f));

            Image halo = UIBuilder.Img("Halo", _portal, TextureLab.Glow(256, 1.6f), UITheme.WithAlpha(UITheme.RarityColor(Rarity.Epic), 0.20f));
            UIBuilder.FillParent(halo.rectTransform, -70f);

            _portalRingOuter = UIBuilder.Img("RingOuter", _portal, TextureLab.Ring(256, 10), UITheme.WithAlpha(UITheme.Shard, 0.75f));
            UIBuilder.FillParent(_portalRingOuter.rectTransform, 10f);

            _portalRingInner = UIBuilder.Img("RingInner", _portal, TextureLab.Ring(256, 16), UITheme.WithAlpha(UITheme.Ember, 0.6f));
            UIBuilder.FillParent(_portalRingInner.rectTransform, 92f);

            _portalCore = UIBuilder.Img("Core", _portal, TextureLab.Glow(192, 2.4f), UITheme.WithAlpha(UITheme.RarityColor(Rarity.Epic), 0.55f));
            UIBuilder.FillParent(_portalCore.rectTransform, 150f);

            Image sigil = UIBuilder.Img("Sigil", _portal, TextureLab.Sigil(13, 128), UITheme.WithAlpha(UITheme.TextPrimary, 0.85f));
            UIBuilder.FillParent(sigil.rectTransform, 210f);
        }

        private void BuildRates()
        {
            RectTransform card = UIBuilder.Node("Rates", Root);
            UIBuilder.Box(card, 0f, 346f, 940f, 170f, new Vector2(0.5f, 0f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(20);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.94f);
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 20, 2);

            Text heading = UIBuilder.Label("Heading", card, "RIFT ODDS", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(heading.rectTransform, 0f, -14f, 400f, 26f, new Vector2(0.5f, 1f));

            RectTransform row = UIBuilder.Node("Row", card);
            UIBuilder.Box(row, 0f, 18f, 880f, 78f, new Vector2(0.5f, 0f));
            UIBuilder.HStack(row, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            BalanceConfig balance = App.Session.Balance;
            float total = balance.SummonWeightCommon + balance.SummonWeightRare + balance.SummonWeightEpic;
            AddRate(row, Rarity.Common, balance.SummonWeightCommon / total);
            AddRate(row, Rarity.Rare, balance.SummonWeightRare / total);
            AddRate(row, Rarity.Epic, balance.SummonWeightEpic / total);
        }

        private void AddRate(RectTransform parent, Rarity rarity, float chance)
        {
            RectTransform cell = UIBuilder.Node("Rate" + rarity, parent);
            UIBuilder.Flexible(cell, 1f, 0f);

            Text label = UIBuilder.Label("Label", cell, UITheme.RarityName(rarity), UITheme.FontTiny,
                                         UITheme.RarityColor(rarity), TextAnchor.UpperCenter);
            UIBuilder.Box(label.rectTransform, 0f, 0f, 260f, 26f, new Vector2(0.5f, 1f));

            Text value = UIBuilder.Label("Value", cell, Mathf.Round(chance * 1000f) / 10f + "%", UITheme.FontBody,
                                         UITheme.TextPrimary, TextAnchor.LowerCenter);
            UIBuilder.Box(value.rectTransform, 0f, 6f, 260f, 34f, new Vector2(0.5f, 0f));
        }

        private void BuildActions()
        {
            _costLabel = UIBuilder.Label("Cost", Root, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_costLabel.rectTransform, 0f, 292f, 900f, 36f, new Vector2(0.5f, 0f));

            _summonButton = UIBuilder.PrimaryButton("Summon", Root, "OPEN THE RIFT", UITheme.FontHeading);
            UIBuilder.Box(_summonButton.Root, 0f, 120f, 860f, 160f, new Vector2(0.5f, 0f));
            _summonButton.OnClick(DoSummon);
        }

        private void BuildReveal()
        {
            _reveal = UIBuilder.Node("Reveal", Root);
            UIBuilder.FillParent(_reveal, 0f);
            _revealGroup = _reveal.gameObject.AddComponent<CanvasGroup>();

            Image dim = _reveal.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = new Color(0f, 0f, 0f, 0.85f);

            _revealGlow = UIBuilder.Img("Glow", _reveal, TextureLab.Glow(256, 1.3f), UITheme.WithAlpha(UITheme.Gold, 0.3f));
            UIBuilder.Box(_revealGlow.rectTransform, 0f, 180f, 900f, 900f, new Vector2(0.5f, 0.5f));

            _revealPortraitHost = UIBuilder.Node("PortraitHost", _reveal);
            UIBuilder.Box(_revealPortraitHost, 0f, 250f, 340f, 340f, new Vector2(0.5f, 0.5f));

            _revealTag = UIBuilder.Label("Tag", _reveal, "", UITheme.FontSmall, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_revealTag.rectTransform, 0f, 30f, 700f, 40f, new Vector2(0.5f, 0.5f));

            _revealName = UIBuilder.Label("Name", _reveal, "", UITheme.FontTitle, UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.Box(_revealName.rectTransform, 0f, -30f, 900f, 60f, new Vector2(0.5f, 0.5f));
            UIBuilder.Shadowed(_revealName, new Color(0f, 0f, 0f, 0.7f), 4f);

            _revealTitle = UIBuilder.Label("Title", _reveal, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(_revealTitle.rectTransform, 0f, -84f, 900f, 36f, new Vector2(0.5f, 0.5f));

            _revealNote = UIBuilder.WrappedLabel("Note", _reveal, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_revealNote.rectTransform, 0f, -150f, 820f, 100f, new Vector2(0.5f, 0.5f));

            _revealClose = UIBuilder.GoldButton("Close", _reveal, "CONTINUE", UITheme.FontHeading);
            UIBuilder.Box(_revealClose.Root, 0f, 150f, 620f, 130f, new Vector2(0.5f, 0f));
            _revealClose.OnClick(CloseReveal);

            _reveal.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            App.RefreshHud();
            _busy = false;
            _reveal.gameObject.SetActive(false);
            Refresh();
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _spin += Time.unscaledDeltaTime;

            if (_portalRingOuter != null)
                _portalRingOuter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _spin * 22f);
            if (_portalRingInner != null)
                _portalRingInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_spin * 34f);
            if (_portalCore != null)
            {
                float pulse = 1f + Mathf.Sin(_spin * 2.2f) * 0.06f;
                _portalCore.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        private void Refresh()
        {
            long cost = App.Session.Balance.SummonCost;
            _costLabel.text = cost + " EMBER SHARDS  -  YOU HOLD " + App.Session.Profile.Shards;
            _summonButton.Interactable = App.Session.CanSummon && !_busy;
            _summonButton.SetText(App.Session.CanSummon ? "OPEN THE RIFT   " + cost + "S" : "NOT ENOUGH SHARDS");
        }

        private void DoSummon()
        {
            if (_busy) return;
            if (!App.Session.CanSummon)
            {
                App.Notify("You need " + App.Session.Balance.SummonCost + " Ember Shards.");
                return;
            }

            _busy = true;
            Refresh();
            App.Audio.Cast();

            int seed = App.Session.Profile.TotalSummons * 7919
                       + (int)(App.Session.Profile.Shards & 0xFFFF)
                       + Mathf.RoundToInt(Time.unscaledTime * 1000f);
            SummonOutcome outcome = App.Session.Summon(seed, TimeUtil.NowUnix());

            App.RefreshHud();
            App.RequestSave();

            if (!outcome.Success)
            {
                _busy = false;
                App.Notify(outcome.Message);
                Refresh();
                return;
            }

            StartCoroutine(PlayReveal(outcome));
        }

        private IEnumerator PlayReveal(SummonOutcome outcome)
        {
            HeroDef def = outcome.Hero;
            Color rarityColor = UITheme.RarityColor(def.Rarity);

            for (int i = _revealPortraitHost.childCount - 1; i >= 0; i--)
                Destroy(_revealPortraitHost.GetChild(i).gameObject);
            RectTransform portrait = Widgets.Portrait("Portrait", _revealPortraitHost, def, def.Rarity, 340f, true);
            UIBuilder.FillParent(portrait, 0f);

            _revealGlow.color = UITheme.WithAlpha(rarityColor, 0.34f);
            _revealTag.text = UITheme.RarityName(def.Rarity) + "  -  " + UITheme.RoleName(def.Role)
                              + "  -  " + AspectChart.DisplayName(def.Aspect);
            _revealTag.color = rarityColor;
            _revealName.text = def.Name;
            _revealTitle.text = def.Title.ToUpperInvariant();
            _revealNote.text = outcome.IsNew
                ? "NEW ALLY  -  " + outcome.Message
                : outcome.Message + "  Converted to " + outcome.GoldRefund + " gold and " + outcome.ShardRefund + " shards.";
            _revealNote.color = outcome.IsNew ? UITheme.Success : UITheme.TextMuted;

            _reveal.gameObject.SetActive(true);
            _reveal.SetAsLastSibling();
            _revealGroup.alpha = 0f;

            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                _revealGroup.alpha = k;
                float scale = Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0f, 1f, k));
                _revealPortraitHost.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            _revealGroup.alpha = 1f;
            _revealPortraitHost.localScale = Vector3.one;

            if (def.Rarity == Rarity.Epic) App.Audio.Victory();
            else App.Audio.Reward();
        }

        private void CloseReveal()
        {
            App.Audio.Tap();
            _reveal.gameObject.SetActive(false);
            _busy = false;
            Refresh();
        }
    }
}
