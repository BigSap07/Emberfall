using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE GUILD DUNGEON LADDER SCREEN AND THE REWARDED AD BLOCK, AS BUILT (Docs/ui-specs/R28-SCREENS.md, sections 2 and 4).
    ///
    /// R28ScreenChecks proves what the presenters say. This proves the screens that draw them are what the spec says
    /// they are, by reading their source (there is no Unity here) and by running the one piece of layout maths that
    /// is engine free (LadderLayout):
    ///   - the ladder screen binds only to fields of GuildDungeonLadderView, writes no copy of its own, calls no backend
    ///     and offers nothing that buys, refills or plays an ad for an attempt;
    ///   - the ad block binds only to fields of RewardedAdView, is built in exactly three places and in no other, with
    ///     the right placement in each, draws nothing at all while AdsSwitch is off, and is never the primary action;
    ///   - every tap target is at least HubStyle.TouchMin on both axes;
    ///   - the capture targets and the menu items are the same list, and cover every state the spec lists.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR28AdsLadderUi(ContentDatabase content, string contentDir)
        {
            Line("");
            Line("R28 ADS AND LADDER SCREENS (Docs/ui-specs/R28-SCREENS.md: GuildDungeonScreen on its view, the rewarded ad block in its three places)");
            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));

            CheckR28bLayoutMaths(root);
            CheckR28bLadderScreen(root);
            CheckR28bAdBlock(root);
            CheckR28bAdPlaces(root);
            CheckR28bCaptureTargets(root);
        }

        // ------------------------------------------------------------------ source helpers

        private static string R28bSource(string root, string relative)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        /// <summary>The source with every comment removed and every string literal kept as it is.</summary>
        private static string R28bCode(string source)
        {
            return Regex.Replace(source, "(\"(?:[^\"\\\\]|\\\\.)*\")|(//[^\\n]*)|(/\\*.*?\\*/)", delegate (Match m)
            {
                return m.Groups[1].Success ? m.Value : " ";
            }, RegexOptions.Singleline);
        }

        /// <summary>The string literals of a file's code (comments excluded), without their quotes.</summary>
        private static List<string> R28bLiterals(string source)
        {
            List<string> found = new List<string>();
            MatchCollection all = Regex.Matches(R28bCode(source), "\"((?:[^\"\\\\]|\\\\.)*)\"");
            for (int i = 0; i < all.Count; i++) found.Add(all[i].Groups[1].Value);
            return found;
        }

        /// <summary>Literals that are not a node name (letters, digits, underscore) and not one of `allowed`.</summary>
        private static List<string> R28bCopyLiterals(string source, params string[] allowed)
        {
            List<string> copy = new List<string>();
            List<string> literals = R28bLiterals(source);
            for (int i = 0; i < literals.Count; i++)
            {
                string s = literals[i];
                if (s.Length == 0 || Regex.IsMatch(s, "^[A-Za-z0-9_]+$")) continue;
                if (Array.IndexOf(allowed, s) >= 0) continue;
                copy.Add(s);
            }
            return copy;
        }

        private static float R28bConst(string source, string name)
        {
            Match m = Regex.Match(source, "\\b" + Regex.Escape(name) + "\\s*=\\s*(-?[0-9.]+)f");
            return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : float.NaN;
        }

        /// <summary>Splits a call's argument text at its top-level commas.</summary>
        private static List<string> R28bArgs(string text)
        {
            List<string> args = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) { args.Add(text.Substring(start, i - start).Trim()); start = i + 1; }
            }
            args.Add(text.Substring(start).Trim());
            return args;
        }

        /// <summary>
        /// The width and height of every UIBuilder.Box placed on a button's Root, as numbers, or NaN where a name could
        /// not be resolved. `known` maps the names a file declares (constants, HubStyle.TouchMin, a tap variable).
        /// </summary>
        private static List<float[]> R28bButtonBoxes(string code, Dictionary<string, float> known, List<string> unresolved)
        {
            List<float[]> boxes = new List<float[]>();
            MatchCollection calls = Regex.Matches(code, "UIBuilder\\.Box\\(\\s*([A-Za-z_][A-Za-z0-9_]*)\\.Root\\s*,([^;]*)\\);");
            for (int i = 0; i < calls.Count; i++)
            {
                List<string> args = R28bArgs(calls[i].Groups[1].Value + ".Root," + calls[i].Groups[2].Value);
                // Box(rt, x, y, w, h, anchor): the button's Root, then x, y, width, height.
                if (args.Count < 6) { unresolved.Add(calls[i].Value); continue; }
                float w = R28bEval(args[3], known), h = R28bEval(args[4], known);
                if (float.IsNaN(w) || float.IsNaN(h)) unresolved.Add(calls[i].Value);
                boxes.Add(new float[] { w, h });
            }
            return boxes;
        }

        private static float R28bEval(string token, Dictionary<string, float> known)
        {
            token = token.Trim();
            float v;
            if (known.TryGetValue(token, out v)) return v;
            Match lit = Regex.Match(token, "^(-?[0-9.]+)f$");
            if (lit.Success) return float.Parse(lit.Groups[1].Value, CultureInfo.InvariantCulture);
            return float.NaN;
        }

        private static string R28bFindDash(string text)
        {
            if (text == null) return null;
            if (text.IndexOf('—') >= 0) return "an em dash";
            if (text.IndexOf('–') >= 0) return "an en dash";
            return null;
        }

        // ------------------------------------------------------------------ the layout maths

        private static void CheckR28bLayoutMaths(string root)
        {
            float pip = LadderLayout.PipSize, gap = LadderLayout.PipBlockGap;
            Check(LadderLayout.BlockGaps(0) == 0 && LadderLayout.BlockGaps(1) == 0 && LadderLayout.BlockGaps(5) == 0
                  && LadderLayout.BlockGaps(6) == 1 && LadderLayout.BlockGaps(10) == 1 && LadderLayout.BlockGaps(11) == 2,
                  "LADDER PIPS: a block gap after every fifth pip and none after the last (5 pips none, 6 one, 10 one)");

            bool fits = true, pipsNeverShrink = true, gapOnlyAfterFifth = true;
            float[] roomy = { 520f, 420f, 600f, 1000f };
            for (int count = 5; count <= 10; count++)
            {
                for (int r = 0; r < roomy.Length; r++)
                {
                    float spacing = LadderLayout.PipSpacing(count, roomy[r], pip, gap);
                    float width = LadderLayout.StripWidth(count, pip, spacing, gap);
                    fits &= width <= roomy[r] + 0.01f && spacing <= LadderLayout.PipSpacingPreferred + 0.001f && spacing >= LadderLayout.PipSpacingFloor - 0.001f;
                }
                float tight = LadderLayout.PipSpacing(count, 330f, pip, gap);
                pipsNeverShrink &= LadderLayout.PipSize == 30f
                                   && tight <= LadderLayout.PipSpacing(count, 1000f, pip, gap) + 0.001f
                                   && LadderLayout.PipX(count - 1, pip, tight, gap) + pip <= LadderLayout.StripWidth(count, pip, tight, gap) + 0.01f;
                for (int i = 1; i < count; i++)
                {
                    float step = LadderLayout.PipX(i, pip, tight, gap) - LadderLayout.PipX(i - 1, pip, tight, gap);
                    float want = pip + tight + (i % LadderLayout.PipsPerBlock == 0 ? gap : 0f);
                    gapOnlyAfterFifth &= Math.Abs(step - want) < 0.001f;
                }
            }
            Check(fits, "LADDER PIPS: five to ten pips fit the room they are given (the spacing shrinks to fit, never below " + LadderLayout.PipSpacingFloor + ")");
            Check(pipsNeverShrink, "LADDER PIPS: at ten the SPACING shrinks and the pip stays " + LadderLayout.PipSize + " wide; the last pip ends where the strip ends");
            Check(gapOnlyAfterFifth, "LADDER PIPS: pip i starts one pip and one spacing after pip i-1, plus the block gap exactly after the fifth");

            float touch = HubStyleTouchMin(root);
            float row = LadderLayout.RowHeight;
            Check(row == 96f && LadderLayout.TapSize(row, touch) == touch && LadderLayout.TapSize(300f, touch) == 300f
                  && LadderLayout.TapOverhang(row, touch) == (touch - row) * 0.5f && LadderLayout.MinRowSpacing(row, touch) == touch - row,
                  "LADDER ROWS: a row is 96 tall, its tap target is at least HubStyle.TouchMin (" + touch + ") and overhangs the row by " + LadderLayout.TapOverhang(row, touch) + " a side");
            string screen = R28bSource(root, "Assets/Scripts/UI/Screens/GuildDungeonScreen.cs");
            float spacingUsed = R28bConst(screen, "RowSpacing");
            Check(!float.IsNaN(spacingUsed) && spacingUsed >= LadderLayout.MinRowSpacing(row, touch),
                  "LADDER ROWS: the list's row spacing (" + spacingUsed + ") is at least the two overhangs, so neighbouring hit rects never overlap");

            // A real ten floor view: its pip strip fits the room the screen gives it.
            float room = R28bConst(screen, "PipsAvailable");
            float ten = LadderLayout.PipSpacing(10, room, pip, gap);
            Check(!float.IsNaN(room) && LadderLayout.StripWidth(10, pip, ten, gap) <= room + 0.01f && LadderLayout.StripWidth(5, pip, LadderLayout.PipSpacing(5, room, pip, gap), gap) <= room + 0.01f,
                  "LADDER PIPS: the screen's pip room (" + room + ") holds a strip of five and a strip of ten");
        }

        private static float HubStyleTouchMin(string root)
        {
            return R28bConst(R28bSource(root, "Assets/Scripts/UI/HubStyle.cs"), "TouchMin");
        }

        // ------------------------------------------------------------------ the ladder screen

        private static void CheckR28bLadderScreen(string root)
        {
            string source = R28bSource(root, "Assets/Scripts/UI/Screens/GuildDungeonScreen.cs");
            string code = R28bCode(source);
            Check(source.Length > 0, "LADDER SCREEN: GuildDungeonScreen.cs is there");

            // 1. It binds only to fields of the view: every v.X, r.X and v.X.Y names a real member.
            List<string> bad = new List<string>();
            AddUnknownMembers(code, "v", typeof(GuildDungeonLadderView), bad);
            AddUnknownMembers(code, "r", typeof(GuildFloorRowView), bad);
            AddUnknownMembers(code, "row", typeof(GuildFloorRowView), bad);
            Check(bad.Count == 0, "LADDER SCREEN: every v.X and r.X it reads is a field of GuildDungeonLadderView or GuildFloorRowView" + (bad.Count > 0 ? " (" + string.Join(", ", bad.ToArray()) + ")" : ""));

            // 2. And it draws every one of the fields the spec binds.
            string[] drawn = { "Title", "GuildName", "Phase", "LadderLine", "NextCycleLine", "AttemptsLine", "CycleLine", "CycleAttemptCap", "AttemptsLeftThisCycle",
                               "PipCount", "PipsCleared", "Rows", "Status", "Banner", "ShowSpinner", "Boards", "Refresh", "Retry", "FindGuild" };
            string[] rowDrawn = { "Title", "Champion", "DividerBefore", "DividerCaption", "State", "StateText", "ClearedByMe", "FirstClearText", "AttemptsText",
                                  "RewardText", "RewardMuted", "Attempt" };
            List<string> missing = new List<string>();
            for (int i = 0; i < drawn.Length; i++) if (!Regex.IsMatch(code, "\\b(v|view)\\." + drawn[i] + "\\b") && drawn[i] != "Title") missing.Add("v." + drawn[i]);
            if (!code.Contains("new GuildDungeonLadderView().Title")) missing.Add("the view's Title");
            for (int i = 0; i < rowDrawn.Length; i++) if (!Regex.IsMatch(code, "\\b(r|row)\\." + rowDrawn[i] + "\\b")) missing.Add("r." + rowDrawn[i]);
            Check(missing.Count == 0, "LADDER SCREEN: every field the spec binds is drawn" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing.ToArray()) + ")" : ""));

            // 3. No copy of its own: a string literal is a node name, the empty string, or one of the three placeholder labels the view overwrites.
            List<string> copy = R28bCopyLiterals(source, "REFRESH", "BOARDS", "GUILD TECH");
            Check(copy.Count == 0, "LADDER SCREEN: it writes no sentence of its own, every string it draws is a field of the view" + (copy.Count > 0 ? " (" + string.Join(" | ", copy.ToArray()) + ")" : ""));
            string dash = R28bFindDash(source);
            Check(dash == null, "LADDER SCREEN: no em or en dash anywhere in the file" + (dash != null ? " (" + dash + ")" : ""));

            // 4. A tap calls the controller or the router, never a backend, and nothing here buys, refills or plays an ad.
            string[] forbidden = { "RecordAttempt", ".Record(", ".Share(", ".ListAttempts(", ".FetchChat(", ".PostMessage(", "IGuildDungeonBackend", "GuildDungeonScale.", "GuildDungeonRewards.",
                                   ".Profile.", "PurchaseService", "StoreOffers", "StoreRules", "RewardedAd", "AdRewards", "AdBackends", "AdContext", "AdPlacementKind", "Refill", "BuyAttempt" };
            List<string> hits = new List<string>();
            for (int i = 0; i < forbidden.Length; i++) if (code.Contains(forbidden[i])) hits.Add(forbidden[i]);
            Check(hits.Count == 0, "LADDER SCREEN: it calls no backend, writes no profile and names no purchase, refill or ad" + (hits.Count > 0 ? " (" + string.Join(", ", hits.ToArray()) + ")" : ""));
            Check(!Regex.IsMatch(string.Join(" ", R28bLiterals(source).ToArray()), "\\b(buy|bought|purchase|price|refill|restore|extra attempt|more attempts|watch|ad|ads|gems?|store|shop)\\b", RegexOptions.IgnoreCase),
                  "LADDER SCREEN: no label on it says buy, refill, watch or ad: attempts cannot be bought");
            Match clicks = Regex.Match(code, "ATTEMPT");
            Check(!clicks.Success, "LADDER SCREEN: the ATTEMPT label is the view's (r.Attempt.Label), never written in the screen");

            // 5. The calls a tap makes: OnClick handlers are the screen's own OnRefresh, OnBoards, OnFindGuild, OpenFloor or a router push to the guild tech.
            MatchCollection onClicks = Regex.Matches(code, "\\.OnClick\\(([^\\n]*)");
            int allowedClicks = 0;
            for (int i = 0; i < onClicks.Count; i++)
            {
                string body = onClicks[i].Groups[1].Value;
                if (Regex.IsMatch(body, "^\\s*(OnRefresh|OnBoards|OnFindGuild)\\s*\\)\\s*;\\s*$") || body.Contains("OpenFloor(floor)") || body.Contains("Router.Show(ScreenId.GuildTech)")) allowedClicks++;
            }
            Check(onClicks.Count == 6 && allowedClicks == 6,
                  "LADDER SCREEN: its six taps (REFRESH, TRY AGAIN, FIND A GUILD, GUILD TECH, BOARDS, ATTEMPT) call only OnRefresh, OnFindGuild, OnBoards, OpenFloor and the router (" + allowedClicks + " of " + onClicks.Count + " recognised)");
            Check(Regex.IsMatch(code, "void OnRefresh\\(\\)[^}]*_controller\\.Refresh\\(\\)") && Regex.IsMatch(code, "void OnFindGuild\\(\\)[^}]*Router\\.Show\\(ScreenId\\.Guild\\)"),
                  "LADDER SCREEN: REFRESH and TRY AGAIN call the controller's Refresh, FIND A GUILD opens the guild screen at its join view");
            Check(Regex.IsMatch(code, "OpenFloor\\(int floor\\)[^}]*Router\\.Show\\(ScreenId\\.GuildFloor\\)") && Regex.IsMatch(code, "Router\\.Show\\(ScreenId\\.GuildDungeonBoards\\)"),
                  "LADDER SCREEN: ATTEMPT hands the floor to the existing line-up screen, BOARDS opens the existing boards screen");

            // 6. Button kinds: ATTEMPT and FIND A GUILD are the primary, everything else secondary.
            Check(Regex.IsMatch(code, "Button\\(\"Attempt\"[^;]*ForgeButtonKind\\.Primary") && Regex.IsMatch(code, "Button\\(\"FindGuild\"[^;]*ForgeButtonKind\\.Primary")
                  && Regex.IsMatch(code, "Button\\(\"Boards\"[^;]*ForgeButtonKind\\.Secondary") && Regex.IsMatch(code, "Button\\(\"Refresh\"[^;]*ForgeButtonKind\\.Secondary")
                  && Regex.IsMatch(code, "Button\\(\"Retry\"[^;]*ForgeButtonKind\\.Secondary"),
                  "LADDER SCREEN: ATTEMPT and FIND A GUILD are the orange primary, BOARDS, REFRESH and TRY AGAIN are secondary");

            // 7. Touch sizes: every button's box is at least HubStyle.TouchMin on both axes.
            float touch = HubStyleTouchMin(root);
            Dictionary<string, float> known = new Dictionary<string, float>();
            known["HubStyle.TouchMin"] = touch;
            known["tap"] = LadderLayout.TapSize(LadderLayout.RowHeight, touch);
            known["ButtonWidth"] = R28bConst(source, "ButtonWidth");
            List<string> unresolved = new List<string>();
            List<float[]> boxes = R28bButtonBoxes(code, known, unresolved);
            bool allBig = boxes.Count == 6 && unresolved.Count == 0;
            for (int i = 0; i < boxes.Count; i++) allBig &= boxes[i][0] >= touch && boxes[i][1] >= touch;
            Check(allBig && code.Contains("tap = LadderLayout.TapSize(LadderLayout.RowHeight, HubStyle.TouchMin)"),
                  "LADDER SCREEN: every tap target (" + boxes.Count + " button boxes) is at least HubStyle.TouchMin (" + touch + ") on both axes" + (unresolved.Count > 0 ? " (unresolved: " + unresolved[0] + ")" : ""));

            // 8. The loop: rebuild on Changed, once a second, only when the view changed.
            Check(code.Contains("_controller.Changed += OnChanged") && code.Contains("_controller.Changed -= OnChanged") && Regex.IsMatch(code, "_tick \\+= Time\\.unscaledDeltaTime;\\s*if \\(_tick < 1f\\) return;")
                  && code.Contains("if (signature == _signature) return;"),
                  "LADDER SCREEN: it rebuilds on the controller's Changed event and once a second, and a tick that changes nothing rebuilds nothing");

            // 9. States: the status block draws Status, Retry and FindGuild; a spinner follows ShowSpinner; the banner and the divider are the view's.
            Check(Regex.IsMatch(code, "DrawStatus[^}]*v\\.Status") && code.Contains("v.FindGuild.Visible") && code.Contains("v.Retry.Visible") && code.Contains("ShowSpinner")
                  && code.Contains("v.Banner") && code.Contains("row.DividerBefore") && code.Contains("DividerCaption"),
                  "LADDER SCREEN: Offline, Unavailable, Loading, Error and the banner are drawn from Phase, Status, FindGuild, Retry, ShowSpinner and Banner");
            Check(code.Contains("LadderLayout.RowHeight") && code.Contains("ScrollList(") && code.Contains("LadderLayout.PipSpacing(") && code.Contains("LadderLayout.PipX("),
                  "LADDER SCREEN: rows are LadderLayout.RowHeight tall in a UIBuilder.ScrollList and the pips are placed by LadderLayout");

            // 10. No ad block anywhere near it; and nothing in the presenter's view offers a purchase.
            GuildDungeonLadderView view = new GuildDungeonLadderView();
            string[] fieldNames = new string[typeof(GuildDungeonLadderView).GetFields().Length];
            int n = 0;
            foreach (FieldInfo f in typeof(GuildDungeonLadderView).GetFields()) fieldNames[n++] = f.Name;
            bool noSale = true;
            for (int i = 0; i < fieldNames.Length; i++) noSale &= !Regex.IsMatch(fieldNames[i], "buy|purchase|refill|ad$|^ad", RegexOptions.IgnoreCase);
            Check(noSale && view.Title == "GUILD DUNGEON", "LADDER VIEW: no field of the view is a purchase, a refill or an ad, and its title is " + view.Title);
        }

        /// <summary>Every `name.Member` (and `name.Member.Sub` for a ButtonModel) in the code names a real member of the type.</summary>
        private static void AddUnknownMembers(string code, string variable, Type type, List<string> into)
        {
            MatchCollection all = Regex.Matches(code, "\\b" + variable + "\\.([A-Za-z_][A-Za-z0-9_]*)(?:\\.([A-Za-z_][A-Za-z0-9_]*))?");
            for (int i = 0; i < all.Count; i++)
            {
                string first = all[i].Groups[1].Value;
                FieldInfo f = type.GetField(first);
                if (f == null) { if (type.GetProperty(first) == null) into.Add(variable + "." + first); continue; }
                string second = all[i].Groups[2].Value;
                if (second.Length > 0 && f.FieldType == typeof(ButtonModel) && typeof(ButtonModel).GetField(second) == null) into.Add(variable + "." + first + "." + second);
            }
        }

        // ------------------------------------------------------------------ the ad block

        private static readonly string[] R28bAdIdentifiers = { "RewardedAdBlock", "RewardedAdController", "RewardedAdPresenter", "AdRewards", "AdBackends", "AdPlacementKind", "AdContext", "AdService" };

        private static void CheckR28bAdBlock(string root)
        {
            string source = R28bSource(root, "Assets/Scripts/UI/RewardedAdBlock.cs");
            string code = R28bCode(source);
            Check(source.Length > 0, "AD BLOCK: RewardedAdBlock.cs is there");

            List<string> bad = new List<string>();
            AddUnknownMembers(code, "v", typeof(RewardedAdView), bad);
            Check(bad.Count == 0, "AD BLOCK: every v.X it reads is a field of RewardedAdView" + (bad.Count > 0 ? " (" + string.Join(", ", bad.ToArray()) + ")" : ""));
            string[] drawn = { "Visible", "RewardLine", "LeftTodayText", "ResultLine", "ResultIsReward", "NextResetText", "Watch", "Dismiss" };
            List<string> missing = new List<string>();
            for (int i = 0; i < drawn.Length; i++) if (!Regex.IsMatch(code, "\\bv\\." + drawn[i] + "\\b")) missing.Add(drawn[i]);
            Check(missing.Count == 0, "AD BLOCK: it draws every field the spec binds (reward line, left today, WATCH AN AD, result line and colour, next one in, OK)" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing.ToArray()) + ")" : ""));

            List<string> copy = R28bCopyLiterals(source, "|", ",");
            Check(copy.Count == 0, "AD BLOCK: it writes no sentence of its own, every string it draws is a field of the view" + (copy.Count > 0 ? " (" + string.Join(" | ", copy.ToArray()) + ")" : ""));
            string dash = R28bFindDash(source);
            Check(dash == null, "AD BLOCK: no em or en dash anywhere in the file" + (dash != null ? " (" + dash + ")" : ""));

            // Never the primary action: its buttons are all secondary.
            int secondary = Regex.Matches(code, "ForgeButtonKind\\.Secondary").Count;
            Check(secondary == 2 && !code.Contains("ForgeButtonKind.Primary") && !code.Contains("GoldButton") && !code.Contains("PrimaryButton"),
                  "AD BLOCK: WATCH AN AD and OK are both secondary buttons, never the primary action");

            // Touch sizes.
            float touch = HubStyleTouchMin(root);
            Dictionary<string, float> known = new Dictionary<string, float>();
            known["HubStyle.TouchMin"] = touch;
            string[] consts = { "WatchWidth", "OkWidth", "WatchWidthNarrow", "OkWidthNarrow" };
            known["watchWidth"] = Math.Min(R28bConst(source, "WatchWidth"), R28bConst(source, "WatchWidthNarrow"));
            known["okWidth"] = Math.Min(R28bConst(source, "OkWidth"), R28bConst(source, "OkWidthNarrow"));
            List<string> unresolved = new List<string>();
            List<float[]> boxes = R28bButtonBoxes(code, known, unresolved);
            bool big = boxes.Count == 2 && unresolved.Count == 0;
            for (int i = 0; i < boxes.Count; i++) big &= boxes[i][0] >= touch && boxes[i][1] >= touch;
            Check(big, "AD BLOCK: WATCH AN AD and OK are at least HubStyle.TouchMin (" + touch + ") on both axes at both widths" + (unresolved.Count > 0 ? " (unresolved: " + unresolved[0] + ")" : ""));
            for (int i = 0; i < consts.Length; i++) Check(R28bConst(source, consts[i]) >= touch, "AD BLOCK: " + consts[i] + " is " + R28bConst(source, consts[i]) + ", at least " + touch);

            // Invisible with the switch off: nothing is drawn unless the view says Visible, and the host starts inactive and empty.
            int hide = code.IndexOf("if (!v.Visible)", StringComparison.Ordinal);
            int draw = code.IndexOf("Draw(v);", StringComparison.Ordinal);
            Check(hide > 0 && draw > hide && code.Contains("_host.gameObject.SetActive(false);") && Regex.IsMatch(code, "if \\(!v\\.Visible\\)\\s*\\{[^}]*return;"),
                  "AD BLOCK: a view that is not Visible builds nothing and hides its host, and Draw is only reached after that");
            Check(AdsSwitch.Enabled == false && Regex.IsMatch(R28bSource(root, "Assets/Scripts/Core/Session/AdsSwitch.cs"), "#else\\s*public static bool Enabled = false;"),
                  "AD BLOCK: AdsSwitch is OFF by default, so with the shipped switch every view is Hidden and no block is built");
            string manifest = R28bSource(root, "Packages/manifest.json");
            Check(manifest.Length > 0 && !Regex.IsMatch(manifest, "levelplay|ironsource|ads\\.mediation|unity\\.ads", RegexOptions.IgnoreCase),
                  "AD BLOCK: no ads package is in Packages/manifest.json (the simulated backend is all there is)");

            // The loop: Changed redraws, a grant saves, and a tick that changes nothing rebuilds nothing.
            Check(code.Contains("_controller.Changed += OnChanged") && code.Contains("_controller.Granted += OnGranted") && Regex.IsMatch(code, "void OnGranted\\(\\)[^}]*_app\\.RequestSave\\(\\)")
                  && code.Contains("if (signature == _signature) return;"),
                  "AD BLOCK: it redraws on the controller's Changed event, saves the game on Granted, and a refresh that changes nothing rebuilds nothing");
            Check(Regex.IsMatch(code, "void Watch\\(\\)[^}]*_controller\\.Tap\\(") && Regex.IsMatch(code, "void Dismiss\\(\\)[^}]*_controller\\.Dismiss\\(\\)"),
                  "AD BLOCK: WATCH AN AD calls the controller's Tap, OK calls its Dismiss, and nothing else");
            Check(!Regex.IsMatch(code, "AdRewards\\.|\\.Grant\\(|Profile\\.|ClaimIdle|AddProvingBrand"),
                  "AD BLOCK: it grants nothing itself, the controller does");
        }

        // ------------------------------------------------------------------ where the block is built

        private static void CheckR28bAdPlaces(string root)
        {
            string ui = Path.Combine(Path.Combine(Path.Combine(root, "Assets"), "Scripts"), "UI");
            List<string> users = new List<string>();
            List<string> onSale = new List<string>();
            if (Directory.Exists(ui))
            {
                foreach (string f in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
                {
                    string code = R28bCode(File.ReadAllText(f));
                    bool mentions = false;
                    for (int i = 0; i < R28bAdIdentifiers.Length && !mentions; i++) mentions = Regex.IsMatch(code, "\\b" + R28bAdIdentifiers[i] + "\\b");
                    if (!mentions) continue;
                    string name = Path.GetFileName(f);
                    users.Add(name);
                    if (Regex.IsMatch(name, "Store|Event|Pass|Wayfarer|Offer|Shop|Marketplace|Armory|Archive|Quartermaster", RegexOptions.IgnoreCase)) onSale.Add(name);
                }
            }
            users.Sort();
            string[] expected = { "IdleClaimPopup.cs", "ProvingScreen.cs", "RewardedAdBlock.cs", "WaveResultScreen.cs" };
            Check(Directory.Exists(ui) && string.Join(",", users.ToArray()) == string.Join(",", expected),
                  "AD PLACES: the ad block is named in exactly three screens and its own file, " + string.Join(", ", expected) + " (found " + string.Join(", ", users.ToArray()) + ")");
            Check(onSale.Count == 0, "AD PLACES: no Store, Events, LiveEvents, Wayfarer's Road, offer, shop or marketplace file names an ad" + (onSale.Count > 0 ? " (" + string.Join(", ", onSale.ToArray()) + ")" : ""));

            // Each of the three builds one block, of the right kind, with the right context.
            string claim = R28bCode(R28bSource(root, "Assets/Scripts/UI/Hud/IdleClaimPopup.cs"));
            string proving = R28bCode(R28bSource(root, "Assets/Scripts/UI/Screens/ProvingScreen.cs"));
            string wave = R28bCode(R28bSource(root, "Assets/Scripts/UI/Screens/WaveResultScreen.cs"));
            Check(Regex.Matches(claim, "new RewardedAdBlock\\(").Count == 1 && Regex.IsMatch(claim, "new RewardedAdBlock\\(_app, AdPlacementKind\\.IdleGold")
                  && Regex.Matches(proving, "new RewardedAdBlock\\(").Count == 1 && Regex.IsMatch(proving, "new RewardedAdBlock\\(App, AdPlacementKind\\.Brand")
                  && Regex.Matches(wave, "new RewardedAdBlock\\(").Count == 1 && Regex.IsMatch(wave, "new RewardedAdBlock\\(App, AdPlacementKind\\.RepeatClear"),
                  "AD PLACES: A1 is the idle claim popup (IdleGold), A2 the Proving (Brand), A3 the stage cleared view (RepeatClear), one block each");
            Check(Regex.IsMatch(claim, "context\\.ClaimGold = _claimedGold") && Regex.IsMatch(claim, "_claimedGold = report\\.Gold;") && Regex.IsMatch(claim, "_claimedGold = 0;")
                  && Regex.IsMatch(wave, "context\\.Stage = _adStage") && Regex.IsMatch(wave, "_adStage = rewards != null && !rewards\\.FirstClear && rewards\\.RepeatCapped \\? _stage : null"),
                  "AD PLACES: A1 passes the Gold the claim paid (nothing before a claim), A3 passes the stage only on a repeat clear that paid the floor");

            // Below the screen's own primary action, never above it.
            int claimButton = claim.IndexOf("_claim = EmberforgeUI.Button(", StringComparison.Ordinal);
            int claimAd = claim.IndexOf("new RewardedAdBlock(", StringComparison.Ordinal);
            Check(claimButton > 0 && claimAd > claimButton && Regex.IsMatch(claim, "UIBuilder\\.Box\\(adHost, 0f, -\\(PopupHeight \\* 0\\.5f \\+ Emberforge\\.Space16 \\+ RewardedAdBlock\\.Height \\* 0\\.5f\\)")
                  && Regex.IsMatch(claim, "_claim = EmberforgeUI\\.Button\\([^;]*ForgeButtonKind\\.Primary"),
                  "AD PLACES A1: the block sits under the claim plate, CLAIM stays the one primary button");
            int fightButton = proving.IndexOf("_fightButton = EmberforgeUI.Button(", StringComparison.Ordinal);
            Check(fightButton > 0 && Regex.IsMatch(proving, "_fightButton = EmberforgeUI\\.Button\\([^;]*ForgeButtonKind\\.Primary") && Regex.IsMatch(proving, "UIBuilder\\.Box\\(host, CardWidth \\* 0\\.5f \\+ UITheme\\.Space24 \\+ AdWidth \\* 0\\.5f, -\\(top \\+ AdBesideBrands\\)")
                  && R28bConst(R28bSource(root, "Assets/Scripts/UI/Screens/ProvingScreen.cs"), "AdWidth") < 600f,
                  "AD PLACES A2: the block sits right of the opponent card at the height of its Brands rows, stacked (narrow), FIGHT stays the primary");
            int continueBox = wave.IndexOf("UIBuilder.Box(_statsCleared.Root", StringComparison.Ordinal);
            int adBox = wave.IndexOf("UIBuilder.Box(_ad.Root", StringComparison.Ordinal);
            Check(continueBox > 0 && adBox > continueBox && Regex.IsMatch(wave, "_continue = EmberforgeUI\\.Button\\([^;]*ForgeButtonKind\\.Primary"),
                  "AD PLACES A3: the block is placed after (under) the CONTINUE row, CONTINUE stays the primary");

            // Hidden hosts: each site starts its host inactive and unbinds on leaving.
            Check(Regex.Matches(claim, "adHost\\.gameObject\\.SetActive\\(false\\)").Count == 1 && Regex.Matches(proving, "host\\.gameObject\\.SetActive\\(false\\)").Count == 1
                  && Regex.Matches(wave, "adHost\\.gameObject\\.SetActive\\(false\\)").Count == 1
                  && claim.Contains("_ad.Unbind()") && proving.Contains("_ad.Unbind()") && wave.Contains("_ad.Unbind()"),
                  "AD PLACES: every host starts inactive and empty (nothing is built until a view is Visible) and lets go of its controller on leaving");

            // The tick: the popup's Refresh, the Proving's one second Refresh, the wave result's coroutine.
            Check(Regex.IsMatch(claim, "_claim\\.Interactable = report\\.HasAnything;\\s*if \\(_ad != null\\) _ad\\.Refresh\\(\\)")
                  && Regex.IsMatch(proving, "RefreshLine\\(opponent\\);\\s*if \\(_ad != null\\) _ad\\.Refresh\\(\\)") && Regex.IsMatch(wave, "WaitForSecondsRealtime\\(1f\\)[^}]*_ad\\.Refresh\\(\\)"),
                  "AD PLACES: each screen refreshes its block on its own once a second tick, for the 'Next one in' countdown");
        }

        // ------------------------------------------------------------------ the capture targets

        private static void CheckR28bCaptureTargets(string root)
        {
            string shots = R28bSource(root, "Assets/Editor/PlayModeScreenshot.cs");
            string stand = R28bSource(root, "Assets/Editor/R28AdsLadderShots.cs");
            string targetsText = R28bSource(root, "Tools/capture-r28b-targets.txt");
            Check(shots.Length > 0 && stand.Length > 0 && targetsText.Length > 0, "R28b CAPTURES: PlayModeScreenshot.cs, R28AdsLadderShots.cs and Tools/capture-r28b-targets.txt are there");

            string[] ladder = { "offline-nocloud", "offline-signedout", "unavailable", "loading", "error", "g4-open", "g30-cap", "g12-grew", "changed-guild", "after-floor8",
                                "banner-refresh", "banner-offline", "banner-refused", "banner-cycleover" };
            string[] ads = { "hidden", "ready", "playing", "granted", "notfinished", "failed", "refused" };
            string[] places = { "AdClaim", "AdProving", "AdStage" };
            string[] sizes = { "1920x1080", "2400x1080" };

            List<string> want = new List<string>();
            for (int i = 0; i < ladder.Length; i++) for (int s = 0; s < sizes.Length; s++) want.Add("Screen:GuildLadder=" + ladder[i] + "@" + sizes[s]);
            for (int p = 0; p < places.Length; p++) for (int i = 0; i < ads.Length; i++) for (int s = 0; s < sizes.Length; s++) want.Add("Screen:" + places[p] + "=" + ads[i] + "@" + sizes[s]);

            List<string> have = new List<string>();
            string[] lines = targetsText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0 && !line.StartsWith("#")) have.Add(line);
            }
            List<string> lacking = new List<string>();
            for (int i = 0; i < want.Count; i++) if (!have.Contains(want[i])) lacking.Add(want[i]);
            List<string> extra = new List<string>();
            for (int i = 0; i < have.Count; i++) if (!want.Contains(have[i])) extra.Add(have[i]);
            Check(lacking.Count == 0 && extra.Count == 0 && have.Count == want.Count && have.Count == 70,
                  "R28b CAPTURES: capture-r28b-targets.txt holds the " + want.Count + " targets (14 ladder states and 7 ad states in each of 3 places, at 1920x1080 and 2400x1080), no more, no fewer"
                  + (lacking.Count > 0 ? " (missing " + lacking[0] + ")" : extra.Count > 0 ? " (extra " + extra[0] + ")" : ""));
            Check(have.Count == new List<string>(new HashSet<string>(have)).Count, "R28b CAPTURES: no target is listed twice");

            // The five ladder states GUILD-DUNGEON-SIZE.md asks for are in the list, and the editor stand-ins know every state by name.
            string[] five = { "g4-open", "g30-cap", "g12-grew", "changed-guild", "after-floor8" };
            bool five1080 = true, known = true;
            for (int i = 0; i < five.Length; i++) five1080 &= have.Contains("Screen:GuildLadder=" + five[i] + "@1920x1080") && have.Contains("Screen:GuildLadder=" + five[i] + "@2400x1080");
            for (int i = 0; i < ladder.Length; i++) known &= stand.Contains("\"" + ladder[i] + "\"");
            Check(five1080 && known, "R28b CAPTURES: the five states GUILD-DUNGEON-SIZE.md asks for are captured at both shapes, and the stand-in backend builds every ladder state by name");

            // One menu item per target (the size is the Game view's), all inside the one marked block.
            MatchCollection items = Regex.Matches(shots, "\\[MenuItem\\(\"Emberfall/Capture/R28 screens/([^\"]+)\", false, (\\d+)\\)\\]\\s*public static void (\\w+)\\(\\) \\{ Begin\\(\"(Screen:[^\"]+)\"\\); \\}");
            List<string> beginTargets = new List<string>();
            for (int i = 0; i < items.Count; i++) beginTargets.Add(items[i].Groups[4].Value);
            List<string> noItem = new List<string>();
            for (int i = 0; i < ladder.Length; i++) if (!beginTargets.Contains("Screen:GuildLadder=" + ladder[i])) noItem.Add(ladder[i]);
            for (int p = 0; p < places.Length; p++) for (int i = 0; i < ads.Length; i++) if (!beginTargets.Contains("Screen:" + places[p] + "=" + ads[i])) noItem.Add(places[p] + "=" + ads[i]);
            Check(items.Count == 35 && noItem.Count == 0 && Regex.Matches(shots, "Emberfall/Capture/R28 screens/").Count == 35,
                  "R28b CAPTURES: one MenuItem under Emberfall/Capture/R28 screens/ for each of the 35 states (" + items.Count + " found" + (noItem.Count > 0 ? ", no item for " + noItem[0] : "") + ")");

            int start = shots.IndexOf("R28 screens (builder b): guild dungeon ladder, rewarded ads", StringComparison.Ordinal);
            int end = shots.IndexOf("end of R28 screens (builder b)", StringComparison.Ordinal);
            bool inside = start > 0 && end > start;
            if (inside)
            {
                foreach (Match m in Regex.Matches(shots, "R28 screens/")) inside &= m.Index > start && m.Index < end;
                foreach (Match m in Regex.Matches(shots, "AdsSwitch\\.Enabled\\s*=[^=]")) inside &= m.Index > start && m.Index < end;
                foreach (Match m in Regex.Matches(shots, "AdBackends\\.Override\\s*=")) inside &= m.Index > start && m.Index < end;
                foreach (Match m in Regex.Matches(shots, "GuildDungeonBackends\\.Override\\s*=")) inside &= m.Index > start && m.Index < end;
            }
            Check(inside && Regex.Matches(shots, "R28bRestore\\(\\);").Count >= 3 && Regex.IsMatch(shots, "R28bRestore\\(\\);\\s*SessionState\\.SetInt\\(ReplayStageKey, 0\\);"),
                  "R28b CAPTURES: the menu items, the AdsSwitch flip and both overrides all sit inside one contiguous block, and Stop puts them back");

            // AdsSwitch is only ever written by the capture tool (the harness forces it for its own checks and puts it back).
            List<string> writers = new List<string>();
            foreach (string dir in new string[] { "Assets/Scripts" })
            {
                string full = Path.Combine(root, dir.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(full)) continue;
                foreach (string f in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
                    if (Regex.IsMatch(R28bCode(File.ReadAllText(f)), "AdsSwitch\\.Enabled\\s*=[^=]")) writers.Add(Path.GetFileName(f));
            }
            Check(writers.Count == 0, "R28b CAPTURES: no game or UI file ever writes AdsSwitch.Enabled, only the capture tool and the harness do" + (writers.Count > 0 ? " (" + string.Join(", ", writers.ToArray()) + ")" : ""));

            // The simulated backend is the only network there is, and nothing here dashes.
            string dash = R28bFindDash(shots.Substring(Math.Max(0, start), end > start ? end - start : 0)) ?? R28bFindDash(stand) ?? R28bFindDash(targetsText);
            Check(dash == null, "R28b CAPTURES: no em or en dash in the capture block, the stand-ins or the targets file" + (dash != null ? " (" + dash + ")" : ""));
        }
    }
}
