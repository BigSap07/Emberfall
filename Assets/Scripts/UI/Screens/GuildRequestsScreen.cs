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
    /// Who is waiting to be let in.
    ///
    /// ITS OWN SCREEN RATHER THAN A SECTION OF THE GUILD ONE, for a reason the friends list does
    /// not have: a request list is visible to two of the three roles, so folding it into the
    /// roster would mean a screen whose middle third appears and disappears with a promotion. A
    /// separate door is honest about that - the button is simply not on the guild screen for a
    /// member - and it leaves the roster free to be one list rather than a list with a lid.
    ///
    /// THE DATABASE FILTERS RATHER THAN REFUSES, which is why this screen can be opened by
    /// anybody and still be safe. `guild_requests_read_own_or_officer` hands a member zero rows
    /// rather than an error, so a member who reached this some other way sees an empty list with
    /// a sentence, not a failure. The button on the guild screen is what makes it unusual to get
    /// here; the policy is what makes it harmless.
    /// </summary>
    public sealed class GuildRequestsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildRequests; } }

        private const float CardWidth = 1180f;
        private const float RowHeight = 116f;

        private Text _headerSubtitle;
        private ScrollRect _scroll;
        private RectTransform _listHost;
        private Text _note;

        private readonly List<GuildRequestEntry> _requests = new List<GuildRequestEntry>();
        private bool _loading;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "REQUESTS", Router, out _headerSubtitle);

            RectTransform content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                                         new RectOffset(0, 0, 8, 48));
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 20f);

            _listHost = UIBuilder.Node("List", content);
            UIBuilder.Sized(_listHost, CardWidth, 0f);
            UIBuilder.VStack(_listHost, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            ContentSizeFitter fitter = _listHost.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _note = EmberforgeUI.Body("Note", content, "", UITheme.FontTiny,
                                           Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Sized(_note.rectTransform, CardWidth - 160f, 90f);
        }

        public override void OnShow()
        {
            App.RefreshHud();
            Reload();
        }

        /// <summary>Clears the in-flight flag, for the reason GuildScreen.OnHide gives at length:
        /// deactivating the screen kills the coroutine that would have cleared it, and a latch
        /// nothing can release turns the next visit into a screen that never loads.</summary>
        public override void OnHide()
        {
            _loading = false;
        }

        private void Reload()
        {
            if (App.Cloud == null || !App.Cloud.SignedIn)
            {
                _requests.Clear();
                Render();
                return;
            }
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            _headerSubtitle.text = "LOADING";
            List<GuildRequestEntry> fetched = null;
            string error = "";
            yield return App.Cloud.Guild.ListRequests(
                delegate (List<GuildRequestEntry> list, string message)
                {
                    fetched = list;
                    error = message;
                });
            _loading = false;

            _requests.Clear();
            if (fetched != null) _requests.AddRange(fetched);
            if (!string.IsNullOrEmpty(error)) App.Notify(error);
            Render();
        }

        private void Render()
        {
            for (int i = _listHost.childCount - 1; i >= 0; i--)
                Destroy(_listHost.GetChild(i).gameObject);

            CloudCoordinator cloud = App.Cloud;
            if (cloud == null || !cloud.SignedIn)
            {
                _headerSubtitle.text = cloud == null ? "LOCAL ONLY" : "OFFLINE";
                _note.text = cloud == null
                    ? "This build has no cloud configured, so there are no guilds and no requests."
                    : "You are offline, so requests cannot be loaded.";
                return;
            }

            GuildService guild = cloud.Guild;
            if (guild.Current == null)
            {
                _headerSubtitle.text = "NO GUILD";
                _note.text = "You are not in a guild, so nobody is waiting on you.";
                return;
            }
            if (!GuildRules.CanAnswerRequests(guild.MyRole))
            {
                // The honest sentence rather than a blank. The list is empty because the policy
                // handed this role no rows, and saying so is more use than an empty state that
                // implies nobody has asked.
                _headerSubtitle.text = GuildRules.RoleLabel(guild.MyRole);
                _note.text = "Only a leader or an officer sees who has asked to join.";
                return;
            }

            _headerSubtitle.text = _requests.Count == 0 ? "NOBODY WAITING"
                : _requests.Count + (_requests.Count == 1 ? " WAITING" : " WAITING");

            bool full = guild.Current.MemberCount >= GuildRules.MemberCap;
            long now = TimeUtil.NowUnix();
            for (int i = 0; i < _requests.Count; i++) AddRow(_requests[i], full, now);

            _note.text = _requests.Count == 0
                ? "Nobody has asked to join. Give somebody the guild code and they show up here."
                : (full
                    ? "The guild is full, so accepting will be refused until somebody leaves."
                    : "Accepting adds them to the roster immediately. Declining removes the request "
                      + "and they can ask again.");
        }

        private void AddRow(GuildRequestEntry entry, bool full, long now)
        {
            RectTransform row = UIBuilder.Node("Request", _listHost);
            UIBuilder.Sized(row, CardWidth, RowHeight);
            // Every row on this page is waiting on an answer, so every row is framed: the page
            // exists only when there is something to decide.
            EmberforgeUI.PanelOn(row, false);

            string shown = string.IsNullOrEmpty(entry.DisplayName) ? "Wayfarer" : entry.DisplayName;
            Text name = EmberforgeUI.Label("Name", row, shown, UITheme.FontBody, Emberforge.TextBody,
                                        TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 28f, -22f, 460f, 38f, new Vector2(0f, 1f));

            Text code = EmberforgeUI.Label("Code", row,
                                        string.IsNullOrEmpty(entry.FriendCode)
                                            ? "" : FriendCode.Format(entry.FriendCode),
                                        UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(code.rectTransform, 28f, -62f, 460f, 28f, new Vector2(0f, 1f));

            Text asked = EmberforgeUI.Label("Asked", row, AskedLabel(entry.CreatedUnix, now),
                                         UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(asked.rectTransform, 520f, 0f, 300f, 34f, new Vector2(0f, 0.5f));

            string capturedId = entry.Id;
            string capturedName = shown;

            UIButton accept = EmberforgeUI.Button("Accept", row, "ACCEPT", UITheme.FontSmall, ForgeButtonKind.Primary);
            UIBuilder.Box(accept.Root, -230f, 0f, 200f, 72f, new Vector2(1f, 0.5f));
            accept.Interactable = !full;
            accept.OnClick(delegate { OnAccept(capturedId, capturedName); });

            UIButton decline = EmberforgeUI.Button("Decline", row, "DECLINE", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(decline.Root, -24f, 0f, 190f, 72f, new Vector2(1f, 0.5f));
            decline.OnClick(delegate { OnDecline(capturedId, capturedName); });
        }

        private static string AskedLabel(long unix, long now)
        {
            if (unix <= 0) return "";
            long seconds = now - unix;
            if (seconds < 90) return "JUST NOW";
            long minutes = seconds / 60;
            if (minutes < 60) return "ASKED " + minutes + "M AGO";
            long hours = minutes / 60;
            if (hours < 48) return "ASKED " + hours + "H AGO";
            return "ASKED " + (hours / 24) + "D AGO";
        }

        private void OnAccept(string requestId, string name)
        {
            App.Audio.Tap();
            StartCoroutine(AcceptRoutine(requestId, name));
        }

        private IEnumerator AcceptRoutine(string requestId, string name)
        {
            string error = "";
            yield return App.Cloud.Guild.AcceptRequest(requestId,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error) ? name + " joined the guild." : error);
            Reload();
        }

        /// <summary>Declining is the same delete a withdrawal is. One row going away covers both
        /// sides, which is why the service has no Decline beside its RemoveRequest.</summary>
        private void OnDecline(string requestId, string name)
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm("DECLINE " + name.ToUpperInvariant() + "?",
                "The request goes away. They are not told, and they can ask again.",
                "DECLINE", "KEEP IT",
                delegate { StartCoroutine(DeclineRoutine(requestId, name)); }, null);
        }

        private IEnumerator DeclineRoutine(string requestId, string name)
        {
            string error = "";
            yield return App.Cloud.Guild.RemoveRequest(requestId,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error) ? name + " declined." : error);
            Reload();
        }
    }
}
