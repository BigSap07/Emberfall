using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

namespace Emberfall.Battle
{
    /// <summary>
    /// WHICH CLIP A BATTLE BEAT PLAYS, decided from the event record and nothing else.
    ///
    /// The battle screen is a player for CombatSimulation's event list, so the choice of clip is
    /// too: every rule below reads events the simulation already emits, and none of them feeds
    /// anything back. A clip choice cannot change a target, a number or an outcome, because by the
    /// time a clip is chosen the whole fight has already been resolved.
    ///
    ///   idle          the rest loop, whenever nothing below applies.
    ///   basic, basic2 a BasicAttack event. A unit's basics alternate, counted in the record: its
    ///                 first, third, fifth basic play `basic` and its second, fourth play `basic2`.
    ///                 Nothing in the record distinguishes one basic from another - same ability,
    ///                 same targeting, same numbers - so the only honest thing a second swing can
    ///                 express is "this is not the same swing as last time".
    ///   skill         an AbilityCast event.
    ///   hit_reaction  a Damage event on the unit, unless the unit is mid-way through its own
    ///                 basic or skill clip, which it is never interrupted out of.
    ///   death         a Death event, held on its last frame.
    ///   controlled    the rest loop, in place of idle, while the unit carries a status that takes
    ///                 its turn away. The screen already mirrors every unit's statuses from
    ///                 StatusApplied / StatusExpired / StatusSpread, so this reads that mirror.
    ///                 Stun is the only such status the engine has: Silence still lets the unit
    ///                 act, so it is not control, and there is no channelled ability in the engine
    ///                 for a clip to belong to.
    ///
    /// A unit that never shipped a clip plays what it played before that clip was wired:
    /// `basic2` falls back to `basic`, `controlled` to `idle`, and a missing `skill` keeps the
    /// rest loop. Placeholder mannequins ship no clips at all and see none of this.
    ///
    /// Deliberately free of UnityEngine, so Tools/DevHarness compiles this file beside the core and
    /// checks the delivered sheets against the same vocabulary the battle actually requests.
    /// </summary>
    public static class BattleClips
    {
        public const string Idle = "idle";
        public const string Basic = "basic";
        public const string Basic2 = "basic2";
        public const string Skill = "skill";
        public const string HitReaction = "hit_reaction";
        public const string Death = "death";
        public const string Controlled = "controlled";

        /// <summary>
        /// Every clip some battle beat can request. A delivered sheet whose clip name is not in
        /// here is unreachable, which is what the harness's CheckEveryDeliveredSheetIsPlayed
        /// reports. Each entry must also be requested somewhere in the runtime code, and the
        /// harness checks that too, so this list cannot claim a clip nothing plays.
        /// </summary>
        public static readonly string[] Played = { Idle, Basic, Basic2, Skill, HitReaction, Death, Controlled };

        /// <summary>The clips that are the unit's own action. A hit reaction never cuts one short.</summary>
        public static bool IsAction(string clip)
        {
            return clip == Basic || clip == Basic2 || clip == Skill;
        }

        /// <summary>
        /// The clip to try when a unit never shipped this one, or null where the answer is "keep
        /// the rest loop". Each fallback is what that beat played before the clip was wired.
        /// </summary>
        public static string FallbackFor(string clip)
        {
            if (clip == Basic2) return Basic;
            if (clip == Controlled) return Idle;
            return null;
        }

        /// <summary>
        /// The basic clip the BasicAttack at events[index] asks for, by how many basics the same
        /// unit has already made in this record. Counted from the list rather than kept on the
        /// actor, so a unit that was off screen, dead and revived, or rebuilt mid-fight still
        /// alternates in the same place, and the Dev Arena and the battle screen agree.
        /// </summary>
        public static string BasicClipFor(IList<BattleEvent> events, int index)
        {
            int source = events[index].SourceId;
            int prior = 0;
            for (int i = 0; i < index; i++)
                if (events[i].Type == BattleEventType.BasicAttack && events[i].SourceId == source) prior++;
            return prior % 2 == 0 ? Basic : Basic2;
        }

        /// <summary>A status that takes the holder's turn away, which is what `controlled` shows.</summary>
        public static bool Controls(StatusKind kind)
        {
            return kind == StatusKind.Stun;
        }
    }
}
