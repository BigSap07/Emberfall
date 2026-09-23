namespace Emberfall.UI.Summon
{
    /// <summary>
    /// Every duration in a summon, in seconds, in one place. DESIGN-SUMMON-PRESENTATION carries
    /// the same table with the reasoning; a number changed here is a number changed there.
    ///
    /// Two sets, Full and Fast, and the Fast set is the Full set with the BUILDUP cut rather than
    /// everything scaled down together. The buildup is what a player has seen a hundred times; the
    /// reveal is the part that still says something new on the hundredth pull, so it keeps its
    /// length in both.
    ///
    /// ENGINE-FREE on purpose: no UnityEngine in this file, so the harness can compile it beside
    /// the core and check the invariants the design doc states. SummonSettings, which reads
    /// PlayerPrefs, lives in its own file for that reason.
    /// </summary>
    public sealed class SummonTiming
    {
        /// <summary>The button stays sunk and the room darkens.</summary>
        public float Dim;

        /// <summary>From the light appearing to the flash starting, for a 3-star. Higher
        /// tiers add their Linger on top, AFTER the tell, and the flat tiers cut theirs short after
        /// it - so the time before the tell is the same for every tier and the length of the wait
        /// cannot leak the result early.</summary>
        public float Buildup;

        /// <summary>How far into the buildup the light commits to its colour.</summary>
        public float Tell;

        /// <summary>Extra time the committed light holds before the flash, per tier. This is the
        /// gap the whole sequence exists for: the player knows, and has not seen yet.</summary>
        public float Linger4;
        public float Linger5;

        /// <summary>
        /// THE FLAT TIERS, 1 and 2 stars. They get a short flat reveal rather than the full show:
        /// if a grey pull got the same 1.25s buildup, the same punch and the same stamp as a gold
        /// one, players would learn to skip all of it, and the gold tell would lose its meaning by
        /// association.
        ///
        /// The cut is made AFTER the tell and never before it. Up to the tell a flat pull looks
        /// and lasts exactly like any other, so the wait still cannot leak the result before the
        /// colour does. Once the light has gone grey or green there is nothing left to build to:
        /// it holds for FlatHold and flashes. So a flat tier's linger is NEGATIVE, the part of the
        /// shared buildup after the tell that it does not sit through.
        /// </summary>
        public const int FlatMaxStars = 2;

        /// <summary>How long a flat tier's committed light holds between the tell and the flash.</summary>
        public float FlatHold;
        /// <summary>A flat tier's mini tell in a batch, in place of MiniTell.</summary>
        public float FlatMini;
        /// <summary>A flat tier's reveal: a softer flash that clears in FlatFlashOut, the name and
        /// stars fading in together at FlatNameAt with no stamp, and settled at FlatSettled.</summary>
        public float FlatFlashOut;
        public float FlatNameAt;
        public float FlatSettled;

        /// <summary>The later pulls of a batch get this instead of a buildup: the light pops in
        /// already committed, holds, and flashes. Never a full buildup ten times.</summary>
        public float MiniTell;
        public float MiniLinger5;

        /// <summary>The white flash: up to full, then off over FlashOut while the hero appears
        /// underneath it.</summary>
        public float FlashIn;
        public float FlashOut;

        /// <summary>The portrait's settle from 1.15 to 1.0.</summary>
        public float PortraitIn;
        /// <summary>When the name stamps, and how long the stamp takes.</summary>
        public float NameAt;
        public float NameIn;
        /// <summary>When the stars stamp, and for a 5-star the gap between each star.</summary>
        public float StarsAt;
        public float StarGap;
        public float StarIn;
        /// <summary>When TAP TO CONTINUE appears. A tap before this completes the reveal instead
        /// of leaving it, so a player hammering through a skip cannot tap past a hero unseen.</summary>
        public float Settled;

        public float BuildupFor(int stars)
        {
            return Buildup + LingerFor(stars);
        }

        public static bool IsFlat(int stars)
        {
            return stars <= FlatMaxStars;
        }

        /// <summary>Time added to (or, for a flat tier, cut from) the shared buildup, always after
        /// the tell. Negative for a flat tier: see FlatMaxStars.</summary>
        public float LingerFor(int stars)
        {
            if (stars >= 5) return Linger5;
            if (stars == 4) return Linger4;
            if (IsFlat(stars)) return Tell + FlatHold - Buildup;
            return 0f;
        }

        public float MiniFor(int stars)
        {
            if (IsFlat(stars)) return FlatMini;
            return MiniTell + (stars >= 5 ? MiniLinger5 : 0f);
        }

        public float FlashOutFor(int stars)
        {
            return IsFlat(stars) ? FlatFlashOut : FlashOut;
        }

        public float SettledFor(int stars)
        {
            return IsFlat(stars) ? FlatSettled : Settled;
        }

        public static readonly SummonTiming Full = new SummonTiming
        {
            Dim = 0.22f,
            Buildup = 1.25f,
            Tell = 0.80f,
            Linger4 = 0.10f,
            Linger5 = 0.30f,
            MiniTell = 0.34f,
            MiniLinger5 = 0.30f,
            FlatHold = 0.14f,
            FlatMini = 0.18f,
            FlatFlashOut = 0.12f,
            FlatNameAt = 0.04f,
            FlatSettled = 0.28f,
            FlashIn = 0.06f,
            FlashOut = 0.24f,
            PortraitIn = 0.34f,
            NameAt = 0.14f,
            NameIn = 0.12f,
            StarsAt = 0.30f,
            StarGap = 0.13f,
            StarIn = 0.12f,
            Settled = 0.55f,
        };

        public static readonly SummonTiming Fast = new SummonTiming
        {
            Dim = 0.12f,
            Buildup = 0.30f,
            Tell = 0.10f,
            Linger4 = 0f,
            Linger5 = 0.08f,
            MiniTell = 0.18f,
            MiniLinger5 = 0.08f,
            FlatHold = 0.06f,
            FlatMini = 0.10f,
            FlatFlashOut = 0.10f,
            FlatNameAt = 0.02f,
            FlatSettled = 0.20f,
            FlashIn = 0.04f,
            FlashOut = 0.18f,
            PortraitIn = 0.26f,
            NameAt = 0.08f,
            NameIn = 0.10f,
            StarsAt = 0.16f,
            StarGap = 0.08f,
            StarIn = 0.10f,
            Settled = 0.35f,
        };

        /// <summary>How long a finger has to stay down before the press counts as HOLD, which
        /// skips the whole summon, rather than a tap.</summary>
        public const float HoldToSkipAll = 0.45f;
    }
}
