using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI
{
    /// <summary>Reusable game-specific pieces built on top of UIBuilder.</summary>
    public static class Widgets
    {
        /// <summary>Circular unit portrait: aspect-tinted disc, emblem, rarity ring.</summary>
        public static RectTransform Portrait(string name, Transform parent, UnitDef def, Rarity rarity, float size, bool showRing)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, size, size);

            Color primary = UITheme.Hex(def.Visual.Primary);
            Color secondary = UITheme.Hex(def.Visual.Secondary);
            Color accent = UITheme.Hex(def.Visual.Accent);

            Image glow = UIBuilder.Img("Glow", root, TextureLab.Glow(128, 1.9f), UITheme.WithAlpha(primary, 0.55f));
            UIBuilder.FillParent(glow.rectTransform, -size * 0.16f);

            Image disc = UIBuilder.Img("Disc", root, TextureLab.Circle(160), secondary);
            UIBuilder.FillParent(disc.rectTransform, 0f);

            Image inner = UIBuilder.Img("Inner", root, TextureLab.Glow(128, 1.1f), UITheme.WithAlpha(primary, 0.85f));
            UIBuilder.FillParent(inner.rectTransform, size * 0.11f);

            Image sigil = UIBuilder.Img("Sigil", root, TextureLab.Sigil(def.Visual.Sigil, 96), accent);
            UIBuilder.FillParent(sigil.rectTransform, size * 0.19f);

            if (showRing)
            {
                Image ring = UIBuilder.Img("Ring", root, TextureLab.Ring(160, 8), UITheme.RarityColor(rarity));
                UIBuilder.FillParent(ring.rectTransform, 0f);
            }

            return root;
        }

        /// <summary>Row of small stars indicating rarity.</summary>
        public static RectTransform StarRow(string name, Transform parent, Rarity rarity, float starSize)
        {
            int count = (int)rarity;
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, starSize * count + 4f * (count - 1), starSize);
            UIBuilder.HStack(root, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            for (int i = 0; i < count; i++)
            {
                Image star = UIBuilder.Img("Star" + i, root, TextureLab.Sigil(13, 48), UITheme.RarityColor(rarity));
                UIBuilder.Sized(star.rectTransform, starSize, starSize);
            }
            return root;
        }

        /// <summary>One row of a stat table: label on the left, value on the right.</summary>
        public static Text StatRow(string name, Transform parent, string label, string value, Color valueColor, float height)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(root, 0f, height);

            Text labelText = UIBuilder.Label("Label", root, label, UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(labelText.rectTransform, 16f, 0f, 0f, 0f);

            Text valueText = UIBuilder.Label("Value", root, value, UITheme.FontSmall, valueColor, TextAnchor.MiddleRight);
            UIBuilder.FillParent(valueText.rectTransform, 0f, 0f, 16f, 0f);

            Image rule = UIBuilder.Img("Rule", root, TextureLab.White, UITheme.WithAlpha(UITheme.BorderSoft, 0.6f));
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.offsetMin = new Vector2(8f, 0f);
            rule.rectTransform.offsetMax = new Vector2(-8f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);

            return valueText;
        }

        /// <summary>Card describing one ability, used on the hero detail screen.</summary>
        public static RectTransform AbilityCard(string name, Transform parent, string kindLabel, string abilityName,
                                                string description, Color tint, int sigilIndex)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(root, 0f, 172f);

            Image background = root.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(18);
            background.type = Image.Type.Sliced;
            background.color = UITheme.PanelSunken;
            UIBuilder.Frame("Edge", root, UITheme.WithAlpha(tint, 0.35f), 18, 2);

            RectTransform icon = UIBuilder.Node("Icon", root);
            UIBuilder.Box(icon, 20f, 0f, 86f, 86f, new Vector2(0f, 0.5f));
            Image iconDisc = icon.gameObject.AddComponent<Image>();
            iconDisc.sprite = TextureLab.Circle(96);
            iconDisc.color = UITheme.WithAlpha(tint, 0.22f);
            iconDisc.raycastTarget = false;
            Image iconRing = UIBuilder.Img("Ring", icon, TextureLab.Ring(96, 5), UITheme.WithAlpha(tint, 0.8f));
            UIBuilder.FillParent(iconRing.rectTransform, 0f);
            Image glyph = UIBuilder.Img("Glyph", icon, TextureLab.Sigil(sigilIndex, 64), tint);
            UIBuilder.FillParent(glyph.rectTransform, 20f);

            Text kind = UIBuilder.Label("Kind", root, kindLabel, UITheme.FontTiny, tint, TextAnchor.UpperLeft);
            UIBuilder.Box(kind.rectTransform, 124f, -18f, 400f, 24f, new Vector2(0f, 1f));

            Text title = UIBuilder.Label("Name", root, abilityName, UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 124f, -44f, 500f, 34f, new Vector2(0f, 1f));

            Text body = UIBuilder.WrappedLabel("Body", root, description, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(body.rectTransform, 124f, -84f, 0f, 72f, new Vector2(0f, 1f));
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(124f, -156f);
            body.rectTransform.offsetMax = new Vector2(-24f, -84f);

            return root;
        }

        /// <summary>Collection tile. The whole tile is the button.</summary>
        public static UIButton HeroCard(string name, Transform parent, HeroInstance hero, bool deployed, Action onClick)
        {
            HeroDef def = hero.Def;
            UIButton card = new UIButton();
            card.Root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(card.Root, 0f, 300f);

            card.Background = card.Root.gameObject.AddComponent<Image>();
            card.Background.sprite = TextureLab.RoundedRect(20);
            card.Background.type = Image.Type.Sliced;
            card.Background.color = UITheme.Panel;

            card.Button = card.Root.gameObject.AddComponent<Button>();
            card.Button.targetGraphic = card.Background;
            card.Root.gameObject.AddComponent<ButtonPress>();
            card.OnClick(onClick);

            Color aspect = UITheme.AspectColor(def.Aspect);

            Image wash = UIBuilder.Img("Wash", card.Root, TextureLab.Glow(128, 1.5f), UITheme.WithAlpha(aspect, 0.16f));
            UIBuilder.FillParent(wash.rectTransform, 0f);

            card.Frame = UIBuilder.Frame("Edge", card.Root, UITheme.WithAlpha(UITheme.RarityColor(def.Rarity), 0.6f), 20, 2);

            RectTransform portrait = Portrait("Portrait", card.Root, def, def.Rarity, 128f, true);
            UIBuilder.Box(portrait, 0f, -18f, 128f, 128f, new Vector2(0.5f, 1f));

            Text nameLabel = UIBuilder.Label("Name", card.Root, def.Name, UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -156f, 260f, 30f, new Vector2(0.5f, 1f));

            RectTransform stars = StarRow("Stars", card.Root, def.Rarity, 18f);
            UIBuilder.Box(stars, 0f, -190f, 120f, 18f, new Vector2(0.5f, 1f));

            RectTransform roleChip = UIBuilder.Chip("Role", card.Root, UITheme.RoleName(def.Role), UITheme.RoleColor(def.Role), UITheme.FontTiny);
            UIBuilder.Box(roleChip, 0f, -222f, 170f, 32f, new Vector2(0.5f, 1f));

            Text level = UIBuilder.Label("Level", card.Root, "Lv " + hero.Level, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.LowerLeft);
            UIBuilder.Box(level.rectTransform, 18f, 14f, 120f, 26f, new Vector2(0f, 0f));

            Text power = UIBuilder.Label("Power", card.Root, hero.Power.ToString(), UITheme.FontTiny, UITheme.Gold, TextAnchor.LowerRight);
            UIBuilder.Box(power.rectTransform, -18f, 14f, 140f, 26f, new Vector2(1f, 0f));

            if (deployed)
            {
                RectTransform badge = UIBuilder.Chip("Deployed", card.Root, "DEPLOYED", UITheme.Success, UITheme.FontTiny);
                UIBuilder.Box(badge, -10f, -10f, 150f, 30f, new Vector2(1f, 1f));
            }

            return card;
        }

        /// <summary>
        /// A hero the player hasn't summoned yet: same card shape as HeroCard so the grid reads
        /// as one collection, but the portrait is a dimmed silhouette and there is no level/power/
        /// deployed state to show - there's no HeroInstance, only the definition.
        /// </summary>
        public static UIButton LockedHeroCard(string name, Transform parent, HeroDef def, Action onClick)
        {
            UIButton card = new UIButton();
            card.Root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(card.Root, 0f, 300f);

            card.Background = card.Root.gameObject.AddComponent<Image>();
            card.Background.sprite = TextureLab.RoundedRect(20);
            card.Background.type = Image.Type.Sliced;
            card.Background.color = UITheme.PanelSunken;

            card.Button = card.Root.gameObject.AddComponent<Button>();
            card.Button.targetGraphic = card.Background;
            card.Root.gameObject.AddComponent<ButtonPress>();
            card.OnClick(onClick);

            card.Frame = UIBuilder.Frame("Edge", card.Root, UITheme.WithAlpha(UITheme.BorderSoft, 0.7f), 20, 2);

            RectTransform portrait = Portrait("Portrait", card.Root, def, def.Rarity, 128f, false);
            UIBuilder.Box(portrait, 0f, -18f, 128f, 128f, new Vector2(0.5f, 1f));
            // dimmed silhouette: a dark scrim over the portrait hides the palette/sigil detail
            // without hiding the shape, and the whole card fades to signal "not yours yet"
            Image scrim = UIBuilder.Img("Scrim", portrait, TextureLab.Circle(160), UITheme.ScrimModal);
            UIBuilder.FillParent(scrim.rectTransform, 0f);
            card.Background.color = UITheme.WithAlpha(card.Background.color, 0.7f);

            Text nameLabel = UIBuilder.Label("Name", card.Root, def.Name, UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -156f, 260f, 30f, new Vector2(0.5f, 1f));

            RectTransform aspectChip = UIBuilder.Chip("Aspect", card.Root, AspectChart.DisplayName(def.Aspect), UITheme.AspectColor(def.Aspect), UITheme.FontTiny);
            UIBuilder.Box(aspectChip, 0f, -190f, 170f, 32f, new Vector2(0.5f, 1f));

            RectTransform roleChip = UIBuilder.Chip("Role", card.Root, UITheme.RoleName(def.Role), UITheme.RoleColor(def.Role), UITheme.FontTiny);
            UIBuilder.Box(roleChip, 0f, -226f, 170f, 32f, new Vector2(0.5f, 1f));

            RectTransform lockedBadge = UIBuilder.Chip("Locked", card.Root, "LOCKED", UITheme.TextDim, UITheme.FontTiny);
            UIBuilder.Box(lockedBadge, -10f, -10f, 150f, 30f, new Vector2(1f, 1f));

            return card;
        }

        /// <summary>Small aspect + level tag used under enemy previews in the campaign list.</summary>
        public static RectTransform EnemyPip(string name, Transform parent, UnitDef def, int level, float size)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, size, size);
            UIBuilder.Sized(root, size, size);

            Image disc = root.gameObject.AddComponent<Image>();
            disc.sprite = TextureLab.Circle(96);
            disc.color = UITheme.Hex(def.Visual.Secondary);
            disc.raycastTarget = false;

            Image glyph = UIBuilder.Img("Glyph", root, TextureLab.Sigil(def.Visual.Sigil, 64), UITheme.Hex(def.Visual.Primary));
            UIBuilder.FillParent(glyph.rectTransform, size * 0.2f);

            Image ring = UIBuilder.Img("Ring", root, TextureLab.Ring(96, 4), UITheme.WithAlpha(UITheme.AspectColor(def.Aspect), 0.8f));
            UIBuilder.FillParent(ring.rectTransform, 0f);

            Text levelLabel = UIBuilder.Label("Level", root, level.ToString(), UITheme.FontTiny, UITheme.TextPrimary, TextAnchor.LowerRight);
            UIBuilder.Box(levelLabel.rectTransform, 4f, -4f, 40f, 22f, new Vector2(1f, 0f));
            UIBuilder.Outlined(levelLabel, UITheme.InkArt, 1.5f);

            // aspect is a reason to change the line, not just a number to compare - the ring
            // colour alone asks the player to have memorised the palette, so name it too
            string aspectCode = AspectChart.DisplayName(def.Aspect);
            if (aspectCode.Length > 3) aspectCode = aspectCode.Substring(0, 3);
            Text aspectLabel = UIBuilder.Label("Aspect", root, aspectCode, UITheme.FontTiny - 4, UITheme.AspectColor(def.Aspect), TextAnchor.UpperLeft);
            UIBuilder.Box(aspectLabel.rectTransform, -2f, 2f, 46f, 18f, new Vector2(0f, 1f));
            UIBuilder.Outlined(aspectLabel, UITheme.InkArt, 1.3f);

            return root;
        }
    }
}
