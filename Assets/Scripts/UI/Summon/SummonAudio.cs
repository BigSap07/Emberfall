using Emberfall.Game;

namespace Emberfall.UI.Summon
{
    /// <summary>The moments in a summon that a sound can land on. Appended to, never reordered.</summary>
    public enum SummonBeat
    {
        /// <summary>The pull button goes down and the room starts to darken.</summary>
        Press,
        /// <summary>The light appears at the centre and starts to grow. Also fires, shorter, at
        /// the start of each later reveal in a multi-pull.</summary>
        BuildupStart,
        /// <summary>The light commits to the rolled tier's colour.</summary>
        Tell,
        /// <summary>The white flash that hides the cut to the hero.</summary>
        Flash,
        /// <summary>The hero is on screen.</summary>
        Reveal,
        /// <summary>One star stamping in. A 5-star fires this five times, one per star; every
        /// other tier fires it once for the whole row.</summary>
        StarPing,
        /// <summary>The ten-pull summary grid appears.</summary>
        Summary,
        /// <summary>The player skipped: a tap past the buildup, a hold, or SKIP ALL.</summary>
        Skip,
    }

    /// <summary>
    /// What a summon sound set has to answer. One method, because every beat carries the same two
    /// facts a sound designer would key off: the rolled tier, and which pull of the batch this is.
    /// </summary>
    public interface ISummonAudio
    {
        /// <param name="beat">Which moment.</param>
        /// <param name="stars">The rolled tier of the hero this beat belongs to. Zero for Summary.</param>
        /// <param name="index">For StarPing, which star (0-based); otherwise the pull's position
        /// in its batch (0-based).</param>
        void Play(SummonBeat beat, int stars, int index);
    }

    /// <summary>
    /// THE SUMMON SEQUENCE'S ONLY WAY TO MAKE A SOUND.
    ///
    /// Hook is nullable and the sequence never assumes it is set: every beat goes through Fire,
    /// which does nothing when no hook is installed. Dropping real SFX in later is writing one
    /// ISummonAudio and assigning it here, with no edit to the sequence.
    ///
    /// SummonScreen installs SfxSummonAudio below when nothing else has, so the screen keeps
    /// the synthesised tones it already played before this sequence existed.
    /// </summary>
    public static class SummonAudio
    {
        public static ISummonAudio Hook;

        public static void Fire(SummonBeat beat, int stars, int index)
        {
            if (Hook != null) Hook.Play(beat, stars, index);
        }
    }

    /// <summary>
    /// The stand-in sound set: the game's existing synthesised tones mapped onto the beats. It
    /// adds no audio asset and no new tone; it exists so the beats are audibly wired while the
    /// real set is owed. Beats with no fitting tone are deliberately silent.
    /// </summary>
    public sealed class SfxSummonAudio : ISummonAudio
    {
        private readonly Sfx _sfx;

        public SfxSummonAudio(Sfx sfx)
        {
            _sfx = sfx;
        }

        public void Play(SummonBeat beat, int stars, int index)
        {
            if (_sfx == null) return;
            switch (beat)
            {
                case SummonBeat.Press: _sfx.Tap(); break;
                case SummonBeat.BuildupStart: if (index == 0) _sfx.Cast(); break;
                case SummonBeat.Reveal:
                    if (stars >= 5) _sfx.Victory();
                    else _sfx.Reward();
                    break;
                case SummonBeat.Summary: _sfx.Reward(); break;
            }
        }
    }
}
