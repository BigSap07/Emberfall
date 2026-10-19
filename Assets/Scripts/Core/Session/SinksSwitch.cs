namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE SINKS SWITCH (DESIGN-SINKS.md): the Armory, which sells for Valor, and the Archive, which
    /// sells for Coda. ON by default since R19, which gathers the shops on the Store's Shops tab. Define
    /// EMBERFALL_SINKS_OFF to roll back to the build before them: no shop screen is built, no door leads to
    /// one, SinkShop refuses every purchase and writes nothing, and the save carries no trace of them. State
    /// already in a save is kept and written back untouched, so turning the switch off never costs a player
    /// what they bought with it on.
    ///
    /// The field is writable so the harness and the capture targets can force it; whatever sets it puts it
    /// back.
    /// </summary>
    public static class SinksSwitch
    {
#if EMBERFALL_SINKS_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
