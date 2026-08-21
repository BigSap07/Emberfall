using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI.Screens
{
    /// <summary>Chapter map. Pick a stage, check the squad, go.</summary>
    public sealed class CampaignScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Campaign; } }

        private Text _headerSubtitle;
        private RectTransform _teamStrip;
        private Text _teamPower;
        private RectTransform _stageList;
        private ScrollRect _scroll;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "CAMPAIGN", Router, out _headerSubtitle);
            BuildTeamStrip();

            _stageList = UIBuilder.ScrollList("Stages", Root, out _scroll, 14f, new RectOffset(0, 0, 6, 48));
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 292f, UITheme.ScreenPadding, 24f);
        }

        private void BuildTeamStrip()
        {
            RectTransform card = UIBuilder.Node("Team", Root);
            UIBuilder.TopStrip(card, 146f, UITheme.TopBarHeight + 132f, UITheme.ScreenPadding);

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(20);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.Panel, 0.94f);
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 20, 2);

            Button open = card.gameObject.AddComponent<Button>();
            open.targetGraphic = background;
            open.onClick.AddListener(delegate
            {
                App.Audio.Tap();
                Router.Show(ScreenId.Heroes);
            });

            Text label = UIBuilder.Label("Label", card, "BANNER", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 22f, -12f, 300f, 24f, new Vector2(0f, 1f));

            _teamPower = UIBuilder.Label("Power", card, "", UITheme.FontBody, UITheme.Gold, TextAnchor.UpperRight);
            UIBuilder.Box(_teamPower.rectTransform, -22f, -14f, 360f, 34f, new Vector2(1f, 1f));

            Text hint = UIBuilder.Label("Hint", card, "TAP TO CHANGE", UITheme.FontTiny, UITheme.TextDim, TextAnchor.LowerRight);
            UIBuilder.Box(hint.rectTransform, -22f, 12f, 300f, 24f, new Vector2(1f, 0f));

            _teamStrip = UIBuilder.Node("Portraits", card);
            UIBuilder.Box(_teamStrip, 22f, 16f, 460f, 92f, new Vector2(0f, 0f));
            UIBuilder.HStack(_teamStrip, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
        }

        public override void OnShow()
        {
            App.RefreshHud();
            RefreshTeam();
            RebuildStages();
        }

        private void RefreshTeam()
        {
            for (int i = _teamStrip.childCount - 1; i >= 0; i--) Destroy(_teamStrip.GetChild(i).gameObject);

            // Five portraits in the 460f strip: 5 * 80 + 4 * 12 spacing = 448. They were 84f at three.
            const float portraitSize = 80f;
            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int slot = 0; slot < PlayerProfile.TeamSize; slot++)
            {
                if (slot < team.Count)
                {
                    HeroInstance hero = team[slot];
                    RectTransform portrait = Widgets.Portrait("P" + slot, _teamStrip, hero.Def, hero.Def.Rarity, portraitSize, true);
                    UIBuilder.Sized(portrait, portraitSize, portraitSize);
                }
                else
                {
                    RectTransform empty = UIBuilder.Node("Empty" + slot, _teamStrip);
                    UIBuilder.Sized(empty, portraitSize, portraitSize);
                    Image image = empty.gameObject.AddComponent<Image>();
                    image.sprite = TextureLab.Ring(96, 5);
                    image.color = UITheme.BorderSoft;
                }
            }

            _teamPower.text = "POWER " + App.Session.Profile.TeamPower();
        }

        private void RebuildStages()
        {
            for (int i = _stageList.childCount - 1; i >= 0; i--) Destroy(_stageList.GetChild(i).gameObject);

            List<ChapterDef> chapters = App.Session.Content.Chapters;
            for (int c = 0; c < chapters.Count; c++)
            {
                ChapterDef chapter = chapters[c];
                if (c == 0) _headerSubtitle.text = chapter.Name.ToUpperInvariant() + "  -  " + chapter.Subtitle;

                BuildChapterHeading(chapter);
                for (int s = 0; s < chapter.Stages.Count; s++) BuildStageRow(chapter.Stages[s]);
            }
        }

        private void BuildChapterHeading(ChapterDef chapter)
        {
            RectTransform row = UIBuilder.Node("Chapter" + chapter.Number, _stageList);
            UIBuilder.Sized(row, 0f, 76f);

            Text title = UIBuilder.Label("Title", row, "CHAPTER " + chapter.Number + "  -  " + chapter.Name.ToUpperInvariant(),
                                         UITheme.FontSmall, UITheme.Ember, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(title.rectTransform, 10f, 0f, 0f, 0f);

            int cleared = 0;
            for (int i = 0; i < chapter.Stages.Count; i++)
                if (App.Session.Profile.HasClearedStage(chapter.Stages[i].Id)) cleared++;

            Text progress = UIBuilder.Label("Progress", row, cleared + " / " + chapter.Stages.Count,
                                            UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleRight);
            UIBuilder.FillParent(progress.rectTransform, 0f, 0f, 10f, 0f);
        }

        private void BuildStageRow(StageDef stage)
        {
            bool unlocked = App.Session.IsStageUnlocked(stage);
            bool cleared = App.Session.Profile.HasClearedStage(stage.Id);
            bool isNext = unlocked && !cleared;

            RectTransform row = UIBuilder.Node(stage.Id, _stageList);
            UIBuilder.Sized(row, 0f, 186f);

            Image background = row.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(20);
            background.type = Image.Type.Sliced;
            background.color = unlocked ? UITheme.Panel : UITheme.WithAlpha(UITheme.PanelSunken, 0.75f);

            Color edge = isNext ? UITheme.Ember : (cleared ? UITheme.WithAlpha(UITheme.Success, 0.5f) : UITheme.BorderSoft);
            UIBuilder.Frame("Edge", row, edge, 20, isNext ? 3 : 2);

            if (isNext)
            {
                Image glow = UIBuilder.Img("Glow", row, TextureLab.Glow(128, 2f), UITheme.WithAlpha(UITheme.Ember, 0.13f));
                UIBuilder.FillParent(glow.rectTransform, 0f);
            }

            // stage code badge
            RectTransform badge = UIBuilder.Node("Badge", row);
            UIBuilder.Box(badge, 22f, 0f, 96f, 96f, new Vector2(0f, 0.5f));
            Image badgeFill = badge.gameObject.AddComponent<Image>();
            badgeFill.sprite = TextureLab.RoundedRect(16);
            badgeFill.type = Image.Type.Sliced;
            badgeFill.color = unlocked ? UITheme.PanelSunken : UITheme.WithAlpha(UITheme.PanelSunken, 0.6f);
            badgeFill.raycastTarget = false;
            UIBuilder.Frame("Edge", badge, UITheme.WithAlpha(edge, 0.8f), 16, 2);

            Text code = UIBuilder.Label("Code", badge, stage.DisplayCode, UITheme.FontHeading,
                                        unlocked ? UITheme.TextPrimary : UITheme.TextDim, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(code.rectTransform, 0f);

            // name + subtitle
            Text name = UIBuilder.Label("Name", row, stage.Name + (stage.IsBoss ? "   [BOSS]" : ""), UITheme.FontBody,
                                        unlocked ? UITheme.TextPrimary : UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 138f, -22f, 620f, 34f, new Vector2(0f, 1f));

            Text subtitle = UIBuilder.Label("Sub", row, stage.Subtitle, UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(subtitle.rectTransform, 138f, -58f, 640f, 26f, new Vector2(0f, 1f));

            // enemy preview
            RectTransform pips = UIBuilder.Node("Enemies", row);
            UIBuilder.Box(pips, 138f, 20f, 300f, 62f, new Vector2(0f, 0f));
            UIBuilder.HStack(pips, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                EnemyDef def = App.Session.Content.GetEnemy(stage.Enemies[i].EnemyId);
                if (def == null) continue;
                Widgets.EnemyPip("E" + i, pips, def, stage.Enemies[i].Level, 58f);
            }

            // rewards
            Text rewards = UIBuilder.Label("Rewards", row, stage.RewardGold + "G   " + stage.RewardShards + "S",
                                           UITheme.FontTiny, UITheme.Gold, TextAnchor.LowerRight);
            UIBuilder.Box(rewards.rectTransform, -24f, 24f, 300f, 28f, new Vector2(1f, 0f));

            Text power = UIBuilder.Label("Power", row, "REC. POWER " + stage.RecommendedPower,
                                         UITheme.FontTiny, UITheme.TextDim, TextAnchor.LowerRight);
            UIBuilder.Box(power.rectTransform, -24f, 54f, 340f, 26f, new Vector2(1f, 0f));

            // state marker / action
            if (!unlocked)
            {
                Image hatch = UIBuilder.Img("Locked", row, TextureLab.Hatch(32, 10), new Color(0f, 0f, 0f, 0.22f));
                UIBuilder.FillParent(hatch.rectTransform, 2f);
                Text lockLabel = UIBuilder.Label("LockText", row, "LOCKED", UITheme.FontSmall, UITheme.TextDim, TextAnchor.UpperRight);
                UIBuilder.Box(lockLabel.rectTransform, -24f, -22f, 220f, 30f, new Vector2(1f, 1f));
                return;
            }

            if (cleared)
            {
                RectTransform tick = UIBuilder.Chip("Cleared", row, "CLEARED", UITheme.Success, UITheme.FontTiny);
                UIBuilder.Box(tick, -24f, -20f, 180f, 34f, new Vector2(1f, 1f));
            }
            else
            {
                RectTransform marker = UIBuilder.Chip("Next", row, "NEXT", UITheme.Ember, UITheme.FontTiny);
                UIBuilder.Box(marker, -24f, -20f, 140f, 34f, new Vector2(1f, 1f));
            }

            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            row.gameObject.AddComponent<ButtonPress>();
            StageDef captured = stage;
            button.onClick.AddListener(delegate { Launch(captured); });
        }

        private void Launch(StageDef stage)
        {
            if (App.Session.Profile.TeamHeroes().Count == 0)
            {
                App.Notify("Deploy at least one hero first.");
                return;
            }
            App.Audio.Tap();
            Router.Battle.SetStage(stage);
            Router.Show(ScreenId.Battle);
        }
    }
}
