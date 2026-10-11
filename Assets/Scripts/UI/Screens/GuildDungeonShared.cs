using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// What the four guild dungeon screens share: the cycle as the server reported it, every
    /// attempt the guild has made in it, and the two attempts a flow is carrying between screens -
    /// the one the player just made, and the one they just WATCHED.
    ///
    /// Held once, on the entry screen, and read by the others through the router, so the floor
    /// view, the result and the boards cannot each hold a different idea of the guild's attempts.
    /// Nothing here decides anything: every rule is GuildDungeon's, in Core.
    /// </summary>
    public sealed class GuildDungeonState
    {
        public GuildDungeonStatus Status;
        public readonly List<GuildDungeonAttempt> Attempts = new List<GuildDungeonAttempt>();
        public bool Loaded;
        public string Error = "";

        /// <summary>The floor the floor view and the boards open on.</summary>
        public int Floor = 1;

        /// <summary>The attempt this player made most recently, and the fight it produced. The
        /// result screen reads both.</summary>
        public GuildDungeonAttempt LastAttempt;
        public BattleResult LastResult;

        /// <summary>
        /// THE HANDOVER'S MEMORY. The attempt this player last watched, set when a replay's
        /// RUN IT YOURSELF is pressed and cleared when the floor view is opened any other way. The
        /// floor view names it, and the result screen sets the player's own fight beside it.
        /// </summary>
        public GuildDungeonAttempt Watched;

        public string SelfId(IGuildDungeonBackend backend) { return backend != null ? backend.SelfUserId : ""; }

        public int Cycle { get { return Status != null ? Status.Cycle : -1; } }
        public int Cap { get { return Status != null ? Status.AttemptCap : 3; } }
        public int FloorCount { get { return Status != null ? Status.FloorCount : 5; } }

        public int AttemptsLeft(string self, int floor)
        {
            return Math.Max(0, Cap - GuildDungeon.AttemptsUsed(Attempts, self, Cycle, floor));
        }

        /// <summary>Status, then every attempt of its cycle. The order is forced: the attempts
        /// read is filtered by the cycle the status names.</summary>
        public IEnumerator Load(IGuildDungeonBackend backend)
        {
            Error = "";
            if (backend == null) { Error = "This build has no cloud configured, so there is no guild."; yield break; }
            if (!backend.Ready) { Error = backend.NotReadyReason; yield break; }

            GuildDungeonStatus status = null;
            string error = "";
            yield return backend.Status(delegate (GuildDungeonStatus s, string e) { status = s; error = e; });
            if (status == null) { Error = string.IsNullOrEmpty(error) ? "The dungeon did not answer." : error; yield break; }

            List<GuildDungeonAttempt> attempts = null;
            yield return backend.ListAttempts(status.Cycle,
                delegate (List<GuildDungeonAttempt> list, string e) { attempts = list; error = e; });
            if (attempts == null) { Error = string.IsNullOrEmpty(error) ? "The guild's attempts did not load." : error; yield break; }

            Status = status;
            Attempts.Clear();
            Attempts.AddRange(attempts);
            Loaded = true;
        }
    }

    /// <summary>
    /// The dungeon's two ways into BattleScreen - fighting an attempt and watching one - and the
    /// small drawing helpers its screens share.
    ///
    /// BOTH PATHS HAND BattleScreen A FINISHED BattleResult. It came from GuildDungeon.Fight or
    /// GuildDungeon.Replay in Core; the battle screen plays it and computes nothing, which is what
    /// makes a replay the fight that happened rather than a second fight that looks like it.
    /// </summary>
    public static class GuildDungeonFlow
    {
        /// <summary>
        /// Fights a floor with a line, stores it, and plays it.
        ///
        /// STORED BEFORE IT IS SHOWN. The fight is a pure function of the line and the floor, so
        /// its outcome exists the moment it is computed; submitting it first means the attempt is
        /// spent whether or not the player watches to the end, and that a refused submission (the
        /// cap, the cycle turning over, no signal) shows no fight at all rather than one that did
        /// not count. The player is told which.
        /// </summary>
        public static IEnumerator Attempt(GameApp app, ScreenRouter router, GuildDungeonState state,
                                          int floor, TeamSnapshot line, Action<string> failed)
        {
            IGuildDungeonBackend backend = GuildDungeonBackends.For(app);
            if (backend == null || !backend.Ready || state.Status == null)
            {
                failed(backend == null ? "There is no guild to fight for." : backend.NotReadyReason);
                yield break;
            }

            BattleResult result;
            GuildDungeonAttempt record = GuildDungeon.Attempt(app.Content, line, state.Status.Seed,
                                                              state.Cycle, floor, out result);
            string self = backend.SelfUserId;
            record.Boards = GuildPath.ClaimMask(app.Content, record,
                                                GuildDungeon.AttemptsUsed(state.Attempts, self, state.Cycle, floor) + 1);
            // What the Guild Path's base pays for this attempt, predicted from this member's own
            // rows before and after it. The server pays; this only says what it will pay.
            int baseBefore = GuildPath.BaseFor(app.Content.GuildPath, state.Attempts, self, state.Cycle);
            GuildDungeonAttempt stored = null;
            string error = "";
            yield return backend.Record(record, delegate (GuildDungeonAttempt a, string e) { stored = a; error = e; });
            if (stored == null)
            {
                failed("The attempt was not recorded, so it was not spent. " + error);
                yield break;
            }

            state.Attempts.Add(stored);
            state.LastAttempt = stored;
            state.LastResult = result;

            int left = state.AttemptsLeft(backend.SelfUserId, floor);
            GuildBattleRun run = new GuildBattleRun();
            run.Stage = GuildDungeonGenerator.BuildFloor(app.Content, state.Status.Seed, floor);
            run.Result = result;
            run.Label = "GUILD DUNGEON   FLOOR " + floor + "   ATTEMPT " + stored.Attempt + " OF " + state.Cap;
            run.Subtitle = "GUILD DUNGEON, FLOOR " + floor + "   -   " + result.Rounds + " TURNS";
            run.Rows.Add(new KeyValuePair<string, string>("TURNS", result.Rounds.ToString()));
            run.Rows.Add(new KeyValuePair<string, string>("HEROES LOST", stored.Fallen + " OF " + stored.Team.Units.Count));
            run.Rows.Add(new KeyValuePair<string, string>("BOARDS ENTERED",
                GuildDungeon.BoardsMet(app.Content, stored).Count + " OF " + GuildDungeon.Boards.Length));
            run.Rows.Add(new KeyValuePair<string, string>("ATTEMPTS LEFT", left + " OF " + state.Cap));
            if (app.Content.GuildPath.IsUsable)
            {
                int paid = GuildPath.BaseFor(app.Content.GuildPath, state.Attempts, self, state.Cycle) - baseBefore;
                run.Rows.Add(new KeyValuePair<string, string>(app.Content.GuildPath.Currency.ToUpperInvariant(),
                    paid > 0 ? "+" + paid : "0 - PAID ONCE A CYCLE"));
            }
            run.PrimaryLabel = "SEE WHERE IT PLACES YOU";
            run.OnPrimary = delegate { router.Show(ScreenId.GuildDungeonResult, false); };
            run.SecondaryLabel = "BACK TO THE FLOOR";
            run.OnSecondary = delegate { router.Show(ScreenId.GuildFloor, false); };
            router.Battle.SetGuildRun(run);
            router.Show(ScreenId.Battle);
        }

        /// <summary>
        /// Plays a stored attempt back, IF Core can vouch for it. When it cannot - the content
        /// moved, the record predates versions, or this build fights it differently - the player
        /// is told why in a sentence and shown nothing, and can still run the floor themselves.
        ///
        /// `back` is where WATCH's secondary button returns to: the chat for a shared card, the
        /// boards for a board row, the result screen for your own.
        /// </summary>
        public static void Watch(GameApp app, ScreenRouter router, GuildDungeonAttempt attempt,
                                 ScreenId back, string backLabel)
        {
            if (attempt == null) return;
            GuildDungeonReplay replay = GuildDungeon.Replay(app.Content, attempt);
            string who = NameOf(attempt);

            if (!replay.Playable)
            {
                IGuildDungeonBackend self = GuildDungeonBackends.For(app);
                if (self != null && attempt.UserId == self.SelfUserId)
                    app.Modal.ShowNotice("CANNOT SHOW THIS FIGHT", replay.Explanation, "CLOSE", null);
                else
                    app.Modal.ShowConfirm("CANNOT SHOW THIS FIGHT", replay.Explanation,
                        "RUN IT YOURSELF", "CLOSE",
                        delegate { TryItYourself(app, router, attempt); }, null);
                return;
            }

            GuildBattleRun run = new GuildBattleRun();
            run.Stage = GuildDungeonGenerator.BuildFloor(app.Content, attempt.Seed, attempt.Floor);
            run.Result = replay.Result;
            run.IsReplay = true;
            run.Label = "REPLAY   " + who.ToUpperInvariant() + "   FLOOR " + attempt.Floor + "   ATTEMPT " + attempt.Attempt;
            run.Subtitle = "REPLAY OF " + who.ToUpperInvariant() + "'S ATTEMPT   -   EXACTLY AS IT HAPPENED";
            run.Rows.Add(new KeyValuePair<string, string>("FOUGHT BY", who));
            run.Rows.Add(new KeyValuePair<string, string>("RESULT", OutcomeLine(attempt)));
            run.Rows.Add(new KeyValuePair<string, string>("HEROES LOST", Math.Max(0, attempt.Fallen) + " OF " + attempt.Team.Units.Count));
            run.Rows.Add(new KeyValuePair<string, string>("REPLAYED FROM", "the stored record, event for event"));
            // Somebody else's fight hands over: the gold button takes you to the same floor with
            // your own line. Your own fight has nothing to hand over, so the gold button goes back.
            IGuildDungeonBackend backend = GuildDungeonBackends.For(app);
            bool own = backend != null && attempt.UserId == backend.SelfUserId;
            if (own)
            {
                run.PrimaryLabel = backLabel;
                run.OnPrimary = delegate { router.Show(back, false); };
            }
            else
            {
                run.PrimaryLabel = "RUN IT YOURSELF";
                run.OnPrimary = delegate { TryItYourself(app, router, attempt); };
                run.SecondaryLabel = backLabel;
                run.OnSecondary = delegate { router.Show(back, false); };
            }
            router.Battle.SetGuildRun(run);
            router.Show(ScreenId.Battle);
        }

        /// <summary>
        /// THE OTHER HALF OF THE HANDOVER: from something watched, straight to the same floor with
        /// your own roster, carrying what you watched with you. A real attempt, from the cap - a
        /// free practice run would make the cap meaningless, since a deterministic fight tried
        /// until it wins is not a puzzle.
        /// </summary>
        public static void TryItYourself(GameApp app, ScreenRouter router, GuildDungeonAttempt watched)
        {
            GuildDungeonState state = router.GuildDungeon.State;
            state.Watched = watched;
            state.Floor = watched.Floor;
            router.GuildFloor.ResetOnNextShow();
            router.Show(ScreenId.GuildFloor);
        }

        // ---------------------------------------------------------------- words

        public static string NameOf(GuildDungeonAttempt a)
        {
            return a == null || string.IsNullOrEmpty(a.DisplayName) ? "Wayfarer" : a.DisplayName;
        }

        public static string OutcomeLine(GuildDungeonAttempt a)
        {
            if (a == null) return "";
            return a.Victory ? "cleared in " + a.Turns + " turns" : "fell after " + a.Turns + " turns";
        }

        public static string Ordinal(int n)
        {
            int mod100 = n % 100;
            if (mod100 >= 11 && mod100 <= 13) return n + "TH";
            switch (n % 10)
            {
                case 1: return n + "ST";
                case 2: return n + "ND";
                case 3: return n + "RD";
            }
            return n + "TH";
        }

        public static string TimeLeft(long seconds)
        {
            if (seconds <= 0) return "ENDING NOW";
            long days = seconds / 86400, hours = (seconds % 86400) / 3600, minutes = (seconds % 3600) / 60;
            if (days > 0) return days + "D " + hours + "H LEFT";
            if (hours > 0) return hours + "H " + minutes + "M LEFT";
            return Math.Max(1, minutes) + "M LEFT";
        }

        public static string ThemeLabel(GuildDungeonTheme theme)
        {
            switch (theme)
            {
                case GuildDungeonTheme.Swarm: return "SWARM";
                case GuildDungeonTheme.Wall: return "WALL";
                case GuildDungeonTheme.Hunt: return "HUNT";
                case GuildDungeonTheme.Attrition: return "ATTRITION";
                case GuildDungeonTheme.Champion: return "CHAMPION";
            }
            return theme.ToString().ToUpperInvariant();
        }

        /// <summary>The condition's family as a lead-in, "RESOURCE.  ", or empty with no condition.</summary>
        public static string FamilyLabel(FloorConditionDef condition)
        {
            if (condition == null) return "";
            switch (condition.Family)
            {
                case FloorConditionFamily.Resource: return "A RESOURCE CONDITION.  ";
                case FloorConditionFamily.Healing: return "A HEALING CONDITION.  ";
                case FloorConditionFamily.Composition: return "A LINE CONDITION.  ";
                case FloorConditionFamily.EnemyOpening: return "AN OPENING CONDITION.  ";
                case FloorConditionFamily.Tempo: return "A TEMPO CONDITION.  ";
            }
            return "";
        }

        /// <summary>
        /// One board on one floor, in a few words: the guild's current lead on it, "NOBODY YET", or
        /// suspended because the floor's condition gives it to every line (C7). The floor card and
        /// the dungeon row both print it, so a player sees what a board is worth before committing.
        /// </summary>
        public static string BoardLead(ContentDatabase content, GuildDungeonState state, int floor, GuildDungeonBoard board)
        {
            if (state.Status == null) return "";
            FloorConditionDef condition = GuildDungeon.ConditionOf(content, state.Status.Seed, floor);
            if (GuildDungeon.Grants(condition, board)) return "SUSPENDED - " + condition.Name.ToUpperInvariant() + " GIVES IT TO ALL";
            List<GuildDungeonAttempt> ranked = GuildDungeon.Rank(content, state.Attempts, state.Cycle, floor, board);
            if (ranked.Count == 0) return "NOBODY YET - FIRST CLEAR LEADS";
            return "LEAD: " + ranked[0].Turns + " TURNS - " + NameOf(ranked[0]).ToUpperInvariant();
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>A line of portraits, left to right, `size` each. Used by the chat card, the
        /// result screen and the floor view's "their line".</summary>
        public static void DrawLine(RectTransform parent, ContentDatabase content, TeamSnapshot line,
                                    float x, float y, float size, float gap)
        {
            for (int i = 0; line != null && i < line.Units.Count; i++)
            {
                HeroDef def = content.GetHero(line.Units[i].DefId);
                if (def == null) continue;
                RectTransform face = EmberforgeUI.PortraitFrame("Hero" + i, parent, def, def.Stars, size);
                UIBuilder.Box(face, x + i * (size + gap), y, size, size, new Vector2(0f, 1f));
            }
        }

        /// <summary>A board's chip: lit in gold when met, set back when not.</summary>
        public static RectTransform BoardChip(RectTransform parent, GuildDungeonBoard board, bool met, int fontSize)
        {
            return EmberforgeUI.Chip(board.ToString(), parent, GuildDungeon.BoardTitle(board),
                                     met ? Emberforge.TextGold : Emberforge.TextQuiet, fontSize);
        }
    }

    /// <summary>
    /// A fight BattleScreen plays without computing: the result is handed in, and so is
    /// everything the result panel says and does. See BattleScreen.SetGuildRun. Named for the
    /// guild dungeon, which came first, but nothing in it is guild-specific: the campaign's WATCH
    /// on the wave result screen hands in a replay the same way. IsReplay means the result is one
    /// somebody already has, and BattleScreen asks for no save on it.
    /// </summary>
    public sealed class GuildBattleRun
    {
        public StageDef Stage;
        public BattleResult Result;
        public bool IsReplay;
        /// <summary>True for a campaign wave's replay: the battle screen draws HOME for it, and
        /// the back key takes OnPrimary. A guild dungeon run leaves it false and has neither.</summary>
        public bool Home;
        public string Label = "";
        public string Subtitle = "";
        public readonly List<KeyValuePair<string, string>> Rows = new List<KeyValuePair<string, string>>();
        public string PrimaryLabel = "";
        public Action OnPrimary;
        public string SecondaryLabel = "";
        public Action OnSecondary;
    }
}
