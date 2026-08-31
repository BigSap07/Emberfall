using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>Home base: identity, the current squad, idle income and the three ways forward.</summary>
    public sealed class MainMenuScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.MainMenu; } }

        private RectTransform _teamRow;
        private Text _idleAmounts;
        private Text _idleTimer;
        private Text _idleRate;
        private UIButton _claimButton;
        private UIButton _continueButton;
        private Text _continueSub;
        private float _tick;

        private Text _chapterLabel;
        private Image _chapterFill;
        private Text _chapterCount;

        // Landscape splits the old single portrait stack into two top-down cursors, one per
        // column, keeping the same rule: each block's position derives from the one above it
        // plus a spacing-scale gap, never an independent hand-tuned literal. Left column is
        // identity and the way forward (title, banner, chapter, continue); right column is
        // income and navigation (cache, nav). Both start below the shared top bar.
        private const float CardWidth = 900f;
        private const float LeftColumnX = -462f;   // half the 24f column gap plus half a card
        private const float RightColumnX = 462f;
        private const float ColumnTop = UITheme.TopBarHeight + UITheme.Space24;

        protected override void Build()
        {
            // Drawn first, so every card and label above it in this method covers it wherever they
            // overlap - the Hub's own base material, not the shared purple vignette every other
            // screen sits on (GameApp's canvas background). The Keeper's fiction is explicit that
            // this office "takes no side among the six" (DESIGN-WORLD, "The Cinder and the Keeper"),
            // so the Hub gets its own neutral floor rather than inheriting a stray accent colour that
            // happens to bleed through every screen's empty space.
            BuildBackdrop();

            float left = ColumnTop;
            left = BuildTitle(left);
            left += UITheme.Space24;
            left = BuildTeamShowcase(left);
            left += UITheme.Space24;
            left = BuildChapterProgress(left);
            left += UITheme.Space24;
            left = BuildContinue(left);

            float right = ColumnTop;
            right = BuildIdleCache(right);

            BuildNav();
            BuildSecondNav();
            BuildCinderMark();
            BuildOptions();
        }

        /// <summary>
        /// The Hub's own neutral floor plus its one quiet ambient cue: a soft, colourless glow
        /// behind the wordmark. Nothing here is tinted by aspect and nothing here moves - the Cinder
        /// "never grows a face, speaks through a mouth, sprouts mascot features, or becomes a cute
        /// companion" (DESIGN-ART-BIBLE section 5), and the same discipline extends to its glow: an
        /// idle shimmer would be the mascot behaviour the source text explicitly forbids, just spread
        /// across a bigger area instead of a corner icon.
        /// </summary>
        private void BuildBackdrop()
        {
            Image baseFill = UIBuilder.Img("Backdrop", Root, TextureLab.White, UITheme.Backdrop);
            UIBuilder.FillParent(baseFill.rectTransform, 0f);

            // 0.10, was 0.05. The original was tuned against this screen's near-black violet floor,
            // where 5% of a cool grey was already a visible lift. The warmed floor is lighter and
            // warmer, and at 0.05 the glow simply stopped registering against it.
            Image glow = UIBuilder.Img("AmbientGlow", Root, TextureLab.Glow(512, 1.6f), UITheme.WithAlpha(UITheme.CinderGlass, 0.10f));
            UIBuilder.Box(glow.rectTransform, 0f, -260f, 1400f, 1400f, new Vector2(0.5f, 1f));
        }

        /// <returns>Distance from the top of the screen to the bottom of the block just built.</returns>
        private float BuildTitle(float top)
        {
            // one type-scale step smaller than the portrait logo - the landscape left column has
            // 1080 of height for four blocks where portrait had 1920 for five
            const float logoHeight = 80f;
            Text logo = UIBuilder.Label("Logo", Root, "EMBERFALL", UITheme.FontDisplay, UITheme.Gold, TextAnchor.UpperCenter);
            UIBuilder.Box(logo.rectTransform, LeftColumnX, -top, 900f, logoHeight, new Vector2(0.5f, 1f));
            // Was a warm ember-brown rim - an aspect colour on the one screen the Keeper's fiction
            // says takes no side among the six. InkPanel is the same neutral rim every other label
            // in the project uses; the wordmark no longer gets a bespoke aspect-flavoured exception.
            UIBuilder.Outlined(logo, UITheme.InkPanel, 3f);
            UIBuilder.Shadowed(logo, UITheme.InkPanel, 5f);
            top += logoHeight;

            top += UITheme.Space16;
            const float ruleHeight = 3f;
            Image underline = UIBuilder.Img("Rule", Root, TextureLab.White, UITheme.WithAlpha(UITheme.CinderGlass, 0.6f));
            UIBuilder.Box(underline.rectTransform, LeftColumnX, -top, 340f, ruleHeight, new Vector2(0.5f, 1f));
            top += ruleHeight;

            top += UITheme.Space16;
            const float subHeight = 32f;
            Text sub = UIBuilder.Label("Sub", Root, "THE ASHEN ROAD", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(sub.rectTransform, LeftColumnX, -top, 700f, subHeight, new Vector2(0.5f, 1f));
            top += subHeight;

            return top;
        }

        private float BuildTeamShowcase(float top)
        {
            const float cardHeight = 316f;
            RectTransform card = UIBuilder.Node("Squad", Root);
            UIBuilder.Box(card, LeftColumnX, -top, CardWidth, cardHeight, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(24);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.Panel, 0.92f);
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 24, 2);

            float inner = UITheme.Space24;
            const float headingHeight = 26f;
            Text heading = UIBuilder.Label("Heading", card, "ACTIVE BANNER", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(heading.rectTransform, 0f, -inner, 400f, headingHeight, new Vector2(0.5f, 1f));
            inner += headingHeight + UITheme.Space8;

            const float rowHeight = 232f;
            _teamRow = UIBuilder.Node("Row", card);
            UIBuilder.Box(_teamRow, 0f, -inner, 880f, rowHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_teamRow, 22f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            return top + cardHeight;
        }

        private float BuildIdleCache(float top)
        {
            const float cardHeight = 358f;
            RectTransform card = UIBuilder.Node("IdleCache", Root);
            UIBuilder.Box(card, RightColumnX, -top, CardWidth, cardHeight, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(24);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.Gold, 0.4f), 24, 2);

            Image glow = UIBuilder.Img("Glow", card, TextureLab.Glow(128, 2f), UITheme.WithAlpha(UITheme.Gold, 0.10f));
            UIBuilder.FillParent(glow.rectTransform, 0f);

            // text stack, top-left, each row separated by the same tight inline gap
            float inner = UITheme.Space24;
            const float headingHeight = 42f;
            Text heading = UIBuilder.Label("Heading", card, "EMBER CACHE", UITheme.FontHeading, UITheme.Gold, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 30f, -inner, 500f, headingHeight, new Vector2(0f, 1f));
            inner += headingHeight + UITheme.Space8;

            const float rateHeight = 26f;
            _idleRate = UIBuilder.Label("Rate", card, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleRate.rectTransform, 30f, -inner, 620f, rateHeight, new Vector2(0f, 1f));
            inner += rateHeight + UITheme.Space8;

            const float amountsHeight = 46f;
            _idleAmounts = UIBuilder.Label("Amounts", card, "", UITheme.FontHeading, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleAmounts.rectTransform, 30f, -inner, 620f, amountsHeight, new Vector2(0f, 1f));
            inner += amountsHeight + UITheme.Space8;

            const float timerHeight = 32f;
            _idleTimer = UIBuilder.Label("Timer", card, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleTimer.rectTransform, 30f, -inner, 620f, timerHeight, new Vector2(0f, 1f));
            inner += timerHeight;

            // COLLECT sits below the whole text stack (plus a section gap) instead of floating in
            // the bottom-right corner, where it used to overlap the timer label by 20px.
            inner += UITheme.Space24;
            _claimButton = UIBuilder.GoldButton("Claim", card, "COLLECT", UITheme.FontBody);
            UIBuilder.Box(_claimButton.Root, -30f, cardHeight - inner - 116f, 300f, 116f, new Vector2(1f, 0f));
            _claimButton.OnClick(ClaimIdle);

            return top + cardHeight;
        }

        private float BuildChapterProgress(float top)
        {
            const float cardHeight = 150f;
            RectTransform card = UIBuilder.Node("Progress", Root);
            UIBuilder.Box(card, LeftColumnX, -top, CardWidth, cardHeight, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(20);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.Panel, 0.9f);
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 20, 2);

            _chapterLabel = UIBuilder.Label("Chapter", card, "", UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_chapterLabel.rectTransform, 26f, -20f, 640f, 34f, new Vector2(0f, 1f));

            _chapterCount = UIBuilder.Label("Count", card, "", UITheme.FontSmall, UITheme.Gold, TextAnchor.UpperRight);
            UIBuilder.Box(_chapterCount.rectTransform, -26f, -20f, 300f, 34f, new Vector2(1f, 1f));

            // Gold, not Ember: this bar counts cleared stages, a fact about the player's own
            // progress rather than a claim about which of the six beliefs the Hub favours. Matches
            // the Claim button and the idle cache's own accent, so "advancement" reads as one colour
            // across the screen instead of picking up whichever aspect happened to be nearby.
            _chapterFill = UIBuilder.Bar("Bar", card, UITheme.PanelSunken, UITheme.Gold);
            RectTransform barRoot = _chapterFill.rectTransform.parent as RectTransform;
            barRoot.anchorMin = new Vector2(0f, 0f);
            barRoot.anchorMax = new Vector2(1f, 0f);
            barRoot.pivot = new Vector2(0.5f, 0f);
            barRoot.offsetMin = new Vector2(26f, 34f);
            barRoot.offsetMax = new Vector2(-26f, 54f);

            return top + cardHeight;
        }

        private float BuildContinue(float top)
        {
            const float buttonHeight = 168f;
            // GoldButton, not PrimaryButton: PrimaryButton fills Ember, which made the single
            // biggest coloured surface on the Hub the Ember aspect's own accent. Gold is this
            // project's established "important action" colour everywhere else on this screen (the
            // idle cache's Claim button, the chapter bar) and carries no aspect meaning at all.
            _continueButton = UIBuilder.GoldButton("Continue", Root, "CONTINUE CAMPAIGN", UITheme.FontHeading);
            UIBuilder.Box(_continueButton.Root, LeftColumnX, -top, CardWidth, buttonHeight, new Vector2(0.5f, 1f));
            _continueButton.OnClick(OpenCampaign);
            top += buttonHeight + UITheme.Space16;

            const float subHeight = 34f;
            _continueSub = UIBuilder.Label("ContinueSub", Root, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_continueSub.rectTransform, LeftColumnX, -top, 900f, subHeight, new Vector2(0.5f, 1f));
            top += subHeight;

            return top;
        }

        private void BuildNav()
        {
            // bottom of the right column, mirroring the portrait bottom-nav muscle memory on the
            // half of the screen the thumb rests on in landscape
            RectTransform nav = UIBuilder.Node("Nav", Root);
            UIBuilder.Box(nav, RightColumnX, 30f, CardWidth, 200f, new Vector2(0.5f, 0f));
            UIBuilder.HStack(nav, 20f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            AddNavButton(nav, "HEROES", 0, UITheme.Shard, OpenHeroes);
            // CinderGlass, not Ember: the campaign spans all six regions (DESIGN-WORLD, "The six
            // regions"), so its nav icon should not read as one of them.
            AddNavButton(nav, "CAMPAIGN", 4, UITheme.CinderGlass, OpenCampaign);
            AddNavButton(nav, "SUMMON", 13, UITheme.StarColor(5), OpenSummon);
        }

        private void AddNavButton(RectTransform parent, string label, int sigil, Color tint, System.Action action)
        {
            UIButton button = UIBuilder.MakeButton("Nav" + label, parent, "", UITheme.FontSmall,
                                                   UITheme.PanelRaised, UITheme.WithAlpha(tint, 0.6f), UITheme.TextPrimary);
            UIBuilder.Flexible(button.Root, 1f, 0f);
            button.OnClick(action);

            Image glyph = UIBuilder.Img("Glyph", button.Root, TextureLab.Sigil(sigil, 64), tint);
            UIBuilder.Box(glyph.rectTransform, 0f, -18f, 62f, 62f, new Vector2(0.5f, 1f));

            button.Label.text = label;
            button.Label.alignment = TextAnchor.LowerCenter;
            UIBuilder.Box(button.Label.rectTransform, 0f, 16f, 260f, 32f, new Vector2(0.5f, 0f));
            button.Label.fontSize = UITheme.FontTiny;
        }

        private void BuildSecondNav()
        {
            // second row of destination buttons, stacked above the primary nav
            RectTransform nav = UIBuilder.Node("Nav2", Root);
            UIBuilder.Box(nav, RightColumnX, 246f, CardWidth, 170f, new Vector2(0.5f, 0f));
            UIBuilder.HStack(nav, 20f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            // The legendary relic grade's own colour, not Ember: DESIGN-WORLD is explicit that
            // relics "carry a fragment of an alternative behaviour of reality" unrelated to the
            // First Light the six aspects came from, so this icon should never borrow one of them.
            AddNavButton(nav, "RELICS", 6, UITheme.RelicGradeColor(RelicGrade.Legendary), OpenRelicShop);
            AddNavButton(nav, "INVENTORY", 7, UITheme.TextMuted, OpenInventory);
            AddNavButton(nav, "EVENTS", 8, UITheme.Success, OpenEvents);
            AddNavButton(nav, "RECITAL", 9, UITheme.Mythic, OpenRecital);
            // Sigil 10 ("block / golem") reads as a solid, blocky shape - the closest fit in the
            // fixed 14-sigil set to an anvil/workbench silhouette. Tinted with the Gilded rung's
            // colour, the same one InventoryScreen's Slag row already uses, so the Forge's icon and
            // the currency it spends read as the same idea at a glance.
            AddNavButton(nav, "FORGE", 10, UITheme.ArmorTemperColor(ArmorTemper.Gilded), OpenForge);
        }

        /// <summary>
        /// The Hub's one corner watermark: a small, static, colourless outline of the Cinder itself,
        /// sitting in the same open gap BuildOptions already uses (idle cache card ends 514, second
        /// nav starts 664 - see that method's own comment). Placed here rather than invented as new
        /// empty space, and sized to clear both the card above and the nav below by roughly 15px so
        /// it reads as a deliberate mark rather than a stray shape wedged into whatever was left, the
        /// caution DESIGN-ART-BIBLE's own engine notes raise about absence-based choices on a dark
        /// panel reading as a rendering fault rather than a design one.
        ///
        /// Static by construction - no Update, no coroutine, nothing driving it frame to frame.
        /// DESIGN-ART-BIBLE section 5 is explicit that the Cinder "never grows a face, speaks
        /// through a mouth, sprouts mascot features, or becomes a cute companion"; an idle bounce or
        /// pulse here would be exactly that, just abstracted from a face onto a shape.
        ///
        /// KNOWN BUG, measured in Play Mode, not yet fixed here: the 15px clearance described above
        /// only exists when the logical canvas is exactly 1080 tall. This mark is anchored to the
        /// TOP edge while the nav row it must clear is anchored to the BOTTOM, so the gap between
        /// them is not a constant - it shrinks by one unit for every unit of logical height lost.
        /// GameApp's scaler is MatchWidthOrHeight at 0.5 against a 1920x1080 reference, so a
        /// 20:9 Android landscape device - the shape this game actually ships to - yields roughly
        /// 959 logical units of height, leaving about 29 units of gap for a 120-unit mark. The
        /// mark then lands on top of the second nav row (it is built after BuildSecondNav, so it
        /// paints over it) and reads as a smudge across the EVENTS/INVENTORY/RECITAL buttons -
        /// precisely the "reads as a rendering fault rather than a design one" outcome the caution
        /// above was written to avoid. Confirmed on a real capture at 1161x515.
        ///
        /// Not fixed in this commit because there is no correct hard-coded y: at realistic device
        /// aspects the gap this mark was placed into does not exist at all, so the fix is a
        /// composition decision, not a number. It belongs to the layout pass that rebuilds this
        /// screen against DesignReference/Main.dc.html.
        /// </summary>
        private void BuildCinderMark()
        {
            const float size = 120f;
            // 0.32, was 0.22. Same reason as the ambient glow: a warmer, lighter floor eats a
            // low-alpha cool grey. The mark is an outline shape with no fill, so it loses legibility
            // faster than a solid would - there is no mass to survive the wash-out, only thin lines.
            Image mark = UIBuilder.Img("CinderMark", Root, TextureLab.CinderFragment(256),
                                       UITheme.WithAlpha(UITheme.CinderGlass, 0.32f));
            UIBuilder.Box(mark.rectTransform, RightColumnX, -529f, size, size, new Vector2(0.5f, 1f));
        }

        private void BuildOptions()
        {
            // right screen edge, in the gap between the cache card (ends 514) and the nav (starts 850)
            UIButton options = UIBuilder.IconButton("Options", Root, "...", 62f, UITheme.WithAlpha(UITheme.PanelRaised, 0.9f), UITheme.TextMuted);
            UIBuilder.Box(options.Root, -UITheme.ScreenPadding, -538f, 62f, 62f, new Vector2(1f, 1f));
            options.OnClick(ShowOptions);
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            RefreshTeam();
            RefreshIdle();
            RefreshContinue();
            RefreshChapter();
            App.RefreshHud();
            _tick = 0f;
        }

        private void RefreshChapter()
        {
            List<ChapterDef> chapters = App.Session.Content.Chapters;
            if (chapters.Count == 0)
            {
                _chapterLabel.text = "NO CAMPAIGN CONTENT";
                _chapterCount.text = "";
                UIBuilder.SetBar(_chapterFill, 0f);
                return;
            }

            ChapterDef chapter = chapters[0];
            int cleared = 0;
            for (int i = 0; i < chapter.Stages.Count; i++)
                if (App.Session.Profile.HasClearedStage(chapter.Stages[i].Id)) cleared++;

            _chapterLabel.text = "CHAPTER " + chapter.Number + "  -  " + chapter.Name.ToUpperInvariant();
            _chapterCount.text = cleared + " / " + chapter.Stages.Count + " CLEARED";
            UIBuilder.SetBar(_chapterFill, chapter.Stages.Count > 0 ? cleared / (float)chapter.Stages.Count : 0f);
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            RefreshIdle();
        }

        private void RefreshTeam()
        {
            for (int i = _teamRow.childCount - 1; i >= 0; i--) Destroy(_teamRow.GetChild(i).gameObject);

            // Five cells across the 880f row: 5 * 158 + 4 * 22 spacing = 878. The old three-across
            // cells were 270f; the portrait still fits, the name labels got narrower with them.
            const float cellWidth = 158f;
            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int slot = 0; slot < PlayerProfile.TeamSize; slot++)
            {
                RectTransform cell = UIBuilder.Node("Slot" + slot, _teamRow);
                UIBuilder.Sized(cell, cellWidth, 196f);

                if (slot >= team.Count)
                {
                    // an open slot is room to grow, not a hole: a ring where the portrait will
                    // stand (same convention as the campaign banner strip) and the slot's number,
                    // so the cell promises a fourth and fifth ally rather than apologising
                    Image ring = UIBuilder.Img("Ring", cell, TextureLab.Ring(96, 5), UITheme.BorderSoft);
                    UIBuilder.Box(ring.rectTransform, 0f, -4f, 122f, 122f, new Vector2(0.5f, 1f));
                    Text hint = UIBuilder.Label("Hint", cell, "SLOT " + (slot + 1), UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
                    UIBuilder.Box(hint.rectTransform, 0f, -132f, 154f, 26f, new Vector2(0.5f, 1f));
                    continue;
                }

                HeroInstance hero = team[slot];
                RectTransform portrait = Widgets.Portrait("Portrait", cell, hero.Def, hero.Def.Stars, 122f, true);
                UIBuilder.Box(portrait, 0f, -4f, 122f, 122f, new Vector2(0.5f, 1f));

                Text name = UIBuilder.Label("Name", cell, hero.Def.Name, UITheme.FontTiny, UITheme.TextPrimary, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -132f, 154f, 26f, new Vector2(0.5f, 1f));

                Text meta = UIBuilder.Label("Meta", cell, "Lv " + hero.Level + "  -  " + hero.Power,
                                            UITheme.FontTiny, UITheme.Gold, TextAnchor.UpperCenter);
                UIBuilder.Box(meta.rectTransform, 0f, -160f, 154f, 26f, new Vector2(0.5f, 1f));
            }
        }

        private void RefreshIdle()
        {
            GameSession session = App.Session;
            IdleReport report = session.PeekIdle(TimeUtil.NowUnix());

            _idleRate.text = "+" + Mathf.RoundToInt(session.IdleGoldPerHour) + " gold  /  +"
                             + Mathf.RoundToInt(session.IdleShardsPerHour) + " shards per hour";
            _idleAmounts.text = report.Gold + " GOLD    " + report.Shards + " SHARDS";
            _idleTimer.text = FormatDuration(report.Seconds) + " stored"
                              + (report.Capped ? "  (cache full)" : "  -  cap " + Mathf.RoundToInt(session.Balance.IdleCapHours) + "h");

            _claimButton.Interactable = report.HasAnything;
        }

        private void RefreshContinue()
        {
            StageDef next = App.Session.NextStage;
            if (next == null)
            {
                _continueButton.SetText("CAMPAIGN");
                _continueSub.text = "No campaign content loaded.";
                return;
            }

            bool complete = App.Session.CampaignComplete;
            _continueButton.SetText(complete ? "REPLAY CAMPAIGN" : "CONTINUE CAMPAIGN");
            _continueSub.text = (complete ? "Chapter cleared  -  " : "Next: ") + next.DisplayCode + "  " + next.Name;
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60) return seconds + "s";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "m";
            long hours = minutes / 60;
            return hours + "h " + (minutes % 60) + "m";
        }

        // ------------------------------------------------------------------ actions

        private void ClaimIdle()
        {
            IdleReport report = App.Session.ClaimIdle(TimeUtil.NowUnix());
            if (!report.HasAnything)
            {
                App.Notify("The cache is still filling.");
                return;
            }
            App.Audio.Reward();
            App.Notify("Collected " + report.Gold + " gold and " + report.Shards + " shards.");
            App.RefreshHud();
            App.RequestSave();
            RefreshIdle();
        }

        private void OpenHeroes()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Heroes);
        }

        private void OpenCampaign()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Campaign);
        }

        private void OpenSummon()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Summon);
        }

        private void OpenRelicShop()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.RelicShop);
        }

        private void OpenInventory()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Inventory);
        }

        private void OpenEvents()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Events);
        }

        private void OpenRecital()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Recital);
        }

        private void OpenForge()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Forge);
        }

        private void ShowOptions()
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm(
                "RESET PROGRESS",
                "This wipes your heroes, currency and campaign progress and starts a fresh account. " +
                "Useful for testing the first-run experience.",
                "RESET", "KEEP PLAYING",
                App.ResetProgress,
                null);
        }
    }
}
