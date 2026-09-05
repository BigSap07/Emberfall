using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

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
    /// (StagesCleared, RecitalHighestFloor, ProvingStanding, the hero list); none of them is
    /// computed or stored fresh for this screen, they are read where they live.
    ///
    /// The one genuinely new piece of state is AvatarHeroId, and it is one string.
    /// </summary>
    public sealed class ProfileScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Profile; } }

        private const int NameLimit = 16;

        private Text _headerSubtitle;
        private InputField _nameField;
        private RectTransform _avatarHost;
        private RectTransform _avatarChoices;
        private Text _avatarNote;
        private RectTransform _statList;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "YOUR PROFILE", Router, out _headerSubtitle);

            ScrollRect scroll;
            RectTransform content = UIBuilder.ScrollList("Content", Root, out scroll, 16f,
                                                         new RectOffset(0, 0, 8, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 20f);

            BuildIdentityCard(content);
            BuildAvatarCard(content);
            BuildStatsCard(content);
        }

        // ------------------------------------------------------------------ name

        /// <summary>
        /// The name, and the first InputField in this project.
        ///
        /// Built here rather than added to UIBuilder because it is the only one: putting a
        /// half-considered TextField helper into shared code on the strength of a single call site
        /// invites the next person to use it for something it was never shaped for. If a second
        /// screen ever needs one, that is the moment it becomes a builder.
        ///
        /// Committed on END EDIT rather than per keystroke - one save per rename instead of one
        /// per letter - and a name that comes back empty or whitespace reverts to the stored one
        /// rather than being accepted, because a blank name would render as an empty top bar with
        /// no way to tell it apart from a bug.
        /// </summary>
        private void BuildIdentityCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 210f);

            Text label = UIBuilder.Label("Label", card, "DISPLAY NAME", UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 28f, -22f, 400f, 26f, new Vector2(0f, 1f));

            RectTransform box = UIBuilder.Node("NameBox", card);
            UIBuilder.Box(box, 28f, -60f, 640f, 76f, new Vector2(0f, 1f));
            Image boxBg = box.gameObject.AddComponent<Image>();
            boxBg.sprite = UISkin.Panel(14);
            boxBg.type = Image.Type.Sliced;
            boxBg.color = UITheme.PanelSunken;
            UIBuilder.Frame("Edge", box, UITheme.Border, 14, 2);

            Text placeholder = UIBuilder.Label("Placeholder", box, "Your name", UITheme.FontSmall,
                                               UITheme.TextDim, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(placeholder.rectTransform, 18f);

            Text shown = UIBuilder.Label("Text", box, "", UITheme.FontSmall,
                                         UITheme.TextPrimary, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(shown.rectTransform, 18f);

            _nameField = box.gameObject.AddComponent<InputField>();
            _nameField.textComponent = shown;
            _nameField.placeholder = placeholder;
            _nameField.characterLimit = NameLimit;
            _nameField.lineType = InputField.LineType.SingleLine;
            _nameField.onEndEdit.AddListener(CommitName);

            Text hint = UIBuilder.Label("Hint", card,
                                        "Shown on the top bar. Up to " + NameLimit + " characters.",
                                        UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
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

            Text label = UIBuilder.Label("Label", card, "AVATAR", UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 28f, -22f, 400f, 26f, new Vector2(0f, 1f));

            _avatarHost = UIBuilder.Node("Current", card);
            UIBuilder.Box(_avatarHost, 40f, -58f, 150f, 150f, new Vector2(0f, 1f));

            _avatarNote = UIBuilder.Label("Note", card, "", UITheme.FontTiny,
                                          UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_avatarNote.rectTransform, 40f, 26f, 620f, 26f, new Vector2(0f, 0f));

            // Stretched across the card's remaining width, to the right of the big portrait.
            // Anchors and offsets are set directly rather than through Box, which pins both
            // anchors to one corner - the first draft did both and the contradiction collapsed
            // this row to a few pixels with every face piled on top of the next.
            _avatarChoices = UIBuilder.Node("Choices", card);
            _avatarChoices.anchorMin = new Vector2(0f, 1f);
            _avatarChoices.anchorMax = new Vector2(1f, 1f);
            _avatarChoices.pivot = new Vector2(0f, 1f);
            _avatarChoices.offsetMin = new Vector2(220f, -208f);
            _avatarChoices.offsetMax = new Vector2(-28f, -58f);
            UIBuilder.HStack(_avatarChoices, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
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
                RectTransform big = Widgets.Portrait("Chosen", _avatarHost, chosen, chosen.Stars, 150f, true);
                UIBuilder.FillParent(big, 0f);
            }
            else
            {
                Image ring = UIBuilder.Img("Empty", _avatarHost, TextureLab.Ring(160, 3),
                                           UITheme.WithAlpha(UITheme.BorderSoft, 0.8f));
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
            UIButton pick = UIBuilder.MakeButton("Avatar_" + def.Id, _avatarChoices, "", UITheme.FontTiny,
                                                 active ? UITheme.Ember : UITheme.PanelRaised,
                                                 active ? UITheme.EmberDeep : UITheme.Border,
                                                 UITheme.TextPrimary);
            // Sized, not Size: HStack runs with childControlWidth, so it takes each child's width
            // from a LayoutElement's preferredWidth and ignores sizeDelta entirely. Setting the
            // rect and adding a second, empty LayoutElement - which is what the first draft did -
            // left every choice at its minimum and squeezed seven faces into a thumbnail.
            UIBuilder.Sized(pick.Root, 118f, 150f);
            pick.Label.gameObject.SetActive(false);

            RectTransform body = UIBuilder.Node("Body", pick.Root);
            UIBuilder.FillParent(body, 0f);
            body.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            RectTransform face = Widgets.Portrait("Face", body, def, def.Stars, 84f, true);
            UIBuilder.Box(face, 0f, -12f, 84f, 84f, new Vector2(0.5f, 1f));

            Text name = UIBuilder.Label("Name", body, def.Name, UITheme.FontTiny - 4,
                                        active ? UITheme.TextOnAccent : UITheme.TextMuted,
                                        TextAnchor.LowerCenter);
            UIBuilder.Box(name.rectTransform, 0f, 10f, 112f, 34f, new Vector2(0.5f, 0f));

            string capturedId = def.Id;
            pick.OnClick(delegate { ChooseAvatar(capturedId); });
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

            Text label = UIBuilder.Label("Label", card, "LIFETIME", UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 28f, -22f, 400f, 26f, new Vector2(0f, 1f));

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

            // StagesCleared is a COUNT, so the furthest stage is the one at index count-1. Naming
            // the stage rather than printing the count is the difference between "7" and
            // "1-7 The Shardfall Verge", and only one of those means anything to a player.
            StageDef furthest = profile.StagesCleared > 0
                ? content.GetStageByGlobalIndex(profile.StagesCleared - 1) : null;
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

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);
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
