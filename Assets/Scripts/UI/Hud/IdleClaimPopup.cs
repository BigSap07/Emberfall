using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Session;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// LAYER 3: the idle cache, opened from the chest.
    ///
    /// Every word of this used to be on the map. A 640x200 panel sat in the top right of the Hub
    /// carrying a heading, the stored amounts, the elapsed time, the cap and the per-hour rates -
    /// five lines of chrome over a painting, permanently, so that a number that changes once a
    /// second could be read without tapping anything. The chest and its bubble keep the part that
    /// earns its place on the map, which is "there is something in here and it is worth this
    /// much"; everything else moved in here, where it costs nothing until it is asked for.
    ///
    /// The popup is built once and reused. It refreshes when it opens and while it is open, so
    /// the timer keeps running under the player's eyes rather than freezing the moment they look
    /// at it, and a claim repaints it in place rather than closing it - the cache is empty now
    /// and saying so is more useful than a popup that vanishes.
    /// </summary>
    public sealed class IdleClaimPopup
    {
        private const float PopupWidth = 760f;
        // 420, measured against the content rather than picked: heading, amounts, stored
        // time, the cap sentence and the rate line come to about 270, and the buttons and
        // padding take the rest. It was 520, and the first capture of it showed a hand's
        // width of empty plate between the rates and the buttons.
        private const float PopupHeight = 420f;
        private const float RowHeight = 34f;

        private readonly GameApp _app;
        private readonly RectTransform _layer;

        private RectTransform _root;
        private Text _amounts;
        private Text _stored;
        private Text _cap;
        private Text _rate;
        private UIButton _claim;

        private UiLayerStack.Layer _layerHandle;

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        public IdleClaimPopup(GameApp app, RectTransform layer)
        {
            _app = app;
            _layer = layer;
        }

        public void Show()
        {
            Build();
            Refresh();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            if (_layerHandle == null) _layerHandle = _app.Layers.Push("popup:claim", CloseInternal);
        }

        public void Close()
        {
            if (_layerHandle != null)
            {
                _app.Layers.Dismiss(_layerHandle);
                return;
            }
            CloseInternal();
        }

        private void CloseInternal()
        {
            _layerHandle = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Repaints from the session. Called when the popup opens, once a second while it is open
        /// (the Hub's own tick drives it, so this class needs no MonoBehaviour of its own) and
        /// straight after a claim.
        /// </summary>
        public void Refresh()
        {
            if (_root == null) return;
            GameSession session = _app.Session;
            if (session == null) return;

            IdleReport report = session.PeekIdle(TimeUtil.NowUnix());

            _amounts.text = report.Gold + " GOLD    " + report.Shards + " SHARDS";
            _stored.text = FormatDuration(report.Seconds) + " stored";
            _cap.text = report.Capped
                ? "The cache is full. Anything earned past the cap is lost."
                : "Fills for up to " + Mathf.RoundToInt(session.Balance.IdleCapHours) + "h while you are away.";
            _rate.text = "+" + Mathf.RoundToInt(session.IdleGoldPerHour) + " gold  /  +"
                       + Mathf.RoundToInt(session.IdleShardsPerHour) + " shards per hour";
            _claim.Interactable = report.HasAnything;
        }

        private void Build()
        {
            if (_root != null) return;

            _root = UIBuilder.Node("IdleClaim", _layer);
            UIBuilder.FillParent(_root, 0f);
            HudWidgets.Scrim(_root, HubStyle.PopupScrimAlpha, Close);

            RectTransform safe = UIBuilder.Node("Safe", _root);
            UIBuilder.FillParent(safe, 0f);
            SafeAreaRect.Attach(safe);

            RectTransform popup = HudWidgets.Popup("Plate", safe, PopupWidth, PopupHeight,
                                                   0f, 0f, new Vector2(0.5f, 0.5f));

            Text heading = UIBuilder.Label("Heading", popup, "IDLE REWARDS", HubStyle.FontSecondary,
                                           HubStyle.SoonText, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, HubStyle.PanelPadX, -HubStyle.PanelPadY,
                          420f, RowHeight, new Vector2(0f, 1f));

            _amounts = UIBuilder.Label("Amounts", popup, "", UITheme.FontHeading, HubStyle.ValueGold,
                                       TextAnchor.UpperLeft);
            UIBuilder.Box(_amounts.rectTransform, HubStyle.PanelPadX, -(HubStyle.PanelPadY + 44f),
                          PopupWidth - HubStyle.PanelPadX * 2f, 52f, new Vector2(0f, 1f));

            _stored = UIBuilder.Label("Stored", popup, "", HubStyle.FontValue, HubStyle.LabelText,
                                      TextAnchor.UpperLeft);
            UIBuilder.Box(_stored.rectTransform, HubStyle.PanelPadX, -(HubStyle.PanelPadY + 112f),
                          PopupWidth - HubStyle.PanelPadX * 2f, RowHeight, new Vector2(0f, 1f));

            _cap = UIBuilder.WrappedLabel("Cap", popup, "", HubStyle.FontSecondary,
                                          HubStyle.TextSecondary, TextAnchor.UpperLeft);
            UIBuilder.Box(_cap.rectTransform, HubStyle.PanelPadX, -(HubStyle.PanelPadY + 156f),
                          PopupWidth - HubStyle.PanelPadX * 2f, 64f, new Vector2(0f, 1f));

            _rate = UIBuilder.Label("Rate", popup, "", HubStyle.FontSecondary,
                                    UITheme.WithAlpha(HubStyle.TextSecondary, HubStyle.SoonAlpha),
                                    TextAnchor.UpperLeft);
            UIBuilder.Box(_rate.rectTransform, HubStyle.PanelPadX, -(HubStyle.PanelPadY + 228f),
                          PopupWidth - HubStyle.PanelPadX * 2f, RowHeight, new Vector2(0f, 1f));

            // The only Gold button on this layer, exactly as CLAIM was the only Gold button on the
            // panel it replaces: Gold is the primary action and there is one of them here.
            _claim = UIBuilder.GoldButton("Claim", popup, "CLAIM", HubStyle.FontButton);
            UIBuilder.Box(_claim.Root, -HubStyle.PanelPadX, HubStyle.PanelPadY, 300f, HubStyle.TouchMin,
                          new Vector2(1f, 0f));
            _claim.OnClick(ClaimNow);

            UIButton close = UIBuilder.SecondaryButton("Close", popup, "CLOSE", HubStyle.FontButton);
            UIBuilder.Box(close.Root, HubStyle.PanelPadX, HubStyle.PanelPadY, 260f, HubStyle.TouchMin,
                          new Vector2(0f, 0f));
            close.OnClick(Close);

            _root.gameObject.SetActive(false);
        }

        private void ClaimNow()
        {
            IdleReport report = _app.Session.ClaimIdle(TimeUtil.NowUnix());
            if (!report.HasAnything)
            {
                _app.Notify("The cache is still filling.");
                return;
            }
            _app.Audio.Reward();
            _app.Notify("Collected " + report.Gold + " gold and " + report.Shards + " shards.");
            _app.RefreshHud();
            _app.RequestSave();
            Refresh();
            if (Claimed != null) Claimed();
        }

        /// <summary>Raised after a successful claim, so the chest's bubble can drop to zero the
        /// moment the cache empties rather than on its next tick.</summary>
        public event Action Claimed;

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60) return seconds + "s";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "m";
            long hours = minutes / 60;
            return hours + "h " + (minutes % 60) + "m";
        }
    }
}
