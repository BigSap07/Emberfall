using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// One talent's reading from a trial: the hero's own output with it picked, against the same
    /// hero with nothing picked, fought seed for seed in the same fight.
    /// </summary>
    public sealed class TalentReading
    {
        /// <summary>0 for "no talent", else 1-3. Reading 0 is the baseline the others are read against.</summary>
        public int Selection;

        /// <summary>Fights run so far. The trial fills readings a few fights at a time, so a
        /// screen can print a reading before it is finished and say how far it has got.</summary>
        public int Fights;

        public int Wins;

        /// <summary>Summed across fights, so the mean is Total / Fights.</summary>
        public long TotalOutput;
        public long TotalRounds;

        /// <summary>Per fight, in seed order, so a pairing with the baseline is seed for seed.</summary>
        public readonly List<long> Outputs = new List<long>();

        /// <summary>How many times the talent's own passive fired, summed. Zero for a talent that
        /// works by changing another ability rather than by firing, which is a third of them.</summary>
        public int Fires;

        public long MeanOutput { get { return Fights > 0 ? TotalOutput / Fights : 0; } }
        public float MeanRounds { get { return Fights > 0 ? (float)TotalRounds / Fights : 0f; } }

        /// <summary>Paired mean change against the baseline, per fight. Filled by TalentTrial.</summary>
        public double Delta;

        /// <summary>Delta over its standard error. Beyond +2 or -2 is a reading; between is a coin.</summary>
        public double T;

        /// <summary>True when every one of this talent's fights produced exactly the baseline's
        /// output: it did not reach this fight at all, which is a different answer from "it
        /// reached it and was worth nothing".</summary>
        public bool Unreached;

        public TalentVerdict Verdict
        {
            get
            {
                if (Selection == 0 || Fights < 2) return TalentVerdict.Pending;
                if (Unreached) return TalentVerdict.Unreached;
                if (Delta > 0 && T >= TalentTrial.ClearT) return TalentVerdict.Gains;
                if (Delta < 0 && T <= -TalentTrial.ClearT) return TalentVerdict.Loses;
                return TalentVerdict.Unclear;
            }
        }
    }

    public enum TalentVerdict
    {
        Pending,
        Gains,
        Loses,
        Unclear,
        Unreached
    }

    /// <summary>
    /// WHAT A TALENT DOES TO THIS PLAYER'S HERO, measured in this player's own line.
    ///
    /// THE QUESTION a talent screen has to answer is not "what does the sentence say" but "which of
    /// these three makes my hero better", and the simulation can answer it directly: it is a pure
    /// function of a request, so the same line can fight the same fight at the same seeds four
    /// times - once with nothing picked, once per talent - and the only thing that differs between
    /// the four is the talent. That is the harness's own talent-value method (paired seeds, the
    /// hero's own output, a t on the difference), pointed at the player's roster instead of at a
    /// sweep arena of stat-sticks.
    ///
    /// THE FIGHT is a MIRROR: this line against a frozen copy of itself with the subject on no
    /// talent, which is a Proving ghost (ProvingGenerator.GhostOf) of the line being measured. It
    /// was the campaign first - the hardest battle of the stage the player is on, the one
    /// GameSession.SweepClearedStage fights - and that was measured and thrown out. On a real
    /// level-13 save the stage fell in two rounds, and 82 of the 90 talents read exactly as not
    /// picking one, because nothing had lived long enough to set them off. A mirror is as even a
    /// fight as this line can have at whatever point the player has reached, so it runs eight to
    /// thirteen rounds whether the save is a day old or a year old, and it is the same body of
    /// opponents for every reading. It also crashed the engine, which is how the ExpireStatuses
    /// guard in CombatSimulation was found.
    ///
    /// THE LINE is the player's deployed team. A hero on the bench is measured standing in the
    /// back slot of that line, and <see cref="StandIn"/> reports whom it replaced, because a
    /// number measured in a line the player does not field has to say which line it was.
    ///
    /// THE QUANTITY is the hero's own output - damage dealt, healing paid and ward granted,
    /// credited by source - which is what the harness's star band measures. It does not see a
    /// talent that buys survival, control or somebody else's damage, so wins are carried beside
    /// it rather than folded into it.
    ///
    /// NOTHING HERE WRITES. Every fight runs on a copy of the hero with the selection set on the
    /// copy, so measuring cannot change a save, and the player's own pick is untouched until they
    /// press the button that calls GameSession.SelectTalent.
    /// </summary>
    public sealed class TalentTrial
    {
        /// <summary>
        /// Fights per reading. Forty, the harness's own TalentValueSeeds, and measured here rather
        /// than borrowed: on a real save (thirty 5-stars at level 13) sixteen seeds left both
        /// talents then audited as traps inside the noise, and forty put Kael's Lasting Materials
        /// at t -4.5 and Velis's Final Denial at t -2.2, the same side of the line the harness
        /// found them on. Velis III has since left the audited list (-341 a battle, t -1.4); which
        /// talents are traps now is TalentAudit.Costly, never this note.
        /// Four readings of forty is 160 fights, which cost 90 ms per hero on average and 129 ms
        /// at worst on a desktop CLR, so a screen spreading them over frames finishes in well
        /// under a second.
        /// </summary>
        public const int DefaultSeeds = 40;

        /// <summary>Fights per reading for this trial.</summary>
        public readonly int Seeds;

        /// <summary>How far past zero, in standard errors, a change has to sit to be printed as a
        /// gain or a loss. The harness's own line (TalentValueLossT).</summary>
        public const double ClearT = 2.0;

        public readonly HeroInstance Subject;
        public readonly StageDef Fight;

        /// <summary>The far side: this line, frozen, with the subject on no talent.</summary>
        public readonly BattleLoadout Mirror;

        /// <summary>The deployed hero the subject stood in for, or null when it is deployed.</summary>
        public readonly HeroInstance StandIn;

        /// <summary>Index 0 is the baseline; 1 to TalentCount are the talents.</summary>
        public readonly TalentReading[] Readings;

        public int TalentCount { get { return Readings.Length - 1; } }

        private readonly GameSession _session;
        private readonly List<HeroInstance>[] _teams;
        private readonly int _seedBase;
        private int _nextSeed;
        private int _nextSelection;

        public bool Finished { get { return _nextSeed >= Seeds; } }

        /// <summary>Fights that threw, and the last thing one threw. Zero on every trial the
        /// engine can finish, which is every one after the ExpireStatuses guard.</summary>
        public int Failures;
        public string LastError = "";

        /// <summary>Wall-clock milliseconds spent inside Step, summed: what the trial actually
        /// cost the device, as against how long it took to spread across frames.</summary>
        public double SimulationMs;

        /// <summary>Fights run, out of Seeds * (TalentCount + 1).</summary>
        public int FightsRun { get { return _nextSeed * Readings.Length + _nextSelection; } }
        public int FightsTotal { get { return Seeds * Readings.Length; } }

        /// <summary>Null when there is nothing to measure: no hero, no talents, or no fight.</summary>
        public static TalentTrial Create(GameSession session, HeroInstance subject)
        {
            return Create(session, subject, DefaultSeeds);
        }

        public static TalentTrial Create(GameSession session, HeroInstance subject, int seeds)
        {
            if (session == null || subject == null || subject.Def == null) return null;
            if (subject.Def.Talents.Count == 0) return null;
            return new TalentTrial(session, subject, Math.Max(2, seeds));
        }

        private TalentTrial(GameSession session, HeroInstance subject, int seeds)
        {
            _session = session;
            Seeds = seeds;
            Subject = subject;
            Fight = ProvingGenerator.BuildStage();

            List<HeroInstance> line = session.Profile.TeamHeroes();
            int at = -1;
            for (int i = 0; i < line.Count; i++) if (line[i] == subject) at = i;
            if (at < 0)
            {
                int max = Math.Max(1, session.Balance.MaxTeamSize);
                if (line.Count >= max)
                {
                    StandIn = line[line.Count - 1];
                    line.RemoveAt(line.Count - 1);
                }
                line.Add(subject);
                at = line.Count - 1;
            }

            int count = subject.Def.Talents.Count;
            Readings = new TalentReading[count + 1];
            _teams = new List<HeroInstance>[count + 1];
            for (int t = 0; t <= count; t++)
            {
                Readings[t] = new TalentReading();
                Readings[t].Selection = t;
                List<HeroInstance> team = new List<HeroInstance>(line);
                team[at] = CopyWithTalent(subject, t);
                _teams[t] = team;
            }

            // The mirror is taken from the BASELINE line, so it is the same body of opponents
            // for all four readings and the only thing that differs between them is the talent.
            Mirror = ProvingGenerator.GhostOf(session.Content, session.Profile, _teams[0], "Your Own Line");

            // Seeded from the hero and the fight, so opening the screen twice prints the same
            // numbers. A reading that shuffled every visit would be read as the talent changing.
            _seedBase = 7100000 + (int)(Hash(subject.DefId + "|" + Fight.Id) % 1000000u);
        }

        /// <summary>
        /// Runs fights until the budget in milliseconds is spent or the trial is finished, and
        /// returns true once it is finished. A whole round of selections at one seed is the unit
        /// of progress only in the sense that readings are compared at equal fight counts; the
        /// budget can stop between any two fights.
        /// </summary>
        public bool Step(double budgetMs)
        {
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            // The per-battle debug hook is taken off for the trial and put back after. It exists
            // to hand a developer the event list of a fight they are watching; these are 160
            // fights nobody watches, and in the editor the hook writes each one to the console,
            // which cost more than the simulation itself on the first capture.
            Action<BattleResult> hook = CombatSimulation.OnBattleComplete;
            CombatSimulation.OnBattleComplete = null;
            try
            {
            while (!Finished)
            {
                RunOne(_nextSelection, _nextSeed);
                _nextSelection++;
                if (_nextSelection >= Readings.Length)
                {
                    _nextSelection = 0;
                    _nextSeed++;
                    Pair();
                }
                if (clock.Elapsed.TotalMilliseconds >= budgetMs) break;
            }
            }
            finally
            {
                CombatSimulation.OnBattleComplete = hook;
            }
            SimulationMs += clock.Elapsed.TotalMilliseconds;
            return Finished;
        }

        /// <summary>Runs the whole trial in one call. For the harness and for timing.</summary>
        public void RunAll()
        {
            while (!Finished) Step(double.MaxValue);
        }

        private void RunOne(int selection, int seedIndex)
        {
            BattleRequest request = new BattleRequest();
            request.Content = _session.Content;
            request.Stage = Fight;
            request.Seed = _seedBase + seedIndex;
            request.Team = _teams[selection];
            request.Profile = _session.Profile;
            request.Opponent = Mirror;
            // A fight that throws is counted and carried on from, not allowed to take the screen
            // down with it: this runs a line against itself, which no authored fight does, and it
            // is exactly how the ExpireStatuses fault was found. It scores zero and says so.
            BattleResult r;
            try { r = CombatSimulation.Run(request); }
            catch (Exception ex)
            {
                r = null;
                Failures++;
                LastError = ex.GetType().Name + ": " + ex.Message;
            }

            TalentReading reading = Readings[selection];
            long output = OutputOf(r, Subject.DefId);
            reading.Fights++;
            reading.Outputs.Add(output);
            reading.TotalOutput += output;
            reading.TotalRounds += r != null ? r.Rounds : 0;
            if (r != null && r.Victory) reading.Wins++;

            PassiveDef talent = Subject.Def.TalentAt(selection);
            if (r != null && talent != null && !string.IsNullOrEmpty(talent.Id))
                for (int i = 0; i < r.Events.Count; i++)
                    if (r.Events[i].Type == BattleEventType.PassiveTrigger && r.Events[i].AbilityId == talent.Id)
                        reading.Fires++;
        }

        /// <summary>The harness's PairUp, over however many seeds every reading has reached.</summary>
        private void Pair()
        {
            List<long> bare = Readings[0].Outputs;
            for (int t = 1; t < Readings.Length; t++)
            {
                TalentReading reading = Readings[t];
                int n = Math.Min(bare.Count, reading.Outputs.Count);
                if (n == 0) continue;
                double mean = 0;
                bool same = true;
                for (int i = 0; i < n; i++)
                {
                    long d = reading.Outputs[i] - bare[i];
                    if (d != 0) same = false;
                    mean += d;
                }
                mean /= n;
                double ss = 0;
                for (int i = 0; i < n; i++)
                {
                    double d = reading.Outputs[i] - bare[i] - mean;
                    ss += d * d;
                }
                double se = n > 1 ? Math.Sqrt(ss / (n - 1) / n) : 0;
                reading.Delta = mean;
                // A change with no spread at all is a certain change, not a zero reading.
                reading.T = se > 0 ? mean / se : (mean < 0 ? -99 : mean > 0 ? 99 : 0);
                reading.Unreached = same;
            }
        }

        /// <summary>Damage dealt, healing paid and ward granted by the subject, credited by source
        /// id - the harness's SubjectOutput. A DoT tick credits whoever applied it.</summary>
        public static long OutputOf(BattleResult r, string defId)
        {
            if (r == null) return 0;
            int id = -1;
            for (int i = 0; i < r.Units.Count; i++)
                if (r.Units[i].IsPlayer && r.Units[i].DefId == defId) { id = r.Units[i].Id; break; }
            if (id < 0) return 0;
            long total = 0;
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.SourceId != id) continue;
                if (e.Type == BattleEventType.Damage || e.Type == BattleEventType.Heal
                    || e.Type == BattleEventType.ShieldGain) total += e.Value;
            }
            return total;
        }

        /// <summary>
        /// The hero as it would fight with this selection. Everything the simulation reads off an
        /// instance is carried, and the owner's instance is never touched.
        ///
        /// The copy is given the ascension tier that authorises the selection being read, which is
        /// at least the hero's own. A trial answers "what would this talent do", and that question
        /// is at its most useful about a talent the hero has NOT unlocked yet - it is what the
        /// locked card on the talent screen is for. Tier is invisible to the fight in every other
        /// way (StatMath.ForHero builds the line from Level), so this authorises the reading and
        /// changes nothing else about it.
        /// </summary>
        private static HeroInstance CopyWithTalent(HeroInstance h, int selection)
        {
            HeroInstance c = new HeroInstance();
            c.Uid = h.Uid;
            c.DefId = h.DefId;
            c.Level = h.Level;
            c.Copies = h.Copies;
            c.AcquiredUnix = h.AcquiredUnix;
            c.EquippedRelicId = h.EquippedRelicId;
            for (int i = 0; i < h.EquippedArmor.Length && i < c.EquippedArmor.Length; i++)
                c.EquippedArmor[i] = h.EquippedArmor[i];
            c.Def = h.Def;
            c.SelectedTalent = selection;
            c.Ascension = Math.Max(h.Ascension, HeroInstance.TierUnlockingTalent(selection));
            return c;
        }

        private static uint Hash(string s)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
