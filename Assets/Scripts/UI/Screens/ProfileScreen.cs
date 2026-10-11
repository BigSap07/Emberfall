using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Who the player is, in the only sense this game has one: a name, a face, and what they have
    /// done. Entirely local - there is no account, no server and nothing here is shared with
    /// anyone, which is why it carries no currency, no claim button and no social concept.
    ///
    /// WHAT WAS ALREADY THERE, and is therefore not reinvented here: PlayerProfile.PlayerName has
    /// existed all along, defaulting to "Wayfarer", written and read by SaveSerializer, and shown
    /// by HudTopBar as "NAME - RANK n". What never existed was any way for a player to CHANGE it -
    /// the only writer was the deserializer. Every lifetime number below already existed too
    /// (ClearedStageIds, RecitalHighestFloor, ProvingStanding, the hero list); none of them is
    /// computed or stored fresh for this screen, they are read where they live.
    ///
    /// The one genuinely new piece of state is AvatarHeroId, and it is one string.
    /// </summary>
    public sealed class ProfileScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Profile; } }

        private const int NameLimit = 16;

        private Text _headerSubtitle;
        private RectTransform _header;
        private Image _ground;
        private ScrollRect _scroll;
        private InputField _nameField;
        private RectTransform _avatarHost;
        private RectTransform _avatarChoices;
        private Text _avatarNote;
        private RectTransform _statList;

        protected override void Build()
        {
            _ground = EmberforgeUI.Ground(Root);
            _header = ScreenChrome.ForgedHeader(Root, "YOUR PROFILE", Router, out _headerSubtitle);

            RectTransform content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                                         new RectOffset(0, 0, 8, 40));
            ApplyContentInset();

            BuildIdentityCard(content);
            BuildAvatarCard(content);
            BuildStatsCard(content);
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
            // The ground goes with the header: inside a panel sheet this page already sits on the
            // sheet's framed surface - see EmberforgeUI.Ground.
            if (_ground != null) _ground.enabled = !embedded;
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

        // ------------------------------------------------------------------ name

        /// <summary>
        /// The name, and the project's first text box - though no longer its only one.
        ///
        /// It used to be built inline here, with a note saying it was not a UIBuilder helper
        /// because it was the only call site and a half-considered TextField in shared code
        /// invites misuse. That note named the condition for changing its mind - "if a second
        /// screen ever needs one" - and the accounts screens need four: a display name, a friend
        /// code, an email and a password. So the box moved to UIBuilder.TextField, which is where
        /// the placeholder, the plate, the border and the password mode now live. What stayed here
        /// is the only part that was ever about THIS screen: what the name means and what happens
        /// when it is committed.
        ///
        /// Committed on END EDIT rather than per keystroke - one save per rename instead of one
        /// per letter - and a name that comes back empty or whitespace reverts to the stored one
        /// rather than being accepted, because a blank name would render as an empty top bar with
        /// no way to tell it apart from a bug.
        /// </summary>
        private void BuildIdentityCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 210f);

            EmberforgeUI.Heading("Label", card, "DISPLAY NAME", 28f, -22f);

            _nameField = EmberforgeUI.TextField("NameBox", card, "Your name", UITheme.FontSmall,
                                                NameLimit, false);
            UIBuilder.Box(_nameField.GetComponent<RectTransform>(), 28f, -60f, 640f, 76f, new Vector2(0f, 1f));
            _nameField.onEndEdit.AddListener(CommitName);

            Text hint = EmberforgeUI.Label("Hint", card,
                                           "Shown on the top bar. Up to " + NameLimit + " characters.",
                                           UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(hint.rectTransform, 28f, -148f, 640f, 26f, new Vector2(0f, 1f));
        }

        private void CommitName(string typed)
        {
            PlayerProfile profile = App.Session.Profile;
            string trimmed = (typed ?? "").Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                _nameField.text = profile.PlayerName;   // reject, do not store a blank
                return;
            }
            if (trimmed == profile.PlayerName) return;

            profile.PlayerName = trimmed;
            App.RefreshHud();
            App.RequestSave();
            App.Notify("You are now " + trimmed + ".");
        }

        // ------------------------------------------------------------------ avatar

        private void BuildAvatarCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 300f);

            EmberforgeUI.Heading("Label", card, "AVATAR", 28f, -22f);

            _avatarHost = UIBuilder.Node("Current", card);
            UIBuilder.Box(_avatarHost, 40f, -58f, 150f, 150f, new Vector2(0f, 1f));

            _avatarNote = EmberforgeUI.Label("Note", card, "", UITheme.FontTiny,
                                             Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_avatarNote.rectTransform, 40f, 26f, 620f, 26f, new Vector2(0f, 0f));

            // A SCROLLING STRIP, not a fixed row.
            //
            // It was a HorizontalLayoutGroup in a fixed rect, which does not overflow: the group
            // runs with childControlWidth and divides the width it HAS between however many
            // children it gets. Seven owned heroes looked correct; thirty-two were fifty pixels
            // each, their badges overlapping and their names printed through one another. How
            // many faces this row holds is a property of the player's collection, so the row
            // needs somewhere to go rather than a width to be squeezed into.
            ScrollRect choiceScroll;
            _avatarChoices = UIBuilder.ScrollStrip("Choices", card, out choiceScroll, 12f,
                                                   new RectOffset(0, 0, 0, 0));
            RectTransform strip = choiceScroll.GetComponent<RectTransform>();
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0f, 1f);
            strip.offsetMin = new Vector2(220f, -212f);
            strip.offsetMax = new Vector2(-28f, -54f);
        }

        /// <summary>
        /// One choice per hero the player OWNS. Locked heroes are deliberately absent rather than
        /// shown greyed: this is a picker, every other row in it is pressable, and a wall of faces
        /// that mostly cannot be chosen would say "you are missing things" on a screen that is
        /// about what you have.
        /// </summary>
        private void RefreshAvatar()
        {
            PlayerProfile profile = App.Session.Profile;

            for (int i = _avatarChoices.childCount - 1; i >= 0; i--)
                Destroy(_avatarChoices.GetChild(i).gameObject);
            for (int i = _avatarHost.childCount - 1; i >= 0; i--)
                Destroy(_avatarHost.GetChild(i).gameObject);

            HeroDef chosen = ResolveAvatar(profile);

            if (chosen != null)
            {
                RectTransform big = EmberforgeUI.PortraitFrame("Chosen", _avatarHost, chosen,
                                                               chosen.Stars, 150f);
                UIBuilder.FillParent(big, 0f);
            }
            else
            {
                Image ring = UIBuilder.Img("Empty", _avatarHost, TextureLab.Ring(160, 3),
                                           UITheme.WithAlpha(Emberforge.GoldDeep, 0.8f));
                UIBuilder.FillParent(ring.rectTransform, 0f);
            }

            List<HeroInstance> owned = profile.Heroes;
            if (owned.Count == 0)
            {
                // The case that has to be handled rather than assumed away. It cannot happen on a
                // normal account - CreateNewProfile grants starters - but it is one line to be
                // right about, and a picker with nothing in it and no explanation is the worst
                // version of this screen.
                _avatarNote.text = "No heroes yet. Open the Rift and your first ally becomes your face.";
                return;
            }

            _avatarNote.text = chosen != null
                ? "Your face is " + chosen.Name + ". Tap another to change it."
                : "Tap a hero to make them your face.";

            for (int i = 0; i < owned.Count; i++)
            {
                HeroInstance hero = owned[i];
                if (hero.Def == null) continue;
                bool active = chosen != null && hero.Def.Id == chosen.Id;
                AddAvatarChoice(hero.Def, active);
            }
        }

        private void AddAvatarChoice(HeroDef def, bool active)
        {
            // A TAB, not a button, and the difference matters at this length. A row of thirty-two
            // teal buttons is thirty-two things shouting "press me" on a page whose actual
            // question is which ONE of them is already true - and teal in this kit means "the way
            // back out", which is not what a face in a picker is. A tab bar says both halves of
            // it: exactly one plate is lit, and the rest are the same object unlit.
            ForgeTab pick = EmberforgeUI.Tab("Avatar_" + def.Id, _avatarChoices, 118f, 150f,
                                             delegate { ChooseAvatar(def.Id); });
            // Sized, not Size: HStack runs with childControlWidth, so it takes each child's width
            // from a LayoutElement's preferredWidth and ignores sizeDelta entirely. Setting the
            // rect and adding a second, empty LayoutElement - which is what the first draft did -
            // left every choice at its minimum and squeezed seven faces into a thumbnail.
            UIBuilder.Sized(pick.Root, 118f, 150f);
            pick.Caption.gameObject.SetActive(false);
            EmberforgeUI.SetTabSelected(pick, active);

            RectTransform body = UIBuilder.Node("Body", pick.Root);
            UIBuilder.FillParent(body, 0f);
            body.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            RectTransform face = EmberforgeUI.PortraitFrame("Face", body, def, def.Stars, 84f);
            UIBuilder.Box(face, 0f, -12f, 84f, 84f, new Vector2(0.5f, 1f));

            Text name = EmberforgeUI.Label("Name", body, def.Name, UITheme.FontTiny - 4,
                                           active ? Emberforge.TextGold : Emberforge.TextBody,
                                           TextAnchor.LowerCenter);
            UIBuilder.Box(name.rectTransform, 0f, 10f, 112f, 34f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void ChooseAvatar(string heroId)
        {
            App.Audio.Tap();
            PlayerProfile profile = App.Session.Profile;
            profile.AvatarHeroId = profile.AvatarHeroId == heroId ? "" : heroId;
            App.RequestSave();
            RefreshAvatar();
        }

        /// <summary>
        /// The stored avatar, but only if the player still owns that hero. A def id survives a uid
        /// dying, which is why it is stored - but it does not survive the hero being salvaged out
        /// of the account, and a face the player no longer commands should not represent them.
        /// </summary>
        private HeroDef ResolveAvatar(PlayerProfile profile)
        {
            if (string.IsNullOrEmpty(profile.AvatarHeroId)) return null;
            if (!profile.Owns(profile.AvatarHeroId)) return null;
            return App.Session.Content.GetHero(profile.AvatarHeroId);
        }

        // ------------------------------------------------------------------ lifetime stats

        private void BuildStatsCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 320f);

            EmberforgeUI.Heading("Label", card, "LIFETIME", 28f, -22f);

            _statList = UIBuilder.Node("Stats", card);
            _statList.anchorMin = new Vector2(0f, 0f);
            _statList.anchorMax = new Vector2(1f, 1f);
            _statList.offsetMin = new Vector2(28f, 20f);
            _statList.offsetMax = new Vector2(-28f, -58f);
            UIBuilder.VStack(_statList, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        }

        /// <summary>
        /// Read-only, and every number is read from where it already lives rather than tracked
        /// separately for this screen - a second copy of "highest floor" is a second thing to keep
        /// in step with the first.
        /// </summary>
        private void RefreshStats()
        {
            for (int i = _statList.childCount - 1; i >= 0; i--)
                Destroy(_statList.GetChild(i).gameObject);

            PlayerProfile profile = App.Session.Profile;
            ContentDatabase content = App.Session.Content;

            // The furthest stage is the cleared id that sits latest in the campaign, read from
            // ClearedStageIds through the static helper because this screen may be handed a
            // profile that is not the session's. Naming the stage rather than printing a count is
            // the difference between "7" and "1-7 The Shardfall Verge", and only one of those
            // means anything to a player.
            StageDef furthest = CampaignProgress.FurthestCleared(profile, content);
            StatRow("FURTHEST STAGE", furthest != null
                ? furthest.DisplayCode + "  " + furthest.Name
                : "none cleared yet", furthest != null ? UITheme.TextPrimary : UITheme.TextDim);

            StatRow("HEROES OWNED", profile.Heroes.Count + " of " + content.Heroes.Count, UITheme.TextPrimary);

            StatRow("DEEP RECITAL", profile.RecitalHighestFloor > 0
                ? "floor " + profile.RecitalHighestFloor
                : "not yet attempted",
                profile.RecitalHighestFloor > 0 ? UITheme.TextPrimary : UITheme.TextDim);

            // Both numbers, because they answer different questions: standing is where you are now
            // and best is what you have ever managed, and a mode whose standing falls on a loss
            // would otherwise erase its own high-water mark from this screen.
            StatRow("PROVING STANDING", profile.ProvingHighestStanding > 0
                ? profile.ProvingStanding + "   (best " + profile.ProvingHighestStanding + ")"
                : "not yet entered",
                profile.ProvingHighestStanding > 0 ? UITheme.TextPrimary : UITheme.TextDim);

            StatRow("WAYFARER RANK", profile.PlayerLevel.ToString(), UITheme.Gold);
            StatRow("RIFT PULLS", profile.TotalSummons.ToString(), UITheme.TextPrimary);
        }

        private void StatRow(string label, string value, Color tint)
        {
            Widgets.StatRow(label, _statList, label, value, tint, 40f);
        }

        // ------------------------------------------------------------------ shared

        private RectTransform Card(RectTransform parent, float height)
        {
            RectTransform card = UIBuilder.Node("Card", parent);
            UIBuilder.Sized(card, 0f, height);
            EmberforgeUI.PanelOn(card, false);
            return card;
        }

        public override void OnShow()
        {
            App.RefreshHud();

            PlayerProfile profile = App.Session.Profile;
            _nameField.text = profile.PlayerName;
            _headerSubtitle.text = "RANK " + profile.PlayerLevel;

            RefreshAvatar();
            RefreshStats();
        }
    }
}
