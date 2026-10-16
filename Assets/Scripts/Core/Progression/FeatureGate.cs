using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// WHEN A FEATURE IS OPEN, read from the one table in onboarding.json (DESIGN-ONBOARDING.md,
    /// "Locked, not hidden").
    ///
    /// Every feature is VISIBLE from the first minute; this says only whether it can be entered.
    /// A feature with a stage opens the moment that stage is CLEARED, by id, so a campaign reshape
    /// moves an unlock with its stage rather than with a position. A feature with no stage, a
    /// stage the content does not name, or a database that never loaded the table is open: a
    /// typo in the table must not lock a feature forever, and ContentDatabase.LoadOnboarding warns
    /// for the typo.
    ///
    /// Decided here and nowhere else. The UI asks this through FeatureEntryPoints for every door it
    /// draws, and the harness's economy walks and power instruments ask it for every system they
    /// spend, so a moved row in the table moves all of them together.
    /// </summary>
    public static class FeatureGate
    {
        /// <summary>The stage whose clear opens this feature, or null when it is open from the
        /// start (or no table is loaded, or the row names a stage this content lacks).</summary>
        public static StageDef UnlockStage(ContentDatabase content, Feature feature)
        {
            if (content == null || content.Onboarding == null) return null;
            FeatureUnlockDef row = content.Onboarding.UnlockOf(feature);
            if (row == null || string.IsNullOrEmpty(row.StageId)) return null;
            return content.GetStage(row.StageId);
        }

        public static bool IsUnlocked(ContentDatabase content, PlayerProfile profile, Feature feature)
        {
            StageDef stage = UnlockStage(content, feature);
            if (stage == null) return true;
            return profile != null && profile.HasClearedStage(stage.Id);
        }

        /// <summary>
        /// Whether a player fighting <paramref name="stage"/> for the first time has this feature:
        /// every stage before it cleared, it not. The instruments' question - a power band read at
        /// 1-7 is a line that has fusion (1-6 cleared) and no armor (1-12 not cleared) - asked
        /// without a profile, because the pinned and CEIL lines are built from a stage alone.
        /// </summary>
        public static bool IsUnlockedArriving(ContentDatabase content, StageDef stage, Feature feature)
        {
            StageDef unlock = UnlockStage(content, feature);
            if (unlock == null || stage == null) return true;
            return unlock.GlobalIndex < stage.GlobalIndex;
        }

        /// <summary>The one line a locked feature says, on its tag and when tapped:
        /// "UNLOCKS AT 1-6". Empty for a feature that is open from the start.</summary>
        public static string LockLine(ContentDatabase content, Feature feature)
        {
            StageDef stage = UnlockStage(content, feature);
            return stage == null ? "" : "UNLOCKS AT " + stage.DisplayCode;
        }

        /// <summary>The key a feature's first opening is recorded under in PlayerProfile.SeenTips,
        /// which is what clears its one-time "new" dot.</summary>
        public static string OpenedKey(Feature feature)
        {
            return "open:" + feature;
        }

        /// <summary>
        /// The one-time "new" dot: a feature that HAS an unlock stage, has reached it, and has not
        /// been opened since. A feature open from the start never carries one - there was no
        /// unlock for it to announce.
        /// </summary>
        public static bool ShowsNewDot(ContentDatabase content, PlayerProfile profile, Feature feature)
        {
            if (UnlockStage(content, feature) == null) return false;
            if (!IsUnlocked(content, profile, feature)) return false;
            return !PlayerProfile.HasSeen(profile, OpenedKey(feature));
        }
    }

    /// <summary>One door into a feature: a stable id the UI binds by, the feature behind it, and
    /// where it is drawn, for the harness's report.</summary>
    public sealed class FeatureEntryPoint
    {
        public readonly string Id;
        public readonly Feature Feature;
        public readonly string Where;

        public FeatureEntryPoint(string id, Feature feature, string where)
        {
            Id = id;
            Feature = feature;
            Where = where;
        }
    }

    /// <summary>
    /// EVERY ENTRY POINT TO EVERY FEATURE, in one list (DESIGN-ONBOARDING.md: "gating must cover
    /// every entry point to a feature: hub buildings, bottom nav, hero detail buttons, deep links").
    ///
    /// The UI never names a feature directly. Each door it draws passes its id from this list to
    /// FeatureGates, which looks the feature up here and asks FeatureGate - so a door cannot be
    /// gated against the wrong row. The "screen:" ids are the router's own doors: ScreenRouter.Show
    /// refuses every one of them while locked, which is what covers a door not drawn on any of the
    /// surfaces below, a deep link included (this build has none; the router is where one would
    /// land). DevHarness CheckFeatureUnlockGating proves every entry against the table, that every
    /// non-screen id is bound in the UI source, and that every screen the router owns is either
    /// listed here or named open in OpenScreens.
    /// </summary>
    public static class FeatureEntryPoints
    {
        public static readonly FeatureEntryPoint[] All =
        {
            // The router: every screen of a gated feature.
            new FeatureEntryPoint("screen:Friends", Feature.Friends, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:FriendTeam", Feature.Friends, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Fusion", Feature.Fusion, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Talents", Feature.Talents, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Inventory", Feature.Armor, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Forge", Feature.Forge, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Guild", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildRequests", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildChat", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildDungeon", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildFloor", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildDungeonResult", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildDungeonBoards", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:GuildTech", Feature.Guilds, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Proving", Feature.Proving, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Recital", Feature.Recital, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:RelicShop", Feature.Relics, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Campaign", Feature.Campaign, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Summon", Feature.Summon, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Formation", Feature.Heroes, "ScreenRouter.Show"),
            new FeatureEntryPoint("screen:Heroes", Feature.Heroes, "ScreenRouter.Show"),

            // The Hub's buildings.
            new FeatureEntryPoint("hub.map.HEROES", Feature.Heroes, "MainMenuScreen map"),
            new FeatureEntryPoint("hub.map.SUMMON", Feature.Summon, "MainMenuScreen map"),
            new FeatureEntryPoint("hub.map.FORGE", Feature.Forge, "MainMenuScreen map"),
            new FeatureEntryPoint("hub.map.RELICS", Feature.Relics, "MainMenuScreen map"),
            new FeatureEntryPoint("hub.map.RECITAL", Feature.Recital, "MainMenuScreen map"),
            new FeatureEntryPoint("hub.map.PROVING", Feature.Proving, "MainMenuScreen map"),
            new FeatureEntryPoint("hub.map.CAMPAIGN", Feature.Campaign, "MainMenuScreen map"),

            // The Hub's frame: the bottom nav, the rail, the campaign card, the idle chest.
            new FeatureEntryPoint("hub.nav.Heroes", Feature.Heroes, "MainMenuScreen nav bar"),
            new FeatureEntryPoint("hub.nav.Bag", Feature.Armor, "MainMenuScreen nav bar"),
            new FeatureEntryPoint("hub.nav.Summon", Feature.Summon, "MainMenuScreen nav bar"),
            new FeatureEntryPoint("hub.nav.Guild", Feature.Guilds, "MainMenuScreen nav bar"),
            new FeatureEntryPoint("hub.nav.Campaign", Feature.Campaign, "MainMenuScreen nav bar"),
            new FeatureEntryPoint("hub.rail.Friends", Feature.Friends, "MainMenuScreen rail"),
            new FeatureEntryPoint("hub.card.Campaign", Feature.Campaign, "MainMenuScreen campaign card"),
            new FeatureEntryPoint("hub.chest", Feature.Idle, "MainMenuScreen idle chest"),

            // The panel sheets, which host screens without going through the router.
            new FeatureEntryPoint("panel.profile.FRIENDS", Feature.Friends, "PanelHost profile tabs"),
            new FeatureEntryPoint("panel.guild", Feature.Guilds, "PanelHost guild sheet"),

            // The hero page and the pages under it.
            new FeatureEntryPoint("herodetail.Talents", Feature.Talents, "HeroDetailScreen"),
            new FeatureEntryPoint("herodetail.Fusion", Feature.Fusion, "HeroDetailScreen"),
            new FeatureEntryPoint("herodetail.ArmorSlot", Feature.Armor, "HeroDetailScreen armor card"),
            new FeatureEntryPoint("talents.LockedCard", Feature.Fusion, "TalentsScreen locked card"),
            new FeatureEntryPoint("inventory.tab.RELIC", Feature.Relics, "InventoryScreen rail"),
            new FeatureEntryPoint("formation.GuildTech", Feature.Guilds, "FormationScreen")
        };

        /// <summary>The router's screens that belong to no gated feature: the Hub, the shared
        /// pages every feature reaches through, and the account pages. A screen in neither this
        /// list nor All fails CheckFeatureUnlockGating, so a new screen has to be placed on purpose.</summary>
        public static readonly string[] OpenScreens =
        {
            "MainMenu", "Profile", "HeroDetail", "Battle", "Events", "Account", "Settings", "WaveResult"
        };

        public static FeatureEntryPoint Find(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>The feature a router screen belongs to, by the screen's name, or false for an
        /// open screen.</summary>
        public static bool TryFeatureOfScreen(string screen, out Feature feature)
        {
            FeatureEntryPoint entry = Find("screen:" + screen);
            feature = entry != null ? entry.Feature : Feature.Campaign;
            return entry != null;
        }

        /// <summary>Whether this entry point lets the player through right now. An id this list
        /// does not know is let through, and the harness fails the build for the unknown id.</summary>
        public static bool Allows(ContentDatabase content, PlayerProfile profile, string id)
        {
            FeatureEntryPoint entry = Find(id);
            return entry == null || FeatureGate.IsUnlocked(content, profile, entry.Feature);
        }

        public static List<FeatureEntryPoint> Of(Feature feature)
        {
            List<FeatureEntryPoint> result = new List<FeatureEntryPoint>();
            for (int i = 0; i < All.Length; i++) if (All[i].Feature == feature) result.Add(All[i]);
            return result;
        }
    }
}
