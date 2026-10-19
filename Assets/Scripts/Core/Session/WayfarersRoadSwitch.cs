namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE WAYFARER'S ROAD SWITCH (Docs/DESIGN-WAYFARERS-ROAD.md): the free track of the season pass.
    /// ON by default, like OnboardingSwitch. Define EMBERFALL_WAYFARERS_ROAD_OFF to roll back to the
    /// build before it: no point is counted, no screen is built, no door is drawn, every call refuses
    /// with Off and the save carries no trace of it. State already in a save is kept and written back
    /// untouched. The field is writable so the harness and the capture targets can force it; whatever
    /// sets it puts it back.
    ///
    /// Its points are the daily loop's claims, so it earns nothing while DailyLoopSwitch is off.
    /// </summary>
    public static class WayfarersRoadSwitch
    {
#if EMBERFALL_WAYFARERS_ROAD_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }

    /// <summary>
    /// THE PAID TRACK'S SWITCH: OFF, and it stays off. With it off the paid track is not drawn. With it
    /// on the paid track is drawn as a LOCKED row, and it is still locked: there is no purchase path,
    /// no field in the save that could hold an unlock, and WayfarersRoad.ClaimPaid refuses every call.
    /// Build it on by defining EMBERFALL_PASS_PAID.
    /// </summary>
    public static class PassPaidSwitch
    {
#if EMBERFALL_PASS_PAID
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }
}
