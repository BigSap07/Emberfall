using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.Game
{
    /// <summary>
    /// The game's one door to sound. Every screen and the battle view say what happened
    /// (Tap, Crit, Cast(aspect), Reward...) and this decides how it sounds.
    ///
    /// TWO SOUND SETS, ONE FLAG. With audio.json's realAudio on (the default) every call becomes a
    /// cue played by AudioService: the owner's sound packs, layered, pitch varied, capped and
    /// volume controlled by AudioPrefs. With it off, or with no audio.json, the same calls play the
    /// small synthesised set this class shipped with, generated on first use. That is the
    /// rollback, and the calls that did not exist before the real set (Buff, Debuff, Shield,
    /// SkillImpact...) are silent there, as they always were.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int SampleRate = 22050;

        /// <summary>The synthesised sounds, by the name the old methods gave them.</summary>
        private enum Old
        {
            Tap, Hit, Crit, Heal, Cast, Death, Victory, Defeat, Reward
        }

        private AudioSource _source;
        private AudioClip[] _old;

        /// <summary>The cue player. Present always; Service.Enabled says whether it is in use.</summary>
        public AudioService Service { get; private set; }

        /// <summary>True when sounds come from audio.json rather than the synthesised set.</summary>
        public bool RealAudio { get { return Service != null && Service.Enabled; } }

        /// <summary>The master mute. Stored on the device (AudioPrefs); the settings screen owns it.</summary>
        public bool Muted
        {
            get { return AudioPrefs.Muted; }
            set { AudioPrefs.Muted = value; }
        }

        private void Awake()
        {
            Service = gameObject.AddComponent<AudioService>();
            Service.Init();
        }

        /// <summary>Test seam: swap the cue config. The rollback check turns the flag off this way.</summary>
        public void Configure(AudioCueConfig config)
        {
            Service.Init(config);
        }

        // ------------------------------------------------------------------ ui

        public void Tap() { Cue("ui.tap", Old.Tap, 0.5f); }
        public void Confirm() { Cue("ui.confirm", Old.Tap, 0.5f); }
        public void Back() { Cue("ui.back", Old.Tap, 0.5f); }
        /// <summary>A locked door or a refused action.</summary>
        public void Locked() { Cue("ui.locked", Old.Tap, 0.5f); }
        /// <summary>A coin or a reward landing.</summary>
        public void Reward() { Cue("ui.reward", Old.Reward, 0.7f); }
        public void LevelUp() { Cue("ui.levelup", Old.Reward, 0.7f); }
        public void Unlock() { Cue("ui.unlock", Old.Reward, 0.7f); }

        // ------------------------------------------------------------------ battle

        public void Hit() { Cue("battle.hit", Old.Hit, 0.75f); }
        public void Crit() { Cue("battle.crit", Old.Crit, 0.95f); }
        public void Heal() { Cue("battle.heal", Old.Heal, 0.6f); }
        public void Cast() { Cue("battle.cast", Old.Cast, 0.8f); }
        public void Death() { Cue("battle.death", Old.Death, 0.8f); }
        public void Buff() { Cue("battle.buff"); }
        public void Debuff() { Cue("battle.debuff"); }
        public void Shield() { Cue("battle.shield"); }

        /// <summary>A skill being cast: the cast sound of the caster's aspect.</summary>
        public void Cast(Aspect aspect)
        {
            Cue("battle.cast." + AspectKey(aspect), Old.Cast, 0.8f);
        }

        /// <summary>A skill landing: the impact sound of the caster's aspect, on the strike.</summary>
        public void SkillImpact(Aspect aspect)
        {
            Cue("battle.impact." + AspectKey(aspect));
        }

        public void Victory()
        {
            if (RealAudio) { Service.Play("music.stinger.victory"); Service.Play("battle.victory"); }
            else PlayOld(Old.Victory, 0.85f);
        }

        public void Defeat()
        {
            if (RealAudio) { Service.Play("music.stinger.defeat"); Service.Play("battle.defeat"); }
            else PlayOld(Old.Defeat, 0.8f);
        }

        // ------------------------------------------------------------------ anything else

        /// <summary>Plays a cue by id. Silent on the synthesised set.</summary>
        public void Play(string cueId)
        {
            Cue(cueId);
        }

        public void ScreenShown(string screenName)
        {
            if (RealAudio) Service.ScreenShown(screenName);
        }

        public void EnterBattle(string kind)
        {
            if (RealAudio) Service.EnterBattle(kind);
        }

        public void StartLoop(string cueId)
        {
            if (RealAudio) Service.StartLoop(cueId);
        }

        public void StopLoop(string cueId, float fadeSeconds)
        {
            if (RealAudio) Service.StopLoop(cueId, fadeSeconds);
        }

        public static string AspectKey(Aspect aspect)
        {
            return aspect.ToString().ToLowerInvariant();
        }

        /// <summary>A cue with no old sound: silent when the real set is off.</summary>
        private void Cue(string cueId)
        {
            if (RealAudio) Service.Play(cueId);
        }

        private void Cue(string cueId, Old old, float oldVolume)
        {
            if (RealAudio) Service.Play(cueId);
            else PlayOld(old, oldVolume);
        }

        // ------------------------------------------------------------------ the synthesised set

        private void PlayOld(Old which, float volume)
        {
            if (Muted) return;
            if (_old == null) BuildOld();
            AudioClip clip = _old[(int)which];
            if (_source == null || clip == null) return;
            _source.PlayOneShot(clip, volume);
        }

        private void BuildOld()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.volume = 0.55f;

            _old = new AudioClip[9];
            _old[(int)Old.Tap] = Tone("tap", 0.06f, 880f, 1320f, 0.35f, 0f);
            _old[(int)Old.Hit] = Tone("hit", 0.11f, 320f, 90f, 0.55f, 0.55f);
            _old[(int)Old.Crit] = Tone("crit", 0.18f, 520f, 130f, 0.7f, 0.45f);
            _old[(int)Old.Heal] = Tone("heal", 0.22f, 520f, 990f, 0.4f, 0f);
            _old[(int)Old.Cast] = Tone("cast", 0.26f, 240f, 720f, 0.45f, 0.15f);
            _old[(int)Old.Death] = Tone("death", 0.34f, 260f, 60f, 0.5f, 0.35f);
            _old[(int)Old.Victory] = Chord("victory", 0.55f, new float[] { 523f, 659f, 784f }, 0.42f);
            _old[(int)Old.Defeat] = Chord("defeat", 0.6f, new float[] { 220f, 262f, 165f }, 0.42f);
            _old[(int)Old.Reward] = Chord("reward", 0.35f, new float[] { 784f, 1046f }, 0.34f);
        }

        /// <summary>Sine sweep with an exponential decay, plus optional noise for impacts.</summary>
        private static AudioClip Tone(string name, float seconds, float startHz, float endHz, float decay, float noise)
        {
            int samples = Mathf.Max(16, (int)(seconds * SampleRate));
            float[] data = new float[samples];
            float phase = 0f;
            uint rng = 0x1234567u;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float hz = Mathf.Lerp(startHz, endHz, t * t);
                phase += 2f * Mathf.PI * hz / SampleRate;
                float envelope = Mathf.Pow(1f - t, 1f + decay * 6f);
                float value = Mathf.Sin(phase);

                if (noise > 0f)
                {
                    rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                    float white = ((rng & 0xFFFF) / 32768f) - 1f;
                    value = Mathf.Lerp(value, white, noise);
                }

                data[i] = value * envelope * 0.55f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            if (clip != null) clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Chord(string name, float seconds, float[] frequencies, float decay)
        {
            int samples = Mathf.Max(16, (int)(seconds * SampleRate));
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float envelope = Mathf.Pow(1f - t, 1f + decay * 4f);
                float value = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    // stagger the notes so it reads as an arpeggio rather than a block chord
                    float start = f / (float)(frequencies.Length * 2);
                    if (t < start) continue;
                    value += Mathf.Sin(2f * Mathf.PI * frequencies[f] * (i / (float)SampleRate));
                }
                data[i] = (value / Mathf.Max(1, frequencies.Length)) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            if (clip != null) clip.SetData(data, 0);
            return clip;
        }
    }
}
