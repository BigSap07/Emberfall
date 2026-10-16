namespace Emberfall.Core.Telemetry
{
    /// <summary>
    /// THE TELEMETRY OFF SWITCH (Docs/DESIGN-TELEMETRY.md). On by default in code, which still sends
    /// nothing: no stat leaves the device without the player's Yes AND a cloud config. Off, the game
    /// is the build before the playtest stats existed: no prompt, no Settings card, no file, no
    /// PlayerPrefs key, no hook, no request.
    ///
    /// Build it off by defining EMBERFALL_TELEMETRY_OFF. The field is writable so the harness can
    /// run a check both ways; whatever sets it puts it back.
    /// </summary>
    public static class TelemetrySwitch
    {
#if EMBERFALL_TELEMETRY_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
