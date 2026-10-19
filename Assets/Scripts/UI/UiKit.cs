using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Json;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>
    /// THE UI KIT'S LOADER (R20): turns a key from UiKitMap into a sprite under Resources/Art/UIKit.
    ///
    /// Every method answers null when UiKitSwitch is off, when the map has no piece for the thing
    /// asked about, and when the file is not there. Null always means "draw what was drawn before",
    /// which is what makes the switch a rollback rather than a second code path: the call sites keep
    /// their old drawing as the fallback and only take the kit when this hands them a sprite.
    ///
    /// A miss is cached as a miss (and reported once), exactly as HudSkin caches its misses, so a
    /// screen that asks for a relic icon per row does not hit Resources.Load per row.
    ///
    /// ABILITY ICONS ARE ONE TEXTURE. The 254 abilities share 109 distinct icons, so the generator packed
    /// them into one atlas (ability_atlas.png) with a map from "hero__ability" to a cell
    /// (ability_atlas_map.json). A key under abilities/ is cut from that texture here, once, with
    /// Sprite.Create, so there is no per-ability file to import and the whole set is one texture in the
    /// player.
    /// </summary>
    public static class UiKit
    {
        /// <summary>True when the kit is on. The one question a call site that must change more than
        /// a sprite (a tint, an ornament, a text colour) asks.</summary>
        public static bool On
        {
            get { return UiKitSwitch.Enabled; }
        }

        private const string AtlasTexture = "Art/UIKit/ability_atlas";
        private const string AtlasMap = "Art/UIKit/ability_atlas_map";   // a TextAsset (.json)

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static Dictionary<string, Rect> _cells;
        private static Texture2D _atlas;

        /// <summary>A sprite by key, or null. A key starting with abilities/ is a cell of the ability atlas.</summary>
        public static Sprite Get(string key)
        {
            return Get(key, true);
        }

        /// <summary>A sprite by key, or null. warn false is for lookups where a miss is ordinary (an ability
        /// the kit has no icon for): no warning, still cached.</summary>
        public static Sprite Get(string key, bool warn)
        {
            if (string.IsNullOrEmpty(key) || !UiKitSwitch.Enabled) return null;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Sprite sprite = key.StartsWith("abilities/")
                ? Cell(key.Substring("abilities/".Length))
                : Resources.Load<Sprite>(UiKitMap.Folder + key);
            if (sprite == null && warn && Reported.Add(key))
                Debug.LogWarning("UiKit: no sprite for key '" + key + "' under Resources/" + UiKitMap.Folder
                                 + ", so this surface draws its normal art.");
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Hud(string slot) { return Get(UiKitMap.HudSlot(slot)); }
        public static Sprite Forge(string slot) { return Get(UiKitMap.ForgeSlot(slot)); }
        public static Sprite Currency(string purse) { return Get(UiKitMap.Currency(purse)); }
        public static Sprite OfferCard(string offerId) { return Get(UiKitMap.OfferCard(offerId)); }
        public static Sprite PassHeader() { return Get(UiKitMap.PassHeader()); }
        public static Sprite Relic(string relicId) { return Get(UiKitMap.RelicIcon(relicId)); }
        public static Sprite Ability(string heroId, string abilityId) { return Get(UiKitMap.AbilityIcon(heroId, abilityId), false); }
        public static Sprite AspectBadge(string aspect) { return Get(UiKitMap.AspectBadge(aspect)); }
        public static Sprite CardFrame(int stars) { return Get(UiKitMap.CardFrame(stars)); }
        public static Sprite CardFrameOpen(int stars) { return Get(UiKitMap.CardFrameOpen(stars)); }

        private static Sprite Cell(string name)
        {
            if (_cells == null) LoadCells();
            Rect rect;
            if (_atlas == null || !_cells.TryGetValue(name, out rect)) return null;
            Sprite sprite = Sprite.Create(_atlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            if (sprite != null) sprite.name = name;
            return sprite;
        }

        private static void LoadCells()
        {
            _cells = new Dictionary<string, Rect>();
            _atlas = Resources.Load<Texture2D>(AtlasTexture);
            TextAsset map = Resources.Load<TextAsset>(AtlasMap);
            if (_atlas == null || map == null) return;
            JsonValue root = JsonValue.Parse(map.text);
            JsonValue cells = root["map"];
            foreach (string name in cells.Keys)
            {
                JsonValue c = cells[name];
                _cells[name] = new Rect(c[0].AsFloat(0f), c[1].AsFloat(0f), c[2].AsFloat(0f), c[3].AsFloat(0f));
            }
        }

        /// <summary>Drops every cached lookup, so a switch flipped while the editor runs is read by
        /// the next screen that builds.</summary>
        public static void Forget()
        {
            Cache.Clear();
            Reported.Clear();
            _cells = null;
            _atlas = null;
        }
    }
}
