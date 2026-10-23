namespace Emberfall.Core.Progression
{
    /// <summary>
    /// WHAT THE HERO SCREEN SAYS AT THE LEVEL CAP (R26), as pure text so the harness can pin the words. The
    /// owner's sentence comes first, exactly, and every hero at its cap gets it; the guidance the page already
    /// gave (what ascending costs, or that a low-star never ascends) follows in the same line.
    /// </summary>
    public static class LevelCapText
    {
        /// <summary>The owner's sentence. "Rank" is kept as the owner worded it.</summary>
        public const string Opening = "Level cap reached, raise your rank.";

        /// <summary>The label of the level buttons at the cap: the button keeps its name and is disabled, it does
        /// not turn into a different word.</summary>
        public const string ButtonLabel = "LEVEL UP";

        public static string Note(int stars, int copiesNeeded, int copiesToSpare, string heroName, int fodderPoints)
        {
            if (stars >= 5)
            {
                if (copiesNeeded < 0) return Opening + " This is the highest ascension this game authors.";
                return Opening + " Ascend with " + copiesNeeded + " more cop" + (copiesNeeded == 1 ? "y" : "ies") + " of " + heroName
                       + " to lift it  -  you hold " + copiesToSpare + " to spare.";
            }
            return Opening + " Below five stars a hero stops here and never ascends; it is worth " + fodderPoints
                   + " points towards converting fodder into a five-star.";
        }
    }
}
