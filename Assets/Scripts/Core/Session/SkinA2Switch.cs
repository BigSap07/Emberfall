namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE A2 SKIN SWITCH (R56): the shared widgets (panels, wells, buttons, tabs, pills, discs, the
    /// ribbon, the bars) drawn in the A2 look, in Lilita One and Nunito. ON by default; define
    /// EMBERFALL_SKIN_A2_OFF to build every screen as it was before R56 (the same opt-out shape as
    /// UiKitSwitch and FormationA2Switch). The field is writable so the harness and the capture
    /// targets can force it; whatever sets it puts it back.
    ///
    /// OFF IS THE OLD LOOK, EXACTLY. A2Skin answers null for every slot and the font helpers answer
    /// the old faces, so EmberforgeSkin and EmberforgeUI take the paths they took before R56. The
    /// switch is a presentation preference of the build: it reads nothing from a save, writes nothing
    /// to one, and never reaches a fight.
    /// </summary>
    public static class SkinA2Switch
    {
#if EMBERFALL_SKIN_A2_OFF
        public static readonly bool ShippedDefault = false;
#else
        public static readonly bool ShippedDefault = true;
#endif

        public static bool Enabled = ShippedDefault;
    }
}
