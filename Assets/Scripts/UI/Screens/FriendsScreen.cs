using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Friends: the people whose line you can go and fight.
    ///
    /// ONE LIST, SPLIT BY WHAT IT WANTS FROM YOU rather than three lists that happen to hold the
    /// same kind of row. A friendship is one database row in one of two states, and the only thing
    /// that differs between the sections here is whose turn it is: a request waiting on YOU has
    /// two buttons and is at the top, a request waiting on THEM has one and says so, and an
    /// accepted friend opens their team. Splitting it any other way would invent categories the
    /// data does not have.
    ///
    /// EVERY RULE THIS SCREEN APPEARS TO ENFORCE IS ENFORCED IN THE DATABASE, and none of them is
    /// enforced here. Only the addressee can accept; a snapshot is unreadable until a friendship
    /// is accepted; neither party can rewrite who a friendship is between. This screen decides
    /// what to DRAW - which is why Accept appears on exactly the rows where it would work, and why
    /// a row where it would not simply does not offer it rather than offering it and failing.
    /// Tools/supabase-rules-test.py is what proves the other half holds with this screen out of
    /// the way entirely.
    /// </summary>
    public sealed class FriendsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Friends; } }

        private const float CardWidth = 1180f;
        private const float RowHeight = 116f;

        private Text _headerSubtitle;
        private ScrollRect _scroll;
        private RectTransform _header;
        private RectTransform _content;

        private InputField _codeField;
        private UIButton _addButton;
        private Text _addNote;

        private RectTransform _listHost;
        private Text _emptyNote;

        private readonly List<FriendEntry> _entries = new List<FriendEntry>();
        private bool _loading;

        protected override void Build()
        {
            _header = ScreenChrome.Header(Root, "FRIENDS", Router, out _headerSubtitle);

            _content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                            new RectOffset(0, 0, 8, 48));
            ApplyContentInset();

            BuildAddCard(_content);

            _listHost = UIBuilder.Node("List", _content);
            UIBuilder.Sized(_listHost, CardWidth, 0f);
            UIBuilder.VStack(_listHost, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            ContentSizeFitter fitter = _listHost.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _emptyNote = UIBuilder.WrappedLabel("Empty", _content, "", UITheme.FontSmall,
                                                UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Sized(_emptyNote.rectTransform, CardWidth - 160f, 90f);
        }


        // ------------------------------------------------------------------ embedding

        /// <summary>
        /// Drops this page's own header when a panel is already drawing one, and lets the content
        /// sit against the sheet's edges instead of under a top bar that is not there. Nothing
        /// else about the screen changes - same fields, same services, same coroutines.
        /// </summary>
        public override void SetEmbedded(bool embedded)
        {
            base.SetEmbedded(embedded);
            if (_header != null) _header.gameObject.SetActive(!embedded);
            ApplyContentInset();
        }

        private void ApplyContentInset()
        {
            if (_scroll == null) return;
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding,
                                 Embedded ? UITheme.Space16 : UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 20f);
        }
        private void BuildAddCard(RectTransform parent)
        {
            RectTransform card = UIBuilder.Node("AddCard", parent);
            UIBuilder.Sized(card, CardWidth, 220f);
            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);

            Text heading = UIBuilder.Label("Heading", card, "ADD BY FRIEND CODE", UITheme.FontTiny,
                                           UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 30f, -22f, 700f, 26f, new Vector2(0f, 1f));

            // Seven, not six: the code is shown grouped as ABC-234 and a player who typed what
            // they read would otherwise lose the last character to the limit. Normalise drops the
            // dash on the way in, so what is sent is still six.
            _codeField = UIBuilder.TextField("Code", card, "ABC-234", UITheme.FontHeading, 7, false);
            UIBuilder.Box(_codeField.GetComponent<RectTransform>(), 30f, -56f, 440f, 84f, new Vector2(0f, 1f));

            _addButton = UIBuilder.GoldButton("Add", card, "SEND REQUEST", UITheme.FontBody);
            UIBuilder.Box(_addButton.Root, 490f, -56f, 360f, 84f, new Vector2(0f, 1f));
            _addButton.OnClick(OnAdd);

            _addNote = UIBuilder.WrappedLabel("Note", card,
                "Six characters, from the code on their Account screen. O, I, L, U, 0 and 1 are not "
                + "in the alphabet, so a code never contains one.",
                UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_addNote.rectTransform, 30f, -152f, CardWidth - 120f, 56f, new Vector2(0f, 1f));
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            Reload();
        }

        private void Reload()
        {
            CloudCoordinator cloud = App.Cloud;
            if (cloud == null || !cloud.SignedIn)
            {
                _entries.Clear();
                RenderList();
                return;
            }
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            _headerSubtitle.text = "LOADING";
            List<FriendEntry> fetched = null;
            string error = "";
            yield return App.Cloud.Social.ListFriends(delegate (List<FriendEntry> list, string message)
            {
                fetched = list;
                error = message;
            });
            _loading = false;

            _entries.Clear();
            if (fetched != null) _entries.AddRange(fetched);
            if (!string.IsNullOrEmpty(error)) App.Notify(error);
            RenderList();
        }

        /// <summary>
        /// The rows, in the order they want attention: requests waiting on this player, then
        /// friends, then requests waiting on somebody else.
        ///
        /// That order is the screen's only opinion. A request addressed to you is the only row
        /// here that is BLOCKING something - your friend cannot see your line until you answer -
        /// so it goes first however few of them there are; an outgoing request is the least
        /// actionable row on the screen and goes last.
        /// </summary>
        private void RenderList()
        {
            for (int i = _listHost.childCount - 1; i >= 0; i--) Destroy(_listHost.GetChild(i).gameObject);

            CloudCoordinator cloud = App.Cloud;
            if (cloud == null)
            {
                _headerSubtitle.text = "LOCAL ONLY";
                _emptyNote.text = "This build has no cloud configured, so there is nobody to add. "
                                + "Everything else in the game works as normal.";
                SetAddEnabled(false);
                return;
            }
            if (!cloud.SignedIn)
            {
                _headerSubtitle.text = "OFFLINE";
                _emptyNote.text = "You are offline, so your friends cannot be loaded. Open Account and "
                                + "tap Sync Now once you are back online.";
                SetAddEnabled(false);
                return;
            }

            SetAddEnabled(true);

            int incoming = 0, accepted = 0, outgoing = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                FriendEntry e = _entries[i];
                if (e.AwaitingMyAnswer) incoming++;
                else if (e.Status == "accepted") accepted++;
                else outgoing++;
            }

            _headerSubtitle.text = accepted + (accepted == 1 ? " FRIEND" : " FRIENDS")
                                 + (incoming > 0 ? "   -   " + incoming + " WAITING ON YOU" : "");

            if (incoming > 0) AddSectionLabel("WAITING ON YOU");
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].AwaitingMyAnswer) AddRow(_entries[i]);

            if (accepted > 0) AddSectionLabel("YOUR FRIENDS");
            for (int i = 0; i < _entries.Count; i++)
                if (!_entries[i].AwaitingMyAnswer && _entries[i].Status == "accepted") AddRow(_entries[i]);

            if (outgoing > 0) AddSectionLabel("WAITING ON THEM");
            for (int i = 0; i < _entries.Count; i++)
                if (!_entries[i].AwaitingMyAnswer && _entries[i].Status != "accepted") AddRow(_entries[i]);

            _emptyNote.text = _entries.Count == 0
                ? "Nobody yet. Swap friend codes with somebody and their line shows up here to fight."
                : "";
        }

        private void SetAddEnabled(bool enabled)
        {
            _addButton.Interactable = enabled;
            _codeField.interactable = enabled;
        }

        private void AddSectionLabel(string text)
        {
            Text label = UIBuilder.Label("Section", _listHost, text, UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.LowerLeft);
            UIBuilder.Sized(label.rectTransform, CardWidth, 40f);
        }

        /// <summary>
        /// One friendship. The name and code on the left, the state in the middle, the actions on
        /// the right - and the actions differ by row because what is possible differs by row.
        /// </summary>
        private void AddRow(FriendEntry entry)
        {
            RectTransform row = UIBuilder.Node("Row", _listHost);
            UIBuilder.Sized(row, CardWidth, RowHeight);
            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(16);
            bg.type = Image.Type.Sliced;
            bg.color = entry.AwaitingMyAnswer
                ? UITheme.WithAlpha(UITheme.PanelRaised, 0.98f) : UITheme.PanelSunken;
            UIBuilder.Frame("Edge", row,
                            entry.AwaitingMyAnswer ? UITheme.WithAlpha(UITheme.Gold, 0.55f) : UITheme.BorderSoft,
                            16, 2);

            string shown = string.IsNullOrEmpty(entry.DisplayName) ? "Wayfarer" : entry.DisplayName;
            Text name = UIBuilder.Label("Name", row, shown, UITheme.FontBody, UITheme.TextPrimary,
                                        TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 28f, -22f, 460f, 38f, new Vector2(0f, 1f));

            Text code = UIBuilder.Label("Code", row,
                                        string.IsNullOrEmpty(entry.FriendCode) ? "" : FriendCode.Format(entry.FriendCode),
                                        UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(code.rectTransform, 28f, -62f, 460f, 28f, new Vector2(0f, 1f));

            string state = entry.AwaitingMyAnswer ? "WANTS TO BE FRIENDS"
                         : entry.Status == "accepted" ? "FRIENDS" : "REQUEST SENT";
            Text status = UIBuilder.Label("State", row, state, UITheme.FontTiny,
                                          entry.AwaitingMyAnswer ? UITheme.Gold
                                          : entry.Status == "accepted" ? UITheme.Success : UITheme.TextMuted,
                                          TextAnchor.MiddleLeft);
            UIBuilder.Box(status.rectTransform, 520f, 0f, 300f, 34f, new Vector2(0f, 0.5f));

            string capturedId = entry.FriendshipId;
            string capturedUser = entry.UserId;
            string capturedName = shown;

            if (entry.AwaitingMyAnswer)
            {
                UIButton accept = UIBuilder.GoldButton("Accept", row, "ACCEPT", UITheme.FontSmall);
                UIBuilder.Box(accept.Root, -230f, 0f, 200f, 72f, new Vector2(1f, 0.5f));
                accept.OnClick(delegate { OnAccept(capturedId, capturedName); });

                // Decline is Remove. One row deleted covers cancelling, declining and unfriending,
                // which is why SocialService has no decline beside its Remove - three player
                // actions, one database effect, and naming them separately here would imply three
                // different outcomes.
                UIButton decline = UIBuilder.SecondaryButton("Decline", row, "DECLINE", UITheme.FontSmall);
                UIBuilder.Box(decline.Root, -24f, 0f, 190f, 72f, new Vector2(1f, 0.5f));
                decline.OnClick(delegate { OnRemove(capturedId, capturedName, "declined"); });
                return;
            }

            if (entry.Status == "accepted")
            {
                UIButton open = UIBuilder.GoldButton("Open", row, "THEIR TEAM", UITheme.FontSmall);
                UIBuilder.Box(open.Root, -230f, 0f, 220f, 72f, new Vector2(1f, 0.5f));
                open.OnClick(delegate { OnOpenTeam(capturedUser, capturedName); });
            }

            UIButton remove = UIBuilder.SecondaryButton("Remove", row,
                                                        entry.Status == "accepted" ? "REMOVE" : "CANCEL",
                                                        UITheme.FontSmall);
            UIBuilder.Box(remove.Root, -24f, 0f, 190f, 72f, new Vector2(1f, 0.5f));
            remove.OnClick(delegate
            {
                OnRemove(capturedId, capturedName, entry.Status == "accepted" ? "removed" : "cancelled");
            });
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// Looks the code up, then sends the request.
        ///
        /// Two round trips rather than one, on purpose. The lookup is what lets this say "nobody
        /// has the code ABC-234" and "that is your own code" - two different problems with two
        /// different fixes - where a single blind insert could only ever report that something
        /// went wrong. The database refuses the same cases anyway; this is about what the player
        /// is told, not about what is allowed.
        /// </summary>
        private void OnAdd()
        {
            if (App.Cloud == null || !App.Cloud.SignedIn) { App.Notify("You are offline."); return; }
            App.Audio.Tap();

            string typed = _codeField.text;
            StartCoroutine(AddRoutine(typed));
        }

        private IEnumerator AddRoutine(string typed)
        {
            SetAddEnabled(false);

            AccountProfile found = null;
            string error = "";
            yield return App.Cloud.Social.LookupByFriendCode(typed,
                delegate (AccountProfile profile, string message) { found = profile; error = message; });

            if (found == null)
            {
                SetAddEnabled(true);
                App.Notify(string.IsNullOrEmpty(error) ? "No player found." : error);
                yield break;
            }

            string sendError = "";
            yield return App.Cloud.Social.SendRequest(found.UserId,
                delegate (bool ok, string message) { sendError = ok ? "" : message; });

            SetAddEnabled(true);
            if (!string.IsNullOrEmpty(sendError)) { App.Notify(sendError); yield break; }

            _codeField.text = "";
            App.Notify("Request sent to "
                       + (string.IsNullOrEmpty(found.DisplayName) ? "them" : found.DisplayName) + ".");
            Reload();
        }

        private void OnAccept(string friendshipId, string name)
        {
            App.Audio.Tap();
            StartCoroutine(AcceptRoutine(friendshipId, name));
        }

        private IEnumerator AcceptRoutine(string friendshipId, string name)
        {
            string error = "";
            yield return App.Cloud.Social.Accept(friendshipId,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error) ? name + " is now a friend." : error);
            Reload();
        }

        private void OnRemove(string friendshipId, string name, string verb)
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm(verb == "removed" ? "REMOVE FRIEND?" : "ARE YOU SURE?",
                verb == "removed"
                    ? "You will no longer see " + name + "'s line, and they will no longer see yours."
                    : "The request will be " + verb + ".",
                "YES", "NO",
                delegate { StartCoroutine(RemoveRoutine(friendshipId, name, verb)); }, null);
        }

        private IEnumerator RemoveRoutine(string friendshipId, string name, string verb)
        {
            string error = "";
            yield return App.Cloud.Social.Remove(friendshipId,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error) ? name + " " + verb + "." : error);
            Reload();
        }

        private void OnOpenTeam(string userId, string name)
        {
            App.Audio.Tap();
            Router.FriendTeam.SetFriend(userId, name);
            Router.Show(ScreenId.FriendTeam);
        }
    }
}
