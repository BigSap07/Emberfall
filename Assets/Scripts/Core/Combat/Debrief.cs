using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Combat
{
    /// <summary>Which fact the Debrief line is built from - lets a caller (or a test) check the
    /// cause without string-matching the sentence.</summary>
    public enum DebriefClause
    {
        Timeout,
        AbilityDominant,
        Aspect,
        Resisted,
        Speed,
        UnspentActive,
        Fallback
    }

    public sealed class DebriefResult
    {
        public DebriefClause Clause;
        public string Text = "";
    }

    /// <summary>
    /// One plain sentence naming the single biggest reason a battle went the way it did. Reads
    /// BattleResult.Events once, after the fact - it never recomputes or influences the fight,
    /// so it cannot affect determinism. Checked in priority order; the first clause the numbers
    /// actually support wins. See DESIGN-BATTLE-READABILITY.md "The Debrief line".
    /// </summary>
    public static class Debrief
    {
        private const float AbilityDominantShare = 0.35f;
        private const float AspectDominantShare = 0.35f;
        private const float AspectFactorThreshold = 1.2f;
        private const float SpeedMajorityShare = 0.5f;

        /// <summary>
        /// Fraction of the player's own harmful statuses that has to be shrugged off before the
        /// resist clause fires, and the fewest attempts that share is allowed to be measured over.
        /// Four is not arbitrary: below it a single unlucky roll clears a half-share threshold and
        /// the line would blame Effect Resist for ordinary variance.
        /// </summary>
        private const float ResistedDominantShare = 0.5f;
        private const int ResistedMinAttempts = 4;

        public static DebriefResult Summarize(BattleResult result)
        {
            DebriefResult clause;
            if (result == null)
            {
                clause = new DebriefResult();
                clause.Clause = DebriefClause.Fallback;
                clause.Text = "";
                return clause;
            }

            if (TryTimeout(result, out clause)) return clause;
            if (TryAbilityDominant(result, out clause)) return clause;
            if (TryAspect(result, out clause)) return clause;
            if (TryResisted(result, out clause)) return clause;
            if (TrySpeed(result, out clause)) return clause;
            if (TryUnspentActive(result, out clause)) return clause;
            return Fallback(result);
        }

        // ------------------------------------------------------------------ 1. timeout

        private static bool TryTimeout(BattleResult r, out DebriefResult clause)
        {
            clause = null;
            if (!r.TimedOut) return false;

            clause = new DebriefResult();
            clause.Clause = DebriefClause.Timeout;
            clause.Text = "The round limit ran out with the enemy at " + Pct(r.EnemyHpRatio)
                         + "% health and your line at " + Pct(r.PlayerHpRatio) + "% - a longer fight, not a lost one.";
            return true;
        }

        // ------------------------------------------------------------------ 2. one ability did the damage

        private static bool TryAbilityDominant(BattleResult r, out DebriefResult clause)
        {
            clause = null;
            Dictionary<string, int> totalByAbility = new Dictionary<string, int>();
            Dictionary<string, int> sourceByAbility = new Dictionary<string, int>();
            int total = 0;

            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type != BattleEventType.Damage || e.Value <= 0) continue;
                UnitSnapshot target = r.FindUnit(e.TargetId);
                if (target == null || !target.IsPlayer) continue;

                string key = string.IsNullOrEmpty(e.AbilityName) ? "an unnamed strike" : e.AbilityName;
                int existing;
                totalByAbility.TryGetValue(key, out existing);
                totalByAbility[key] = existing + e.Value;
                sourceByAbility[key] = e.SourceId;
                total += e.Value;
            }
            if (total <= 0) return false;

            string bestKey = null;
            int bestValue = 0;
            foreach (KeyValuePair<string, int> kv in totalByAbility)
                if (kv.Value > bestValue) { bestValue = kv.Value; bestKey = kv.Key; }
            if (bestKey == null) return false;

            float share = bestValue / (float)total;
            if (share <= AbilityDominantShare) return false;

            int sourceId;
            sourceByAbility.TryGetValue(bestKey, out sourceId);
            UnitSnapshot source = r.FindUnit(sourceId);
            string sourceName = source != null ? source.Name : "One enemy";

            clause = new DebriefResult();
            clause.Clause = DebriefClause.AbilityDominant;
            clause.Text = sourceName + "'s " + bestKey + " alone did " + Pct(share)
                         + "% of the damage to your line (" + bestValue + " of " + total + ").";
            return true;
        }

        // ------------------------------------------------------------------ 3. aspect

        private static bool TryAspect(BattleResult r, out DebriefResult clause)
        {
            clause = null;
            int weak = 0;
            int total = 0;

            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type != BattleEventType.Damage || e.Value <= 0) continue;
                UnitSnapshot target = r.FindUnit(e.TargetId);
                if (target == null || !target.IsPlayer) continue;

                total += e.Value;
                if (e.AspectFactor >= AspectFactorThreshold) weak += e.Value;
            }
            if (total <= 0) return false;

            float share = weak / (float)total;
            if (share <= AspectDominantShare) return false;

            clause = new DebriefResult();
            clause.Clause = DebriefClause.Aspect;
            clause.Text = Pct(share) + "% of the damage you took landed for extra Aspect damage - the line was the wrong colour for this fight.";
            return true;
        }

        // ------------------------------------------------------------------ 4. afflictions did not land

        /// <summary>
        /// Counts the player's own offensive afflictions and how many the enemy shrugged off,
        /// reading StatusApplied against StatusResisted. Sits below the two damage clauses because
        /// a fight is usually decided by damage, and above Speed because this one is actionable:
        /// it names Effect Hit and Effect Resist, which the player can change by choosing who to
        /// deploy, where turn order is not something they pick.
        ///
        /// Player-cast and enemy-targeted only. An enemy resisting its own self-buff is impossible
        /// (beneficial statuses are never contested) and enemy debuffs failing on the player is
        /// good news, not an explanation of a loss.
        /// </summary>
        private static bool TryResisted(BattleResult r, out DebriefResult clause)
        {
            clause = null;
            int attempts = 0;
            int resisted = 0;
            Dictionary<int, int> resistedByTarget = new Dictionary<int, int>();

            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                bool applied = e.Type == BattleEventType.StatusApplied;
                bool blocked = e.Type == BattleEventType.StatusResisted;
                if (!applied && !blocked) continue;
                if (!StatusInfo.IsHarmful(e.Status)) continue;

                UnitSnapshot source = r.FindUnit(e.SourceId);
                UnitSnapshot target = r.FindUnit(e.TargetId);
                if (source == null || target == null) continue;
                if (!source.IsPlayer || target.IsPlayer) continue;

                attempts++;
                if (!blocked) continue;
                resisted++;
                int existing;
                resistedByTarget.TryGetValue(e.TargetId, out existing);
                resistedByTarget[e.TargetId] = existing + 1;
            }

            if (attempts < ResistedMinAttempts) return false;
            float share = resisted / (float)attempts;
            if (share <= ResistedDominantShare) return false;

            int worstId = -1;
            int worstCount = 0;
            foreach (KeyValuePair<int, int> kv in resistedByTarget)
                if (kv.Value > worstCount) { worstCount = kv.Value; worstId = kv.Key; }
            UnitSnapshot worst = r.FindUnit(worstId);

            clause = new DebriefResult();
            clause.Clause = DebriefClause.Resisted;
            clause.Text = worst != null
                ? Pct(share) + "% of your afflictions were shrugged off (" + resisted + " of " + attempts
                  + "), most of them by " + worst.Name + " - this line needs more Effect Hit against them."
                : Pct(share) + "% of your afflictions were shrugged off (" + resisted + " of " + attempts
                  + ") - this line needs more Effect Hit for this fight.";
            return true;
        }

        // ------------------------------------------------------------------ 5. speed

        /// <summary>
        /// For every player TurnStart, what fraction of that round's enemy turns had already
        /// happened. Averaged across the whole fight; a single round proves nothing (speed is
        /// re-rolled every round), but a consistent majority does.
        /// </summary>
        private static bool TrySpeed(BattleResult r, out DebriefResult clause)
        {
            clause = null;
            Dictionary<int, int> enemyTurnsInRound = new Dictionary<int, int>();
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type != BattleEventType.TurnStart) continue;
                UnitSnapshot u = r.FindUnit(e.SourceId);
                if (u == null || u.IsPlayer) continue;
                int existing;
                enemyTurnsInRound.TryGetValue(e.Round, out existing);
                enemyTurnsInRound[e.Round] = existing + 1;
            }
            if (enemyTurnsInRound.Count == 0) return false;

            double fractionSum = 0;
            int fractionCount = 0;
            int currentRound = int.MinValue;
            int enemiesSoFar = 0;

            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type != BattleEventType.TurnStart) continue;
                if (e.Round != currentRound) { currentRound = e.Round; enemiesSoFar = 0; }

                UnitSnapshot u = r.FindUnit(e.SourceId);
                if (u == null) continue;
                if (!u.IsPlayer) { enemiesSoFar++; continue; }

                int enemyTotalThisRound;
                enemyTurnsInRound.TryGetValue(e.Round, out enemyTotalThisRound);
                if (enemyTotalThisRound <= 0) continue; // nothing left to be outsped by this round

                fractionSum += enemiesSoFar / (double)enemyTotalThisRound;
                fractionCount++;
            }
            if (fractionCount == 0) return false;

            double average = fractionSum / fractionCount;
            if (average <= SpeedMajorityShare) return false;

            clause = new DebriefResult();
            clause.Clause = DebriefClause.Speed;
            clause.Text = "Enemies acted before your line more often than not across the fight - you were outsped, not outmatched.";
            return true;
        }

        // ------------------------------------------------------------------ 6. unspent active

        private static bool TryUnspentActive(BattleResult r, out DebriefResult clause)
        {
            clause = null;
            List<int> deadIds = new List<int>();
            List<int> castIds = new List<int>();
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type == BattleEventType.Death) deadIds.Add(e.TargetId);
                else if (e.Type == BattleEventType.AbilityCast) castIds.Add(e.SourceId);
            }

            for (int i = 0; i < r.Units.Count; i++)
            {
                UnitSnapshot u = r.Units[i];
                if (!u.IsPlayer) continue;
                if (string.IsNullOrEmpty(u.ActiveAbilityName)) continue;
                if (deadIds.Contains(u.Id)) continue;
                if (castIds.Contains(u.Id)) continue;

                clause = new DebriefResult();
                clause.Clause = DebriefClause.UnspentActive;
                clause.Text = u.Name + " never got to use " + u.ActiveAbilityName + " - the fight ended before their energy did.";
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ 7. fallback

        /// <summary>Always produces a clause: the first player Death, if any, plus the largest single hit. Never fails.</summary>
        private static DebriefResult Fallback(BattleResult r)
        {
            int deathRound = -1;
            string victimName = null;
            string killerName = null;
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type != BattleEventType.Death) continue;
                UnitSnapshot target = r.FindUnit(e.TargetId);
                if (target == null || !target.IsPlayer) continue;
                deathRound = e.Round;
                victimName = target.Name;
                UnitSnapshot killer = r.FindUnit(e.SourceId);
                killerName = killer != null ? killer.Name : "the enemy";
                break;
            }

            int bestValue = -1;
            string bestVictim = null;
            string bestSource = null;
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type != BattleEventType.Damage) continue;
                UnitSnapshot target = r.FindUnit(e.TargetId);
                if (target == null || !target.IsPlayer) continue;
                if (e.Value <= bestValue) continue;
                bestValue = e.Value;
                bestVictim = target.Name;
                UnitSnapshot source = r.FindUnit(e.SourceId);
                bestSource = source != null ? source.Name : "an enemy";
            }
            if (bestValue < 0)
            {
                // the player took no damage at all - fall back to the biggest hit dealt, if any
                for (int i = 0; i < r.Events.Count; i++)
                {
                    BattleEvent e = r.Events[i];
                    if (e.Type != BattleEventType.Damage || e.Value <= bestValue) continue;
                    bestValue = e.Value;
                    UnitSnapshot target = r.FindUnit(e.TargetId);
                    bestVictim = target != null ? target.Name : "the enemy";
                    UnitSnapshot source = r.FindUnit(e.SourceId);
                    bestSource = source != null ? source.Name : "an enemy";
                }
            }

            DebriefResult result = new DebriefResult();
            result.Clause = DebriefClause.Fallback;
            if (deathRound >= 0 && bestValue >= 0)
                result.Text = victimName + " fell in round " + deathRound + " to " + killerName
                             + ". The biggest single hit was " + bestValue + ", from " + bestSource + ".";
            else if (deathRound >= 0)
                result.Text = victimName + " fell in round " + deathRound + " to " + killerName + ".";
            else if (bestValue >= 0)
                result.Text = "The biggest single hit was " + bestValue + ", dealt to " + bestVictim + " by " + bestSource + ".";
            else
                result.Text = "A quiet fight - no notable hits either way.";
            return result;
        }

        private static int Pct(float ratio)
        {
            return (int)Math.Round(ratio * 100f, MidpointRounding.AwayFromZero);
        }
    }
}
