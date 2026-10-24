using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE GUILD TECH TREE (claude/DESIGN-GUILD-TECH.md), which replaced the four-node Guild Path.
    ///
    /// Four family tracks down the page. Each carries three stat rungs the MEMBER buys with their
    /// own Waymarks, and one guild talent slot the GUILD picks from three - and the screen has to
    /// make that split obvious, because it is the thing a player will otherwise get wrong. A rung
    /// is yours and travels with you; a talent is your guild's and you lose it if you leave.
    ///
    /// GUILD LEVEL IS THE GATE AND NOTHING ELSE (G6). Every rung and every slot names the level it
    /// opens at, whether or not that level has been reached, so the whole road is legible from the
    /// first day rather than appearing a piece at a time.
    ///
    /// A HERO'S FAMILY IS ITS ROLE (G1), so each track says how many heroes the player actually
    /// owns in it. A narrow roster gets less from the tree, the design says so on purpose, and a
    /// player deciding which track to buy should be able to see that before they spend.
    ///
    /// AND THIS ONE DOES CHANGE FIGHTS. The Path screen used to promise the opposite under its
    /// tree; that promise is retired, and saying nothing where a player is used to reading a
    /// reassurance would be its own kind of lie - so the note says plainly what the tree is now and
    /// that a player in no guild still finishes the campaign (G5).
    /// </summary>
    public sealed class GuildTechScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildTech; } }

        private const float PageWidth = 1680f;
        private const float TrackHeight = 268f;
        private const float StatusHeight = 176f;
        private const float RungWidth = 220f;

        private Text _headerSubtitle;
        private RectTransform _content;
        private ForgePill _balance;
        private Text _balanceNote;
        private Text _level;
        private Image _levelBar;
        private Text _levelNote;
        private Text _pays;
        private RectTransform _tracks;
        private Text _note;
        private bool _loading;
        private bool _busy;
        private string _offline = "";

        /// <summary>True while the server is being asked. Read by the capture run.</summary>
        public bool Loading { get { return _loading; } }
        public bool Busy { get { return _busy; } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "GUILD TECH", Router, out _headerSubtitle);

            ScrollRect scroll;
            _content = UIBuilder.ScrollList("Content", Root, out scroll, 16f, new RectOffset(0, 0, 8, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), UITheme.ScreenPadding,
                                 UITheme.TopBarHeight + 118f, UITheme.ScreenPadding, 20f);

            BuildStatus();

            _tracks = UIBuilder.Node("Tracks", _content);
            UIBuilder.Sized(_tracks, PageWidth, 0f);
            UIBuilder.VStack(_tracks, 14f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            _tracks.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _note = EmberforgeUI.Body("Note", _content, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Sized(_note.rectTransform, PageWidth - 200f, 88f);
        }

        private void BuildStatus()
        {
            RectTransform card = UIBuilder.Node("Status", _content);
            UIBuilder.Sized(card, PageWidth, StatusHeight);
            EmberforgeUI.PanelOn(card, true);

            _balance = EmberforgeUI.CurrencyPill("Waymarks", card, 320f, 68f, UITheme.Waymark, null);
            UIBuilder.Box(_balance.Root, 44f, -34f, 320f, 68f, new Vector2(0f, 1f));

            _balanceNote = EmberforgeUI.Label("BalanceNote", card, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_balanceNote.rectTransform, 48f, -112f, 330f, 28f, new Vector2(0f, 1f));

            _level = EmberforgeUI.Title("Level", card, "", UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_level.rectTransform, 420f, -28f, 460f, 36f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_level, Emberforge.Outline, Emberforge.StrokeThin);

            _levelBar = EmberforgeUI.ProgressBar("LevelBar", card);
            UIBuilder.Box((RectTransform)_levelBar.rectTransform.parent, 420f, -72f, 440f, 22f, new Vector2(0f, 1f));

            _levelNote = EmberforgeUI.Label("LevelNote", card, "", UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_levelNote.rectTransform, 420f, -102f, 520f, 26f, new Vector2(0f, 1f));

            Text heading = EmberforgeUI.Title("PaysHeading", card, "WHAT THE GUILD DUNGEON PAYS", UITheme.FontSmall,
                                              Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 960f, -26f, 700f, 32f, new Vector2(0f, 1f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);

            _pays = EmberforgeUI.Body("Pays", card, "", UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_pays.rectTransform, 960f, -62f, 680f, 100f, new Vector2(0f, 1f));
        }

        // ================================================================== lifecycle

        public override void OnShow()
        {
            App.RefreshHud();
            _busy = false;
            Render();
            Reload();
        }

        public override void OnHide()
        {
            _loading = false;
        }

        private void Reload()
        {
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        /// <summary>
        /// TWO CALLS, AND BOTH ARE NEEDED. The Waymark balance lives in 0011's ledger and the tree
        /// lives in 0012's tables, so the screen asks for the purse and for the tree. They are
        /// separate because the purse is the guild dungeon's and the tree is what spends it, and
        /// folding them into one call would make the tech tree own a number it does not mint.
        /// </summary>
        private IEnumerator LoadRoutine()
        {
            IGuildPathBackend purse = GuildPathBackends.For(App);
            IGuildTechBackend tech = GuildTechBackends.For(App);
            if (purse == null || tech == null)
            {
                _offline = "This build has no cloud configured, so there is no guild tech here.";
            }
            else
            {
                _headerSubtitle.text = "LOADING";
                string error = "";
                GuildPathState paid = null;
                yield return purse.State(delegate (GuildPathState s, string e) { paid = s; error = e; });
                if (paid != null) App.Session.ApplyGuildPathState(paid);

                GuildTechState state = null;
                string techError = "";
                yield return tech.State(delegate (GuildTechState s, string e) { state = s; techError = e; });
                if (state != null) App.Session.ApplyGuildTechState(state);

                _offline = paid != null && state != null ? "" : (state == null ? techError : error);
                if (paid != null || state != null) App.SaveNow();
            }
            _loading = false;
            Render();
        }

        // ================================================================== render

        private void Render()
        {
            GuildTechConfig tree = App.Content.GuildTech;
            GuildPathConfig pay = App.Content.GuildPath;
            GameSession session = App.Session;
            GuildTechState state = session.Profile.GuildTech;
            string currency = tree.Currency.ToUpperInvariant();

            _headerSubtitle.text = !string.IsNullOrEmpty(_offline)
                ? "OFFLINE   -   THE TREE AS IT STOOD WHEN YOU LAST CONNECTED"
                : "YOUR RUNGS ARE YOURS.  THE TALENTS ARE YOUR GUILD'S.";
            _balance.Value.text = session.Profile.Waymarks.ToString("N0");
            _balanceNote.text = currency + " TO SPEND ON RUNGS";

            int level = session.GuildLevel;
            int members = state != null ? Mathf.Max(1, state.Members) : 1;
            _level.text = "GUILD LEVEL " + level + " OF " + tree.LevelCeiling;
            long per = tree.XpForLevel(members);
            long into = per > 0 ? (state != null ? state.Xp : 0) % per : 0;
            EmberforgeUI.SetProgress(_levelBar, level >= tree.LevelCeiling ? 1f : (per > 0 ? into / (float)per : 0f));
            _levelNote.text = level >= tree.LevelCeiling
                ? "THE WHOLE TREE IS OPEN"
                : into.ToString("N0") + " OF " + per.ToString("N0") + " TOWARD LEVEL " + (level + 1)
                  + "   -   " + members + (members == 1 ? " MEMBER" : " MEMBERS");

            _pays.text = pay.PayAttempt + " for your first attempt of the week, won or lost.   "
                       + pay.PayClear + " more for your first clear, on any floor.\n"
                       + "Up to " + pay.PayBoardPoint + " for each of floor " + pay.BoardFloor + "'s boards once the week is over.\n"
                       + "Everything the guild is paid is also what levels it.";

            DrawTracks(tree, session, state, level);

            _note.text = !string.IsNullOrEmpty(_offline)
                ? _offline
                : "A hero belongs to a track by its ROLE, and by nothing else. Rungs are bought with your own "
                  + tree.Currency + " and stay yours in any guild or none; the talent on each track is your guild's "
                  + "choice, one of three, and it changes at the turn of the week. This tech makes your heroes stronger - "
                  + "a guild gets there sooner, and a player in no guild still finishes the campaign.";
        }

        private void DrawTracks(GuildTechConfig tree, GameSession session, GuildTechState state, int level)
        {
            for (int i = _tracks.childCount - 1; i >= 0; i--) Destroy(_tracks.GetChild(i).gameObject);
            for (int f = 0; f < tree.Families.Count; f++) DrawTrack(tree, tree.Families[f], session, state, level);
        }

        /// <summary>One family: what it is, how many of the player's heroes it covers, its three
        /// rungs, and its talent slot.</summary>
        private void DrawTrack(GuildTechConfig tree, GuildTechFamilyDef family, GameSession session,
                               GuildTechState state, int level)
        {
            RectTransform row = UIBuilder.Node(family.Id, _tracks);
            UIBuilder.Sized(row, PageWidth, TrackHeight);
            EmberforgeUI.PanelOn(row, true);

            Text name = EmberforgeUI.Title("Name", row, family.Name.ToUpperInvariant(), UITheme.FontHeading,
                                           Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 30f, -18f, 300f, 42f, new Vector2(0f, 1f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            List<string> roles = new List<string>();
            for (int r = 0; r < family.Roles.Count; r++) roles.Add(family.Roles[r].ToString().ToUpperInvariant());
            Text who = EmberforgeUI.Label("Roles", row, string.Join("  ", roles.ToArray()), UITheme.FontTiny - 4,
                                          Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(who.rectTransform, 30f, -64f, 300f, 24f, new Vector2(0f, 1f));

            // How many of the player's OWN heroes this track reaches. G1's cost, made visible
            // before anybody spends on a track their roster is thin in.
            int owned = 0;
            for (int i = 0; i < session.Profile.Heroes.Count; i++)
            {
                Emberfall.Core.Model.HeroInstance h = session.Profile.Heroes[i];
                if (h != null && h.Def != null && family.Roles.Contains(h.Def.Role)) owned++;
            }
            Text mine = EmberforgeUI.Label("Mine", row, owned + (owned == 1 ? " HERO YOU OWN" : " HEROES YOU OWN"),
                                           UITheme.FontTiny - 2, owned > 0 ? Emberforge.TextBody : UITheme.Danger,
                                           TextAnchor.UpperLeft);
            UIBuilder.Box(mine.rectTransform, 30f, -92f, 300f, 26f, new Vector2(0f, 1f));

            List<string> stats = new List<string>();
            for (int s = 0; s < family.RungStats.Count; s++) stats.Add(StatLabel(family.RungStats[s]));
            Text buys = EmberforgeUI.Body("Buys", row, "Rungs here buy " + string.Join(" and ", stats.ToArray()) + ".",
                                          UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(buys.rectTransform, 30f, -124f, 300f, 48f, new Vector2(0f, 1f));

            for (int r = 1; r <= tree.RungsPerFamily; r++) DrawRung(tree, family, session, state, level, row, r);
            DrawSlot(tree, family, session, state, level, row);
        }

        private static string StatLabel(GuildTechStat stat)
        {
            switch (stat)
            {
                case GuildTechStat.MaxHp: return "Max HP";
                case GuildTechStat.Attack: return "Attack";
                case GuildTechStat.Defense: return "Defense";
                case GuildTechStat.Speed: return "Speed";
                case GuildTechStat.CritChance: return "Crit Chance";
                case GuildTechStat.CritDamage: return "Crit Damage";
            }
            return stat.ToString();
        }

        private void DrawRung(GuildTechConfig tree, GuildTechFamilyDef family, GameSession session,
                              GuildTechState state, int level, RectTransform row, int rung)
        {
            int owned = session.RungsOn(family.Id);
            bool bought = rung <= owned;
            int needs = tree.LevelForRung(family.Id, rung);
            bool open = level >= needs;
            int cost = tree.RungCost(rung);
            bool next = !bought && rung == owned + 1;

            RectTransform card = UIBuilder.Node("Rung" + rung, row);
            UIBuilder.Box(card, 352f + (rung - 1) * (RungWidth + 14f), -22f, RungWidth, TrackHeight - 44f, new Vector2(0f, 1f));
            if (bought) EmberforgeUI.PlainPanelOn(card); else EmberforgeUI.SunkenOn(card);

            Text title = EmberforgeUI.Title("Title", card, "RUNG " + rung, UITheme.FontSmall,
                                            bought ? Emberforge.TextGold : open ? Emberforge.TextBody : Emberforge.TextQuiet,
                                            TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 16f, -12f, RungWidth - 32f, 28f, new Vector2(0f, 1f));

            // WHAT IT ACTUALLY DOES, in the stats it moves, not as an abstract share. The share is
            // one number for every family by design; a player wants to read Max HP.
            Text what = EmberforgeUI.Body("What", card, RungText(family, tree), UITheme.FontTiny - 5,
                                          Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(what.rectTransform, 16f, -42f, RungWidth - 32f, 60f, new Vector2(0f, 1f));

            if (bought)
            {
                RectTransform chip = EmberforgeUI.Chip("Owned", card, "BOUGHT", UITheme.Success, UITheme.FontTiny - 4);
                UIBuilder.Box(chip, 16f, 16f, RungWidth - 32f, 32f, new Vector2(0f, 0f));
                return;
            }

            Text price = EmberforgeUI.Label("Cost", card, cost + " " + tree.Currency.ToUpperInvariant(),
                                            UITheme.FontTiny - 3, UITheme.Waymark, TextAnchor.UpperLeft);
            UIBuilder.Box(price.rectTransform, 16f, -110f, RungWidth - 32f, 24f, new Vector2(0f, 1f));

            string why = "rung " + (owned + 1) + " first";
            int nextRung, nextCost;
            bool can = false;
            if (next)
            {
                string reason;
                can = session.CanBuyRung(family.Id, out nextRung, out nextCost, out reason);
                why = can ? "" : reason;
            }

            UIButton buy = EmberforgeUI.Button("Buy", card, can ? "BUY" : !open ? "GUILD LEVEL " + needs : why.ToUpperInvariant(),
                                               UITheme.FontTiny - 2, can ? ForgeButtonKind.Primary : ForgeButtonKind.Secondary);
            UIBuilder.Box(buy.Root, 16f, 14f, RungWidth - 32f, 50f, new Vector2(0f, 0f));
            buy.Interactable = can && string.IsNullOrEmpty(_offline) && !_busy;
            string id = family.Id;
            buy.OnClick(delegate { OnBuy(id); });
        }

        private static string RungText(GuildTechFamilyDef family, GuildTechConfig tree)
        {
            List<string> stats = new List<string>();
            for (int s = 0; s < family.RungStats.Count; s++) stats.Add(StatLabel(family.RungStats[s]));
            return "+" + (family.RungScale * 100f).ToString("0.0") + "% of a " + family.Name.ToLowerInvariant()
                   + " hero's power, as " + string.Join(" and ", stats.ToArray()) + ".";
        }

        /// <summary>
        /// The talent slot: three options, one live. THE GUILD'S, so the buttons say so and a
        /// member who cannot set it reads why rather than finding a dead control.
        /// </summary>
        private void DrawSlot(GuildTechConfig tree, GuildTechFamilyDef family, GameSession session,
                              GuildTechState state, int level, RectTransform row)
        {
            int needs = tree.LevelForTalentSlot(family.Id);
            bool open = level >= needs;
            string active = state != null ? state.TalentIn(family.Id) : "";
            bool mayLead = state != null && state.CanPick;

            float left = 352f + tree.RungsPerFamily * (RungWidth + 14f);
            RectTransform host = UIBuilder.Node("Slot", row);
            // The row is as wide as the list's view, which is wider than PageWidth on a wide screen, so the host
            // spans from the rungs to the row's right edge and the talent cards share what it has (R46: at
            // 1920 wide the third card used to run off the end).
            SpanX(host, left, 30f, -22f, TrackHeight - 44f);

            Text heading = EmberforgeUI.Title("SlotTitle", host,
                open ? "GUILD TALENT   -   YOUR GUILD PICKS ONE" : "GUILD TALENT   -   OPENS AT GUILD LEVEL " + needs,
                UITheme.FontTiny - 1, open ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 4f, -2f, 620f, 26f, new Vector2(0f, 1f));

            for (int t = 0; t < family.Talents.Count; t++)
            {
                PassiveDef talent = family.Talents[t];
                bool live = open && talent.Id == active;

                RectTransform card = UIBuilder.Node(talent.Id, host);
                float count = Mathf.Max(1, family.Talents.Count);
                card.anchorMin = new Vector2(t / count, 1f);
                card.anchorMax = new Vector2((t + 1) / count, 1f);
                card.pivot = new Vector2(0.5f, 1f);
                card.sizeDelta = new Vector2(-12f, TrackHeight - 78f);
                card.anchoredPosition = new Vector2(0f, -30f);
                if (live) EmberforgeUI.PlainPanelOn(card); else EmberforgeUI.SunkenOn(card);

                Text tname = EmberforgeUI.Title("Name", card, talent.Name.ToUpperInvariant(), UITheme.FontTiny,
                                                live ? Emberforge.TextGold : open ? Emberforge.TextBody : Emberforge.TextQuiet,
                                                TextAnchor.UpperLeft);
                SpanX(tname.rectTransform, 14f, 14f, -10f, 26f);

                Text text = EmberforgeUI.Body("Text", card, talent.Description, UITheme.FontTiny - 5,
                                              open ? Emberforge.TextBody : Emberforge.TextQuiet, TextAnchor.UpperLeft);
                SpanX(text.rectTransform, 14f, 14f, -38f, 76f);

                if (live)
                {
                    RectTransform chip = EmberforgeUI.Chip("Live", card, "RUNNING", UITheme.Success, UITheme.FontTiny - 5);
                    SpanX(chip, 14f, 14f, 12f, 28f, false);
                    continue;
                }

                bool mayPick = open && mayLead;
                UIButton pick = EmberforgeUI.Button("Pick", card,
                    !open ? "LOCKED" : !mayLead ? "YOUR GUILD'S CHOICE" : "RUN THIS NEXT WEEK",
                    UITheme.FontTiny - 4, mayPick ? ForgeButtonKind.Primary : ForgeButtonKind.Secondary);
                SpanX(pick.Root, 14f, 14f, 10f, 42f, false);
                pick.Interactable = mayPick && string.IsNullOrEmpty(_offline) && !_busy;
                string fid = family.Id, tid = talent.Id;
                pick.OnClick(delegate { OnPick(fid, tid); });
            }
        }

        /// <summary>Spans a child across its parent with the given insets, hung from the top (y is the drop from
        /// the top edge, negative) or standing on the bottom (y is the rise).</summary>
        private static void SpanX(RectTransform rt, float left, float right, float y, float h, bool top = true)
        {
            float a = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, a);
            rt.anchorMax = new Vector2(1f, a);
            rt.pivot = new Vector2(0.5f, a);
            rt.sizeDelta = new Vector2(-(left + right), h);
            rt.anchoredPosition = new Vector2((left - right) * 0.5f, y);
        }

        // ================================================================== spending

        private void OnBuy(string familyId)
        {
            if (_busy) return;
            GuildTechConfig tree = App.Content.GuildTech;
            GuildTechFamilyDef family = tree.Family(familyId);
            int rung, cost;
            string why;
            if (family == null || !App.Session.CanBuyRung(familyId, out rung, out cost, out why)) return;
            App.Audio.Tap();
            App.Modal.ShowConfirm("BUY RUNG " + rung + " ON " + family.Name.ToUpperInvariant() + "?",
                RungText(family, tree) + " It costs " + cost + " " + tree.Currency
                + ", and it is yours whatever guild you are in.",
                "BUY", "NOT YET",
                delegate { StartCoroutine(BuyRoutine(familyId)); }, null);
        }

        private IEnumerator BuyRoutine(string familyId)
        {
            IGuildTechBackend backend = GuildTechBackends.For(App);
            if (backend == null) yield break;
            _busy = true;
            Render();
            GuildTechState state = null;
            string error = "";
            yield return backend.BuyRung(familyId, delegate (GuildTechState s, string e) { state = s; error = e; });
            _busy = false;
            if (state == null)
            {
                App.Notify("Not bought: " + error);
                Render();
                yield break;
            }
            App.Session.ApplyGuildTechState(state);
            // The rung was paid for out of the Waymark ledger, so the purse has to be re-read or
            // the screen keeps showing money that is already spent.
            yield return RefreshPurse();
            App.SaveNow();
            GuildTechFamilyDef family = App.Content.GuildTech.Family(familyId);
            App.Notify((family != null ? family.Name : familyId) + " tech raised.");
            Render();
        }

        private void OnPick(string familyId, string talentId)
        {
            if (_busy) return;
            GuildTechConfig tree = App.Content.GuildTech;
            GuildTechFamilyDef family = tree.Family(familyId);
            PassiveDef talent = family != null ? family.Talent(talentId) : null;
            if (talent == null) return;
            App.Audio.Tap();
            App.Modal.ShowConfirm("RUN " + talent.Name.ToUpperInvariant() + "?",
                talent.Description + "\n\nIt applies to every " + family.Name + " hero every member owns, and it starts at "
                + "the turn of the week - not now. Until then the guild runs what it is running.",
                "SET IT", "NOT YET",
                delegate { StartCoroutine(PickRoutine(familyId, talentId)); }, null);
        }

        private IEnumerator PickRoutine(string familyId, string talentId)
        {
            IGuildTechBackend backend = GuildTechBackends.For(App);
            if (backend == null) yield break;
            _busy = true;
            Render();
            GuildTechState state = null;
            string error = "";
            yield return backend.PickTalent(familyId, talentId,
                delegate (GuildTechState s, string e) { state = s; error = e; });
            _busy = false;
            if (state == null)
            {
                App.Notify("Not set: " + error);
                Render();
                yield break;
            }
            App.Session.ApplyGuildTechState(state);
            App.SaveNow();
            App.Notify("Set. It starts at the turn of the week.");
            Render();
        }

        private IEnumerator RefreshPurse()
        {
            IGuildPathBackend purse = GuildPathBackends.For(App);
            if (purse == null) yield break;
            GuildPathState paid = null;
            string error = "";
            yield return purse.State(delegate (GuildPathState s, string e) { paid = s; error = e; });
            if (paid != null) App.Session.ApplyGuildPathState(paid);
        }
    }
}
