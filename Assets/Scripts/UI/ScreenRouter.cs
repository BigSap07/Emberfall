using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;

namespace Emberfall.UI
{
    public enum ScreenId
    {
        MainMenu,
        Heroes,
        HeroDetail,
        Campaign,
        Summon,
        Battle
    }

    /// <summary>Base class for every full-screen page. Built once, then shown and hidden.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public GameApp App;
        public ScreenRouter Router;
        public RectTransform Root;
        public CanvasGroup Group;

        /// <summary>True when the screen draws its own header instead of the shared top bar.</summary>
        public virtual bool HidesTopBar { get { return false; } }

        public abstract ScreenId Id { get; }

        public void Initialise(GameApp app, ScreenRouter router, RectTransform root)
        {
            App = app;
            Router = router;
            Root = root;
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            Build();
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build();

        /// <summary>Called every time the screen becomes visible - repaint from the profile here.</summary>
        public virtual void OnShow() { }
        public virtual void OnHide() { }
    }

    /// <summary>Owns the screen instances and the transition between them.</summary>
    public sealed class ScreenRouter : MonoBehaviour
    {
        private readonly List<UIScreen> _screens = new List<UIScreen>();
        private readonly List<ScreenId> _history = new List<ScreenId>();

        public GameApp App;
        public RectTransform ScreenLayer;
        public RectTransform OverlayLayer;

        public Screens.MainMenuScreen MainMenu;
        public Screens.HeroesScreen Heroes;
        public Screens.HeroDetailScreen HeroDetail;
        public Screens.CampaignScreen Campaign;
        public Screens.SummonScreen Summon;
        public Screens.BattleScreen Battle;

        public UIScreen Current { get; private set; }

        public void Build(GameApp app, RectTransform screenLayer, RectTransform overlayLayer)
        {
            App = app;
            ScreenLayer = screenLayer;
            OverlayLayer = overlayLayer;

            MainMenu = Create<Screens.MainMenuScreen>();
            Heroes = Create<Screens.HeroesScreen>();
            HeroDetail = Create<Screens.HeroDetailScreen>();
            Campaign = Create<Screens.CampaignScreen>();
            Summon = Create<Screens.SummonScreen>();
            Battle = Create<Screens.BattleScreen>();
        }

        /// <summary>
        /// The component lives on the screen's own root object, so deactivating the screen
        /// also suspends its Update loops and coroutines.
        /// </summary>
        private T Create<T>() where T : UIScreen
        {
            RectTransform root = UIBuilder.Node(typeof(T).Name, ScreenLayer);
            UIBuilder.FillParent(root, 0f);
            T screen = root.gameObject.AddComponent<T>();
            try
            {
                screen.Initialise(App, this, root);
            }
            catch (System.Exception ex)
            {
                // one malformed screen should not take the whole prototype down
                Debug.LogError("Emberfall: failed to build " + typeof(T).Name + ": " + ex);
                root.gameObject.SetActive(false);
            }
            _screens.Add(screen);
            return screen;
        }

        public void Show(ScreenId id)
        {
            Show(id, true);
        }

        public void Show(ScreenId id, bool remember)
        {
            UIScreen next = Find(id);
            if (next == null) return;
            if (Current == next)
            {
                next.OnShow();
                return;
            }

            if (Current != null)
            {
                Current.OnHide();
                Current.Root.gameObject.SetActive(false);
                if (remember) _history.Add(Current.Id);
            }

            // returning to a screen consumes its history entry rather than stacking a duplicate
            if (!remember && _history.Count > 0 && _history[_history.Count - 1] == id)
                _history.RemoveAt(_history.Count - 1);

            Current = next;
            next.Root.gameObject.SetActive(true);
            next.Root.SetAsLastSibling();
            next.OnShow();
            App.SetTopBarVisible(!next.HidesTopBar);
            StopAllCoroutines();
            StartCoroutine(FadeIn(next.Group));
        }

        private IEnumerator FadeIn(CanvasGroup group)
        {
            if (group == null) yield break;
            float t = 0f;
            const float duration = 0.16f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        public void Back()
        {
            if (_history.Count == 0)
            {
                Show(ScreenId.MainMenu, false);
                return;
            }
            ScreenId previous = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Show(previous, false);
        }

        public void ClearHistory()
        {
            _history.Clear();
        }

        private UIScreen Find(ScreenId id)
        {
            for (int i = 0; i < _screens.Count; i++) if (_screens[i].Id == id) return _screens[i];
            return null;
        }
    }
}
