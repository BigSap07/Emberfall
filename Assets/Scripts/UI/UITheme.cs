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

        /// <summary>
        /// Slag's own tone: the hot-iron red of a fresh pour. Forge salvage, and until now the one
        /// currency in the game with no colour of its own - ForgeScreen was tinting its Slag banner
        /// with ArmorTemperColor(Gilded), borrowing a rung off the armour ladder for a purse. That
        /// said "Slag is Gilded", which is not a thing, and it is the same class of accident that
        /// put Lumen's old pale gold next to the currency colour.
        ///
        /// Deliberately redder than Ember (E8642F, orange) and less pink than Danger (E0574C), so a
        /// Slag figure is never mistaken for an ember accent or for a failure state.
        /// </summary>
        public static readonly Color Slag = Hex("C9553A");

        /// <summary>
        /// Echoes: the Deep Recital's attempt token, banked to a cap and refilled on a clock.
        ///
        /// Violet rather than the warm brass the mockup's hourglass is painted in, because warm is
        /// already spoken for three times over at this end of the palette - Gold is currency,
        /// Warning is a state, ChromeBrass is structure - and a fourth warm value sitting beside
        /// them is exactly the collision UITheme's own notes keep warning about. The hourglass
        /// SHAPE carries "this one refills with time"; the colour only has to say "this one is the
        /// Recital's", and violet is the Recital's already.
        ///
        /// Distinct from Mythic (E84FD1, magenta - a relic grade) by hue and from Dust (A8967A) by
        /// temperature, so the strip's six icons never read as five.
        /// </summary>
        public static readonly Color Echo = Hex("9B7FE8");

        /// <summary>
        /// The stand-in accent for an event's own currency.
        ///
        /// EventDef carries a CurrencyName but no colour, and events.json is content this pass was
        /// told not to touch, so every event's token renders in this one tone rather than in a
        /// per-event accent. That is a real limitation, not a design choice: two simultaneous
        /// events with currencies would be indistinguishable by colour alone in the strip. It is
        /// survivable today because the currency cell is labelled and only one event carries a
        /// currency at a time; the fix is a colour field on EventDef, which is a content change.
        /// </summary>
        /// <summary>
        /// The Deep Recital's own currency. Warm amber-gold, distinct from Echo (violet, the
        /// attempt gate) by hue, from Gold (the campaign currency) by saturation, and from
        /// Warning (EAA640) by lightness. Coda is the reward, Echo is the cost.
        /// </summary>
        public static readonly Color Coda = Hex("D4A843");

        public static readonly Color EventToken = Hex("D6455E");

        /// <summary>
        /// Waymarks, the guild dungeon's own currency, spent on the Guild Path. A pale jade: the
        /// one hue no other purse holds - distinct from Success (5BC98C) by lightness and
        /// saturation, from Shard (blue) and Energy (cyan) by hue - so a jewel on the Path screen
        /// never reads as a stamina or a victory mark.
        /// </summary>
        public static readonly Color Waymark = Hex("86D4BC");

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
        /// six aspect accents and from Gold, so the Hub's one recurring motif never reads as a
        /// seventh palette entry competing with the six. Used only for the Hub's own quiet
        /// watermark and rule lines - the Cinder never carries an aspect colour at rest, and no
        /// call site anywhere in this project may tint it with one.
        ///
        /// Hue is unchanged from the pass that introduced it. What changed with the Hub's warmed
        /// floor is only how much of it is let through: cool grey at 0.05 and 0.22 alpha was
        /// tuned against a near-black violet backdrop, and on a warmer, lighter floor those same
        /// alphas read as dirt on the panel rather than as a mark. The two Hub call sites now sit
        /// at 0.10 (ambient glow) and 0.32 (watermark). Raising alpha, not saturation, is the
        /// deliberate choice - the Cinder stays colourless.
        /// </summary>
        public static readonly Color CinderGlass = Hex("BFC3CE");

        /// <summary>
        /// Structural chrome only. The duller, cooler, desaturated brass that carries the
        /// gold-accent feel of DesignReference/*.dc.html without taking any of Gold's meaning.
        ///
        /// Allowed: panel borders, tab and section dividers, icon-button outlines, secondary label
        /// text, frame edges - surfaces that delineate the layout and are not themselves the point.
        ///
        /// Forbidden: anything a player acts on or reads as a value. Buttons, currency figures,
        /// progress fills, counts, badges and hover/pressed states are Gold's, and Gold's alone.
        /// The split exists because Gold is doing three jobs at once already (currency, progress,
        /// primary action); a chrome accent close enough to be confused with it would erase all
        /// three at once. If a highlight or hover tint is needed, reuse Gold rather than adding a
        /// third warm constant - an unnecessary fourth warm entry sitting next to Gold is exactly
        /// how Lumen's F5E08A came to collide with the currency colour in the first place.
        ///
        /// Contrast, measured rather than assumed, against the Hub's warmed floor: 4.7:1 on
        /// Backdrop, 4.3:1 on Panel, 3.7:1 on PanelRaised. Comfortable for borders, dividers and
        /// headings at any size, and for secondary label text down to about FontSmall. It is NOT
        /// a safe colour for small body copy on a raised panel - that is what TextMuted is for.
        /// </summary>
        public static readonly Color ChromeBrass = Hex("8A7A52");

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
        // ---------------------------------------------------------------- unit-tag readouts
        // The four values the battle unit tag was drawing at raw literals. Named at their EXACT
        // current values, deliberately: this file's own migration rule is to move numbers, not
        // change them, and the same rule is why CampaignScreen's locked hatch is still a literal.
        // Nothing here is a restyle; it is the same pixels with a name a reader can follow.

        /// <summary>Track behind the health bar's ghost (the drain afterimage). Darker than
        /// InkPanel because it sits over the battlefield, lighter than InkArt because a bar is a
        /// solid shape rather than text needing to survive a moving backdrop.</summary>
        public static readonly Color BarTrackDeep = new Color(0f, 0f, 0f, 0.8f);

        /// <summary>The drain afterimage itself: white, half strength, so it reads as the health
        /// that WAS there rather than as a second health bar.</summary>
        public static readonly Color BarGhostFill = new Color(1f, 1f, 1f, 0.5f);

        /// <summary>A bar with no track of its own, layered over one that has its own backing.
        /// Color.clear by another name, and named so "no track" reads as a decision.</summary>
        public static readonly Color BarTrackNone = new Color(0f, 0f, 0f, 0f);

        /// <summary>Warm-dark outline for the result title. Not pure black - it carries a little
        /// of the Ember red so a VICTORY or DEFEAT title sits in the game's own palette rather
        /// than being knocked out in neutral ink.</summary>
        public static readonly Color InkTitle = new Color(0.2f, 0.05f, 0f, 0.95f);

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
        /// <summary>
        /// THE COLOUR OF A UNIT THAT HAS NOT BEEN DRAWN YET - on the battlefield mannequin and on
        /// its portrait alike, so the two read as the same absent character rather than as two
        /// different stand-ins.
        ///
        /// The aspect colour lightened toward white, not the aspect colour itself. The mannequin is
        /// drawn in flat near-white values and its material multiplies, so a fully saturated hue
        /// lands at full strength: an Umbra or Lumen placeholder (9264E8, 3568E8) would come out a
        /// dark solid shape on a dark battlefield, and the small value steps that separate its
        /// head, torso and limbs would stop reading. Lifting toward white keeps the aspect legible
        /// while leaving the figure light enough to be a silhouette.
        ///
        /// 0.3 for no better reason than that it is where the darkest aspect stops disappearing and
        /// the brightest has not yet gone pastel. It is a look; this is the place to change it.
        /// </summary>
        public static Color PlaceholderTint(Aspect aspect)
        {
            return Color.Lerp(AspectColor(aspect), Color.white, 0.3f);
        }

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
        /// The six aspect accents EXACTLY as DESIGN-ART-BIBLE section 1 specifies them.
        ///
        /// This exists because AspectColor above does NOT match the bible for five of six aspects,
        /// and its own doc comment says so: Gale is a different hue there and Umbra a different
        /// colour family entirely. Reconciling AspectColor is a project-wide visual pass touching
        /// every hero card, armour disc and aspect chip in the game, which is not this pass's job
        /// and would have silently restyled six screens this pass was told not to touch.
        ///
        /// So: screens that have had their visual pass call THIS. Screens still awaiting one keep
        /// calling AspectColor. That is a fork, and a fork in exactly the place this project has
        /// already been burned once - two portrait conventions sat side by side undetected until an
        /// audit found them - so it is written down here rather than left to be discovered. The two
        /// must converge, by moving AspectColor onto these values in a pass that owns every screen
        /// at once. Until then, do not add a third set, and do not "fix" one call site to point at
        /// the other in isolation.
        /// </summary>
        public static Color AspectAccent(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return Hex("E84532");
                case Aspect.Tide: return Hex("28C7D9");
                case Aspect.Verdant: return Hex("62A77A");
                case Aspect.Gale: return Hex("C6E34A");
                case Aspect.Umbra: return Hex("E7E0D0");
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

        /// <summary>
        /// A colour per role, PAIRED: the two halves of each family sit next to each other on the
        /// wheel so a chip reads as "a tank" or "a healer" before it reads as which one. The
        /// second of each pair is the cooler, slightly desaturated sibling.
        ///
        /// Every one of the ten is named. The `TextMuted` fallthrough below used to be reachable -
        /// Ranger had no case and rendered grey - and is now unreachable by construction, kept only
        /// so adding a role without a colour degrades to a legible chip instead of failing to
        /// compile in a UI file nobody is looking at.
        /// </summary>
        public static Color RoleColor(HeroRole role)
        {
            switch (role)
            {
                case HeroRole.Bulwark:  return Hex("D8A05A");   // amber, the line
                case HeroRole.Vanguard: return Hex("C07A46");   // deeper amber, the line that hits back
                case HeroRole.Breaker:  return Hex("E88A3C");   // orange, area damage
                case HeroRole.Binder:   return Hex("C9702E");   // burnt orange, control
                case HeroRole.Mender:   return Hex("64C98A");   // green, healing
                case HeroRole.Warden:   return Hex("5AA8E0");   // blue, support
                case HeroRole.Marksman: return Hex("D9C46A");   // pale gold, ranged single-target
                case HeroRole.Tinkerer: return Hex("8FBF9E");   // sage, ranged utility
                case HeroRole.Striker:  return Hex("E06A6A");   // red, the finisher
                case HeroRole.Stalker:  return Hex("A97BE8");   // violet, the assassin
            }
            return TextMuted;
        }

        public static string RoleName(HeroRole role)
        {
            return role.ToString().ToUpperInvariant();
        }

    }
}
