using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI
{
    /// <summary>
    /// THE ONE ITEM TILE (R47). Every place the game draws an item, owned or on offer, comes through here
    /// (ItemCard forwards to it), so a Gilded helm is the same tile in the Inventory, the Forge, a hero's slot
    /// column, a shop shelf and a reward row.
    ///
    /// ONE ENTRY POINT FOR "A THING THAT IS NOT OWNED". ForSlot takes slot, temper and aspect and nothing else,
    /// so a store offer, a reward preview and a bag piece all call the same method with the same three facts.
    /// ForArmor is only ForSlot with the facts read off a piece.
    ///
    /// A ROUNDED PLATE COLOURED BY TIER. The plate and rim come from ItemTilePalette (the only place the rarity
    /// colours are defined). The top tier adds a faint glow ring and a corner gem. A thicker rim and one more
    /// lit pip per tier carry the same rank without colour; the harness proves all four channels rise.
    ///
    /// FIXED CORNERS, and nothing else on the tile, at most four marks:
    ///   top left      the aspect set icon, small
    ///   top right     the temper (or relic tier) number
    ///   bottom row    the tier pips
    ///   bottom left   the equipped check, when the piece is worn
    /// The gem on a top-tier plate sits on the bottom-right corner of the frame. It is part of the plate, not a
    /// fifth mark. An empty or locked slot is the same plate dimmed, with a faint silhouette and no marks.
    ///
    /// FALLBACK. With the kit off or a sprite missing, the slot mark is the old drawn shape (ItemCard.SlotIcon),
    /// the aspect mark is a small diamond in the aspect's accent, and the check is the old drawn check. Nothing
    /// here throws on a missing sprite.
    /// </summary>
    public static class ItemTile
    {
        private const float PipFraction = 0.115f;
        private const float PipGapFraction = 0.045f;
        private const float IconFraction = 0.58f;
        private const float RelicIconFraction = 0.66f;
        private const float AspectFraction = 0.22f;
        private const float CornerGemFraction = 0.2f;

        // ------------------------------------------------------------------ entry points

        /// <summary>A piece of armor or a weapon described by its three facts. The entry point for store offers.</summary>
        public static RectTransform ForSlot(string name, Transform parent, float size, ArmorSlot slot,
                                            ArmorTemper temper, Aspect aspect, bool equipped)
        {
            int tier = (int)temper;
            RectTransform tile = Plate(name, parent, size, ItemTilePalette.ArmorPlate(tier),
                                       ItemTilePalette.ArmorRim(tier), ItemTilePalette.ArmorFrame(tier),
                                       ItemTilePalette.ArmorHasGem(tier));
            Color rim = Rgb(ItemTilePalette.ArmorRim(tier));

            Silhouette(tile, size, ItemCard.SlotIcon(slot), IconFraction, ItemCard.MarkScale(ItemCard.SlotIcon(slot)),
                       true, ItemTilePalette.ArmorMarks(tier) > 0);
            AspectMark(tile, size, aspect);
            Number(tile, size, (tier + 1).ToString(), rim);
            Pips(tile, size, rim, ItemTilePalette.ArmorMarks(tier), ItemTilePalette.ArmorTiers);
            if (equipped) Equipped(tile, size);
            return tile;
        }

        /// <summary>A piece the player owns: its slot, temper and aspect read straight off it.</summary>
        public static RectTransform ForArmor(string name, Transform parent, float size, ArmorPiece piece, bool equipped)
        {
            return ForSlot(name, parent, size, piece.Slot, piece.Temper, piece.Aspect, equipped);
        }

        /// <summary>A relic. Two grades, so two plates; the painted relic icon stays as it is (a relic is never a
        /// slot silhouette) and there is no aspect mark, because a relic has no aspect.</summary>
        public static RectTransform ForRelic(string name, Transform parent, float size, RelicDef def,
                                             int tier, int maxTier, bool equipped)
        {
            int grade = def.Grade == RelicGrade.Mythic ? 1 : 0;
            RectTransform tile = Plate(name, parent, size, ItemTilePalette.RelicPlate(grade),
                                       ItemTilePalette.RelicRim(grade), ItemTilePalette.RelicFrame(grade),
                                       ItemTilePalette.RelicHasGem(grade));
            Color rim = Rgb(ItemTilePalette.RelicRim(grade));
            int max = Mathf.Max(1, maxTier);
            int lit = Mathf.Clamp(tier, 0, max);

            Sprite kit = UiKit.Relic(def.Id);
            if (kit != null) Silhouette(tile, size, kit, RelicIconFraction, 1f, false, true);
            else Silhouette(tile, size, TextureLab.Sigil(ItemCard.RelicSigil(def.Id), 128), IconFraction, 1f, true, true);
            if (lit > 0) Number(tile, size, lit.ToString(), rim);
            Pips(tile, size, rim, lit, max);
            if (equipped) Equipped(tile, size);
            return tile;
        }

        /// <summary>A slot with nothing in it, or a locked one: the plate dimmed, a faint silhouette, no marks.
        /// Same footprint as a full tile so a column of slots stays a column.</summary>
        public static RectTransform Vacant(string name, Transform parent, float size, ArmorSlot slot)
        {
            RectTransform tile = Plate(name, parent, size, ItemTilePalette.DimPlate, ItemTilePalette.DimRim, 2, false);
            Sprite icon = ItemCard.SlotIcon(slot);
            Image mark = UIBuilder.Img("Icon", tile, icon, UITheme.WithAlpha(Rgb(ItemTilePalette.DimRim), 0.62f));
            float s = size * IconFraction * ItemCard.MarkScale(icon);
            UIBuilder.Box(mark.rectTransform, 0f, 0f, s, s, new Vector2(0.5f, 0.5f));
            return tile;
        }

        // ------------------------------------------------------------------ plate

        /// <summary>The rounded plate: soft glow and ring for a top tier, the dark outline, the tier fill, the rim.</summary>
        private static RectTransform Plate(string name, Transform parent, float size, int plate, int rim,
                                           int frame, bool top)
        {
            RectTransform tile = UIBuilder.Node(name, parent);
            UIBuilder.Size(tile, size, size);

            int radius = Mathf.Max(6, Mathf.RoundToInt(size * 0.16f));
            Color rimColor = Rgb(rim);

            if (top)
            {
                Image glow = UIBuilder.Img("Glow", tile, TextureLab.Glow(128, 1.8f), UITheme.WithAlpha(rimColor, 0.30f));
                UIBuilder.FillParent(glow.rectTransform, -size * 0.14f);
                Image ring = UIBuilder.Frame("Ring", tile, UITheme.WithAlpha(rimColor, 0.45f), radius + 3, 2);
                UIBuilder.FillParent(ring.rectTransform, -4f);
            }

            Image outline = tile.gameObject.AddComponent<Image>();
            outline.sprite = TextureLab.RoundedRect(radius);
            outline.type = Image.Type.Sliced;
            outline.color = Rgb(ItemTilePalette.Outline);

            Image fill = UIBuilder.Img("Plate", tile, TextureLab.RoundedRect(radius), Rgb(plate));
            fill.type = Image.Type.Sliced;
            UIBuilder.FillParent(fill.rectTransform, 2f);

            Image edge = UIBuilder.Frame("Rim", tile, rimColor, radius, frame);
            UIBuilder.FillParent(edge.rectTransform, 2f);

            if (top) Gem(tile, size, rimColor);
            return tile;
        }

        /// <summary>The corner gem of a top-tier plate: a dark diamond with a lighter stone, on the bottom-right
        /// corner of the frame. It is part of the plate, so it is not counted as a mark.</summary>
        private static void Gem(RectTransform tile, float size, Color rim)
        {
            // Centred on the corner so it sits on the frame and clears the last pip.
            float g = size * CornerGemFraction;
            float tuck = g * 0.32f;
            Image back = UIBuilder.Img("GemBack", tile, TextureLab.Diamond(32), Rgb(ItemTilePalette.Outline));
            UIBuilder.Box(back.rectTransform, tuck, -tuck, g, g, new Vector2(1f, 0f));
            Image stone = UIBuilder.Img("Gem", tile, TextureLab.Diamond(32), Color.Lerp(rim, Color.white, 0.4f));
            UIBuilder.Box(stone.rectTransform, tuck - g * 0.15f, -tuck + g * 0.15f, g * 0.7f, g * 0.7f, new Vector2(1f, 0f));
        }

        // ------------------------------------------------------------------ marks

        private static void Silhouette(RectTransform tile, float size, Sprite icon, float fraction, float scale,
                                       bool tint, bool hasPips)
        {
            if (icon == null) return;
            Image mark = UIBuilder.Img("Icon", tile, icon, tint ? Rgb(ItemTilePalette.Silhouette) : Color.white);
            float s = size * fraction * scale;
            UIBuilder.Box(mark.rectTransform, 0f, hasPips ? size * 0.05f : 0f, s, s, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Top left. The aspect's set icon from the kit, or a small diamond in the aspect accent.</summary>
        private static void AspectMark(RectTransform tile, float size, Aspect aspect)
        {
            Color tint = Color.white;
            Sprite fallback = TextureLab.Diamond(32);
            Sprite sprite = UiKit.SetGlyph(aspect, fallback, ref tint);
            if (sprite == fallback) tint = UITheme.AspectAccent(aspect);
            float a = size * AspectFraction;
            Image mark = UIBuilder.Img("Aspect", tile, sprite, tint);
            UIBuilder.Box(mark.rectTransform, size * 0.08f, -size * 0.08f, a, a, new Vector2(0f, 1f));
        }

        /// <summary>Top right. One or two characters, in the tile's own rim colour with a dark edge, so it reads
        /// on every plate.</summary>
        private static void Number(RectTransform tile, float size, string text, Color rim)
        {
            int font = Mathf.Max(12, Mathf.RoundToInt(size * 0.22f));
            Text label = EmberforgeUI.Label("Number", tile, text, font, Color.Lerp(rim, Color.white, 0.55f),
                                            TextAnchor.UpperRight);
            UIBuilder.Outlined(label, Rgb(ItemTilePalette.Outline), 1.5f);
            UIBuilder.Box(label.rectTransform, -size * 0.09f, -size * 0.06f, size * 0.34f, size * 0.26f,
                          new Vector2(1f, 1f));
        }

        /// <summary>The bottom row: diamonds, lit up to the tier, in the rim colour.</summary>
        private static void Pips(RectTransform tile, float size, Color rim, int lit, int max)
        {
            if (max <= 0) return;
            float pip = size * PipFraction;
            float gap = size * PipGapFraction;
            float total = max * pip + (max - 1) * gap;
            float left = -total * 0.5f + pip * 0.5f;

            RectTransform row = UIBuilder.Node("Pips", tile);
            UIBuilder.Box(row, 0f, size * 0.07f, total, pip, new Vector2(0.5f, 0f));
            for (int i = 0; i < max; i++)
            {
                Image dot = UIBuilder.Img("Pip" + i, row, TextureLab.Diamond(32),
                                          i < lit ? rim : new Color(0f, 0f, 0f, 0.45f));
                UIBuilder.Box(dot.rectTransform, left + i * (pip + gap), 0f, pip, pip, new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>Bottom left, the one fixed spot for "worn".</summary>
        private static void Equipped(RectTransform tile, float size)
        {
            // Centred on the bottom-left corner, mirroring the gem, so it never sits on a pip.
            float c = size * 0.24f;
            float lift = c * 0.3f;
            Image disc = UIBuilder.Img("EquippedBack", tile, TextureLab.Circle(48), UITheme.WithAlpha(Rgb(ItemTilePalette.Outline), 0.92f));
            UIBuilder.Box(disc.rectTransform, -lift, -lift, c, c, new Vector2(0f, 0f));
            Image check = UiKit.CheckImage("Equipped", tile, 32, Emberforge.TextGold);
            UIBuilder.Box(check.rectTransform, -lift + c * 0.12f, -lift + c * 0.12f, c * 0.76f, c * 0.76f, new Vector2(0f, 0f));
        }

        // ------------------------------------------------------------------ colour

        public static Color Rgb(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        }
    }
}
