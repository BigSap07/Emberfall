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
    /// THE GUILD PATH (claude/DESIGN-GUILDS-PHASE-TWO.md, system 2): the Waymarks the guild dungeon
    /// has paid this player, what they pay for, and the nodes to spend them on.
    ///
    /// THE PLAYER'S, NOT THE GUILD'S. Reached from the formation screen, where the lines it sells
    /// are switched, and from the dungeon that pays for it; it asks the server for the account's
    /// own ledger and never for the guild's. A player who left their guild yesterday opens this and finds everything still
    /// here, which is the property that keeps the Path from becoming lock-in.
    ///
    /// THE TREE IS DRAWN FROM guild-path.json. A node's column is its depth and a child sits on its
    /// parent's row when it is the parent's first child, so the file decides the picture and a new
    /// node needs no layout code.
    ///
    /// NOTHING HERE MAKES A HERO STRONGER, and the screen says so under the tree: a player deciding
    /// whether to join a guild should be able to read that it will not out-level them.
    /// </summary>
    public sealed class GuildPathScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildPath; } }

        private const float PageWidth = 1680f;
        private const float NodeWidth = 500f;
        private const float NodeHeight = 214f;
        private const float ColumnGap = 90f;
        private const float RowGap = 28f;
        private const float StatusHeight = 176f;

        private Text _headerSubtitle;
        private RectTransform _content;
        private ForgePill _balance;
        private Text _balanceNote;
        private Text _earned;
        private Text _lines;
        private Text _pays;
        private RectTransform _tree;
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
            ScreenChrome.ForgedHeader(Root, "GUILD PATH", Router, out _headerSubtitle);

            ScrollRect scroll;
            _content = UIBuilder.ScrollList("Content", Root, out scroll, 18f, new RectOffset(0, 0, 8, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), UITheme.ScreenPadding,
                                 UITheme.TopBarHeight + 118f, UITheme.ScreenPadding, 20f);

            BuildStatus();

            _tree = UIBuilder.Node("Tree", _content);
            UIBuilder.Sized(_tree, PageWidth, NodeHeight);

            _note = EmberforgeUI.Body("Note", _content, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Sized(_note.rectTransform, PageWidth - 200f, 64f);
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

            Text heading = EmberforgeUI.Title("PaysHeading", card, "WHAT THE GUILD DUNGEON PAYS", UITheme.FontSmall,
                                              Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 420f, -26f, 820f, 32f, new Vector2(0f, 1f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);

            _pays = EmberforgeUI.Body("Pays", card, "", UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_pays.rectTransform, 420f, -62f, 860f, 100f, new Vector2(0f, 1f));

            _earned = EmberforgeUI.Label("Earned", card, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperRight);
            UIBuilder.Box(_earned.rectTransform, -44f, -34f, 320f, 32f, new Vector2(1f, 1f));

            _lines = EmberforgeUI.Label("Lines", card, "", UITheme.FontSmall, Emberforge.TextGold, TextAnchor.UpperRight);
            UIBuilder.Box(_lines.rectTransform, -44f, -78f, 320f, 32f, new Vector2(1f, 1f));
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

        private IEnumerator LoadRoutine()
        {
            IGuildPathBackend backend = GuildPathBackends.For(App);
            if (backend == null)
            {
                _offline = "This build has no cloud configured, so the guild dungeon pays nothing here.";
            }
            else
            {
                _headerSubtitle.text = "LOADING";
                GuildPathState state = null;
                string error = "";
                yield return backend.State(delegate (GuildPathState s, string e) { state = s; error = e; });
                if (state != null)
                {
                    _offline = "";
                    App.Session.ApplyGuildPathState(state);
                    App.SaveNow();
                }
                else _offline = error;
            }
            _loading = false;
            Render();
        }

        // ================================================================== render

        private void Render()
        {
            GuildPathConfig cfg = App.Content.GuildPath;
            GameSession session = App.Session;
            string currency = cfg.Currency.ToUpperInvariant();

            _headerSubtitle.text = string.IsNullOrEmpty(_offline)
                ? "YOURS, WHATEVER GUILD YOU ARE IN"
                : "OFFLINE   -   WHAT YOU OWNED WHEN YOU LAST CONNECTED";
            _balance.Value.text = session.Profile.Waymarks.ToString("N0");
            _balanceNote.text = currency + " TO SPEND";
            _earned.text = session.Profile.GuildPathNodes.Count + " OF " + cfg.Nodes.Count + " NODES OWNED";
            _lines.text = "LINES   " + session.LinesAllowed + " OF " + (1 + CountLines(cfg));
            _pays.text = cfg.PayAttempt + " for your first attempt of the week, won or lost.   "
                       + cfg.PayClear + " more for your first clear, on any floor.\n"
                       + "Up to " + cfg.PayBoardPoint + " for each of floor " + cfg.BoardFloor + "'s boards once the week "
                       + "is over: first place pays it all, last place nothing, and the rest their share of the board they beat.";

            DrawTree(cfg, session);

            _note.text = !string.IsNullOrEmpty(_offline) ? _offline
                : "Every node here is a way to hold more options. None of them makes a hero stronger, so a player "
                  + "outside a guild levels exactly as fast as one inside it. What you own stays yours if you leave.";
        }

        private static int CountLines(GuildPathConfig cfg)
        {
            int n = 0;
            for (int i = 0; i < cfg.Nodes.Count; i++) if (cfg.Nodes[i].Kind == GuildPathNodeKind.Line) n++;
            return n;
        }

        /// <summary>Column is depth; a first child shares its parent's row, a later one takes the
        /// next free row in its column. Connectors are drawn first so the cards sit over them.</summary>
        private void DrawTree(GuildPathConfig cfg, GameSession session)
        {
            for (int i = _tree.childCount - 1; i >= 0; i--) Destroy(_tree.GetChild(i).gameObject);

            Dictionary<string, int> rowOf = new Dictionary<string, int>();
            Dictionary<int, int> nextFree = new Dictionary<int, int>();
            int rows = 0;
            for (int i = 0; i < cfg.Nodes.Count; i++)
            {
                GuildPathNodeDef n = cfg.Nodes[i];
                int column = n.Depth - 1;
                int free;
                nextFree.TryGetValue(column, out free);
                int parentRow;
                int row = !string.IsNullOrEmpty(n.Requires) && rowOf.TryGetValue(n.Requires, out parentRow)
                    ? Mathf.Max(parentRow, free) : free;
                rowOf[n.Id] = row;
                nextFree[column] = row + 1;
                rows = Mathf.Max(rows, row + 1);
            }
            float height = rows * NodeHeight + (rows - 1) * RowGap;
            UIBuilder.Sized(_tree, PageWidth, height);

            for (int i = 0; i < cfg.Nodes.Count; i++)
            {
                GuildPathNodeDef n = cfg.Nodes[i];
                GuildPathNodeDef parent = cfg.Node(n.Requires);
                if (parent == null) continue;
                Connect(Left(parent) + NodeWidth, Middle(rowOf[parent.Id]), Left(n), Middle(rowOf[n.Id]),
                        session.OwnsGuildPathNode(parent.Id));
            }
            for (int i = 0; i < cfg.Nodes.Count; i++) DrawNode(cfg.Nodes[i], rowOf[cfg.Nodes[i].Id], cfg, session);
        }

        private static float Left(GuildPathNodeDef n) { return (n.Depth - 1) * (NodeWidth + ColumnGap); }
        private static float Middle(int row) { return row * (NodeHeight + RowGap) + NodeHeight * 0.5f; }

        /// <summary>An elbow from a parent's right edge to a child's left edge, lit gold when the
        /// parent is owned - the way onward is open - and quiet when it is not.</summary>
        private void Connect(float x0, float y0, float x1, float y1, bool lit)
        {
            Color c = lit ? Emberforge.GoldMid : Emberforge.GoldDeep;
            float t = Emberforge.RuleHeight;
            float mid = x0 + (x1 - x0) * 0.5f;
            Bar(x0, y0 - t * 0.5f, mid - x0 + t * 0.5f, t, c);
            if (Mathf.Abs(y1 - y0) > 0.5f) Bar(mid - t * 0.5f, y0 - t * 0.5f, t, y1 - y0 + t, c);
            Bar(mid - t * 0.5f, y1 - t * 0.5f, x1 - mid + t * 0.5f, t, c);
        }

        private void Bar(float x, float y, float w, float h, Color color)
        {
            Image bar = UIBuilder.Img("Link", _tree, TextureLab.White, color);
            bar.raycastTarget = false;
            UIBuilder.Box(bar.rectTransform, x, -y, w, h, new Vector2(0f, 1f));
        }

        private void DrawNode(GuildPathNodeDef n, int row, GuildPathConfig cfg, GameSession session)
        {
            bool owned = session.OwnsGuildPathNode(n.Id);
            bool open = string.IsNullOrEmpty(n.Requires) || session.OwnsGuildPathNode(n.Requires);
            long balance = session.Profile.Waymarks;
            bool affordable = balance >= n.Cost;

            ForgeCard card = EmberforgeUI.SelectableCard(n.Id, _tree, null);
            UIBuilder.Box(card.Root, Left(n), -row * (NodeHeight + RowGap), NodeWidth, NodeHeight, new Vector2(0f, 1f));
            EmberforgeUI.SetCardSelected(card, owned);
            card.Button.Button.interactable = false;

            Text name = EmberforgeUI.Title("Name", card.Body, n.Name.ToUpperInvariant(), UITheme.FontBody,
                                           owned ? Emberforge.TextGold : Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 26f, -20f, NodeWidth - 236f, 36f, new Vector2(0f, 1f));
            // LINES KEEP TALENTS ran into its cost chip at full size; the name shrinks to fit instead.
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = UITheme.FontTiny - 2;
            name.resizeTextMaxSize = UITheme.FontBody;
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform chip = owned
                ? EmberforgeUI.Chip("Owned", card.Body, "OWNED", UITheme.Success, UITheme.FontTiny - 2)
                : EmberforgeUI.Chip("Cost", card.Body, n.Cost + " " + cfg.Currency.ToUpperInvariant(), UITheme.Waymark, UITheme.FontTiny - 2);
            UIBuilder.Box(chip, -20f, -20f, 176f, 36f, new Vector2(1f, 1f));

            Text text = EmberforgeUI.Body("Text", card.Body, n.Text, UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(text.rectTransform, 26f, -66f, NodeWidth - 52f, 70f, new Vector2(0f, 1f));

            if (owned)
            {
                Text kept = EmberforgeUI.Label("Kept", card.Body, "YOURS, IN ANY GUILD OR NONE", UITheme.FontTiny - 2,
                                               Emberforge.TextQuiet, TextAnchor.LowerLeft);
                UIBuilder.Box(kept.rectTransform, 26f, 20f, NodeWidth - 52f, 28f, new Vector2(0f, 0f));
                return;
            }

            string label = !open ? "NEEDS " + NameOf(cfg, n.Requires)
                         : !affordable ? (n.Cost - balance) + " MORE " + cfg.Currency.ToUpperInvariant()
                         : "UNLOCK";
            UIButton unlock = EmberforgeUI.Button("Unlock", card.Root, label, UITheme.FontTiny,
                                                  open && affordable ? ForgeButtonKind.Primary : ForgeButtonKind.Secondary);
            UIBuilder.Box(unlock.Root, 26f, 18f, NodeWidth - 52f, 58f, new Vector2(0f, 0f));
            unlock.Interactable = open && affordable && string.IsNullOrEmpty(_offline) && !_busy;
            string id = n.Id;
            unlock.OnClick(delegate { OnUnlock(id); });
        }

        private static string NameOf(GuildPathConfig cfg, string id)
        {
            GuildPathNodeDef n = cfg.Node(id);
            return (n != null ? n.Name : id).ToUpperInvariant();
        }

        // ================================================================== spending

        private void OnUnlock(string id)
        {
            if (_busy) return;
            GuildPathNodeDef n = App.Content.GuildPath.Node(id);
            if (n == null) return;
            App.Audio.Tap();
            App.Modal.ShowConfirm("UNLOCK " + n.Name.ToUpperInvariant() + "?",
                n.Text + " It costs " + n.Cost + " " + App.Content.GuildPath.Currency + ", and it is yours whatever guild you are in.",
                "UNLOCK", "NOT YET",
                delegate { StartCoroutine(UnlockRoutine(id)); }, null);
        }

        private IEnumerator UnlockRoutine(string id)
        {
            IGuildPathBackend backend = GuildPathBackends.For(App);
            if (backend == null) yield break;
            _busy = true;
            Render();
            GuildPathState state = null;
            string error = "";
            yield return backend.Unlock(id, delegate (GuildPathState s, string e) { state = s; error = e; });
            _busy = false;
            if (state == null)
            {
                App.Notify("Not unlocked: " + error);
                Render();
                yield break;
            }
            App.Session.ApplyGuildPathState(state);
            App.SaveNow();
            GuildPathNodeDef n = App.Content.GuildPath.Node(id);
            App.Notify((n != null ? n.Name : id) + " is yours."
                       + (n != null && n.Kind == GuildPathNodeKind.Line ? " Switch lines on your banner." : ""));
            Render();
        }
    }
}
