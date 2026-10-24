using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// LAYER 3: the rail's overflow menu - the short list of things that are neither a place on
    /// the map nor a door anybody presses twice a session.
    ///
    /// It holds Settings, and in a development build it also holds the Dev Arena. That pairing is
    /// the whole point of the menu existing: both of them used to be loose buttons stacked down
    /// the right edge of the Hub, where DEV overlapped the PROVING marker and the "..." button it
    /// was stacked under, and where a shipped build simply had a hole in the stack because the
    /// dev entry had compiled away.
    ///
    /// THE DEV ENTRY IS COMPILED OUT, NOT HIDDEN. Everything below that names the arena sits
    /// inside #if UNITY_EDITOR || DEVELOPMENT_BUILD, which is the same gate every file under
    /// Assets/Scripts/Dev carries and the same one MainMenuScreen used to carry for this button.
    /// A release player has no menu row because it has no method.
    ///
    /// It closes on the back key, on its own rows, and on a tap anywhere outside it, which is
    /// what a menu anchored to a button is expected to do. The scrim that catches that outside
    /// tap is invisible rather than dark: this is a menu, not a modal, and dimming the whole Hub
    /// behind four rows would say otherwise.
    /// </summary>
    public sealed class HubOverflowMenu
    {
        private const float MenuWidth = 420f;
        private const float RowGap = 6f;
        private const float MenuPad = 12f;

        private readonly GameApp _app;
        private readonly RectTransform _layer;
        private readonly Action _openSettings;

        private RectTransform _root;
        private RectTransform _plate;

        private UiLayerStack.Layer _layerHandle;

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        public HubOverflowMenu(GameApp app, RectTransform layer, Action openSettings)
        {
            _app = app;
            _layer = layer;
            _openSettings = openSettings;
        }

        /// <summary>
        /// Opens the menu under the button that owns it. The caller passes the distance from the
        /// top of the safe area to that button, so the menu tracks the rail rather than carrying
        /// its own copy of where the rail's fourth entry happens to sit.
        /// </summary>
        public void Show(float topOffset)
        {
            Build();

            // Beside the rail, on the rail's own edge. Both are driven by the same switch, so a
            // rail that moves takes its menu with it rather than leaving it hanging in the middle
            // of the map.
            bool right = HubStyle.RailOnRightEdge;
            float column = HubStyle.HudEdge + HubStyle.TouchMin + HubStyle.RailGap;
            _plate.anchorMin = new Vector2(right ? 1f : 0f, 1f);
            _plate.anchorMax = _plate.anchorMin;
            _plate.pivot = _plate.anchorMin;
            // Kept above the nav bar and inside the safe area: a short canvas (20:9) or a narrow one (16:10, where the
            // nav reaches this column) slides the menu up instead of letting it run under the nav or off the bottom.
            RectTransform safe = _plate.parent as RectTransform;
            float safeHeight = safe != null && safe.rect.height > 200f ? safe.rect.height : UITheme.ReferenceResolution.y;
            float lowest = safeHeight - HubStyle.NavBarHeight - RowGap - _plate.sizeDelta.y;
            topOffset = Mathf.Max(0f, Mathf.Min(topOffset, lowest));
            _plate.anchoredPosition = new Vector2(right ? -column : column, -topOffset);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            if (_layerHandle == null) _layerHandle = _app.Layers.Push("menu:overflow", CloseInternal);
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

        private void Build()
        {
            if (_root != null) return;

            _root = UIBuilder.Node("OverflowMenu", _layer);
            UIBuilder.FillParent(_root, 0f);
            HudWidgets.Scrim(_root, 0f, Close);

            RectTransform safe = UIBuilder.Node("Safe", _root);
            UIBuilder.FillParent(safe, 0f);
            SafeAreaRect.Attach(safe);

            List<string> rows = new List<string>();
            List<Action> actions = new List<Action>();

            rows.Add("SETTINGS");
            actions.Add(delegate
            {
                Close();
                if (_openSettings != null) _openSettings();
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            rows.Add("DEV ARENA");
            actions.Add(delegate
            {
                Close();
                OpenDevArena();
            });

            // The component gallery joins the arena in the same guard, for the same reason: it is
            // a page for whoever is BUILDING this game, and a release player has no row for it
            // because it has no method. Built lazily - the gallery constructs several dozen live
            // components, and a menu that assembled all of them to show two rows of text would
            // pay for a page most sessions never open.
            rows.Add("DESIGN GALLERY");
            actions.Add(delegate
            {
                Close();
                OpenDesignGallery();
            });
#endif

            float height = MenuPad * 2f + rows.Count * HubStyle.TouchMin + (rows.Count - 1) * RowGap;
            _plate = HudWidgets.Popup("Plate", safe, MenuWidth, height, 0f, 0f, new Vector2(1f, 1f));

            for (int i = 0; i < rows.Count; i++)
            {
                UIButton row = EmberforgeUI.Button("Row" + i, _plate, rows[i], HubStyle.FontButton,
                                                   ForgeButtonKind.Secondary);
                UIBuilder.Box(row.Root, 0f, -(MenuPad + i * (HubStyle.TouchMin + RowGap)),
                              MenuWidth - MenuPad * 2f, HubStyle.TouchMin, new Vector2(0.5f, 1f));
                Action action = actions[i];
                row.OnClick(delegate
                {
                    _app.Audio.Tap();
                    action();
                });
            }

            _root.gameObject.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Dev.DesignGallery _gallery;

        /// <summary>Opens the component gallery, building it the first time it is asked for.
        /// Public so the capture tool can reach it without going through the menu, which is the
        /// same door MainMenuScreen.OpenLayerForCapture uses for every other Hub layer.</summary>
        public void OpenDesignGallery()
        {
            if (_gallery == null) _gallery = new Dev.DesignGallery(_app, _layer);
            _gallery.Show();
        }

        private static void OpenDevArena()
        {
            // Loaded by name so this does not need a reference to the scene asset. The scene is in
            // the build only when the build was a Development Build; if it is not there, say so
            // rather than failing silently.
            if (!Application.CanStreamedLevelBeLoaded("DevArena"))
            {
                Debug.LogWarning("Emberfall: the Dev Arena scene is not in this build. It is added "
                                 + "only to Development Builds.");
                return;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("DevArena");
        }
#endif
    }
}
