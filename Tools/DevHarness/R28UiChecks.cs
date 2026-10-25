using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R28 UI CHECKS: the two screens built on the R28 presenters (the Leaderboards screen and the REMINDERS card of the
    /// Settings page), held to Docs/ui-specs/R28-SCREENS.md. The presenters' own states are R28ScreenChecks'; these are
    /// the screens above them.
    ///
    /// The UI is not compiled into the harness, so what a screen does is proven the way R26UiChecks proves it: by reading
    /// its source. The rules read are the ones the spec makes: every string drawn is a field of the view (no sentence of
    /// the screen's own, no em or en dash), a tap calls only the controller methods the spec names, no screen touches a
    /// backend or the save, every door exists, and every tap target is at least HubStyle.TouchMin on both axes. The
    /// layout maths the screens use is engine-free (R28ScreenLayout.cs), so it is run, not read.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR28Ui(ContentDatabase d, string contentDir)
        {
            Line("");
            Line("R28 UI (Leaderboards screen, REMINDERS card: bound to the views, taps, doors, touch sizes, captures)");
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            bool adsWas = AdsSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, liveWas = LiveEventsSwitch.Enabled;
            try
            {
                CheckR28UiLeaderboardsSource(root);
                CheckR28UiLeaderboardsLayout(d);
                CheckR28UiDoors(root);
                CheckR28UiRemindersSource(root);
                CheckR28UiRemindersLayout(d);
                CheckR28UiRound2(d, root);
                CheckR28UiCaptures(root);
            }
            finally
            {
                AdsSwitch.Enabled = adsWas;
                DailyLoopSwitch.Enabled = loopWas;
                LiveEventsSwitch.Enabled = liveWas;
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string R28UiRead(string root, string relative)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        /// <summary>The source with its comments blanked, so a quote inside a comment is not read as a string.</summary>
        private static string R28UiCode(string source)
        {
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", "");
        }

        /// <summary>
        /// The string literals of a file that LOOK like words a player reads: they hold a space, or are a run of capitals
        /// (REFRESH, ON). Node names (Row, RetryReason) and format fragments are not copy. A literal in the allowed list
        /// is a constant the spec names.
        /// </summary>
        private static List<string> R28UiCopy(string source, params string[] allowed)
        {
            List<string> found = new List<string>();
            foreach (Match m in Regex.Matches(R28UiCode(source), "\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\""))
            {
                string lit = m.Groups[1].Value;
                if (lit.Length == 0) continue;
                bool copy = lit.IndexOf(' ') >= 0 || Regex.IsMatch(lit, "^[A-Z]{2,}$");
                if (copy && Array.IndexOf(allowed, lit) < 0) found.Add(lit);
            }
            return found;
        }

        private static string R28UiBody(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return "";
            int open = source.IndexOf('{', at);
            if (open < 0) return "";
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            return "";
        }

        private static List<string> R28UiCalls(string source, string receiver)
        {
            List<string> calls = new List<string>();
            foreach (Match m in Regex.Matches(source, Regex.Escape(receiver) + @"\.(\w+)\("))
                if (!calls.Contains(m.Groups[1].Value)) calls.Add(m.Groups[1].Value);
            return calls;
        }

        private static string R28UiUnused(string code, Type view, string receiverPattern, params string[] exempt)
        {
            List<string> missing = new List<string>();
            foreach (System.Reflection.FieldInfo f in view.GetFields())
            {
                if (f.IsStatic || Array.IndexOf(exempt, f.Name) >= 0) continue;
                if (!Regex.IsMatch(code, receiverPattern + @"\." + f.Name + @"\b")) missing.Add(f.Name);
            }
            return string.Join(", ", missing.ToArray());
        }

        private static bool R28UiNoLongDash(string text)
        {
            return text.IndexOf('\u2014') < 0 && text.IndexOf('\u2013') < 0;
        }

        private static float R28UiTouchMin(string root)
        {
            Match m = Regex.Match(R28UiRead(root, "Assets/Scripts/UI/HubStyle.cs"), @"TouchMin\s*=\s*([0-9.]+)f");
            return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1f;
        }

        // ------------------------------------------------------------------ the Leaderboards screen, read

        private static void CheckR28UiLeaderboardsSource(string root)
        {
            string src = R28UiRead(root, "Assets/Scripts/UI/Screens/LeaderboardsScreen.cs");
            string code = R28UiCode(src);
            Check(src.Length > 0, "R28 UI LEADERBOARDS: LeaderboardsScreen.cs exists under Assets/Scripts/UI/Screens");

            // ---- bound to the view and nothing else
            List<string> copy = R28UiCopy(src);
            Check(copy.Count == 0,
                  "R28 UI LEADERBOARDS: no sentence or label of its own, every string drawn is a field of the view" + (copy.Count > 0 ? " (found: " + string.Join(" | ", copy.ToArray()) + ")" : ""));
            Check(R28UiNoLongDash(src), "R28 UI LEADERBOARDS: no em or en dash in the screen's source");
            string unusedView = R28UiUnused(code, typeof(LeaderboardsView), @"\b(?:v|_view)", "Board", "Title");
            string unusedRow = R28UiUnused(code, typeof(LeaderboardRowView), @"\b(?:r|PinnedSelf|Rows\[i\])", "Rank");
            string unusedTab = R28UiUnused(code, typeof(LeaderboardTabView), @"\bv\.Tabs\[i\]");
            Check(unusedView.Length == 0, "R28 UI LEADERBOARDS: every field of the view is used (Board is read through each tab)" + (unusedView.Length > 0 ? " - unused: " + unusedView : ""));
            Check(unusedRow.Length == 0, "R28 UI LEADERBOARDS: every field of a row the spec binds is drawn (rank text, name, value text, is-me)" + (unusedRow.Length > 0 ? " - unused: " + unusedRow : ""));
            Check(unusedTab.Length == 0, "R28 UI LEADERBOARDS: every field of a tab is used (its label, whether it is selected, its board)" + (unusedTab.Length > 0 ? " - unused: " + unusedTab : ""));
            Check(code.Contains("new LeaderboardsView().Title"), "R28 UI LEADERBOARDS: the header's title is the view's own Title");
            Check(code.Contains("ScreenChrome.ForgedHeader(") && code.Contains("EmberforgeUI.Tab(") && code.Contains("ForgeButtonKind.Secondary") && !code.Contains("ForgeButtonKind.Primary"),
                  "R28 UI LEADERBOARDS: the kit's header, tabs and secondary buttons; the screen has no primary action (boards pay nothing)");
            Check(code.Contains("r.IsMe ? UITheme.Gold : UITheme.TextPrimary"),
                  "R28 UI LEADERBOARDS: the caller's own name is the one gold name");
            Check(code.Contains("r.IsMe ? EmberforgeUI.PlainPanelOn(row)"),
                  "R28 UI LEADERBOARDS: the caller's row is the kit's raised plate with its gold rim");

            // ---- taps call only the controller methods the spec names; nothing else is reached
            List<string> calls = R28UiCalls(code, "_controller");
            List<string> allowed = new List<string> { "Open", "Refresh", "SelectTab" };
            List<string> extra = new List<string>();
            foreach (string c in calls) if (!allowed.Contains(c)) extra.Add(c);
            Check(extra.Count == 0 && calls.Contains("Open") && calls.Contains("Refresh") && calls.Contains("SelectTab"),
                  "R28 UI LEADERBOARDS: a tap calls Open, Refresh or SelectTab on the controller and nothing else" + (extra.Count > 0 ? " (also: " + string.Join(", ", extra.ToArray()) + ")" : ""));
            Check(code.Contains("_controller.Changed += OnChanged") && code.Contains("_controller.Changed -= OnChanged") && code.Contains("_controller.View"),
                  "R28 UI LEADERBOARDS: it subscribes to the controller's Changed on show, lets go on hide, and reads the controller's View");
            Check(Regex.IsMatch(code, @"Time\.unscaledDeltaTime[\s\S]*?_tick < 1f") && code.Contains("_view.ResetInSeconds >= 0"),
                  "R28 UI LEADERBOARDS: it looks again once a second while a countdown is on screen");
            Check(code.Contains("LeaderboardsLayout.StructureKey(") && code.Contains("_resetText.text = _view.ResetLine") && code.Contains("_updatedText.text = _view.UpdatedLine") && code.Contains("_bannerText.text = _view.Banner"),
                  "R28 UI LEADERBOARDS: the once-a-second look only rewrites the reset, updated and banner lines, so the list keeps its scroll position");
            string forbidden = "";
            string[] forbiddenWords = { "RequestSave", "SaveSerializer", "SaveStore", "PlayerPrefs", "ILeaderboardBackend", "SupabaseLeaderboardBackend", ".Top(", ".Submit(",
                                        "FeatureGate.", "FeatureGates.", "IsUnlocked", "StartCoroutine", "Profile." };
            foreach (string w in forbiddenWords) if (code.Contains(w)) forbidden += (forbidden.Length > 0 ? ", " : "") + w;
            Check(forbidden.Length == 0,
                  "R28 UI LEADERBOARDS: no backend, no save, no gate call, no coroutine of its own in the screen" + (forbidden.Length > 0 ? " (found: " + forbidden + ")" : ""));
            Check(Regex.Matches(code, @"LeaderboardBackends\.").Count == 1 && code.Contains("new LeaderboardsController(LeaderboardBackends.For(App), App, App.Session)"),
                  "R28 UI LEADERBOARDS: the one place it touches the backends is building the controller, with LeaderboardBackends.For(app), the app as runner and the session");
            string onShow = R28UiBody(code, "public override void OnShow()");
            int sel = onShow.IndexOf("_controller.SelectTab(", StringComparison.Ordinal), open = onShow.IndexOf("_controller.Open()", StringComparison.Ordinal);
            Check(sel > 0 && open > sel, "R28 UI LEADERBOARDS: a door's board is selected with SelectTab before Open, both on show");

            // ---- no medals, rows are not buttons, ranks 1 to 3 are not decorated
            Check(!Regex.IsMatch(code, @"Medal|Crown|Trophy|TextureLab\.Star|Laurel", RegexOptions.IgnoreCase)
                  && !Regex.IsMatch(code, @"Rank\s*(==|<=|<)\s*[1-4]\b") && !Regex.IsMatch(code, @"Rows\[\s*[0-2]\s*\]"),
                  "R28 UI LEADERBOARDS: no medal, no crown and no row treated differently by its place; boards pay nothing");
            string buildRow = R28UiBody(code, "private static void BuildRow(");
            Check(buildRow.Contains("r.IsMe ? EmberforgeUI.PlainPanelOn(row) : EmberforgeUI.SunkenOn(row)") && !buildRow.Contains("UISkin.") && !buildRow.Contains("UIBuilder.Frame("),
                  "R28 UI LEADERBOARDS: an ordinary row is the kit's brown recess (the kit's plain panel carries a gold rim of its own); only the caller's row is the raised plate with the rim");
            Check(buildRow.Length > 0 && !buildRow.Contains("Button") && !buildRow.Contains("OnClick") && !buildRow.Contains("raycastTarget = true"),
                  "R28 UI LEADERBOARDS: a row is not tappable (the server sends no user id, so no profile opens from a board)");
            Check(code.Contains("BuildRow(pinned, v.PinnedSelf)") && code.Contains("UIBuilder.Divider(") && code.Contains("if (v.PinnedSelf != null)"),
                  "R28 UI LEADERBOARDS: the caller's row below the top list is pinned under a divider, outside the scroll");
            Check(code.Contains("BuildSpinner(plan, canvasHeight, true)") && code.Contains("if (v.ShowSpinner) BuildSpinner(plan, canvasHeight, false)")
                  && code.Contains("v.Phase == ScreenPhase.Loading") && code.Contains("bool status = v.Phase != ScreenPhase.Ready && v.Phase != ScreenPhase.Loading"),
                  "R28 UI LEADERBOARDS: Loading is a spinner alone, a refresh is a spinner over the rows, the other phases are the status block");
            Check(code.Contains("if (!v.Retry.Visible) return;") && code.Contains("_refresh.Root.gameObject.SetActive(v.Refresh.Visible)") && code.Contains("_refresh.Interactable = v.Refresh.Enabled"),
                  "R28 UI LEADERBOARDS: REFRESH and TRY AGAIN are drawn only when the view says visible, and tappable only when it says enabled");

            // ---- touch sizes: every button and tab takes its size from the layout, which is at least TouchMin
            float touch = R28UiTouchMin(root);
            bool sized = true;
            int buttons = 0;
            foreach (Match m in Regex.Matches(code, @"Box\((tab\.Root|_refresh\.Root|retry\.Root)[^;]*;"))
            {
                buttons++;
                if (!m.Value.Contains("LeaderboardsLayout.TabHeight")) sized = false;
            }
            sized &= buttons == 3;
            Check(touch > 0 && LeaderboardsLayout.TapMin == touch,
                  "R28 UI LEADERBOARDS: the layout's tap minimum (" + LeaderboardsLayout.TapMin + ") is HubStyle.TouchMin (" + touch + ")");
            Check(sized && LeaderboardsLayout.TabHeight >= touch && LeaderboardsLayout.TabWidth >= touch && LeaderboardsLayout.RefreshWidth >= touch && LeaderboardsLayout.RetryWidth >= touch,
                  "R28 UI LEADERBOARDS: tabs, REFRESH and TRY AGAIN are all sized from the layout and are at least TouchMin on both axes");
        }

        // ------------------------------------------------------------------ the Leaderboards layout, run

        private static LeaderboardsView R28UiBoardView(ContentDatabase d, GameSession s, int rows, int meRank, long now)
        {
            LeaderboardsPresenter p = new LeaderboardsPresenter();
            p.Online();
            p.BeginLoad(LeaderboardBoard.Recital);
            p.TopLoaded(LeaderboardBoard.Recital, R28Rows(rows, meRank), R28Now);
            return p.Build(d, s.Profile, now);
        }

        private static void CheckR28UiLeaderboardsLayout(ContentDatabase d)
        {
            // ---- the bands never overlap and the list keeps room, on the shortest canvas the game targets
            float top = 260f;   // ScreenChrome.HeaderTop + 108 + UITheme.Space8
            bool ordered = true, fits = true;
            int worstRows = int.MaxValue, plainRows = 0;
            foreach (float h in new float[] { 966f, 1080f })
                foreach (bool banner in new bool[] { false, true })
                    foreach (bool lockLine in new bool[] { false, true })
                        foreach (bool pinned in new bool[] { false, true })
                        {
                            LeaderboardsPlan p = LeaderboardsLayout.Plan(top, h, banner, lockLine, pinned);
                            float y = p.TabsTop + LeaderboardsLayout.TabHeight;
                            ordered &= p.CaptionTop >= y; y = p.CaptionTop + LeaderboardsLayout.CaptionHeight;
                            if (banner) { ordered &= p.BannerTop >= y; y = p.BannerTop + LeaderboardsLayout.BannerHeight; }
                            if (lockLine) { ordered &= p.LockTop >= y; y = p.LockTop + LeaderboardsLayout.LockHeight; }
                            ordered &= p.ColumnHeaderTop >= y && p.ListTop >= p.ColumnHeaderTop + LeaderboardsLayout.ColumnHeaderHeight;
                            fits &= p.ListTop + p.ListHeight + p.ListBottomInset <= h + 0.01f && p.ListHeight > 0f
                                    && p.ListBottomInset >= LeaderboardsLayout.BottomMargin + LeaderboardsLayout.RankLineHeight
                                    && (!pinned || p.PinnedInset >= LeaderboardsLayout.BottomMargin + LeaderboardsLayout.RankLineHeight);
                            if (h == 966f) worstRows = Math.Min(worstRows, p.RowsVisible);
                            if (h == 1080f && !banner && !lockLine && !pinned) plainRows = p.RowsVisible;
                        }
            Check(ordered, "R28 UI LEADERBOARDS LAYOUT: tabs, caption, banner, lock line, column header and list stack top to bottom without overlapping, in every combination");
            Check(fits && worstRows >= 3 && plainRows >= 6,
                  "R28 UI LEADERBOARDS LAYOUT: the list stays inside the canvas and shows " + worstRows + " rows on the shortest canvas with the banner, lock line and pinned row all drawn, " + plainRows + " on a plain 1080");

            // ---- the pinned row and the rank line sit under the list, not over it
            LeaderboardsPlan pinnedPlan = LeaderboardsLayout.Plan(top, 1080f, false, false, true);
            LeaderboardsPlan plainPlan = LeaderboardsLayout.Plan(top, 1080f, false, false, false);
            Check(pinnedPlan.ListBottomInset == plainPlan.ListBottomInset + LeaderboardsLayout.Gap + LeaderboardsLayout.RowHeight + LeaderboardsLayout.Gap + LeaderboardsLayout.DividerHeight
                  && pinnedPlan.PinnedInset == plainPlan.ListBottomInset,
                  "R28 UI LEADERBOARDS LAYOUT: a pinned row takes its own strip and a divider off the bottom of the list and nothing else moves");

            // ---- the once-a-second look: what ticks does not rebuild, what changes does
            GameSession late = R28Cleared(d, "ch3_1");
            LeaderboardsView a = R28UiBoardView(d, late, 10, 2, R28Now);
            LeaderboardsView same = R28UiBoardView(d, late, 10, 2, R28Now + 20);
            LeaderboardsView older = R28UiBoardView(d, late, 10, 2, R28Now + 200);
            Check(LeaderboardsLayout.StructureKey(a) == LeaderboardsLayout.StructureKey(same) && LeaderboardsLayout.StructureKey(a) == LeaderboardsLayout.StructureKey(older)
                  && older.UpdatedLine != a.UpdatedLine,
                  "R28 UI LEADERBOARDS: a view that differs only in 'Updated ... ago' has the same structure, so the list is not rebuilt for it");

            LeaderboardsPresenter weekly = new LeaderboardsPresenter();
            weekly.Online();
            weekly.Select(LeaderboardsPresenter.WeeklyBoard);
            weekly.BeginLoad(LeaderboardsPresenter.WeeklyBoard);
            weekly.TopLoaded(LeaderboardsPresenter.WeeklyBoard, R28Rows(5, 0), R28Now);
            LeaderboardsView w1 = weekly.Build(d, late.Profile, R28Now);
            LeaderboardsView w2 = weekly.Build(d, late.Profile, R28Now + 3600);
            Check(w1.ResetLine != w2.ResetLine && LeaderboardsLayout.StructureKey(w1) == LeaderboardsLayout.StructureKey(w2),
                  "R28 UI LEADERBOARDS: the weekly reset countdown ticks without a rebuild (" + w1.ResetLine + " then " + w2.ResetLine + ")");

            string baseline = LeaderboardsLayout.StructureKey(a);
            List<string> blind = new List<string>();
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Title += "x"; }, "Title");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Caption += "x"; }, "Caption");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.ValueHeader += "x"; }, "ValueHeader");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.MyRankLine += "x"; }, "MyRankLine");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.SubmitLine += "x"; }, "SubmitLine");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.LockLine += "x"; }, "LockLine");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Status += "x"; }, "Status");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Banner = "x"; }, "Banner appearing");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.ShowSpinner = !v.ShowSpinner; }, "ShowSpinner");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Phase = ScreenPhase.Error; }, "Phase");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Refresh = ButtonModel.Off("REFRESHING", "x"); }, "Refresh");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Retry = ButtonModel.On("TRY AGAIN"); }, "Retry");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Rows[0].Name += "x"; }, "a row's name");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Rows[1].IsMe = false; }, "a row's IsMe");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.Tabs[2].Selected = !v.Tabs[2].Selected; }, "a tab's Selected");
            R28UiMove(blind, baseline, d, late, delegate (LeaderboardsView v) { v.PinnedSelf = v.Rows[1]; }, "PinnedSelf");
            Check(blind.Count == 0, "R28 UI LEADERBOARDS: the structure changes whenever anything the screen draws changes" + (blind.Count > 0 ? " (blind to: " + string.Join(", ", blind.ToArray()) + ")" : ""));

            // ---- the four boards, four tabs, with the weekly tab the highest floor board
            Check(a.Tabs.Count == 4 && a.Tabs[1].Board == LeaderboardBoard.RecitalWeek && a.Tabs[0].Selected && !a.Tabs[1].Selected,
                  "R28 UI LEADERBOARDS: four tabs, and the THIS WEEK tab carries the weekly highest floor board");
        }

        private static void R28UiMove(List<string> blind, string baseline, ContentDatabase d, GameSession s, Action<LeaderboardsView> change, string what)
        {
            LeaderboardsView v = R28UiBoardView(d, s, 10, 2, R28Now);
            change(v);
            if (LeaderboardsLayout.StructureKey(v) == baseline) blind.Add(what);
        }

        // ------------------------------------------------------------------ doors and the router

        private static void CheckR28UiDoors(string root)
        {
            string router = R28UiRead(root, "Assets/Scripts/UI/ScreenRouter.cs");
            string profile = R28UiCode(R28UiRead(root, "Assets/Scripts/UI/Screens/ProfileScreen.cs"));
            string recital = R28UiCode(R28UiRead(root, "Assets/Scripts/UI/Screens/RecitalScreen.cs"));
            string proving = R28UiCode(R28UiRead(root, "Assets/Scripts/UI/Screens/ProvingScreen.cs"));
            Check(Regex.IsMatch(router, @"public enum ScreenId[^}]*\bLeaderboards\b") && router.Contains("public Screens.LeaderboardsScreen Leaderboards;")
                  && router.Contains("Leaderboards = Create<Screens.LeaderboardsScreen>()"),
                  "R28 UI DOORS: ScreenId.Leaderboards exists, the router holds the screen and builds it in Build");
            Check(Array.IndexOf(FeatureEntryPoints.OpenScreens, "Leaderboards") >= 0 && FeatureEntryPoints.Find("screen:Leaderboards") == null,
                  "R28 UI DOORS: Leaderboards is named in FeatureEntryPoints.OpenScreens, so CheckFeatureUnlockGating places it; no feature gates it");
            Check(profile.Contains("\"LEADERBOARDS\"") && profile.Contains("Router.Show(ScreenId.Leaderboards)") && Regex.IsMatch(profile, @"Box\(boards\.Root[^;]*HubStyle\.TouchMin"),
                  "R28 UI DOORS: ProfileScreen has a LEADERBOARDS button that opens the screen, at least TouchMin tall");
            string identity = R28UiBody(profile, "private void BuildIdentityCard(");
            Check(identity.Contains("\"LEADERBOARDS\"") && identity.Contains("Box(boards.Root, -28f, -46f, 420f, HubStyle.TouchMin, new Vector2(1f, 1f))") && !profile.Contains("BuildBoardsCard")
                  && identity.IndexOf("boards", StringComparison.Ordinal) > identity.IndexOf("_nameField", StringComparison.Ordinal),
                  "R28 UI DOORS: the LEADERBOARDS door sits in the Profile's identity card, the first card of the page, 46 below its top and at least TouchMin tall, so it is in the first frame");
            Check(recital.Contains("\"BOARDS\"") && recital.Contains("Router.Leaderboards.OpenBoard(Emberfall.Core.Cloud.LeaderboardBoard.Recital)")
                  && recital.Contains("ForgeButtonKind.Secondary") && Regex.IsMatch(recital, @"Box\(boards\.Root[^;]*HubStyle\.TouchMin"),
                  "R28 UI DOORS: RecitalScreen has a BOARDS secondary button that opens the leaderboards on the Deep Recital board, at least TouchMin tall");
            Check(proving.Contains("\"BOARDS\"") && proving.Contains("Router.Leaderboards.OpenBoard(Emberfall.Core.Cloud.LeaderboardBoard.Proving)")
                  && proving.Contains("ForgeButtonKind.Secondary") && Regex.IsMatch(proving, @"Box\(boards\.Root[^;]*HubStyle\.TouchMin"),
                  "R28 UI DOORS: ProvingScreen has a BOARDS secondary button that opens the leaderboards on the Proving board, at least TouchMin tall");
            string store = R28UiRead(root, "Assets/Scripts/UI/Screens/StoreScreen.cs") + R28UiRead(root, "Assets/Scripts/UI/Screens/LiveEventsScreen.cs")
                         + R28UiRead(root, "Assets/Scripts/UI/Screens/WayfarersRoadScreen.cs") + R28UiRead(root, "Assets/Scripts/UI/Screens/EventsScreen.cs");
            string board = R28UiRead(root, "Assets/Scripts/UI/Screens/LeaderboardsScreen.cs");
            Check(!store.Contains("Leaderboards") && !Regex.IsMatch(R28UiCode(board), @"ScreenId\.(Store|Events|LiveEvents|WayfarersRoad)") && !board.Contains("ScreenId.Store"),
                  "R28 UI DOORS: nothing on the Store, Events or pass screens links to the boards, and the boards link to none of them (a rank is never bought)");
            Check(R28UiNoLongDash(R28UiRead(root, "Assets/Scripts/UI/Screens/ProfileScreen.cs")) && R28UiNoLongDash(R28UiRead(root, "Assets/Scripts/UI/Screens/RecitalScreen.cs"))
                  && R28UiNoLongDash(R28UiRead(root, "Assets/Scripts/UI/Screens/ProvingScreen.cs")) && R28UiNoLongDash(router),
                  "R28 UI DOORS: no em or en dash in the files the doors were added to");
        }

        // ------------------------------------------------------------------ the REMINDERS card, read

        private static void CheckR28UiRemindersSource(string root)
        {
            string src = R28UiRead(root, "Assets/Scripts/UI/Screens/RemindersCard.cs");
            string code = R28UiCode(src);
            string settings = R28UiCode(R28UiRead(root, "Assets/Scripts/UI/Screens/SettingsScreen.cs"));
            Check(src.Length > 0, "R28 UI REMINDERS: RemindersCard.cs exists under Assets/Scripts/UI/Screens");

            List<string> copy = R28UiCopy(src, "REMINDERS", "COMING UP", "OPEN PHONE SETTINGS", "ON", "OFF");
            Check(copy.Count == 0,
                  "R28 UI REMINDERS: no sentence of its own; REMINDERS, COMING UP, ON, OFF and the settings button's label are the only constants" + (copy.Count > 0 ? " (found: " + string.Join(" | ", copy.ToArray()) + ")" : ""));
            Check(R28UiNoLongDash(src), "R28 UI REMINDERS: no em or en dash in the card's source");
            string unusedView = R28UiUnused(code, typeof(NotificationSettingsView), @"\bv", "Title");
            string unusedToggle = R28UiUnused(code, typeof(ToggleModel), @"\bt", "Description", "Reason", "On");
            string unusedUp = R28UiUnused(code, typeof(UpcomingReminderView), @"\bu", "Kind", "FireUnix");
            Check(unusedView.Length == 0, "R28 UI REMINDERS: every field of the view is used (the page's own heading stands in for Title)" + (unusedView.Length > 0 ? " - unused: " + unusedView : ""));
            Check(unusedToggle.Length == 0 && code.Contains("RemindersCardLayout.SubLine(t, ") && code.Contains("RemindersCardLayout.SwitchShowsOn(t)"),
                  "R28 UI REMINDERS: every field of a switch is drawn: label, on, enabled, note, and its reason or description through SubLine" + (unusedToggle.Length > 0 ? " - unused: " + unusedToggle : ""));
            Check(unusedUp.Length == 0, "R28 UI REMINDERS: every upcoming reminder is drawn with its title, its time and its body" + (unusedUp.Length > 0 ? " - unused: " + unusedUp : ""));

            List<string> calls = R28UiCalls(code, "_controller");
            List<string> allowed = new List<string> { "Toggle", "PermissionButton", "View" };
            List<string> extra = new List<string>();
            foreach (string c in calls) if (!allowed.Contains(c)) extra.Add(c);
            // Changed is an event, so it is an assignment and not a call; View is a property read through the same receiver pattern.
            Check(extra.Count == 0 && calls.Contains("Toggle") && calls.Contains("PermissionButton"),
                  "R28 UI REMINDERS: a tap calls Toggle or PermissionButton on the controller and nothing else" + (extra.Count > 0 ? " (also: " + string.Join(", ", extra.ToArray()) + ")" : ""));
            Check(code.Contains("_controller.Toggle(topic)") && code.Contains("null, NotificationKind.IdleChestFull, NotificationKind.OrdersReset, NotificationKind.EventEndingWeekly"),
                  "R28 UI REMINDERS: Reminders is Toggle(null) and the topics are Toggle(IdleChestFull / OrdersReset / EventEndingWeekly)");
            Check(code.Contains("_controller.Changed += OnChanged") && code.Contains("_controller.Changed -= OnChanged") && code.Contains("_controller.View") && code.Contains("if (_tick < 1f) return;"),
                  "R28 UI REMINDERS: it rebuilds on the controller's Changed and looks once a second, rebuilding only when the view differs");
            string forbidden = "";
            string[] forbiddenWords = { "PlayerPrefs", "NotificationPrefsStore", "NotificationBackends", "INotificationBackend", "NotificationScheduler", "NotificationPlanner", "NotificationSettingsPresenter",
                                        "RequestSave", "SaveSerializer", "FeatureGate", "IsUnlocked", "App.Session.Profile", "Application.OpenURL" };
            foreach (string w in forbiddenWords) if (code.Contains(w)) forbidden += (forbidden.Length > 0 ? ", " : "") + w;
            Check(forbidden.Length == 0,
                  "R28 UI REMINDERS: no prefs, no backend, no planner, no save and no gate call in the card" + (forbidden.Length > 0 ? " (found: " + forbidden + ")" : ""));
            Check(Regex.Matches(code, @"new NotificationSettingsController\(").Count == 1 && code.Contains("new NotificationSettingsController(_app.Session, null)"),
                  "R28 UI REMINDERS: the one controller is built from the session, with the controller's own backend (NotificationBackends.Current inside it)");

            // ---- the switches: indented topics, disabled when the view says, at least TouchMin
            float touch = R28UiTouchMin(root);
            Check(touch > 0 && RemindersCardLayout.TapMin == touch && RemindersCardLayout.SwitchWidth >= touch && RemindersCardLayout.PermissionButtonWidth >= touch,
                  "R28 UI REMINDERS: the switch and the permission button are at least HubStyle.TouchMin (" + touch + ") on both axes");
            bool sized = true;
            int buttons = 0;
            foreach (Match m in Regex.Matches(code, @"Box\((ask\.Root|toggle\.Root)[^;]*;"))
            {
                buttons++;
                if (!m.Value.Contains("RemindersCardLayout.TapMin") || !(m.Value.Contains("RemindersCardLayout.SwitchWidth") || m.Value.Contains("RemindersCardLayout.PermissionButtonWidth"))) sized = false;
            }
            sized &= buttons == 2;
            Check(sized, "R28 UI REMINDERS: both buttons are sized from the layout's TapMin and widths");
            Check(code.Contains("toggle.Interactable = t.Enabled") && code.Contains("ask.Interactable = model.Enabled") && code.Contains("if (!button) return;"),
                  "R28 UI REMINDERS: a switch is tappable only when the view says Enabled, and the permission button is built only when Visible");
            Check(code.Contains("float indent = index == 0 ? 0f : RemindersCardLayout.TopicIndent") && RemindersCardLayout.TopicIndent == 24f,
                  "R28 UI REMINDERS: the three topics are indented UITheme.Space24 (24) under Reminders");
            Check(!code.Contains("shownOn ? ForgeButtonKind.Primary") && code.Contains("model.Label == OpenSettingsLabel ? ForgeButtonKind.Secondary : ForgeButtonKind.Primary"),
                  "R28 UI REMINDERS: ALLOW REMINDERS and ASKING are the primary button, OPEN PHONE SETTINGS the secondary one, and an ON switch is never orange (R53)");

            // ---- where it sits on the page
            string build = R28UiBody(settings, "protected override void Build()");
            int audio = build.IndexOf("BuildAudioCard(content)", StringComparison.Ordinal), reminders = build.IndexOf("BuildRemindersCard(content)", StringComparison.Ordinal);
            int pair = build.IndexOf("BuildSummonCard(pair)", StringComparison.Ordinal);
            Check(audio > 0 && reminders > audio && pair > reminders,
                  "R28 UI REMINDERS: the card sits on SettingsScreen directly under the audio card, in the same panel style (the page's own Card)");
            Check(settings.Contains("_remindersCard.Show()") && settings.Contains("_remindersCard.Hide()") && settings.Contains("_remindersCard.Tick(Time.unscaledDeltaTime)"),
                  "R28 UI REMINDERS: SettingsScreen shows the card on show, lets go on hide and ticks it");

            // ---- today's real state: no notification package, so Unsupported
            string manifest = R28UiRead(root, "Packages/manifest.json");
            string service = R28UiRead(root, "Assets/Scripts/Game/Notifications/NotificationService.cs");
            Check(!manifest.Contains("com.unity.mobile.notifications") && service.Contains("public NotificationPermission Permission { get { return NotificationPermission.Unsupported; } }"),
                  "R28 UI REMINDERS: no notification package is installed, so the unconfigured backend reports Unsupported and the card says so");
        }

        // ------------------------------------------------------------------ the REMINDERS card, run

        private static NotificationSettingsView R28UiReminders(ContentDatabase d, string through, NotificationPermission permission, bool asking, NotificationPrefs prefs)
        {
            GameSession s = R28Cleared(d, through);
            s.Profile.LastIdleClaimUnix = R28Now - 3600L;
            return NotificationSettingsPresenter.Build(s, prefs, permission, asking, R28Now, 0);
        }

        private static void CheckR28UiRemindersLayout(ContentDatabase d)
        {
            DailyLoopSwitch.Enabled = true;
            LiveEventsSwitch.Enabled = true;
            NotificationPrefs on = new NotificationPrefs();
            NotificationPrefs off = new NotificationPrefs();
            off.Enabled = false;
            const float width = 1180f;

            NotificationSettingsView unsupported = R28UiReminders(d, null, NotificationPermission.Unsupported, false, on);
            NotificationSettingsView noStage = R28UiReminders(d, null, NotificationPermission.NotAsked, false, on);
            NotificationSettingsView notAsked = R28UiReminders(d, "ch1_6", NotificationPermission.NotAsked, false, on);
            NotificationSettingsView asking = R28UiReminders(d, "ch1_6", NotificationPermission.NotAsked, true, on);
            NotificationSettingsView denied = R28UiReminders(d, "ch1_6", NotificationPermission.Denied, false, on);
            NotificationSettingsView grantedOn = R28UiReminders(d, "ch1_6", NotificationPermission.Granted, false, on);
            NotificationSettingsView grantedOff = R28UiReminders(d, "ch1_6", NotificationPermission.Granted, false, off);
            NotificationSettingsView[] states = { unsupported, noStage, notAsked, asking, denied, grantedOn, grantedOff };
            string[] names = { "Unsupported", "NotAsked, no stage", "NotAsked", "asking", "Denied", "Granted on", "Granted off" };

            string bad = "";
            for (int s = 0; s < states.Length; s++)
            {
                RemindersPlan p = RemindersCardLayout.Plan(states[s], width);
                bool ok = p.Height > 0f && p.PermissionTop >= RemindersCardLayout.HeadingBand && p.RuleTop1 >= p.PermissionTop + p.PermissionHeight;
                float prev = p.RuleTop1 + RemindersCardLayout.RuleBand;
                for (int i = 0; i < 4; i++)
                {
                    ok &= p.ToggleTops[i] >= prev && p.ToggleHeights[i] >= RemindersCardLayout.TapMin;
                    prev = p.ToggleTops[i] + p.ToggleHeights[i];
                }
                if (p.ShowUpcoming)
                {
                    ok &= p.RuleTop2 >= prev && p.UpcomingHeaderTop > p.RuleTop2;
                    prev = p.UpcomingHeaderTop + 36f;
                    for (int i = 0; i < p.UpcomingTops.Count; i++) { ok &= p.UpcomingTops[i] >= prev; prev = p.UpcomingTops[i] + p.UpcomingHeights[i]; }
                    if (states[s].UpcomingStatus.Length > 0) { ok &= p.UpcomingStatusTop >= prev; prev = p.UpcomingStatusTop + RemindersCardLayout.LineHeight; }
                }
                ok &= p.RuleTop3 >= prev && p.RulesTop > p.RuleTop3 && p.Height >= p.RulesTop + states[s].RuleLines.Count * RemindersCardLayout.LineHeight;
                if (!ok) bad += (bad.Length > 0 ? ", " : "") + names[s];
            }
            Check(bad.Length == 0, "R28 UI REMINDERS LAYOUT: the heading, permission row, four switches, list and rules stack without overlapping in all seven permission states" + (bad.Length > 0 ? " (overlap in: " + bad + ")" : ""));

            // ---- the Unsupported state: one sentence, every switch disabled, no list
            RemindersPlan u = RemindersCardLayout.Plan(unsupported, width);
            ToggleModel[] ut = { unsupported.Master, unsupported.IdleChest, unsupported.Orders, unsupported.Events };
            bool allOff = true, once = true;
            for (int i = 0; i < ut.Length; i++)
            {
                allOff &= !ut[i].Enabled;
                once &= RemindersCardLayout.SubLine(ut[i], unsupported.Status) != unsupported.Status;
            }
            Check(unsupported.Phase == ScreenPhase.Unavailable && unsupported.PermissionLine == "Reminders are not available on this device yet." && allOff
                  && !u.ShowUpcoming && unsupported.Upcoming.Count == 0 && unsupported.UpcomingStatus.Length == 0 && !unsupported.PermissionButton.Visible,
                  "R28 UI REMINDERS UNSUPPORTED: 'Reminders are not available on this device yet.' in the permission slot, every switch disabled, no button, no list");
            Check(once, "R28 UI REMINDERS UNSUPPORTED: the sentence is printed once, not repeated under each of the four switches (each shows its own description)");

            // ---- a switch that cannot be flipped says why; one that can shows its description
            Check(RemindersCardLayout.SubLine(denied.IdleChest, denied.Status) == "Allow reminders in your phone's settings first."
                  && RemindersCardLayout.SubLine(grantedOff.IdleChest, "") == "Turn on Reminders first."
                  && RemindersCardLayout.SubLine(grantedOn.IdleChest, "") == grantedOn.IdleChest.Description,
                  "R28 UI REMINDERS: a disabled switch prints its Reason in place of its Description, an enabled one its Description");

            // ---- the buttons the spec lists
            Check(noStage.PermissionButton.Visible && !noStage.PermissionButton.Enabled && RemindersCardLayout.Plan(noStage, width).ReasonUnderButton && noStage.PermissionButton.Reason == "Clear your first stage first.",
                  "R28 UI REMINDERS NOT ASKED, NO STAGE: ALLOW REMINDERS is drawn disabled with 'Clear your first stage first.' under it");
            Check(notAsked.PermissionButton.Enabled && notAsked.PermissionButton.Label == "ALLOW REMINDERS" && notAsked.Master.Enabled && notAsked.UpcomingStatus == "Nothing is scheduled until reminders are allowed.",
                  "R28 UI REMINDERS NOT ASKED: ALLOW REMINDERS is tappable, the switches can be set ahead, and the list says nothing is scheduled");
            Check(asking.PermissionButton.Label == "ASKING" && !asking.PermissionButton.Enabled, "R28 UI REMINDERS ASKING: the button reads ASKING and cannot be tapped");
            Check(denied.PermissionButton.Label == "OPEN PHONE SETTINGS" && denied.PermissionButton.Enabled && !denied.Master.Enabled, "R28 UI REMINDERS DENIED: OPEN PHONE SETTINGS, every switch disabled");
            Check(grantedOn.Upcoming.Count > 0 && RemindersCardLayout.Plan(grantedOn, width).UpcomingTops.Count == grantedOn.Upcoming.Count,
                  "R28 UI REMINDERS GRANTED: the COMING UP list is the planner's own answer, one row per reminder (" + grantedOn.Upcoming.Count + ")");
            Check(grantedOff.UpcomingStatus == "Reminders are off." && !grantedOff.IdleChest.Enabled && grantedOff.Master.Enabled && RemindersCardLayout.Plan(grantedOff, width).UpcomingTops.Count == 0,
                  "R28 UI REMINDERS GRANTED, REMINDERS OFF: 'Reminders are off.', the topics disabled, the Reminders switch still live");

            // ---- the card is rebuilt when the view differs and not when it does not
            Check(RemindersCardLayout.StructureKey(grantedOn) == RemindersCardLayout.StructureKey(R28UiReminders(d, "ch1_6", NotificationPermission.Granted, false, on))
                  && RemindersCardLayout.StructureKey(grantedOn) != RemindersCardLayout.StructureKey(grantedOff)
                  && RemindersCardLayout.StructureKey(notAsked) != RemindersCardLayout.StructureKey(asking),
                  "R28 UI REMINDERS: the same view has the same structure, a switched or asking view does not");

            Check(RemindersCardLayout.LinesFor("", 800f) == 0 && RemindersCardLayout.LinesFor("a", 800f) == 1
                  && RemindersCardLayout.LinesFor(new string('a', 200), 800f) == 3 && RemindersCardLayout.TextWidth(width, 24f) > 400f,
                  "R28 UI REMINDERS LAYOUT: a sentence counts the lines it wraps to, and a row's text keeps room beside the switch");
        }

        // ------------------------------------------------------------------ round 2: the fixes after the first captures

        private static void CheckR28UiRound2(ContentDatabase d, string root)
        {
            // ---- the whole Leaderboards path with rows, long names, a pinned row, a banner and a spinner at once
            GameSession late = R28Cleared(d, "ch3_1");
            LeaderboardsPresenter p = new LeaderboardsPresenter();
            p.Online();
            p.BeginLoad(LeaderboardBoard.Recital);
            List<LeaderboardRow> rows = R28Rows(LeaderboardsPresenter.Limit, LeaderboardsPresenter.Limit + 212);
            for (int i = 0; i < rows.Count; i++) rows[i].DisplayName = new string('W', 40) + i;
            p.TopLoaded(LeaderboardBoard.Recital, rows, R28Now);
            p.TopFailed(LeaderboardBoard.Recital, LeaderboardFailure.Server);
            LeaderboardsView failedView = p.Build(d, late.Profile, R28Now + 300);
            p.BeginLoad(LeaderboardBoard.Recital);
            LeaderboardsView loadingView = p.Build(d, late.Profile, R28Now + 300);
            // The presenter never shows a banner and a spinner together (a new load clears the failure), so the two
            // are exercised separately, each over the same 100 long-named rows and the pinned row, and the layout is
            // asked for the worst combination of all its flags.
            bool built = failedView.Phase == ScreenPhase.Ready && failedView.Rows.Count == LeaderboardsPresenter.Limit && failedView.PinnedSelf != null
                         && failedView.Banner.Length > 0 && !failedView.ShowSpinner && failedView.Rows[5].Name.Length > 40
                         && loadingView.Phase == ScreenPhase.Ready && loadingView.PinnedSelf != null && loadingView.ShowSpinner && loadingView.Banner.Length == 0
                         && !loadingView.Refresh.Enabled && loadingView.Refresh.Label == "REFRESHING";
            LeaderboardsPlan plan = LeaderboardsLayout.Plan(260f, 966f, true, true, true);
            string key = LeaderboardsLayout.StructureKey(failedView);
            Check(built && plan.RowsVisible >= 3 && plan.ListHeight > 0f && key.Length > 0 && key.Contains(failedView.PinnedSelf.RankText),
                  "R28 UI ROUND 2: " + failedView.Rows.Count + " rows of 40 character names with the pinned row, under a banner and under a spinner, build their views and key them; the worst layout keeps "
                  + plan.RowsVisible + " rows visible on the shortest canvas");
            LeaderboardsView again = p.Build(d, late.Profile, R28Now + 301);
            Check(LeaderboardsLayout.StructureKey(again) == LeaderboardsLayout.StructureKey(loadingView),
                  "R28 UI ROUND 2: the same board a second later has the same structure, so the once-a-second look never rebuilds under it");

            // ---- a switch that cannot be flipped is not drawn ON
            ToggleModel held = new ToggleModel(); held.On = true; held.Enabled = false;
            ToggleModel live = new ToggleModel(); live.On = true; live.Enabled = true;
            ToggleModel offLive = new ToggleModel(); offLive.On = false; offLive.Enabled = true;
            NotificationPrefs on = new NotificationPrefs();
            NotificationPrefs off = new NotificationPrefs(); off.Enabled = false;
            NotificationSettingsView denied = R28UiReminders(d, "ch1_6", NotificationPermission.Denied, false, on);
            NotificationSettingsView unsupported = R28UiReminders(d, "ch1_6", NotificationPermission.Unsupported, false, on);
            NotificationSettingsView remindersOff = R28UiReminders(d, "ch1_6", NotificationPermission.Granted, false, off);
            NotificationSettingsView allowed = R28UiReminders(d, "ch1_6", NotificationPermission.Granted, false, on);
            Check(!RemindersCardLayout.SwitchShowsOn(held) && RemindersCardLayout.SwitchShowsOn(live) && !RemindersCardLayout.SwitchShowsOn(offLive)
                  && !RemindersCardLayout.SwitchShowsOn(denied.IdleChest) && !RemindersCardLayout.SwitchShowsOn(denied.Master) && !RemindersCardLayout.SwitchShowsOn(unsupported.Orders)
                  && !RemindersCardLayout.SwitchShowsOn(remindersOff.Events) && !RemindersCardLayout.SwitchShowsOn(remindersOff.Master)
                  && RemindersCardLayout.SwitchShowsOn(allowed.Master) && RemindersCardLayout.SwitchShowsOn(allowed.IdleChest),
                  "R28 UI ROUND 2: blocked by the phone, unavailable, or Reminders off, every switch reads OFF even where the view holds it on; allowed, they read as they are");
            string card = R28UiCode(R28UiRead(root, "Assets/Scripts/UI/Screens/RemindersCard.cs"));
            Check(card.Contains("bool shownOn = RemindersCardLayout.SwitchShowsOn(t);") && !card.Contains("t.On ?"),
                  "R28 UI ROUND 2: the card draws its switch from SwitchShowsOn and never reads On alone");

            // ---- dividers span the card's inner width, and the card lays out at its real width
            string rule = R28UiBody(card, "private void Rule(");
            Check(rule.Contains("rt.anchorMin = new Vector2(0f, 1f)") && rule.Contains("rt.anchorMax = new Vector2(1f, 1f)") && rule.Contains("offsetMin = new Vector2(RemindersCardLayout.Pad,")
                  && rule.Contains("offsetMax = new Vector2(-RemindersCardLayout.Pad,") && !rule.Contains("_w") && !rule.Contains("_width"),
                  "R28 UI ROUND 2: a divider is stretched between the card's padding, so it spans the inner width of a card of any width (it was 1120 px in a 1850 px card)");
            Check(card.Contains("Mathf.Abs(CurrentWidth() - _w) > 1f") && card.Contains("RemindersCardLayout.Plan(v, _w)") && !Regex.IsMatch(card, @"\b_width\b[^;]*(Plan|TextWidth)"),
                  "R28 UI ROUND 2: the card plans its text at the width the page really gives it and looks again the moment that width differs from the one it was built for");
            bool fits = true;
            foreach (float w in new float[] { 1180f, 1850f })
                foreach (NotificationSettingsView v in new NotificationSettingsView[] { unsupported, denied, remindersOff, allowed })
                {
                    RemindersPlan rp = RemindersCardLayout.Plan(v, w);
                    float prev = rp.PermissionTop + rp.PermissionHeight;
                    for (int i = 0; i < 4; i++) { fits &= rp.ToggleTops[i] >= prev; prev = rp.ToggleTops[i] + rp.ToggleHeights[i]; }
                    fits &= rp.Height > prev && RemindersCardLayout.TextWidth(w, RemindersCardLayout.TopicIndent) > 400f;
                }
            Check(fits, "R28 UI ROUND 2: the card's bands stack without overlapping at the width it was built for (1180) and at the page's full width (1850)");

            // ---- the settings page opens at its top, and the capture scroll aims at the card's top edge, not its middle
            string settings = R28UiCode(R28UiRead(root, "Assets/Scripts/UI/Screens/SettingsScreen.cs"));
            string onShow = R28UiBody(settings, "public override void OnShow()");
            Check(onShow.Contains("_scroll.content.anchoredPosition = new Vector2(_scroll.content.anchoredPosition.x, 0f)") && onShow.IndexOf("_remindersCard.Show()", StringComparison.Ordinal) > 0,
                  "R28 UI ROUND 2: the Settings page opens at its top, not wherever the last visit left it");
            Check(settings.Contains("card.anchoredPosition.y + card.rect.height * (1f - card.pivot.y)") && !settings.Contains("-card.anchoredPosition.y - 8f"),
                  "R28 UI ROUND 2: the capture scrolls to the card's top edge (its pivot is its middle; scrolled by the middle the page opened at the bottom of the card)");
        }

        private static void CheckR28UiRetryFiles(string root, Dictionary<string, List<string>> known)
        {
            string all = R28UiRead(root, "Tools/capture-r28a-retry.txt");
            List<string> union = new List<string>();
            bool small = true;
            for (int n = 1; n < 20; n++)
            {
                string text = R28UiRead(root, "Tools/capture-r28a-retry-" + n + ".txt");
                if (text.Length == 0) break;
                int lines = 0;
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    lines++;
                    union.Add(line);
                }
                small &= lines <= 10;
            }
            List<string> every = new List<string>();
            bool known1 = true;
            foreach (string raw in all.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                every.Add(line);
                Match m = Regex.Match(line, @"^Screen:(\w+)@(?:1920x1080|2400x1080)$");
                if (!m.Success || !known.ContainsKey(m.Groups[1].Value)) known1 = false;
            }
            Check(all.Length > 0 && small && every.Count > 0 && string.Join("|", union.ToArray()) == string.Join("|", every.ToArray()) && known1,
                  "R28 UI CAPTURES: the retry list is split into files of at most 10 targets whose lines add up to Tools/capture-r28a-retry.txt, every one a real target (" + every.Count + " in all)");
        }

        // ------------------------------------------------------------------ the capture targets

        private static void CheckR28UiCaptures(string root)
        {
            string targets = R28UiRead(root, "Tools/capture-r28a-targets.txt");
            string shot = R28UiRead(root, "Assets/Editor/PlayModeScreenshot.cs");
            Check(targets.Length > 0, "R28 UI CAPTURES: Tools/capture-r28a-targets.txt exists");
            Check(R28UiNoLongDash(targets) && R28UiNoLongDash(R28UiRead(root, "Docs/ui-specs/R28-SCREENS.md")), "R28 UI CAPTURES: no em or en dash in the targets file or the spec");

            List<string> lines = new List<string>();
            foreach (string raw in targets.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0 && !line.StartsWith("#")) lines.Add(line);
            }
            Dictionary<string, List<string>> sizes = new Dictionary<string, List<string>>();
            bool wellFormed = true;
            foreach (string line in lines)
            {
                Match m = Regex.Match(line, @"^Screen:(\w+)@(1920x1080|2400x1080)$");
                if (!m.Success) { wellFormed = false; continue; }
                if (!sizes.ContainsKey(m.Groups[1].Value)) sizes[m.Groups[1].Value] = new List<string>();
                sizes[m.Groups[1].Value].Add(m.Groups[2].Value);
            }
            Check(wellFormed && lines.Count > 0, "R28 UI CAPTURES: every target line is Screen:<Name>@1920x1080 or @2400x1080");

            // The states the spec lists, each at both sizes. A name here with no line, no menu item or no case fails the build.
            string[] required =
            {
                "LeaderboardsOfflineNoCloud", "LeaderboardsOfflineSignedOut", "LeaderboardsLoading", "LeaderboardsLocked", "LeaderboardsReady", "LeaderboardsPinned",
                "LeaderboardsRefreshing", "LeaderboardsBannerFailed", "LeaderboardsBannerOffline", "LeaderboardsEmpty", "LeaderboardsEmptyWeekly", "LeaderboardsError",
                "LeaderboardsNotOpen", "LeaderboardsWeekly", "LeaderboardsProving", "LeaderboardsCampaign",
                "SettingsRemindersUnsupported", "SettingsRemindersNoStage", "SettingsRemindersNotAsked", "SettingsRemindersAsking", "SettingsRemindersDenied",
                "SettingsRemindersGranted", "SettingsRemindersOff", "ProfileLeaderboardsDoor"
            };
            string missing = "", unwired = "";
            foreach (string name in required)
            {
                List<string> got;
                if (!sizes.TryGetValue(name, out got) || !got.Contains("1920x1080") || !got.Contains("2400x1080")) missing += " " + name;
                if (!shot.Contains("Begin(\"Screen:" + name + "\")") || !shot.Contains("case \"" + name + "\"")) unwired += " " + name;
            }
            Check(missing.Length == 0, "R28 UI CAPTURES: every state of both screens is a target at 1920x1080 and 2400x1080" + (missing.Length > 0 ? " (missing:" + missing + ")" : ""));
            Check(unwired.Length == 0, "R28 UI CAPTURES: every target has a menu item and a case in PlayModeScreenshot" + (unwired.Length > 0 ? " (unwired:" + unwired + ")" : ""));
            Check(Regex.Matches(shot, "\\[MenuItem\\(\"Emberfall/Capture/R28 screens/").Count >= required.Length,
                  "R28 UI CAPTURES: the menu items sit under Emberfall/Capture/R28 screens/");
            Check(shot.Contains("LeaderboardBackends.Override") && shot.Contains("NotificationBackends.Override") && shot.Contains("Emberfall Scratch"),
                  "R28 UI CAPTURES: the states are reached through the in-memory backends, on the scratch identity only");
            bool doorsShot = sizes.ContainsKey("ProfileLeaderboardsDoor") && sizes.ContainsKey("Recital") && sizes.ContainsKey("Proving");
            Check(doorsShot, "R28 UI CAPTURES: the three screens that gained a door (Profile at its first frame, Recital, Proving) are targets too");
            CheckR28UiRetryFiles(root, sizes);
            Check(shot.Contains("R28a verify ") && shot.Contains("EditorApplication.QueuePlayerLoopUpdate()") && shot.Contains("if (current != expected) app.Router.Show(expected);"),
                  "R28 UI CAPTURES: each R28a target logs what the router shows just before the shot and shows the screen again if it is not the one asked for, so a Hub frame can be told from a router that went home");
        }
    }
}
