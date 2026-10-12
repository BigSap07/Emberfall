using UnityEngine;

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

        /// <summary>
        /// What goes between the parts of the bar's second line, and between the parts of a
        /// callout. The middle dot U+00B7 is in Barlow Condensed, the face both are set in, and in
        /// Cinzel; if a reskin ever picks a face without it, this is the line to change to "  |  ".
        /// </summary>
        public const string Separator = " · ";

        // ---------------------------------------------------------------- faces

        /// <summary>Every label in the battle HUD is set in the kit's UI face (Barlow Condensed).
        /// The call sites use EmberforgeUI.Label, which is what applies it.</summary>
        public static Font Face { get { return Emberforge.UiFont; } }
    }
}
