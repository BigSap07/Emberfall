using UnityEngine;
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
    /// The summon's sound set, through the one Sfx door. With real audio on (audio.json's
    /// realAudio, the default) each beat is a cue: summon.start on the press, the summon.charge
    /// loop while the light builds, summon.reveal.N for the tier on the reveal, summon.star per
    /// star. With it off the beats play the game's original synthesised tones, as they did before
    /// the real set existed; beats with no fitting tone are deliberately silent there.
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
            if (_sfx.RealAudio) { PlayCue(beat, stars); return; }
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

        private void PlayCue(SummonBeat beat, int stars)
        {
            switch (beat)
            {
                case SummonBeat.Press: _sfx.Play("summon.start"); break;
                case SummonBeat.BuildupStart: _sfx.StartLoop("summon.charge"); break;
                // The flash hides the cut to the hero: the charge ends under it, the reveal starts on it.
                case SummonBeat.Flash: _sfx.StopLoop("summon.charge", 0.12f); break;
                case SummonBeat.Reveal:
                    _sfx.StopLoop("summon.charge", 0.05f);
                    _sfx.Play("summon.reveal." + Mathf.Clamp(stars, 1, 5));
                    break;
                case SummonBeat.StarPing: _sfx.Play("summon.star"); break;
                case SummonBeat.Summary: _sfx.Reward(); break;
                case SummonBeat.Skip: _sfx.StopLoop("summon.charge", 0.05f); break;
            }
        }
    }
}
