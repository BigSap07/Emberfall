using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.UI
{
    /// <summary>
    /// The whole visual identity in one place: palette, type scale and layout metrics.
    /// Change a value here and every screen follows.
    /// </summary>
    public static class UITheme
    {
        // ---------- palette ----------
        public static readonly Color Backdrop = Hex("0B0813");
        public static readonly Color BackdropDeep = Hex("06040C");
        public static readonly Color Panel = Hex("191325");
        public static readonly Color PanelRaised = Hex("241D36");
        public static readonly Color PanelSunken = Hex("120E1C");
        public static readonly Color Border = Hex("3B3157");
        public static readonly Color BorderSoft = Hex("2A2340");

        public static readonly Color TextPrimary = Hex("F3EEE6");
        public static readonly Color TextMuted = Hex("9A8FB0");
        public static readonly Color TextDim = Hex("6C6382");
        public static readonly Color TextOnAccent = Hex("1B1108");

        public static readonly Color Gold = Hex("F2C14E");
        public static readonly Color GoldDeep = Hex("B8862A");
        public static readonly Color Ember = Hex("E8642F");
        public static readonly Color EmberDeep = Hex("A83C16");
        // The mythic relic grade's badge colour - the tier above legendary/gold, so it is
        // deliberately not gold and not any AspectColor value (a relic badge must never read as an
        // aspect tint). Picked, not derived; a real visual pass in the editor should confirm it
        // reads clearly against Panel and next to Gold rather than clashing with it.
        public static readonly Color Mythic = Hex("E84FD1");
        public static readonly Color Shard = Hex("7FA8FF");
        public static readonly Color ShardDeep = Hex("3B5BB8");
        // Relic Dust's own tone - a warm, desaturated ash, distinct from every aspect accent and
        // from Gold/Shard. Replaces a stray UITheme.Ember tint on the HudTopBar chip: Dust is
        // salvage from broken relics (DESIGN-WORLD, "Relics"), which are explicitly unrelated to the
        // First Light the six aspects came from, so its own currency colour should not borrow one.
        public static readonly Color Dust = Hex("A8967A");

        public static readonly Color Success = Hex("5BC98C");
        public static readonly Color Danger = Hex("E0574C");
        public static readonly Color Warning = Hex("EAA640");
        public static readonly Color Energy = Hex("4FC8F7");
        public static readonly Color HealthAlly = Hex("62C96E");
        public static readonly Color HealthEnemy = Hex("D9504A");
        public static readonly Color Shield = Hex("8FD8FF");

        /// <summary>
        /// The Cinder's own tone (DESIGN-ART-BIBLE section 5): "translucent, almost colorless
        /// material." A cool, heavily desaturated glass-grey, chosen to sit clearly apart from all
        /// six aspect accents (nearest neighbours are Umbra's warm ivory E7E0D0 and Lumen's cobalt
        /// 3568E8; this is neither warm nor saturated) and from Gold, so the Hub's one recurring
        /// motif never reads as a seventh palette entry competing with the six. Used only for the
        /// Hub's own quiet watermark and rule lines - the Cinder never carries an aspect colour at
        /// rest, and no call site anywhere in this project may tint it with one.
        /// </summary>
        public static readonly Color CinderGlass = Hex("BFC3CE");

        // ---------- ink: text edge treatment ----------
        // The near-black a label is outlined or shadowed with to hold it apart from whatever sits
        // behind it. Every one of these was a raw new Color(0,0,0,a) at a call site before, at
        // eleven drifted alphas between 0.5 and 0.95, which is three intentions and eight
        // accidents. Consolidated to the three intentions, at values already in use.
        //
        // Colour only. The DISTANCE argument to Outlined/Shadowed stays at the call site on
        // purpose: it scales with the label's font size, so a shared constant would be wrong at
        // both ends of the type scale. A 20pt status tag wants 1.2 and a 62pt logo wants 5.
        //
        // One coloured (non-black) edge is deliberately NOT here and stays per-screen: the battle
        // result title's. That is an accent belonging to one moment, not a shared treatment. The
        // main menu logo used to have a second one - a warm ember-brown rim, an aspect colour
        // painted onto the one screen the Keeper's fiction says takes no side among the six - and
        // it was removed in the Hub's Cinder theme pass rather than kept here as a second exception.

        /// <summary>Text sitting on a solid UI panel. Quiet, just enough to stop it dissolving.</summary>
        public static readonly Color InkPanel = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>
        /// Text over artwork, a portrait or the battlefield, where the backing is busy and moving.
        ///
        /// THEME: seven of this tier's eight users are in BattleScreen, and they were the highest
        /// alphas in the project before consolidation (0.9 to 0.95) because that text sits over a
        /// painted backdrop and moving characters rather than a flat panel. It is arguable that
        /// the battle screen wants its own tier here, or a different technique entirely (a soft
        /// backing plate behind the text rather than a rim on it, which is what the banner already
        /// does with ScrimBacking). Grouping it with the rest of the UI is the conservative choice
        /// for a migration, not a claim that it is right. Flagged for a human; see
        /// DESIGN-UI-THEME.md.
        /// </summary>
        public static readonly Color InkArt = new Color(0f, 0f, 0f, 0.9f);

        /// <summary>Display text over a dimmed screen, wanting a soft drop shadow rather than a hard rim.</summary>
        public static readonly Color InkDisplay = new Color(0f, 0f, 0f, 0.7f);

        // ---------- scrim: overlays that push content back ----------
        // Unlike Ink above, these are kept at their exact existing values rather than merged into
        // fewer tiers. A full-screen dim is a much more visible surface than a 1.5px text rim, so
        // the same 0.05 rounding that is imperceptible on an outline is not obviously safe here,
        // and the differences between them read as deliberate rather than as drift. Named for
        // intent so they can be tuned as a set later; not re-valued as part of this migration.

        /// <summary>Behind a modal, and over locked content that should read as a shape but not a reveal.</summary>
        public static readonly Color ScrimModal = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>Behind the battle result panel.</summary>
        public static readonly Color ScrimResult = new Color(0f, 0f, 0f, 0.78f);

        /// <summary>Behind a summon reveal, the most opaque of the three: this one is a moment.</summary>
        public static readonly Color ScrimReveal = new Color(0f, 0f, 0f, 0.85f);

        /// <summary>A backing plate under floating text, dark enough to read against, light enough to see through.</summary>
        public static readonly Color ScrimBacking = new Color(0f, 0f, 0f, 0.34f);

        // ---------- interactive: button state ----------
        // These were already central, inside UIBuilder.MakeButton's body, so they were never
        // scattered. They were unnamed though, which meant the one place the whole project's
        // button feel is decided read as five magic numbers in the middle of a construction
        // method. Same values, stated as theme.

        /// <summary>Multiplied over a button's fill on hover. Above 1 to lift it.</summary>
        public static readonly Color ButtonHighlight = new Color(1.08f, 1.08f, 1.08f, 1f);

        /// <summary>Multiplied over a button's fill while held.</summary>
        public static readonly Color ButtonPressed = new Color(0.82f, 0.82f, 0.82f, 1f);

        /// <summary>Multiplied over a button's fill when it cannot be used.</summary>
        public static readonly Color ButtonDisabled = new Color(0.55f, 0.55f, 0.55f, 0.7f);

        /// <summary>The glass highlight across a button's upper half.</summary>
        public static readonly Color ButtonSheen = new Color(1f, 1f, 1f, 0.07f);

        /// <summary>Seconds to cross-fade between button states.</summary>
        public const float ButtonFadeDuration = 0.06f;

        // ---------- type scale ----------
        public const int FontDisplay = 62;
        public const int FontTitle = 44;
        public const int FontHeading = 34;
        public const int FontBody = 28;
        public const int FontSmall = 24;
        public const int FontTiny = 20;

        // ---------- layout ----------
        // Landscape since the 5v5 team size change: ten characters across a battle line need
        // width, and 1080 of portrait width did not carry that read. The Game view in the
        // editor must be set to a 1920x1080 (or 16:9) preset to match - a portrait Game view
        // against this canvas has already produced one apparent layout bug that was really a
        // preview mismatch.
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public const float ScreenPadding = 32f;
        public const float TopBarHeight = 132f;
        public const float NavBarHeight = 168f;
        public const float CornerRadius = 22f;
        public const float CornerRadiusSmall = 14f;

        // ---------- spacing scale ----------
        // Shared gap/padding units - screens should compose layouts from these instead of
        // inventing a new literal per call site. Card/button/portrait sizes stay bespoke;
        // this scale is for the space *between* things.
        public const float Space4 = 4f;
        public const float Space8 = 8f;
        public const float Space12 = 12f;
        public const float Space16 = 16f;
        public const float Space24 = 24f;
        public const float Space32 = ScreenPadding;
        public const float Space48 = 48f;
        public const float Space64 = 64f;
        public const float Space96 = 96f;

        private static Font _font;

        /// <summary>
        /// Unity's built-in font. Deliberately the legacy dynamic font so the project needs
        /// zero asset imports to run - no TMP essentials step, no missing-font pink boxes.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                if (_font == null) Debug.LogWarning("Emberfall: no built-in font available, text will not render.");
                return _font;
            }
        }

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length < 6) return Color.magenta;
            float r = HexPair(hex, 0);
            float g = HexPair(hex, 2);
            float b = HexPair(hex, 4);
            float a = hex.Length >= 8 ? HexPair(hex, 6) : 1f;
            return new Color(r, g, b, a);
        }

        private static float HexPair(string hex, int index)
        {
            int hi = HexDigit(hex[index]);
            int lo = HexDigit(hex[index + 1]);
            return (hi * 16 + lo) / 255f;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        public static Color Shade(Color c, float amount)
        {
            return new Color(c.r * amount, c.g * amount, c.b * amount, c.a);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.Lerp(a, b, t);
        }

        // ---------- game-specific colours ----------

        /// <summary>
        /// Badge tint for a relic's grade. Gold for legendary (the stat-only grade - formerly
        /// labelled Common, values unchanged, only the word moved), the Mythic accent for mythic
        /// (the hook-plus-stats grade above it), and the gap between them is the point: a relic
        /// card shows its grade because it is what tells a player whether they are reading a plain
        /// stat list or a stat list stacked on a unique passive.
        ///
        /// Deliberately not an aspect colour anywhere in this function. A mythic relic is never
        /// flavoured or paired to an aspect, so tinting its badge with one would say the opposite
        /// in the one place a player actually looks.
        /// </summary>
        public static Color RelicGradeColor(RelicGrade grade)
        {
            return grade == RelicGrade.Mythic ? Mythic : Gold;
        }

        /// <summary>The display word, with no noun after it - "Mythic", not "Mythic relic".</summary>
        public static string RelicGradeLabel(RelicGrade grade)
        {
            return grade == RelicGrade.Mythic ? "MYTHIC" : "LEGENDARY";
        }

        /// <summary>
        /// Colour for an armor piece's Temper rung. A four-step ramp from muted to bright, so the
        /// ladder reads as a ladder at a glance in a list of pieces - which is the one thing a
        /// player scanning an inventory needs from it.
        ///
        /// Deliberately shares no colour with RelicGradeColor. Gold means "legendary relic" and
        /// Mythic means "mythic relic" everywhere else in the UI, and a Radiant armor piece is
        /// neither - reusing either would say the two ladders were one ladder, which is exactly the
        /// confusion the Temper naming exists to avoid.
        /// </summary>
        public static Color ArmorTemperColor(ArmorTemper temper)
        {
            switch (temper)
            {
                case ArmorTemper.Rough: return TextDim;
                case ArmorTemper.Honed: return Hex("8FB3C9");
                case ArmorTemper.Gilded: return Hex("6FD0A8");
                case ArmorTemper.Radiant: return Hex("FF9E4A");
            }
            return TextMuted;
        }

        /// <summary>
        /// The six aspect accents. DESIGN-ART-BIBLE section 1 is the specification for these.
        ///
        /// Only Lumen currently matches it. The other five still carry the values this switch was
        /// first written with, and all five differ from the bible - Ember F0743A vs E84532, Tide
        /// 4A9BD8 vs 28C7D9, Verdant 55B96A vs 62A77A, Gale 7FD8E8 vs C6E34A, Umbra 9264E8 vs
        /// E7E0D0. Four of those are shade drift within the same hue family, but Gale's is a hue
        /// change (cyan here, chartreuse in the bible) and Umbra's is a whole family change (purple
        /// here, bone-white there). Reconciling them is a real visual pass with real consequences
        /// for every hero card, armour disc and aspect chip in the game, and it was not this
        /// change's job. Do not read the five unfixed entries as verified against anything.
        /// </summary>
        public static Color AspectColor(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return Hex("F0743A");
                case Aspect.Tide: return Hex("4A9BD8");
                case Aspect.Verdant: return Hex("55B96A");
                case Aspect.Gale: return Hex("7FD8E8");
                case Aspect.Umbra: return Hex("9264E8");
                // Hard cobalt blue, per DESIGN-ART-BIBLE section 1. Was F5E08A, a pale gold that
                // sat close enough to UITheme.Gold (F2C14E) that a Lumen hero's aspect chip and a
                // gold currency figure read as the same colour at a glance - the collision this
                // fix exists to remove, since Gold is a load-bearing semantic colour (currency,
                // progress, primary action) and an aspect accent must never be mistaken for it.
                case Aspect.Lumen: return Hex("3568E8");
            }
            return TextMuted;
        }

        /// <summary>
        /// Color for a hero's star tier. Provides a clear visual hierarchy from 1 to 5.
        /// Stars 4 and 5 reuse the old Rare/Epic colors so existing hero cards read identically
        /// at their new tier; stars 1-3 fill in the lower end of the progression.
        /// </summary>
        public static Color StarColor(int stars)
        {
            switch (stars)
            {
                case 1: return Hex("8FA7C4"); // steel blue
                case 2: return Hex("5EA87A"); // muted green
                case 3: return Hex("7B6BE0"); // purple
                case 4: return Hex("C070E0"); // violet
                case 5: return Hex("F2A93B"); // amber
            }
            return Hex("8FA7C4");
        }

        public static Color RoleColor(HeroRole role)
        {
            switch (role)
            {
                case HeroRole.Bulwark: return Hex("D8A05A");
                case HeroRole.Striker: return Hex("E06A6A");
                case HeroRole.Breaker: return Hex("E88A3C");
                case HeroRole.Mender: return Hex("64C98A");
                case HeroRole.Warden: return Hex("5AA8E0");
                case HeroRole.Stalker: return Hex("A97BE8");
            }
            return TextMuted;
        }

        public static string RoleName(HeroRole role)
        {
            return role.ToString().ToUpperInvariant();
        }

    }
}
