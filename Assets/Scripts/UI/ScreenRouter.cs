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
        Profile,
        Formation,
        Heroes,
        HeroDetail,
        Campaign,
        Summon,
        Battle,
        RelicShop,
        Inventory,
        Events,
        Recital,
        Proving,
        Forge,
        Account,
        Friends,
        FriendTeam,
        Guild,
        GuildRequests,
        GuildChat,
        Settings,
        Talents,
        GuildDungeon,
        GuildFloor,
        GuildDungeonResult,
        GuildDungeonBoards
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

        /// <summary>
        /// True while this screen is being drawn INSIDE a panel rather than as a full page.
        ///
        /// A hosted screen is the same object either way - same fields, same services, same
        /// coroutines - and this flag is the whole of the difference: the panel is already
        /// drawing a title and a close button, so the screen's own header would be a second one,
        /// and its content can sit against the sheet's edges instead of under a top bar that is
        /// not there. Screens that are never hosted ignore it entirely.
        /// </summary>
        public bool Embedded { get; private set; }

        /// <summary>
        /// Called by PanelHost as a screen is hosted and again as it is handed back. Overriders
        /// call base first, then hide their header and re-inset their content; the base class
        /// only records the state, because there is nothing about it that every screen shares.
        /// </summary>
        public virtual void SetEmbedded(bool embedded)
        {
            Embedded = embedded;
        }

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
        public Screens.ProfileScreen Profile;
        public Screens.FormationScreen Formation;
        public Screens.HeroesScreen Heroes;
        public Screens.HeroDetailScreen HeroDetail;
        public Screens.CampaignScreen Campaign;
        public Screens.SummonScreen Summon;
        public Screens.BattleScreen Battle;
        public Screens.RelicShopScreen RelicShop;
        public Screens.InventoryScreen Inventory;
        public Screens.EventsScreen Events;
        public Screens.RecitalScreen Recital;
        public Screens.ProvingScreen Proving;
        public Screens.ForgeScreen Forge;
        public Screens.AccountScreen Account;
        public Screens.FriendsScreen Friends;
        public Screens.FriendTeamScreen FriendTeam;
        public Screens.GuildScreen Guild;
        public Screens.GuildRequestsScreen GuildRequests;
        public Screens.GuildChatScreen GuildChat;
        public Screens.SettingsScreen Settings;
        public Screens.TalentsScreen Talents;
        public Screens.GuildDungeonScreen GuildDungeon;
        public Screens.GuildFloorScreen GuildFloor;
        public Screens.GuildDungeonResultScreen GuildDungeonResult;
        public Screens.GuildDungeonBoardsScreen GuildDungeonBoards;

        public UIScreen Current { get; private set; }

        public void Build(GameApp app, RectTransform screenLayer, RectTransform overlayLayer)
        {
            App = app;
            ScreenLayer = screenLayer;
            OverlayLayer = overlayLayer;

            MainMenu = Create<Screens.MainMenuScreen>();
            Profile = Create<Screens.ProfileScreen>();
            Formation = Create<Screens.FormationScreen>();
            Heroes = Create<Screens.HeroesScreen>();
            HeroDetail = Create<Screens.HeroDetailScreen>();
            Campaign = Create<Screens.CampaignScreen>();
            Summon = Create<Screens.SummonScreen>();
            Battle = Create<Screens.BattleScreen>();
            RelicShop = Create<Screens.RelicShopScreen>();
            Inventory = Create<Screens.InventoryScreen>();
            Events = Create<Screens.EventsScreen>();
            Recital = Create<Screens.RecitalScreen>();
            Proving = Create<Screens.ProvingScreen>();
            Forge = Create<Screens.ForgeScreen>();

            // Built whether or not this build has a cloud. Each of the six renders an honest
            // empty state when GameApp.Cloud is null - "this build has no account service" rather
            // than a blank - which is cheaper and more useful than a router that sometimes has
            // six fewer screens in it and a menu that sometimes has three fewer buttons.
            Account = Create<Screens.AccountScreen>();
            Friends = Create<Screens.FriendsScreen>();
            FriendTeam = Create<Screens.FriendTeamScreen>();
            Guild = Create<Screens.GuildScreen>();
            GuildRequests = Create<Screens.GuildRequestsScreen>();
            GuildChat = Create<Screens.GuildChatScreen>();

            // Reached through the player panel's Settings tab rather than by routing to it, but
            // built here with everything else: PanelHost hosts UIScreens, and a screen the router
            // does not own would be the one thing in the panel with a different lifetime.
            Settings = Create<Screens.SettingsScreen>();

            // Reached from the hero page's TALENTS button, for one hero at a time.
            Talents = Create<Screens.TalentsScreen>();

            // The guild dungeon: entry, floor view, result and boards. Reached from the guild
            // screen and from a card in guild chat. Built unconditionally like the other cloud
            // screens, and each says honestly what it cannot show without a guild.
            GuildDungeon = Create<Screens.GuildDungeonScreen>();
            GuildFloor = Create<Screens.GuildFloorScreen>();
            GuildDungeonResult = Create<Screens.GuildDungeonResultScreen>();
            GuildDungeonBoards = Create<Screens.GuildDungeonBoardsScreen>();
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

            // A panel or popup is a layer OVER a screen, so a screen change with one still open
            // would draw it over a page it knows nothing about - and, worse, over a page that may
            // be the very screen the panel was hosting. Closing them here rather than at each
            // call site means every door out of the Hub is covered by one rule.
            if (App != null && App.Layers != null) App.Layers.CloseAll();
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
