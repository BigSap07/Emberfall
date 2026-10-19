namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE STORE SWITCH (R19, Docs/DESIGN-STORE.md): the purchase path and the Store. OFF by default. Define
    /// EMBERFALL_STORE_ON to turn it on. With it off nothing can be bought, restored or recorded: every
    /// PurchaseLedger call refuses with Off, no Store door is drawn, and the save carries no trace of it, so
    /// the build is the one before R19 byte for byte. State already in a save is kept and written back
    /// untouched. The field is writable so the harness and the capture targets can force it; whatever sets it
    /// puts it back.
    ///
    /// What the build does with a purchase is decided separately by StoreMode.Resolve: a release build never
    /// sells anything through this run's code, whatever this switch says.
    /// </summary>
    public static class StoreSwitch
    {
#if EMBERFALL_STORE_ON
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }

    /// <summary>
    /// THE HERO CONTRACT'S SWITCH: a copy of a 5-star the player already owns, for ascension only, limit two a
    /// hero a month. OFF, and the owner turns it on after the curve is re-measured with paid copies. Define
    /// EMBERFALL_HERO_CONTRACT to build it on. With it off the offer is not listed and cannot be bought.
    /// </summary>
    public static class HeroContractSwitch
    {
#if EMBERFALL_HERO_CONTRACT
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }

    /// <summary>What the build does when a player taps an offer.</summary>
    public enum StoreMode
    {
        /// <summary>The Store is off. Nothing is drawn and nothing can be bought.</summary>
        Off,
        /// <summary>A release build without a live store: every offer is shown in full with its price tier
        /// and the words "coming soon". There is NO purchase button.</summary>
        ComingSoon,
        /// <summary>The editor (Unity IAP's fake store) or a development build (the debug store): a purchase
        /// runs end to end and no real money moves.</summary>
        Test,
        /// <summary>A real store. Reserved: it needs a release build, the Unity IAP package AND the define
        /// EMBERFALL_STORE_LIVE, which nothing in this run sets.</summary>
        Live
    }

    /// <summary>Decides the StoreMode from facts the build knows. Pure, so the harness proves the table.</summary>
    public static class StoreModes
    {
        public static StoreMode Resolve(bool switchOn, bool editorOrDevelopmentBuild, bool liveDefine, bool iapPackage)
        {
            if (!switchOn) return StoreMode.Off;
            if (editorOrDevelopmentBuild) return StoreMode.Test;
            if (liveDefine && iapPackage) return StoreMode.Live;
            return StoreMode.ComingSoon;
        }

        /// <summary>True when a purchase button may be drawn.</summary>
        public static bool CanBuy(StoreMode mode)
        {
            return mode == StoreMode.Test || mode == StoreMode.Live;
        }
    }
}
