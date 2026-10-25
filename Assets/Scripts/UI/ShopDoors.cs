using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// THE FOUR SHOP DOORS (R26), drawn once and shared by the Store's SHOPS tab and the Marketplace screen.
    /// Order, words and door ids come from MarketplaceShops (Core); this adds the picture, the purse line and
    /// the lock. Each card passes its own door id ("marketplace.RelicShop" ...) to the gate, so a card keeps
    /// its own lock line and its own one-time new dot. A shop whose screen this build does not contain says
    /// NOT IN THIS BUILD.
    /// </summary>
    public static class ShopDoors
    {
        public const float CardWidth = 420f;
        public const float CardGap = 26f;
        public const float CardHeight = 600f;
        public const float MinCardHeight = 440f;

        /// <summary>The router's screen for a shop, or null when this build does not contain it.</summary>
        public static UIScreen ScreenOf(ScreenRouter router, MarketplaceShop shop)
        {
            if (router == null) return null;
            switch (shop.Key)
            {
                case "RelicShop": return router.RelicShop;
                case "Quartermaster": return router.Quartermaster;
                case "Armory": return router.Armory;
                case "Archive": return router.Archive;
            }
            return null;
        }

        /// <summary>The door id this card passes to the gate. Spelled out here so each id is bound in the UI
        /// source (the harness proves every drawn door names its id); MarketplaceShops carries the same ids and
        /// a harness check keeps the two equal.</summary>
        public static string EntryOf(MarketplaceShop shop)
        {
            switch (shop.Key)
            {
                case "RelicShop": return "marketplace.RelicShop";
                case "Quartermaster": return "marketplace.Quartermaster";
                case "Armory": return "marketplace.Armory";
                default: return "marketplace.Archive";
            }
        }

        public static ScreenId IdOf(MarketplaceShop shop)
        {
            switch (shop.Key)
            {
                case "RelicShop": return ScreenId.RelicShop;
                case "Quartermaster": return ScreenId.Quartermaster;
                case "Armory": return ScreenId.Armory;
                default: return ScreenId.Archive;
            }
        }

        /// <summary>How many of the four can be walked into now: built in this build and not locked.</summary>
        public static int OpenCount(GameApp app, ScreenRouter router)
        {
            int open = 0;
            for (int i = 0; i < MarketplaceShops.All.Length; i++)
            {
                MarketplaceShop shop = MarketplaceShops.All[i];
                if (ScreenOf(router, shop) != null && FeatureGates.Allows(app, EntryOf(shop))) open++;
            }
            return open;
        }

        /// <summary>Draws the four cards in a row, top-centred under <paramref name="parent"/>, and returns how
        /// many are open.</summary>
        public static int Build(GameApp app, ScreenRouter router, RectTransform parent, float top)
        {
            return Build(app, router, parent, top, CardHeight);
        }

        /// <summary>The same cards at a chosen height, for a parent shorter than the cards' own 600 (the
        /// Store's body at 20:9). Down to <see cref="MinCardHeight"/> the inside spacing shrinks with the card
        /// (icon, gaps, description room, purse and button), so nothing overlaps; at full height it is unchanged.</summary>
        public static int Build(GameApp app, ScreenRouter router, RectTransform parent, float top, float cardHeight)
        {
            cardHeight = Mathf.Clamp(cardHeight, MinCardHeight, CardHeight);
            // 0 at the smallest card, 1 at the full one: the inner measures blend between the two
            float t = (cardHeight - MinCardHeight) / (CardHeight - MinCardHeight);
            float iconTop = Mathf.Lerp(20f, 36f, t);
            float iconSize = Mathf.Lerp(88f, 120f, t);
            float nameTop = Mathf.Lerp(116f, 176f, t);
            float paysTop = Mathf.Lerp(158f, 222f, t);
            float sellsTop = Mathf.Lerp(192f, 262f, t);
            float sellsHeight = Mathf.Lerp(84f, 150f, t);
            float buttonBottom = Mathf.Lerp(20f, 30f, t);
            float buttonHeight = Mathf.Lerp(56f, 64f, t);
            float purseBottom = buttonBottom + buttonHeight + Mathf.Lerp(8f, 48f, t);
            PlayerProfile p = app.Session.Profile;
            int count = MarketplaceShops.All.Length;
            float startX = -(CardWidth * count + CardGap * (count - 1)) * 0.5f + CardWidth * 0.5f;
            for (int i = 0; i < count; i++)
            {
                MarketplaceShop shop = MarketplaceShops.All[i];
                Sprite icon;
                Color tint;
                string purse;
                string kit;
                Look(shop, p, out icon, out tint, out purse, out kit);
                bool built = ScreenOf(router, shop) != null;

                RectTransform card = UIBuilder.Node("Shop" + i, parent);
                UIBuilder.Box(card, startX + i * (CardWidth + CardGap), top, CardWidth, cardHeight, new Vector2(0.5f, 1f));
                EmberforgeUI.PanelOn(card, true);

                Sprite kitMark = UiKit.Currency(kit);
                Image mark = UIBuilder.Img("Icon", card, kitMark != null ? kitMark : icon,
                                           kitMark != null ? Color.white : UITheme.WithAlpha(tint, 0.95f));
                UIBuilder.Box(mark.rectTransform, 0f, -iconTop, iconSize, iconSize, new Vector2(0.5f, 1f));
                Text name = EmberforgeUI.Title("Name", card, shop.Name, UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -nameTop, CardWidth - 24f, 40f, new Vector2(0.5f, 1f));
                UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);
                Text pays = EmberforgeUI.Label("Pays", card, "SPENDS " + shop.Pays.ToUpperInvariant(), UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(pays.rectTransform, 0f, -paysTop, CardWidth - 24f, 28f, new Vector2(0.5f, 1f));
                Text sells = EmberforgeUI.Body("Sells", card, shop.Sells, UITheme.FontSmall - 2, Emberforge.TextBody, TextAnchor.UpperCenter);
                UIBuilder.Box(sells.rectTransform, 0f, -sellsTop, CardWidth - 48f, sellsHeight, new Vector2(0.5f, 1f));
                Text purseLabel = EmberforgeUI.Label("Purse", card, "YOU HAVE " + purse.ToUpperInvariant(), UITheme.FontTiny, UITheme.Gold, TextAnchor.UpperCenter);
                UIBuilder.Box(purseLabel.rectTransform, 0f, purseBottom, CardWidth - 24f, 28f, new Vector2(0.5f, 0f));

                // The padlock and the one-time new dot of this shop's own door. No tag: the card has its chip.
                FeatureLockView.Attach(card, EntryOf(shop), name,
                                       new Vector2(-CardWidth * 0.5f + 40f, cardHeight * 0.5f - 40f),
                                       FeatureLockView.TagSide.Above, false).Refresh(app);

                string lockLine = built ? FeatureGates.LockLine(app, EntryOf(shop)) : "NOT IN THIS BUILD";
                if (lockLine.Length > 0)
                {
                    RectTransform chip = EmberforgeUI.Chip("Lock", card, lockLine.ToUpperInvariant(), Emberforge.TextBody, UITheme.FontTiny - 2);
                    UIBuilder.Box(chip, 0f, buttonBottom, CardWidth - 48f, Mathf.Min(52f, buttonHeight), new Vector2(0.5f, 0f));
                    continue;
                }
                // Four doors that are equal choices, none of them the thing to press now: all Secondary (R53).
                UIButton open = EmberforgeUI.Button("Open", card, "OPEN", UITheme.FontSmall, ForgeButtonKind.Secondary);
                UIBuilder.Box(open.Root, 0f, buttonBottom, CardWidth - 48f, buttonHeight, new Vector2(0.5f, 0f));
                ScreenId target = IdOf(shop);
                ScreenRouter capturedRouter = router;
                GameApp capturedApp = app;
                open.OnClick(delegate { capturedApp.Audio.Tap(); capturedRouter.Show(target); });
            }
            return OpenCount(app, router);
        }

        private static void Look(MarketplaceShop shop, PlayerProfile p, out Sprite icon, out Color tint, out string purse, out string kit)
        {
            switch (shop.Key)
            {
                case "RelicShop":
                    icon = TextureLab.Diamond(128); tint = UITheme.Dust; purse = HudTopBar.Format(p.RelicDust) + " Dust"; kit = "dust"; return;
                case "Quartermaster":
                    icon = TextureLab.Chest(128); tint = UITheme.Gold; purse = HudTopBar.Format(p.Gold) + " Gold"; kit = "gold"; return;
                case "Armory":
                    icon = TextureLab.Plate(128); tint = UITheme.Ember; purse = HudTopBar.Format(p.Valor) + " Valor"; kit = "valor"; return;
                default:
                    icon = TextureLab.SpiralIcon(128); tint = UITheme.Mythic; purse = HudTopBar.Format(p.Coda) + " Coda"; kit = "coda"; return;
            }
        }
    }
}
