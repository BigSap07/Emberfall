namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE SINKS SWITCH (DESIGN-SINKS.md): the Armory, which sells for Valor, and the Archive, which
    /// sells for Coda. OFF by default, which is the build before they existed: no shop screen is
    /// built, no door leads to one, SinkShop refuses every purchase and writes nothing, and the
    /// save carries no trace of them. State already in a save is kept and written back untouched,
    /// so turning the switch off never costs a player what they bought with it on.
    ///
    /// Build it on by defining EMBERFALL_SINKS_ON. The field is writable so the harness and the
    /// capture targets can force it on; whatever sets it puts it back.
    /// </summary>
    public static class SinksSwitch
    {
#if EMBERFALL_SINKS_ON
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }
}
