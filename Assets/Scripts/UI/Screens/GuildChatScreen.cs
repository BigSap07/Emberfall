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
    /// Guild chat: the newest hundred lines, and a box to add one.
    ///
    /// IT POLLS, AND ONLY WHILE IT IS OPEN. There is no websocket in this project and adding one
    /// is the single thing SupabaseClient's own note says would reopen the decision not to take
    /// the Supabase SDK - a realtime subscription is the one shape plain REST does not make easy.
    /// A guild of thirty is not a chat room that needs sub-second delivery, so it asks every few
    /// seconds while somebody is looking at it, and not at all when nobody is: the poll starts in
    /// OnShow and stops in OnHide, so leaving this screen ends the traffic rather than leaving a
    /// coroutine talking to a server about a screen nobody can see.
    ///
    /// THE WHOLE WINDOW IS RE-READ EACH TIME rather than paged forward from the last id. The
    /// reasoning is on GuildChatLog and it is not about bandwidth: `guild_messages.id` comes from
    /// a sequence, and a sequence hands out numbers in the order they are asked for rather than
    /// the order rows commit in, so a cursor can step over a message permanently. A chat that
    /// silently drops a line is a worse bug than a chat that re-reads twenty kilobytes.
    ///
    /// The rate limit is the database's, and this screen is honest about it rather than hiding
    /// it: a refusal comes back as a 429, the service turns it into one sentence, and the typed
    /// line stays in the box so the player can send it a second later.
    ///
    /// A SHARED DUNGEON ATTEMPT IS A CARD, NOT A LINE. The message carries the whole attempt
    /// (GuildMessageEntry.Attempt, embedded by the chat read), so the card shows the floor, the
    /// result, the line that fought and the boards it entered, and it has two buttons: WATCH, which
    /// replays that exact fight from the record, and RUN IT YOURSELF, which opens the same floor
    /// with your own roster. That is the guild dungeon's handover, and it lives here because this
    /// is where a guild talks.
    ///
    /// The chat talks to the server through IGuildDungeonBackend rather than GuildService
    /// directly, for the one reason the backend exists: the capture tool can put an in-memory
    /// guild behind it and drive the handover through this screen with two members.
    /// </summary>
    public sealed class GuildChatScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildChat; } }

        private const float CardWidth = 1180f;
        private const float PollSeconds = 4f;

        private Text _headerSubtitle;
        private ScrollRect _scroll;
        private RectTransform _listHost;
        private Text _emptyNote;
        private InputField _entry;
        private UIButton _sendButton;
        private Text _sendNote;

        private readonly GuildChatLog _log = new GuildChatLog();
        private Coroutine _poll;
        private bool _fetching;
        private bool _sending;

        /// <summary>The window as it was last drawn, so a poll that changed nothing does not
        /// destroy and rebuild a hundred rows underneath a player who is reading them.</summary>
        private string _drawnFingerprint = "";

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "GUILD CHAT", Router, out _headerSubtitle);

            _listHost = UIBuilder.ScrollList("Content", Root, out _scroll, 10f,
                                             new RectOffset(0, 0, 8, 16));
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 168f);

            _emptyNote = EmberforgeUI.Body("Empty", _listHost, "", UITheme.FontTiny,
                                                Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Sized(_emptyNote.rectTransform, CardWidth - 160f, 70f);

            BuildComposer();
        }

        private void BuildComposer()
        {
            RectTransform bar = UIBuilder.Node("Composer", Root);
            UIBuilder.Box(bar, 0f, 24f, CardWidth, 128f, new Vector2(0.5f, 0f));

            EmberforgeUI.PanelOn(bar, false);

            _entry = EmberforgeUI.TextField("Entry", bar, "Say something", UITheme.FontSmall,
                                         GuildRules.MessageMax, false);
            UIBuilder.Box(_entry.GetComponent<RectTransform>(), 24f, 0f, 820f, 80f, new Vector2(0f, 0.5f));

            _sendButton = EmberforgeUI.Button("Send", bar, "SEND", UITheme.FontBody, ForgeButtonKind.Primary);
            UIBuilder.Box(_sendButton.Root, -24f, 0f, 280f, 80f, new Vector2(1f, 0.5f));
            _sendButton.OnClick(OnSend);

            _sendNote = EmberforgeUI.Label("SendNote", bar, "", UITheme.FontTiny - 4,
                                        Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.Box(_sendNote.rectTransform, 24f, 6f, 820f, 22f, new Vector2(0f, 0f));
        }

        // ================================================================== lifecycle

        public override void OnShow()
        {
            App.RefreshHud();
            _log.Clear();
            _drawnFingerprint = "";
            Render();

            IGuildDungeonBackend backend = Backend;
            if (backend == null || !backend.Ready) return;
            if (_poll == null) _poll = StartCoroutine(PollRoutine());
        }

        /// <summary>
        /// Stops the poll. Not an optimisation - it is the whole of "no traffic when the chat is
        /// closed", which is the only promise this screen makes about what it costs.
        /// </summary>
        public override void OnHide()
        {
            if (_poll != null) { StopCoroutine(_poll); _poll = null; }
            _fetching = false;
        }

        private IGuildDungeonBackend Backend { get { return GuildDungeonBackends.For(App); } }

        private IEnumerator PollRoutine()
        {
            while (true)
            {
                yield return FetchRoutine();
                // Presence, at most once a minute whatever this asks for - the service throttles
                // it, so a four-second poll does not become a write every four seconds.
                if (App.Cloud != null && GuildDungeonBackends.Override == null) yield return App.Cloud.Guild.Touch();
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        private IEnumerator FetchRoutine()
        {
            if (_fetching) yield break;
            _fetching = true;

            List<GuildMessageEntry> fetched = null;
            string error = "";
            IGuildDungeonBackend backend = Backend;
            if (backend == null) { _fetching = false; yield break; }
            yield return backend.FetchChat(
                delegate (List<GuildMessageEntry> list, string message)
                {
                    fetched = list;
                    error = message;
                });
            _fetching = false;

            if (fetched == null)
            {
                // A failed poll is silent. The screen already shows whatever it last had, the next
                // poll is four seconds away, and a toast every four seconds on a bad connection is
                // worse than saying nothing.
                _headerSubtitle.text = "RECONNECTING";
                yield break;
            }
            _log.Replace(fetched);
            Render();
        }

        // ================================================================== render

        private void Render()
        {
            IGuildDungeonBackend backend = Backend;
            CloudCoordinator cloud = App.Cloud;
            if (backend == null || (GuildDungeonBackends.Override == null && !cloud.SignedIn))
            {
                _headerSubtitle.text = backend == null ? "LOCAL ONLY" : "OFFLINE";
                _emptyNote.text = backend == null
                    ? "Guild chat needs an online account, and this version cannot connect to one. "
                      + "Everything else in the game works as normal."
                    : "You are offline, so the chat cannot be loaded. It will be here when you are "
                      + "back online. Connect to the internet and open this page again.";
                SetComposerEnabled(false);
                ClearRows();
                return;
            }
            if (!backend.Ready)
            {
                _headerSubtitle.text = "NO GUILD";
                _emptyNote.text = "You are not in a guild yet.";
                SetComposerEnabled(false);
                ClearRows();
                return;
            }

            _headerSubtitle.text = backend.GuildName.ToUpperInvariant();
            SetComposerEnabled(true);
            _sendNote.text = "Up to " + GuildRules.MessageMax + " characters, one message every "
                           + GuildRules.ChatCooldownSeconds + " seconds.";

            string fingerprint = Fingerprint();
            if (fingerprint == _drawnFingerprint)
            {
                _emptyNote.text = _log.Count == 0 ? EmptyText() : "";
                return;
            }
            _drawnFingerprint = fingerprint;

            // Oldest first, top to bottom, because the VStack lays children out in sibling order
            // and the log is already sorted ascending. NOTHING in AddRow may reorder a sibling:
            // the first version added the row's background with SetAsFirstSibling, and since that
            // Image lives on the ROW rather than on a child, every row it added jumped to the top
            // and the whole conversation rendered backwards.
            ClearRows();
            long now = TimeUtil.NowUnix();
            for (int i = 0; i < _log.Count; i++) AddRow(_log.Messages[i], now);

            _emptyNote.text = _log.Count == 0 ? EmptyText() : "";
            _emptyNote.rectTransform.SetAsLastSibling();

            // The newest line is the one somebody opened this to read.
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        private string EmptyText()
        {
            return "Nothing said yet. Whatever goes here stays for everybody in the guild to read.";
        }

        /// <summary>The ids in the window, in order. Compared rather than counted, so a message
        /// removed and another added in the same poll still redraws.</summary>
        private string Fingerprint()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _log.Count; i++) { sb.Append(_log.Messages[i].Id); sb.Append(','); }
            return sb.ToString();
        }

        private void ClearRows()
        {
            for (int i = _listHost.childCount - 1; i >= 0; i--)
            {
                GameObject child = _listHost.GetChild(i).gameObject;
                if (_emptyNote != null && child == _emptyNote.gameObject) continue;
                Destroy(child);
            }
        }

        /// <summary>
        /// One line. The author, when, and what they said - and the player's own lines are tinted
        /// rather than aligned to the other side, because at 1180 wide a right-aligned bubble
        /// leaves a metre of empty panel and reads as a layout fault rather than as authorship.
        /// </summary>
        private void AddRow(GuildMessageEntry entry, long now)
        {
            if (entry.AttemptId != 0) { AddAttemptCard(entry, now); return; }

            RectTransform row = UIBuilder.Node("Line", _listHost);

            // YOUR OWN LINE stands proud of the sheet and everyone else's is set into it -
            // depth carrying authorship, which is the same thing the friends list and the guild
            // roster do with their own rows. It is still not right-aligned, for the reason the
            // note above gives: at 1180 wide a right-aligned bubble leaves a metre of empty panel.
            if (entry.IsMine) EmberforgeUI.PlainPanelOn(row);
            else EmberforgeUI.SunkenOn(row);

            Text who = EmberforgeUI.Label("Who", row,
                                       (string.IsNullOrEmpty(entry.DisplayName)
                                            ? "Wayfarer" : entry.DisplayName).ToUpperInvariant()
                                       + "   " + WhenLabel(entry.CreatedUnix, now),
                                       UITheme.FontTiny - 4,
                                       entry.IsMine ? Emberforge.TextGold : Emberforge.TextQuiet,
                                       TextAnchor.UpperLeft);
            UIBuilder.Box(who.rectTransform, 22f, -10f, CardWidth - 60f, 24f, new Vector2(0f, 1f));

            Text body = EmberforgeUI.Body("Body", row, entry.Body, UITheme.FontSmall,
                                               Emberforge.TextBody, TextAnchor.UpperLeft);

            // THE BOX COMES BEFORE THE MEASUREMENT, and a capture is what taught that. A wrapping
            // Text computes preferredHeight against the width its RectTransform currently has, and
            // a label read before it has been placed is measured against whatever Node's default
            // rect happened to be - which produced 230-unit cells for a one-line message. Placing
            // it first makes the number a real answer about a 1120-wide label.
            UIBuilder.Box(body.rectTransform, 22f, -36f, CardWidth - 60f, 30f, new Vector2(0f, 1f));
            float text = Mathf.Max(30f, body.preferredHeight);
            float height = 20f + 24f + text;

            UIBuilder.Sized(row, CardWidth, height);
            UIBuilder.Box(body.rectTransform, 22f, -36f, CardWidth - 60f, text, new Vector2(0f, 1f));
        }

        /// <summary>
        /// A SHARED ATTEMPT, drawn as something to play. Framed, because it is the one thing in
        /// the chat that has an action on it; the author's name and the time sit where a line's
        /// would, so it still reads as said BY somebody.
        ///
        /// The card checks the one thing it can check without running a fight - whether the record
        /// was fought under this build's content - and says so on the card rather than offering a
        /// WATCH that would only refuse. Everything else Replay decides when WATCH is pressed.
        /// </summary>
        private void AddAttemptCard(GuildMessageEntry entry, long now)
        {
            const float height = 206f;
            GuildDungeonAttempt a = entry.Attempt;
            RectTransform card = UIBuilder.Node("Attempt", _listHost);
            UIBuilder.Sized(card, CardWidth, height);
            EmberforgeUI.PanelOn(card, false);

            string author = string.IsNullOrEmpty(entry.DisplayName) ? "Wayfarer" : entry.DisplayName;
            Text who = EmberforgeUI.Label("Who", card, author.ToUpperInvariant() + " SHARED A DUNGEON ATTEMPT   "
                                          + WhenLabel(entry.CreatedUnix, now), UITheme.FontTiny - 4,
                                          entry.IsMine ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(who.rectTransform, 30f, -16f, 700f, 24f, new Vector2(0f, 1f));

            if (a == null)
            {
                Text gone = EmberforgeUI.Body("Gone", card, entry.Body + "\nThis attempt can no longer be played.",
                                              UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.UpperLeft);
                UIBuilder.Box(gone.rectTransform, 30f, -48f, CardWidth - 60f, 120f, new Vector2(0f, 1f));
                return;
            }

            Text title = EmberforgeUI.Title("Title", card, "FLOOR " + a.Floor + "   "
                                            + (a.Victory ? "CLEARED IN " + a.Turns + " TURNS" : "FELL AFTER " + a.Turns + " TURNS"),
                                            UITheme.FontHeading, a.Victory ? Emberforge.TextGold : UITheme.Danger,
                                            TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 30f, -44f, 640f, 44f, new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform line = UIBuilder.Node("Line", card);
            UIBuilder.Box(line, 30f, -96f, 5 * 70f, 64f, new Vector2(0f, 1f));
            GuildDungeonFlow.DrawLine(line, App.Content, a.Team, 0f, 0f, 64f, 6f);

            // What the attempt entered, or how it went.
            List<GuildDungeonBoard> met = GuildDungeon.BoardsMet(App.Content, a);
            string detail = a.Victory
                ? (met.Count == 0 ? "No board entered." : "Entered " + BoardList(met) + ".")
                : Mathf.Max(0, a.Fallen) + " of " + a.Team.Units.Count + " heroes fell. Attempt " + a.Attempt + ".";
            Text sub = EmberforgeUI.Body("Detail", card, detail, UITheme.FontTiny - 4, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(sub.rectTransform, 400f, -100f, 360f, 60f, new Vector2(0f, 1f));

            bool sameContent = !string.IsNullOrEmpty(a.ContentVersion) && a.ContentVersion == App.Content.ContentVersion;
            GuildDungeonState state = Router.GuildDungeon.State;
            IGuildDungeonBackend backend = Backend;
            bool mine = backend != null && a.UserId == backend.SelfUserId;
            bool current = !state.Loaded || state.Cycle == a.Cycle;

            UIButton watch = EmberforgeUI.Button("Watch", card, sameContent ? "WATCH" : "CAN'T REPLAY",
                                                 UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(watch.Root, -30f, -36f, 330f, 70f, new Vector2(1f, 1f));
            watch.Interactable = sameContent;
            GuildDungeonAttempt captured = a;
            watch.OnClick(delegate
            {
                App.Audio.Tap();
                GuildDungeonFlow.Watch(App, Router, captured, ScreenId.GuildChat, "BACK TO CHAT");
            });

            if (!mine)
            {
                UIButton run = EmberforgeUI.Button("RunIt", card, current ? "RUN IT YOURSELF" : "CYCLE ENDED",
                                                   UITheme.FontSmall, ForgeButtonKind.Secondary);
                UIBuilder.Box(run.Root, -30f, -116f, 330f, 64f, new Vector2(1f, 1f));
                run.Interactable = current;
                run.OnClick(delegate
                {
                    App.Audio.Tap();
                    GuildDungeonFlow.TryItYourself(App, Router, captured);
                });
            }

            if (!sameContent)
            {
                Text why = EmberforgeUI.Label("Why", card,
                    string.IsNullOrEmpty(a.ContentVersion) ? "Recorded before replays were versioned."
                                                           : "Fought under earlier game numbers.",
                    UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperRight);
                UIBuilder.Box(why.rectTransform, -380f, -128f, 380f, 22f, new Vector2(1f, 1f));
            }
        }

        private static string BoardList(List<GuildDungeonBoard> boards)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < boards.Count; i++) names.Add(GuildDungeon.BoardTitle(boards[i]));
            return string.Join(", ", names.ToArray());
        }

        private static string WhenLabel(long unix, long now)
        {
            if (unix <= 0) return "";
            long seconds = now - unix;
            if (seconds < 90) return "JUST NOW";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "M AGO";
            long hours = minutes / 60;
            if (hours < 48) return hours + "H AGO";
            return (hours / 24) + "D AGO";
        }

        private void SetComposerEnabled(bool enabled)
        {
            _entry.interactable = enabled && !_sending;
            _sendButton.Interactable = enabled && !_sending;
        }

        // ================================================================== sending

        private void OnSend()
        {
            IGuildDungeonBackend backend = Backend;
            if (backend == null || !backend.Ready) { App.Notify("You are offline."); return; }
            if (_sending) return;
            App.Audio.Tap();
            StartCoroutine(SendRoutine(_entry.text));
        }

        /// <summary>
        /// Sends, then re-reads rather than appending what was typed.
        ///
        /// The line the server stored is not always the line that was sent - it is trimmed - and a
        /// screen that appended its own copy would show a different string from the one everybody
        /// else reads, until the next poll quietly replaced it. Re-reading costs one request and
        /// means there is only ever one version of a message on this screen.
        ///
        /// THE BOX IS ONLY CLEARED ON SUCCESS. A refusal - too long, too fast, offline - leaves
        /// what was typed exactly where it was, because the player is going to want to send it
        /// again in a second and retyping it is the wrong price for a rate limit.
        /// </summary>
        private IEnumerator SendRoutine(string typed)
        {
            _sending = true;
            SetComposerEnabled(true);

            bool ok = false;
            string error = "";
            yield return Backend.PostMessage(typed,
                delegate (bool success, string message) { ok = success; error = message; });

            _sending = false;
            SetComposerEnabled(true);

            if (!ok)
            {
                App.Notify(string.IsNullOrEmpty(error) ? "That did not send." : error);
                yield break;
            }
            _entry.text = "";
            yield return FetchRoutine();
        }
    }
}
