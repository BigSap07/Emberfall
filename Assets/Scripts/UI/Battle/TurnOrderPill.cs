using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Battle;
using Emberfall.Core.Combat;

namespace Emberfall.UI.Battle
{
    /// <summary>
    /// THE TURN ORDER, as a pill at the bottom centre of the screen: the acting unit's portrait
    /// disc, enlarged, then NEXT and the units still to act this round as smaller discs, capped at
    /// BattleHudMetrics.NextMaxUpcoming.
    ///
    /// It draws the units it is handed and nothing else. The order is the round's TurnStart events,
    /// read by BattleScreen off the record and walked with its cursor; this widget never looks at
    /// an event. Each disc wears the unit's profile badge where one is declared, read off the
    /// snapshot's visual rather than looked up in content, and otherwise the unit's initial, over
    /// a disc tinted by side so a badged ally and a badged enemy still read apart at a glance.
    /// </summary>
    public sealed class TurnOrderPill
    {
        public RectTransform Root;

        private Text _label;
        private Disc _acting;
        private readonly List<Disc> _upcoming = new List<Disc>();

        private sealed class Disc
        {
            public RectTransform Root;
            public Image Ground;
            public Image Face;
            public Text Initial;
        }

        public static TurnOrderPill Create(Transform parent)
        {
            TurnOrderPill pill = new TurnOrderPill();
            pill.Root = UIBuilder.Node("TurnOrder", parent);
            pill.Root.anchorMin = Vector2.zero;
            pill.Root.anchorMax = Vector2.zero;
            pill.Root.pivot = Vector2.zero;

            Image plate = pill.Root.gameObject.AddComponent<Image>();
            plate.sprite = BattleHudStyle.PillPlate;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            plate.raycastTarget = false;

            pill._acting = NewDisc("Acting", pill.Root, BattleHudMetrics.NextActingDisc);
            pill._label = EmberforgeUI.Label("Next", pill.Root, "NEXT", BattleHudMetrics.NextLabelFont,
                                             BattleHudStyle.NextLabel, TextAnchor.MiddleCenter);
            UIBuilder.Outlined(pill._label, BattleHudStyle.TextOutline, Emberforge.StrokeThin);
            for (int i = 0; i < BattleHudMetrics.NextMaxUpcoming; i++)
                pill._upcoming.Add(NewDisc("Upcoming" + i, pill.Root, BattleHudMetrics.NextUpcomingDisc));

            pill.Root.gameObject.SetActive(false);
            return pill;
        }

        private static Disc NewDisc(string name, RectTransform parent, float size)
        {
            Disc disc = new Disc();
            disc.Root = UIBuilder.Node(name, parent);
            disc.Root.anchorMin = new Vector2(0f, 0.5f);
            disc.Root.anchorMax = new Vector2(0f, 0.5f);
            disc.Root.pivot = new Vector2(0f, 0.5f);
            UIBuilder.Size(disc.Root, size, size);

            disc.Ground = UIBuilder.Img("Ground", disc.Root, BattleHudStyle.Disc, Color.white);
            UIBuilder.FillParent(disc.Ground.rectTransform, 0f);
            disc.Face = UIBuilder.Img("Face", disc.Root, null, Color.white);
            UIBuilder.FillParent(disc.Face.rectTransform, BattleHudMetrics.NextDiscRim);
            disc.Face.preserveAspect = true;
            disc.Initial = EmberforgeUI.Label("Initial", disc.Root, "", Mathf.RoundToInt(size * 0.5f),
                                              BattleHudStyle.DiscInitial, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(disc.Initial.rectTransform, 0f);
            UIBuilder.Outlined(disc.Initial, BattleHudStyle.TextOutline, Emberforge.StrokeThin);
            return disc;
        }

        /// <summary>
        /// Draws the acting unit (or none, before the round's first turn) and the units still to
        /// come, at most NextMaxUpcoming of them. Hidden when there is no order to show. Call Place
        /// afterwards, and whenever the screen changes shape, to put the pill where it belongs.
        /// </summary>
        public void Show(UnitSnapshot acting, IList<UnitSnapshot> upcoming)
        {
            int count = upcoming != null ? Mathf.Min(upcoming.Count, BattleHudMetrics.NextMaxUpcoming) : 0;
            _hasActing = acting != null;
            _count = count;
            bool any = _hasActing || count > 0;
            Root.gameObject.SetActive(any);
            if (!any) return;

            float x = BattleHudMetrics.NextPadding;
            _acting.Root.gameObject.SetActive(_hasActing);
            if (_hasActing)
            {
                Paint(_acting, acting);
                _acting.Root.anchoredPosition = new Vector2(x, 0f);
                x += BattleHudMetrics.NextActingDisc + BattleHudMetrics.NextDiscGap;
            }

            UIBuilder.Box(_label.rectTransform, x, 0f, BattleHudMetrics.NextLabelWidth, BattleHudMetrics.NextHeight,
                          new Vector2(0f, 0.5f));
            x += BattleHudMetrics.NextLabelWidth + BattleHudMetrics.NextDiscGap;

            for (int i = 0; i < _upcoming.Count; i++)
            {
                bool on = i < count;
                _upcoming[i].Root.gameObject.SetActive(on);
                if (!on) continue;
                Paint(_upcoming[i], upcoming[i]);
                _upcoming[i].Root.anchoredPosition = new Vector2(x, 0f);
                x += BattleHudMetrics.NextUpcomingDisc + BattleHudMetrics.NextDiscGap;
            }
        }

        /// <summary>Puts the pill at the bottom centre of a canvas this wide, NextBottom above the
        /// safe area's bottom edge, at the width its discs need (BattleHudLayout.Next).</summary>
        public void Place(float canvasWidth, float safeBottomCanvas)
        {
            BattleFraming.CanvasRect rect = BattleHudLayout.Next(canvasWidth, safeBottomCanvas, _hasActing, _count);
            Root.anchoredPosition = new Vector2(rect.X, rect.Y);
            Root.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        private bool _hasActing;
        private int _count;

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private static void Paint(Disc disc, UnitSnapshot unit)
        {
            disc.Ground.color = unit.IsPlayer ? BattleHudStyle.DiscAlly : BattleHudStyle.DiscEnemy;
            Sprite badge = Widgets.DeclaredBadge(unit.DefId, unit.Name, unit.Visual);
            disc.Face.sprite = badge;
            disc.Face.enabled = badge != null;
            disc.Initial.text = badge == null && !string.IsNullOrEmpty(unit.Name)
                ? unit.Name.Substring(0, 1).ToUpperInvariant() : "";
        }
    }
}
