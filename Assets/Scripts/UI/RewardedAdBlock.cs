using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Screens;
using Emberfall.Game;
using Emberfall.Game.Ads;

namespace Emberfall.UI
{
    /// <summary>
    /// THE REWARDED AD BLOCK (Docs/ui-specs/R28-SCREENS.md, "Rewarded ads"; Docs/DESIGN-REWARDED-ADS.md): one plate that
    /// draws RewardedAdPresenter's view and nothing else, built in exactly three places and never anywhere else:
    /// the idle claim popup after a claim (A1), the Proving beside the Brands counter (A2), and the wave result's
    /// stage cleared view on a repeat clear that paid the floor (A3). It is NEVER built on the Store, the Events
    /// screens, the Wayfarer's Road or any offer card, and the harness checks fail the build if a file with one of
    /// those names mentions it.
    ///
    /// THIS CLASS DECIDES NOTHING. Whether the block is there (View.Visible), every sentence on it, whether WATCH AN
    /// AD can be tapped and what the result was are fields of the view; a tap calls RewardedAdController.Tap or
    /// Dismiss and nothing else. With AdsSwitch off every view is Hidden, so nothing is built at all: the host node
    /// has no children and is inactive.
    ///
    /// The block sits BELOW the screen's own primary action and its button is a Secondary one, never the primary:
    /// the host screen's COLLECT, FIGHT or CONTINUE stays the one orange thing to press.
    /// </summary>
    public sealed class RewardedAdBlock
    {
        /// <summary>The plate's height at a wide width: two lines of text and the small column beside them, then a full
        /// touch-minimum button row.</summary>
        public const float Height = 208f;

        /// <summary>Below this width the block stacks (the Proving's, beside its card): the line gets the whole width,
        /// the small line sits under it, then the buttons.</summary>
        public const float NarrowBelow = 600f;

        /// <summary>The plate's height at a narrow width: three lines of text, the small line, then the buttons.</summary>
        public const float HeightNarrow = 288f;

        private const float Pad = 16f;
        private const float TextHeight = 64f;
        private const float TextHeightNarrow = 92f;
        private const float RightColumn = 230f;
        private const float WatchWidth = 280f;
        private const float OkWidth = 180f;
        private const float WatchWidthNarrow = 220f;
        private const float OkWidthNarrow = 140f;

        /// <summary>The height a block of this width is drawn at.</summary>
        public static float HeightFor(float width)
        {
            return width < NarrowBelow ? HeightNarrow : Height;
        }

        private readonly GameApp _app;
        private readonly AdPlacementKind _kind;
        private readonly RectTransform _host;
        private readonly float _width;
        private readonly Func<AdContext> _context;

        private RewardedAdController _controller;
        private string _signature = "";

        /// <summary>The host node: size and place it, the block fills it. Inactive and empty while the view is Hidden.</summary>
        public RectTransform Root { get { return _host; } }

        public RewardedAdBlock(GameApp app, AdPlacementKind kind, RectTransform host, float width, Func<AdContext> context)
        {
            _app = app;
            _kind = kind;
            _host = host;
            _width = width;
            _context = context;
            UIBuilder.Size(_host, width, HeightFor(width));
        }

        public AdPlacementKind Kind { get { return _kind; } }

        /// <summary>The view as it stands, for a caller that needs to know whether the block is drawn.</summary>
        public RewardedAdView View
        {
            get { return _controller != null ? _controller.View(Context()) : null; }
        }

        /// <summary>
        /// A new controller for a new visit: the ad backend is read now (the simulated one in the editor and
        /// development builds, none otherwise, whatever AdBackends.Override holds), and the result of an earlier
        /// visit does not follow the player in. Then draws.
        /// </summary>
        public void Bind()
        {
            Unbind();
            if (_app == null || _app.Session == null) return;
            _controller = new RewardedAdController(_kind, _app.Session, AdBackends.Current);
            _controller.Changed += OnChanged;
            _controller.Granted += OnGranted;
            _signature = "";
            Refresh();
        }

        public void Unbind()
        {
            if (_controller == null) return;
            _controller.Changed -= OnChanged;
            _controller.Granted -= OnGranted;
            _controller = null;
        }

        /// <summary>The tap on WATCH AN AD. Public so a capture drives the same door a finger does.</summary>
        public void Watch()
        {
            if (_controller == null) return;
            _app.Audio.Tap();
            _controller.Tap(Context());
        }

        /// <summary>The tap on OK.</summary>
        public void Dismiss()
        {
            if (_controller == null) return;
            _app.Audio.Tap();
            _controller.Dismiss();
        }

        private AdContext Context()
        {
            return _context != null ? _context() : new AdContext();
        }

        private void OnChanged()
        {
            Refresh();
        }

        /// <summary>The network's rewarded callback paid the offer: the game is saved, the purse shown, and a sound plays.</summary>
        private void OnGranted()
        {
            _app.Audio.Reward();
            _app.RefreshHud();
            _app.RequestSave();
        }

        /// <summary>
        /// Draws the view. Called on every controller change and once a second while a countdown is on screen; it
        /// returns at once when the view says what it said last time, so the once-a-second call never rebuilds
        /// a button under a thumb.
        /// </summary>
        public void Refresh()
        {
            if (_controller == null || _host == null) return;
            RewardedAdView v = _controller.View(Context());
            string signature = Signature(v);
            if (signature == _signature) return;
            _signature = signature;
            Clear();
            if (!v.Visible)
            {
                _host.gameObject.SetActive(false);
                return;
            }
            _host.gameObject.SetActive(true);
            Draw(v);
        }

        private void Clear()
        {
            for (int i = _host.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_host.GetChild(i).gameObject);
        }

        private void Draw(RewardedAdView v)
        {
            // The plate is a child of its own, so a redraw only ever destroys children.
            RectTransform plate = UIBuilder.Node("Plate", _host);
            UIBuilder.FillParent(plate, 0f);
            EmberforgeUI.SunkenOn(plate);

            bool narrow = _width < NarrowBelow;
            float okWidth = narrow ? OkWidthNarrow : OkWidth;
            float watchWidth = narrow ? WatchWidthNarrow : WatchWidth;

            // The line left, the small column right (wide); the line over the small line (narrow): the reward line
            // before the tap, or the result after it.
            bool showingResult = v.ResultLine.Length > 0;
            string main = showingResult ? v.ResultLine : v.RewardLine;
            Color mainColor = showingResult ? (v.ResultIsReward ? HubStyle.ValueGold : Emberforge.TextQuiet) : Emberforge.TextBody;
            Text line = EmberforgeUI.Body("Line", _host, main, UITheme.FontSmall, mainColor, TextAnchor.UpperLeft);
            if (narrow) UIBuilder.Box(line.rectTransform, Pad, -Pad, _width - Pad * 2f, TextHeightNarrow, new Vector2(0f, 1f));
            else UIBuilder.Box(line.rectTransform, Pad, -Pad, _width - Pad * 3f - RightColumn, TextHeight, new Vector2(0f, 1f));

            string side = v.NextResetText.Length > 0 ? v.NextResetText : v.LeftTodayText;
            if (side.Length > 0)
            {
                Text small = EmberforgeUI.Label("Side", _host, side, UITheme.FontTiny, Emberforge.TextQuiet,
                                                narrow ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
                if (narrow) UIBuilder.Box(small.rectTransform, Pad, -(Pad + TextHeightNarrow), _width - Pad * 2f, 30f, new Vector2(0f, 1f));
                else UIBuilder.Box(small.rectTransform, -Pad, -Pad, RightColumn, 32f, new Vector2(1f, 1f));
            }

            // The buttons, right to left: OK on the outside edge, WATCH AN AD beside it. Both are Secondary and both a
            // full touch minimum tall.
            float x = -Pad;
            if (v.Dismiss.Visible)
            {
                UIButton ok = EmberforgeUI.Button("Dismiss", _host, v.Dismiss.Label, UITheme.FontSmall, ForgeButtonKind.Secondary);
                UIBuilder.Box(ok.Root, x, Pad, okWidth, HubStyle.TouchMin, new Vector2(1f, 0f));
                ok.Interactable = v.Dismiss.Enabled;
                ok.OnClick(Dismiss);
                x -= okWidth + Pad;
            }
            if (v.Watch.Visible)
            {
                UIButton watch = EmberforgeUI.Button("Watch", _host, v.Watch.Label, UITheme.FontSmall, ForgeButtonKind.Secondary);
                UIBuilder.Box(watch.Root, x, Pad, watchWidth, HubStyle.TouchMin, new Vector2(1f, 0f));
                watch.Interactable = v.Watch.Enabled;
                watch.OnClick(Watch);
            }
        }

        /// <summary>Every field the block draws, as one string: equal strings are the same picture.</summary>
        private static string Signature(RewardedAdView v)
        {
            return v.State + "|" + v.Visible + "|" + v.RewardLine + "|" + v.LeftTodayText + "|" + v.ResultLine + "|" + v.ResultIsReward
                 + "|" + v.NextResetText + "|" + v.Watch.Visible + "," + v.Watch.Enabled + "," + v.Watch.Label
                 + "|" + v.Dismiss.Visible + "," + v.Dismiss.Enabled + "," + v.Dismiss.Label;
        }
    }
}
