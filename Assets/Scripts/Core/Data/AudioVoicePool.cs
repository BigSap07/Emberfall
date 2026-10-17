using System.Collections.Generic;

namespace Emberfall.Core.Data
{
    /// <summary>One voice's bookkeeping: when it frees up, what it is playing and how much that
    /// matters. AudioService derives from this to hang an AudioSource on it, so the rules below run
    /// unchanged in the DevHarness.</summary>
    public class VoiceSlot
    {
        /// <summary>Seconds on the clock when the sound ends. Free when now is at or past it.</summary>
        public float BusyUntil;
        public float Started;
        public string Cue = "";
        public int Priority;
    }

    /// <summary>
    /// WHO GETS A VOICE (R10-AUDIO). A big turn can ask for twenty sounds in a second; these rules
    /// are why it does not get twenty. Pure arithmetic over slots, so the harness proves the cap.
    ///
    ///   1. A cue that already holds its share (perCueVoices) takes its own oldest voice back.
    ///   2. Otherwise a free voice.
    ///   3. Otherwise the weakest busy voice of equal or lower priority (the oldest of them).
    ///   4. Otherwise nothing: the new sound is dropped.
    /// </summary>
    public static class AudioVoicePool
    {
        /// <summary>The index of the slot the new sound takes, or -1 to drop it.</summary>
        public static int Choose<T>(IList<T> pool, string cue, int priority, int perCueVoices, float now) where T : VoiceSlot
        {
            int free = -1, oldestSame = -1, weakest = -1, sameCount = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                VoiceSlot v = pool[i];
                if (now >= v.BusyUntil) { if (free < 0) free = i; continue; }
                if (v.Cue == cue)
                {
                    sameCount++;
                    if (oldestSame < 0 || v.Started < pool[oldestSame].Started) oldestSame = i;
                }
                if (v.Priority <= priority)
                {
                    bool weaker = weakest < 0 || v.Priority < pool[weakest].Priority
                                  || (v.Priority == pool[weakest].Priority && v.Started < pool[weakest].Started);
                    if (weaker) weakest = i;
                }
            }
            if (sameCount >= perCueVoices && oldestSame >= 0) return oldestSame;
            if (free >= 0) return free;
            return weakest;
        }

        public static int Busy<T>(IList<T> pool, float now) where T : VoiceSlot
        {
            int n = 0;
            for (int i = 0; i < pool.Count; i++) if (now < pool[i].BusyUntil) n++;
            return n;
        }
    }

    /// <summary>The minimum gap between two plays of one cue. A cue that is asked again inside its
    /// gap is not played: an area hit that lands five Damage events in one frame sounds once.</summary>
    public sealed class CueCooldowns
    {
        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();

        /// <summary>True and records the play when the cue may sound now; false inside its gap.</summary>
        public bool TryFire(string cue, float now, float cooldown)
        {
            float last;
            if (cooldown > 0f && _last.TryGetValue(cue, out last) && now - last < cooldown) return false;
            _last[cue] = now;
            return true;
        }
    }
}
