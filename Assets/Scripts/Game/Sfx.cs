using UnityEngine;

namespace Emberfall.Game
{
    /// <summary>
    /// Tiny synthesised sound set - no audio assets required. Every clip is generated once
    /// at startup from a handful of envelopes, which is plenty for prototype feedback.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int SampleRate = 22050;

        private AudioSource _source;
        private AudioClip _tap;
        private AudioClip _hit;
        private AudioClip _crit;
        private AudioClip _heal;
        private AudioClip _cast;
        private AudioClip _death;
        private AudioClip _victory;
        private AudioClip _defeat;
        private AudioClip _reward;

        public bool Muted;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.volume = 0.55f;

            _tap = Tone("tap", 0.06f, 880f, 1320f, 0.35f, 0f);
            _hit = Tone("hit", 0.11f, 320f, 90f, 0.55f, 0.55f);
            _crit = Tone("crit", 0.18f, 520f, 130f, 0.7f, 0.45f);
            _heal = Tone("heal", 0.22f, 520f, 990f, 0.4f, 0f);
            _cast = Tone("cast", 0.26f, 240f, 720f, 0.45f, 0.15f);
            _death = Tone("death", 0.34f, 260f, 60f, 0.5f, 0.35f);
            _victory = Chord("victory", 0.55f, new float[] { 523f, 659f, 784f }, 0.42f);
            _defeat = Chord("defeat", 0.6f, new float[] { 220f, 262f, 165f }, 0.42f);
            _reward = Chord("reward", 0.35f, new float[] { 784f, 1046f }, 0.34f);
        }

        public void Tap() { Play(_tap, 0.5f); }
        public void Hit() { Play(_hit, 0.75f); }
        public void Crit() { Play(_crit, 0.95f); }
        public void Heal() { Play(_heal, 0.6f); }
        public void Cast() { Play(_cast, 0.8f); }
        public void Death() { Play(_death, 0.8f); }
        public void Victory() { Play(_victory, 0.85f); }
        public void Defeat() { Play(_defeat, 0.8f); }
        public void Reward() { Play(_reward, 0.7f); }

        private void Play(AudioClip clip, float volume)
        {
            if (Muted || _source == null || clip == null) return;
            _source.PlayOneShot(clip, volume);
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
