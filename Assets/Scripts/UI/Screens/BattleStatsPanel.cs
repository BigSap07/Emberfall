using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Battle stats panel over the wave result screen: one row per hero in the line, with
    /// damage dealt, taken, healed and shielded as a figure over a bar, and kills.
    ///
    /// PRESENTATION ONLY, and a placeholder look: every figure, bar fraction, scope and the MVP
    /// line is decided by BattleStatsView in Core, and this only paints it. The layout constants
    /// are kept apart from the drawing so final art can restyle it without touching the logic.
    /// </summary>
    public sealed class BattleStatsPanel
    {
        private const float PanelWidth = 1680f;
        private const float PanelHeight = 880f;
        private const float Pad = 40f;

        private const float NameX = 40f;
        private const float NameWidth = 370f;
        private const float BarX0 = 430f;
        private const float BarStep = 270f;
        private const float BarWidth = 240f;
        private const float KillsX = 1530f;
        private const float KillsWidth = 110f;

        private const float HeaderY = -150f;
        private const float RowsY = -196f;
        private const float RowHeight = 106f;
        private const float BarHeight = 16f;
        private const float MvpY = -760f;

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        private readonly Color[] _barColors =
        {
            UITheme.Gold, UITheme.HealthEnemy, UITheme.HealthAlly, UITheme.Shield
        };

        private RectTransform _root;
        private RectTransform _panel;
        private RectTransform _rows;
        private RectTransform _mvp;
        private Text _heading;
        private ForgeTab _wholeTab;
        private ForgeTab _battleTab;
        private BattleStatsView _view;

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        /// <summary>Builds the panel hidden, as the last child of parent so it draws over
        /// everything the screen already holds. onClose is told after the panel has hidden.</summary>
        public void Build(RectTransform parent, Action onClose)
        {
            // The scrim is also the hit catcher: nothing under the panel can be tapped while it is open.
            _root = UIBuilder.Node("StatsLayer", parent);
            UIBuilder.FillParent(_root, 0f);
            Image scrim = _root.gameObject.AddComponent<Image>();
            scrim.sprite = TextureLab.White;
            scrim.color = UITheme.ScrimResult;

            _panel = UIBuilder.Node("StatsPanel", _root);
            UIBuilder.Box(_panel, 0f, 0f, PanelWidth, PanelHeight, Middle);
            EmberforgeUI.PanelOn(_panel, true);

            Text title = EmberforgeUI.Title("Title", _panel, "Battle stats", UITheme.FontTitle,
                                            Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(title.rectTransform, Pad, -28f, 640f, 56f, TopLeft);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThick);

            _heading = EmberforgeUI.Label("Heading", _panel, "", UITheme.FontBody, Emberforge.TextBody,
                                          TextAnchor.MiddleLeft);
            UIBuilder.Box(_heading.rectTransform, Pad, -90f, 760f, 36f, TopLeft);
            UIBuilder.Outlined(_heading, Emberforge.Outline, Emberforge.StrokeThin);

            _wholeTab = EmberforgeUI.Tab("WHOLE WAVE", _panel, 270f, 64f, delegate { Select(StatsScope.Whole); });
            UIBuilder.Box(_wholeTab.Root, 840f, -34f, 270f, 64f, TopLeft);
            _battleTab = EmberforgeUI.Tab("BATTLE", _panel, 270f, 64f, delegate { Select(StatsScope.Battle); });
            UIBuilder.Box(_battleTab.Root, 1126f, -34f, 270f, 64f, TopLeft);

            string[] heads = { "DAMAGE", "TAKEN", "HEALED", "SHIELDED" };
            for (int i = 0; i < heads.Length; i++) Header(heads[i], BarX0 + i * BarStep, BarWidth);
            Header("KILLS", KillsX, KillsWidth);
            Image rule = EmberforgeUI.Rule("Rule", _panel);
            UIBuilder.Box(rule.rectTransform, 0f, HeaderY - 38f, PanelWidth - Pad * 2f, Emberforge.RuleHeight,
                          new Vector2(0.5f, 1f));

            _rows = UIBuilder.Node("Rows", _panel);
            UIBuilder.Box(_rows, 0f, RowsY, PanelWidth, RowHeight * 5f, new Vector2(0.5f, 1f));

            _mvp = UIBuilder.Node("Mvp", _panel);
            UIBuilder.Box(_mvp, 0f, MvpY, PanelWidth - Pad * 2f, 56f, new Vector2(0.5f, 1f));

            // Last, so the close disc draws over the tabs and rows.
            EmberforgeUI.CloseDisc(_panel, delegate { Hide(); if (onClose != null) onClose(); });

            _root.gameObject.SetActive(false);
        }

        public void Show(BattleStatsView view)
        {
            if (view == null) return;
            _view = view;
            _view.Select(_view.DefaultScope);
            _battleTab.Caption.text = "BATTLE " + _view.BattleNumber;
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            Paint();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>Switches scope from outside too, so a capture can photograph either one.</summary>
        public void Select(StatsScope scope)
        {
            if (_view == null) return;
            _view.Select(scope);
            Paint();
        }

        private void Header(string text, float x, float width)
        {
            Text label = EmberforgeUI.Label("Head" + text, _panel, text, UITheme.FontTiny, Emberforge.TextQuiet,
                                            TextAnchor.MiddleLeft);
            UIBuilder.Box(label.rectTransform, x, HeaderY, width, 30f, TopLeft);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void Paint()
        {
            _heading.text = _view.Heading;
            EmberforgeUI.SetTabSelected(_wholeTab, _view.Scope == StatsScope.Whole);
            EmberforgeUI.SetTabSelected(_battleTab, _view.Scope == StatsScope.Battle);

            for (int i = _rows.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_rows.GetChild(i).gameObject);
            for (int i = 0; i < _view.Rows.Count; i++) PaintRow(_view.Rows[i], i);

            for (int i = _mvp.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_mvp.GetChild(i).gameObject);
            if (_view.MvpName.Length > 0) PaintMvp();
        }

        private void PaintRow(StatsRow row, int index)
        {
            float y = -index * RowHeight;
            RectTransform host = UIBuilder.Node("Row" + index, _rows);
            UIBuilder.Box(host, 0f, y, PanelWidth - Pad * 2f, RowHeight - 6f, TopLeft);
            host.anchoredPosition = new Vector2(Pad, y);
            Image stripe = host.gameObject.AddComponent<Image>();
            stripe.sprite = TextureLab.White;
            stripe.color = UITheme.WithAlpha(Emberforge.SunkenTop, index % 2 == 0 ? 0.55f : 0.3f);
            stripe.raycastTarget = false;

            float lx = NameX - Pad + 12f;
            Text name = EmberforgeUI.Title("Name", host, row.Name, UITheme.FontHeading, Emberforge.TextGold,
                                           TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, lx, -8f, NameWidth - 12f, 44f, TopLeft);
            UIBuilder.BestFit(name, 16, UITheme.FontHeading);
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            Text sub = EmberforgeUI.Label("Sub", host, row.Position + "  ·  " + row.Aspect.ToString().ToUpperInvariant(),
                                          UITheme.FontTiny, UITheme.AspectColor(row.Aspect), TextAnchor.MiddleLeft);
            UIBuilder.Box(sub.rectTransform, lx, -56f, NameWidth - 12f, 30f, TopLeft);
            UIBuilder.Outlined(sub, Emberforge.Outline, Emberforge.StrokeThin);

            long[] values = { row.Dealt, row.Taken, row.Healed, row.Shielded };
            float[] bars = { row.DealtBar, row.TakenBar, row.HealedBar, row.ShieldedBar };
            for (int i = 0; i < 4; i++)
            {
                float x = BarX0 + i * BarStep - Pad;
                Text figure = EmberforgeUI.Title("Figure" + i, host, values[i].ToString("N0"), UITheme.FontHeading,
                                                 Emberforge.TextBody, TextAnchor.MiddleLeft);
                UIBuilder.Box(figure.rectTransform, x, -8f, BarWidth, 44f, TopLeft);
                UIBuilder.BestFit(figure, 16, UITheme.FontHeading);
                UIBuilder.Outlined(figure, Emberforge.Outline, Emberforge.StrokeThin);

                Image trough = UIBuilder.Img("Trough" + i, host, TextureLab.White, UITheme.WithAlpha(Color.black, 0.5f));
                UIBuilder.Box(trough.rectTransform, x, -62f, BarWidth, BarHeight, TopLeft);
                trough.raycastTarget = false;
                if (bars[i] > 0f)
                {
                    Image fill = UIBuilder.Img("Fill", trough.rectTransform, TextureLab.White, _barColors[i]);
                    fill.rectTransform.anchorMin = new Vector2(0f, 0f);
                    fill.rectTransform.anchorMax = new Vector2(bars[i], 1f);
                    fill.rectTransform.offsetMin = Vector2.zero;
                    fill.rectTransform.offsetMax = Vector2.zero;
                    fill.raycastTarget = false;
                }
            }

            Text kills = EmberforgeUI.Title("Kills", host, row.Kills.ToString(), UITheme.FontHeading,
                                            Emberforge.TextBody, TextAnchor.MiddleLeft);
            UIBuilder.Box(kills.rectTransform, KillsX - Pad, -8f, KillsWidth, 44f, TopLeft);
            UIBuilder.Outlined(kills, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void PaintMvp()
        {
            Text tag = EmberforgeUI.Title("Tag", _mvp, "MVP", UITheme.FontHeading, Emberforge.TextGold,
                                          TextAnchor.MiddleLeft);
            UIBuilder.Box(tag.rectTransform, 12f, 0f, 110f, 56f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(tag, Emberforge.Outline, Emberforge.StrokeThin);

            Text name = EmberforgeUI.Title("Name", _mvp, _view.MvpName, UITheme.FontHeading, Emberforge.TextBody,
                                           TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 130f, 0f, 420f, 56f, new Vector2(0f, 0.5f));
            UIBuilder.BestFit(name, 16, UITheme.FontHeading);
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            Text share = EmberforgeUI.Label("Share", _mvp, Mathf.RoundToInt(_view.MvpShare * 100f) + "% OF DAMAGE",
                                            UITheme.FontBody, Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(share.rectTransform, 570f, 0f, 520f, 56f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(share, Emberforge.Outline, Emberforge.StrokeThin);
        }
    }
}
