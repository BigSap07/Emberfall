using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Lists every relic the player can purchase with Relic Dust. Each entry shows the relic name,
    /// its grade badge, its own text - a legendary's one-sentence rule or a common's stat line -
    /// and the price. Buying adds it to the player's collection unassigned; assigning it to a hero
    /// is done from the Inventory screen.
    ///
    /// Both grades sell from the same list at the same price, which is a placeholder rather than a
    /// decision: DESIGN-RELIC-PROGRESSION defers relative pricing to the balance pass, and a
    /// legendary costing the same as a stat bundle is one of the things that pass has to fix.
    ///
    /// No aspect filtering of any kind: any relic can be purchased and assigned to any hero.
    /// </summary>
    public sealed class RelicShopScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.RelicShop; } }

        private Text _headerSubtitle;
        private Text _dustDisplay;
        private RectTransform _listContent;

        // Per-relic buy buttons rebuilt each OnShow so their disabled state stays accurate.
        private readonly List<UIButton> _buyButtons = new List<UIButton>();

        protected override void Build()
        {
            ScreenChrome.Header(Root, "RELIC SHOP", Router, out _headerSubtitle);

            // Dust balance banner sits between the chrome header and the scroll list.
            BuildDustBanner();

            ScrollRect scroll;
            _listContent = UIBuilder.ScrollList("RelicList", Root, out scroll,
                                                16f, new RectOffset(32, 32, 12, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 0f, ScreenChrome.HeaderTop + 108f + 72f, 0f, 0f);
        }

        private void BuildDustBanner()
        {
            RectTransform banner = UIBuilder.Node("DustBanner", Root);
            float top = ScreenChrome.HeaderTop + 108f + 8f;
            UIBuilder.Box(banner, 0f, -top, 900f, 56f, new Vector2(0.5f, 1f));

            Image bg = banner.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            UIBuilder.Frame("Edge", banner, UITheme.WithAlpha(UITheme.Ember, 0.45f), 14, 2);

            _dustDisplay = UIBuilder.Label("Dust", banner, "", UITheme.FontSmall,
                                           UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_dustDisplay.rectTransform, 12f);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            int dust = (int)App.Session.Profile.RelicDust;
            int price = App.Session.Balance.RelicShopPrice;
            _dustDisplay.text = "RELIC DUST   " + dust + "   (each relic costs " + price + " dust)";
            _headerSubtitle.text = App.Session.Content.Relics.Count + " RELICS AVAILABLE";

            // Clear old cards and button refs.
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);
            _buyButtons.Clear();

            List<RelicDef> relics = App.Session.Content.Relics;
            for (int i = 0; i < relics.Count; i++) BuildRelicCard(relics[i], dust, price);

            if (relics.Count == 0)
            {
                Text empty = UIBuilder.Label("Empty", _listContent, "No relics in the content database.",
                                             UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, 0f, 80f);
            }
        }

        private void BuildRelicCard(RelicDef relic, int dust, int price)
        {
            bool owned = App.Session.Profile.OwnsRelic(relic.Id);
            bool affordable = !owned && dust >= price;

            RectTransform card = UIBuilder.Node("Relic_" + relic.Id, _listContent);
            UIBuilder.Sized(card, 0f, 160f);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(20);
            bg.type = Image.Type.Sliced;
            bg.color = owned ? UITheme.WithAlpha(UITheme.PanelRaised, 0.7f) : UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(owned ? UITheme.Ember : UITheme.BorderSoft, 0.6f), 20, 2);

            // Icon disc on the left.
            RectTransform icon = UIBuilder.Node("Icon", card);
            UIBuilder.Box(icon, 28f, 0f, 80f, 80f, new Vector2(0f, 0.5f));
            Image iconDisc = icon.gameObject.AddComponent<Image>();
            iconDisc.sprite = TextureLab.Circle(96);
            iconDisc.color = UITheme.WithAlpha(UITheme.Ember, owned ? 0.55f : 0.22f);
            iconDisc.raycastTarget = false;
            Image iconRing = UIBuilder.Img("Ring", icon, TextureLab.Ring(96, 5),
                                           UITheme.WithAlpha(UITheme.Ember, 0.85f));
            UIBuilder.FillParent(iconRing.rectTransform, 0f);

            // Name.
            Text nameLabel = UIBuilder.Label("Name", card, relic.Name,
                                             UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(nameLabel.rectTransform, 128f, -22f, 0f, 34f, new Vector2(0f, 1f));
            nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameLabel.rectTransform.offsetMin = new Vector2(128f, -56f);
            nameLabel.rectTransform.offsetMax = new Vector2(-200f, -22f);

            // Grade badge, then the paired-hero hint beside it. The badge is on every card of both
            // grades: it is what tells a player whether the line below is a rule or a stat list.
            RectTransform gradeBadge = UIBuilder.Chip("Grade", card, UITheme.RelicGradeLabel(relic.Grade),
                                                      UITheme.RelicGradeColor(relic.Grade), UITheme.FontTiny);
            UIBuilder.Box(gradeBadge, 128f, -60f, 132f, 30f, new Vector2(0f, 1f));

            // Paired hero hint (display only, never a restriction, and legendary-only in practice -
            // the common grade is universal and carries no pairing at all).
            string pairedText = "";
            if (!string.IsNullOrEmpty(relic.PairedHeroId))
            {
                HeroDef paired = App.Session.Content.GetHero(relic.PairedHeroId);
                if (paired != null) pairedText = "Designed for " + paired.Name + "  -  any hero may equip it";
            }
            Text pairedLabel = UIBuilder.Label("Paired", card, pairedText,
                                               UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            pairedLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            pairedLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            pairedLabel.rectTransform.offsetMin = new Vector2(272f, -82f);
            pairedLabel.rectTransform.offsetMax = new Vector2(-200f, -56f);

            // The relic's own text: a legendary states its rule, a common states its numbers.
            // Both come from CardText() rather than being chosen here, so this screen and the
            // inventory cannot drift into describing the same relic two different ways.
            Text ruleLabel = UIBuilder.WrappedLabel("Rule", card, relic.CardText(),
                                                    UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            ruleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            ruleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            ruleLabel.rectTransform.offsetMin = new Vector2(128f, 20f);
            ruleLabel.rectTransform.offsetMax = new Vector2(-200f, -84f);

            // Buy button or owned badge on the right.
            if (owned)
            {
                RectTransform ownedBadge = UIBuilder.Chip("Owned", card, "OWNED", UITheme.Ember, UITheme.FontTiny);
                UIBuilder.Box(ownedBadge, -24f, 0f, 150f, 40f, new Vector2(1f, 0.5f));
            }
            else
            {
                UIButton buyButton = UIBuilder.GoldButton("Buy", card, price + " DUST", UITheme.FontSmall);
                UIBuilder.Box(buyButton.Root, -20f, 0f, 164f, 80f, new Vector2(1f, 0.5f));
                buyButton.Interactable = affordable;

                RelicDef captured = relic;
                buyButton.OnClick(delegate { OnBuy(captured); });
                _buyButtons.Add(buyButton);
            }
        }

        // ------------------------------------------------------------------ actions

        private void OnBuy(RelicDef relic)
        {
            bool success = App.Session.BuyRelic(relic.Id);
            if (!success)
            {
                App.Notify("Not enough Relic Dust.");
                return;
            }
            App.Audio.Reward();
            App.Notify(relic.Name + " added to your inventory.");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }
    }
}
