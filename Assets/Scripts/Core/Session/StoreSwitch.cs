namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE STORE SWITCH (R19, Docs/DESIGN-STORE.md): the purchase path and the Store. ON by default, like
    /// QuartermasterSwitch. Define EMBERFALL_STORE_OFF to roll back to the build before R19: nothing can be
    /// bought, restored or recorded, every PurchaseLedger call refuses with Off, no Store door is drawn, and the
    /// save carries no trace of it. State already in a save is kept and written back untouched. The field is
    /// writable so the harness and the capture targets can force it; whatever sets it puts it back.
    ///
    /// ON does not mean a player can pay. What the build does with a purchase is decided separately by
    /// StoreModes.Resolve: a release build shows every offer as COMING SOON with no buy button, whatever this
    /// switch says; the editor and a development build run the test store with no real money.
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
    /// THE HERO CONTRACT'S SWITCH: a copy of a 5-star the player already owns, for ascension only, limit two a
    /// hero a month and four a month for the whole account (store.json's accountLimit). ON by default since R24,
    /// with the account cap; before that it was OFF behind EMBERFALL_HERO_CONTRACT. Define
    /// EMBERFALL_HERO_CONTRACT_OFF to roll back: the offer is not listed and cannot be bought, exactly as it was
    /// while the switch was off. The field is writable so the harness can force it; whatever sets it puts it back.
    /// </summary>
    public static class HeroContractSwitch
    {
#if EMBERFALL_HERO_CONTRACT_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
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
