using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// LAYER 2: a sheet over a dimmed map, with an existing screen drawn inside it.
    ///
    /// The Hub used to reach its social and account screens by NAVIGATING to them - the map
    /// disappeared, a full page took over, and coming back was a back button's job. That is the
    /// right shape for a place you go (the campaign, a battle, the forge) and the wrong shape for
    /// something you glance at and dismiss, which is what an account, a friends list and a guild
    /// are. So they are panels now: the Hub stays on screen, dimmed, underneath.
    ///
    /// NOTHING HERE IS A NEW SCREEN. The panel is a frame; what it draws inside itself is the
    /// same AccountScreen, FriendsScreen, ProfileScreen, SettingsScreen and GuildScreen the
    /// router owns, reparented into the sheet for as long as the panel is open and handed back
    /// afterwards. They keep their own state, their own services and their own coroutines, and
    /// they still work as full pages if anything ever routes to them directly - UIScreen.Embedded
    /// is the one thing that changes, and all it does is hide the page header the sheet is already
    /// providing and let the content sit against the sheet's edges instead of under a top bar.
    ///
    /// WHY THE ROUTER IS NOT INVOLVED. A panel is not a screen change: Router.Current stays on
    /// the Hub the whole time, the map keeps ticking behind the dim, and the back stack that
    /// closes the panel is UiLayerStack rather than the router's history. The one place the two
    /// meet is ScreenRouter.Show, which closes every open layer before it changes screens - a
    /// panel left standing over a page it knows nothing about is the bug that rule prevents.
    /// </summary>
    public sealed class PanelHost
    {
        /// <summary>One tab: a caption and the screen drawn when it is selected.</summary>
        private struct Tab
        {
            public string Caption;
            public UIScreen Screen;
        }

        private readonly GameApp _app;
        private readonly RectTransform _layer;

        private RectTransform _root;
        private RectTransform _sheet;
        private RectTransform _body;
        private RectTransform _tabRow;
        private Text _title;

        private readonly List<Tab> _tabs = new List<Tab>();
        private readonly List<HudElement> _tabButtons = new List<HudElement>();

        private UIScreen _hosted;
        private UiLayerStack.Layer _layerHandle;

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        /// <summary>Which tab is showing, so a second tap on the same door is a no-op rather than
        /// a rebuild, and so a capture can name what it is looking at.</summary>
        public string OpenTabName { get; private set; }

        public PanelHost(GameApp app, RectTransform layer)
        {
            _app = app;
            _layer = layer;
        }

        // ------------------------------------------------------------------ the doors

        /// <summary>
        /// The player's own panel. Four tabs, and the order is the order a player asks the
        /// questions in: who am I, what is my account, who do I play with, and what can I change.
        ///
        /// PROFILE IS A TAB RATHER THAN A SEPARATE DOOR because the plate on the top bar is the
        /// only thing that opens any of this, and a plate that opened one page while three
        /// related ones hid elsewhere is the shape this whole pass exists to remove.
        /// </summary>
        public void ShowProfile(string tab)
        {
            _tabs.Clear();
            Add("PROFILE", _app.Router.Profile);
            Add("ACCOUNT", _app.Router.Account);
            Add("FRIENDS", _app.Router.Friends);
            Add("SETTINGS", _app.Router.Settings);
            Open("PROFILE", tab);
        }

        /// <summary>The guild, from the nav bar. One tab, so the bar is not drawn at all - a tab
        /// row with a single tab in it is decoration pretending to be a control.</summary>
        public void ShowGuild()
        {
            _tabs.Clear();
            Add("GUILD", _app.Router.Guild);
            Open("GUILD", "GUILD");
        }

        private void Add(string caption, UIScreen screen)
        {
            if (screen == null) return;   // a screen that failed to build is skipped, not fatal
            Tab tab = new Tab();
            tab.Caption = caption;
            tab.Screen = screen;
            _tabs.Add(tab);
        }

        // ------------------------------------------------------------------ open and close

        private void Open(string title, string tab)
        {
            if (_tabs.Count == 0) return;
            Build();

            _title.text = title;
            BuildTabs();

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            int index = 0;
            for (int i = 0; i < _tabs.Count; i++)
                if (string.Equals(_tabs[i].Caption, tab, StringComparison.OrdinalIgnoreCase)) index = i;
            Select(index);

            // Registered once per opening rather than per tab change: switching tabs inside an
            // open panel is not a new layer, and treating it as one would make the back key close
            // the panel one tab at a time.
            if (_layerHandle == null)
                _layerHandle = _app.Layers.Push("panel:" + title, CloseInternal);
        }

        /// <summary>Closes the panel through the layer stack, which is what every caller outside
        /// this class wants: the close button, the scrim and the back key all end up here.</summary>
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
            OpenTabName = "";
            Detach();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ hosting a screen

        private void Select(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            Tab tab = _tabs[index];
            if (_hosted == tab.Screen) return;

            Detach();
            _hosted = tab.Screen;
            OpenTabName = tab.Caption;

            for (int i = 0; i < _tabButtons.Count; i++)
                HudWidgets.SetTabSelected(_tabButtons[i], i == index);

            // Reparented rather than rebuilt. The screen keeps every field it has ever filled in,
            // so opening a panel costs one SetParent and an OnShow rather than a page's worth of
            // construction, and a friends list that was already loaded is still loaded.
            _hosted.Root.SetParent(_body, false);
            UIBuilder.FillParent(_hosted.Root, 0f);
            _hosted.SetEmbedded(true);
            _hosted.Root.gameObject.SetActive(true);
            if (_hosted.Group != null) _hosted.Group.alpha = 1f;
            _hosted.OnShow();
        }

        private void Detach()
        {
            if (_hosted == null) return;
            UIScreen screen = _hosted;
            _hosted = null;

            screen.OnHide();
            screen.Root.gameObject.SetActive(false);
            screen.SetEmbedded(false);
            screen.Root.SetParent(_app.ScreenLayer, false);
            UIBuilder.FillParent(screen.Root, 0f);
        }

        // ------------------------------------------------------------------ construction

        private void Build()
        {
            if (_root != null) return;

            _root = UIBuilder.Node("Panel", _layer);
            UIBuilder.FillParent(_root, 0f);

            // The dim is full bleed and the sheet is inside the safe area: the map dims edge to
            // edge including under a notch, and nothing readable goes there.
            HudWidgets.Scrim(_root, HubStyle.PanelScrimAlpha, Close);

            RectTransform safe = UIBuilder.Node("Safe", _root);
            UIBuilder.FillParent(safe, 0f);
            SafeAreaRect.Attach(safe);

            _sheet = HudWidgets.Sheet("Sheet", safe);
            UIBuilder.FillParent(_sheet, HubStyle.HudEdge);

            // Cinzel in gold, because a sheet's title is a TITLE - the one word that says which
            // door the player came through. Same rect and same anchor as before: the tab row and
            // the body below it are measured against SheetHeaderHeight, and a conversion that
            // moved this would have moved every screen the sheet hosts.
            _title = EmberforgeUI.Title("Title", _sheet, "", UITheme.FontHeading,
                                        Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(_title.rectTransform, HubStyle.PanelPadX, -HubStyle.PanelPadY,
                          720f, HubStyle.SheetHeaderHeight - HubStyle.PanelPadY * 2f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_title, Emberforge.Outline, Emberforge.StrokeThin);

            HudWidgets.CloseButton(_sheet, Close);

            _tabRow = UIBuilder.Node("Tabs", _sheet);
            UIBuilder.Box(_tabRow, HubStyle.PanelPadX, -HubStyle.SheetHeaderHeight, 1200f,
                          HubStyle.TabHeight, new Vector2(0f, 1f));
            UIBuilder.HStack(_tabRow, HubStyle.TabGap, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

            _body = UIBuilder.Node("Body", _sheet);
            UIBuilder.FillParent(_body, 0f, HubStyle.SheetHeaderHeight + HubStyle.TabHeight + HubStyle.TabGap,
                                 0f, 0f);

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The tab row is rebuilt per opening rather than kept, because the two doors into this
        /// panel carry different tabs and a row of buttons that is sometimes four and sometimes
        /// one is not worth the bookkeeping to reuse.
        /// </summary>
        private void BuildTabs()
        {
            for (int i = _tabRow.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_tabRow.GetChild(i).gameObject);
            _tabButtons.Clear();

            bool showTabs = _tabs.Count > 1;
            _tabRow.gameObject.SetActive(showTabs);

            // With no tab row the body reclaims its height, so a single-screen panel is not
            // drawn with an empty strip across the top of it.
            UIBuilder.FillParent(_body, 0f,
                                 HubStyle.SheetHeaderHeight + (showTabs ? HubStyle.TabHeight + HubStyle.TabGap : 0f),
                                 0f, 0f);
            if (!showTabs) return;

            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                HudElement button = HudWidgets.Tab(_tabs[i].Caption, _tabRow, delegate
                {
                    _app.Audio.Tap();
                    Select(index);
                });
                _tabButtons.Add(button);
            }
        }
    }
}
