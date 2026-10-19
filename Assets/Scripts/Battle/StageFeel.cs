using System;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Game;

namespace Emberfall.Battle
{
    /// <summary>
    /// THE R15 "v2" SWITCH, as the stage half of the battle view sees it, and the one place the
    /// stage reads who a unit looks like.
    ///
    /// v2 is: units 20 percent larger on a re-solved formation (BattleFraming, BattleStage),
    /// finished enemy art standing in for placeholders in chapters 1 and 2, and every unit facing
    /// its opponent (StagePresentation). v1 is R12 exactly, and every v2 path below returns the
    /// original object when v2 is off, so the rollback is not a similar look but the same code
    /// and the same data.
    ///
    /// WHERE IT COMES FROM. vfx.json "feelPreset" ("v2" shipped; a missing key reads "v1", what an
    /// old file meant). In the editor or a development build EMBERFALL_FEEL=v1 or =v2 forces one
    /// launch, and ForceForCapture does the same for one capture target (the ".v1" suffix); a
    /// release build reads the file alone. This is the stage's own reader of the same key the effects
    /// half reads, kept apart on purpose so neither half has to wait for the other.
    /// </summary>
    public static class StageFeel
    {
        public const string EnvVar = "EMBERFALL_FEEL";

        private static bool? _forced;
        private static bool? _fromFile;
        private static StagePresentation _config;

        /// <summary>Test seam: where a stand-in's look comes from instead of the content database.</summary>
        public static Func<string, VisualDef> StandInLookOverride;

        public static bool V2
        {
            get
            {
                if (_forced != null) return _forced.Value;
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    string env = Environment.GetEnvironmentVariable(EnvVar);
                    if (env == "v1") return false;
                    if (env == "v2") return true;
                }
                if (_fromFile == null) _fromFile = ReadFile();
                return _fromFile.Value;
            }
        }

        /// <summary>Capture seam: true forces v2, false forces v1, null hands control back to the
        /// file. Statics survive a no-domain-reload session, so a target that forces it resets it.</summary>
        public static void ForceForCapture(bool? v2) { _forced = v2; }

        /// <summary>Test seam: drop everything cached so the next read starts again.</summary>
        public static void Reset()
        {
            _forced = null;
            _fromFile = null;
            _config = null;
        }

        private static bool ReadFile()
        {
            TextAsset asset = Resources.Load<TextAsset>("Content/vfx");
            if (asset == null) return false;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(asset.text, out root, out err) || !root.IsObject) return false;
            return root.GetString("feelPreset", "v1") == "v2";
        }

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

        /// <summary>The VisualDef a unit's rig is built from: the snapshot's own in v1, the same
        /// object, and the v2 resolution otherwise.</summary>
        public static VisualDef Resolve(UnitSnapshot snapshot)
        {
            bool v2 = V2;
            if (!v2) return snapshot.Visual;
            return Config.Resolve(snapshot.DefId, snapshot.Visual, StandInLook, true);
        }
    }
}
