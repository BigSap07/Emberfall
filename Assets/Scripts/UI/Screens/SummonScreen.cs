using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
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

        /// <summary>
        /// How many pulls one tap buys. Only ever 1 or 10 - this is a toggle, not a spinner, so it
        /// is an int rather than an enum because every use of it is arithmetic (cost, loop count,
        /// the summary line) and none of it is a branch on which mode is selected.
        /// </summary>
        private int _pullCount = 1;
        private UIButton _toggleOne;
        private UIButton _toggleTen;

        private RectTransform _results;
        private CanvasGroup _resultsGroup;
        private RectTransform _resultsList;
        private Text _resultsTitle;
        private Text _resultsSummary;

        private RectTransform _reveal;
        private CanvasGroup _revealGroup;
        private RectTransform _revealPortraitHost;
        private RectTransform _revealStarHost;
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
            BuildResults();
        }

        private void BuildPortal()
        {
            // landscape: the portal takes the left half of the screen, the odds and the button
            // take the right, instead of everything stacking down the middle
            _portal = UIBuilder.Node("Portal", Root);
            UIBuilder.Box(_portal, -480f, -20f, 680f, 680f, new Vector2(0.5f, 0.5f));

            Image halo = UIBuilder.Img("Halo", _portal, TextureLab.Glow(256, 1.6f), UITheme.WithAlpha(UITheme.StarColor(5), 0.20f));
            UIBuilder.FillParent(halo.rectTransform, -70f);

            _portalRingOuter = UIBuilder.Img("RingOuter", _portal, TextureLab.Ring(256, 10), UITheme.WithAlpha(UITheme.Shard, 0.75f));
            UIBuilder.FillParent(_portalRingOuter.rectTransform, 10f);

            _portalRingInner = UIBuilder.Img("RingInner", _portal, TextureLab.Ring(256, 16), UITheme.WithAlpha(UITheme.Ember, 0.6f));
            UIBuilder.FillParent(_portalRingInner.rectTransform, 92f);

            _portalCore = UIBuilder.Img("Core", _portal, TextureLab.Glow(192, 2.4f), UITheme.WithAlpha(UITheme.StarColor(5), 0.55f));
            UIBuilder.FillParent(_portalCore.rectTransform, 150f);

            Image sigil = UIBuilder.Img("Sigil", _portal, TextureLab.Sigil(13, 128), UITheme.WithAlpha(UITheme.TextPrimary, 0.85f));
            UIBuilder.FillParent(sigil.rectTransform, 210f);
        }

        private void BuildRates()
        {
            RectTransform card = UIBuilder.Node("Rates", Root);
            UIBuilder.Box(card, 470f, 150f, 880f, 170f, new Vector2(0.5f, 0.5f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = UISkin.Panel(20);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.94f);
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 20, 2);

            Text heading = UIBuilder.Label("Heading", card, "RIFT ODDS", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(heading.rectTransform, 0f, -14f, 400f, 26f, new Vector2(0.5f, 1f));

            RectTransform row = UIBuilder.Node("Row", card);
            UIBuilder.Box(row, 0f, 18f, 820f, 78f, new Vector2(0.5f, 0f));
            UIBuilder.HStack(row, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            BalanceConfig balance = App.Session.Balance;
            float[] w = { balance.SummonWeight1, balance.SummonWeight2, balance.SummonWeight3,
                           balance.SummonWeight4, balance.SummonWeight5 };
            float total = 0f;
            for (int i = 0; i < w.Length; i++) total += w[i];
            if (total <= 0f) total = 1f;
            int tiersOffered = 0;
            int onlyTier = 0;
            for (int s = 1; s <= 5; s++)
                if (w[s - 1] > 0f) { tiersOffered++; onlyTier = s; }

            // The heading has to say WHAT the number is a chance OF, because the pool is currently
            // a single tier and "RIFT ODDS / 5 stars / 100%" reads as "100% chance of a good pull"
            // rather than "every pull is 5 stars". Those mean the same thing here, but only one of
            // them survives the pool being widened later, and only one of them is unambiguous now.
            //
            // When exactly one tier is offered the panel states the outcome outright instead of
            // leaving a lone 100% to be interpreted. Anything else falls back to naming the axis,
            // which is all a multi-tier table needs.
            heading.text = tiersOffered == 1
                ? "RIFT ODDS  -  EVERY PULL IS " + onlyTier + "-STAR"
                : "RIFT ODDS  -  CHANCE PER PULL, BY RARITY";

            for (int s = 1; s <= 5; s++)
                if (w[s - 1] > 0f) AddRate(row, s, w[s - 1] / total);
        }

        private void AddRate(RectTransform parent, int stars, float chance)
        {
            RectTransform cell = UIBuilder.Node("Rate" + stars, parent);
            UIBuilder.Flexible(cell, 1f, 0f);

            RectTransform starRow = Widgets.StarRow("Stars", cell, stars, 18f);
            UIBuilder.Box(starRow, 0f, 0f, 96f, 18f, new Vector2(0.5f, 1f));

            Text value = UIBuilder.Label("Value", cell, Mathf.Round(chance * 1000f) / 10f + "%", UITheme.FontBody,
                                         UITheme.TextPrimary, TextAnchor.LowerCenter);
            UIBuilder.Box(value.rectTransform, 0f, 6f, 260f, 34f, new Vector2(0.5f, 0f));
        }

        private void BuildActions()
        {
            _costLabel = UIBuilder.Label("Cost", Root, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(_costLabel.rectTransform, 470f, 20f, 860f, 36f, new Vector2(0.5f, 0.5f));

            // The pull button gives up its right-hand end to the count toggle rather than the
            // toggle being stacked somewhere else on the screen: the two controls are one decision
            // ("open the rift, this many times") and reading them left-to-right as a single row is
            // what makes the cost label above them unambiguous.
            _summonButton = UIBuilder.PrimaryButton("Summon", Root, "OPEN THE RIFT", UITheme.FontHeading);
            UIBuilder.Box(_summonButton.Root, 380f, -110f, 580f, 160f, new Vector2(0.5f, 0.5f));
            _summonButton.OnClick(DoSummon);

            _toggleOne = BuildCountToggle("PullOne", "x1", 1, -72f);
            _toggleTen = BuildCountToggle("PullTen", "x10", 10, -148f);
            ApplyToggleStyle();
        }

        /// <summary>
        /// One segment of the x1 / x10 control. Kept as two plain buttons rather than a Unity
        /// Toggle group because everything else on these screens is a UIButton and the selected
        /// look is just a colour swap - a ToggleGroup would add a second interaction model for one
        /// control.
        /// </summary>
        private UIButton BuildCountToggle(string name, string label, int count, float y)
        {
            UIButton button = UIBuilder.MakeButton(name, Root, label, UITheme.FontBody,
                                                   UITheme.PanelRaised, UITheme.Border, UITheme.TextPrimary);
            UIBuilder.Box(button.Root, 790f, y, 190f, 74f, new Vector2(0.5f, 0.5f));
            int captured = count;
            button.OnClick(delegate { SetPullCount(captured); });
            return button;
        }

        private void SetPullCount(int count)
        {
            if (_busy || _pullCount == count) return;
            App.Audio.Tap();
            _pullCount = count;
            ApplyToggleStyle();
            Refresh();
        }

        /// <summary>
        /// Paints the selected segment. Ember fill for the active count, the standard raised panel
        /// for the other, so which one a tap will buy is readable without reading the cost line.
        /// </summary>
        private void ApplyToggleStyle()
        {
            Style(_toggleOne, _pullCount == 1);
            Style(_toggleTen, _pullCount == 10);
        }

        private static void Style(UIButton button, bool selected)
        {
            if (button == null) return;
            if (button.Background != null)
                button.Background.color = selected ? UITheme.Ember : UITheme.PanelRaised;
            if (button.Frame != null)
                button.Frame.color = selected ? UITheme.EmberDeep : UITheme.Border;
            if (button.Label != null)
                button.Label.color = selected ? UITheme.TextOnAccent : UITheme.TextMuted;
        }

        private void BuildReveal()
        {
            _reveal = UIBuilder.Node("Reveal", Root);
            UIBuilder.FillParent(_reveal, 0f);
            _revealGroup = _reveal.gameObject.AddComponent<CanvasGroup>();

            Image dim = _reveal.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimReveal;

            _revealGlow = UIBuilder.Img("Glow", _reveal, TextureLab.Glow(256, 1.3f), UITheme.WithAlpha(UITheme.Gold, 0.3f));
            UIBuilder.Box(_revealGlow.rectTransform, 0f, 180f, 900f, 900f, new Vector2(0.5f, 0.5f));

            _revealPortraitHost = UIBuilder.Node("PortraitHost", _reveal);
            UIBuilder.Box(_revealPortraitHost, 0f, 250f, 340f, 340f, new Vector2(0.5f, 0.5f));

            _revealStarHost = UIBuilder.Node("StarHost", _reveal);
            UIBuilder.Box(_revealStarHost, 0f, 75f, 130f, 24f, new Vector2(0.5f, 0.5f));

            _revealTag = UIBuilder.Label("Tag", _reveal, "", UITheme.FontSmall, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_revealTag.rectTransform, 0f, 30f, 700f, 40f, new Vector2(0.5f, 0.5f));

            _revealName = UIBuilder.Label("Name", _reveal, "", UITheme.FontTitle, UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.Box(_revealName.rectTransform, 0f, -30f, 900f, 60f, new Vector2(0.5f, 0.5f));
            UIBuilder.Shadowed(_revealName, UITheme.InkDisplay, 4f);

            _revealTitle = UIBuilder.Label("Title", _reveal, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(_revealTitle.rectTransform, 0f, -84f, 900f, 36f, new Vector2(0.5f, 0.5f));

            _revealNote = UIBuilder.WrappedLabel("Note", _reveal, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_revealNote.rectTransform, 0f, -150f, 820f, 100f, new Vector2(0.5f, 0.5f));

            _revealClose = UIBuilder.GoldButton("Close", _reveal, "CONTINUE", UITheme.FontHeading);
            UIBuilder.Box(_revealClose.Root, 0f, 150f, 620f, 130f, new Vector2(0.5f, 0f));
            _revealClose.OnClick(CloseReveal);

            _reveal.gameObject.SetActive(false);
        }

        /// <summary>
        /// Where a pull's outcome is read, on this screen, without leaving it.
        ///
        /// Replaces a navigation: CloseReveal used to push HeroDetail on the reasoning that "the
        /// pull's meaning is the choice it forces". That holds for one new hero and falls apart for
        /// everything else - a duplicate has no choice to force, and ten pulls have ten of them, so
        /// the old path would have had to pick one hero to land on and silently drop the other
        /// nine. The list is also the only place the shard-to-currency conversion can actually be
        /// SEEN rather than inferred from a counter that moved.
        /// </summary>
        private void BuildResults()
        {
            _results = UIBuilder.Node("Results", Root);
            UIBuilder.FillParent(_results, 0f);
            _resultsGroup = _results.gameObject.AddComponent<CanvasGroup>();

            Image dim = _results.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimModal;

            RectTransform panel = UIBuilder.Node("Panel", _results);
            UIBuilder.Box(panel, 0f, 0f, 1020f, 880f, new Vector2(0.5f, 0.5f));

            Image panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = UISkin.Panel(24);
            panelBg.type = Image.Type.Sliced;
            panelBg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", panel, UITheme.Border, 24, 3);

            _resultsTitle = UIBuilder.Label("Title", panel, "", UITheme.FontHeading,
                                            UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(_resultsTitle.rectTransform, 0f, -26f, 940f, 46f, new Vector2(0.5f, 1f));

            _resultsSummary = UIBuilder.Label("Summary", panel, "", UITheme.FontSmall,
                                              UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_resultsSummary.rectTransform, 0f, -78f, 940f, 34f, new Vector2(0.5f, 1f));

            ScrollRect scroll;
            _resultsList = UIBuilder.ScrollList("List", panel, out scroll, 8f, new RectOffset(16, 16, 8, 8));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 20f, 124f, 20f, 132f);

            UIButton close = UIBuilder.GoldButton("ResultsClose", panel, "CONTINUE", UITheme.FontHeading);
            UIBuilder.Box(close.Root, 0f, 24f, 420f, 96f, new Vector2(0.5f, 0f));
            close.OnClick(CloseResults);

            _results.gameObject.SetActive(false);
        }

        /// <summary>
        /// One pull. New and duplicate are separated three ways at once - the row tint, the
        /// right-hand status, and its colour - because this is the whole point of the panel and a
        /// player scanning ten rows should not have to read any of them to count their new allies.
        /// The NAME carries the star colour, so rarity survives the pool being widened later even
        /// though every pull is 5-star today.
        /// </summary>
        private void AddResultRow(SummonOutcome outcome)
        {
            HeroDef def = outcome.Hero;

            RectTransform row = UIBuilder.Node("Result_" + def.Id, _resultsList);
            UIBuilder.Sized(row, 0f, 66f);

            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(12);
            bg.type = Image.Type.Sliced;
            bg.color = outcome.IsNew
                ? UITheme.WithAlpha(UITheme.Success, 0.16f)
                : UITheme.PanelRaised;

            Text name = UIBuilder.Label("Name", row, def.Name, UITheme.FontSmall,
                                        UITheme.StarColor(def.Stars), TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(24f, 0f);
            name.rectTransform.offsetMax = new Vector2(-460f, 0f);

            string status = outcome.IsNew
                ? "NEW ALLY"
                : "DUPLICATE  -  +" + outcome.GoldRefund + " gold, +" + outcome.ShardRefund + " shards";
            Text statusLabel = UIBuilder.Label("Status", row, status, UITheme.FontSmall,
                                               outcome.IsNew ? UITheme.Success : UITheme.Gold,
                                               TextAnchor.MiddleRight);
            statusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusLabel.rectTransform.offsetMin = new Vector2(440f, 0f);
            statusLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);
        }

        private void ShowResults(List<SummonOutcome> outcomes)
        {
            for (int i = _resultsList.childCount - 1; i >= 0; i--)
                Destroy(_resultsList.GetChild(i).gameObject);

            int newCount = 0;
            int dupeCount = 0;
            int gold = 0;
            int shards = 0;
            for (int i = 0; i < outcomes.Count; i++)
            {
                SummonOutcome o = outcomes[i];
                if (o.IsNew) newCount++;
                else
                {
                    dupeCount++;
                    gold += o.GoldRefund;
                    shards += o.ShardRefund;
                }
                AddResultRow(o);
            }

            _resultsTitle.text = outcomes.Count == 1
                ? "THE RIFT OPENS"
                : outcomes.Count + " PULLS";

            // States both halves even when one is zero. "3 NEW - 0 DUPLICATES" answers the question
            // a player actually has; omitting the zero leaves them counting rows to be sure.
            string summary = newCount + (newCount == 1 ? " NEW ALLY" : " NEW ALLIES")
                             + "   -   " + dupeCount + (dupeCount == 1 ? " DUPLICATE" : " DUPLICATES");
            if (dupeCount > 0)
                summary += "   -   CONVERTED TO +" + gold + " GOLD, +" + shards + " SHARDS";
            _resultsSummary.text = summary;

            _results.gameObject.SetActive(true);
            _results.SetAsLastSibling();
            _resultsGroup.alpha = 1f;
        }

        private void CloseResults()
        {
            App.Audio.Tap();
            _results.gameObject.SetActive(false);
            _busy = false;
            App.RefreshHud();
            Refresh();
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            App.RefreshHud();
            _busy = false;
            _reveal.gameObject.SetActive(false);
            _results.gameObject.SetActive(false);
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
            // Affordability is computed for the WHOLE batch here rather than deferring to
            // Session.CanSummon, which only ever answers for a single pull. Ten pulls are only
            // started when all ten are already paid for - see DoSummon for why a part-funded batch
            // is refused rather than run until the shards stop.
            long unit = App.Session.Balance.SummonCost;
            long cost = unit * _pullCount;
            bool affordable = App.Session.Profile.Shards >= cost;

            _costLabel.text = cost + " EMBER SHARDS  -  YOU HOLD " + App.Session.Profile.Shards;
            _summonButton.Interactable = affordable && !_busy;
            _summonButton.SetText(affordable
                ? "OPEN THE RIFT   " + cost + "S"
                : "NOT ENOUGH SHARDS");
        }

        private void DoSummon()
        {
            if (_busy) return;

            long unit = App.Session.Balance.SummonCost;
            long cost = unit * _pullCount;
            if (App.Session.Profile.Shards < cost)
            {
                App.Notify("You need " + cost + " Ember Shards for " + _pullCount + ".");
                return;
            }

            _busy = true;
            Refresh();
            App.Audio.Cast();

            // The batch is a LOOP over the existing single Summon, deliberately, and not a new
            // multi-pull path in GameSession. Everything that makes a pull what it is - the weight
            // table, the first-three unowned guarantee, duplicate conversion, the event activity
            // record - already lives in there and is driven off Profile.TotalSummons, which each
            // call advances. Looping gets all of it for free and keeps the odds this change was
            // told not to touch literally untouched. It also means pull 4 of a batch sees the state
            // pulls 1-3 left behind, which is what the pity rule expects.
            long now = TimeUtil.NowUnix();
            List<SummonOutcome> outcomes = new List<SummonOutcome>();
            for (int i = 0; i < _pullCount; i++)
            {
                // Same seed formula as the single pull, plus the index. TotalSummons and Shards
                // both move between iterations so the seed would vary anyway; i is there so two
                // pulls cannot collide if a duplicate refund happens to restore the exact shard
                // count a previous pull started from.
                int seed = App.Session.Profile.TotalSummons * 7919
                           + (int)(App.Session.Profile.Shards & 0xFFFF)
                           + Mathf.RoundToInt(Time.unscaledTime * 1000f)
                           + i * 104729;

                SummonOutcome outcome = App.Session.Summon(seed, now);
                if (!outcome.Success)
                {
                    // Affordability was checked for the whole batch above, so this is a content
                    // fault rather than a purse that ran dry. Keep whatever did land instead of
                    // discarding it, and say what went wrong.
                    App.Notify(outcome.Message);
                    break;
                }
                outcomes.Add(outcome);
            }

            App.RefreshHud();
            App.RequestSave();

            if (outcomes.Count == 0)
            {
                _busy = false;
                Refresh();
                return;
            }

            // A single pull keeps the portrait reveal - it is the one case where there is a single
            // subject worth showing at that size. The batch goes straight to the list, because ten
            // sequential reveals is ten taps to dismiss.
            if (outcomes.Count == 1) StartCoroutine(PlayReveal(outcomes[0]));
            else ShowResults(outcomes);
        }

        private IEnumerator PlayReveal(SummonOutcome outcome)
        {
            HeroDef def = outcome.Hero;
            Color starColor = UITheme.StarColor(def.Stars);

            for (int i = _revealPortraitHost.childCount - 1; i >= 0; i--)
                Destroy(_revealPortraitHost.GetChild(i).gameObject);
            RectTransform portrait = Widgets.Portrait("Portrait", _revealPortraitHost, def, def.Stars, 340f, true);
            UIBuilder.FillParent(portrait, 0f);

            for (int i = _revealStarHost.childCount - 1; i >= 0; i--)
                Destroy(_revealStarHost.GetChild(i).gameObject);
            Widgets.StarRow("Stars", _revealStarHost, def.Stars, 24f);

            _revealGlow.color = UITheme.WithAlpha(starColor, 0.34f);
            _revealTag.text = UITheme.RoleName(def.Role) + "  -  " + AspectChart.DisplayName(def.Aspect);
            _revealTag.color = starColor;
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

            if (def.Stars >= 5) App.Audio.Victory();
            else App.Audio.Reward();
        }

        /// <summary>
        /// Back to the Rift, not away from it.
        ///
        /// This used to push HeroDetail for the hero just pulled, on the reasoning that the pull's
        /// meaning is the choice it forces. The cost of that was the summon loop: a player who
        /// wanted a second pull was three taps and two screens from it, and one who pulled a
        /// duplicate got sent to a hero page they already knew about. Staying here also means the
        /// shard counter they are spending stays on screen the whole time.
        /// </summary>
        private void CloseReveal()
        {
            App.Audio.Tap();
            _reveal.gameObject.SetActive(false);
            _busy = false;
            App.RefreshHud();
            Refresh();
        }
    }
}
