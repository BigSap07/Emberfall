using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// The only thing in the game that creates or alters an armor piece's rolls (DESIGN-ARMOR
    /// sections 3-5 and 8). Everything here is seeded through DeterministicRng, so a drop and a
    /// reroll are both reproducible from an integer in a test - which is the only way a generator
    /// this branchy can be checked at all.
    ///
    /// Deliberately in Progression rather than in Session: it is a pure function of (config, seed)
    /// with no profile, no clock and no side effects. GameSession owns WHEN a piece is rolled and
    /// what happens to it afterwards; this owns only what the piece is.
    ///
    /// Reuses DeterministicRng rather than System.Random for the same reason the simulation does -
    /// same seed, same result, on any platform. It does NOT share an RNG instance with a battle:
    /// a drop is rolled after the fight has already resolved, from its own seed, so rolling one
    /// cannot move a single draw inside the simulation.
    /// </summary>
    public static class ArmorRoller
    {
        /// <summary>
        /// A complete piece for a slot at a temper. The uid is the caller's to assign - the roller
        /// does not know about the profile that will own it.
        ///
        /// Returns null when the config cannot roll (no primary table, no substat table, no
        /// tempers, or no primary range for this slot's guaranteed stat). Null is the "this build
        /// has no armor content" answer, not an error, exactly as a missing relics.json means a
        /// build with no relics.
        /// </summary>
        public static ArmorPiece Roll(ArmorConfig config, ArmorSlot slot, ArmorTemper temper, Aspect aspect, int seed)
        {
            if (config == null || !config.IsUsable) return null;

            ArmorRollDef primaryRange = config.GetPrimaryRoll(slot);
            if (primaryRange == null) return null;

            ArmorTemperDef temperDef = config.GetTemper(temper);
            float primaryScale = temperDef != null ? temperDef.PrimaryScale : 1f;

            DeterministicRng rng = new DeterministicRng(seed);

            ArmorPiece piece = new ArmorPiece();
            piece.Slot = slot;
            piece.Temper = temper;
            piece.Aspect = aspect;
            piece.Name = RollName(config, slot, rng);

            // The primary is chosen by the slot, never drawn, so it costs no RNG to decide - only
            // its magnitude does. ArmorInfo.PrimaryStat is the guarantee and this reads it rather
            // than restating the table.
            ArmorStat primaryStat = ArmorInfo.PrimaryStat(slot);
            float primaryValue = RollInRange(rng, primaryRange) * primaryScale;
            piece.Primary = new ArmorStatRoll(primaryStat, ArmorInfo.ModeFor(primaryStat), primaryValue);

            RollSubStats(config, piece, rng);
            return piece;
        }

        /// <summary>
        /// Rerolls every substat on an existing piece in place, keeping its uid, name, slot,
        /// primary, aspect and temper. The Forge's whole Reroll operation.
        ///
        /// All-or-nothing by construction: there is no parameter for which lines to keep, because
        /// DESIGN-ARMOR section 7 rules out per-line locking - a lock turns the sink into a ratchet,
        /// and the sink is the point of the currency existing.
        ///
        /// Returns false and changes nothing when the config cannot roll, so a caller that has
        /// already charged the player can tell the difference between "rerolled" and "did nothing".
        /// GameSession.RerollArmor calls this BEFORE deducting Slag for exactly that reason.
        /// </summary>
        public static bool Reroll(ArmorConfig config, ArmorPiece piece, int seed)
        {
            if (config == null || !config.IsUsable || piece == null) return false;

            DeterministicRng rng = new DeterministicRng(seed);
            piece.SubStats.Clear();
            RollSubStats(config, piece, rng);
            return true;
        }

        /// <summary>
        /// A temper for a drop at a stage depth. Each rung's weight is its base plus its
        /// per-stage-index slope, floored at 1 rather than at 0 - so a deep stage skews toward
        /// Honed without ever making Rough impossible. A rung that content deliberately disabled
        /// (dropWeight 0 and no slope) stays disabled: the floor applies to the computed weight,
        /// not to an authored zero.
        ///
        /// Never returns anything above ArmorInfo.MaxDropTemper, REGARDLESS of what armor.json
        /// authors for a rung above it. That filter runs before weights are even computed, so it is
        /// a hard ceiling rather than a side effect of the shipped content happening to zero out
        /// Gilded and Radiant's drop weight - a future content edit that gave Radiant a huge
        /// dropWeight would still never see it selected here. Gilded and Radiant exist only through
        /// GameSession.CombineArmor; see DESIGN-ARMOR section 8.
        /// </summary>
        public static ArmorTemper RollTemper(ArmorConfig config, int stageGlobalIndex, int seed)
        {
            if (config == null || config.Tempers.Count == 0) return ArmorTemper.Rough;

            DeterministicRng rng = new DeterministicRng(seed);
            int total = 0;
            int[] weights = new int[config.Tempers.Count];
            for (int i = 0; i < config.Tempers.Count; i++)
            {
                ArmorTemperDef t = config.Tempers[i];
                if ((int)t.Temper > (int)ArmorInfo.MaxDropTemper) { weights[i] = 0; continue; }
                if (t.DropWeight <= 0 && t.DropWeightPerStageIndex == 0f) { weights[i] = 0; continue; }
                float w = t.DropWeight + t.DropWeightPerStageIndex * Math.Max(0, stageGlobalIndex);
                weights[i] = w < 1f ? 1 : (int)Math.Round(w, MidpointRounding.AwayFromZero);
                total += weights[i];
            }
            if (total <= 0) return config.Tempers[0].Temper;

            int pick = rng.Range(0, total);
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= weights[i];
                if (pick < 0) return config.Tempers[i].Temper;
            }
            // Unreachable given how `pick` and `total` are constructed above - kept as a safe
            // fallback rather than trusted to be dead code, and it too respects the ceiling by
            // walking backward for the last rung with nonzero weight instead of returning the
            // list's literal last entry (which could be Radiant).
            for (int i = weights.Length - 1; i >= 0; i--)
                if (weights[i] > 0) return config.Tempers[i].Temper;
            return config.Tempers[0].Temper;
        }

        /// <summary>A uniform slot. Every slot is equally likely; depth tilts temper, not slot.</summary>
        public static ArmorSlot RollSlot(int seed)
        {
            DeterministicRng rng = new DeterministicRng(seed);
            return ArmorInfo.AllSlots[rng.Range(0, ArmorInfo.AllSlots.Length)];
        }

        /// <summary>
        /// A uniform aspect across all six. Deliberately NOT biased toward the stage's own aspect or
        /// the player's roster: either would quietly make attunement a reward for farming the right
        /// stage rather than a choice about what to equip, and attunement is the one place armor
        /// asks the player to decide something.
        /// </summary>
        public static Aspect RollAspect(int seed)
        {
            DeterministicRng rng = new DeterministicRng(seed);
            Array values = Enum.GetValues(typeof(Aspect));
            return (Aspect)values.GetValue(rng.Range(0, values.Length));
        }

        // ---------------------------------------------------------------- internals

        private static void RollSubStats(ArmorConfig config, ArmorPiece piece, DeterministicRng rng)
        {
            int wanted = ArmorInfo.SubStatCount(piece.Temper);

            // No duplicates WITHIN the substat list - four lines of Attack is a rounding error with
            // a UI, not a roll. The primary is deliberately not excluded: a Weapon rolling Attack as
            // a substat reads clearly, and forbidding it would make the best-in-slot roll depend on
            // which slot you were looking at. See DESIGN-ARMOR section 5.
            List<ArmorStat> taken = new List<ArmorStat>();

            for (int i = 0; i < wanted; i++)
            {
                ArmorRollDef chosen = PickWeighted(config.SubRolls, taken, rng);
                // Exhausted the pool - fewer distinct stats are authored than this temper wants
                // substats. Stopping short is right: repeating one to hit the count would break the
                // no-duplicates rule to satisfy a number, and content validation warns about it.
                if (chosen == null) break;

                taken.Add(chosen.Stat);
                ArmorStatMode mode = ArmorInfo.ModeFor(chosen.Stat);
                piece.SubStats.Add(new ArmorStatRoll(chosen.Stat, mode, RollInRange(rng, chosen)));
            }
        }

        /// <summary>
        /// Weighted draw over the substat table, skipping stats already on the piece. One pass to
        /// total the eligible weights and one to walk them, so the draw costs exactly one RNG value
        /// regardless of how many entries are excluded - a rejection loop would consume a
        /// content-dependent number of draws and make the seed meaningless.
        /// </summary>
        private static ArmorRollDef PickWeighted(List<ArmorRollDef> pool, List<ArmorStat> exclude, DeterministicRng rng)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (exclude.Contains(pool[i].Stat)) continue;
                total += pool[i].Weight;
            }
            if (total <= 0) return null;

            int pick = rng.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (exclude.Contains(pool[i].Stat)) continue;
                pick -= pool[i].Weight;
                if (pick < 0) return pool[i];
            }
            return null;
        }

        /// <summary>
        /// Uniform in [Min, Max]. A degenerate range (Min == Max) still draws, so that an authored
        /// fixed-value entry does not change how many RNG values a roll consumes relative to a
        /// ranged one - the alternative makes a piece's seed depend on the tuning of the entries it
        /// happened to draw.
        /// </summary>
        private static float RollInRange(DeterministicRng rng, ArmorRollDef range)
        {
            float t = rng.NextFloat();
            return range.Min + (range.Max - range.Min) * t;
        }

        /// <summary>
        /// A flavour name from the slot's pool, or the slot's own label when content authored none.
        /// Always draws, whether or not a pool exists, so an unnamed slot and a named one consume
        /// the same number of RNG values and a piece's stats do not shift when someone adds names.
        /// </summary>
        private static string RollName(ArmorConfig config, ArmorSlot slot, DeterministicRng rng)
        {
            List<string> pool = config.NamesFor(slot);
            int draw = rng.Range(0, pool != null && pool.Count > 0 ? pool.Count : 1);
            if (pool == null || pool.Count == 0) return ArmorInfo.SlotLabel(slot);
            return pool[draw];
        }
    }
}
