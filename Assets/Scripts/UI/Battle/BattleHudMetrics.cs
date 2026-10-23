namespace Emberfall.UI.Battle
{
    /// <summary>
    /// EVERY SIZE AND GAP IN THE BATTLE HUD, in canvas units against the 1920x1080 reference.
    ///
    /// The battle screen lays its HUD out from these numbers and nothing else, and so does
    /// Tools/DevHarness (BattleHudChecks), which builds every plate, both pills and the top bar
    /// through BattleHudLayout at three phone aspects and proves none of them overlap. A number
    /// moved here moves both, which is the point of the file: a layout that is checked against a
    /// copy of its numbers is checked against nothing.
    ///
    /// Deliberately free of UnityEngine, so the harness compiles it. Colours, fonts and sprite
    /// slots are BattleHudStyle's; this file never holds one.
    /// </summary>
    public static class BattleHudMetrics
    {
        // ---------------------------------------------------------------- the canvas

        /// <summary>UITheme.ReferenceResolution. Restated because UITheme is engine-side; the
        /// harness reads both files and fails if the two ever disagree.</summary>
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>The root canvas's CanvasScaler matchWidthOrHeight, set in GameApp. Asserted
        /// against that file by the harness for the same reason.</summary>
        public const float CanvasMatch = 0.5f;

        // ---------------------------------------------------------------- the top bar

        /// <summary>The bar's height below the top of the safe area. The battle camera's viewport
        /// stops at its lower edge, so nothing on the field is ever drawn under it.</summary>
        public const float BarHeight = 100f;

        /// <summary>The rule along the bar's lower edge, inside its height.</summary>
        public const float BarRuleHeight = 2f;

        /// <summary>Where the two text lines start while HOME is up: just past its hit rect, which
        /// is HubStyle.TouchMin wide, plus a gap.</summary>
        public const float BarLabelInsetWithHome = 112f;
        public const float BarLabelInset = 4f;

        public const float BarLabelWidth = 900f;
        public const float BarStageLineTop = 20f;
        public const float BarStageLineHeight = 30f;
        public const int BarStageLineFont = 24;
        public const float BarStatusLineTop = 54f;
        public const float BarStatusLineHeight = 26f;
        public const int BarStatusLineFont = 20;

        /// <summary>HOME's hit rect is HubStyle.TouchMin (108) square, so it is placed 4 above
        /// the bar's top to sit centred on a 100-tall bar. Its visible disc is 84.</summary>
        public const float BarHomeTop = 4f;

        public const float BarButtonHeight = 56f;
        public const float BarSpeedWidth = 84f;
        public const float BarSkipWidth = 100f;
        public const float BarAutoWidth = 120f;
        public const float BarButtonGap = 8f;
        public const int BarButtonFont = 20;

        // ---------------------------------------------------------------- one unit's plate
        //
        // The plate hangs UNDER the unit's feet. Below the feet: a gap, the name on one line, a
        // gap, the HP bar, a hairline, the energy bar. Above the feet: the status icon row, empty
        // at rest. The turn cue lifts the whole plate by PlateTurnLift while the unit acts.

        public const float PlateNameGap = 2f;
        public const float PlateNameHeight = 18f;
        public const int PlateNameFont = 16;
        public const int PlateNameFontMin = 11;
        public const float PlateNameToHp = 2f;
        public const float PlateHpHeight = 10f;
        public const float PlateHpToEnergy = 1f;
        public const float PlateEnergyHeight = 4f;

        /// <summary>How far each bar's fill sits inside its track. One unit, so a 4-tall energy
        /// bar still shows a 2-tall fill; UIBuilder.Bar's default of 2 would leave it none.</summary>
        public const float PlateBarFillInset = 1f;

        /// <summary>Width of the HP and energy bars. The name may run wider, up to PlateNameWidth.</summary>
        public const float PlateBarWidth = 160f;
        public const float PlateNameWidth = 240f;

        public const float PlateTurnPipSize = 10f;

        /// <summary>The turn pip's gap to the left end of the HP bar.</summary>
        public const float PlateTurnPipGap = 8f;

        public const float PlateIconGap = 2f;
        public const float PlateIconSize = 16f;
        public const float PlateIconSpacing = 3f;
        public const int PlateIconDepthFont = 11;
        public const int PlateIconOverflowFont = 14;
        public const float PlateIconOverflowWidth = 26f;

        /// <summary>At most this many status icons; past it, "+N" counts the kinds not shown.</summary>
        public const int PlateMaxIcons = 3;

        /// <summary>How far the turn cue raises the acting unit's plate.</summary>
        public const float PlateTurnLift = 14f;

        /// <summary>Everything under the feet: 2 + 18 + 2 + 10 + 1 + 4 = 37.</summary>
        public const float PlateBelowFeet = PlateNameGap + PlateNameHeight + PlateNameToHp
                                          + PlateHpHeight + PlateHpToEnergy + PlateEnergyHeight;

        /// <summary>Everything over the feet at rest: the icon row, 2 + 16 = 18.</summary>
        public const float PlateAboveFeet = PlateIconGap + PlateIconSize;

        /// <summary>The smallest gap allowed between two plates' boxes, and between a plate and
        /// the safe area's bottom edge.</summary>
        public const float PlateMinClearance = 8f;

        // ---------------------------------------------------------------- the plate over the head
        //
        // In the formation (FormationLayout) the plate stands on the unit's HEAD instead of hanging
        // under its feet: from the bottom up the energy bar, the HP bar, the name, with the status
        // icons beside the bars. The widget is the same; only where its root goes differs.

        /// <summary>The gap between the top of the head and the bottom of the energy bar.</summary>
        public const float PlateHeadGap = 4f;

        /// <summary>The whole stack over the gap: energy 4, hairline 1, HP 10, gap 2, name 18 = 35.</summary>
        public const float PlateStackHeight = PlateEnergyHeight + PlateHpToEnergy + PlateHpHeight
                                            + PlateNameToHp + PlateNameHeight;

        /// <summary>Whether the plate over a head carries the unit's name. DEFAULT TAKEN: no. The target
        /// shows the two bars alone over each head, and the name over them would cost 20 canvas units of
        /// height that the rows 0.05 of the height apart cannot spare. The widget still builds the name
        /// (UnitPlate) and hides it; flip this to bring it back and the harness re-proves the boxes.</summary>
        public static readonly bool PlateNameOverHead = false;

        /// <summary>Whether the turn pip (the dot beside the HP bar) is drawn over a head. DEFAULT TAKEN: no. The
        /// captures showed it as a small yellow dot that was hard to read and crowded the next plate; the turn cue
        /// over a head is the plate's lift (PlateTurnLift). The inert bookkeeping status kinds (the grey diamond) are
        /// left off the plate over a head for the same reason (BattleScreen.RefreshStatusText).</summary>
        public static readonly bool PlateTurnPipOverHead = false;

        /// <summary>The two bars alone: energy 4, hairline 1, HP 10 = 15.</summary>
        public const float PlateBarsHeight = PlateEnergyHeight + PlateHpToEnergy + PlateHpHeight;

        /// <summary>The icon row sits right of the bars, this far from the HP bar's end.</summary>
        public const float PlateIconsInset = 6f;

        /// <summary>Over a head the icon row reaches toward the centre, where the next plate may be only
        /// a few units away (a boss's plate sits higher than its neighbours'), so it holds two icons and
        /// the "+N" for the rest rather than three: a plate's whole width is what the harness keeps clear.</summary>
        public const int PlateMaxIconsOverHead = 2;

        /// <summary>The icons, the "+N" and the spacing: 2 x 16 + 26 + 2 x 3 = 64.</summary>
        public const float PlateIconsWidth = PlateMaxIconsOverHead * PlateIconSize + PlateIconOverflowWidth
                                           + PlateMaxIconsOverHead * PlateIconSpacing;

        // ---------------------------------------------------------------- the callout pill

        /// <summary>The ability callout hangs this far under the top bar, centred on the screen.</summary>
        public const float CalloutGap = 8f;
        public const float CalloutHeight = 40f;
        public const float CalloutMaxWidth = 640f;
        public const float CalloutPadding = 24f;
        public const int CalloutFont = 20;

        // ---------------------------------------------------------------- the NEXT pill

        /// <summary>The turn order pill's bottom edge, above the safe area's bottom.</summary>
        public const float NextBottom = 12f;
        public const float NextHeight = 52f;
        public const float NextMaxWidth = 440f;
        public const float NextPadding = 10f;

        /// <summary>The acting unit's disc fills the pill's height; the ones to come are smaller.</summary>
        public const float NextActingDisc = 52f;
        public const float NextUpcomingDisc = 34f;
        public const float NextDiscGap = 6f;
        public const float NextLabelWidth = 50f;
        public const int NextLabelFont = 18;

        /// <summary>The most upcoming units the pill shows.</summary>
        public const int NextMaxUpcoming = 6;

        /// <summary>The badge sits this far inside its disc, so the side's tint shows as a rim.</summary>
        public const float NextDiscRim = 3f;

        // ---------------------------------------------------------------- floating numbers

        /// <summary>A number spawns this far above the head it belongs to.</summary>
        public const float FloatSpawnAboveHead = 10f;

        /// <summary>A number's climb stops when its centre is this far, plus half a line, below
        /// the bar.</summary>
        public const float FloatStopBelowBar = 30f;
        public const float FloatHalfLine = 18f;
    }
}
