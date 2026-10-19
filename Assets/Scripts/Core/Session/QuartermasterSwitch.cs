namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE QUARTERMASTER SWITCH (Docs/DESIGN-QUARTERMASTER.md): the daily stock of rolled armor sold
    /// for Gold. ON by default, like OnboardingSwitch. Define EMBERFALL_QUARTERMASTER_OFF to roll it
    /// back to the build before it: no screen is built, no door leads to one, Quartermaster.Buy
    /// refuses with Off, and the save carries no trace of it. State already in a save is kept and
    /// written back untouched, so switching it off never costs a player the shelf they were
    /// looking at. The field is writable so the harness and the capture targets can force it;
    /// whatever sets it puts it back.
    /// </summary>
    public static class QuartermasterSwitch
    {
#if EMBERFALL_QUARTERMASTER_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
