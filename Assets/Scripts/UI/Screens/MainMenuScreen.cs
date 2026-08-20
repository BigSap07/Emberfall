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

        protected override void Build()
        {
            BuildTitle();
            BuildTeamShowcase();
            BuildIdleCache();
            BuildChapterProgress();
            BuildContinue();
            BuildNav();
            BuildOptions();
        }

        private void BuildTitle()
        {
            Text logo = UIBuilder.Label("Logo", Root, "EMBERFALL", UITheme.FontDisplay + 22, UITheme.Gold, TextAnchor.UpperCenter);
            UIBuilder.Box(logo.rectTransform, 0f, -180f, 900f, 100f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(logo, new Color(0.35f, 0.12f, 0.02f, 0.9f), 3f);
            UIBuilder.Shadowed(logo, new Color(0f, 0f, 0f, 0.6f), 5f);

            Image underline = UIBuilder.Img("Rule", Root, TextureLab.White, UITheme.WithAlpha(UITheme.Ember, 0.75f));
            UIBuilder.Box(underline.rectTransform, 0f, -288f, 340f, 3f, new Vector2(0.5f, 1f));

            Text sub = UIBuilder.Label("Sub", Root, "THE ASHEN ROAD", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(sub.rectTransform, 0f, -312f, 700f, 32f, new Vector2(0.5f, 1f));
        }

        private void BuildTeamShowcase()
        {
            RectTransform card = UIBuilder.Node("Squad", Root);
            UIBuilder.Box(card, 0f, -392f, 940f, 300f, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(24);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.Panel, 0.92f);
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 24, 2);

            Text heading = UIBuilder.Label("Heading", card, "ACTIVE BANNER", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(heading.rectTransform, 0f, -16f, 400f, 26f, new Vector2(0.5f, 1f));

            _teamRow = UIBuilder.Node("Row", card);
            UIBuilder.Box(_teamRow, 0f, -54f, 880f, 232f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_teamRow, 22f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        }

        private void BuildIdleCache()
        {
            RectTransform card = UIBuilder.Node("IdleCache", Root);
            UIBuilder.Box(card, 0f, -722f, 940f, 320f, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(24);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.Gold, 0.4f), 24, 2);

            Image glow = UIBuilder.Img("Glow", card, TextureLab.Glow(128, 2f), UITheme.WithAlpha(UITheme.Gold, 0.10f));
            UIBuilder.FillParent(glow.rectTransform, 0f);

            Text heading = UIBuilder.Label("Heading", card, "EMBER CACHE", UITheme.FontHeading, UITheme.Gold, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 30f, -22f, 500f, 42f, new Vector2(0f, 1f));

            _idleRate = UIBuilder.Label("Rate", card, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleRate.rectTransform, 30f, -66f, 620f, 26f, new Vector2(0f, 1f));

            _idleAmounts = UIBuilder.Label("Amounts", card, "", UITheme.FontHeading, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleAmounts.rectTransform, 30f, -108f, 620f, 46f, new Vector2(0f, 1f));

            _idleTimer = UIBuilder.Label("Timer", card, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleTimer.rectTransform, 30f, -160f, 620f, 32f, new Vector2(0f, 1f));

            _claimButton = UIBuilder.GoldButton("Claim", card, "COLLECT", UITheme.FontBody);
            UIBuilder.Box(_claimButton.Root, -30f, 32f, 300f, 116f, new Vector2(1f, 0f));
            _claimButton.OnClick(ClaimIdle);
        }

        private void BuildChapterProgress()
        {
            RectTransform card = UIBuilder.Node("Progress", Root);
            UIBuilder.Box(card, 0f, 470f, 940f, 150f, new Vector2(0.5f, 0f));

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
        }

        private void BuildContinue()
        {
            _continueButton = UIBuilder.PrimaryButton("Continue", Root, "CONTINUE CAMPAIGN", UITheme.FontHeading);
            UIBuilder.Box(_continueButton.Root, 0f, 286f, 940f, 168f, new Vector2(0.5f, 0f));
            _continueButton.OnClick(OpenCampaign);

            _continueSub = UIBuilder.Label("ContinueSub", Root, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_continueSub.rectTransform, 0f, 244f, 900f, 34f, new Vector2(0.5f, 0f));
        }

        private void BuildNav()
        {
            RectTransform nav = UIBuilder.Node("Nav", Root);
            UIBuilder.BottomStrip(nav, 200f, 30f, UITheme.ScreenPadding);
            UIBuilder.HStack(nav, 20f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            AddNavButton(nav, "HEROES", 0, UITheme.Shard, OpenHeroes);
            AddNavButton(nav, "CAMPAIGN", 4, UITheme.Ember, OpenCampaign);
            AddNavButton(nav, "SUMMON", 13, UITheme.RarityColor(Rarity.Epic), OpenSummon);
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

        private void BuildOptions()
        {
            UIButton options = UIBuilder.IconButton("Options", Root, "...", 62f, UITheme.WithAlpha(UITheme.PanelRaised, 0.9f), UITheme.TextMuted);
            UIBuilder.Box(options.Root, -UITheme.ScreenPadding, -150f, 62f, 62f, new Vector2(1f, 1f));
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

            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int slot = 0; slot < 3; slot++)
            {
                RectTransform cell = UIBuilder.Node("Slot" + slot, _teamRow);
                UIBuilder.Sized(cell, 270f, 196f);

                if (slot >= team.Count)
                {
                    Image empty = cell.gameObject.AddComponent<Image>();
                    empty.sprite = TextureLab.RoundedRect(18);
                    empty.type = Image.Type.Sliced;
                    empty.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.8f);
                    Text hint = UIBuilder.Label("Hint", cell, "EMPTY", UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleCenter);
                    UIBuilder.FillParent(hint.rectTransform, 0f);
                    continue;
                }

                HeroInstance hero = team[slot];
                RectTransform portrait = Widgets.Portrait("Portrait", cell, hero.Def, hero.Def.Rarity, 122f, true);
                UIBuilder.Box(portrait, 0f, -4f, 122f, 122f, new Vector2(0.5f, 1f));

                Text name = UIBuilder.Label("Name", cell, hero.Def.Name, UITheme.FontTiny, UITheme.TextPrimary, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -132f, 260f, 26f, new Vector2(0.5f, 1f));

                Text meta = UIBuilder.Label("Meta", cell, "Lv " + hero.Level + "  -  " + hero.Power,
                                            UITheme.FontTiny, UITheme.Gold, TextAnchor.UpperCenter);
                UIBuilder.Box(meta.rectTransform, 0f, -160f, 260f, 26f, new Vector2(0.5f, 1f));
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
