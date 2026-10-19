namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE STORE SWITCH (Docs/DESIGN-STORE.md): the Store screen on the Hub, its offers and their rules.
    /// ON by default, like QuartermasterSwitch. Define EMBERFALL_STORE_OFF to roll back to the build before
    /// it: no screen is built, no door is drawn, every StoreOffers call refuses with Off and writes nothing,
    /// the idle claim is the claim it always was, and the save carries no trace of it. State already in a
    /// save is kept and written back untouched, so switching it off never costs a player what they bought.
    /// The field is writable so the harness and the capture targets can force it; whatever sets it puts it
    /// back.
    ///
    /// The Store sells nothing here. Whether a purchase can complete at all is the purchase path's to
    /// say (a release build shows every offer as coming soon).
    /// </summary>
    public static class StoreSwitch
    {
#if EMBERFALL_STORE_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }

    /// <summary>
    /// THE HERO CONTRACT'S SWITCH: OFF. A contract is one more copy of a 5-star the buyer already owns, for
    /// ascension: copies are what power is made of, so this is the one offer that can move the curve. The
    /// owner turns it on after the curve is re-measured with paid copies. Build it on by defining
    /// EMBERFALL_HERO_CONTRACT. Off means the offer is not listed, is not drawn and cannot be bought.
    /// </summary>
    public static class HeroContractSwitch
    {
#if EMBERFALL_HERO_CONTRACT
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }
}
