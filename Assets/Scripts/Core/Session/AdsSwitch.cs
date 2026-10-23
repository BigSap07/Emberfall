namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE REWARDED ADS SWITCH (R28, Docs/DESIGN-REWARDED-ADS.md). OFF by default: whether to ship ads at all is
    /// owner decision 3 in that record. Define EMBERFALL_ADS to turn it on. Off means no ad button is drawn
    /// (RewardedAdPresenter answers Hidden), AdRewards grants nothing, no SDK is initialised, and the save carries
    /// no ads key, so a build with the switch off writes byte for byte the save the build before wrote. Once it
    /// ships on, the define flips to EMBERFALL_ADS_OFF as the rollback, as HeroContractSwitch's did. The field is
    /// writable so the harness can force it; whatever sets it puts it back.
    /// </summary>
    public static class AdsSwitch
    {
#if EMBERFALL_ADS
        public static bool Enabled = true;
#else
        public static bool Enabled = false;
#endif
    }
}
