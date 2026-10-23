using System.Collections.Generic;
using Emberfall.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI.Battle
{
    /// <summary>
    /// ONE UNIT'S PLATE, stood on the unit's head in the formation (or hung under its feet when
    /// FormationLayout.Enabled is false; the screen places the root, the widget is the same): from
    /// the bottom up a thin energy bar, the HP bar with the ward drawn over it and the ghost trailing
    /// behind, and the name on one line; a turn pip to the left of the HP bar, and a row of status
    /// icons to the right of the bars that is empty at rest.
    ///
    /// The widget draws what it is given and decides nothing. Which statuses a unit carries, how
    /// deep, when its turn starts and what its HP is are all read off the event record by
    /// BattleScreen, which hands the answers in here. Sizes are BattleHudMetrics', colours and
    /// sprites BattleHudStyle's.
    ///
    /// The root is the plate's bottom centre with zero height, so every child is placed as a
    /// distance above it, which is how BattleHudLayout.PlateBoxAboveHead describes the plate the
    /// harness proves never overlaps another.
    /// </summary>
    public sealed class UnitPlate
    {
        public RectTransform Root;
        public CanvasGroup Group;
        public Text Name;
        public Image HpFill;
        public Image HpGhost;
        public Image WardFill;
        public Image EnergyFill;
        public Image TurnPip;

        private RectTransform _icons;
        private readonly List<IconView> _iconViews = new List<IconView>();
        private Text _overflow;

        private sealed class IconView
        {
            public RectTransform Root;
            public Image Glyph;
            public Text Depth;
        }

        public static UnitPlate Create(Transform parent, string unitName, bool isPlayer)
        {
            UnitPlate plate = new UnitPlate();
            plate.Root = UIBuilder.Node("Plate_" + unitName, parent);
            plate.Root.anchorMin = Vector2.zero;
            plate.Root.anchorMax = Vector2.zero;
            plate.Root.pivot = new Vector2(0.5f, 0f);
            UIBuilder.Size(plate.Root, BattleHudMetrics.PlateNameWidth, 0f);
            plate.Group = plate.Root.gameObject.AddComponent<CanvasGroup>();
            plate.Group.blocksRaycasts = false;

            // Everything is a distance UP from the root, which is the plate's bottom centre: the energy
            // bar first, the HP bar over it, the name over that.
            Vector2 bottom = new Vector2(0.5f, 0f);
            float energyY = 0f;
            float hpY = energyY + BattleHudMetrics.PlateEnergyHeight + BattleHudMetrics.PlateHpToEnergy;
            float nameY = hpY + BattleHudMetrics.PlateHpHeight + BattleHudMetrics.PlateNameToHp;

            // Name, one line, best-fit down to PlateNameFontMin inside the plate's full width
            // rather than overflowing it: the width is part of the box the harness checks.
            plate.Name = EmberforgeUI.Label("Name", plate.Root, unitName, BattleHudMetrics.PlateNameFont,
                                            BattleHudStyle.PlateName, TextAnchor.MiddleCenter);
            UIBuilder.Box(plate.Name.rectTransform, 0f, nameY, BattleHudMetrics.PlateNameWidth,
                          BattleHudMetrics.PlateNameHeight, bottom);
            UIBuilder.BestFit(plate.Name, BattleHudMetrics.PlateNameFontMin, BattleHudMetrics.PlateNameFont);
            UIBuilder.Outlined(plate.Name, BattleHudStyle.TextOutline, BattleHudStyle.PlateNameStroke);
            // Over a head the name is left off (BattleHudMetrics.PlateNameOverHead); under the feet it stays.
            if (FormationLayout.Enabled && !BattleHudMetrics.PlateNameOverHead) plate.Name.gameObject.SetActive(false);

            // HP, with the ghost under it and the ward drawn over the same rectangle, so a warded
            // unit is still one bar. Plain rectangles: at 10 tall there is no middle left for a
            // nine-sliced trough to stretch.
            plate.HpGhost = Bar("HpGhost", plate.Root, BattleHudStyle.HpTrack, BattleHudStyle.HpGhost, hpY,
                                BattleHudMetrics.PlateHpHeight);
            plate.HpFill = Bar("Hp", plate.Root, UITheme.BarTrackNone,
                               isPlayer ? BattleHudStyle.HpAlly : BattleHudStyle.HpEnemy, hpY,
                               BattleHudMetrics.PlateHpHeight);
            plate.WardFill = Bar("Ward", plate.Root, UITheme.BarTrackNone, BattleHudStyle.Ward, hpY,
                                 BattleHudMetrics.PlateHpHeight);
            UIBuilder.SetBar(plate.WardFill, 0f);

            plate.EnergyFill = Bar("Energy", plate.Root, BattleHudStyle.EnergyTrack, BattleHudStyle.Energy, energyY,
                                   BattleHudMetrics.PlateEnergyHeight);

            // The side facing the centre is the icon row's, the other side is the pip's: allies stand on
            // the left, so their pip is left of the bars and their icons right of them, and the enemies are
            // the mirror image. That keeps the outer plates (the back row, 0.09 from the edge) on screen.
            float inward = isPlayer ? 1f : -1f;

            // The turn pip, a dot beside the HP bar on the outer side, centred on it. Starts transparent;
            // the turn cue lights it.
            plate.TurnPip = UIBuilder.Panel("TurnPip", plate.Root, Color.clear, 5);
            float pipSize = BattleHudMetrics.PlateTurnPipSize;
            UIBuilder.Box(plate.TurnPip.rectTransform,
                          -inward * (BattleHudMetrics.PlateBarWidth * 0.5f + BattleHudMetrics.PlateTurnPipGap + pipSize * 0.5f),
                          hpY + (BattleHudMetrics.PlateHpHeight - pipSize) * 0.5f, pipSize, pipSize, bottom);

            // The icon row, right of the bars and level with them: up to PlateMaxIcons icons and a "+N".
            // Over the head there is no room above the name (the formation keeps that strip clear), and
            // beside the bars it is empty at rest.
            plate._icons = UIBuilder.Node("Statuses", plate.Root);
            UIBuilder.Box(plate._icons,
                          inward * (BattleHudMetrics.PlateBarWidth * 0.5f + BattleHudMetrics.PlateIconsInset
                                    + BattleHudMetrics.PlateIconsWidth * 0.5f),
                          (BattleHudMetrics.PlateBarsHeight - BattleHudMetrics.PlateIconSize) * 0.5f,
                          BattleHudMetrics.PlateIconsWidth, BattleHudMetrics.PlateIconSize, bottom);
            UIBuilder.HStack(plate._icons, BattleHudMetrics.PlateIconSpacing, new RectOffset(0, 0, 0, 0),
                             isPlayer ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            for (int i = 0; i < BattleHudMetrics.PlateMaxIcons; i++) plate._iconViews.Add(plate.NewIcon(i));
            plate._overflow = EmberforgeUI.Label("More", plate._icons, "", BattleHudMetrics.PlateIconOverflowFont,
                                                 BattleHudStyle.StatusOverflow, TextAnchor.MiddleLeft);
            UIBuilder.Sized(plate._overflow.rectTransform, BattleHudMetrics.PlateIconOverflowWidth,
                            BattleHudMetrics.PlateIconSize);
            UIBuilder.Outlined(plate._overflow, BattleHudStyle.TextOutline, Emberforge.StrokeThin);
            plate.ShowStatuses(null, 0);
            return plate;
        }

        private static Image Bar(string name, RectTransform parent, Color track, Color fill, float bottomY, float height)
        {
            Image image = UIBuilder.Bar(name, parent, track, fill);
            RectTransform host = image.rectTransform.parent as RectTransform;
            UIBuilder.Box(host, 0f, bottomY, BattleHudMetrics.PlateBarWidth, height, new Vector2(0.5f, 0f));
            float inset = BattleHudMetrics.PlateBarFillInset;
            image.rectTransform.offsetMin = new Vector2(inset, inset);
            image.rectTransform.offsetMax = new Vector2(-inset, -inset);
            return image;
        }

        private IconView NewIcon(int index)
        {
            IconView view = new IconView();
            view.Root = UIBuilder.Node("Icon" + index, _icons);
            UIBuilder.Sized(view.Root, BattleHudMetrics.PlateIconSize, BattleHudMetrics.PlateIconSize);
            Image backing = UIBuilder.Img("Backing", view.Root, BattleHudStyle.StatusBackingSprite,
                                          BattleHudStyle.StatusBacking);
            UIBuilder.FillParent(backing.rectTransform, -1f);
            view.Glyph = UIBuilder.Img("Glyph", view.Root, null, Color.white);
            UIBuilder.FillParent(view.Glyph.rectTransform, 1f);
            view.Glyph.preserveAspect = true;
            view.Depth = EmberforgeUI.Label("Depth", view.Root, "", BattleHudMetrics.PlateIconDepthFont,
                                            BattleHudStyle.StatusDepth, TextAnchor.LowerRight);
            UIBuilder.FillParent(view.Depth.rectTransform, -4f);
            UIBuilder.Outlined(view.Depth, BattleHudStyle.TextOutline, Emberforge.StrokeThin);
            return view;
        }

        /// <summary>Draws these icons and a "+N" for the kinds left off. Null clears the row.</summary>
        public void ShowStatuses(List<StatusIcon> icons, int overflow)
        {
            for (int i = 0; i < _iconViews.Count; i++)
            {
                IconView view = _iconViews[i];
                bool on = icons != null && i < icons.Count;
                view.Root.gameObject.SetActive(on);
                if (!on) continue;
                Color tint;
                view.Glyph.sprite = BattleHudStyle.StatusSprite(icons[i].Kind, icons[i].Role, out tint);
                view.Glyph.color = tint;
                view.Depth.text = icons[i].Depth > 1 ? icons[i].Depth.ToString() : "";
            }
            _overflow.text = overflow > 0 ? "+" + overflow : "";
            _overflow.gameObject.SetActive(overflow > 0);
        }

        /// <summary>The energy fill, gold at full.</summary>
        public void SetEnergy(float fraction)
        {
            if (EnergyFill == null) return;
            float k = Mathf.Clamp01(fraction);
            UIBuilder.SetBar(EnergyFill, k);
            EnergyFill.color = k >= 0.999f ? BattleHudStyle.EnergyFull : BattleHudStyle.Energy;
        }

        /// <summary>The HP and ward fills. The ghost is the caller's to drain.</summary>
        public void SetHp(float hpFraction, float wardFraction)
        {
            UIBuilder.SetBar(HpFill, hpFraction);
            UIBuilder.SetBar(WardFill, wardFraction);
        }

        /// <summary>The turn pip at this alpha: 0 at rest, 1 while the unit acts.</summary>
        public void SetTurnPip(float alpha)
        {
            if (TurnPip != null) TurnPip.color = UITheme.WithAlpha(BattleHudStyle.TurnPip, alpha);
        }
    }
}
