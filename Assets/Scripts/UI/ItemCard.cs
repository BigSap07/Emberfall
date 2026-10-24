using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI
{
    /// <summary>
    /// The one way an item is drawn, anywhere. The Inventory grid, the Hero Detail equip slots and
    /// the Forge's anvil and comparison table all call this, so a Radiant helm looks the same in
    /// all three places by construction rather than by three screens agreeing to keep matching.
    ///
    /// ANATOMY, from the mockups: a chamfered square on a sunken fill, a THIN CRISP border in the
    /// item's rarity colour, the item's glyph centred, and a row of small diamond pips along the
    /// bottom counting its tier. Nothing else. The border is the rarity read and the pips are the
    /// tier read, and they are deliberately different channels - colour for which ladder rung,
    /// count for how far along - so neither has to be inferred from the other.
    ///
    /// TWO LADDERS, NEVER MIXED. Relics use RelicGrade, which has exactly two rungs, Legendary and
    /// Mythic, and takes its colour from UITheme.RelicGradeColor. Armor uses ArmorTemper, which has
    /// exactly four, Rough through Radiant, and takes its colour from UITheme.ArmorTemperColor.
    /// Those two functions already share no colour with each other, on purpose - see their notes -
    /// so a card can never say "this armour is Mythic" or "this relic is Gilded".
    ///
    /// ASPECT ACCENTS ARE A GLOW, NEVER THE BORDER. An armour piece carries its own rolled Aspect
    /// (ArmorPiece.Aspect), which is a different fact from its temper. Painting it on the border
    /// would overwrite the rarity read with something unrelated, so it is a soft glow behind the
    /// card instead: present when the piece has an aspect worth showing, invisible when it does
    /// not, and never competing with the frame for the same meaning.
    /// </summary>
    public static class ItemCard
    {
        /// <summary>Pip diameter, and the gap between pips, as fractions of the card's own size.</summary>
        private const float PipFraction = 0.115f;
        private const float PipGapFraction = 0.045f;
        private const float IconFraction = 0.60f;

        /// <summary>
        /// The card, at its most general. Everything else here funnels into this so there is one
        /// place that decides what an item card IS.
        /// </summary>
        /// <param name="rarity">Border, pip and glow tint. The rarity ladder's colour, never an aspect.</param>
        /// <param name="pips">Filled pips. 0 draws the row dim, which is what an empty slot wants.</param>
        /// <param name="maxPips">Total pips. 0 omits the row entirely.</param>
        /// <param name="aspectGlow">Soft backing glow, or Color.clear for none.</param>
        public static RectTransform Build(string name, Transform parent, float size, Sprite icon,
                                          Color rarity, int pips, int maxPips, Color aspectGlow)
        {
            return Build(name, parent, size, icon, rarity, pips, maxPips, aspectGlow,
                         IconTint(icon, Emberforge.TextBody), IconFraction);
        }

        /// <summary>The card with the icon's tint and size stated: a UI kit icon is painted in colour, so
        /// it is drawn white (the flat glyphs are tinted) and a little larger.</summary>
        private static RectTransform Build(string name, Transform parent, float size, Sprite icon,
                                           Color rarity, int pips, int maxPips, Color aspectGlow,
                                           Color iconTint, float iconFraction)
        {
            RectTransform card = UIBuilder.Node(name, parent);
            UIBuilder.Size(card, size, size);

            int cut = Mathf.Max(6, Mathf.RoundToInt(size * 0.14f));

            // The aspect glow sits OUTSIDE the card's own bounds and behind everything, so the card
            // reads as lit rather than as tinted - the same treatment a mythic relic gets in the
            // shop, for the same reason.
            if (aspectGlow.a > 0.001f)
            {
                Image glow = UIBuilder.Img("AspectGlow", card, TextureLab.Glow(128, 1.8f), aspectGlow);
                UIBuilder.FillParent(glow.rectTransform, -size * 0.16f);
            }

            // THE CHAMFERED SILHOUETTE STAYS, and this is the one place in the game where it
            // does. Emberforge rounds every corner it draws, which is right for chrome - a panel,
            // a button, a pill - and wrong for the thing chrome CONTAINS: a card cut square is
            // how this game has drawn an object since the mockups, and rounding it would have
            // made a helm and the panel behind it the same shape.
            //
            // What the kit does bring is the outline. It is the one property every element in
            // Emberforge shares without exception, and a rarity border with no dark line outside
            // it was the one silhouette in the game floating free of the rest - see the outline
            // note in Emberforge on why that single shared property is what makes a kit read as
            // one kit.
            Image fill = card.gameObject.AddComponent<Image>();
            fill.sprite = TextureLab.NotchedRect(cut);
            fill.type = Image.Type.Sliced;
            fill.color = Emberforge.SunkenMid;

            UIBuilder.NotchedFrame("Outline", card, Emberforge.Outline, cut, Emberforge.OutlineWeight);

            // THIN and CRISP, per the mockups: a 3px edge at full strength, not a wide soft one.
            // Thickness does not scale with the card - a 96px slot and a 180px forge focus want the
            // same weight of line, or the big one reads as a heavier grade than the small one.
            //
            // Inset by the outline's own weight so the two lines sit side by side rather than on
            // top of each other: the rarity read is the INNER line and the dark one wraps it.
            Image edge = UIBuilder.NotchedFrame("Edge", card, rarity, cut, 3);
            UIBuilder.FillParent(edge.rectTransform, Emberforge.OutlineWeight);

            if (icon != null)
            {
                Image glyph = UIBuilder.Img("Icon", card, icon, iconTint);
                float iconSize = size * iconFraction * MarkScale(icon);
                float lift = maxPips > 0 ? size * 0.06f : 0f;
                UIBuilder.Box(glyph.rectTransform, 0f, lift, iconSize, iconSize, new Vector2(0.5f, 0.5f));
            }

            if (maxPips > 0) BuildPips(card, size, rarity, pips, maxPips);
            return card;
        }

        /// <summary>
        /// The tier row. Diamonds, because the mockups draw diamonds and because a diamond reads as
        /// a discrete step at 11px where a circle reads as a bullet and a bar reads as progress.
        ///
        /// Laid out by hand rather than with an HStack: the row must stay centred on the card at any
        /// pip count, and a layout group on a node this small brings a rebuild pass along with it
        /// for six 11px children.
        /// </summary>
        private static void BuildPips(RectTransform card, float size, Color rarity, int pips, int maxPips)
        {
            float pip = size * PipFraction;
            float gap = size * PipGapFraction;
            float total = maxPips * pip + (maxPips - 1) * gap;
            float left = -total * 0.5f + pip * 0.5f;

            RectTransform row = UIBuilder.Node("Pips", card);
            UIBuilder.Box(row, 0f, size * 0.055f, total, pip, new Vector2(0.5f, 0f));

            for (int i = 0; i < maxPips; i++)
            {
                Image dot = UIBuilder.Img("Pip" + i, row, TextureLab.Diamond(32),
                                          i < pips ? rarity : Emberforge.TextQuiet);
                UIBuilder.Box(dot.rectTransform, left + i * (pip + gap), 0f, pip, pip, new Vector2(0.5f, 0.5f));
            }
        }

        // ------------------------------------------------------------------ domain wrappers

        /// <summary>
        /// An armour piece. Border and pips come from its Temper, the glow from its own Aspect.
        ///
        /// Four pips always, filled to the temper's rung, so an empty pip states the rungs this
        /// piece did NOT reach. A Rough piece showing one of four is a different sentence from a
        /// Rough piece showing one of one.
        /// </summary>
        public static RectTransform ForArmor(string name, Transform parent, float size, ArmorPiece piece)
        {
            Color temper = UITheme.ArmorTemperColor(piece.Temper);
            return Build(name, parent, size, SlotIcon(piece.Slot), temper,
                         (int)piece.Temper + 1, 4,
                         UITheme.WithAlpha(UITheme.AspectAccent(piece.Aspect), 0.16f));
        }

        /// <summary>
        /// A relic. Border and pips come from its Grade and Tier.
        ///
        /// NO ASPECT GLOW, and that is a rule rather than an omission: a relic is never flavoured or
        /// paired to an aspect anywhere in this game, so giving one an aspect tint would say the
        /// opposite of what UITheme.RelicGradeColor's own note is careful to protect.
        /// </summary>
        public static RectTransform ForRelic(string name, Transform parent, float size,
                                             RelicDef def, int tier, int maxTier)
        {
            Color grade = UITheme.RelicGradeColor(def.Grade);

            // The UI kit's icon for this relic id, when the kit is on: drawn white and larger. Otherwise
            // the sigil it always had.
            Sprite kit = UiKit.Relic(def.Id);
            if (kit != null)
                return Build(name, parent, size, kit, grade,
                             Mathf.Clamp(tier, 0, Mathf.Max(1, maxTier)), Mathf.Max(1, maxTier), Color.clear,
                             Color.white, 0.72f);
            return Build(name, parent, size, TextureLab.Sigil(RelicSigil(def.Id), 128), grade,
                         Mathf.Clamp(tier, 0, Mathf.Max(1, maxTier)), Mathf.Max(1, maxTier), Color.clear);
        }

        /// <summary>
        /// A slot with nothing in it. Same frame, same footprint, so a half-equipped hero's slot
        /// column stays a column instead of collapsing into gaps.
        /// </summary>
        public static RectTransform Vacant(string name, Transform parent, float size, ArmorSlot slot)
        {
            RectTransform card = Build(name, parent, size, SlotIcon(slot),
                                       Emberforge.TextQuiet, 0, 4, Color.clear);
            Image icon = card.Find("Icon") != null ? card.Find("Icon").GetComponent<Image>() : null;
            if (icon != null) icon.color = IconTint(icon.sprite, UITheme.WithAlpha(Emberforge.TextQuiet, 0.55f));
            return card;
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

        private static float MarkScale(Sprite icon)
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
