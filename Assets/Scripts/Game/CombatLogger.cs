#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

namespace Emberfall.Game
{
    /// <summary>
    /// Writes a human-readable combat log to {project}/Logs/combat/ after each battle.
    /// Editor-only: the whole file is stripped by #if UNITY_EDITOR in non-editor builds.
    /// Logs/ is gitignored at the project root, so nothing written here is ever committed.
    /// Wired up from GameApp.Awake under a matching UNITY_EDITOR guard.
    /// </summary>
    internal static class CombatLogger
    {
        public static void Write(BattleResult r)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/combat"));
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string file = r.StageId + "_" + r.Seed + "_" + stamp + ".txt";
                string path = Path.Combine(dir, file);
                File.WriteAllText(path, Format(r), new UTF8Encoding(false));
                Debug.Log("Emberfall: combat log -> " + path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: combat log write failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ formatter

        private static string Format(BattleResult r)
        {
            StringBuilder sb = new StringBuilder(8192);

            // Header
            sb.AppendLine("=== EMBERFALL COMBAT LOG ===");
            sb.AppendLine("Stage    " + r.StageId);
            sb.AppendLine("Seed     " + r.Seed);
            string outcome = r.Victory ? "VICTORY" : "DEFEAT";
            if (r.TimedOut) outcome += "  [TIMED OUT]";
            sb.AppendLine("Result   " + outcome
                + "  " + r.Rounds + " rounds"
                + "  player HP " + (int)(r.PlayerHpRatio * 100f) + "%");
            sb.AppendLine();

            // Roster
            sb.AppendLine("ROSTER");
            for (int i = 0; i < r.Units.Count; i++)
            {
                UnitSnapshot u = r.Units[i];
                string tag  = u.IsPlayer ? "[HERO ]" : "[ENEMY]";
                string kind = u.IsPlayer ? u.Role.ToString() : (u.IsBoss ? "boss" : "enemy");
                sb.AppendLine("  " + tag + "  " + PadR(u.Name, 24)
                    + PadR(u.Aspect.ToString(), 9)
                    + PadR(kind, 10)
                    + "Lv" + u.Level
                    + "  slot " + u.Slot);
            }

            // Events
            for (int i = 0; i < r.Events.Count; i++)
                AppendEvent(sb, r, r.Events[i]);

            return sb.ToString();
        }

        private static void AppendEvent(StringBuilder sb, BattleResult r, BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.BattleStart:
                    // roster already printed above; no per-event line needed
                    break;

                case BattleEventType.RoundStart:
                    sb.AppendLine();
                    sb.AppendLine("──────────────────────────────── Round " + e.Round + " ────────────────────────────────");
                    break;

                case BattleEventType.TurnStart:
                    sb.AppendLine("  Turn    " + N(r, e.SourceId));
                    break;

                case BattleEventType.BasicAttack:
                    sb.AppendLine("  Basic   " + N(r, e.SourceId) + " uses " + e.AbilityName);
                    break;

                case BattleEventType.AbilityCast:
                    sb.AppendLine("  Active  " + N(r, e.SourceId) + " uses " + e.AbilityName);
                    break;

                case BattleEventType.PassiveTrigger:
                    sb.AppendLine("  Passive " + N(r, e.SourceId) + "'s " + e.AbilityName + " triggers");
                    break;

                case BattleEventType.Damage:
                {
                    string line = "  Damage  " + N(r, e.SourceId) + " -> " + N(r, e.TargetId) + "  " + e.Value + " hp";
                    if (e.Critical) line += "  [CRIT]";
                    if (e.AspectFactor > 1.005f)      line += "  [WEAK "   + e.AspectFactor.ToString("F2") + "x]";
                    else if (e.AspectFactor < 0.995f) line += "  [RESIST " + e.AspectFactor.ToString("F2") + "x]";
                    if (!string.IsNullOrEmpty(e.AbilityName)) line += "  (" + e.AbilityName + ")";
                    sb.AppendLine(line);
                    break;
                }

                case BattleEventType.Heal:
                {
                    string line = "  Heal    " + N(r, e.SourceId) + " -> " + N(r, e.TargetId) + "  +" + e.Value + " hp";
                    if (!string.IsNullOrEmpty(e.AbilityName)) line += "  (" + e.AbilityName + ")";
                    sb.AppendLine(line);
                    break;
                }

                case BattleEventType.ShieldGain:
                    sb.AppendLine("  Shield  " + N(r, e.TargetId) + "  +" + e.Value + " ward");
                    break;

                case BattleEventType.ShieldAbsorb:
                    sb.AppendLine("  Ward    " + N(r, e.TargetId) + " absorbed " + e.Value + " damage");
                    break;

                case BattleEventType.StatusApplied:
                    sb.AppendLine("  Status  " + N(r, e.TargetId) + " <- " + StatusInfo.DisplayName(e.Status) + " (" + e.Duration + "r)");
                    break;

                case BattleEventType.StatusResisted:
                    sb.AppendLine("  Resist  " + N(r, e.TargetId) + " resisted " + StatusInfo.DisplayName(e.Status));
                    break;

                case BattleEventType.StatusExpired:
                    sb.AppendLine("  Expire  " + StatusInfo.DisplayName(e.Status) + " on " + N(r, e.TargetId));
                    break;

                // SourceId is who the status came off, TargetId is who it landed on, Value is the
                // unit that applied it in the first place.
                case BattleEventType.StatusSpread:
                    sb.AppendLine("  Spread  " + StatusInfo.DisplayName(e.Status) + " jumps from "
                                  + N(r, e.SourceId) + " to " + N(r, e.TargetId)
                                  + " (" + e.Duration + "r, from " + N(r, e.Value) + ")");
                    break;

                case BattleEventType.EnergyChanged:
                {
                    // Value = unit's current energy (absolute). SourceId is the granter where one
                    // handed it over, and equals TargetId for a unit's own energy change.
                    string line = "  Energy  " + N(r, e.TargetId) + " -> " + e.Value;
                    if (e.SourceId != e.TargetId) line += "  [granted by " + N(r, e.SourceId) + "]";
                    sb.AppendLine(line);
                    break;
                }

                case BattleEventType.Death:
                    sb.AppendLine("  Death   " + N(r, e.TargetId) + "  (by " + N(r, e.SourceId) + ")");
                    break;

                // ExtraTurn and TargetAnchored are no longer emitted - Gale's kill re-entry and the
                // Lumen targeting anchor were their only sources, and both rules are gone. The arms
                // stay so an older log file replayed through this formatter still reads.
                case BattleEventType.ExtraTurn:
                    // Value = how many extra turns this unit has taken this round.
                    sb.AppendLine("  Extra   " + N(r, e.SourceId) + " re-enters turn order  [legacy, #" + e.Value + " this round]");
                    break;

                case BattleEventType.TurnOrderShift:
                    // Value = places moved, signed: negative advanced, positive delayed.
                    sb.AppendLine("  Shift   " + N(r, e.TargetId) + (e.Value < 0 ? " moved up " : " moved back ")
                                  + (e.Value < 0 ? -e.Value : e.Value) + " places");
                    break;

                case BattleEventType.TargetAnchored:
                    // Value = original TargetMode cast as int. TargetId = the unit that answered.
                    sb.AppendLine("  Anchor  " + N(r, e.TargetId) + " intercepts for its side  [legacy, original mode " + (TargetMode)e.Value + "]");
                    break;

                case BattleEventType.BattleEnd:
                    sb.AppendLine();
                    string bar = r.Victory
                        ? "═══════════════════════════════ VICTORY ═══════════════════════════════"
                        : "═══════════════════════════════  DEFEAT ═══════════════════════════════";
                    sb.AppendLine(bar);
                    break;

                default:
                    // Future event types (e.g. PositionSwapped from swap-positions-effectkind) fall
                    // here until an explicit case is added.
                    sb.AppendLine("  [" + e.Type + "]  src=" + N(r, e.SourceId)
                        + "  tgt=" + N(r, e.TargetId) + "  val=" + e.Value);
                    break;
            }
        }

        private static string N(BattleResult r, int id)
        {
            if (id < 0) return "-";
            UnitSnapshot u = r.FindUnit(id);
            return u != null ? u.Name : "Unit#" + id;
        }

        private static string PadR(string s, int width)
        {
            if (s == null) s = "";
            return s.Length >= width ? s : s + new string(' ', width - s.Length);
        }
    }
}
#endif
