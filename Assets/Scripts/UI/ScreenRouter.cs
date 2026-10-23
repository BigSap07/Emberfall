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
        GuildDungeonBoards,
        GuildTech,
        Fusion,
        WaveResult,
        Orders,
        Armory,
        Archive,
        Quartermaster,
        WayfarersRoad,
        Store,
        LiveEvents,
        Marketplace
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

        /// <summary>
        /// The hardware back key's answer on this screen, asked before the router's history. True
        /// means the screen stepped back itself and the press is spent. The default is false: the
        /// history decides. A screen overrides it when "one screen back" is a place the history
        /// does not hold, because the screens between it and there were shown without remembering.
        /// </summary>
        public virtual bool TryStepBack() { return false; }
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
        public Screens.GuildTechScreen GuildTech;
        public Screens.FusionScreen Fusion;
        public Screens.WaveResultScreen WaveResult;

        /// <summary>The daily orders page. Null unless DailyLoopSwitch is on, so a build with the
        /// switch off has no such screen, and Show(ScreenId.Orders) is a no-op.</summary>
        public Screens.OrdersScreen Orders;

        /// <summary>The Armory (Valor) and the Archive (Coda), DESIGN-SINKS.md. Null unless
        /// SinksSwitch is on, so a build with the switch off has no such screens, and
        /// Show(ScreenId.Armory) or Show(ScreenId.Archive) is a no-op.</summary>
        public Screens.ArmoryScreen Armory;

        /// <summary>The Quartermaster (Gold), Docs/DESIGN-QUARTERMASTER.md. Null unless
        /// QuartermasterSwitch is on; Show(ScreenId.Quartermaster) is then a no-op.</summary>
        public Screens.QuartermasterScreen Quartermaster;

        /// <summary>Wayfarer's Road, the free track of the season pass (Docs/DESIGN-WAYFARERS-ROAD.md).
        /// Null unless WayfarersRoadSwitch is on; Show(ScreenId.WayfarersRoad) is then a no-op.</summary>
        public Screens.WayfarersRoadScreen WayfarersRoad;
        public Screens.ArchiveScreen Archive;

        /// <summary>The Store (Docs/DESIGN-STORE.md): the pass, the packs and the shops in one place. Null
        /// unless StoreSwitch is on; Show(ScreenId.Store) is then a no-op.</summary>
        public Screens.StoreScreen Store;

        /// <summary>The weekly and monthly live events (the events section of Docs/DESIGN-STORE.md). Null unless
        /// LiveEventsSwitch is on; Show(ScreenId.LiveEvents) is then a no-op and the Hub's Events door opens the
        /// limited-time events page as it always did.</summary>
        public Screens.LiveEventsScreen LiveEvents;

        /// <summary>The Marketplace (R26): the overview of the four shops. Always built; a shop this build lacks
        /// shows NOT IN THIS BUILD on its card.</summary>
        public Screens.MarketplaceScreen Marketplace;

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
            Marketplace = Create<Screens.MarketplaceScreen>();

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

            // The guild tech tree (claude/DESIGN-GUILD-TECH.md), which replaced the four-node
            // Guild Path. Reached from the formation screen, where the lines its early levels
            // grant are switched, and from the dungeon whose Waymarks buy its rungs.
            GuildTech = Create<Screens.GuildTechScreen>();

            // Fusion and ascension: the one page both halves of DESIGN-FUSION's ladder are spent
            // on. Reached from the hero page - from the cap note of a maxed hero, which is the
            // moment the player is told levelling has stopped - and from the talent page's locked
            // cards, which is the moment they are told what would open one.
            Fusion = Create<Screens.FusionScreen>();

            // One result screen per resolved wave. Reached through CampaignFlow.Launch, never by
            // routing to it directly: it resolves the wave as it opens, so it needs a stage.
            WaveResult = Create<Screens.WaveResultScreen>();

            // The daily orders, orders.json's chests and the login calendar (DESIGN-DAILY-LOOP.md).
            // Built only with the switch on: off, this build has no such screen at all.
            if (Emberfall.Core.Session.DailyLoopSwitch.Enabled) Orders = Create<Screens.OrdersScreen>();

            // The two sink shops (DESIGN-SINKS.md). Built only with the switch on: off, this build
            // has no such screens at all and nothing leads to them.
            if (Emberfall.Core.Session.SinksSwitch.Enabled)
            {
                Armory = Create<Screens.ArmoryScreen>();
                Archive = Create<Screens.ArchiveScreen>();
            }

            // The Quartermaster (Docs/DESIGN-QUARTERMASTER.md). Built only with the switch on: off,
            // this build has no such screen at all and nothing leads to it.
            if (Emberfall.Core.Session.QuartermasterSwitch.Enabled) Quartermaster = Create<Screens.QuartermasterScreen>();

            // Wayfarer's Road (Docs/DESIGN-WAYFARERS-ROAD.md). Built only with the switch on: off, this
            // build has no such screen at all and nothing leads to it.
            if (Emberfall.Core.Session.WayfarersRoadSwitch.Enabled) WayfarersRoad = Create<Screens.WayfarersRoadScreen>();

            // The Store (Docs/DESIGN-STORE.md). Built only with the switch on: off, this build has no such
            // screen at all and no door on the Hub leads to it.
            if (Emberfall.Core.Session.StoreSwitch.Enabled) Store = Create<Screens.StoreScreen>();

            // The live events page. Built only with the switch on: off, this build has no such screen at all and the
            // Hub's Events door opens the page it always opened.
            if (Emberfall.Core.Session.LiveEventsSwitch.Enabled) LiveEvents = Create<Screens.LiveEventsScreen>();
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

        /// <summary>Raised after every screen change, with the screen now showing. The onboarding
        /// coach and the free-play tips listen here rather than in each screen.</summary>
        public event System.Action<ScreenId> Shown;

        public void Show(ScreenId id, bool remember)
        {
            UIScreen next = Find(id);
            if (next == null) return;

            // THE UNLOCK TABLE'S LAST DOOR (DESIGN-ONBOARDING.md). Every screen of a locked
            // feature is refused here, whoever asked: a hub building, a button on another page or
            // a deep link. Each drawn door also checks first, so the player sees its lock before
            // tapping; this is what makes a door nobody drew a lock on safe anyway.
            if (App != null && !FeatureGates.Allows(App, FeatureGates.ScreenEntry(id)))
            {
                FeatureGates.TryEnter(App, FeatureGates.ScreenEntry(id));
                return;
            }

            // A panel or popup is a layer OVER a screen, so a screen change with one still open
            // would draw it over a page it knows nothing about - and, worse, over a page that may
            // be the very screen the panel was hosting. Closing them here rather than at each
            // call site means every door out of the Hub is covered by one rule.
            if (App != null && App.Layers != null) App.Layers.CloseAll();
            if (Current == next)
            {
                next.OnShow();
                if (Shown != null) Shown(id);
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
            if (FeatureGates.NoteOpened(App, id))
            {
                // The first time a newly unlocked feature's door is walked through.
                if (App.Audio != null) App.Audio.Unlock();
                App.RequestSave();
            }
            if (Shown != null) Shown(id);
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

        /// <summary>
        /// The main menu, in one step, with nothing behind it to go back to. Resolves, pays and
        /// saves nothing: it is a screen change and no more.
        /// </summary>
        public void GoHome()
        {
            _history.Clear();
            Show(ScreenId.MainMenu, false);
        }

        private UIScreen Find(ScreenId id)
        {
            for (int i = 0; i < _screens.Count; i++) if (_screens[i].Id == id) return _screens[i];
            return null;
        }
    }
}
