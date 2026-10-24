using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI
{
    /// <summary>
    /// The one way an item is drawn, anywhere, by name. Since R47 the drawing itself lives in ItemTile (a rounded
    /// plate coloured by tier, fixed corners, at most four marks); this class keeps the callers' familiar entry
    /// points and the slot glyph helpers (SlotIcon, MarkScale, RailGlyph) and the segmented StatBar.
    ///
    /// TWO LADDERS, NEVER MIXED. Relics use RelicGrade (Legendary, Mythic) and armor uses ArmorTemper (Rough,
    /// Honed, Gilded, Radiant). Each ladder has its own table in ItemTilePalette, and a relic's painted icon
    /// is never a slot silhouette, so a card does not say "this armour is Mythic" or "this relic is Gilded".
    /// </summary>
    public static class ItemCard
    {
        // ------------------------------------------------------------------ domain wrappers
        // R47: every card is an ItemTile. The wrappers stay so callers keep one familiar name, and so the
        // tile can change in one file without any screen noticing.

        /// <summary>An armour piece: plate, rim and pips from its Temper, the set icon from its Aspect.</summary>
        public static RectTransform ForArmor(string name, Transform parent, float size, ArmorPiece piece)
        {
            return ItemTile.ForArmor(name, parent, size, piece, false);
        }

        /// <summary>The same tile with the worn mark, for a slot the hero is wearing.</summary>
        public static RectTransform ForArmor(string name, Transform parent, float size, ArmorPiece piece, bool equipped)
        {
            return ItemTile.ForArmor(name, parent, size, piece, equipped);
        }

        /// <summary>A piece that is not owned (a store offer, a reward preview): slot, temper and aspect only.</summary>
        public static RectTransform ForSlot(string name, Transform parent, float size, ArmorSlot slot,
                                            ArmorTemper temper, Aspect aspect)
        {
            return ItemTile.ForSlot(name, parent, size, slot, temper, aspect, false);
        }

        /// <summary>A relic. Two grades, so two plates; no aspect mark, because a relic has no aspect.</summary>
        public static RectTransform ForRelic(string name, Transform parent, float size,
                                             RelicDef def, int tier, int maxTier)
        {
            return ItemTile.ForRelic(name, parent, size, def, tier, maxTier, false);
        }

        /// <summary>A slot with nothing in it: the same footprint, the plate dimmed.</summary>
        public static RectTransform Vacant(string name, Transform parent, float size, ArmorSlot slot)
        {
            return ItemTile.Vacant(name, parent, size, slot);
        }

        // ------------------------------------------------------------------ glyphs

        /// <summary>The kit's name for a slot's mark (UiKitMap.Icons). Both accessories share one.</summary>
        private static string KitName(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Helm: return "item_helm";
                case ArmorSlot.Plate: return "item_plate";
                case ArmorSlot.Boots: return "item_boots";
                case ArmorSlot.AccessoryA:
                case ArmorSlot.AccessoryB: return "item_accessory";
            }
            return "item_weapon";
        }

        /// <summary>
        /// R45: the kit's sword is a thin blade in a tall frame, so at the same box size it reads far smaller than
        /// the helm, plate and boots. Its mark is drawn larger (the blade still sits inside the card, clear of the
        /// pips); every other mark, and the flat fallback sword, is drawn at 1.
        /// </summary>
        private const float WeaponMarkScale = 1.28f;

        public static float MarkScale(Sprite icon)
        {
            if (icon == null || !UiKit.On) return 1f;
            return icon == UiKit.Icon(KitName(ArmorSlot.Weapon)) ? WeaponMarkScale : 1f;
        }

        /// <summary>The tint for a slot mark. The kit's marks are painted grey and gold, so they are drawn
        /// white (the old tint's alpha kept); the flat fallback shapes keep the tint they always had.</summary>
        public static Color IconTint(Sprite icon, Color old)
        {
            return IsKitMark(icon) ? new Color(1f, 1f, 1f, old.a) : old;
        }

        private static bool IsKitMark(Sprite icon)
        {
            if (icon == null || !UiKit.On) return false;
            ArmorSlot[] slots = ArmorInfo.AllSlots;
            for (int i = 0; i < slots.Length; i++)
                if (icon == UiKit.Icon(KitName(slots[i]))) return true;
            return false;
        }

        /// <summary>A filter rail glyph's colour for selected or not: gold or quiet for the flat shapes, white
        /// (dimmed when not selected) for a kit mark.</summary>
        public static void RailGlyph(Image glyph, bool active)
        {
            if (IsKitMark(glyph.sprite)) glyph.color = new Color(1f, 1f, 1f, active ? 1f : 0.6f);
            else glyph.color = active ? Emberforge.TextGold : Emberforge.TextQuiet;
        }

        /// <summary>The mark for a slot. Both accessories share the ring, differing only by stone.</summary>
        public static Sprite SlotIcon(ArmorSlot slot)
        {
            Sprite kit = UiKit.Icon(KitName(slot));
            if (kit != null) return kit;
            switch (slot)
            {
                case ArmorSlot.Weapon: return TextureLab.Sword(128);
                case ArmorSlot.Helm: return TextureLab.Helm(128);
                case ArmorSlot.Plate: return TextureLab.Plate(128);
                case ArmorSlot.Boots: return TextureLab.Boot(128);
                case ArmorSlot.AccessoryA: return TextureLab.Ring(128, true);
                case ArmorSlot.AccessoryB: return TextureLab.Ring(128, false);
            }
            return TextureLab.Sword(128);
        }

        /// <summary>
        /// The slot name at rail width. Shared so the Inventory and the Forge cannot disagree -
        /// the Forge was truncating SlotLabel to five characters, which rendered BOTH accessories
        /// as "ACCES".
        /// </summary>
        public static string ShortSlotName(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Weapon: return "WEAP";
                case ArmorSlot.Helm: return "HELM";
                case ArmorSlot.Plate: return "PLATE";
                case ArmorSlot.Boots: return "BOOT";
                case ArmorSlot.AccessoryA: return "ACC I";
                case ArmorSlot.AccessoryB: return "ACC II";
            }
            return "?";
        }

        public static string SlotName(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.AccessoryA: return "ACCESSORY I";
                case ArmorSlot.AccessoryB: return "ACCESSORY II";
            }
            return slot.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Placeholder emblem for a relic, until relics have art.
        ///
        /// The same curated ten the Relic Shop uses and for the same reasons - sigil 10 is a golem's
        /// filled square and 2, 7 and 11 read as patterns rather than marks. Hashed here rather than
        /// assigned by position, because unlike the shop's five-across shelf these appear one at a
        /// time in an inventory row, where a stable mark per relic matters more than guaranteeing
        /// two neighbours differ.
        /// </summary>
        public static int RelicSigil(string relicId)
        {
            int[] curated = { 0, 1, 3, 4, 5, 6, 8, 9, 12, 13 };
            int hash = 0;
            for (int i = 0; i < relicId.Length; i++) hash = hash * 31 + relicId[i];
            return curated[Mathf.Abs(hash) % curated.Length];
        }

        // ------------------------------------------------------------------ segmented bars

        /// <summary>
        /// A stat as a row of discrete segments, the way all three mockups draw one.
        ///
        /// Segments rather than a continuous fill because these bars carry values with no natural
        /// maximum - an Attack of 812 is not 81% of anything - so a smooth bar would be inventing a
        /// denominator and stating it with false precision. A count of lit blocks out of a fixed
        /// track is honest about being a rough magnitude, and is directly comparable between two
        /// items sitting side by side, which is the whole job on the Forge's before/after panel.
        ///
        /// Returns the row so a caller can hang a delta marker off it.
        /// </summary>
        public static RectTransform StatBar(string name, Transform parent, Sprite icon, Color tint,
                                            float ratio, int segments, float height)
        {
            RectTransform row = UIBuilder.Node(name, parent);
            UIBuilder.Sized(row, 0f, height);

            Image glyph = UIBuilder.Img("Icon", row, icon, tint);
            UIBuilder.Box(glyph.rectTransform, 0f, 0f, height * 0.82f, height * 0.82f, new Vector2(0f, 0.5f));

            RectTransform track = UIBuilder.Node("Track", row);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(height * 0.82f + UITheme.Space12, -height * 0.30f);
            track.offsetMax = new Vector2(0f, height * 0.30f);

            int lit = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ratio) * segments), 0, segments);
            float step = 1f / segments;
            for (int i = 0; i < segments; i++)
            {
                Image seg = UIBuilder.Img("Seg" + i, track,
                                          EmberforgeSkin.Get(i < lit ? EmberforgeArt.Slot.BarFill : EmberforgeArt.Slot.BarTrough),
                                          i < lit ? tint : Color.white);
                seg.type = Image.Type.Sliced;
                RectTransform rt = seg.rectTransform;
                rt.anchorMin = new Vector2(i * step, 0f);
                rt.anchorMax = new Vector2((i + 1) * step, 1f);
                rt.offsetMin = new Vector2(1.5f, 0f);
                rt.offsetMax = new Vector2(-1.5f, 0f);
            }
            return row;
        }
    }
}
