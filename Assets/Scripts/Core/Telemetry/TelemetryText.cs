namespace Emberfall.Core.Telemetry
{
    /// <summary>
    /// The words the player reads about play stats, in one place so the prompt and the Settings
    /// card say the same thing and a reviewer can read the promise without opening a screen.
    /// </summary>
    public static class TelemetryText
    {
        public const string PromptTitle = "PLAY STATS";

        /// <summary>The question, exactly as the design records it.</summary>
        public const string PromptQuestion =
            "Share anonymous play stats to help us improve the game? No account, no name, no personal data.";

        public const string PromptNote = "You can change this any time in Settings.";

        public const string Yes = "YES";
        public const string No = "NO";

        public const string SettingsHeading = "PLAY STATS";
        public const string SettingsOn = "SHARE PLAY STATS: ON";
        public const string SettingsOff = "SHARE PLAY STATS: OFF";

        public const string SettingsNoteOn =
            "Anonymous counts only, such as which stage stops a run. No account, no name, no personal data. "
            + "Turning this off deletes the random id and anything unsent.";

        public const string SettingsNoteOff =
            "Nothing is recorded or sent. Turn this on to share anonymous play stats: no account, no name, no personal data.";
    }
}
