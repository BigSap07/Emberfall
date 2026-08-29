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
            BuildOptions();
        }

        /// <returns>Distance from the top of the screen to the bottom of the block just built.</returns>
        private float BuildTitle(float top)
        {
            // one type-scale step smaller than the portrait logo - the landscape left column has
            // 1080 of height for four blocks where portrait had 1920 for five
            const float logoHeight = 80f;
            Text logo = UIBuilder.Label("Logo", Root, "EMBERFALL", UITheme.FontDisplay, UITheme.Gold, TextAnchor.UpperCenter);
            UIBuilder.Box(logo.rectTransform, LeftColumnX, -top, 900f, logoHeight, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(logo, new Color(0.35f, 0.12f, 0.02f, 0.9f), 3f);
            UIBuilder.Shadowed(logo, UITheme.InkPanel, 5f);
            top += logoHeight;

            top += UITheme.Space16;
            const float ruleHeight = 3f;
            Image underline = UIBuilder.Img("Rule", Root, TextureLab.White, UITheme.WithAlpha(UITheme.Ember, 0.75f));
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

            _chapterCount = UIBuilder.Label("Count", card, "", UITheme.FontSmall, UITheme.Ember, TextAnchor.UpperRight);
            UIBuilder.Box(_chapterCount.rectTransform, -26f, -20f, 300f, 34f, new Vector2(1f, 1f));

            _chapterFill = UIBuilder.Bar("Bar", card, UITheme.PanelSunken, UITheme.Ember);
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
            _continueButton = UIBuilder.PrimaryButton("Continue", Root, "CONTINUE CAMPAIGN", UITheme.FontHeading);
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
            AddNavButton(nav, "CAMPAIGN", 4, UITheme.Ember, OpenCampaign);
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

            AddNavButton(nav, "RELICS", 6, UITheme.Ember, OpenRelicShop);
            AddNavButton(nav, "INVENTORY", 7, UITheme.TextMuted, OpenInventory);
            AddNavButton(nav, "EVENTS", 8, UITheme.Success, OpenEvents);
            AddNavButton(nav, "RECITAL", 9, UITheme.Mythic, OpenRecital);
            // Sigil 10 ("block / golem") reads as a solid, blocky shape - the closest fit in the
            // fixed 14-sigil set to an anvil/workbench silhouette. Tinted with the Gilded rung's
            // colour, the same one InventoryScreen's Slag row already uses, so the Forge's icon and
            // the currency it spends read as the same idea at a glance.
            AddNavButton(nav, "FORGE", 10, UITheme.ArmorTemperColor(ArmorTemper.Gilded), OpenForge);
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
