using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.UI
{
    /// <summary>
    /// THE ONLY PLACE ART ENTERS THE HUD, and the reason no HUD element hard-codes a sprite.
    ///
    /// Every element of the Hub's frame - the rail buttons, the nav cells, the chest, the bubble,
    /// the panel sheet, the popup plate, the pending dot - asks this class for a NAMED SLOT and
    /// draws whatever comes back. A slot resolves in two steps:
    ///
    ///   1. Resources.Load a sprite at Sprites/Hud/&lt;slot&gt;. If a file is there, that is the art.
    ///   2. Otherwise fall back to the procedural shape this project already draws in TextureLab.
    ///
    /// So a re-skin is a folder of PNGs, not a code change: dropping
    /// Assets/Resources/Sprites/Hud/hud_nav_heroes.png into the project replaces that one icon,
    /// on every aspect, with no rebuild of the layout and nothing recompiled. Delete it again and
    /// the procedural shape comes back. Nothing in this file knows what any slot LOOKS like, and
    /// no call site outside it names a TextureLab shape for a HUD element.
    ///
    /// WHY THIS RATHER THAN UNITY PREFABS. This project builds its whole UI in code from a
    /// deliberately near-empty scene - see CLAUDE.md - so there is no prefab asset anywhere in it
    /// to hang a sprite slot off. The factory methods in HudWidgets are this project's prefabs:
    /// one function per element, called from one place, with its art exposed here by name. An
    /// artist changes the art without opening a single .cs file, which is the property a prefab
    /// with exposed sprite slots was asked for.
    ///
    /// A LOADED SPRITE IS USED EXACTLY AS AUTHORED. A nine-sliced plate needs its borders set on
    /// the importer, the same as any other sliced sprite in Unity; this class does not invent
    /// them. The procedural fallbacks are already sliced correctly, so the untouched project
    /// looks right and an authored replacement looks like what the artist drew.
    /// </summary>
    public static class HudSkin
    {
        /// <summary>Where an authored override lives. Under Resources so it ships inside the APK
        /// without an addressable or a bundle, which is how every other sprite in this game is
        /// loaded.</summary>
        public const string Folder = "Sprites/Hud/";

        // ------------------------------------------------------------------ slot names
        //
        // Named constants rather than string literals at the call sites, so the set of slots an
        // artist can fill is enumerable from one place and a typo is a compile error rather than
        // a silently-ignored PNG.

        public const string PlateSheet = "hud_sheet";
        public const string PlatePopup = "hud_popup";
        public const string PlatePanel = "hud_panel";
        public const string PlateBubble = "hud_bubble";
        public const string PlateTab = "hud_tab";

        public const string Disc = "hud_disc";
        public const string DiscRing = "hud_ring";
        public const string BadgeDot = "hud_badge_dot";

        public const string IconFriends = "hud_rail_friends";
        public const string IconMail = "hud_rail_mail";
        public const string IconEvents = "hud_rail_events";
        public const string IconMore = "hud_rail_more";

        public const string IconHeroes = "hud_nav_heroes";
        public const string IconBag = "hud_nav_bag";
        public const string IconSummon = "hud_nav_summon";
        public const string IconGuild = "hud_nav_guild";
        public const string IconCampaign = "hud_nav_campaign";

        public const string IconChest = "hud_chest";
        public const string IconClose = "hud_close";

        /// <summary>The battle plates' status icons, one slot per role. A kind's own slot
        /// (StatusKindSlot) is tried before its role's; see BattleHudStyle.StatusSprite.</summary>
        public const string StatusBuff = "hud_status_buff";
        public const string StatusDebuff = "hud_status_debuff";
        public const string StatusShield = "hud_status_shield";
        public const string StatusInert = "hud_status_inert";

        /// <summary>One status kind's own icon slot, hud_status_&lt;kind&gt; in lower case, so
        /// hud_status_burn.png reskins Burn alone and leaves every other debuff on its role's art.</summary>
        public static string StatusKindSlot(StatusKind kind)
        {
            return "hud_status_" + kind.ToString().ToLowerInvariant();
        }

        // ------------------------------------------------------------------ resolution

        /// <summary>
        /// Cached per slot, INCLUDING the misses. Resources.Load on a path with no asset behind it
        /// is not free, and every one of these is asked for while a screen builds, so a null is
        /// remembered exactly like a hit.
        /// </summary>
        private static readonly Dictionary<string, Sprite> Overrides = new Dictionary<string, Sprite>();

        private static Sprite Authored(string slot)
        {
            Sprite found;
            if (Overrides.TryGetValue(slot, out found)) return found;
            found = Resources.Load<Sprite>(Folder + slot);
            Overrides[slot] = found;
            return found;
        }

        /// <summary>Drops every cached lookup, so a sprite imported while the editor is running is
        /// picked up by the next screen that builds rather than after a domain reload.</summary>
        public static void Forget()
        {
            Overrides.Clear();
        }

        /// <summary>True when an artist has supplied this slot. Only useful for reporting.</summary>
        public static bool IsAuthored(string slot)
        {
            return Authored(slot) != null;
        }

        // ------------------------------------------------------------------ the slots themselves

        /// <summary>A nine-sliced plate: panels, sheets, bubbles, tabs.</summary>
        public static Sprite Plate(string slot, int radius)
        {
            Sprite authored = Authored(slot);
            return authored != null ? authored : TextureLab.RoundedRect(radius);
        }

        /// <summary>A filled circle: the ground under a rail or nav glyph.</summary>
        public static Sprite Circle(string slot, int size)
        {
            Sprite authored = Authored(slot);
            return authored != null ? authored : TextureLab.Circle(size);
        }

        /// <summary>A ring: the warm rim around a circular button.</summary>
        public static Sprite Ring(string slot, int size, int thickness)
        {
            Sprite authored = Authored(slot);
            return authored != null ? authored : TextureLab.Ring(size, thickness);
        }

        /// <summary>
        /// An icon glyph. The fallback is passed in by the caller rather than switched on here,
        /// because the procedural shapes live in TextureLab and this class's job is to decide
        /// WHETHER to use one, not to own the drawing of it.
        /// </summary>
        public static Sprite Icon(string slot, Sprite fallback)
        {
            Sprite authored = Authored(slot);
            return authored != null ? authored : fallback;
        }
    }
}
