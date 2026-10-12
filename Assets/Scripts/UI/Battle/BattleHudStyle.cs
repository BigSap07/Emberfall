using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.UI.Battle
{
    /// <summary>
    /// HOW THE BATTLE HUD LOOKS: every colour, face and sprite slot the bar, the plates and the
    /// two pills draw with. This is the one file an art pass touches. Where things go and how big
    /// they are is BattleHudMetrics' business and BattleHudLayout's, and nothing in either of
    /// those knows what any of it looks like.
    ///
    /// Tokens are taken from UITheme and Emberforge rather than restated, so a palette change in
    /// the kit reaches the battle HUD without a second edit here.
    /// </summary>
    public static class BattleHudStyle
    {
        // ---------------------------------------------------------------- the top bar

        /// <summary>Opaque: the field stops at the bar's lower edge, so there is nothing under it
        /// to show through.</summary>
        public static readonly Color BarFill = Emberforge.PillFill;
        public static readonly Color BarRule = UITheme.WithAlpha(Emberforge.PillInnerLine, 0.85f);
        public static readonly Color StageLine = Emberforge.TextBody;
        public static readonly Color StatusLine = Emberforge.TextQuiet;

        /// <summary>The kit's dark outline, under every label the HUD draws over the field.</summary>
        public static readonly Color TextOutline = Emberforge.Outline;

        // ---------------------------------------------------------------- plates

        public static readonly Color PlateName = Emberforge.TextOnColor;

        /// <summary>The name's outline weight. 2.4 rather than the kit's thin stroke, because the
        /// name sits on a PAINTED backdrop and the bright horizon band of the ashen and proving
        /// fields gives light text almost no separation without it.</summary>
        public const float PlateNameStroke = 2.4f;

        public static readonly Color HpAlly = UITheme.HealthAlly;
        public static readonly Color HpEnemy = UITheme.HealthEnemy;
        public static readonly Color HpTrack = UITheme.BarTrackDeep;
        public static readonly Color HpGhost = UITheme.BarGhostFill;

        /// <summary>The ward draws over the HP bar's own rectangle, so a warded unit is one bar.</summary>
        public static readonly Color Ward = UITheme.WithAlpha(UITheme.Shield, 0.95f);
        public static readonly Color EnergyTrack = UITheme.InkDisplay;
        public static readonly Color Energy = UITheme.Energy;

        /// <summary>Energy lights gold at full: "casts on its next turn" is the state worth seeing
        /// across the field, not the fill level.</summary>
        public static readonly Color EnergyFull = UITheme.Gold;
        public static readonly Color TurnPip = UITheme.Gold;

        // ---------------------------------------------------------------- status icons

        public static readonly Color StatusBuff = UITheme.Success;
        public static readonly Color StatusDebuff = UITheme.Danger;
        public static readonly Color StatusShield = UITheme.Shield;
        public static readonly Color StatusInert = UITheme.TextMuted;
        public static readonly Color StatusDepth = Emberforge.TextOnColor;
        public static readonly Color StatusOverflow = Emberforge.TextBody;

        /// <summary>A dark disc behind each icon, so a green chevron still reads over green grass.</summary>
        public static readonly Color StatusBacking = UITheme.WithAlpha(Emberforge.Outline, 0.72f);
        public static Sprite StatusBackingSprite { get { return TextureLab.Circle(IconTexture); } }

        /// <summary>
        /// The sprite for a status icon, and the tint to draw it with. Three steps, most specific
        /// first, through HudSkin, so an artist reskins by dropping a PNG and nothing else:
        ///
        ///   1. Sprites/Hud/hud_status_&lt;kind&gt;.png, one kind's own art, drawn as authored;
        ///   2. Sprites/Hud/hud_status_&lt;role&gt;.png, the art for every kind in that role;
        ///   3. the drawn placeholder: a green chevron up for a buff, a red chevron down for a
        ///      debuff, the shield glyph for Shield, a grey diamond for the inert bookkeeping kinds.
        ///
        /// Authored art is drawn white, so the PNG's own colours show; the placeholders are white
        /// masks and take the role's colour.
        /// </summary>
        public static Sprite StatusSprite(StatusKind kind, StatusIconRole role, out Color tint)
        {
            string kindSlot = HudSkin.StatusKindSlot(kind);
            if (HudSkin.IsAuthored(kindSlot)) { tint = Color.white; return HudSkin.Icon(kindSlot, null); }

            string roleSlot = RoleSlot(role);
            if (HudSkin.IsAuthored(roleSlot)) { tint = Color.white; return HudSkin.Icon(roleSlot, null); }

            switch (role)
            {
                case StatusIconRole.Debuff: tint = StatusDebuff; return TextureLab.Chevron(IconTexture, false);
                case StatusIconRole.Shield: tint = StatusShield; return TextureLab.Shield(IconTexture);
                case StatusIconRole.Inert: tint = StatusInert; return TextureLab.Diamond(IconTexture);
                default: tint = StatusBuff; return TextureLab.Chevron(IconTexture, true);
            }
        }

        private static string RoleSlot(StatusIconRole role)
        {
            switch (role)
            {
                case StatusIconRole.Debuff: return HudSkin.StatusDebuff;
                case StatusIconRole.Shield: return HudSkin.StatusShield;
                case StatusIconRole.Inert: return HudSkin.StatusInert;
                default: return HudSkin.StatusBuff;
            }
        }

        /// <summary>The placeholder glyphs' texture size: four times the icon, so they stay crisp
        /// at the canvas scale of a 20:9 phone.</summary>
        private const int IconTexture = 64;

        /// <summary>
        /// What goes between the parts of the bar's second line, and between the parts of a
        /// callout. The middle dot U+00B7 is in Barlow Condensed, the face both are set in, and in
        /// Cinzel; if a reskin ever picks a face without it, this is the line to change to "  |  ".
        /// </summary>
        public const string Separator = " · ";

        // ---------------------------------------------------------------- the pills

        /// <summary>The ability's own name, in the kit's gold; the caster after it; PASSIVE quieter.</summary>
        public static readonly Color CalloutAbility = Emberforge.TextGold;
        public static readonly Color CalloutCaster = Emberforge.TextBody;
        public static readonly Color CalloutPassive = Emberforge.TextQuiet;

        public static readonly Color NextLabel = Emberforge.TextQuiet;

        /// <summary>A turn order disc is tinted by side, so a badged ally and a badged enemy still
        /// read apart by the rim the badge leaves showing.</summary>
        public static readonly Color DiscAlly = UITheme.HealthAlly;
        public static readonly Color DiscEnemy = UITheme.HealthEnemy;

        /// <summary>A disc for a unit with no drawn badge shows its initial in this colour.</summary>
        public static readonly Color DiscInitial = Emberforge.TextOnColor;

        /// <summary>Both pills' plate: the kit's dark pill with the gold line inside it.</summary>
        public static Sprite PillPlate { get { return EmberforgeSkin.Get(EmberforgeArt.Slot.Pill); } }

        /// <summary>The ground of a turn order disc, through HudSkin's disc slot.</summary>
        public static Sprite Disc { get { return HudSkin.Circle(HudSkin.Disc, 64); } }

        // ---------------------------------------------------------------- faces

        /// <summary>Every label in the battle HUD is set in the kit's UI face (Barlow Condensed).
        /// The call sites use EmberforgeUI.Label, which is what applies it.</summary>
        public static Font Face { get { return Emberforge.UiFont; } }
    }
}
