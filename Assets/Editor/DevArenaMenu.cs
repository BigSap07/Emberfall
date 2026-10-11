#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Opens the Dev Arena, and keeps it out of anything a player could install.
    ///
    /// TWO GATES, AND THE DEFINE IS THE ONE THAT MATTERS.
    ///
    /// 1. Every file under Assets/Scripts/Dev is wrapped in
    ///    `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. That is a COMPILER gate: a release player does
    ///    not contain the classes, not because a script chose not to include them but because they
    ///    were never compiled. Nothing at build time can undo it and no custom build pipeline can
    ///    forget it.
    /// 2. Assets/Dev/DevArena.unity is not in EditorBuildSettings in the committed state, and the
    ///    preprocessor below adds it ONLY when the build carries BuildOptions.Development, then
    ///    takes it out again afterwards. That is the convenience half - it is what lets a dev APK
    ///    have the scene without anyone remembering to add it - and it is deliberately not the
    ///    guarantee, because a build-time script is exactly the kind of thing that gets bypassed.
    ///
    /// Tools/build-check.ps1 asserts both from the files, so "release builds contain none of it"
    /// is a measurement rather than an intention.
    /// </summary>
    public static class DevArenaMenu
    {
        public const string ScenePath = "Assets/Dev/DevArena.unity";

        [MenuItem("Emberfall/Dev Arena", false, 10)]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorCaptureGuard.EnterPlayMode();
        }

        [MenuItem("Emberfall/Dev Arena (create or repair the scene)", false, 11)]
        public static void EnsureScene()
        {
            if (!Directory.Exists("Assets/Dev")) AssetDatabase.CreateFolder("Assets", "Dev");
            if (File.Exists(ScenePath)) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject host = new GameObject("DevArena");
            // Added by name so this editor file does not have to reference the Dev assembly's
            // types, which only exist under the development define.
            Type t = FindArenaType();
            if (t != null) host.AddComponent(t);
            else Debug.LogError("Emberfall: Emberfall.Dev.DevArena was not found - is the project "
                                + "compiled with UNITY_EDITOR or DEVELOPMENT_BUILD?");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("Emberfall: created " + ScenePath);
        }

        private static Type FindArenaType()
        {
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("Emberfall.Dev.DevArena");
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Adds the arena scene to the build for a Development Build and to nothing else, then
        /// puts the scene list back exactly as it was.
        /// </summary>
        public sealed class BuildHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport
        {
            public int callbackOrder { get { return 0; } }

            private static EditorBuildSettingsScene[] _restore;

            public void OnPreprocessBuild(BuildReport report)
            {
                _restore = EditorBuildSettings.scenes;
                bool development = (report.summary.options & BuildOptions.Development) != 0;
                if (!development)
                {
                    // Belt as well as braces: if somebody added it by hand, take it out.
                    EditorBuildSettings.scenes = Without(_restore, ScenePath);
                    return;
                }
                if (!File.Exists(ScenePath)) return;
                if (Contains(_restore, ScenePath)) return;

                List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(_restore);
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }

            public void OnPostprocessBuild(BuildReport report)
            {
                if (_restore != null) EditorBuildSettings.scenes = _restore;
                _restore = null;
            }

            private static bool Contains(EditorBuildSettingsScene[] scenes, string path)
            {
                for (int i = 0; i < scenes.Length; i++)
                    if (string.Equals(scenes[i].path, path, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            private static EditorBuildSettingsScene[] Without(EditorBuildSettingsScene[] scenes, string path)
            {
                List<EditorBuildSettingsScene> keep = new List<EditorBuildSettingsScene>();
                for (int i = 0; i < scenes.Length; i++)
                    if (!string.Equals(scenes[i].path, path, StringComparison.OrdinalIgnoreCase))
                        keep.Add(scenes[i]);
                return keep.ToArray();
            }
        }
    }
}
#endif
