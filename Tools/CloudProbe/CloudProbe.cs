// Tools/CloudProbe - runs the REAL account, sync and save code against a fake engine.
//
// The harness in Tools/DevHarness compiles Core only, so it can prove what SyncPlanner decides and
// what SaveSerializer reads, but not what AccountService does with a token or what SaveService
// does with a file. Those live in Emberfall.Game and need UnityEngine. This probe compiles the six
// Game files the cloud and save paths are made of against FakeEngine.cs, scripts the server's
// answers, and asserts on what the code then does to the device's state.
//
// Every scenario is one entry in Docs/CLOUD-GAPS.md and is named after it. A scenario whose defect
// was fixed asserts the fixed behaviour. A scenario whose defect was written up and left alone
// asserts that the defect is STILL PRESENT, so the day somebody fixes it this goes red and the
// line in the doc gets moved, the same flipped-tripwire rule the harness uses.
//
//   powershell -File Tools/CloudProbe/run-cloud-probe.ps1
//
// Needs no network, no Unity and no Supabase project. Every URL is example.invalid.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;
using Emberfall.Game;
using Emberfall.Game.Cloud;

internal static class CloudProbe
{
    private static int _failures;
    private static readonly System.Collections.Generic.List<string> _made = new System.Collections.Generic.List<string>();
    private static ContentDatabase _content;
    private static string _root;

    private static int Main(string[] args)
    {
        _root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        _content = LoadContent(Path.Combine(_root, Path.Combine("Assets", Path.Combine("Resources", "Content"))));
        if (_content == null) { Console.WriteLine("could not load content"); return 1; }

        Console.WriteLine("EMBERFALL CLOUD PROBE - the real Game/Cloud and SaveService code, against a fake engine");

        SaveFallsBackToTheMirror();
        UnreadableSaveIsKeptAside();
        UnreadableDocumentIsNeverUploaded();
        LoserOfARaceIsTold();
        PendingEmailStaysAnonymous();
        NonSessionAnswerKeepsTheAccount();
        InvalidApiKeyStillDropsTheAccount();
        TransportFailureKeepsTheAccount();
        OfflineSendsNothing();
        SecretKeyIsRefused();

        for (int i = 0; i < _made.Count; i++)
        {
            try { Directory.Delete(_made[i], true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        Console.WriteLine("");
        Console.WriteLine(_failures == 0 ? "RESULT: all probe checks passed" : "RESULT: " + _failures + " probe check(s) FAILED");
        return _failures == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ save integrity

    /// <summary>CLOUD-GAPS P3. SaveService writes the file and then the PlayerPrefs mirror; a
    /// process killed inside File.WriteAllText leaves a partial file and an intact mirror. Load
    /// read the file, found it unreadable, and started a new game without looking at the mirror.</summary>
    private static void SaveFallsBackToTheMirror()
    {
        Section("P3  a partial save file with an intact PlayerPrefs mirror");
        FreshDevice();
        PlayerProfile profile = RichProfile();
        SaveService.Save(profile);
        string whole = File.ReadAllText(SaveService.FilePath);
        File.WriteAllText(SaveService.FilePath, whole.Substring(0, whole.Length / 2));

        PlayerProfile loaded = SaveService.Load(_content);
        Check(loaded != null, "Load recovers the save from the mirror instead of starting a new game");
        Check(loaded != null && loaded.Gold == profile.Gold && loaded.Heroes.Count == profile.Heroes.Count,
              "and it is the save that was written (gold " + (loaded != null ? loaded.Gold : -1) + " of " + profile.Gold + ")");
    }

    /// <summary>CLOUD-GAPS P1. A save this build cannot read - a newer build's, or one whose every
    /// hero has left content - made GameApp start fresh and SaveNow over it at once. The document
    /// is now copied aside before Load reports it unusable, so the overwrite no longer destroys it.</summary>
    private static void UnreadableSaveIsKeptAside()
    {
        Section("P1  a save this build refuses is kept, not overwritten");
        FreshDevice();
        string newer = SaveSerializer.Serialize(RichProfile()).Replace("\"version\": " + SaveSerializer.CurrentVersion,
                                                                       "\"version\": " + (SaveSerializer.CurrentVersion + 1));
        File.WriteAllText(SaveService.FilePath, newer);
        PlayerPrefs.SetString(SaveService.PrefsKey, newer);

        PlayerProfile loaded = SaveService.Load(_content);
        Check(loaded == null, "a save from a newer build is still refused, not read");
        string[] kept = Directory.GetFiles(Application.persistentDataPath, "emberfall_save.unreadable-*.json");
        Check(kept.Length == 1 && File.ReadAllText(kept[0]) == newer,
              "and a byte-for-byte copy of it is kept beside the save (" + kept.Length + " copy)");

        // What GameApp does next on a fresh start, and why the copy is the point.
        SaveService.Save(GameSession.CreateNewProfile(_content, 1700000000));
        Check(File.Exists(kept.Length > 0 ? kept[0] : "missing") && File.ReadAllText(kept[0]) == newer,
              "the fresh start's first save overwrites the live file and leaves the copy alone");
    }

    /// <summary>CLOUD-GAPS P2. CloudSaveCodec.WritePushBody replaced a document it could not parse
    /// with {} and push_save stored it, so a partial local file became an empty cloud save at a new
    /// revision, and the base revision moved to match.</summary>
    private static void UnreadableDocumentIsNeverUploaded()
    {
        Section("P2  a partial document is never sent to push_save");
        FreshDevice();
        SignedInSaves saves = SignIn();
        string whole = SaveSerializer.Serialize(RichProfile());
        string partial = whole.Substring(0, whole.Length / 3);
        saves.State.BaseRevision = 5;

        UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, Row(6, "{}")));
        int sentBefore = UnityWebRequest.Sent.Count;
        CloudSaveRecord pushed = null;
        string error = null;
        Run(saves.Service.Push(partial, SaveSerializer.CurrentVersion, 5,
            delegate (CloudSaveRecord r, string e) { pushed = r; error = e; }));

        bool reached = UnityWebRequest.Sent.Count > sentBefore;
        Check(!reached, "no request reached push_save" + (reached ? " - it sent: " + Clip(UnityWebRequest.Sent[UnityWebRequest.Sent.Count - 1].Body) : ""));
        Check(pushed == null && !string.IsNullOrEmpty(error), "the push reports a failure instead (" + error + ")");
        Check(saves.State.BaseRevision == 5, "and the base revision did not move (" + saves.State.BaseRevision + ")");
        UnityWebRequest.Script.Clear();
    }

    // ------------------------------------------------------------------ concurrency

    /// <summary>CLOUD-GAPS S1, client half. The loser of a push_save race gets a 409; it must be
    /// told and its base revision must not move, or the next sync would think it had won.</summary>
    private static void LoserOfARaceIsTold()
    {
        Section("S1  the loser of a push race, client side");
        FreshDevice();
        SignedInSaves saves = SignIn();
        saves.State.BaseRevision = 7;
        UnityWebRequest.Script.Enqueue(FakeResponse.Json(409,
            "{\"code\":\"PT409\",\"message\":\"stale revision: 8 is stored, 7 was sent\"}"));
        string error = null;
        CloudSaveRecord pushed = null;
        Run(saves.Service.Push(SaveSerializer.Serialize(RichProfile()), SaveSerializer.CurrentVersion, 7,
            delegate (CloudSaveRecord r, string e) { pushed = r; error = e; }));
        Check(pushed == null && error != null && error.Contains("moved"),
              "the losing push is reported, in words (" + error + ")");
        Check(saves.State.BaseRevision == 7, "and its base revision stays at 7, so the next sync asks");
    }

    // ------------------------------------------------------------------ identity

    /// <summary>CLOUD-GAPS P4. LinkEmail with confirmation on stores the PENDING address. The next
    /// refresh passed it to AdoptSession as if it were the account's email, which cleared
    /// IsAnonymous, which enabled Sign Out on an account nobody can sign back into.</summary>
    private static void PendingEmailStaysAnonymous()
    {
        Section("P4  an unconfirmed email does not make the account safe to sign out of");
        for (int variant = 0; variant < 2; variant++)
        {
            bool serverSendsFlag = variant == 0;
            string tag = serverSendsFlag ? " (server sends is_anonymous)" : " (older server, no is_anonymous)";
            FreshDevice();
            SupabaseClient client = Client();
            CloudLocalState state = CloudLocalState.Load();
            AccountService account = new AccountService(client, state);

            UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, Session("at-1", "rt-1", "u-anon", "", null, serverSendsFlag ? "true" : null)));
            Run(account.Resume(null));
            Check(account.SignedIn && state.IsAnonymous, "a first launch signs in anonymously" + tag);

            UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, User("u-anon", "", "me@example.com", serverSendsFlag ? "true" : null)));
            string linkNote = null;
            Run(account.LinkEmail("me@example.com", "hunter2-Aa1", delegate (bool ok, string m) { linkNote = m; }));
            Check(state.IsAnonymous && !account.CanSignOutSafely,
                  "linking with confirmation pending leaves it anonymous for now" + tag);

            // The relaunch: a new client and service over the same PlayerPrefs.
            SupabaseClient client2 = Client();
            CloudLocalState state2 = CloudLocalState.Load();
            AccountService account2 = new AccountService(client2, state2);
            UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, Session("at-2", "rt-2", "u-anon", "", "me@example.com", serverSendsFlag ? "true" : null)));
            Run(account2.Resume(null));
            Check(account2.SignedIn && state2.UserId == "u-anon", "the next launch refreshes the same account" + tag);
            Check(state2.IsAnonymous, "AND IT IS STILL ANONYMOUS - the address is only pending" + tag);
            Check(!account2.CanSignOutSafely, "so signing out is still refused" + tag);
            Check(state2.Email == "me@example.com", "while the pending address is still remembered for display" + tag);
        }
    }

    /// <summary>CLOUD-GAPS P5. A refresh answered 2xx with something that is not a session - a
    /// captive portal, a proxy - produced the client's own sentence "refresh returned no session",
    /// and TokenWasRejected matches on the word "refresh". The account was thrown away and a new
    /// anonymous one made, on a device that had simply not reached the auth server.</summary>
    private static void NonSessionAnswerKeepsTheAccount()
    {
        Section("P5  a 2xx that is not a session does not throw the account away");
        FreshDevice();
        SeedStoredAccount("u-real", "rt-real", true);
        CloudLocalState state = CloudLocalState.Load();
        AccountService account = new AccountService(Client(), state);
        UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, "<html><body>Sign in to the hotel wifi</body></html>"));
        UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, Session("at-new", "rt-new", "u-stranger", "", null, "true")));
        bool ok = true;
        Run(account.Resume(delegate (bool success, string m) { ok = success; }));
        Check(state.UserId == "u-real" && state.RefreshToken == "rt-real",
              "the stored account and refresh token survive (" + state.UserId + ")");
        Check(!ok, "and the sign-in is deferred rather than replaced");
        UnityWebRequest.Script.Clear();
    }

    /// <summary>CLOUD-GAPS P8, written up and not fixed. A gateway 401 "Invalid API key" - what every
    /// device on an old build sees after the anon key is rotated - still reads as "the token is
    /// dead", so each such device abandons its account. Asserted as still present.</summary>
    private static void InvalidApiKeyStillDropsTheAccount()
    {
        Section("P8  STILL PRESENT: a rotated anon key reads as a dead refresh token");
        FreshDevice();
        SeedStoredAccount("u-real", "rt-real", true);
        CloudLocalState state = CloudLocalState.Load();
        AccountService account = new AccountService(Client(), state);
        UnityWebRequest.Script.Enqueue(FakeResponse.Json(401, "{\"message\":\"Invalid API key\"}"));
        UnityWebRequest.Script.Enqueue(FakeResponse.Json(401, "{\"message\":\"Invalid API key\"}"));
        Run(account.Resume(null));
        Check(state.UserId == "" && state.RefreshToken == "",
              "STILL PRESENT - the stored account is cleared on an API-key refusal (user now '" + state.UserId + "')");
        UnityWebRequest.Script.Clear();
    }

    /// <summary>The control for P5 and P8: no signal must keep the account, and does.</summary>
    private static void TransportFailureKeepsTheAccount()
    {
        Section("control  no signal keeps the account");
        FreshDevice();
        SeedStoredAccount("u-real", "rt-real", true);
        CloudLocalState state = CloudLocalState.Load();
        AccountService account = new AccountService(Client(), state);
        UnityWebRequest.Script.Enqueue(FakeResponse.NoNetwork("Cannot resolve destination host"));
        Run(account.Resume(null));
        Check(state.UserId == "u-real" && state.RefreshToken == "rt-real", "a transport failure leaves the account alone");
    }

    // ------------------------------------------------------------------ offline

    /// <summary>CLOUD-GAPS "offline": with no supabase.json the config is unconfigured, and every
    /// path that could reach the network stops before building a request.</summary>
    private static void OfflineSendsNothing()
    {
        Section("offline  no supabase.json means no request, from any entry point");
        FreshDevice();
        Resources.Files.Clear();
        SupabaseConfig config = SupabaseConfig.Load();
        Check(!config.Configured, "with no Config/supabase the config is not configured (" + config.Problem + ")");

        int before = UnityWebRequest.Sent.Count;
        SupabaseClient client = new SupabaseClient(config, new MonoBehaviour());
        CloudLocalState state = CloudLocalState.Load();
        AccountService account = new AccountService(client, state);
        CloudSaveService saves = new CloudSaveService(client, account, state);
        Run(account.Resume(null));
        Run(account.SignInAnonymously(null));
        Run(account.RefreshSession(null));
        Run(account.SignInWithEmail("a@example.com", "x", null));
        Run(saves.Sync("{}", null));
        Run(client.Send("GET", "rest/v1/saves", null, null));
        Check(UnityWebRequest.Sent.Count == before,
              "not one request was built, even when the services are driven directly (" + (UnityWebRequest.Sent.Count - before) + ")");
    }

    /// <summary>CLOUD-GAPS P12. The service role key bypasses RLS. Nothing stopped one being pasted
    /// into supabase.json, and the Python guard meant to catch it could never fire.</summary>
    private static void SecretKeyIsRefused()
    {
        Section("P12  a service-role or secret key is refused by the game's config loader");
        string header = B64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string service = header + "." + B64Url("{\"iss\":\"supabase\",\"ref\":\"example\",\"role\":\"service_role\"}") + ".sig";
        string anon = header + "." + B64Url("{\"iss\":\"supabase\",\"ref\":\"example\",\"role\":\"anon\"}") + ".sig";

        Check(!service.Contains("service_role"),
              "the literal text 'service_role' never appears in a service-role JWT - why the old Python guard was dead");

        Resources.Files[SupabaseConfig.ResourcePath] = "{\"url\":\"https://example.invalid\",\"anonKey\":\"" + service + "\"}";
        SupabaseConfig refused = SupabaseConfig.Load();
        Check(!refused.Configured, "a service-role JWT leaves the game unconfigured, so it is never sent");
        Check(refused.Problem.Length > 0 && refused.Problem.IndexOf(service, StringComparison.Ordinal) < 0,
              "and the problem sentence does not repeat the key");

        // Split so this file does not itself trip CloudChecks' scan of the tree for secret keys.
        Resources.Files[SupabaseConfig.ResourcePath] = "{\"url\":\"https://example.invalid\",\"anonKey\":\"" + "sb_" + "secret_0123456789abcdef" + "\"}";
        Check(!SupabaseConfig.Load().Configured, "and so does a new-format sb_secret_ key");

        Resources.Files[SupabaseConfig.ResourcePath] = "{\"url\":\"https://example.invalid\",\"anonKey\":\"" + anon + "\"}";
        Check(SupabaseConfig.Load().Configured, "while an anon JWT configures normally");
        Resources.Files[SupabaseConfig.ResourcePath] = "{\"url\":\"https://example.invalid\",\"anonKey\":\"sb_publishable_0123456789abcdef\"}";
        Check(SupabaseConfig.Load().Configured, "and so does a new-format sb_publishable_ key");
        Resources.Files.Clear();
    }

    // ------------------------------------------------------------------ plumbing

    private sealed class SignedInSaves
    {
        public CloudLocalState State;
        public CloudSaveService Service;
    }

    private static SignedInSaves SignIn()
    {
        SupabaseClient client = Client();
        CloudLocalState state = CloudLocalState.Load();
        AccountService account = new AccountService(client, state);
        UnityWebRequest.Script.Enqueue(FakeResponse.Json(200, Session("at-1", "rt-1", "u-1", "", null, "true")));
        Run(account.Resume(null));
        SignedInSaves s = new SignedInSaves();
        s.State = state;
        s.Service = new CloudSaveService(client, account, state);
        return s;
    }

    private static SupabaseClient Client()
    {
        SupabaseConfig config = new SupabaseConfig();
        config.Url = "https://example.invalid";
        config.AnonKey = "probe-anon-key";
        return new SupabaseClient(config, new MonoBehaviour());
    }

    private static void SeedStoredAccount(string userId, string refresh, bool anonymous)
    {
        CloudLocalState seeded = CloudLocalState.Load();
        seeded.UserId = userId;
        seeded.RefreshToken = refresh;
        seeded.IsAnonymous = anonymous;
        seeded.Save();
    }

    private static void FreshDevice()
    {
        PlayerPrefs.Store.Clear();
        UnityWebRequest.Script.Clear();
        string dir = Path.Combine(Path.GetTempPath(), "emberfall-cloud-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _made.Add(dir);
        Application.persistentDataPath = dir;
    }

    private static PlayerProfile RichProfile()
    {
        PlayerProfile profile = GameSession.CreateNewProfile(_content, 1700000000);
        GameSession session = new GameSession(_content, profile);
        session.LevelUpHero(profile.Heroes[0], 6);
        session.GrantBattleResult(_content.AllStages[0], true, 1700000100);
        profile.Gold = 123456;
        return profile;
    }

    private static string Session(string access, string refresh, string userId, string email, string newEmail, string isAnonymous)
    {
        long expires = TimeUtil.NowUnix() + 3600;
        return "{\"access_token\":\"" + access + "\",\"token_type\":\"bearer\",\"expires_in\":3600,\"expires_at\":" + expires
             + ",\"refresh_token\":\"" + refresh + "\",\"user\":" + User(userId, email, newEmail, isAnonymous) + "}";
    }

    private static string User(string userId, string email, string newEmail, string isAnonymous)
    {
        return "{\"id\":\"" + userId + "\",\"email\":\"" + email + "\""
             + (newEmail != null ? ",\"new_email\":\"" + newEmail + "\"" : "")
             + (isAnonymous != null ? ",\"is_anonymous\":" + isAnonymous : "") + "}";
    }

    private static string Row(int revision, string data)
    {
        return "{\"user_id\":\"u-1\",\"save_version\":" + SaveSerializer.CurrentVersion + ",\"revision\":" + revision
             + ",\"device_id\":\"d\",\"data\":" + data + ",\"updated_at\":\"2026-09-22T00:00:00+00:00\"}";
    }

    private static string B64Url(string text)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static void Run(IEnumerator routine) { FakeRunner.RunToEnd(routine); }

    private static string Clip(string s) { return s == null ? "null" : (s.Length > 120 ? s.Substring(0, 120) + "..." : s); }

    private static void Section(string title) { Console.WriteLine(""); Console.WriteLine(title); }

    private static void Check(bool condition, string label)
    {
        if (!condition) _failures++;
        Console.WriteLine((condition ? "  [ok]   " : "  [FAIL] ") + label);
    }

    private static ContentDatabase LoadContent(string dir)
    {
        try
        {
            Func<string, string> read = delegate (string name)
            {
                string path = Path.Combine(dir, name);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            };
            return ContentDatabase.Load(read("heroes.json"), read("enemies.json"), read("campaign.json"),
                                        read("balance.json"), read("relics.json"), read("events.json"), read("armor.json"));
        }
        catch (Exception ex)
        {
            Console.WriteLine("content: " + ex.Message);
            return null;
        }
    }
}
