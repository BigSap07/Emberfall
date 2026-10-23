namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE DAILY LOOP SWITCH (DESIGN-DAILY-LOOP.md): the daily orders, the daily and weekly chests
    /// and the seven day login calendar. ON by default since the owner's decision of 2026-10-23,
    /// which also wakes Wayfarer's Road's free track (its points are the loop's claims). Define
    /// EMBERFALL_DAILY_LOOP_OFF to roll back to the build before the loop: no order is generated, no
    /// progress is counted, no key is written to the save, and the hub shows no door to them. State
    /// already in a save is kept and written back untouched, so turning the switch off never costs a
    /// player what they earned with it on.
    ///
    /// The field is writable so the harness and the capture targets can force it; whatever sets it
    /// puts it back.
    /// </summary>
    public static class DailyLoopSwitch
    {
#if EMBERFALL_DAILY_LOOP_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
