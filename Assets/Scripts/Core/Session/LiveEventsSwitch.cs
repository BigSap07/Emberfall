namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE LIVE EVENTS SWITCH (R24, the events section of Docs/DESIGN-STORE.md): the weekly and the monthly
    /// event with their quests, points, free track, paid Event Track and event pack. ON by default, like
    /// StoreSwitch. Define EMBERFALL_LIVE_EVENTS_OFF to roll back to the build before R24: no quest counts, no
    /// reward is claimed, the Hub's Events door opens the limited-time events page it always opened, every call
    /// here refuses with Off, and the save carries no trace of it. State already in a save is kept and written
    /// back untouched. The field is writable so the harness and the capture targets can force it; whatever sets
    /// it puts it back.
    ///
    /// ON does not mean a player can pay: what the build does with a purchase is StoreModes.Resolve's, as for
    /// every offer. The track and the pack are store offers like any other and need StoreSwitch on too.
    /// </summary>
    public static class LiveEventsSwitch
    {
#if EMBERFALL_LIVE_EVENTS_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
