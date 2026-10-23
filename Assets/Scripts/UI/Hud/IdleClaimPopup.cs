using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
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

        /// <summary>
        /// Measured against the content rather than picked, and re-measured against a capture
        /// rather than against the arithmetic.
        ///
        /// The amounts, the stored line and the well come to 262 below the ribbon, the buttons
        /// take a full TouchMin, and the padding takes the rest. Every predecessor of this number
        /// was too big and the fault was the same each time: 520 before the Hub pass showed a
        /// hand's width of empty plate above the buttons, 420 fixed that, and 470 put it back the
        /// moment the ribbon and the well changed what was above them. It is checked by looking at
        /// the photograph, because nothing in the harness can see a gap.
        /// </summary>
        private const float PopupHeight = 430f;

        /// <summary>
        /// How far the plate sits above the screen's centre. The toast ("Collected ... gold") lands 300 up from the bottom
        /// edge, which on a 966 tall canvas is across the foot of a centred 430 plate and over CLOSE and CLAIM; lifted this
        /// far the plate's foot clears the toast on both canvas shapes (the ad block under it can still be crossed by the
        /// toast for its second and a half, which costs nothing).
        /// </summary>
        private const float PopupLift = 120f;
        private const float RowHeight = 34f;

        /// <summary>
        /// Where content starts, measured from the plate's top edge.
        ///
        /// The ribbon STRADDLES that edge, so the first row has to clear the half hanging below
        /// it rather than the whole thing. The first capture of this popup cleared neither and the
        /// amounts sat against the ribbon's underside.
        /// </summary>
        private const float ContentTop = Emberforge.RibbonHeight * 0.5f + Emberforge.Space8;

        private readonly GameApp _app;
        private readonly RectTransform _layer;

        private RectTransform _root;
        private Text _amounts;
        private Text _stored;
        private Text _cap;
        private Text _rate;
        private UIButton _claim;

        /// <summary>
        /// A1 of the rewarded ads (Docs/ui-specs/R28-SCREENS.md): the block under the plate, drawn after a claim and
        /// never before one. _claimedGold is the Gold the last claim in this visit paid, which is the context the
        /// block is asked about; it is 0 on a fresh visit, so the block is not there until something is claimed.
        /// With AdsSwitch off the block draws nothing at all.
        /// </summary>
        private RewardedAdBlock _ad;
        private long _claimedGold;

        private UiLayerStack.Layer _layerHandle;

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        public IdleClaimPopup(GameApp app, RectTransform layer)
        {
            _app = app;
            _layer = layer;
        }

        /// <summary>The ad block, for the capture tool and the checks. Null until the popup is first shown.</summary>
        public RewardedAdBlock AdBlock { get { return _ad; } }

        public void Show()
        {
            Build();
            _claimedGold = 0;
            if (_ad != null) _ad.Bind();
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
            if (_ad != null) _ad.Unbind();
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
            if (_ad != null) _ad.Refresh();
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
                                                   0f, PopupLift, new Vector2(0.5f, 0.5f));

            // The heading is a ribbon across the top of the plate rather than a line of text
            // inside it, which is the whole of what this layer gains from the design system: the
            // popup now announces itself the way every other framed surface in the game does.
            EmberforgeUI.TitleRibbon("Heading", popup, "IDLE REWARDS", PopupWidth * 0.56f);

            float inner = Emberforge.Space24;

            _amounts = EmberforgeUI.Title("Amounts", popup, "", UITheme.FontHeading,
                                          Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(_amounts.rectTransform, 0f, -ContentTop, PopupWidth - inner * 2f, 54f,
                          new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_amounts, Emberforge.Outline, Emberforge.StrokeThick);

            _stored = EmberforgeUI.Label("Stored", popup, "", HubStyle.FontValue,
                                         Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(_stored.rectTransform, 0f, -(ContentTop + 60f), PopupWidth - inner * 2f, RowHeight,
                          new Vector2(0.5f, 1f));

            // The two quiet lines sit in a sunken well, so the popup separates "what you have
            // earned" from "how this works" by depth rather than by font size alone.
            RectTransform well = UIBuilder.Node("Well", popup);
            UIBuilder.Box(well, 0f, -(ContentTop + 102f), PopupWidth - inner * 2f, 124f, new Vector2(0.5f, 1f));
            EmberforgeUI.SunkenOn(well);

            _cap = EmberforgeUI.Body("Cap", well, "", HubStyle.FontSecondary,
                                     Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_cap.rectTransform, 0f, -Emberforge.Space16,
                          PopupWidth - inner * 2f - Emberforge.Space32, 62f, new Vector2(0.5f, 1f));

            _rate = EmberforgeUI.Label("Rate", well, "", HubStyle.FontSecondary,
                                       Emberforge.TextGold, TextAnchor.LowerCenter);
            UIBuilder.Box(_rate.rectTransform, 0f, Emberforge.Space12,
                          PopupWidth - inner * 2f - Emberforge.Space32, RowHeight,
                          new Vector2(0.5f, 0f));

            // The only primary button on this layer, exactly as CLAIM was the only Gold button on
            // the panel it replaces: one orange thing to press, and it is the thing the layer
            // exists for.
            _claim = EmberforgeUI.Button("Claim", popup, "CLAIM", HubStyle.FontButton,
                                         ForgeButtonKind.Primary);
            UIBuilder.Box(_claim.Root, -inner, inner, 300f, HubStyle.TouchMin, new Vector2(1f, 0f));
            _claim.OnClick(ClaimNow);

            UIButton close = EmberforgeUI.Button("Close", popup, "CLOSE", HubStyle.FontButton,
                                                 ForgeButtonKind.Secondary);
            UIBuilder.Box(close.Root, inner, inner, 260f, HubStyle.TouchMin, new Vector2(0f, 0f));
            close.OnClick(Close);

            // A1: BELOW the plate, so it is under both buttons and never above CLAIM. Its own host node, outside the plate,
            // because the plate is a fixed size and the block is only there after a claim.
            RectTransform adHost = UIBuilder.Node("AdBlock", safe);
            UIBuilder.Box(adHost, 0f, PopupLift - (PopupHeight * 0.5f + Emberforge.Space16 + RewardedAdBlock.Height * 0.5f),
                          PopupWidth, RewardedAdBlock.Height, new Vector2(0.5f, 0.5f));
            _ad = new RewardedAdBlock(_app, AdPlacementKind.IdleGold, adHost, PopupWidth, delegate
            {
                AdContext context = new AdContext();
                context.ClaimGold = _claimedGold;
                return context;
            });
            adHost.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        /// <summary>CAPTURE ONLY: the tap on CLAIM, through the same private handler the button drives.</summary>
        public void ClaimForCapture() { ClaimNow(); }

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
            _claimedGold = report.Gold;
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
