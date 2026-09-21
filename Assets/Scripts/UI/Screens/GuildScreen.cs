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
    /// The guild, in both of the states a player can be in about one.
    ///
    /// ONE SCREEN, TWO FACES, and that is deliberate rather than a saving. "Do you have a guild"
    /// is not a place a player navigates between - it is a fact about them that changes twice in
    /// a lifetime - so making it two screens would mean a router decision on every entry, a back
    /// stack that remembers a screen that no longer applies, and two empty states to keep in
    /// step. It is the same reasoning FriendsScreen records for keeping one list and splitting it
    /// by what it wants from you.
    ///
    /// EVERY PERMISSION HERE IS ASKED OF GuildRules, AND GuildRules AGREES WITH THE DATABASE BY
    /// HAND. A member does not see a Promote button, an officer does not see Disband, and nobody
    /// sees Leave while they are the leader - but none of that is what makes those things
    /// impossible. 0004's policies are, and `Tools/supabase-guild-rules-test.py` proves it with
    /// this screen out of the way entirely. What this decides is what to DRAW: an action that
    /// would be refused is not offered, because offering it and failing tells the player nothing
    /// about why.
    ///
    /// HANDING THE GUILD OVER IS A MODE, NOT A BUTTON ON A ROW. It is the one action here that
    /// cannot be undone by the person taking it, and a row already carrying Team, Promote and a
    /// remove cross has nowhere safe to put a fourth. So the leader arms it from the settings
    /// card, every row becomes MAKE LEADER until they pick or cancel, and the pick is confirmed.
    /// Two deliberate taps and a confirm, rather than one tap next to a cross.
    /// </summary>
    public sealed class GuildScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Guild; } }

        private const float CardWidth = 1180f;
        private const float RowHeight = 116f;

        private Text _headerSubtitle;
        private ScrollRect _scroll;
        private RectTransform _content;

        // ---------- the no-guild face ----------
        private RectTransform _noGuildHost;
        private InputField _nameField;
        private InputField _blurbField;
        private UIButton _createButton;
        private InputField _codeField;
        private UIButton _findButton;
        private Text _findNote;
        private UIButton _joinButton;
        private RectTransform _pendingHost;
        private GuildSummary _found;

        // ---------- the in-guild face ----------
        private RectTransform _guildHost;
        private Text _guildName;
        private Text _guildCode;
        private UIButton _copyButton;
        private Text _guildBlurb;
        private Text _guildCounts;
        private UIButton _requestsButton;
        private UIButton _chatButton;
        private UIButton _leaveButton;

        private RectTransform _settingsCard;
        private InputField _settingsBlurb;
        private UIButton _openToggle;
        private UIButton _saveSettings;
        private UIButton _handOverButton;
        private UIButton _disbandButton;
        private bool _openPending;

        private RectTransform _rosterHost;
        private Text _rosterNote;
        private Text _transferBanner;
        private UIButton _transferCancel;

        private readonly List<GuildMemberEntry> _roster = new List<GuildMemberEntry>();
        private int _pendingRequestCount;
        private bool _loading;

        /// <summary>Armed by HAND OVER and disarmed by picking somebody, cancelling, or leaving the
        /// screen. Never survives a hide - an armed dangerous mode a player walked away from is a
        /// trap the next visit springs.</summary>
        private bool _transferMode;

        // ================================================================== build

        protected override void Build()
        {
            ScreenChrome.Header(Root, "GUILD", Router, out _headerSubtitle);

            _content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                            new RectOffset(0, 0, 8, 48));
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 20f);

            BuildNoGuildFace(_content);
            BuildGuildFace(_content);

            // Both hidden until Render picks one. A screen that has not decided yet must show
            // neither face rather than both, which is what a half-built state used to look like.
            _noGuildHost.gameObject.SetActive(false);
            _guildHost.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ no guild

        private void BuildNoGuildFace(RectTransform parent)
        {
            _noGuildHost = UIBuilder.Node("NoGuild", parent);
            UIBuilder.Sized(_noGuildHost, CardWidth, 0f);
            UIBuilder.VStack(_noGuildHost, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            ContentSizeFitter fitter = _noGuildHost.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- create
            RectTransform create = Card(_noGuildHost, 320f);
            Heading(create, "START A GUILD");

            _nameField = UIBuilder.TextField("Name", create, "A name", UITheme.FontSmall,
                                             GuildRules.NameMax, false);
            UIBuilder.Box(_nameField.GetComponent<RectTransform>(), 30f, -56f, 520f, 76f, new Vector2(0f, 1f));

            _createButton = UIBuilder.GoldButton("Create", create, "CREATE", UITheme.FontBody);
            UIBuilder.Box(_createButton.Root, 570f, -56f, 300f, 76f, new Vector2(0f, 1f));
            _createButton.OnClick(OnCreate);

            _blurbField = UIBuilder.TextField("Blurb", create, "What it is for (optional)",
                                              UITheme.FontSmall, GuildRules.DescriptionMax, false);
            UIBuilder.Box(_blurbField.GetComponent<RectTransform>(), 30f, -144f, CardWidth - 120f, 76f,
                          new Vector2(0f, 1f));

            Text createNote = UIBuilder.WrappedLabel("Note", create,
                "You lead the guild you make. It gets a six-character code to give people, and you "
                + "choose whether anybody with the code walks straight in or waits to be let in.",
                UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(createNote.rectTransform, 30f, -232f, CardWidth - 120f, 62f, new Vector2(0f, 1f));

            // ---- join
            RectTransform join = Card(_noGuildHost, 300f);
            Heading(join, "JOIN BY CODE");

            _codeField = UIBuilder.TextField("Code", join, "ABC-234", UITheme.FontHeading, 7, false);
            UIBuilder.Box(_codeField.GetComponent<RectTransform>(), 30f, -56f, 440f, 84f, new Vector2(0f, 1f));

            _findButton = UIBuilder.SecondaryButton("Find", join, "FIND", UITheme.FontBody);
            UIBuilder.Box(_findButton.Root, 490f, -56f, 260f, 84f, new Vector2(0f, 1f));
            _findButton.OnClick(OnFind);

            _joinButton = UIBuilder.GoldButton("Join", join, "ASK TO JOIN", UITheme.FontBody);
            UIBuilder.Box(_joinButton.Root, 770f, -56f, 380f, 84f, new Vector2(0f, 1f));
            _joinButton.OnClick(OnJoin);

            _findNote = UIBuilder.WrappedLabel("FindNote", join,
                "Six characters, from the guild's own screen. The same alphabet as a friend code - "
                + "no O, I, L, U, 0 or 1.",
                UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_findNote.rectTransform, 30f, -154f, CardWidth - 120f, 84f, new Vector2(0f, 1f));

            // ---- what you have asked for
            _pendingHost = UIBuilder.Node("Pending", _noGuildHost);
            UIBuilder.Sized(_pendingHost, CardWidth, 0f);
            UIBuilder.VStack(_pendingHost, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            ContentSizeFitter pendingFit = _pendingHost.gameObject.AddComponent<ContentSizeFitter>();
            pendingFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ------------------------------------------------------------------ in a guild

        private void BuildGuildFace(RectTransform parent)
        {
            _guildHost = UIBuilder.Node("Guild", parent);
            UIBuilder.Sized(_guildHost, CardWidth, 0f);
            UIBuilder.VStack(_guildHost, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            ContentSizeFitter fitter = _guildHost.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- identity
            RectTransform card = Card(_guildHost, 268f);

            _guildName = UIBuilder.Label("Name", card, "", UITheme.FontTitle, UITheme.TextPrimary,
                                         TextAnchor.UpperLeft);
            UIBuilder.Box(_guildName.rectTransform, 30f, -24f, 720f, 54f, new Vector2(0f, 1f));

            Text codeLabel = UIBuilder.Label("CodeLabel", card, "GUILD CODE", UITheme.FontTiny,
                                             UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(codeLabel.rectTransform, 30f, -86f, 300f, 24f, new Vector2(0f, 1f));

            // Title size with the alphabet's own dash, for the same reason the friend code is:
            // this is a string one player reads out loud to another.
            _guildCode = UIBuilder.Label("Code", card, "------", UITheme.FontTitle, UITheme.Gold,
                                         TextAnchor.UpperLeft);
            UIBuilder.Box(_guildCode.rectTransform, 30f, -114f, 400f, 54f, new Vector2(0f, 1f));

            _copyButton = UIBuilder.SecondaryButton("Copy", card, "COPY", UITheme.FontSmall);
            UIBuilder.Box(_copyButton.Root, 440f, -110f, 200f, 62f, new Vector2(0f, 1f));
            _copyButton.OnClick(OnCopyCode);

            _guildBlurb = UIBuilder.WrappedLabel("Blurb", card, "", UITheme.FontTiny,
                                                 UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_guildBlurb.rectTransform, 30f, -182f, CardWidth - 120f, 60f, new Vector2(0f, 1f));

            _guildCounts = UIBuilder.Label("Counts", card, "", UITheme.FontSmall, UITheme.TextDim,
                                           TextAnchor.UpperRight);
            UIBuilder.Box(_guildCounts.rectTransform, -30f, -30f, 420f, 34f, new Vector2(1f, 1f));

            _requestsButton = UIBuilder.SecondaryButton("Requests", card, "REQUESTS", UITheme.FontSmall);
            UIBuilder.Box(_requestsButton.Root, -30f, -78f, 260f, 72f, new Vector2(1f, 1f));
            _requestsButton.OnClick(OnOpenRequests);

            _chatButton = UIBuilder.GoldButton("Chat", card, "CHAT", UITheme.FontSmall);
            UIBuilder.Box(_chatButton.Root, -300f, -78f, 240f, 72f, new Vector2(1f, 1f));
            _chatButton.OnClick(OnOpenChat);

            _leaveButton = UIBuilder.SecondaryButton("Leave", card, "LEAVE", UITheme.FontSmall);
            UIBuilder.Box(_leaveButton.Root, -30f, -162f, 260f, 66f, new Vector2(1f, 1f));
            _leaveButton.OnClick(OnLeave);

            // ---- roster, ABOVE the settings card and not below it.
            //
            // The first build put SETTINGS second and a capture showed what that costs: with the
            // identity card at 268 and the settings card at 292, a LEADER's roster started below
            // the fold and the screen opened on two cards of chrome. A member saw the roster and
            // a leader did not, which is backwards - the roster is what everybody opens this for,
            // and the settings are three buttons a leader touches twice in the guild's life. So
            // the rare card goes last, where scrolling to it is the right price.
            _transferBanner = UIBuilder.Label("TransferBanner", _guildHost, "", UITheme.FontSmall,
                                              UITheme.Warning, TextAnchor.MiddleLeft);
            UIBuilder.Sized(_transferBanner.rectTransform, CardWidth, 0f);

            _rosterHost = UIBuilder.Node("Roster", _guildHost);
            UIBuilder.Sized(_rosterHost, CardWidth, 0f);
            UIBuilder.VStack(_rosterHost, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            ContentSizeFitter rosterFit = _rosterHost.gameObject.AddComponent<ContentSizeFitter>();
            rosterFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _rosterNote = UIBuilder.WrappedLabel("RosterNote", _guildHost, "", UITheme.FontTiny,
                                                 UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Sized(_rosterNote.rectTransform, CardWidth - 160f, 60f);

            BuildSettingsCard(_guildHost);
        }

        private void BuildSettingsCard(RectTransform parent)
        {
            _settingsCard = Card(parent, 292f);
            Heading(_settingsCard, "SETTINGS");

            _settingsBlurb = UIBuilder.TextField("SettingsBlurb", _settingsCard, "What it is for",
                                                 UITheme.FontSmall, GuildRules.DescriptionMax, false);
            UIBuilder.Box(_settingsBlurb.GetComponent<RectTransform>(), 30f, -56f, 700f, 76f,
                          new Vector2(0f, 1f));

            _openToggle = UIBuilder.SecondaryButton("Open", _settingsCard, "", UITheme.FontSmall);
            UIBuilder.Box(_openToggle.Root, 750f, -56f, 400f, 76f, new Vector2(0f, 1f));
            _openToggle.OnClick(OnToggleOpen);

            _saveSettings = UIBuilder.GoldButton("Save", _settingsCard, "SAVE", UITheme.FontSmall);
            UIBuilder.Box(_saveSettings.Root, 30f, -144f, 300f, 72f, new Vector2(0f, 1f));
            _saveSettings.OnClick(OnSaveSettings);

            _handOverButton = UIBuilder.SecondaryButton("HandOver", _settingsCard, "HAND OVER",
                                                        UITheme.FontSmall);
            UIBuilder.Box(_handOverButton.Root, 350f, -144f, 340f, 72f, new Vector2(0f, 1f));
            _handOverButton.OnClick(OnArmTransfer);

            _disbandButton = UIBuilder.SecondaryButton("Disband", _settingsCard, "DISBAND",
                                                       UITheme.FontSmall);
            UIBuilder.Box(_disbandButton.Root, 710f, -144f, 300f, 72f, new Vector2(0f, 1f));
            _disbandButton.OnClick(OnDisband);

            Text note = UIBuilder.WrappedLabel("Note", _settingsCard,
                "Open lets anybody with the code walk straight in. Handing over makes somebody else "
                + "the leader and leaves you an officer. Disbanding ends the guild for everybody.",
                UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(note.rectTransform, 30f, -228f, CardWidth - 120f, 56f, new Vector2(0f, 1f));
        }

        // ================================================================== refresh

        public override void OnShow()
        {
            App.RefreshHud();
            _transferMode = false;
            _found = null;
            Reload();
        }

        /// <summary>
        /// Disarms the transfer mode, and FORGETS THAT A LOAD WAS RUNNING.
        ///
        /// The second half is not tidiness. Deactivating a screen's GameObject kills the
        /// coroutines running on it, so a player who opens the chat while the roster is still
        /// arriving leaves `_loading` true with nothing left to clear it - and the next OnShow
        /// sees it, returns early, and the screen never loads again for the rest of the session.
        /// It presented as a guild home with an empty roster and both faces drawn at once, which
        /// is what "Render never ran" looks like. Cleared here because here is where the
        /// coroutine actually died.
        /// </summary>
        public override void OnHide()
        {
            _transferMode = false;
            _loading = false;
        }

        private void Reload()
        {
            if (App.Cloud == null || !App.Cloud.SignedIn)
            {
                _roster.Clear();
                Render();
                return;
            }
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        /// <summary>
        /// Membership first, then whatever that turned out to mean.
        ///
        /// The order matters: the roster read is scoped to the guild the membership names, and the
        /// requests read is only offered to a role the membership names. Fetching them in parallel
        /// would mean guessing at both before either had answered.
        /// </summary>
        private IEnumerator LoadRoutine()
        {
            _headerSubtitle.text = "LOADING";
            GuildService guild = App.Cloud.Guild;

            string error = "";
            yield return guild.Refresh(delegate (string message) { error = message; });

            if (!string.IsNullOrEmpty(error))
            {
                _loading = false;
                App.Notify(error);
                Render();
                yield break;
            }

            _roster.Clear();
            _pendingRequestCount = 0;

            if (guild.Current != null)
            {
                List<GuildMemberEntry> members = null;
                yield return guild.ListRoster(delegate (List<GuildMemberEntry> list, string message)
                {
                    members = list;
                    error = message;
                });
                if (members != null) _roster.AddRange(members);

                if (GuildRules.CanAnswerRequests(guild.MyRole))
                {
                    List<GuildRequestEntry> requests = null;
                    yield return guild.ListRequests(delegate (List<GuildRequestEntry> list, string message)
                    {
                        requests = list;
                    });
                    if (requests != null) _pendingRequestCount = requests.Count;
                }

                yield return guild.Touch();
            }
            else
            {
                List<GuildSummary> mine = null;
                yield return guild.ListMyRequests(delegate (List<GuildSummary> list, string message)
                {
                    mine = list;
                });
                _myRequests.Clear();
                if (mine != null) _myRequests.AddRange(mine);
            }

            _loading = false;
            if (!string.IsNullOrEmpty(error)) App.Notify(error);
            Render();
        }

        private readonly List<GuildSummary> _myRequests = new List<GuildSummary>();

        // ================================================================== render

        private void Render()
        {
            CloudCoordinator cloud = App.Cloud;

            if (cloud == null)
            {
                ShowFace(false, false);
                _headerSubtitle.text = "LOCAL ONLY";
                _rosterNote.text = "";
                ShowOfflineNote("This build has no cloud configured, so there are no guilds to join. "
                                + "Everything else in the game works as normal.");
                return;
            }
            if (!cloud.SignedIn || !cloud.Guild.Known)
            {
                ShowFace(false, false);
                _headerSubtitle.text = "OFFLINE";
                ShowOfflineNote("You are offline, so your guild cannot be loaded. Open Account and tap "
                                + "Sync Now once you are back online.");
                return;
            }

            GuildService guild = cloud.Guild;
            if (guild.Current == null)
            {
                ShowFace(true, false);
                _headerSubtitle.text = "NO GUILD";
                RenderMyRequests();
                _joinButton.Interactable = _found != null;
                return;
            }

            ShowFace(false, true);
            RenderGuild(guild);
        }

        /// <summary>The offline and unconfigured states borrow the no-guild face's note rather
        /// than owning a third layout: both are "you cannot do anything here yet", and a screen
        /// with three empty states has two too many.</summary>
        private RectTransform _offlineHost;
        private Text _offlineNote;

        private void ShowOfflineNote(string text)
        {
            if (_offlineHost == null)
            {
                _offlineHost = UIBuilder.Node("Offline", _content);
                UIBuilder.Sized(_offlineHost, CardWidth, 200f);
                Image bg = _offlineHost.gameObject.AddComponent<Image>();
                bg.sprite = UISkin.Panel(18);
                bg.type = Image.Type.Sliced;
                bg.color = UITheme.Panel;
                UIBuilder.Frame("Edge", _offlineHost, UITheme.BorderSoft, 18, 2);

                _offlineNote = UIBuilder.WrappedLabel("Text", _offlineHost, "", UITheme.FontSmall,
                                                      UITheme.TextMuted, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(_offlineNote.rectTransform, 40f);
            }
            _offlineHost.gameObject.SetActive(true);
            _offlineNote.text = text;
        }

        private void ShowFace(bool noGuild, bool inGuild)
        {
            _noGuildHost.gameObject.SetActive(noGuild);
            _guildHost.gameObject.SetActive(inGuild);
            if (_offlineHost != null) _offlineHost.gameObject.SetActive(false);
        }

        private void RenderMyRequests()
        {
            for (int i = _pendingHost.childCount - 1; i >= 0; i--)
                Destroy(_pendingHost.GetChild(i).gameObject);

            if (_myRequests.Count == 0) return;

            Text label = UIBuilder.Label("Section", _pendingHost, "WAITING ON THEM", UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.LowerLeft);
            UIBuilder.Sized(label.rectTransform, CardWidth, 40f);

            for (int i = 0; i < _myRequests.Count; i++)
            {
                GuildSummary pending = _myRequests[i];
                RectTransform row = UIBuilder.Node("Pending" + i, _pendingHost);
                UIBuilder.Sized(row, CardWidth, RowHeight);
                Image bg = row.gameObject.AddComponent<Image>();
                bg.sprite = UISkin.Panel(16);
                bg.type = Image.Type.Sliced;
                bg.color = UITheme.PanelSunken;
                UIBuilder.Frame("Edge", row, UITheme.BorderSoft, 16, 2);

                Text name = UIBuilder.Label("Name", row, pending.Name, UITheme.FontBody,
                                            UITheme.TextPrimary, TextAnchor.UpperLeft);
                UIBuilder.Box(name.rectTransform, 28f, -22f, 560f, 38f, new Vector2(0f, 1f));

                Text code = UIBuilder.Label("Code", row, GuildRules.FormatCode(pending.Code),
                                            UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
                UIBuilder.Box(code.rectTransform, 28f, -62f, 560f, 28f, new Vector2(0f, 1f));

                Text state = UIBuilder.Label("State", row, "ASKED TO JOIN", UITheme.FontTiny,
                                             UITheme.TextMuted, TextAnchor.MiddleLeft);
                UIBuilder.Box(state.rectTransform, 620f, 0f, 320f, 34f, new Vector2(0f, 0.5f));

                string requestId = pending.PendingRequestId;
                string guildName = pending.Name;
                UIButton withdraw = UIBuilder.SecondaryButton("Withdraw", row, "WITHDRAW",
                                                              UITheme.FontSmall);
                UIBuilder.Box(withdraw.Root, -24f, 0f, 240f, 72f, new Vector2(1f, 0.5f));
                withdraw.OnClick(delegate { OnWithdraw(requestId, guildName); });
            }
        }

        private void RenderGuild(GuildService guild)
        {
            GuildSummary current = guild.Current;
            GuildRole mine = guild.MyRole;

            _headerSubtitle.text = GuildRules.RoleLabel(mine) + "   -   "
                                 + _roster.Count + " / " + GuildRules.MemberCap;
            _guildName.text = current.Name.ToUpperInvariant();
            _guildCode.text = GuildRules.FormatCode(current.Code);
            _guildBlurb.text = string.IsNullOrEmpty(current.Description)
                ? "No description." : current.Description;
            _guildCounts.text = (current.IsOpen ? "OPEN" : "BY REQUEST") + "   -   "
                              + _roster.Count + " / " + GuildRules.MemberCap;

            bool canAnswer = GuildRules.CanAnswerRequests(mine);
            _requestsButton.Root.gameObject.SetActive(canAnswer);
            if (canAnswer)
            {
                _requestsButton.SetText(_pendingRequestCount > 0
                    ? "REQUESTS  " + _pendingRequestCount : "REQUESTS");
            }

            _leaveButton.Root.gameObject.SetActive(GuildRules.CanLeave(mine));
            _settingsCard.gameObject.SetActive(GuildRules.CanEditSettings(mine));
            if (GuildRules.CanEditSettings(mine))
            {
                if (!_settingsBlurb.isFocused) _settingsBlurb.text = current.Description;
                _openPending = current.IsOpen;
                _openToggle.SetText(_openPending ? "OPEN TO ANYONE" : "BY REQUEST ONLY");
                _handOverButton.Interactable = _roster.Count > 1;
            }

            _transferBanner.text = _transferMode
                ? "PICK WHO LEADS - every line below becomes MAKE LEADER until you choose or cancel."
                : "";
            UIBuilder.Sized(_transferBanner.rectTransform, CardWidth, _transferMode ? 44f : 0f);

            if (_transferCancel == null)
            {
                _transferCancel = UIBuilder.SecondaryButton("TransferCancel", _transferBanner.rectTransform,
                                                            "CANCEL", UITheme.FontSmall);
                UIBuilder.Box(_transferCancel.Root, 0f, 0f, 200f, 40f, new Vector2(1f, 0.5f));
                _transferCancel.OnClick(OnCancelTransfer);
            }
            _transferCancel.Root.gameObject.SetActive(_transferMode);

            RenderRoster(mine);
        }

        private void RenderRoster(GuildRole mine)
        {
            for (int i = _rosterHost.childCount - 1; i >= 0; i--)
                Destroy(_rosterHost.GetChild(i).gameObject);

            long now = TimeUtil.NowUnix();
            for (int i = 0; i < _roster.Count; i++) AddMemberRow(_roster[i], mine, now);

            // Says THEIR TEAM because that is what the button says. The first draft read "tap a
            // guildmate's line", which is not what opens anything here and is the kind of
            // instruction a player follows once, fails at, and stops trusting.
            _rosterNote.text = _roster.Count <= 1
                ? "Nobody else yet. Give somebody the code above and they can ask to join."
                : "THEIR TEAM shows the five heroes a guildmate fields. A duel there is friendly - "
                  + "nothing is at stake, and they are not told.";
        }

        /// <summary>
        /// One member. Name and role on the left, what they field in the middle, what you may do
        /// about them on the right - and the right-hand side is different per row because what is
        /// possible is different per row.
        /// </summary>
        private void AddMemberRow(GuildMemberEntry entry, GuildRole mine, long now)
        {
            RectTransform row = UIBuilder.Node("Member", _rosterHost);
            UIBuilder.Sized(row, CardWidth, RowHeight);
            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(16);
            bg.type = Image.Type.Sliced;
            bg.color = entry.IsSelf ? UITheme.WithAlpha(UITheme.PanelRaised, 0.98f) : UITheme.PanelSunken;
            UIBuilder.Frame("Edge", row,
                            entry.IsSelf ? UITheme.WithAlpha(UITheme.Gold, 0.45f) : UITheme.BorderSoft,
                            16, 2);

            string shown = string.IsNullOrEmpty(entry.DisplayName) ? "Wayfarer" : entry.DisplayName;
            Text name = UIBuilder.Label("Name", row, shown + (entry.IsSelf ? "   (YOU)" : ""),
                                        UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 28f, -20f, 420f, 38f, new Vector2(0f, 1f));

            RectTransform chip = UIBuilder.Chip("Role", row, GuildRules.RoleLabel(entry.Role),
                                                RoleTint(entry.Role), UITheme.FontTiny - 4);
            UIBuilder.Box(chip, 28f, -64f, 150f, 32f, new Vector2(0f, 1f));

            Text seen = UIBuilder.Label("Seen", row, GuildRules.LastSeenLabel(entry.LastSeenUnix, now),
                                        UITheme.FontTiny - 2,
                                        now - entry.LastSeenUnix < 300 ? UITheme.Success : UITheme.TextDim,
                                        TextAnchor.UpperLeft);
            UIBuilder.Box(seen.rectTransform, 190f, -64f, 200f, 30f, new Vector2(0f, 1f));

            // -1 is "no line published", which is a different sentence from a line worth nothing.
            Text power = UIBuilder.Label("Power", row,
                                         entry.PowerScore < 0 ? "NO LINE" : "POWER  " + entry.PowerScore,
                                         UITheme.FontSmall,
                                         entry.PowerScore < 0 ? UITheme.TextDim : UITheme.Gold,
                                         TextAnchor.MiddleLeft);
            UIBuilder.Box(power.rectTransform, 470f, 0f, 300f, 34f, new Vector2(0f, 0.5f));

            string capturedId = entry.UserId;
            string capturedName = shown;
            GuildRole capturedRole = entry.Role;

            if (_transferMode)
            {
                if (GuildRules.CanMakeLeader(mine, entry.Role, entry.IsSelf))
                {
                    UIButton make = UIBuilder.GoldButton("MakeLeader", row, "MAKE LEADER",
                                                         UITheme.FontSmall);
                    UIBuilder.Box(make.Root, -24f, 0f, 320f, 72f, new Vector2(1f, 0.5f));
                    make.OnClick(delegate { OnMakeLeader(capturedId, capturedName); });
                }
                return;
            }

            float right = -24f;
            if (GuildRules.CanRemove(mine, entry.Role, entry.IsSelf))
            {
                UIButton remove = UIBuilder.IconButton("Remove", row, "x", 62f,
                                                       UITheme.WithAlpha(UITheme.Danger, 0.28f),
                                                       UITheme.TextPrimary);
                UIBuilder.Box(remove.Root, right, 0f, 62f, 62f, new Vector2(1f, 0.5f));
                remove.OnClick(delegate { OnRemove(capturedId, capturedName, capturedRole); });
                right -= 78f;
            }

            if (GuildRules.CanPromote(mine, entry.Role, entry.IsSelf))
            {
                UIButton promote = UIBuilder.SecondaryButton("Promote", row, "PROMOTE", UITheme.FontSmall);
                UIBuilder.Box(promote.Root, right, 0f, 220f, 72f, new Vector2(1f, 0.5f));
                promote.OnClick(delegate { OnSetRole(capturedId, capturedName, GuildRole.Officer); });
                right -= 236f;
            }
            else if (GuildRules.CanDemote(mine, entry.Role, entry.IsSelf))
            {
                UIButton demote = UIBuilder.SecondaryButton("Demote", row, "DEMOTE", UITheme.FontSmall);
                UIBuilder.Box(demote.Root, right, 0f, 220f, 72f, new Vector2(1f, 0.5f));
                demote.OnClick(delegate { OnSetRole(capturedId, capturedName, GuildRole.Member); });
                right -= 236f;
            }

            if (!entry.IsSelf)
            {
                UIButton team = UIBuilder.GoldButton("Team", row, "THEIR TEAM", UITheme.FontSmall);
                UIBuilder.Box(team.Root, right, 0f, 250f, 72f, new Vector2(1f, 0.5f));
                team.OnClick(delegate { OnOpenTeam(capturedId, capturedName); });
            }
        }

        private static Color RoleTint(GuildRole role)
        {
            if (role == GuildRole.Leader) return UITheme.Gold;
            if (role == GuildRole.Officer) return UITheme.Shard;
            return UITheme.TextMuted;
        }

        // ================================================================== actions

        private void OnCreate()
        {
            if (App.Cloud == null || !App.Cloud.SignedIn) { App.Notify("You are offline."); return; }
            App.Audio.Tap();
            StartCoroutine(CreateRoutine(_nameField.text, _blurbField.text));
        }

        private IEnumerator CreateRoutine(string name, string blurb)
        {
            _createButton.Interactable = false;
            GuildSummary made = null;
            string error = "";
            yield return App.Cloud.Guild.Create(name, blurb,
                delegate (GuildSummary guild, string message) { made = guild; error = message; });
            _createButton.Interactable = true;

            if (made == null) { App.Notify(string.IsNullOrEmpty(error) ? "That did not work." : error); yield break; }
            _nameField.text = "";
            _blurbField.text = "";
            App.Notify(made.Name + " founded. Your code is " + GuildRules.FormatCode(made.Code) + ".");
            Reload();
        }

        private void OnFind()
        {
            if (App.Cloud == null || !App.Cloud.SignedIn) { App.Notify("You are offline."); return; }
            App.Audio.Tap();
            StartCoroutine(FindRoutine(_codeField.text));
        }

        /// <summary>
        /// Look first, join second, and the two taps are the point rather than a round trip that
        /// could have been saved. An open guild admits on arrival, so "ask to join" is sometimes
        /// "walk in" - and walking into a guild whose name you never saw is not a decision.
        /// </summary>
        private IEnumerator FindRoutine(string typed)
        {
            _findButton.Interactable = false;
            GuildSummary found = null;
            string error = "";
            yield return App.Cloud.Guild.LookupByCode(typed,
                delegate (GuildSummary guild, string message) { found = guild; error = message; });
            _findButton.Interactable = true;

            _found = found;
            if (found == null)
            {
                _findNote.text = string.IsNullOrEmpty(error) ? "No guild has that code." : error;
                _joinButton.Interactable = false;
                yield break;
            }

            bool full = found.MemberCount >= GuildRules.MemberCap;
            _findNote.text = found.Name + "  -  " + found.MemberCount + " / " + GuildRules.MemberCap
                           + (found.IsOpen ? "  -  anybody with the code can walk in"
                                           : "  -  a leader or an officer has to let you in")
                           + (string.IsNullOrEmpty(found.Description) ? "" : "\n" + found.Description);
            _joinButton.SetText(found.IsOpen ? "JOIN" : "ASK TO JOIN");
            _joinButton.Interactable = !full;
            if (full) _findNote.text += "\nThat guild is full.";
        }

        private void OnJoin()
        {
            if (_found == null) return;
            App.Audio.Tap();
            StartCoroutine(JoinRoutine(_found.Code, _found.Name));
        }

        private IEnumerator JoinRoutine(string code, string name)
        {
            _joinButton.Interactable = false;
            string outcome = "";
            string error = "";
            yield return App.Cloud.Guild.RequestJoin(code,
                delegate (string result, string message) { outcome = result; error = message; });
            _joinButton.Interactable = true;

            if (!string.IsNullOrEmpty(error)) { App.Notify(error); yield break; }
            _codeField.text = "";
            _found = null;
            App.Notify(outcome == "joined" ? "You are in " + name + "." : "Asked to join " + name + ".");
            Reload();
        }

        private void OnWithdraw(string requestId, string guildName)
        {
            App.Audio.Tap();
            StartCoroutine(SimpleRoutine(App.Cloud.Guild.RemoveRequest(requestId, null),
                                         "Withdrew your request to " + guildName + "."));
        }

        private void OnCopyCode()
        {
            GuildService guild = App.Cloud != null ? App.Cloud.Guild : null;
            if (guild == null || guild.Current == null) return;
            App.Audio.Tap();
            // Unformatted, like the friend code: the dash is a reading aid and Normalise strips it
            // anyway, so what is copied is what gets pasted into somebody's Join box.
            GUIUtility.systemCopyBuffer = guild.Current.Code;
            App.Notify("Guild code copied.");
        }

        private void OnToggleOpen()
        {
            App.Audio.Tap();
            _openPending = !_openPending;
            _openToggle.SetText(_openPending ? "OPEN TO ANYONE" : "BY REQUEST ONLY");
        }

        private void OnSaveSettings()
        {
            App.Audio.Tap();
            StartCoroutine(SaveSettingsRoutine(_settingsBlurb.text, _openPending));
        }

        private IEnumerator SaveSettingsRoutine(string blurb, bool open)
        {
            _saveSettings.Interactable = false;
            string error = "";
            yield return App.Cloud.Guild.SaveSettings(blurb, open,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            _saveSettings.Interactable = true;
            App.Notify(string.IsNullOrEmpty(error) ? "Saved." : error);
            Reload();
        }

        /// <summary>
        /// Arms the hand-over mode, and scrolls back to the roster.
        ///
        /// The scroll is not a flourish. HAND OVER lives in the settings card at the bottom of the
        /// screen and the banner it raises sits above the roster, so arming it without moving the
        /// view leaves a player looking at the button they just pressed and no sign anything
        /// happened. This is the one action here whose feedback is entirely somewhere else.
        /// </summary>
        private void OnArmTransfer()
        {
            App.Audio.Tap();
            _transferMode = true;
            Render();
            if (_scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void OnCancelTransfer()
        {
            App.Audio.Tap();
            _transferMode = false;
            Render();
        }

        private void OnMakeLeader(string userId, string name)
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm("HAND THE GUILD OVER?",
                name + " becomes the leader and you become an officer. You cannot take it back "
                + "yourself - only they can hand it on.",
                "HAND OVER", "KEEP IT",
                delegate { StartCoroutine(TransferRoutine(userId, name)); },
                delegate { _transferMode = false; Render(); });
        }

        private IEnumerator TransferRoutine(string userId, string name)
        {
            _transferMode = false;
            string error = "";
            yield return App.Cloud.Guild.TransferLeadership(userId,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error) ? name + " now leads the guild." : error);
            Reload();
        }

        private void OnSetRole(string userId, string name, GuildRole role)
        {
            App.Audio.Tap();
            StartCoroutine(SetRoleRoutine(userId, name, role));
        }

        private IEnumerator SetRoleRoutine(string userId, string name, GuildRole role)
        {
            string error = "";
            yield return App.Cloud.Guild.SetRole(userId, role,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error)
                ? name + " is now " + (role == GuildRole.Officer ? "an officer" : "a member") + "."
                : error);
            Reload();
        }

        private void OnRemove(string userId, string name, GuildRole role)
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm("REMOVE " + name.ToUpperInvariant() + "?",
                "They leave the guild and lose the chat. They can ask to join again.",
                "REMOVE", "KEEP",
                delegate { StartCoroutine(RemoveRoutine(userId, name, role)); }, null);
        }

        private IEnumerator RemoveRoutine(string userId, string name, GuildRole role)
        {
            string error = "";
            yield return App.Cloud.Guild.RemoveMember(userId, role,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            App.Notify(string.IsNullOrEmpty(error) ? name + " removed." : error);
            Reload();
        }

        private void OnLeave()
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm("LEAVE THE GUILD?",
                "You lose the roster and the chat. You can ask to join again, or join another one.",
                "LEAVE", "STAY",
                delegate { StartCoroutine(SimpleRoutine(App.Cloud.Guild.Leave(null), "You left the guild.")); },
                null);
        }

        private void OnDisband()
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm("DISBAND THE GUILD?",
                "This ends the guild for everybody in it. The roster, the requests and the whole "
                + "chat go with it, and none of it can be brought back.",
                "DISBAND", "KEEP IT",
                delegate { StartCoroutine(SimpleRoutine(App.Cloud.Guild.Disband(null), "Guild disbanded.")); },
                null);
        }

        /// <summary>
        /// The shape every one-shot guild action shares: run it, say one thing, reload.
        ///
        /// It takes the coroutine already built with a null callback, which means the SERVICE's
        /// own refusal message is lost and the screen says its own sentence instead. That is fine
        /// for these four and only these four: leaving, disbanding and withdrawing are refused by
        /// the client before they are sent - GuildRules answers the same question the policy does
        /// - so a failure here is a network failure, which the notify already covers.
        /// </summary>
        private IEnumerator SimpleRoutine(IEnumerator action, string saidOnSuccess)
        {
            yield return action;
            App.Notify(saidOnSuccess);
            Reload();
        }

        private void OnOpenRequests()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.GuildRequests);
        }

        private void OnOpenChat()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.GuildChat);
        }

        /// <summary>
        /// A guildmate's line, on the screen a friend's line already uses.
        ///
        /// REUSED RATHER THAN COPIED, and the reason is in the database rather than in the UI: the
        /// screen fetches a `team_snapshots` row and hands it to the battle screen, and 0006 made
        /// a guildmate's row readable by adding a policy beside the friendship one. So the screen
        /// needs no idea which of the two bought the read - it asks for a line and either gets one
        /// or does not, exactly as before. Guildmates may duel without being friends because of
        /// that policy, not because of anything here.
        /// </summary>
        private void OnOpenTeam(string userId, string name)
        {
            App.Audio.Tap();
            Router.FriendTeam.SetFriend(userId, name);
            Router.Show(ScreenId.FriendTeam);
        }

        // ================================================================== shared

        private RectTransform Card(RectTransform parent, float height)
        {
            RectTransform card = UIBuilder.Node("Card", parent);
            UIBuilder.Sized(card, CardWidth, height);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);
            return card;
        }

        private Text Heading(RectTransform card, string text)
        {
            Text label = UIBuilder.Label("Heading", card, text, UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 30f, -22f, 700f, 26f, new Vector2(0f, 1f));
            return label;
        }
    }
}
