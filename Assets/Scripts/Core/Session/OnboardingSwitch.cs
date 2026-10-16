namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE ONBOARDING OFF SWITCH (DESIGN-ONBOARDING.md). On by default. Off, the game is the build
    /// before onboarding existed: a new save is the ordinary new profile with the tutorial counted
    /// complete, the coach is never installed, and FeatureGate treats every feature as unlocked.
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
