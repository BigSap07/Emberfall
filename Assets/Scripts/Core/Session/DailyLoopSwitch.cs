namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE DAILY LOOP SWITCH (DESIGN-DAILY-LOOP.md): the daily orders, the daily and weekly chests
    /// and the seven day login calendar. OFF by default, which is the build before they existed:
    /// no order is generated, no progress is counted, no key is written to the save, and the hub
    /// shows no door to them. State already in a save is kept and written back untouched, so
    /// turning the switch off never costs a player what they earned with it on.
    ///
    /// Build it on by defining EMBERFALL_DAILY_LOOP_ON. The field is writable so the harness and
    /// the capture targets can force it on; whatever sets it puts it back.
    /// </summary>
    public static class DailyLoopSwitch
    {
#if EMBERFALL_DAILY_LOOP_ON
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }
}
