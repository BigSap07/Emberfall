using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The account, in the only terms a player has one: a name other people see, a code they can
    /// be found by, whether their progress is safe anywhere but this phone, and how to move it.
    ///
    /// EVERY SENTENCE ON THIS SCREEN IS ABOUT RECOVERY, and that is deliberate. The account exists
    /// so a save has somewhere to go; the player never asked for one and never saw a login wall.
    /// So this screen's job is not to explain accounts - it is to answer the one question that
    /// matters, "if I lose this phone, do I lose my game", and to make the answer changeable. That
    /// is why the link-email card is the loudest thing here while the account is anonymous, and
    /// why it goes quiet the moment it is not.
    ///
    /// SIGN OUT IS GATED, AND THE GATE IS NOT HERE. AccountService.SignOut refuses outright while
    /// the account is anonymous, because an anonymous account is reachable only through the
    /// refresh token on this device and discarding it destroys the player's access to their own
    /// save with nothing that can recover it. This screen dims the button and says why; the
    /// service is what makes it true. A UI-only guard on a data-destroying action is a guard that
    /// the next screen to call the same method does not have.
    /// </summary>
    public sealed class AccountScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Account; } }

        private const int NameLimit = DisplayNameRules.Max;
        private const float CardWidth = 1180f;

        private Text _headerSubtitle;
        private ScrollRect _scroll;
        private RectTransform _header;

        private Text _statusValue;
        private Text _statusNote;
        private UIButton _syncButton;

        private InputField _nameField;
        private UIButton _nameButton;

        private Text _friendCode;
        private UIButton _copyButton;

        private RectTransform _linkCard;
        private Text _linkHeading;
        private Text _linkWarning;
        private InputField _emailField;
        private InputField _passwordField;
        private UIButton _linkButton;

        private InputField _signInEmail;
        private InputField _signInPassword;
        private UIButton _signInButton;

        private UIButton _signOutButton;
        private Text _signOutNote;

        private Image _ground;
        private float _tick;

        protected override void Build()
        {
            _ground = EmberforgeUI.Ground(Root);
            _header = ScreenChrome.ForgedHeader(Root, "ACCOUNT", Router, out _headerSubtitle);

            RectTransform content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                                         new RectOffset(0, 0, 8, 48));
            ApplyContentInset();

            BuildStatusCard(content);
            BuildIdentityCard(content);
            BuildLinkCard(content);
            BuildOtherDeviceCard(content);
            BuildSignOutCard(content);
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
            // The ground goes with the header: inside a panel sheet this page already sits on the
            // sheet's framed surface - see EmberforgeUI.Ground.
            if (_ground != null) _ground.enabled = !embedded;
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
        // ------------------------------------------------------------------ status

        private void BuildStatusCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 190f);
            Heading(card, "STATUS");

            _statusValue = EmberforgeUI.Title("Value", card, "", UITheme.FontHeading, Emberforge.TextBody,
                                           TextAnchor.UpperLeft);
            UIBuilder.Box(_statusValue.rectTransform, 30f, -56f, 760f, 44f, new Vector2(0f, 1f));

            _statusNote = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny, Emberforge.TextBody,
                                                 TextAnchor.UpperLeft);
            UIBuilder.Box(_statusNote.rectTransform, 30f, -104f, 760f, 64f, new Vector2(0f, 1f));

            _syncButton = EmberforgeUI.Button("Sync", card, "SYNC NOW", UITheme.FontBody,
                                              ForgeButtonKind.Primary);
            UIBuilder.Box(_syncButton.Root, -30f, 0f, 300f, 92f, new Vector2(1f, 0.5f));
            _syncButton.OnClick(OnSyncNow);
        }

        // ------------------------------------------------------------------ name and code

        private void BuildIdentityCard(RectTransform parent)
        {
            // 296, measured rather than guessed, and it was 250: heading 22-48, name field 56-132,
            // hint 140-166, code label 176-202, the code itself 206-256 and the COPY button
            // 204-266 - so the last two rows hung 16 units past the bottom edge and sat on the
            // card below them. Verified in a capture, which is the only thing that can see it.
            RectTransform card = Card(parent, 296f);
            Heading(card, "WHO YOU ARE");

            _nameField = EmberforgeUI.TextField("Name", card, "Your name", UITheme.FontSmall, NameLimit, false);
            UIBuilder.Box(_nameField.GetComponent<RectTransform>(), 30f, -56f, 620f, 76f, new Vector2(0f, 1f));

            _nameButton = EmberforgeUI.Button("SaveName", card, "SAVE NAME", UITheme.FontSmall,
                                              ForgeButtonKind.Secondary);
            UIBuilder.Box(_nameButton.Root, 670f, -56f, 260f, 76f, new Vector2(0f, 1f));
            _nameButton.OnClick(OnSaveName);

            Text nameHint = EmberforgeUI.Label("NameHint", card,
                "The name a friend sees on your line. Separate from the one on your top bar.",
                UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(nameHint.rectTransform, 30f, -140f, 900f, 26f, new Vector2(0f, 1f));

            Text codeLabel = EmberforgeUI.Label("CodeLabel", card, "FRIEND CODE", UITheme.FontTiny,
                                             Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(codeLabel.rectTransform, 30f, -176f, 400f, 26f, new Vector2(0f, 1f));

            // Big, monospaced by spacing rather than by font - this is a string a player reads out
            // loud to somebody, so it is set at title size with the dash the alphabet's own
            // formatter puts in.
            _friendCode = EmberforgeUI.Title("Code", card, "------", UITheme.FontTitle, Emberforge.TextGold,
                                          TextAnchor.UpperLeft);
            UIBuilder.Box(_friendCode.rectTransform, 30f, -206f, 500f, 54f, new Vector2(0f, 1f));

            _copyButton = EmberforgeUI.Button("Copy", card, "COPY", UITheme.FontSmall,
                                              ForgeButtonKind.Secondary);
            UIBuilder.Box(_copyButton.Root, 540f, -202f, 220f, 62f, new Vector2(0f, 1f));
            _copyButton.OnClick(OnCopyCode);
        }

        // ------------------------------------------------------------------ linking an email

        /// <summary>
        /// The card that decides whether this player can ever get their game back.
        ///
        /// Its warning is not boilerplate and is not hidden behind a tooltip: until an email is
        /// attached, the account lives entirely in one token on one device, and a lost or wiped
        /// phone takes the save with it. The player was never asked to make an account, so they
        /// have no reason to assume any of that - saying it plainly is the whole point of the card.
        /// </summary>
        private void BuildLinkCard(RectTransform parent)
        {
            _linkCard = Card(parent, 372f);
            _linkHeading = Heading(_linkCard, "PROTECT THIS ACCOUNT");

            // 108, not 68, and the four lines below it move down to match. At FontSmall over 1060
            // units the anonymous warning is three lines, and a 68-tall box clipped the last one -
            // which in this case was "Link an email and it becomes recoverable from anywhere", the
            // sentence the whole card exists to deliver. A capture caught it; the maths would not
            // have, because how many lines a wrapped string takes is a font metric.
            _linkWarning = EmberforgeUI.Body("Warning", _linkCard, "", UITheme.FontSmall,
                                                  UITheme.Warning, TextAnchor.UpperLeft);
            UIBuilder.Box(_linkWarning.rectTransform, 30f, -56f, CardWidth - 120f, 108f, new Vector2(0f, 1f));

            _emailField = EmberforgeUI.TextField("Email", _linkCard, "your@email", UITheme.FontSmall, 96, false);
            UIBuilder.Box(_emailField.GetComponent<RectTransform>(), 30f, -174f, 520f, 76f, new Vector2(0f, 1f));

            _passwordField = EmberforgeUI.TextField("Password", _linkCard, "a password", UITheme.FontSmall, 72, true);
            UIBuilder.Box(_passwordField.GetComponent<RectTransform>(), 570f, -174f, 380f, 76f, new Vector2(0f, 1f));

            _linkButton = EmberforgeUI.Button("Link", _linkCard, "LINK EMAIL", UITheme.FontBody,
                                              ForgeButtonKind.Primary);
            UIBuilder.Box(_linkButton.Root, 30f, -266f, 460f, 84f, new Vector2(0f, 1f));
            _linkButton.OnClick(OnLinkEmail);
        }

        // ------------------------------------------------------------------ another device

        private void BuildOtherDeviceCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 300f);
            Heading(card, "SIGN IN ON THIS DEVICE");

            Text note = EmberforgeUI.Body("Note", card,
                "If you linked an email on another phone, sign in with it here to bring that account "
                + "to this device. You will be asked which save to keep before anything is replaced.",
                UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(note.rectTransform, 30f, -56f, CardWidth - 120f, 60f, new Vector2(0f, 1f));

            _signInEmail = EmberforgeUI.TextField("SignInEmail", card, "your@email", UITheme.FontSmall, 96, false);
            UIBuilder.Box(_signInEmail.GetComponent<RectTransform>(), 30f, -124f, 520f, 76f, new Vector2(0f, 1f));

            _signInPassword = EmberforgeUI.TextField("SignInPassword", card, "your password", UITheme.FontSmall, 72, true);
            UIBuilder.Box(_signInPassword.GetComponent<RectTransform>(), 570f, -124f, 380f, 76f, new Vector2(0f, 1f));

            _signInButton = EmberforgeUI.Button("SignIn", card, "SIGN IN", UITheme.FontBody,
                                               ForgeButtonKind.Secondary);
            UIBuilder.Box(_signInButton.Root, 30f, -216f, 460f, 84f, new Vector2(0f, 1f));
            _signInButton.OnClick(OnSignInWithEmail);
        }

        // ------------------------------------------------------------------ signing out

        private void BuildSignOutCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 190f);
            Heading(card, "SIGN OUT");

            _signOutNote = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny,
                                                  Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_signOutNote.rectTransform, 30f, -56f, 760f, 68f, new Vector2(0f, 1f));

            _signOutButton = EmberforgeUI.Button("SignOut", card, "SIGN OUT", UITheme.FontBody,
                                                ForgeButtonKind.Secondary);
            UIBuilder.Box(_signOutButton.Root, -30f, 0f, 300f, 84f, new Vector2(1f, 0.5f));
            _signOutButton.OnClick(OnSignOut);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            CloudCoordinator cloud = App.Cloud;
            if (cloud != null)
            {
                cloud.Changed -= Refresh;
                cloud.Changed += Refresh;
                if (cloud.SignedIn && cloud.Profile == null) StartCoroutine(cloud.RefreshProfile(null));
            }
            _tick = 0f;
            Refresh();
        }

        public override void OnHide()
        {
            if (App != null && App.Cloud != null) App.Cloud.Changed -= Refresh;
        }

        /// <summary>The "last synced" line ages while the screen is open, so it is re-rendered on a
        /// slow tick. Nothing else here changes without an event.</summary>
        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 5f) return;
            _tick = 0f;
            RefreshStatusLine();
        }

        private void Refresh()
        {
            CloudCoordinator cloud = App.Cloud;

            if (cloud == null)
            {
                // No cloud at all: a build with no config. Said plainly rather than shown as a
                // failure, because it is the ordinary state of a fresh clone and of every build
                // nobody has pointed at a project.
                _headerSubtitle.text = "LOCAL ONLY";
                _statusValue.text = "NO ACCOUNT SERVICE";
                _statusValue.color = Emberforge.TextQuiet;
                _statusNote.text = "This version cannot connect to an online account. Your progress is saved on this "
                                 + "device and nowhere else, and everything in the game works as normal.";
                SetAll(false);
                _friendCode.text = "------";
                _linkWarning.text = "";
                _signOutNote.text = "";
                return;
            }

            _headerSubtitle.text = cloud.SignedIn
                ? (cloud.State.IsAnonymous ? "NOT PROTECTED" : cloud.State.Email.ToUpperInvariant())
                : "OFFLINE";

            RefreshStatusLine();

            bool signedIn = cloud.SignedIn;
            bool busy = cloud.Busy;
            bool anonymous = cloud.State.IsAnonymous;

            _syncButton.Interactable = cloud.Configured && !busy;
            _nameButton.Interactable = signedIn && !busy;
            _nameField.interactable = signedIn && !busy;
            _copyButton.Interactable = signedIn && cloud.Profile != null
                                       && !string.IsNullOrEmpty(cloud.Profile.FriendCode);

            AccountProfile profile = cloud.Profile;
            if (profile != null)
            {
                if (!_nameField.isFocused) _nameField.text = profile.DisplayName;
                _friendCode.text = string.IsNullOrEmpty(profile.FriendCode)
                    ? "------" : FriendCode.Format(profile.FriendCode);
            }
            else
            {
                _friendCode.text = "------";
            }

            // The link card carries the whole warning while the account is anonymous and becomes a
            // receipt once it is not - same card, two states, so a player who linked an email can
            // see that they did.
            if (anonymous)
            {
                _linkHeading.text = "PROTECT THIS ACCOUNT";
                _linkWarning.text = "This account has no email. It lives only on this device - if you "
                                  + "lose this phone, reinstall, or clear the app's data, the save goes "
                                  + "with it and NOBODY can get it back. Link an email and it becomes "
                                  + "recoverable from anywhere.";
                _linkWarning.color = UITheme.Warning;
                _linkButton.SetText("LINK EMAIL");
                _linkButton.Interactable = signedIn && !busy;
                _emailField.interactable = signedIn && !busy;
                _passwordField.interactable = signedIn && !busy;
            }
            else
            {
                _linkHeading.text = "THIS ACCOUNT IS PROTECTED";
                _linkWarning.text = "Linked to " + cloud.State.Email + ". Sign in with that email on any "
                                  + "device to reach this save.";
                _linkWarning.color = UITheme.Success;
                _linkButton.Interactable = false;
                _emailField.interactable = false;
                _passwordField.interactable = false;
            }

            _signInButton.Interactable = cloud.Configured && !busy;
            _signInEmail.interactable = cloud.Configured && !busy;
            _signInPassword.interactable = cloud.Configured && !busy;

            // The gate, restated. The service refuses this while anonymous whatever the button
            // looks like; the button explains rather than enforces.
            _signOutButton.Interactable = cloud.Account != null && cloud.Account.CanSignOutSafely && !busy;
            _signOutNote.text = anonymous
                ? "You cannot sign out yet. This account has no email, so signing out would lose it for "
                  + "good. Link an email first and this becomes available."
                : "Signs this device out. Your save stays in the cloud and you can sign back in with "
                  + cloud.State.Email + ".";
        }

        private void RefreshStatusLine()
        {
            CloudCoordinator cloud = App.Cloud;
            if (cloud == null) return;

            switch (cloud.Status)
            {
                case CloudStatus.NotConfigured:
                    _statusValue.text = "NO ACCOUNT SERVICE";
                    _statusValue.color = Emberforge.TextQuiet;
                    break;
                case CloudStatus.Offline:
                    _statusValue.text = "OFFLINE";
                    _statusValue.color = Emberforge.TextQuiet;
                    break;
                case CloudStatus.Working:
                    _statusValue.text = "WORKING";
                    _statusValue.color = Emberforge.TextGold;
                    break;
                case CloudStatus.Failed:
                    _statusValue.text = "LAST SYNC FAILED";
                    _statusValue.color = UITheme.Danger;
                    break;
                default:
                    _statusValue.text = "SIGNED IN";
                    _statusValue.color = UITheme.Success;
                    break;
            }

            string note = "";
            if (cloud.Status == CloudStatus.Offline)
                note = "Your progress is saved on this device. It will sync when you are back online.";
            if (!string.IsNullOrEmpty(cloud.Message)) note = cloud.Message;

            long last = cloud.LastSyncUnix;
            string when = last > 0 ? "Last synced " + Ago(last) + "." : "Not synced yet.";
            _statusNote.text = string.IsNullOrEmpty(note) ? when : when + "  " + note;
        }

        private static string Ago(long unix)
        {
            long seconds = TimeUtil.NowUnix() - unix;
            if (seconds < 90) return "just now";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + " minute" + (minutes == 1 ? "" : "s") + " ago";
            long hours = minutes / 60;
            if (hours < 48) return hours + " hour" + (hours == 1 ? "" : "s") + " ago";
            return (hours / 24) + " days ago";
        }

        private void SetAll(bool interactable)
        {
            _syncButton.Interactable = interactable;
            _nameButton.Interactable = interactable;
            _copyButton.Interactable = interactable;
            _linkButton.Interactable = interactable;
            _signInButton.Interactable = interactable;
            _signOutButton.Interactable = interactable;
            _nameField.interactable = interactable;
            _emailField.interactable = interactable;
            _passwordField.interactable = interactable;
            _signInEmail.interactable = interactable;
            _signInPassword.interactable = interactable;
        }

        // ------------------------------------------------------------------ actions

        private void OnSyncNow()
        {
            if (App.Cloud == null) return;
            App.Audio.Tap();
            StartCoroutine(App.Cloud.SyncNow(delegate (string error)
            {
                // A conflict is not an error and reports none: the chooser is already open and the
                // player is mid-answer, so a toast saying "sync failed" on top of it would be
                // wrong twice over.
                if (!string.IsNullOrEmpty(error)) App.Notify(error);
                Refresh();
            }));
        }

        private void OnSaveName()
        {
            if (App.Cloud == null) return;
            App.Audio.Tap();
            string typed = _nameField.text;
            StartCoroutine(App.Cloud.SetDisplayName(typed, delegate (string error)
            {
                App.Notify(string.IsNullOrEmpty(error) ? "You are " + typed.Trim() + " to your friends." : error);
                Refresh();
            }));
        }

        /// <summary>
        /// The code to the system clipboard, unformatted - no dash.
        ///
        /// The dash is a reading aid the formatter adds; what is copied is what gets pasted into
        /// somebody else's Add Friend box, and FriendCode.Normalise would strip it anyway. Copying
        /// the plain six characters means the paste is correct even somewhere that does not.
        /// </summary>
        private void OnCopyCode()
        {
            if (App.Cloud == null || App.Cloud.Profile == null) return;
            App.Audio.Tap();
            string code = App.Cloud.Profile.FriendCode;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            App.Notify("Friend code copied.");
        }

        private void OnLinkEmail()
        {
            if (App.Cloud == null) return;
            App.Audio.Tap();

            string email = _emailField.text;
            string password = _passwordField.text;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                App.Notify("An email and a password are both needed.");
                return;
            }

            StartCoroutine(App.Cloud.LinkEmail(email, password, delegate (string error)
            {
                if (!string.IsNullOrEmpty(error)) { App.Notify(error); Refresh(); return; }
                _passwordField.text = "";
                // The service's own message survives here: if the project requires confirmation,
                // it says the account is not recoverable until the link is clicked, and that is
                // precisely the sentence a player must not be told a rosier version of.
                App.Modal.ShowNotice("EMAIL LINKED",
                    string.IsNullOrEmpty(App.Cloud.Message)
                        ? "Your account is now recoverable. Sign in with " + email.Trim()
                          + " on any device to reach this save."
                        : App.Cloud.Message,
                    "GOOD", null);
                Refresh();
            }));
        }

        /// <summary>
        /// Signing in as somebody else, behind a confirm - and the confirm is not a formality.
        ///
        /// This REPLACES the account this device is using. If that account is anonymous it cannot
        /// be reached again afterwards by anyone, which is the same loss signing out would cause
        /// and is why signing out is refused outright. It is allowed here because the player is
        /// doing something with a purpose rather than discarding one, but they have to be told the
        /// price in the same words.
        /// </summary>
        private void OnSignInWithEmail()
        {
            if (App.Cloud == null) return;
            App.Audio.Tap();

            string email = _signInEmail.text;
            string password = _signInPassword.text;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                App.Notify("An email and a password are both needed.");
                return;
            }

            if (App.Cloud.State.IsAnonymous)
            {
                App.Modal.ShowConfirm("LEAVE THIS ACCOUNT?",
                    "The account on this device has no email attached, so once you sign in as somebody "
                    + "else it cannot be reached again by anyone - including you. Link an email first if "
                    + "you want to keep it.",
                    "SIGN IN ANYWAY", "GO BACK",
                    delegate { StartSignIn(email, password); }, null);
                return;
            }
            StartSignIn(email, password);
        }

        private void StartSignIn(string email, string password)
        {
            StartCoroutine(App.Cloud.SignInWithEmail(email, password, delegate (string error)
            {
                if (!string.IsNullOrEmpty(error)) App.Notify(error);
                else
                {
                    _signInPassword.text = "";
                    App.Notify("Signed in.");
                }
                Refresh();
            }));
        }

        private void OnSignOut()
        {
            if (App.Cloud == null) return;
            App.Audio.Tap();
            App.Modal.ShowConfirm("SIGN OUT?",
                "This device will forget the account. Your save stays in the cloud and you can sign "
                + "back in with " + App.Cloud.State.Email + " at any time.",
                "SIGN OUT", "STAY",
                delegate
                {
                    StartCoroutine(App.Cloud.SignOut(delegate (string error)
                    {
                        App.Notify(string.IsNullOrEmpty(error) ? "Signed out." : error);
                        Refresh();
                    }));
                }, null);
        }

        // ------------------------------------------------------------------ shared

        private RectTransform Card(RectTransform parent, float height)
        {
            RectTransform card = UIBuilder.Node("Card", parent);
            UIBuilder.Sized(card, CardWidth, height);

            EmberforgeUI.PanelOn(card, false);
            return card;
        }

        /// <summary>A section heading, and deliberately not a ribbon: this page is a column of
        /// five cards, and five ribbons stacked down it would be five banners fighting each
        /// other. See the note on EmberforgeUI.Heading.</summary>
        private Text Heading(RectTransform card, string text)
        {
            return EmberforgeUI.Heading("Heading", card, text, 30f, -22f);
        }
    }
}
