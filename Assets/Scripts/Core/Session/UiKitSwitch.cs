using System;
using System.Collections.Generic;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE UI KIT SWITCH (R20, default flipped in R26): the generated art kit that skins the Hub, the Battle
    /// HUD, the Store, the Hero screen, Victory and Summon. ON by default; define EMBERFALL_UIKIT_OFF to
    /// build the old look (the same opt-out shape as EMBERFALL_STORE_OFF and EMBERFALL_SINKS_OFF). The
    /// field is writable so the harness and the capture targets can force it; whatever sets it puts it
    /// back. ShippedDefault is what this compile ships, so a capture restores to it and not to a literal.
    ///
    /// OFF IS THE OLD LOOK, EXACTLY. Every lookup in UiKitMap answers null while this is off, and every
    /// call site treats null as "draw what was always drawn", so with the switch off no kit sprite is
    /// loaded, no tint changes, no ornament is skipped and no layout moves. The switch is a presentation
    /// preference of the build: it reads nothing from a save, writes nothing to one, and never reaches
    /// a fight.
    /// </summary>
    public static class UiKitSwitch
    {
#if EMBERFALL_UIKIT_OFF
        public static readonly bool ShippedDefault = false;
#else
        public static readonly bool ShippedDefault = true;
#endif

        public static bool Enabled = ShippedDefault;
    }

    /// <summary>
    /// WHICH KIT PIECE DRAWS WHICH SURFACE, as plain strings, so the harness can prove every mapping
    /// and the rollback without an engine. Every method returns null while UiKitSwitch is off, and null
    /// also means "no piece for this one": the caller then draws what it drew before. A key is a path
    /// under Resources/Art/UIKit/ without the extension; UiKit (the engine half) loads it.
    ///
    /// The tables are the single place a slot or an id meets a piece. Nothing else in the UI names a kit
    /// file, so a swap is one line here.
    /// </summary>
    public static class UiKitMap
    {
        /// <summary>Resources path of the folder every key lives under.</summary>
        public const string Folder = "Art/UIKit/";

        /// <summary>Resources path of the ability atlas map (a .json): "hero__ability" to its cell in
        /// ability_atlas.png. The 254 abilities share 109 icons, so they are one texture, not 254 files.</summary>
        public const string AtlasMapPath = "Art/UIKit/ability_atlas_map";

        // HudSkin slot names (the Hub's frame) to a kit piece.
        private static readonly Dictionary<string, string> HudSlots = new Dictionary<string, string>
        {
            { "hud_nav_heroes", "nav_heroes" },
            { "hud_nav_bag", "nav_relics" },
            { "hud_nav_summon", "nav_summon" },
            { "hud_nav_guild", "nav_guild" },
            { "hud_nav_campaign", "nav_campaign" },
            { "hud_nav_store", "nav_store" },
            { "hud_nav_pass", "nav_pass" },
            { "hud_nav_forge", "nav_forge" },
            { "hud_rail_more", "nav_settings" },
            { "hud_chest", "chest" },
            { "hud_close", "close_x" },
        };

        // EmberforgeArt slot names (the shared surfaces) to a kit piece. A slot that is not here keeps
        // its baked sprite when the kit is on.
        private static readonly Dictionary<string, string> ForgeSlots = new Dictionary<string, string>
        {
            { "forge_panel", "panel_large" },
            { "forge_panel_plain", "panel_small" },
            { "forge_btn_primary", "btn_primary" },
            { "forge_btn_primary_down", "btn_primary" },
            { "forge_btn_secondary", "btn_secondary" },
            { "forge_btn_secondary_down", "btn_secondary" },
            { "forge_btn_disabled", "btn_disabled" },
            // The generic pill (battle callout, turn order, the chest's bubble) is the kit's dark plate
            // with the gold line. The currency pill's own plate has a coin socket painted into its left
            // end, so only CurrencyPill asks for it, by the name below.
            { "forge_pill", "btn_secondary" },
            { "forge_pill_currency", "pill_currency" },
            // R26: the filigree corner (top-left; the UI mirrors it into the other three). Drawn by
            // Tools/uikit-polish-gen.py. With the kit off the slot keeps its baked diamond.
            { "forge_ornament", "corner_ornament" },
            { "forge_tab_on", "tab_on" },
            { "forge_tab_off", "tab_off" },
            { "forge_disc", "btn_round" },
            { "forge_disc_down", "btn_round" },
        };

        // Currency icons by the name the code uses for the purse.
        private static readonly Dictionary<string, string> Currencies = new Dictionary<string, string>
        {
            { "gold", "gold" },
            { "shards", "shards" },
            { "dust", "dust" },
            { "valor", "valor" },
            { "coda", "coda" },
            { "slag", "slag" },
            { "energy", "energy" },
            { "echo", "hourglass" },
        };

        // Store offer ids (StoreOffers in the Store) to a card.
        private static readonly Dictionary<string, string> OfferCards = new Dictionary<string, string>
        {
            { "starter_pact", "store/offer_starter_pact" },
            { "weekly_pack", "store/offer_weekly" },
            { "monthly_pack", "store/offer_monthly" },
            { "ember_ledger", "store/offer_ember_ledger" },
            { "growth_fund", "store/offer_growth_fund" },
            { "pass_premium", "store/offer_pass_premium" },
        };

        public static string HudSlot(string slot)
        {
            return Look(HudSlots, slot);
        }

        public static string ForgeSlot(string slot)
        {
            return Look(ForgeSlots, slot);
        }

        public static string Currency(string purse)
        {
            return Look(Currencies, purse == null ? null : purse.ToLowerInvariant());
        }

        public static string OfferCard(string offerId)
        {
            return Look(OfferCards, offerId);
        }

        /// <summary>The header on the Pass tab.</summary>
        public static string PassHeader()
        {
            return UiKitSwitch.Enabled ? "store/header_wayfarers_road" : null;
        }

        /// <summary>The boot splash, the key art shown over the Hub for a moment when the app starts.</summary>
        public static string Splash()
        {
            return UiKitSwitch.Enabled ? "splash" : null;
        }

        /// <summary>One icon per relic id in relics.json.</summary>
        public static string RelicIcon(string relicId)
        {
            if (!UiKitSwitch.Enabled || string.IsNullOrEmpty(relicId)) return null;
            return "relics/" + relicId;
        }

        /// <summary>An ability's icon, whatever the slot (basic, active, passive1). The engine half cuts it
        /// from the ability atlas, so a shared icon is one cell.</summary>
        public static string AbilityIcon(string heroId, string abilityId)
        {
            if (!UiKitSwitch.Enabled || string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(abilityId)) return null;
            return "abilities/" + heroId + "__" + abilityId;
        }

        /// <summary>The aspect badge for an aspect name (Ember, Tide, Verdant, Gale, Umbra, Lumen).</summary>
        public static string AspectBadge(string aspect)
        {
            if (!UiKitSwitch.Enabled || string.IsNullOrEmpty(aspect)) return null;
            return "aspect_" + aspect.ToLowerInvariant();
        }

        /// <summary>The same frame with its centre cut out, for drawing over a portrait.</summary>
        public static string CardFrameOpen(int stars)
        {
            string key = CardFrame(stars);
            return key == null ? null : key + "_open";
        }

        /// <summary>The kit's card frame for a star tier (1 to 5), the backing under a portrait.</summary>
        public static string CardFrame(int stars)
        {
            if (!UiKitSwitch.Enabled) return null;
            int tier = stars < 1 ? 1 : (stars > 5 ? 5 : stars);
            return "card_frame_" + tier;
        }

        /// <summary>Every key a table can answer with, for the harness's file check. Reads the tables
        /// directly, so it answers whatever the switch says.</summary>
        public static List<string> AllTableKeys()
        {
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, string> kv in HudSlots) if (!keys.Contains(kv.Value)) keys.Add(kv.Value);
            foreach (KeyValuePair<string, string> kv in ForgeSlots) if (!keys.Contains(kv.Value)) keys.Add(kv.Value);
            foreach (KeyValuePair<string, string> kv in Currencies) if (!keys.Contains(kv.Value)) keys.Add(kv.Value);
            foreach (KeyValuePair<string, string> kv in OfferCards) if (!keys.Contains(kv.Value)) keys.Add(kv.Value);
            string[] aspects = { "ember", "tide", "verdant", "gale", "umbra", "lumen" };
            for (int i = 0; i < aspects.Length; i++) keys.Add("aspect_" + aspects[i]);
            for (int i = 1; i <= 5; i++) { keys.Add("card_frame_" + i); keys.Add("card_frame_" + i + "_open"); }
            keys.Add("store/header_wayfarers_road");
            keys.Add("splash");
            return keys;
        }

        /// <summary>The slot names the tables are keyed by, for the harness's rollback sweep.</summary>
        public static List<string> AllHudSlots()
        {
            return new List<string>(HudSlots.Keys);
        }

        public static List<string> AllForgeSlots()
        {
            return new List<string>(ForgeSlots.Keys);
        }

        public static List<string> AllOfferIds()
        {
            return new List<string>(OfferCards.Keys);
        }

        public static List<string> AllCurrencies()
        {
            return new List<string>(Currencies.Keys);
        }

        private static string Look(Dictionary<string, string> table, string name)
        {
            if (!UiKitSwitch.Enabled || string.IsNullOrEmpty(name)) return null;
            string key;
            return table.TryGetValue(name, out key) ? key : null;
        }
    }
}
