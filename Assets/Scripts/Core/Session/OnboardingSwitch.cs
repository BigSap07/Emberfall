namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE ONBOARDING OFF SWITCH (DESIGN-ONBOARDING.md). On by default. Off, a new install starts as
    /// it did before the Keeper's first session: the save is the ordinary new profile (with the two
    /// version 21 keys read complete and every tip seen), the first session is the old FirstRun
    /// opening, the coach is never installed, and FeatureGate treats every feature as unlocked.
    ///
    /// Build it off by defining EMBERFALL_ONBOARDING_OFF. The field is writable so the harness can
    /// run a check both ways; whatever sets it puts it back.
    /// </summary>
    public static class OnboardingSwitch
    {
#if EMBERFALL_ONBOARDING_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
