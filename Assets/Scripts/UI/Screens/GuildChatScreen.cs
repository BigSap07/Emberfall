using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
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
            ScreenChrome.Header(Root, "GUILD CHAT", Router, out _headerSubtitle);

            _listHost = UIBuilder.ScrollList("Content", Root, out _scroll, 10f,
                                             new RectOffset(0, 0, 8, 16));
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 168f);

            _emptyNote = UIBuilder.WrappedLabel("Empty", _listHost, "", UITheme.FontTiny,
                                                UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Sized(_emptyNote.rectTransform, CardWidth - 160f, 70f);

            BuildComposer();
        }

        private void BuildComposer()
        {
            RectTransform bar = UIBuilder.Node("Composer", Root);
            UIBuilder.Box(bar, 0f, 24f, CardWidth, 128f, new Vector2(0.5f, 0f));

            Image bg = bar.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", bar, UITheme.BorderSoft, 18, 2);

            _entry = UIBuilder.TextField("Entry", bar, "Say something", UITheme.FontSmall,
                                         GuildRules.MessageMax, false);
            UIBuilder.Box(_entry.GetComponent<RectTransform>(), 24f, 0f, 820f, 80f, new Vector2(0f, 0.5f));

            _sendButton = UIBuilder.GoldButton("Send", bar, "SEND", UITheme.FontBody);
            UIBuilder.Box(_sendButton.Root, -24f, 0f, 280f, 80f, new Vector2(1f, 0.5f));
            _sendButton.OnClick(OnSend);

            _sendNote = UIBuilder.Label("SendNote", bar, "", UITheme.FontTiny - 4,
                                        UITheme.TextDim, TextAnchor.LowerLeft);
            UIBuilder.Box(_sendNote.rectTransform, 24f, 6f, 820f, 22f, new Vector2(0f, 0f));
        }

        // ================================================================== lifecycle

        public override void OnShow()
        {
            App.RefreshHud();
            _log.Clear();
            _drawnFingerprint = "";
            Render();

            if (App.Cloud == null || !App.Cloud.SignedIn) return;
            if (App.Cloud.Guild.Current == null) return;
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

        private IEnumerator PollRoutine()
        {
            while (true)
            {
                yield return FetchRoutine();
                // Presence, at most once a minute whatever this asks for - the service throttles
                // it, so a four-second poll does not become a write every four seconds.
                yield return App.Cloud.Guild.Touch();
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        private IEnumerator FetchRoutine()
        {
            if (_fetching) yield break;
            _fetching = true;

            List<GuildMessageEntry> fetched = null;
            string error = "";
            yield return App.Cloud.Guild.FetchChat(
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
            CloudCoordinator cloud = App.Cloud;
            if (cloud == null || !cloud.SignedIn)
            {
                _headerSubtitle.text = cloud == null ? "LOCAL ONLY" : "OFFLINE";
                _emptyNote.text = cloud == null
                    ? "This build has no cloud configured, so there is no guild chat."
                    : "You are offline, so the chat cannot be loaded. It will be here when you are "
                      + "back online.";
                SetComposerEnabled(false);
                ClearRows();
                return;
            }
            if (cloud.Guild.Current == null)
            {
                _headerSubtitle.text = "NO GUILD";
                _emptyNote.text = "You are not in a guild yet.";
                SetComposerEnabled(false);
                ClearRows();
                return;
            }

            _headerSubtitle.text = cloud.Guild.Current.Name.ToUpperInvariant();
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
            RectTransform row = UIBuilder.Node("Line", _listHost);

            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(14);
            bg.type = Image.Type.Sliced;
            bg.color = entry.IsMine
                ? UITheme.WithAlpha(UITheme.PanelRaised, 0.95f) : UITheme.PanelSunken;

            Text who = UIBuilder.Label("Who", row,
                                       (string.IsNullOrEmpty(entry.DisplayName)
                                            ? "Wayfarer" : entry.DisplayName).ToUpperInvariant()
                                       + "   " + WhenLabel(entry.CreatedUnix, now),
                                       UITheme.FontTiny - 4,
                                       entry.IsMine ? UITheme.Gold : UITheme.TextDim,
                                       TextAnchor.UpperLeft);
            UIBuilder.Box(who.rectTransform, 22f, -10f, CardWidth - 60f, 24f, new Vector2(0f, 1f));

            Text body = UIBuilder.WrappedLabel("Body", row, entry.Body, UITheme.FontSmall,
                                               UITheme.TextPrimary, TextAnchor.UpperLeft);

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
            if (App.Cloud == null || !App.Cloud.SignedIn) { App.Notify("You are offline."); return; }
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
            yield return App.Cloud.Guild.PostMessage(typed,
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
