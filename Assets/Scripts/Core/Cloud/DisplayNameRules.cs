namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// The name a friend or a guild-mate sees on a player's line.
    ///
    /// ONE RULE WITH THE GUILD NAME, and that is deliberate rather than convenient: the server
    /// applies the guild-name rule (`btrim`, 3 to 20 characters, no control characters) to
    /// display names too, so this validates through the same constants and the same helpers
    /// GuildRules does. There is no second copy of 3 or 20 to drift from the first. Only the
    /// words differ, because a refusal that says "a guild name" to somebody choosing their own
    /// name is a refusal about the wrong thing.
    ///
    /// The client's job, as with every rule in this namespace, is to stop a player BEFORE a round
    /// trip and to send the cleaned string rather than the typed one. Whether a name is allowed is
    /// the database's call; this is only ever stricter than it, never looser.
    /// </summary>
    public static class DisplayNameRules
    {
        /// <summary>What `profiles.display_name` holds until a player chooses: the column's
        /// default. A name equal to this is "not chosen yet" unless the player typed it themselves
        /// - see CloudLocalState.ChosenNameFor.</summary>
        public const string Default = "Wayfarer";

        public const int Min = GuildRules.NameMin;
        public const int Max = GuildRules.NameMax;

        /// <summary>
        /// Trims, measures and screens a typed name. `cleaned` is what to send; `problem` is one
        /// sentence for the player when this returns false, and empty when it returns true.
        /// </summary>
        public static bool Validate(string typed, out string cleaned, out string problem)
        {
            cleaned = (typed == null ? "" : typed).Trim();
            int length = GuildRules.CharacterCount(cleaned);
            if (length < Min || length > Max)
            {
                problem = "A name is " + Min + " to " + Max + " characters.";
                return false;
            }
            if (GuildRules.HasControlCharacter(cleaned))
            {
                problem = "A name is a single line.";
                return false;
            }
            problem = "";
            return true;
        }
    }
}
