using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.Game
{
    /// <summary>
    /// THE PURCHASE PATH'S HOST (R19, Docs/DESIGN-STORE.md): builds the PurchaseFlow for this build and keeps
    /// it ticking. A thin layer. Every rule that decides a grant is in Core (PurchaseGrants, PurchaseLedger,
    /// PurchaseFlow) and is proven by the harness without Unity; this file only chooses a store and gives the
    /// flow the live session, the clock and a save.
    ///
    /// WHICH STORE (StoreModes.Resolve, proven by CheckStoreModes):
    ///  - the Store switch off: nothing, no flow is built;
    ///  - the editor: Unity IAP's FAKE store when the package is compiled in (EMBERFALL_UNITY_IAP), else the
    ///    debug store; ForceDebugStore selects the scripted debug store for captures and tests;
    ///  - a development build: the DEBUG store (SimulatedStore). Unity IAP on a phone would reach the real
    ///    store, which this run must not;
    ///  - a release build with EMBERFALL_TESTSTORE (R26): the DEBUG store, purchases free, a TEST STORE banner on
    ///    the Store and the Events screen. Together with EMBERFALL_STORE_LIVE it falls back to COMING SOON;
    ///  - a release build: COMING SOON. No backend is built, so no buy path exists, whatever the screen draws.
    ///    The live store needs a release build, the IAP package and the define EMBERFALL_STORE_LIVE, which
    ///    nothing in this run sets.
    /// No real product id, key or receipt is in this file.
    /// </summary>
    public sealed class PurchaseService
    {
        public static PurchaseService Instance { get; private set; }

        /// <summary>Captures and tests set this to use the scripted debug store even in the editor.</summary>
        public static bool ForceDebugStore;

        /// <summary>Where the debug store keeps its account (its memory of purchases, for a restore on a
        /// new profile). Null means the default file in persistentDataPath.</summary>
        public static string DebugAccountPath;

        public PurchaseFlow Flow { get; private set; }
        public StoreMode Mode { get; private set; }

        /// <summary>The debug store when one is running, so a capture can approve or decline a held purchase.</summary>
        public SimulatedStore DebugStore { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            ForceDebugStore = false;
            DebugAccountPath = null;
        }

        /// <summary>What this build does with a purchase, from the switch and the build's own facts.</summary>
        public static StoreMode ModeOfThisBuild()
        {
            bool test = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            test = true;
#endif
            bool live = false;
#if EMBERFALL_STORE_LIVE
            live = true;
#endif
            bool iap = false;
#if EMBERFALL_UNITY_IAP
            iap = true;
#endif
            return StoreModes.Resolve(StoreSwitch.Enabled, test, live, iap, TestStoreSwitch.Enabled);
        }

        /// <summary>The one flow, built the first time the Store asks for it. The rules and offers are the
        /// Store's. Null when the Store is off.</summary>
        public static PurchaseService Ensure(IStoreRules rules, IList<StoreOffer> offers)
        {
            if (Instance != null) return Instance;
            StoreMode mode = ModeOfThisBuild();
            if (mode == StoreMode.Off || rules == null) return null;

            PurchaseService s = new PurchaseService();
            s.Mode = mode;
            IStoreBackend backend = null;
            if (mode == StoreMode.Test)
            {
#if EMBERFALL_UNITY_IAP && UNITY_EDITOR
                if (!ForceDebugStore) backend = new UnityIapBackend();
#endif
                if (backend == null) backend = s.MakeDebugStore();
            }
#if EMBERFALL_UNITY_IAP && EMBERFALL_STORE_LIVE
            else if (mode == StoreMode.Live) backend = new UnityIapBackend();
#endif
            s.DebugStore = backend as SimulatedStore;
            s.Flow = new PurchaseFlow(rules, backend, mode, CurrentSession, TimeUtil.NowUnix, SaveNow);
            Instance = s;
            s.Flow.Start(offers);
            if (backend != null) StoreTicker.Create();
            return s;
        }

        private static GameSession CurrentSession()
        {
            return GameApp.Instance == null ? null : GameApp.Instance.Session;
        }

        private static void SaveNow()
        {
            if (GameApp.Instance != null) GameApp.Instance.SaveNow();
        }

        private SimulatedStore MakeDebugStore()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || EMBERFALL_TESTSTORE
            SimulatedStore store = new SimulatedStore();
            string path = DebugAccountPath ?? Path.Combine(Application.persistentDataPath, "debug-store-account.json");
            store.LoadAccount = () =>
            {
                try { return File.Exists(path) ? File.ReadAllText(path) : null; }
                catch (Exception) { return null; }
            };
            store.SaveAccount = text =>
            {
                try { File.WriteAllText(path, text); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("Emberfall store: the debug store account was not saved: " + ex.Message); }
            };
            return store;
#else
            return null;
#endif
        }

        /// <summary>Per frame, from the ticker.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Flow != null) Flow.Tick(deltaSeconds);
        }
    }

    /// <summary>Ticks the purchase flow every frame. One hidden object, made with the service.</summary>
    public sealed class StoreTicker : MonoBehaviour
    {
        private static StoreTicker _instance;

        public static void Create()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("StoreTicker");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoreTicker>();
        }

        private void Update()
        {
            if (PurchaseService.Instance != null) PurchaseService.Instance.Tick(Time.unscaledDeltaTime);
        }
    }
}
