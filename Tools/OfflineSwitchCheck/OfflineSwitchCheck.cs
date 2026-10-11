// Runs the REAL forced-offline switch (SupabaseConfig, GameApp.StartCloud, EditorCaptureGuard) against
// the offline Unity shim, with a config file standing in front of the loader, and proves that with
// the switch set the file is never read, no SupabaseClient is built and no cloud exists.
//
// Compiled and run by Tools/build-check.ps1 against EmberfallEngineCheck.dll (the engine-facing code,
// compiled with UNITY_EDITOR). No Unity, no network, and the real supabase.json is never opened: the
// "file" is a fake one registered with the shim, and its values are invented placeholders.
//
// Prints "[ok] ..." / "[FAIL] ..." lines; exits non-zero on any failure.
using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using Emberfall.EditorTools;
using Emberfall.Game;
using Emberfall.Game.Cloud;

public static class OfflineSwitchCheck
{
    private const string FakeUrl = "https://offline-switch-check.invalid";
    private const string FakeKey = "anon-key-for-the-offline-switch-check";

    private static int _failures;

    private static void Check(bool pass, string what)
    {
        Console.WriteLine((pass ? "[ok] " : "[FAIL] ") + "offline-switch: " + what);
        if (!pass) _failures++;
    }

    public static int Main()
    {
        Resources.Register(SupabaseConfig.ResourcePath,
            new TextAsset("{ \"url\": \"" + FakeUrl + "\", \"anonKey\": \"" + FakeKey + "\" }"));
        SessionState.SetBool(SupabaseConfig.ForceOfflineKey, false);

        // ---- control: the stand-in file loads as a configured backend when the switch is off, so
        // everything below that says "unconfigured" is the switch's doing and not a broken fixture.
        SupabaseConfig control = SupabaseConfig.Load();
        Check(control.Configured && control.Url == FakeUrl, "control: switch off, the stand-in file loads as configured");
        Check(SupabaseConfig.Current.Configured, "control: switch off, Current is configured (and is now cached)");

        // ---- the switch, through the one entry point tools use.
        EditorCaptureGuard.Arm();
        Check(SupabaseConfig.ForcedOffline, "EditorCaptureGuard.Arm sets SupabaseConfig.ForcedOffline");

        int loadsBefore = Resources.LoadCalls;
        SupabaseConfig forced = SupabaseConfig.Load();
        Check(!forced.Configured, "switch on: Load returns an unconfigured config even though the file is present");
        Check(Resources.LoadCalls == loadsBefore, "switch on: Load never asks Resources for the file (it is not read)");
        Check(forced.Url == "" && forced.AnonKey == "", "switch on: the config holds no url and no key");
        Check(forced.Problem == SupabaseConfig.ForcedOfflineProblem, "switch on: the problem sentence is the forced-offline one");
        Check(forced.Problem.IndexOf(FakeKey, StringComparison.Ordinal) < 0
              && forced.Problem.IndexOf(FakeUrl, StringComparison.Ordinal) < 0,
              "switch on: nothing from the file appears in anything the config says");
        Check(!SupabaseConfig.Current.Configured, "switch on: Current is unconfigured, ignoring the configured answer cached above");

        // ---- GameApp.StartCloud, the only place the client is built. Invoked on an uninitialised
        // GameApp (no Unity needed): it must take the unconfigured branch and build nothing.
        GameApp app = (GameApp)FormatterServices.GetUninitializedObject(typeof(GameApp));
        MethodInfo startCloud = typeof(GameApp).GetMethod("StartCloud", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(startCloud != null, "GameApp.StartCloud is where the check can reach the cloud boot");
        if (startCloud != null)
        {
            SupabaseConfig.ClientsConstructed = 0;
            loadsBefore = Resources.LoadCalls;
            Debug.LastLog = null;
            startCloud.Invoke(app, new object[] { false });
            Check(Debug.LastLog != null && Debug.LastLog.IndexOf(SupabaseConfig.ForcedOfflineProblem, StringComparison.Ordinal) >= 0,
                  "switch on: StartCloud ran and took the 'no cloud configured' branch, citing the forced-offline reason");
            Check(SupabaseConfig.ClientsConstructed == 0, "switch on: StartCloud constructed no SupabaseClient");
            Check(app.Cloud == null, "switch on: StartCloud left GameApp.Cloud null (no sign-in, no sync, no conflict chooser)");
            Check(Resources.LoadCalls == loadsBefore, "switch on: StartCloud never reached the file");
        }

        // ---- the switch is scoped to one Play session and does not poison later ones.
        EditorApplication.RaisePlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
        Check(SupabaseConfig.ForcedOffline, "entering Play mode leaves the switch set");
        EditorApplication.RaisePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
        Check(!SupabaseConfig.ForcedOffline, "returning to edit mode clears the switch");
        Check(SupabaseConfig.Current.Configured, "switch off again: Current is configured again (the offline answer was never cached)");

        Console.WriteLine(_failures == 0
            ? "offline-switch: all checks passed"
            : "offline-switch: " + _failures + " check(s) FAILED");
        return _failures == 0 ? 0 : 1;
    }
}
