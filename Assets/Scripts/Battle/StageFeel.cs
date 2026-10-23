using System;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Game;

namespace Emberfall.Battle
{
    /// <summary>
    /// THE R15 "v2" SWITCH, as the stage half of the battle view asks it, and the one place the
    /// stage reads who a unit looks like.
    ///
    /// v2 is: units 20 percent larger on a re-solved formation (BattleFraming, BattleStage),
    /// finished enemy art standing in for placeholders in chapters 1 and 2, and every unit facing
    /// its opponent (StagePresentation). v1 is R12 exactly, and every v2 path below returns the
    /// original object when v2 is off, so the rollback is not a similar look but the same code
    /// and the same data.
    ///
    /// There is ONE switch and this class does not own it: V2 is the effects half's Feel.V2
    /// (vfx.json "feelPreset", EMBERFALL_FEEL, the .v1 and .v2 capture suffixes, and the R12 juice
    /// rollback, which takes the whole pass with it). stage-v2.json is read only once that says v2.
    /// </summary>
    public static class StageFeel
    {
        private static StagePresentation _config;

        /// <summary>Test seam: where a stand-in's look comes from instead of the content database.</summary>
        public static Func<string, VisualDef> StandInLookOverride;

        public static bool V2 { get { return Feel.V2; } }

        public static StagePresentation Config
        {
            get
            {
                if (_config == null)
                {
                    TextAsset asset = Resources.Load<TextAsset>(StagePresentation.ResourcePath);
                    _config = asset != null ? StagePresentation.FromJson(asset.text) : StagePresentation.Empty;
                    for (int i = 0; i < _config.Warnings.Count; i++)
                        Debug.LogWarning("Emberfall content: " + _config.Warnings[i]);
                }
                return _config;
            }
        }

        private static VisualDef StandInLook(string enemyId)
        {
            if (StandInLookOverride != null) return StandInLookOverride(enemyId);
            GameApp app = GameApp.Instance;
            if (app == null || app.Content == null) return null;
            EnemyDef def = app.Content.GetEnemy(enemyId);
            return def != null ? def.Visual : null;
        }

        /// <summary>The clips of this unit whose facing v2 inverts, or null (always null in v1).</summary>
        public static HashSet<string> ClipFlips(string defId)
        {
            if (!V2 || defId == null) return null;
            HashSet<string> set;
            return Config.ClipFlip.TryGetValue(defId, out set) ? set : null;
        }

        /// <summary>A unit's own factor on the formation's common height: stage-v2.json's scaleMul in v2
        /// (the Carrion Vulture's wings are too wide for the height every unit shares), 1 otherwise.</summary>
        public static float FormationMul(string defId)
        {
            float mul;
            return V2 && defId != null && Config.ScaleMul.TryGetValue(defId, out mul) ? mul : 1f;
        }

        /// <summary>The VisualDef a unit's rig is built from: the snapshot's own in v1, the same
        /// object, and the v2 resolution otherwise. stage-v2.json is not touched in v1.</summary>
        public static VisualDef Resolve(UnitSnapshot snapshot)
        {
            if (!V2) return snapshot.Visual;
            return Config.Resolve(snapshot.DefId, snapshot.Visual, StandInLook, true);
        }
    }
}
