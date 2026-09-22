using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The campaign, as the reviewed "squad diorama" mockup: a painted backdrop, a chapter chip
    /// row, the banner standing in the middle of the frame, and one flat tray along the bottom
    /// carrying the stage stepper between Setup Team and Enter Battle.
    ///
    /// WHAT THIS REPLACED, and why the screen got smaller rather than bigger: a vertically
    /// scrolling list of 186px stage cards, one per stage, each repeating the stage's name,
    /// subtitle, enemy pips, rewards and recommended power. That was a reasonable shape for 24
    /// stages and a bad one for 144 - the list was already the tallest thing in the game and the
    /// wave restructure multiplied what sits behind each row by 144. The stepper shows one stage
    /// at a time because there is now far more to say about one stage than a row can hold.
    ///
    /// PRESENTATION ONLY. Launch and Sweep below are untouched: the sweep rule, the repeat cap and
    /// everything the wave structure landed still resolve exactly as they did, and this screen
    /// still never simulates anything itself.
    /// </summary>
    public sealed class CampaignScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Campaign; } }


        private const float TrayHeight = 168f;
        private const float PortraitSize = 132f;
        private const float PortraitGap = 28f;

        private Text _headerSubtitle;
        private RectTransform _chipRow;
        private RectTransform _diorama;
        private RectTransform _stepper;
        private Text _stageCode;
        private Text _stageName;
        private Text _aspectName;
        private Image _aspectDot;
        private Text _squadPower;
        private UIButton _enter;

        /// <summary>Which chapter's stages the stepper is showing, and which of them is selected.
        /// Both are view state and deliberately not persisted - the screen resolves them from
        /// campaign progress every time it opens, so it always lands on the fight the player is
        /// actually up to rather than wherever they last poked.</summary>
        private int _chapterIndex;
        private int _stageIndex;

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            BuildBackdrop();
            ScreenChrome.ForgedHeader(Root, "CAMPAIGN", Router, out _headerSubtitle);
            BuildChipRow();
            BuildStageLabel();
            BuildSquadPower();
            BuildDiorama();
            BuildTray();
        }

        /// <summary>
        /// One static painting behind everything, loaded the way MainMenuScreen loads hub.png -
        /// same Resources.Load, same Sprite.Create, same EnvelopeParent fitter off the authored
        /// dimensions. Deliberately NOT keyed by environment id: this is one image for the whole
        /// campaign, so it needs none of BattleAssets' per-environment lookup or its generated
        /// fallback, and pointing the Hub's own path at a second filename is the entire change.
        ///
        /// A missing file leaves the screen on the flat theme backdrop rather than throwing, which
        /// is the same degradation the Hub takes.
        /// </summary>
        private void BuildBackdrop()
        {
            Texture2D painting = Resources.Load<Texture2D>("Backgrounds/campaign");
            if (painting == null) return;

            Sprite sprite = Sprite.Create(painting, new Rect(0f, 0f, painting.width, painting.height),
                                          new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);

            RectTransform frame = UIBuilder.Node("Backdrop", Root);
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;

            Image art = UIBuilder.Img("Art", frame, sprite, Color.white);
            art.raycastTarget = false;
            UIBuilder.FillParent(art.rectTransform, 0f);

            AspectRatioFitter fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            // From the sprite's own rect. This was two hand-declared constants, because the
            // importer used to rescale a non-power-of-two texture per axis and hand back a
            // different shape than the file. Every painting in Assets/Resources/Backgrounds now
            // carries nPOTScale 0, so the imported texture is the authored size - same change,
            // same reason, as the Hub's map.
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

            // A readability wash, not a style choice. The painting is a lit diorama and the text
            // that sits on it - stage name, aspect, squad power - is small and light. Without this
            // the top-left label competes with whatever the art happens to put behind it.
            Image scrim = UIBuilder.Img("Scrim", Root, TextureLab.White,
                                        UITheme.WithAlpha(Emberforge.Outline, 0.36f));
            scrim.raycastTarget = false;
            UIBuilder.FillParent(scrim.rectTransform, 0f);
        }

        /// <summary>
        /// One chip per chapter. Count comes from chapter.Stages.Count, never from a constant -
        /// there is no stages-per-chapter number written anywhere in this file, so a chapter of a
        /// different length renders correctly without being told.
        /// </summary>
        private void BuildChipRow()
        {
            _chipRow = UIBuilder.Node("Chapters", Root);
            UIBuilder.Box(_chipRow, 0f, -(ScreenChrome.HeaderTop + 104f), 1600f, 84f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_chipRow, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        }

        private void BuildStageLabel()
        {
            RectTransform block = UIBuilder.Node("StageLabel", Root);
            UIBuilder.Box(block, UITheme.ScreenPadding, -(ScreenChrome.HeaderTop + 210f), 620f, 120f, new Vector2(0f, 1f));

            _stageCode = EmberforgeUI.Label("Code", block, "", UITheme.FontSmall, Emberforge.TextGold,
                                            TextAnchor.UpperLeft);
            UIBuilder.Box(_stageCode.rectTransform, 0f, 0f, 620f, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_stageCode, Emberforge.Outline, Emberforge.StrokeThin);

            // OUTLINED rather than shadowed, and that is the rule for every label on this screen:
            // these three sit directly on a painting rather than on a plate, and a drop shadow
            // only separates text from whatever happens to be BELOW it. A stroke all the way
            // round is what keeps a light word readable over a lit horizon.
            _stageName = EmberforgeUI.Title("Name", block, "", UITheme.FontHeading, Emberforge.TextOnColor,
                                            TextAnchor.UpperLeft);
            UIBuilder.Box(_stageName.rectTransform, 0f, -32f, 620f, 46f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_stageName, Emberforge.Outline, Emberforge.StrokeThick);

            // Dot plus name, both driven by UITheme.AspectColor - the palette every other screen
            // reads. No colour is defined in this file.
            _aspectDot = UIBuilder.Img("Dot", block, TextureLab.Circle(64), UITheme.TextDim);
            UIBuilder.Box(_aspectDot.rectTransform, 2f, -88f, 18f, 18f, new Vector2(0f, 1f));

            _aspectName = EmberforgeUI.Label("Aspect", block, "", UITheme.FontTiny, Emberforge.TextBody,
                                             TextAnchor.UpperLeft);
            UIBuilder.Box(_aspectName.rectTransform, 30f, -86f, 560f, 26f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_aspectName, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void BuildSquadPower()
        {
            RectTransform block = UIBuilder.Node("SquadPower", Root);
            UIBuilder.Box(block, -UITheme.ScreenPadding, -(ScreenChrome.HeaderTop + 210f), 420f, 90f, new Vector2(1f, 1f));

            Text caption = EmberforgeUI.Label("Caption", block, "YOUR SQUAD", UITheme.FontTiny,
                                              Emberforge.TextBody, TextAnchor.UpperRight);
            UIBuilder.Box(caption.rectTransform, 0f, 0f, 420f, 26f, new Vector2(1f, 1f));
            UIBuilder.Outlined(caption, Emberforge.Outline, Emberforge.StrokeThin);

            _squadPower = EmberforgeUI.Title("Power", block, "", UITheme.FontHeading,
                                             Emberforge.TextGold, TextAnchor.UpperRight);
            UIBuilder.Box(_squadPower.rectTransform, 0f, -28f, 420f, 46f, new Vector2(1f, 1f));
            UIBuilder.Outlined(_squadPower, Emberforge.Outline, Emberforge.StrokeThick);
        }

        private void BuildDiorama()
        {
            _diorama = UIBuilder.Node("Diorama", Root);
            float width = PlayerProfile.TeamSize * PortraitSize + (PlayerProfile.TeamSize - 1) * PortraitGap;
            UIBuilder.Box(_diorama, 0f, -40f, width, PortraitSize + 40f, new Vector2(0.5f, 0.5f));
            UIBuilder.HStack(_diorama, PortraitGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        }

        private void BuildTray()
        {
            RectTransform tray = UIBuilder.Node("Tray", Root);
            UIBuilder.Box(tray, 0f, UITheme.ScreenPadding, 1780f, TrayHeight, new Vector2(0.5f, 0f));

            EmberforgeUI.PanelOn(tray, true);

            UIButton setup = EmberforgeUI.Button("SetupTeam", tray, "SETUP TEAM", UITheme.FontBody,
                                                 ForgeButtonKind.Secondary);
            UIBuilder.Box(setup.Root, 28f, 0f, 320f, 104f, new Vector2(0f, 0.5f));
            setup.OnClick(delegate
            {
                App.Audio.Tap();
                Router.Show(ScreenId.Formation);
            });

            _enter = EmberforgeUI.Button("EnterBattle", tray, "ENTER BATTLE", UITheme.FontBody,
                                         ForgeButtonKind.Primary);
            UIBuilder.Box(_enter.Root, -28f, 0f, 340f, 104f, new Vector2(1f, 0.5f));
            _enter.OnClick(delegate { EnterBattle(); });

            _stepper = UIBuilder.Node("Stepper", tray);
            UIBuilder.Box(_stepper, 0f, 0f, 1000f, 104f, new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            ResolveSelection();
            RefreshChips();
            RefreshDiorama();
            RefreshStepper();
            RefreshStageLabel();
        }

        /// <summary>
        /// Opens on the stage the player is actually up to: the first uncleared stage, or the last
        /// one when the campaign is finished. Derived rather than remembered, so the screen cannot
        /// open on a stale selection after a clear.
        /// </summary>
        private void ResolveSelection()
        {
            List<ChapterDef> chapters = App.Session.Content.Chapters;
            if (chapters.Count == 0) return;

            _chapterIndex = 0;
            _stageIndex = 0;
            for (int c = 0; c < chapters.Count; c++)
            {
                for (int s = 0; s < chapters[c].Stages.Count; s++)
                {
                    if (App.Session.Profile.HasClearedStage(chapters[c].Stages[s].Id)) continue;
                    _chapterIndex = c;
                    _stageIndex = s;
                    return;
                }
            }
            _chapterIndex = chapters.Count - 1;
            _stageIndex = Mathf.Max(0, chapters[_chapterIndex].Stages.Count - 1);
        }

        private ChapterDef ActiveChapter
        {
            get
            {
                List<ChapterDef> chapters = App.Session.Content.Chapters;
                if (chapters.Count == 0) return null;
                return chapters[Mathf.Clamp(_chapterIndex, 0, chapters.Count - 1)];
            }
        }

        private StageDef SelectedStage
        {
            get
            {
                ChapterDef ch = ActiveChapter;
                if (ch == null || ch.Stages.Count == 0) return null;
                return ch.Stages[Mathf.Clamp(_stageIndex, 0, ch.Stages.Count - 1)];
            }
        }

        private void RefreshChips()
        {
            for (int i = _chipRow.childCount - 1; i >= 0; i--) Destroy(_chipRow.GetChild(i).gameObject);

            List<ChapterDef> chapters = App.Session.Content.Chapters;
            for (int c = 0; c < chapters.Count; c++)
            {
                ChapterDef chapter = chapters[c];
                bool active = c == _chapterIndex;

                // A TAB, not a chip. One chapter of several, exactly one of them open, is what a
                // tab bar is for, and the selected state is a different plate rather than an
                // alpha on one - which matters on a screen whose background is a PAINTING, where
                // a translucent plate reads as whatever happens to be behind it.
                int captured = c;
                ForgeTab tab = EmberforgeUI.Tab(chapter.Name, _chipRow, 300f, 84f,
                                                delegate { SelectChapter(captured); });
                UIBuilder.Sized(tab.Root, 300f, 84f);
                tab.Caption.gameObject.SetActive(false);
                EmberforgeUI.SetTabSelected(tab, active);

                Text name = EmberforgeUI.Title("Name", tab.Root, chapter.Name.ToUpperInvariant(),
                                               UITheme.FontSmall,
                                               active ? Emberforge.TextGold : Emberforge.TextQuiet,
                                               TextAnchor.UpperCenter);
                // -20, not -14. The tab plate spends its top five pixels on an outline and a
                // gold frame, and Cinzel's capitals reach the top of their box, so at -14 the
                // heads of the letters were printed into the frame.
                UIBuilder.Box(name.rectTransform, 0f, -20f, 280f, 32f, new Vector2(0.5f, 1f));
                UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

                int cleared = 0;
                for (int s = 0; s < chapter.Stages.Count; s++)
                    if (App.Session.Profile.HasClearedStage(chapter.Stages[s].Id)) cleared++;

                Text count = EmberforgeUI.Label("Count", tab.Root,
                                                cleared + " / " + chapter.Stages.Count + " CLEARED",
                                                UITheme.FontTiny,
                                                active ? Emberforge.TextBody : Emberforge.TextQuiet,
                                                TextAnchor.UpperCenter);
                UIBuilder.Box(count.rectTransform, 0f, -52f, 280f, 24f, new Vector2(0.5f, 1f));
                UIBuilder.Outlined(count, Emberforge.Outline, Emberforge.StrokeThin);
            }
        }

        private void SelectChapter(int index)
        {
            if (index == _chapterIndex) return;
            App.Audio.Tap();
            _chapterIndex = index;
            _stageIndex = 0;
            RefreshChips();
            RefreshStepper();
            RefreshStageLabel();
        }

        /// <summary>
        /// The banner as it will walk into the fight: one circle per team slot, always
        /// PlayerProfile.TeamSize of them so the row's shape does not change with how many heroes
        /// are deployed. A filled slot is the same Widgets.Portrait every other screen draws, so
        /// framed badges and sigil fallbacks both arrive here already correct and this screen asks
        /// for no art of its own.
        /// </summary>
        private void RefreshDiorama()
        {
            for (int i = _diorama.childCount - 1; i >= 0; i--) Destroy(_diorama.GetChild(i).gameObject);

            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int slot = 0; slot < PlayerProfile.TeamSize; slot++)
            {
                RectTransform cell = UIBuilder.Node("Slot" + slot, _diorama);
                UIBuilder.Sized(cell, PortraitSize, PortraitSize);

                if (slot < team.Count)
                {
                    // Drop shadow first so it sits under everything, offset down the way a lit
                    // diorama would throw it.
                    Image shadow = UIBuilder.Img("Shadow", cell, TextureLab.Glow(128, 1.6f), new Color(0f, 0f, 0f, 0.55f));
                    shadow.raycastTarget = false;
                    UIBuilder.Box(shadow.rectTransform, 0f, -10f, PortraitSize + 18f, PortraitSize + 18f, new Vector2(0.5f, 0.5f));

                    // NO GOLD RING. A drawn badge arrives with its own gold frame and four spikes
                    // reaching the edge of the image, so a ring around one is a second frame with
                    // the spikes clipped against it. PortraitFrame puts the tier on a plate BEHIND
                    // the badge instead, which is a reading the ring did not carry at all.
                    HeroInstance hero = team[slot];
                    RectTransform portrait = EmberforgeUI.PortraitFrame("P" + slot, cell, hero.Def,
                                                                        hero.Def.Stars, PortraitSize);
                    UIBuilder.Box(portrait, 0f, 0f, PortraitSize, PortraitSize, new Vector2(0.5f, 0.5f));
                }
                else
                {
                    Image dashed = UIBuilder.Img("Empty", cell, DashedRing(160, 6, 14),
                                                 UITheme.WithAlpha(Emberforge.GoldDeep, 0.85f));
                    dashed.raycastTarget = false;
                    UIBuilder.Box(dashed.rectTransform, 0f, 0f, PortraitSize, PortraitSize, new Vector2(0.5f, 0.5f));
                }
            }

            _squadPower.text = App.Session.Profile.TeamPower().ToString();
        }

        /// <summary>
        /// One stepper chevron: the kit's rail disc with a Cinzel glyph on it, inside a full-size
        /// hit rect. The glyph is text rather than a sprite for the reason ScreenChrome's back
        /// chevron is - there is no arrow in the generator, and adding one for two call sites
        /// would put a shape in the shared kit that nothing else asks for.
        /// </summary>
        private UIButton Chevron(string name, string glyph, Vector2 anchor)
        {
            ForgeDisc disc = EmberforgeUI.IconButton(name, _stepper, "", TextureLab.Sigil(0, 64),
                                                     Emberforge.TextGold, 64f, null);
            UIBuilder.Box(disc.Root, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, anchor);
            disc.Glyph.enabled = false;

            Text mark = EmberforgeUI.Title("Glyph", disc.Face, glyph, UITheme.FontBody,
                                           Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(mark.rectTransform, 0f);
            UIBuilder.Outlined(mark, Emberforge.Outline, Emberforge.StrokeThin);
            mark.raycastTarget = false;
            return disc.Button;
        }

        private void RefreshStageLabel()
        {
            StageDef stage = SelectedStage;
            ChapterDef chapter = ActiveChapter;
            // The chapter NAME is on the chip directly below this line, so the subtitle carries only
            // the flavour text. It used to lead with the name and read as a stutter on screen.
            if (chapter != null) _headerSubtitle.text = chapter.Subtitle;

            if (stage == null)
            {
                _stageCode.text = "";
                _stageName.text = "";
                _aspectName.text = "";
                return;
            }

            _stageCode.text = "STAGE " + stage.DisplayCode;
            _stageName.text = stage.Name.ToUpperInvariant() + (stage.IsBoss ? "   [BOSS]" : "");

            // The aspect the stage's line is built from, read off its own roster rather than stored
            // anywhere: the most common aspect among its enemies, which is what the player will
            // actually be fighting into.
            Aspect aspect;
            if (TryDominantAspect(stage, out aspect))
            {
                _aspectDot.color = UITheme.AspectColor(aspect);
                _aspectName.text = aspect.ToString().ToUpperInvariant() + " ENEMIES";
            }
            else
            {
                _aspectDot.color = UITheme.TextDim;
                _aspectName.text = "";
            }

            bool unlocked = App.Session.IsStageUnlocked(stage);
            bool cleared = App.Session.Profile.HasClearedStage(stage.Id);
            _enter.SetText(!unlocked ? "LOCKED" : (cleared ? "SWEEP STAGE" : "ENTER BATTLE"));
            _enter.Interactable = unlocked;
        }

        private bool TryDominantAspect(StageDef stage, out Aspect aspect)
        {
            aspect = Aspect.Ember;
            if (stage == null || stage.Enemies.Count == 0) return false;

            Dictionary<Aspect, int> counts = new Dictionary<Aspect, int>();
            int best = 0;
            bool found = false;
            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                EnemyDef def = App.Session.Content.GetEnemy(stage.Enemies[i].EnemyId);
                if (def == null) continue;
                int n;
                counts.TryGetValue(def.Aspect, out n);
                n++;
                counts[def.Aspect] = n;
                if (n > best) { best = n; aspect = def.Aspect; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Chevron, numbered circles, chevron. The current stage is gold and larger; a stage the
        /// player has not unlocked shows a padlock instead of its number, so "locked" is a shape
        /// rather than a shade - a dimmed number reads as a rendering fault, a padlock does not.
        /// </summary>
        private void RefreshStepper()
        {
            for (int i = _stepper.childCount - 1; i >= 0; i--) Destroy(_stepper.GetChild(i).gameObject);

            ChapterDef chapter = ActiveChapter;
            if (chapter == null) return;
            int count = chapter.Stages.Count;

            UIButton prev = Chevron("Prev", "<", new Vector2(0f, 0.5f));
            prev.Interactable = _stageIndex > 0;
            prev.OnClick(delegate { StepStage(-1); });

            UIButton next = Chevron("Next", ">", new Vector2(1f, 0.5f));
            next.Interactable = _stageIndex < count - 1;
            next.OnClick(delegate { StepStage(1); });

            RectTransform strip = UIBuilder.Node("Pips", _stepper);
            UIBuilder.Box(strip, 0f, 0f, 840f, 76f, new Vector2(0.5f, 0.5f));
            UIBuilder.HStack(strip, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            for (int s = 0; s < count; s++)
            {
                StageDef stage = chapter.Stages[s];
                bool current = s == _stageIndex;
                bool unlocked = App.Session.IsStageUnlocked(stage);
                bool cleared = App.Session.Profile.HasClearedStage(stage.Id);
                float size = current ? 68f : 52f;

                RectTransform pip = UIBuilder.Node("Pip" + s, strip);
                UIBuilder.Sized(pip, size, size);

                // The disc is a fixed SQUARE child rather than filling the cell, because HStack
                // sets childForceExpandHeight and stretches every cell to the strip's full height.
                // Filling that gave 52x76 ellipses instead of circles - visible only once the pips
                // were drawn and zoomed, not from reading the layout call.
                RectTransform art = UIBuilder.Node("Art", pip);
                UIBuilder.Box(art, 0f, 0f, size, size, new Vector2(0.5f, 0.5f));

                // THE KIT'S DISC, in its two depths. The stage you are on stands proud - the
                // gold-framed disc with its lit top - and every other stage is the same disc
                // pressed, which is a recess. Depth says "here" where a tint used to, and a tint
                // on a baked bevel darkens its highlight and its shadow by the same factor, so a
                // strip of twelve of them was twelve exposures of one object.
                Image disc = UIBuilder.Img("Disc", art,
                                           EmberforgeSkin.Get(current ? EmberforgeArt.Slot.Disc
                                                                      : EmberforgeArt.Slot.DiscDown),
                                           Color.white);
                UIBuilder.FillParent(disc.rectTransform, 0f);

                if (unlocked)
                {
                    Text number = EmberforgeUI.Title("N", art, (s + 1).ToString(),
                                                     current ? UITheme.FontBody : UITheme.FontSmall,
                                                     current ? Emberforge.TextGold
                                                             : (cleared ? UITheme.Success : Emberforge.TextBody),
                                                     TextAnchor.MiddleCenter);
                    UIBuilder.FillParent(number.rectTransform, 0f);
                    UIBuilder.Outlined(number, Emberforge.Outline, Emberforge.StrokeThin);
                }
                else
                {
                    // 0.62 of the pip, not 0.5: at 52px a half-size padlock is 26px, which holds its
                    // silhouette but reads as a smudge next to a full-size numeral. Locked has to be
                    // a shape you recognise, not just a cell with something small in it.
                    Image padlock = UIBuilder.Img("Lock", art, Padlock(64), Emberforge.TextQuiet);
                    padlock.raycastTarget = false;
                    UIBuilder.Box(padlock.rectTransform, 0f, 0f, size * 0.62f, size * 0.62f, new Vector2(0.5f, 0.5f));
                }

                Button button = pip.gameObject.AddComponent<Button>();
                button.targetGraphic = disc;
                int captured = s;
                button.onClick.AddListener(delegate { SelectStage(captured); });
            }
        }

        private void StepStage(int delta)
        {
            ChapterDef chapter = ActiveChapter;
            if (chapter == null) return;
            SelectStage(Mathf.Clamp(_stageIndex + delta, 0, chapter.Stages.Count - 1));
        }

        private void SelectStage(int index)
        {
            if (index == _stageIndex) return;
            App.Audio.Tap();
            _stageIndex = index;
            RefreshStepper();
            RefreshStageLabel();
        }

        // ------------------------------------------------------------------ procedural marks

        private static readonly Dictionary<string, Sprite> Marks = new Dictionary<string, Sprite>();

        /// <summary>
        /// An open slot: a ring broken into dashes. Drawn here rather than added to TextureLab
        /// because it is this screen's only caller; promote it if a second screen ever wants one.
        /// </summary>
        private static Sprite DashedRing(int size, int thickness, int dashes)
        {
            string key = "dash_" + size + "_" + thickness + "_" + dashes;
            Sprite cached;
            if (Marks.TryGetValue(key, out cached)) return cached;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] px = new Color32[size * size];
            float r = size * 0.5f;
            float segment = Mathf.PI * 2f / Mathf.Max(1, dashes);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.Clamp01(r - d) - Mathf.Clamp01(r - thickness - d);
                    if (band > 0f)
                    {
                        float a = Mathf.Atan2(dy, dx) + Mathf.PI;
                        float phase = (a % segment) / segment;
                        if (phase > 0.55f) band = 0f;   // the gap between dashes
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(band) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Marks[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A padlock: a solid body with a keyhole, under a shackle arch. Shape rather than shade is
        /// the point - see RefreshStepper.
        /// </summary>
        private static Sprite Padlock(int size)
        {
            string key = "lock_" + size;
            Sprite cached;
            if (Marks.TryGetValue(key, out cached)) return cached;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] px = new Color32[size * size];
            float s = size;
            float bodyLeft = s * 0.22f, bodyRight = s * 0.78f;
            float bodyTop = s * 0.52f, bodyBottom = s * 0.94f;
            float shackleCx = s * 0.5f, shackleCy = s * 0.46f;
            float shackleOuter = s * 0.22f, shackleInner = s * 0.13f;
            float keyCx = s * 0.5f, keyCy = s * 0.70f, keyR = s * 0.06f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture y runs bottom-up; flip so the shackle is drawn on top.
                    float fx = x + 0.5f;
                    float fy = size - (y + 0.5f);
                    float a = 0f;

                    if (fx >= bodyLeft && fx <= bodyRight && fy >= bodyTop && fy <= bodyBottom) a = 1f;

                    if (fy < shackleCy)
                    {
                        float dx = fx - shackleCx;
                        float dy = fy - shackleCy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= shackleOuter && d >= shackleInner) a = 1f;
                    }

                    float kx = fx - keyCx;
                    float ky = fy - keyCy;
                    if (Mathf.Sqrt(kx * kx + ky * ky) <= keyR) a = 0f;
                    if (fx >= keyCx - keyR * 0.5f && fx <= keyCx + keyR * 0.5f
                        && fy >= keyCy && fy <= keyCy + keyR * 2.2f) a = 0f;

                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Marks[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ actions

        private void EnterBattle()
        {
            StageDef stage = SelectedStage;
            if (stage == null) return;
            if (!App.Session.IsStageUnlocked(stage))
            {
                App.Notify("Clear the stage before it to unlock this one.");
                return;
            }
            Launch(stage);
        }

        private void Launch(StageDef stage)
        {
            if (App.Session.Profile.TeamHeroes().Count == 0)
            {
                App.Notify("Deploy at least one hero first.");
                return;
            }
            App.Audio.Tap();

            // A STAGE ALREADY CLEARED IS SWEPT, NOT REFOUGHT.
            //
            // DESIGN-CAMPAIGN-STRUCTURE.md: "a cleared stage becomes a one-tap sweep, resolved
            // instantly, not a full 144-battle real-time replay". Farming for gold cannot mean
            // sitting through 144 fights, so the replay path resolves here and never opens the
            // battle screen at all. It still spends a daily repeat, because a sweep IS a stage
            // clear and that system's granularity is unchanged.
            //
            // A stage never cleared falls through and is fought battle by battle.
            if (App.Session.CanSweep(stage))
            {
                Sweep(stage);
                return;
            }

            Router.Battle.SetStage(stage);
            Router.Show(ScreenId.Battle);
        }

        /// <summary>
        /// Resolves a repeat clear in one tap and reports it as a toast rather than a result
        /// screen, because there is no fight to narrate - the outcome is already known by the time
        /// the tap finishes.
        ///
        /// A sweep can fail. It runs the stage's hardest battle to decide (see
        /// GameSession.SweepClearedStage), so a line that has been stripped of gear since the
        /// original clear is told so instead of being paid for a stage it can no longer beat.
        /// </summary>
        private void Sweep(StageDef stage)
        {
            int seed = Mathf.Abs(stage.Id.GetHashCode() * 31
                                 + App.Session.Profile.TotalBattlesWon * 7919
                                 + Mathf.RoundToInt(Time.unscaledTime * 997f));
            if (seed == 0) seed = 1;

            BattleRewards rewards = App.Session.SweepClearedStage(stage, seed, TimeUtil.NowUnix());
            if (!rewards.Victory)
            {
                App.Notify("Your line cannot sweep " + stage.Name + " any more.");
                App.Audio.Defeat();
                return;
            }

            App.Audio.Victory();
            App.Notify(rewards.RepeatCapped
                ? "Swept " + stage.Name + ". Daily repeat cap reached, no spoils."
                : "Swept " + stage.Name + ".  +" + rewards.Gold + "g  +" + rewards.Shards + " shards");
            App.RefreshHud();
            OnShow();
        }
    }
}
