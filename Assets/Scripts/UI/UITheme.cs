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
        public static readonly Color Shard = Hex("7FA8FF");
        public static readonly Color ShardDeep = Hex("3B5BB8");

        public static readonly Color Success = Hex("5BC98C");
        public static readonly Color Danger = Hex("E0574C");
        public static readonly Color Warning = Hex("EAA640");
        public static readonly Color Energy = Hex("4FC8F7");
        public static readonly Color HealthAlly = Hex("62C96E");
        public static readonly Color HealthEnemy = Hex("D9504A");
        public static readonly Color Shield = Hex("8FD8FF");

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

        public static Color AspectColor(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return Hex("F0743A");
                case Aspect.Tide: return Hex("4A9BD8");
                case Aspect.Verdant: return Hex("55B96A");
                case Aspect.Gale: return Hex("7FD8E8");
                case Aspect.Umbra: return Hex("9264E8");
                case Aspect.Lumen: return Hex("F5E08A");
            }
            return TextMuted;
        }

        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return Hex("7B6BE0");
                case Rarity.Epic: return Hex("F2A93B");
            }
            return Hex("8FA7C4");
        }

        public static string RarityName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return "RARE";
                case Rarity.Epic: return "EPIC";
            }
            return "COMMON";
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

        public static string Stars(Rarity rarity)
        {
            int count = (int)rarity;
            string s = "";
            for (int i = 0; i < count; i++) s += "*";
            return s;
        }
    }
}
