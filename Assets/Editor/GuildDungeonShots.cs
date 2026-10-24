#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.Game;
using Emberfall.Game.Cloud;
using Emberfall.UI;
using Emberfall.UI.Screens;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// A GUILD IN MEMORY: the second implementation of IGuildDungeonBackend, for the capture run.
    ///
    /// It holds what the server would hold - attempt rows and chat rows as JSON - and every record
    /// it hands back has been through the same codecs the live backend uses (WriteRpcArgs into a
    /// row, GuildDungeon.ReadRow out; a message row with the attempt embedded under "attempt",
    /// GuildCodec.ReadMessage out). So the screens driven against it read exactly the shapes they
    /// read in play.
    ///
    /// It also holds 0008's rules for guild_dungeon_submit - the cycle, the cap, the floor gate,
    /// the normalised line - because a stand-in that accepted anything would let the screens pass
    /// a flow the server refuses. It is not the proof of those rules; the live rules test is.
    ///
    /// Several members, one of them "you": Self is whoever the screens are acting as, and can be
    /// switched, which is how the run reads the chat as a second member.
    /// </summary>
    public sealed class LocalGuildDungeonBackend : IGuildDungeonBackend
    {
        public const string Guild = "7c1de0a4-5b2f-4e39-9a61-2f0c8d4e1b77";
        public string GuildTitle = "The Ember Wake";
        public string Self = "";
        public int Cycle = 38;
        public long StartsUnix;
        public long EndsUnix;
        public readonly Dictionary<string, string> Names = new Dictionary<string, string>();

        private readonly ContentDatabase _content;
        private readonly List<JsonValue> _rows = new List<JsonValue>();
        private readonly List<JsonValue> _messages = new List<JsonValue>();
        private long _nextAttempt = 5001;
        private long _nextMessage = 901;
        private long _clock;

        public LocalGuildDungeonBackend(ContentDatabase content, long now)
        {
            _content = content;
            StartsUnix = now - (long)(3.7 * 86400);
            EndsUnix = now + 3 * 86400 + 7 * 3600 + 20 * 60;
            _clock = now - 6 * 3600;
        }

        public bool Ready { get { return true; } }
        public string NotReadyReason { get { return ""; } }
        public string SelfUserId { get { return Self; } }
        public string GuildName { get { return GuildTitle; } }

        public IEnumerator Status(Action<GuildDungeonStatus, string> done)
        {
            JsonValue row = JsonValue.NewObject();
            row.Set("guild_id", Guild);
            row.Set("cycle", Cycle);
            row.Set("starts_at", Stamp(StartsUnix));
            row.Set("ends_at", Stamp(EndsUnix));
            row.Set("attempt_cap", _content.Balance.GuildDungeonAttemptsPerFloor);
            row.Set("floor_count", _content.Balance.GuildDungeonFloors);
            JsonValue array = JsonValue.NewArray();
            array.Add(row);
            done(GuildDungeonStatus.Read(JsonValue.Parse(array.ToJson(false))), "");
            yield break;
        }

        public IEnumerator ListAttempts(int cycle, Action<List<GuildDungeonAttempt>, string> done)
        {
            done(Attempts(cycle), "");
            yield break;
        }

        public List<GuildDungeonAttempt> Attempts(int cycle)
        {
            List<GuildDungeonAttempt> list = new List<GuildDungeonAttempt>();
            for (int i = 0; i < _rows.Count; i++)
            {
                GuildDungeonAttempt a = GuildDungeon.ReadRow(JsonValue.Parse(_rows[i].ToJson(false)));
                if (a.Cycle == cycle) list.Add(a);
            }
            return list;
        }

        public IEnumerator Record(GuildDungeonAttempt attempt, Action<GuildDungeonAttempt, string> done)
        {
            GuildDungeonAttempt stored;
            string error = SubmitAs(Self, attempt, out stored);
            done(stored, error);
            yield break;
        }

        /// <summary>guild_dungeon_submit, as 0008 writes it, for any member.</summary>
        public string SubmitAs(string userId, GuildDungeonAttempt record, out GuildDungeonAttempt stored)
        {
            stored = null;
            JsonValue args = JsonValue.Parse(GuildDungeon.WriteRpcArgs(record).ToJson(false));
            int floor = args.GetInt("p_floor", 0);
            if (args.GetInt("p_cycle", -1) != Cycle) return "that dungeon cycle is not the current one (" + Cycle + ")";
            if (floor < 1 || floor > _content.Balance.GuildDungeonFloors) return "a floor is 1 to " + _content.Balance.GuildDungeonFloors;
            List<JsonValue> team = args.GetList("p_team");
            if (team.Count < 1 || team.Count > 5) return "a team is one to five units";
            for (int i = 0; i < team.Count; i++)
                if (team[i].GetInt("level", -1) != _content.Balance.GuildDungeonHeroLevel || team[i].Has("relicId") || team[i].Has("armor"))
                    return "every hero fights the dungeon at level " + _content.Balance.GuildDungeonHeroLevel + " with no relic and no armor";
            if (string.IsNullOrEmpty(args.GetString("p_content_version", ""))) return "an attempt names the content it was fought under";
            List<GuildDungeonAttempt> all = Attempts(Cycle);
            if (!GuildDungeon.FloorOpen(all, Cycle, floor))
                return "floor " + floor + " opens when somebody in your guild clears floor " + (floor - 1);
            int used = GuildDungeon.AttemptsUsed(all, userId, Cycle, floor);
            if (used >= _content.Balance.GuildDungeonAttemptsPerFloor)
                return "no attempts left on floor " + floor + " this cycle";

            JsonValue row = JsonValue.NewObject();
            row.Set("id", _nextAttempt++);
            row.Set("guild_id", Guild);
            row.Set("user_id", userId);
            row.Set("cycle", Cycle);
            row.Set("floor", floor);
            row.Set("attempt", used + 1);
            row.Set("seed", args.GetInt("p_seed", 0));
            row.Set("team", args["p_team"]);
            row.Set("victory", args.GetBool("p_victory", false));
            row.Set("turns", args.GetInt("p_turns", 0));
            row.Set("power", args.GetInt("p_power", 0));
            row.Set("avg_stars", Math.Round(args.GetFloat("p_avg_stars", 0f), 2));
            row.Set("fallen", args.GetInt("p_fallen", 0));
            row.Set("content_version", args.GetString("p_content_version", ""));
            row.Set("created_at", Stamp(Tick()));
            JsonValue profile = JsonValue.NewObject();
            profile.Set("display_name", NameOf(userId));
            row.Set("profiles", profile);
            _rows.Add(row);
            stored = GuildDungeon.ReadRow(JsonValue.Parse(row.ToJson(false)));
            return "";
        }

        public IEnumerator Share(GuildDungeonAttempt attempt, Action<bool, string> done)
        {
            string error = ShareAs(Self, attempt);
            done(error.Length == 0, error);
            yield break;
        }

        /// <summary>guild_share_attempt: only your own attempt, in the guild it was fought for.</summary>
        public string ShareAs(string userId, GuildDungeonAttempt attempt)
        {
            JsonValue found = null;
            for (int i = 0; i < _rows.Count; i++)
                if (attempt != null && _rows[i].GetLong("id", 0) == attempt.Id && _rows[i].GetString("user_id", "") == userId)
                    found = _rows[i];
            if (found == null) return "you can only share your own attempts, in the guild they were fought for";

            JsonValue args = GuildDungeon.WriteShareArgs(attempt);
            JsonValue message = Message(userId, args.GetString("p_body", ""));
            message.Set("attempt_id", attempt.Id);
            // The embed, as the chat read's alias returns it: the row's own columns, no profile.
            JsonValue embedded = JsonValue.NewObject();
            foreach (string column in GuildDungeon.RowColumns.Split(','))
                embedded.Set(column, found[column]);
            message.Set("attempt", embedded);
            _messages.Add(message);
            return "";
        }

        public void SayAs(string userId, string text) { _messages.Add(Message(userId, text)); }

        private JsonValue Message(string userId, string body)
        {
            JsonValue m = JsonValue.NewObject();
            m.Set("id", _nextMessage++);
            m.Set("user_id", userId);
            m.Set("body", body);
            m.Set("created_at", Stamp(Tick()));
            JsonValue profile = JsonValue.NewObject();
            profile.Set("display_name", NameOf(userId));
            m.Set("profiles", profile);
            return m;
        }

        public IEnumerator FetchChat(Action<List<GuildMessageEntry>, string> done)
        {
            done(Chat(), "");
            yield break;
        }

        public List<GuildMessageEntry> Chat()
        {
            List<GuildMessageEntry> list = new List<GuildMessageEntry>();
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                GuildMessageEntry e = GuildCodec.ReadMessage(JsonValue.Parse(_messages[i].ToJson(false)), Self);
                if (e != null) list.Add(e);
            }
            return list;
        }

        public IEnumerator PostMessage(string typed, Action<bool, string> done)
        {
            string cleaned, problem;
            if (!GuildRules.ValidateMessage(typed, out cleaned, out problem)) { done(false, problem); yield break; }
            SayAs(Self, cleaned);
            done(true, "");
        }

        public string NameOf(string userId)
        {
            string name;
            return Names.TryGetValue(userId, out name) ? name : "Wayfarer";
        }

        private long Tick() { _clock += 17 * 60; return _clock; }

        private static string Stamp(long unix)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix)
                   .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "+00:00";
        }
    }

    /// <summary>
    /// A GUILD PATH IN MEMORY, for the same run: a ledger and an unlock list held the way 0011
    /// holds them, with 0011's four refusals on a purchase - unknown node, owned, requirement not
    /// owned, not enough Waymarks - so the screen is driven against the rules it meets in play.
    /// Like the guild above, not the proof of those rules; the harness pins the server's numbers.
    /// </summary>
    public sealed class LocalGuildPathBackend : IGuildPathBackend
    {
        private readonly ContentDatabase _content;
        public long Earned;
        public readonly List<string> Owned = new List<string>();

        /// <summary>Waymarks spent on guild tech rungs. 0012 subtracts the same thing from
        /// guild_path_balance, so the in-memory purse has to as well or the capture run would
        /// photograph money a player has already spent.</summary>
        public long Spent;

        public LocalGuildPathBackend(ContentDatabase content) { _content = content; }

        public bool Ready { get { return true; } }
        public string NotReadyReason { get { return ""; } }

        public long Balance
        {
            get
            {
                long spent = 0;
                for (int i = 0; i < Owned.Count; i++)
                {
                    GuildPathNodeDef n = _content.GuildPath.Node(Owned[i]);
                    if (n != null) spent += n.Cost;
                }
                return Earned - spent - Spent;
            }
        }

        public IEnumerator State(Action<GuildPathState, string> done)
        {
            done(Answer(), "");
            yield break;
        }

        public IEnumerator Unlock(string nodeId, Action<GuildPathState, string> done)
        {
            GuildPathNodeDef n = _content.GuildPath.Node(nodeId);
            if (n == null) { done(null, "there is no Path node called " + nodeId); yield break; }
            if (Owned.Contains(n.Id)) { done(null, "you already own " + n.Id); yield break; }
            if (!string.IsNullOrEmpty(n.Requires) && !Owned.Contains(n.Requires)) { done(null, n.Id + " needs " + n.Requires + " first"); yield break; }
            if (Balance < n.Cost) { done(null, n.Id + " costs " + n.Cost + " Waymarks and you have " + Balance); yield break; }
            Owned.Add(n.Id);
            done(Answer(), "");
        }

        /// <summary>Through the same reader the live answer goes through.</summary>
        private GuildPathState Answer()
        {
            JsonValue row = JsonValue.NewObject();
            row.Set("balance", Balance);
            row.Set("earned", Earned);
            JsonValue nodes = JsonValue.NewArray();
            for (int i = 0; i < Owned.Count; i++) nodes.Add(Owned[i]);
            row.Set("nodes", nodes);
            JsonValue array = JsonValue.NewArray();
            array.Add(row);
            return GuildPathState.Read(JsonValue.Parse(array.ToJson(false)));
        }
    }

    /// <summary>
    /// A GUILD TECH TREE IN MEMORY, for the same run: a guild level, a member count and a rung
    /// ledger held the way 0012 holds them, with 0012's refusals on a purchase - unknown track,
    /// every rung bought, the guild level too low, not enough Waymarks. Like the two above, not the
    /// proof of those rules; GuildTechChecks pins the server's numbers and the harness derives them.
    ///
    /// It shares the Path backend's purse deliberately, because 0012 does: a rung is bought out of
    /// the same Waymark balance a Path node was, and a capture run in which the two disagreed would
    /// photograph a screen no player can reach.
    /// </summary>
    public sealed class LocalGuildTechBackend : IGuildTechBackend
    {
        private readonly ContentDatabase _content;
        private readonly LocalGuildPathBackend _purse;
        public int Level;
        public int Members = 3;
        public bool CanPick = true;
        public readonly Dictionary<string, int> Rungs = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Picks = new Dictionary<string, string>();

        public LocalGuildTechBackend(ContentDatabase content, LocalGuildPathBackend purse)
        {
            _content = content;
            _purse = purse;
        }

        public bool Ready { get { return true; } }
        public string NotReadyReason { get { return ""; } }

        public IEnumerator State(Action<GuildTechState, string> done)
        {
            done(Answer(), "");
            yield break;
        }

        public IEnumerator BuyRung(string familyId, Action<GuildTechState, string> done)
        {
            GuildTechConfig tree = _content.GuildTech;
            GuildTechFamilyDef family = tree.Family(familyId);
            if (family == null) { done(null, "there is no guild tech track called " + familyId); yield break; }
            int have;
            Rungs.TryGetValue(familyId, out have);
            if (have >= tree.RungsPerFamily) { done(null, "every rung on " + familyId + " is bought"); yield break; }
            int want = have + 1, cost = tree.RungCost(want), needs = tree.LevelForRung(familyId, want);
            if (needs > Level) { done(null, "rung " + want + " on " + familyId + " needs guild level " + needs); yield break; }
            if (_purse.Balance < cost) { done(null, "rung " + want + " costs " + cost + " Waymarks and you have " + _purse.Balance); yield break; }
            Rungs[familyId] = want;
            _purse.Spent += cost;
            done(Answer(), "");
        }

        public IEnumerator PickTalent(string familyId, string talentId, Action<GuildTechState, string> done)
        {
            GuildTechConfig tree = _content.GuildTech;
            GuildTechFamilyDef family = tree.Family(familyId);
            if (family == null || family.Talent(talentId) == null) { done(null, talentId + " is not a talent on the " + familyId + " track"); yield break; }
            if (!CanPick) { done(null, "only a leader or an officer sets the guild talents"); yield break; }
            int slot = tree.LevelForTalentSlot(familyId);
            if (slot > Level) { done(null, "the " + familyId + " talent slot opens at guild level " + slot); yield break; }
            Picks[familyId] = talentId;
            done(Answer(), "");
        }

        /// <summary>Through the same reader the live answer goes through.</summary>
        private GuildTechState Answer()
        {
            GuildTechState s = new GuildTechState();
            s.Level = Level;
            s.Members = Members;
            s.CanPick = CanPick;
            s.Xp = _content.GuildTech.XpForLevel(Members) * Level;
            foreach (KeyValuePair<string, int> kv in Rungs) s.Rungs[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, string> kv in Picks) s.Talents[kv.Key] = kv.Value;
            return GuildTechState.Read(JsonValue.Parse(s.Write().ToJson(false)));
        }
    }

    /// <summary>
    /// PHOTOGRAPHS THE GUILD DUNGEON, AND DRIVES IT END TO END, on a scratch copy of the real save.
    ///
    /// Two members of one guild (you, from the copied save, and Mira; Oren is a third so the
    /// boards have something to rank). Mira and Oren's attempts are made through the same Core
    /// calls a client makes and stored through the in-memory guild's copy of 0008's rules. Then
    /// the run presses the real buttons: open the chat, WATCH Mira's lost fight, RUN IT YOURSELF,
    /// ATTEMPT with your own line, see the result, POST it, read the boards - and finally reads the
    /// chat AS MIRA and replays your fight from the card, which is the handover in the other
    /// direction. What it checked is written to Tools/Screenshots/guild-dungeon-e2e.txt.
    ///
    /// The whole flow runs once per aspect (1920x1080, then 2400x1080) on a fresh guild, so both
    /// sets of shots are of the same moments.
    ///
    /// THE CLOUD IS HELD OFF (SupabaseConfig.ForcedOffline, armed by EditorCaptureGuard.EnterPlayMode). A scratch identity has no session, and a
    /// configured build would sign a new anonymous account up on the live project for it.
    ///
    ///   Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.GuildDungeonShots.Run
    ///
    /// NOT -batchMode: ScreenCapture photographs the Game view, and batch mode has none.
    /// </summary>
    [InitializeOnLoad]
    public static class GuildDungeonShots
    {
        private const string ActiveKey = "GuildDungeonShots.Active";
        private const string RestoreKey = "GuildDungeonShots.Restore";
        private const string ExitKey = "GuildDungeonShots.ExitWhenDone";
        private const string OrigCompanyKey = "GuildDungeonShots.OrigCompany";
        private const string OrigProductKey = "GuildDungeonShots.OrigProduct";
        private const string OrigSuppressKey = "GuildDungeonShots.OrigSuppress";
        private const string LiveSaveKey = "GuildDungeonShots.LiveSave";
        private const string LiveHashKey = "GuildDungeonShots.LiveHash";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Guild Dungeon Shots";

        private const string Mira = "11111111-aaaa-4aaa-8aaa-111111111111";
        private const string Oren = "33333333-cccc-4ccc-8ccc-333333333333";
        private const string You = "22222222-bbbb-4bbb-8bbb-222222222222";

        private static readonly int[][] Sizes = { new[] { 1920, 1080 }, new[] { 2400, 1080 } };

        private sealed class Step
        {
            public string Name;
            public Action Do;
            public Func<bool> Ready;
            public int Settle = 20;
            public string Shot;
        }

        private static List<Step> _steps;
        private static int _stepIndex;
        private static int _sizeIndex;
        private static int _stage;
        private static int _wait;
        private static int _waited;
        private static string _pending;
        private static LocalGuildDungeonBackend _guild;
        private static LocalGuildPathBackend _path;
        private static LocalGuildTechBackend _tech;
        private static GuildDungeonAttempt _miraLoss;
        private static BattleResult _miraLossFight;
        private static readonly List<string> _report = new List<string>();
        private static int _failures;

        static GuildDungeonShots()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run()
        {
            SessionState.SetBool(ExitKey, true);
            Begin();
        }

        [MenuItem("Emberfall/Capture/Guild dungeon", false, 83)]
        public static void RunFromMenu()
        {
            SessionState.SetBool(ExitKey, false);
            Begin();
        }

        private static void Begin()
        {
            string liveSave = Path.Combine(Application.persistentDataPath, SaveService.FileName);
            SessionState.SetString(LiveSaveKey, liveSave);
            SessionState.SetString(LiveHashKey, HashOf(liveSave));

            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("GuildDungeonShots: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            Directory.CreateDirectory(dataPath);
            string save = Path.Combine(dataPath, SaveService.FileName);
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();
            if (File.Exists(liveSave)) File.Copy(liveSave, save);
            else Debug.LogWarning("GuildDungeonShots: no live save at '" + liveSave + "', so this runs on a fresh profile.");

            Directory.CreateDirectory(OutDir());
            if (!GameViewSize.Apply(Sizes[0][0], Sizes[0][1]))
                Debug.LogWarning("GuildDungeonShots: could not pin the Game view size.");

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            _stage = 0;
            EditorCaptureGuard.EnterPlayMode();
        }

        private static string OutDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/ui-shots"));
        }

        private static string ReportPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/Screenshots/guild-dungeon-e2e.txt"));
        }

        // ================================================================== the guild

        /// <summary>
        /// A fresh guild: three members, floor 1 cleared by Mira and Oren, and on floor 2 Mira's
        /// LOSS (shared to chat, the fight the handover is about) then her clear, and Oren's clear.
        /// Oren also shares a floor 1 fight stamped with an earlier content version, which the
        /// chat must show as unplayable rather than play.
        /// </summary>
        private static void SeedGuild(GameApp app)
        {
            ContentDatabase content = app.Content;
            _guild = new LocalGuildDungeonBackend(content, TimeUtil.NowUnix());
            _guild.Names[Mira] = "Mira";
            _guild.Names[Oren] = "Oren";
            _guild.Names[You] = string.IsNullOrEmpty(app.Session.Profile.PlayerName) ? "You" : app.Session.Profile.PlayerName;
            _guild.Self = You;
            GuildDungeonBackends.Override = _guild;

            // Three weeks of the dungeon at the numbers guild-path.json pays, less the Second Line
            // already bought: enough for one more node at depth two and not for two.
            _path = new LocalGuildPathBackend(content);
            GuildPathConfig pay = content.GuildPath;
            _path.Earned = 3 * (pay.PayAttempt + pay.PayClear) + 4 * pay.PayBoardPoint;
            _path.Owned.Add("second-line");
            GuildPathBackends.Override = _path;

            // A guild partway up the tree: past the four grants and the first talent slot, so the
            // shot shows a track with a rung to buy, one that is still level-gated, and a talent
            // slot that is open and running.
            _tech = new LocalGuildTechBackend(content, _path);
            _tech.Level = 5;
            _tech.Members = 3;
            if (content.GuildTech.Families.Count > 0 && content.GuildTech.Families[0].Talents.Count > 0)
                _tech.Picks[content.GuildTech.Families[0].Id] = content.GuildTech.Families[0].Talents[0].Id;
            // Enough for one rung and not for two, so BUY is live on one card and priced out on the
            // next - which is the state worth photographing.
            _path.Earned += content.GuildTech.RungCost(1) + content.GuildTech.RungCost(1) / 2;
            GuildTechBackends.Override = _tech;

            int seed = GuildDungeon.SeedFor(LocalGuildDungeonBackend.Guild, _guild.Cycle);
            DeterministicRng rng = new DeterministicRng(0x6D17A);
            List<HeroDef> all = new List<HeroDef>(content.Heroes);
            List<HeroDef> humble = all.FindAll(h => h.Stars <= 3 && h.Role != HeroRole.Mender);

            TeamSnapshot miraOne = Find(content, all, 5, rng, seed, 1, true);
            TeamSnapshot orenOne = Find(content, humble, 4, rng, seed, 1, true);
            Submit(content, Mira, miraOne, seed, 1);
            Submit(content, Oren, orenOne, seed, 1);

            TeamSnapshot miraLossLine = Find(content, all, 5, rng, seed, 2, false);
            _miraLoss = Submit(content, Mira, miraLossLine, seed, 2);
            _miraLossFight = GuildDungeon.Fight(content, miraLossLine, seed, 2);
            TeamSnapshot miraWin = Find(content, all, 5, rng, seed, 2, true);
            Submit(content, Mira, miraWin, seed, 2);
            TeamSnapshot orenWin = Find(content, humble, 4, rng, seed, 2, true);
            Submit(content, Oren, orenWin, seed, 2);

            // An old fight: the same record shape, stamped as if fought before a balance patch.
            BattleResult ignored;
            GuildDungeonAttempt old = GuildDungeon.Attempt(content, Find(content, all, 5, rng, seed, 1, true), seed, _guild.Cycle, 1, out ignored);
            old.ContentVersion = "c1-00000000000000aa";
            GuildDungeonAttempt oldStored;
            _guild.SubmitAs(Oren, old, out oldStored);

            _guild.SayAs(Oren, "Floor 1 is open for everyone. The warden up front soaks everything, go around it.");
            _guild.ShareAs(Oren, oldStored);
            _guild.SayAs(Mira, "Floor 2 beat me on my first try. Posting it, watch what the hunters do to my back line.");
            _guild.ShareAs(Mira, _miraLoss);

            Note("guild seeded: cycle " + _guild.Cycle + ", seed " + seed + ", content " + content.ContentVersion);
            Note("  Mira floor 2 attempt " + _miraLoss.Attempt + ": " + GuildDungeonFlow.OutcomeLine(_miraLoss)
                 + " with " + _miraLoss.Team.Units.Count + " heroes (shared)");
        }

        /// <summary>
        /// The seeded guild for the PlayModeScreenshot targets (GuildDungeon, GuildFloor, GuildDungeonResult,
        /// GuildDungeonBoards, GuildTech): SeedGuild, plus two clears by the player on floors 1 and 2 so the pips
        /// are lit, the floor rows carry the player's tick and the result has an attempt of its own. Scratch only.
        /// </summary>
        internal static void SeedForCapture(GameApp app)
        {
            SeedGuild(app);
            ContentDatabase content = app.Content;
            int seed = GuildDungeon.SeedFor(LocalGuildDungeonBackend.Guild, _guild.Cycle);
            DeterministicRng rng = new DeterministicRng(0x46C0DE);
            List<HeroDef> all = new List<HeroDef>(content.Heroes);
            Submit(content, You, Find(content, all, 5, rng, seed, 1, true), seed, 1);
            Submit(content, You, Find(content, all, 5, rng, seed, 2, true), seed, 2);
        }

        /// <summary>Puts the three in-memory backends away, as the end of the full run does.</summary>
        internal static void ClearCaptureOverrides()
        {
            GuildDungeonBackends.Override = null;
            GuildPathBackends.Override = null;
            GuildTechBackends.Override = null;
        }

        private static GuildDungeonAttempt Submit(ContentDatabase content, string user, TeamSnapshot line, int seed, int floor)
        {
            BattleResult fought;
            GuildDungeonAttempt record = GuildDungeon.Attempt(content, line, seed, _guild.Cycle, floor, out fought);
            GuildDungeonAttempt stored;
            string error = _guild.SubmitAs(user, record, out stored);
            Check(error.Length == 0, "seeding: " + _guild.NameOf(user) + " floor " + floor + " stored" + (error.Length > 0 ? " - " + error : ""));
            return stored;
        }

        /// <summary>A line of `size` drawn from `from` that clears (or loses) the floor. Drawn
        /// deterministically, so every run seeds the same guild.</summary>
        private static TeamSnapshot Find(ContentDatabase content, List<HeroDef> from, int size, DeterministicRng rng,
                                         int seed, int floor, bool wantClear)
        {
            TeamSnapshot line = null;
            // A loss is searched for at the asked size first, then with fewer heroes: on the
            // 72-stage campaign's pool no five-hero line at level 40 loses floor 2 in 400 tries.
            int shrinkTo = wantClear ? size : 1;
            for (int tries = 0; tries < 400 * (size - shrinkTo + 1); tries++)
            {
                if (tries > 0 && tries % 400 == 0) size--;
                List<HeroDef> pool = new List<HeroDef>(from);
                line = new TeamSnapshot();
                for (int i = 0; i < size && pool.Count > 0; i++)
                {
                    int pick = rng.Range(0, pool.Count);
                    line.Units.Add(new SnapshotUnit { DefId = pool[pick].Id, Level = 40, Stars = pool[pick].Stars });
                    pool.RemoveAt(pick);
                }
                // A line the floor's condition refuses would never have been committed.
                string broke;
                if (!GuildDungeon.LineLegal(content, GuildDungeon.ConditionOf(content, seed, floor), line, out broke)) continue;
                if (GuildDungeon.Fight(content, line, seed, floor).Victory == wantClear) return line;
            }
            Check(false, "seeding: found a line that " + (wantClear ? "clears" : "loses") + " floor " + floor);
            return line;
        }

        // ================================================================== the run

        private static List<Step> BuildSteps(GameApp app)
        {
            ScreenRouter router = app.Router;
            List<Step> steps = new List<Step>();

            steps.Add(new Step
            {
                Name = "entry",
                Do = delegate { router.Show(ScreenId.GuildDungeon); },
                Ready = delegate { return router.GuildDungeon.State.Loaded && !router.GuildDungeon.Loading; },
                Shot = "guild-dungeon"
            });
            steps.Add(new Step
            {
                Name = "chat",
                Do = delegate { router.Show(ScreenId.GuildChat); },
                Ready = delegate { return Find(router.GuildChat.Root, "Watch") != null; },
                Settle = 30,
                Shot = "guild-chat-card"
            });
            steps.Add(new Step
            {
                Name = "watch Mira's loss",
                Do = delegate { Check(Press(router.GuildChat.Root, "Watch"), "chat: the WATCH button on Mira's card is pressable"); },
                Ready = delegate { return router.Current == router.Battle && router.Battle.RoundUnderway; },
                Settle = 150,
                Shot = "guild-dungeon-replay"
            });
            steps.Add(new Step
            {
                Name = "replay to the end",
                Do = delegate { router.Battle.SkipToEndForCapture(); },
                Ready = delegate { return router.Battle.Finished && Find(router.Battle.Root, "GuildPrimary") != null; },
                Settle = 40,
                Shot = "guild-dungeon-replay-end"
            });
            steps.Add(new Step
            {
                Name = "run it yourself",
                Do = delegate
                {
                    CheckReplayWasTheFight(app);
                    Check(Press(router.Battle.Root, "GuildPrimary"), "replay: RUN IT YOURSELF is pressable");
                },
                Ready = delegate { return router.Current == router.GuildFloor && router.GuildDungeon.State.Loaded; },
                Settle = 30,
                Shot = "guild-dungeon-handover"
            });
            steps.Add(new Step
            {
                Name = "make the line fit the floor",
                Do = delegate
                {
                    GuildDungeonState state = router.GuildDungeon.State;
                    Check(state.Watched != null && state.Watched.Id == _miraLoss.Id && state.Floor == _miraLoss.Floor,
                          "handover: the floor view opened on Mira's floor, carrying the fight that was watched");
                    FitLineToCondition(app);
                },
                Ready = delegate { return router.Current == router.GuildFloor; },
                Settle = 20,
                Shot = "guild-dungeon-floor-condition"
            });
            steps.Add(new Step
            {
                Name = "attempt",
                Do = delegate
                {
                    Check(Press(router.GuildFloor.Root, "Attempt"), "floor: ATTEMPT is pressable");
                },
                Ready = delegate { return Find(app.OverlayLayer, "Confirm") != null; },
                Settle = 10
            });
            steps.Add(new Step
            {
                Name = "confirm",
                Do = delegate { Check(Press(app.OverlayLayer, "Confirm"), "floor: the confirm dialog's ATTEMPT is pressable"); },
                Ready = delegate { return router.Current == router.Battle && router.Battle.RoundUnderway; },
                Settle = 150,
                Shot = "guild-dungeon-fight"
            });
            steps.Add(new Step
            {
                Name = "fight to the end",
                Do = delegate { router.Battle.SkipToEndForCapture(); },
                Ready = delegate { return router.Battle.Finished && Find(router.Battle.Root, "GuildPrimary") != null; },
                Settle = 40,
                Shot = "guild-dungeon-fight-end"
            });
            steps.Add(new Step
            {
                Name = "result",
                Do = delegate { Check(Press(router.Battle.Root, "GuildPrimary"), "fight: SEE WHERE IT PLACES YOU is pressable"); },
                Ready = delegate { return router.Current == router.GuildDungeonResult; },
                Settle = 40,
                Shot = "guild-dungeon-result"
            });
            steps.Add(new Step
            {
                Name = "post to chat",
                Do = delegate
                {
                    CheckScored(app);
                    Check(Press(router.GuildDungeonResult.Root, "Share"), "result: POST TO GUILD CHAT is pressable");
                },
                Ready = delegate { return router.GuildDungeonResult.Shared; },
                Settle = 10
            });
            steps.Add(new Step
            {
                Name = "chat with your card",
                Do = delegate { router.Show(ScreenId.GuildChat); },
                Ready = delegate { return Count(router.GuildChat.Root, "Attempt") >= 3; },
                // Long enough for the "Posted" toast to fade: it covered the next two shots.
                Settle = 420,
                Shot = "guild-chat-posted"
            });
            steps.Add(new Step
            {
                Name = "boards",
                Do = delegate
                {
                    router.GuildDungeonBoards.SetFloor(_miraLoss.Floor);
                    router.Show(ScreenId.GuildDungeonBoards);
                },
                Ready = delegate { return router.GuildDungeon.State.Loaded && Find(router.GuildDungeonBoards.Root, "Entry0") != null; },
                Settle = 600,
                Shot = "guild-dungeon-boards"
            });
            steps.Add(new Step
            {
                Name = "a floor, opened from the entry",
                Do = delegate { router.GuildDungeon.OpenFloor(1); },
                Ready = delegate { return router.Current == router.GuildFloor; },
                Settle = 30,
                Shot = "guild-dungeon-floor"
            });
            steps.Add(new Step
            {
                Name = "as Mira",
                Do = delegate { CheckMiraWatchesYou(app); },
                Ready = delegate { return true; },
                Settle = 2
            });

            // ---- the guild tech tree, the rungs it sells and the lines its levels grant
            long before = 0;
            string firstFamily = app.Content.GuildTech.Families.Count > 0 ? app.Content.GuildTech.Families[0].Id : "line";
            steps.Add(new Step
            {
                Name = "guild tech",
                Do = delegate { router.Show(ScreenId.GuildTech); },
                Ready = delegate { return router.Current == router.GuildTech && !router.GuildTech.Loading; },
                Settle = 30,
                Shot = "guild-tech"
            });
            steps.Add(new Step
            {
                Name = "buy the first rung",
                Do = delegate
                {
                    before = app.Session.Profile.Waymarks;
                    // G9: the early guild levels granted the lines the Path used to sell, and the
                    // profile carries them without anything having been bought.
                    Check(app.Session.OwnsGuildPathNode("second-line") && app.Session.LinesAllowed >= 2,
                          "tech: the guild level has granted the saved lines - " + app.Session.LinesAllowed + " lines");
                    Check(Press(Child(Child(router.GuildTech.Root, firstFamily), "Rung1"), "Buy"),
                          "tech: BUY on the first rung is pressable");
                },
                Ready = delegate { return Find(app.OverlayLayer, "Confirm") != null; },
                Settle = 10
            });
            steps.Add(new Step
            {
                Name = "confirm the rung",
                Do = delegate { Check(Press(app.OverlayLayer, "Confirm"), "tech: the confirm dialog's BUY is pressable"); },
                Ready = delegate { return app.Session.RungsOn(firstFamily) >= 1 && !router.GuildTech.Busy; },
                // Long enough for the toast to fade, as the chat shot above.
                Settle = 420,
                Shot = "guild-tech-bought"
            });
            steps.Add(new Step
            {
                Name = "the banner, with its lines",
                Do = delegate
                {
                    int cost = app.Content.GuildTech.RungCost(1);
                    Check(app.Session.Profile.Waymarks == before - cost,
                          "tech: the rung cost exactly its price (" + before + " to " + app.Session.Profile.Waymarks + ")");
                    Check(Find(Child(Child(router.GuildTech.Root, firstFamily), "Rung2"), "Buy") != null,
                          "tech: rung 2 is drawn, and priced out rather than hidden");
                    router.Show(ScreenId.Formation);
                },
                Ready = delegate { return router.Current == router.Formation; },
                Settle = 30,
                Shot = "formation-lines"
            });
            steps.Add(new Step
            {
                Name = "switch to line II",
                Do = delegate { Check(Press(router.Formation.Root, "TabLINE II"), "formation: the LINE II tab is pressable"); },
                Ready = delegate { return app.Session.Profile.ActiveLine == 1; },
                Settle = 20,
                Shot = "formation-line-two"
            });
            return steps;
        }

        // ================================================================== what the run checks

        /// <summary>The fight on screen came from Mira's stored record, and it is her fight.</summary>
        private static void CheckReplayWasTheFight(GameApp app)
        {
            GuildMessageEntry card = _guild.Chat().Find(e => e.AttemptId == _miraLoss.Id);
            Check(card != null && card.Attempt != null && !card.IsMine,
                  "you read Mira's shared attempt as a card carrying her whole record");
            if (card == null || card.Attempt == null) return;
            GuildDungeonReplay replay = GuildDungeon.Replay(app.Content, card.Attempt);
            Check(replay.Playable, "and the record replays (" + replay.Verdict + ")");
            if (!replay.Playable) return;
            Check(SameFight(replay.Result, _miraLossFight),
                  "THE REPLAY IS HER FIGHT, event for event: " + replay.Result.Events.Count + " events, "
                  + (replay.Result.Victory ? "a clear" : "a loss") + " in " + replay.Result.Rounds + " turns");
        }

        /// <summary>Both attempts on floor 2 - Mira's and yours, different rosters - scored and on
        /// exactly the boards their lines satisfy.</summary>
        private static void CheckScored(GameApp app)
        {
            GuildDungeonState state = app.Router.GuildDungeon.State;
            GuildDungeonAttempt mine = state.LastAttempt;
            Check(mine != null && mine.UserId == You && mine.Floor == _miraLoss.Floor && mine.Attempt == 1,
                  "your attempt was stored as your attempt 1 on floor " + _miraLoss.Floor);
            if (mine == null) return;
            Note("  your floor " + mine.Floor + " attempt: " + GuildDungeonFlow.OutcomeLine(mine) + " with "
                 + mine.Team.Units.Count + " heroes, boards [" + string.Join(", ", GuildDungeon.BoardsMet(app.Content, mine).ConvertAll(b => b.ToString()).ToArray()) + "]");

            HashSet<string> distinct = new HashSet<string>();
            for (int i = 0; i < mine.Team.Units.Count; i++) distinct.Add(mine.Team.Units[i].DefId);
            bool differentRoster = false;
            for (int i = 0; i < _miraLoss.Team.Units.Count; i++) if (!distinct.Contains(_miraLoss.Team.Units[i].DefId)) differentRoster = true;
            Check(differentRoster, "and it was a different roster from Mira's");

            List<GuildDungeonAttempt> attempts = _guild.Attempts(_guild.Cycle);
            bool right = true;
            foreach (GuildDungeonBoard board in GuildDungeon.Boards)
            {
                List<GuildDungeonAttempt> ranked = GuildDungeon.Rank(app.Content, attempts, _guild.Cycle, mine.Floor, board);
                foreach (string user in new[] { You, Mira, Oren })
                {
                    bool on = ranked.Exists(a => a.UserId == user);
                    bool should = attempts.Exists(a => a.UserId == user && a.Floor == mine.Floor && GuildDungeon.Satisfies(app.Content, a, board));
                    if (on != should) { right = false; Note("  ! " + _guild.NameOf(user) + " on " + board + ": " + on + ", should be " + should); }
                }
                Note("  floor " + mine.Floor + " " + GuildDungeon.BoardTitle(board) + ": "
                     + string.Join(", ", ranked.ConvertAll(a => _guild.NameOf(a.UserId) + " " + a.Turns + "t (try " + a.Attempt + ")").ToArray()));
            }
            Check(right, "every member on floor " + mine.Floor + " is on exactly the boards their attempts satisfy");
            Check(!GuildDungeon.Rank(app.Content, attempts, _guild.Cycle, mine.Floor, GuildDungeonBoard.FirstAttempt).Exists(a => a.Id == _miraLoss.Id),
                  "Mira's shared loss is on no board");
            Check(state.AttemptsLeft(You, mine.Floor) == state.Cap - 1, "you have " + (state.Cap - 1) + " attempts left on that floor");
        }

        /// <summary>THE OTHER DIRECTION: read the chat as Mira, and watch the fight you posted.</summary>
        private static void CheckMiraWatchesYou(GameApp app)
        {
            GuildDungeonState state = app.Router.GuildDungeon.State;
            GuildDungeonAttempt mine = state.LastAttempt;
            _guild.Self = Mira;
            List<GuildMessageEntry> seen = _guild.Chat();
            _guild.Self = You;
            GuildMessageEntry card = mine == null ? null : seen.Find(e => e.AttemptId == mine.Id);
            Check(card != null && card.Attempt != null && !card.IsMine && card.DisplayName == _guild.NameOf(You),
                  "Mira reads your posted attempt as a card from you");
            if (card == null || card.Attempt == null || state.LastResult == null) return;
            GuildDungeonReplay replay = GuildDungeon.Replay(app.Content, card.Attempt);
            Check(replay.Playable && SameFight(replay.Result, state.LastResult),
                  "AND WATCHES YOUR FIGHT EXACTLY: " + (replay.Playable ? replay.Result.Events.Count + " events" : replay.Explanation));

            GuildMessageEntry old = seen.Find(e => e.Attempt != null && e.Attempt.ContentVersion != app.Content.ContentVersion);
            GuildDungeonReplay refused = old == null ? null : GuildDungeon.Replay(app.Content, old.Attempt);
            Check(refused != null && refused.Verdict == GuildDungeonReplayVerdict.ContentMoved && refused.Result == null,
                  "and Oren's pre-patch card is refused, not played: \"" + (refused != null ? refused.Explanation : "") + "\"");
        }

        /// <summary>
        /// THE FLOOR'S CONDITION, MET THE WAY A PLAYER MEETS IT: read on the card, then answered by
        /// tapping. Every hero the condition refuses comes out of the line through its slot, and
        /// heroes it allows go in from the roster strip until the line is as full as the condition
        /// lets it be. Nothing here reaches past the buttons a player has.
        /// </summary>
        private static void FitLineToCondition(GameApp app)
        {
            ScreenRouter router = app.Router;
            GuildDungeonState state = router.GuildDungeon.State;
            FloorConditionDef condition = GuildDungeon.ConditionOf(app.Content, state.Status.Seed, state.Floor);
            Note("  floor " + state.Floor + " carries " + (condition != null ? condition.Name + ": " + condition.Text : "no condition"));
            string broke;
            bool refused = !GuildDungeon.LineLegal(app.Content, condition, LineOf(app, router.GuildFloor.CurrentLine), out broke);
            Note("  your formation " + (refused ? "is refused on the card before anything is spent: " + broke : "may enter as it is"));
            Check(!refused || Find(router.GuildFloor.Root, "Attempt") == null,
                  "floor: while the line breaks the condition, ATTEMPT cannot be pressed");

            for (int guard = 0; guard < 12; guard++)
            {
                IList<string> line = router.GuildFloor.CurrentLine;
                if (GuildDungeon.LineLegal(app.Content, condition, LineOf(app, line), out broke)) break;
                int drop = line.Count - 1;
                for (int i = line.Count - 1; i >= 0; i--)
                {
                    string alone;
                    if (!GuildDungeon.LineLegal(app.Content, condition, LineOf(app, new List<string> { line[i] }), out alone)) { drop = i; break; }
                }
                Press(router.GuildFloor.Root, "Slot" + drop);
            }

            int size = app.Content.Balance.MaxTeamSize;
            if (condition != null && condition.Line.MaxHeroes > 0) size = Math.Min(size, condition.Line.MaxHeroes);
            List<HeroInstance> owned = new List<HeroInstance>();
            foreach (HeroInstance h in app.Session.Profile.Heroes) if (h != null && h.Def != null) owned.Add(h);
            owned.Sort((a, b) => b.Def.Stars != a.Def.Stars ? b.Def.Stars.CompareTo(a.Def.Stars) : string.CompareOrdinal(a.Def.Name, b.Def.Name));
            foreach (HeroInstance h in owned)
            {
                List<string> line = new List<string>(router.GuildFloor.CurrentLine);
                if (line.Count >= size) break;
                if (line.Contains(h.DefId)) continue;
                line.Add(h.DefId);
                if (GuildDungeon.LineLegal(app.Content, condition, LineOf(app, line), out broke)) Press(router.GuildFloor.Root, "Pick_" + h.DefId);
            }

            List<string> final = new List<string>(router.GuildFloor.CurrentLine);
            Check(final.Count > 0 && GuildDungeon.LineLegal(app.Content, condition, LineOf(app, final), out broke),
                  "floor: tapped into a line the condition allows (" + string.Join(", ", final.ToArray()) + ")");
        }

        private static TeamSnapshot LineOf(GameApp app, IList<string> ids)
        {
            TeamSnapshot t = new TeamSnapshot();
            for (int i = 0; i < ids.Count; i++)
            {
                HeroDef d = app.Content.GetHero(ids[i]);
                if (d != null) t.Units.Add(new SnapshotUnit { DefId = d.Id, Level = 40, Stars = d.Stars });
            }
            return t;
        }

        private static bool SameFight(BattleResult a, BattleResult b)
        {
            if (a == null || b == null || a.Victory != b.Victory || a.Rounds != b.Rounds || a.Events.Count != b.Events.Count)
                return false;
            for (int i = 0; i < a.Events.Count; i++)
            {
                BattleEvent x = a.Events[i], y = b.Events[i];
                if (x.Type != y.Type || x.SourceId != y.SourceId || x.TargetId != y.TargetId || x.Value != y.Value
                    || x.Critical != y.Critical || x.Status != y.Status) return false;
            }
            return true;
        }

        // ================================================================== driving

        private static Button Find(Transform root, string name)
        {
            if (root == null) return null;
            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject.name == name && buttons[i].interactable && buttons[i].gameObject.activeInHierarchy)
                    return buttons[i];
            return null;
        }

        /// <summary>The first active descendant with this name, so a button can be pressed
        /// inside one card of several that each carry a button of the same name.</summary>
        private static Transform Child(Transform root, string name)
        {
            if (root == null) return null;
            RectTransform[] all = root.GetComponentsInChildren<RectTransform>(false);
            for (int i = 0; i < all.Length; i++) if (all[i].gameObject.name == name) return all[i];
            return null;
        }

        private static int Count(Transform root, string name)
        {
            int n = 0;
            RectTransform[] all = root.GetComponentsInChildren<RectTransform>(false);
            for (int i = 0; i < all.Length; i++) if (all[i].gameObject.name == name) n++;
            return n;
        }

        /// <summary>Presses a button the way a tap does: through its own onClick.</summary>
        private static bool Press(Transform root, string name)
        {
            Button b = Find(root, name);
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        private static void Check(bool ok, string label)
        {
            if (!ok) _failures++;
            Note((ok ? "[ok]   " : "[FAIL] ") + label);
        }

        private static void Note(string line)
        {
            _report.Add(line);
            Debug.Log("GuildDungeonShots: " + line);
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            GameApp app = GameApp.Instance;
            ForceGameViewRepaint();

            if (_stage == 0)
            {
                if (app == null || !GameApp.BootCompleted) return;
                Application.runInBackground = true;
                _report.Clear();
                _failures = 0;
                Note("scratch identity: " + Application.persistentDataPath);
                Check(app.Cloud == null, "the cloud is held off for this run - nothing reaches the live project");
                _sizeIndex = 0;
                StartPass(app);
                return;
            }

            if (_wait > 0) { _wait--; return; }

            if (_stage == 1)
            {
                if (_pending != null)
                {
                    if (!File.Exists(_pending)) return;
                    _pending = null;
                }
                if (_stepIndex >= _steps.Count)
                {
                    _sizeIndex++;
                    if (_sizeIndex >= Sizes.Length) { _stage = 9; return; }
                    GameViewSize.Apply(Sizes[_sizeIndex][0], Sizes[_sizeIndex][1]);
                    StartPass(app);
                    _wait = 30;
                    return;
                }
                Step step = _steps[_stepIndex];
                Note("step: " + step.Name + " @" + Sizes[_sizeIndex][0]);
                try { step.Do(); }
                catch (Exception ex) { Check(false, step.Name + " threw: " + ex); }
                _stage = 2;
                _waited = 0;
                _wait = 3;
                return;
            }

            if (_stage == 2)
            {
                Step step = _steps[_stepIndex];
                bool ready = false;
                try { ready = step.Ready(); } catch (Exception) { ready = false; }
                if (!ready && _waited < 1800) { _waited++; return; }
                if (!ready) Check(false, step.Name + " never became ready");
                Canvas.ForceUpdateCanvases();
                _stage = 3;
                _wait = step.Settle;
                return;
            }

            if (_stage == 3)
            {
                Step step = _steps[_stepIndex];
                _stepIndex++;
                _stage = 1;
                if (string.IsNullOrEmpty(step.Shot)) return;
                string file = step.Shot + (_sizeIndex == 0 ? "" : "-2400") + ".png";
                _pending = Path.Combine(OutDir(), file);
                if (File.Exists(_pending)) File.Delete(_pending);
                ScreenCapture.CaptureScreenshot(_pending, 1);
                Note("shot: " + file);
                _wait = 2;
                return;
            }

            if (_stage == 9)
            {
                Note(_failures == 0 ? "ALL CHECKS PASSED" : _failures + " CHECK(S) FAILED");
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath()));
                File.WriteAllLines(ReportPath(), _report.ToArray());
                GuildDungeonBackends.Override = null;
                GuildPathBackends.Override = null;
                GuildTechBackends.Override = null;
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(RestoreKey, true);
                Debug.Log("GuildDungeonShots: done");
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>A fresh guild and a fresh dungeon state, so each aspect photographs the same
        /// moments rather than the second pass spending the first pass's attempts.</summary>
        private static void StartPass(GameApp app)
        {
            Note("---- pass at " + Sizes[_sizeIndex][0] + "x" + Sizes[_sizeIndex][1]);
            SeedGuild(app);
            GuildDungeonState state = app.Router.GuildDungeon.State;
            state.Loaded = false;
            state.Status = null;
            state.Attempts.Clear();
            state.LastAttempt = null;
            state.LastResult = null;
            state.Watched = null;
            // The Path and the lines too: each aspect starts owning what SeedGuild seeds and
            // standing on line I, not on whatever the last pass bought and switched to.
            if (app.Session.Profile.ActiveLine != 0) app.Session.SwitchLine(0);
            app.Session.Profile.Lines.Clear();
            app.Session.Profile.GuildPathNodes.Clear();
            app.Session.Profile.Waymarks = 0;
            _steps = BuildSteps(app);
            _stepIndex = 0;
            _pending = null;
            _stage = 1;
            _wait = 20;
        }

        private static void ForceGameViewRepaint()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null) return;
            window.Repaint();
        }

        private static string HashOf(string path)
        {
            if (!File.Exists(path)) return "absent";
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static void PumpRestore()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(RestoreKey, false);

            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            if (!string.IsNullOrEmpty(product))
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                AssetDatabase.SaveAssets();
            }
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, SessionState.GetBool(OrigSuppressKey, false));

            // The real save, read again under the real identity: untouched, or this run is a failure.
            string live = SessionState.GetString(LiveSaveKey, "");
            string before = SessionState.GetString(LiveHashKey, "");
            string after = HashOf(live);
            string verdict = before == after ? "[ok]   the real save is byte for byte what it was before the run"
                                             : "[FAIL] the real save CHANGED during the run";
            Debug.Log("GuildDungeonShots: " + verdict);
            try { File.AppendAllText(ReportPath(), verdict + Environment.NewLine); } catch (Exception) { }

            Debug.Log("GuildDungeonShots: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
