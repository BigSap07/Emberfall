using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE A2 HERO DETAIL SCREEN (R58): the hero page drawn to the A2 boards. A round back disc and the word HERO
    /// on the left of the header, the shared top bar slimmed to Gold and Shards on the right, then one cream panel
    /// in two columns. On the left one card carries the portrait, the name, the role and aspect on one line, the
    /// level and stars, the power as one big number and the slot line. On the right one well carries a tab rail
    /// (STATS, SKILLS, GEAR) with one tab lit. The bottom bar leads with ONE Primary.
    /// Built only while HeroDetailA2Switch is on; the page as it was before R58 is the other half of this partial
    /// class and is left untouched, and is what the switch gives back.
    ///
    /// NOTHING HERE DECIDES A RULE. Every number is read from StatMath, GameSession and AbilityMath exactly as the
    /// old page read it, and every action calls the same method it called before (LevelUp, OpenFusion, OpenTalents,
    /// OpenArmorSlot, CycleSkin, OpenFormation, GoToSummon). The words and the order of the buttons come from the
    /// pure HeroDetailPick helpers.
    ///
    /// THE SAME NODE NAMES. The onboarding coach finds Header/Back, LevelUp, Slot_*, Talents, Fusion, Obtain,
    /// Formation and Skin; the A2 build keeps every one of them, and keeps the persistent ones active.
    ///
    /// WHAT MOVED. The six equip slots and the attunement line live on the GEAR tab, the five abilities on SKILLS
    /// (a tile each, the description in a card under them), Effect Hit and Effect Resist on one More line under the
    /// four stat rows, the six aspect matchups under that. CHANGE BANNER and SKINS are two Quiet links under the
    /// portrait card. TALENTS is a Secondary at the foot of SKILLS.
    /// </summary>
    public sealed partial class HeroDetailScreen
    {
        // ------------------------------------------------------------------ constants

        private const float A2HeaderHeight = 108f;
        private const float A2HeaderTop = 12f;              // puts the header's middle on the pills' middle (TopBarHeight / 2)
        private const float A2BottomMargin = 32f;
        private const float A2PanelPad = 20f;
        private const float A2BarHeight = 112f;
        private const float A2BarGap = 12f;
        private const float A2ColumnGap = 16f;
        private const float A2LeftShare = 0.4f;
        private const float A2RailWidth = 176f;
        private const float A2TabWidth = 152f;
        private const float A2TabHeight = 72f;
        private const float A2TabGap = 12f;
        private const float A2PrimaryWidth = 520f;
        private const float A2PrimaryHeight = 96f;
        private const float A2SecondaryWidth = 340f;
        private const float A2SecondaryHeight = 80f;
        private const float A2TenWidth = 110f;
        private const float A2TenHeight = 64f;
        private const float A2ButtonGap = 12f;
        private const float A2LinkHeight = 56f;
        private const float A2FooterHeight = 60f;
        private const float A2PortraitBackHeight = 270f;
        private const float A2PortraitWindow = 242f;
        private const float A2StatPitch = 80f;
        private const float A2GearTile = 196f;
        private const float A2GearGap = 36f;
        private const float A2SkillTileHeight = 140f;

        /// <summary>The tint a hero not yet summoned wears in the portrait window (the same one the Heroes grid uses).</summary>
        private static readonly Color A2DetailLockedFace = new Color(0.78f, 0.74f, 0.72f, 0.9f);

        // ------------------------------------------------------------------ state

        /// <summary>Which half of this partial class built the screen (decided once, at Build).</summary>
        private bool _a2;

        /// <summary>The tab the right well shows (a HeroDetailPick tab). Reset to the start tab when the hero changes.</summary>
        private int _a2Tab = HeroDetailPick.TabStats;

        /// <summary>The ability tile that is selected on SKILLS (an index into the hero's skills).</summary>
        private int _a2Ability;

        /// <summary>The hero the tab state belongs to: an owned hero's uid, or "L:" and a locked hero's definition id.</summary>
        private string _a2HeroKey = "";

        private RectTransform _a2LeftHost;
        private RectTransform _a2Pane;
        private readonly RectTransform[] _a2TabRoot = new RectTransform[HeroDetailPick.TabCount];
        private readonly Image[] _a2TabFace = new Image[HeroDetailPick.TabCount];
        private readonly Text[] _a2TabText = new Text[HeroDetailPick.TabCount];

        private sealed class A2Skill
        {
            public AbilityDef Ability;
            public PassiveDef Passive;
            public string Kind;
            public Color Tint;
        }

        /// <summary>Flags the capture tooling and the harness read: this screen drew the A2 half.</summary>
        public bool IsA2 { get { return _a2; } }

        /// <summary>The shared top bar stays, slimmed to Gold and Shards (HudTopBar.SetSlim), only while this half is on.</summary>
        public override bool SlimTopBar { get { return _a2; } }

        // ------------------------------------------------------------------ build

        private void BuildA2()
        {
            _a2 = true;
            _a2HeroKey = "";

            A2Kit.Backdrop(Root);
            BuildA2Header();

            RectTransform main = UIBuilder.Node("Main", Root);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(UITheme.ScreenPadding, A2BottomMargin);
            main.offsetMax = new Vector2(-UITheme.ScreenPadding, -UITheme.TopBarHeight);
            Image face = A2Kit.ChunkPanel("Panel", main, 26);
            UIBuilder.FillParent(face.rectTransform.parent as RectTransform, 0f);

            RectTransform inner = UIBuilder.Node("Inner", main);
            UIBuilder.FillParent(inner, A2PanelPad);

            BuildA2Bar(inner);

            RectTransform body = UIBuilder.Node("Body", inner);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(0f, A2BarHeight + A2BarGap);
            body.offsetMax = Vector2.zero;
            BuildA2Left(body);
            BuildA2Right(body);

            _abilityCard = new AbilityCardSheet(Router.OverlayLayer, App.Layers, App.Audio.Tap);
        }

        private void BuildA2Header()
        {
            RectTransform header = UIBuilder.Node("Header", Root);
            UIBuilder.TopStrip(header, A2HeaderHeight, A2HeaderTop, UITheme.ScreenPadding);

            // Back: a cream round disc. The disc is smaller than the hit, so the hit stays a full touch target.
            RectTransform back = UIBuilder.Node("Back", header);
            UIBuilder.Box(back, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0f, 0.5f));
            Image disc = UIBuilder.Img("Disc", back, TextureLab.Circle(128), A2Kit.Cream);
            UIBuilder.FillParent(disc.rectTransform, 8f);
            disc.raycastTarget = true;
            Image edge = UIBuilder.Img("Edge", back, TextureLab.Ring(128, 5), A2Kit.Ink);
            UIBuilder.FillParent(edge.rectTransform, 8f);
            edge.raycastTarget = false;
            Text chevron = A2Kit.Label("Glyph", back, "<", 52, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.FillParent(chevron.rectTransform, 0f);
            Button button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            UIButton backHandle = new UIButton();
            backHandle.Root = back;
            backHandle.Button = button;
            backHandle.Background = disc;
            backHandle.Label = chevron;
            backHandle.OnClick(delegate { App.Audio.Tap(); Router.Back(); });

            Text title = A2Kit.Chunky("Title", header, "HERO", 56, A2Kit.TextOnDark, TextAnchor.MiddleLeft);
            UIBuilder.Outlined(title, A2Kit.InkDeep, 3f);
            UIBuilder.Box(title.rectTransform, HubStyle.TouchMin + 18f, 0f, 560f, A2HeaderHeight, new Vector2(0f, 0.5f));
        }

        /// <summary>
        /// The bottom bar: the hint line on the left, the buttons on the right. Four persistent buttons (LEVEL UP,
        /// x10, the ASCEND / CONVERT door and OPEN THE RIFT) that RefreshA2Bar shows, hides and dresses. At most one
        /// of them is a Primary at a time.
        /// </summary>
        private void BuildA2Bar(RectTransform inner)
        {
            RectTransform bar = UIBuilder.Node("Actions", inner);
            UIBuilder.BottomStrip(bar, A2BarHeight, 0f, 0f);

            _capNote = A2Kit.Label("CapNote", bar, "", 24, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            _capNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _capNote.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform noteRect = _capNote.rectTransform;
            noteRect.anchorMin = Vector2.zero;
            noteRect.anchorMax = Vector2.one;
            noteRect.offsetMin = new Vector2(8f, 6f);
            noteRect.offsetMax = new Vector2(-(A2PrimaryWidth + A2ButtonGap + A2TenWidth + A2ButtonGap + A2SecondaryWidth + 2f * A2ButtonGap), -6f);

            _levelUpButton = A2Kit.Button("LevelUp", bar, "LEVEL UP", 38, A2ButtonKind.Primary);
            _levelUpButton.OnClick(delegate { LevelUp(1); });

            _levelUpTenButton = A2Kit.Button("LevelUpTen", bar, "x10", 30, A2ButtonKind.Quiet);
            _levelUpTenButton.OnClick(delegate { LevelUp(10); });

            // The door the cap note points at. Quiet while LEVEL UP leads; the Primary the moment ASCEND is ready.
            _fusionButton = A2Kit.Button("Fusion", bar, "ASCEND", 30, A2ButtonKind.Quiet);
            _fusionButton.OnClick(OpenFusion);
            FeatureLockView.Attach(_fusionButton.Root, "herodetail.Fusion", _fusionButton.Label,
                                   new Vector2(-A2SecondaryWidth * 0.5f + 20f, 0f), FeatureLockView.TagSide.Above);

            _obtainButton = A2Kit.Button("Obtain", bar, "OPEN THE RIFT", 38, A2ButtonKind.Primary);
            UIBuilder.Box(_obtainButton.Root, 0f, 0f, A2PrimaryWidth, A2PrimaryHeight, new Vector2(1f, 0.5f));
            _obtainButton.OnClick(GoToSummon);
        }

        private void BuildA2Left(RectTransform body)
        {
            RectTransform left = UIBuilder.Node("Hero", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(A2LeftShare, 1f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(-A2ColumnGap * 0.5f, 0f);

            Image panel = UIBuilder.Panel("Card", left, A2Kit.Card, 22);
            panel.raycastTarget = false;
            UIBuilder.FillParent(panel.rectTransform, 0f);
            UIBuilder.Frame("Edge", panel.rectTransform, A2Kit.Ink, 22, 4);

            _a2LeftHost = UIBuilder.Node("Content", left);
            UIBuilder.FillParent(_a2LeftHost, 0f);

            // Two Quiet links at the foot of the card, each shown only when it does something today.
            _openFormation = A2Kit.Button("Formation", left, "CHANGE BANNER", 26, A2ButtonKind.Quiet);
            UIBuilder.Box(_openFormation.Root, 16f, 16f, 300f, A2LinkHeight, new Vector2(0f, 0f));
            _openFormation.OnClick(OpenFormation);

            _skinButton = A2Kit.Button("Skin", left, "SKINS", 26, A2ButtonKind.Quiet);
            UIBuilder.Box(_skinButton.Root, 16f + 300f + A2ButtonGap, 16f, 200f, A2LinkHeight, new Vector2(0f, 0f));
            _skinButton.OnClick(OnSkinsA2);
        }

        private void BuildA2Right(RectTransform body)
        {
            RectTransform right = UIBuilder.Node("Sheet", body);
            right.anchorMin = new Vector2(A2LeftShare, 0f);
            right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(A2ColumnGap * 0.5f, 0f);
            right.offsetMax = Vector2.zero;

            Image well = A2Kit.Well("Well", right, 22);
            UIBuilder.FillParent(well.rectTransform, 0f);

            _a2Pane = UIBuilder.Node("Pane", right);
            _a2Pane.anchorMin = Vector2.zero;
            _a2Pane.anchorMax = Vector2.one;
            _a2Pane.offsetMin = new Vector2(16f, 16f);
            _a2Pane.offsetMax = new Vector2(-(A2RailWidth + 8f), -16f);

            // The rail on the right edge: three text tabs, one lit.
            for (int i = 0; i < HeroDetailPick.TabCount; i++) BuildA2Tab(right, i);

            // TALENTS: a Secondary at the foot of SKILLS, with the line that says which talent is running.
            _openTalents = A2Kit.Button("Talents", right, "TALENTS", 26, A2ButtonKind.Secondary);
            UIBuilder.Box(_openTalents.Root, -(A2RailWidth + 8f), 16f, 260f, A2FooterHeight, new Vector2(1f, 0f));
            _openTalents.OnClick(OpenTalents);
            FeatureLockView.Attach(_openTalents.Root, "herodetail.Talents", _openTalents.Label,
                                   new Vector2(-110f, 0f), FeatureLockView.TagSide.Above);

            _talentRunning = A2Kit.Label("TalentRunning", right, "", 24, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(_talentRunning.rectTransform, 24f, 16f, 480f, A2FooterHeight, new Vector2(0f, 0f));
        }

        private void BuildA2Tab(RectTransform right, int tab)
        {
            RectTransform node = UIBuilder.Node("Tab_" + HeroDetailPick.TabLabel(tab), right);
            UIBuilder.Box(node, -12f, -(16f + tab * (A2TabHeight + A2TabGap)), A2TabWidth, A2TabHeight, new Vector2(1f, 1f));
            Image face = UIBuilder.Panel("Face", node, A2Kit.Card, 16);
            face.raycastTarget = true;
            UIBuilder.FillParent(face.rectTransform, 0f);
            UIBuilder.Frame("Edge", face.rectTransform, A2Kit.Ink, 16, 3);
            Text label = A2Kit.Label("Text", node, HeroDetailPick.TabLabel(tab), 30, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.FillParent(label.rectTransform, 4f);
            Button button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            UIButton handle = new UIButton();
            handle.Root = node;
            handle.Button = button;
            int captured = tab;
            handle.OnClick(delegate { OnTabA2(captured); });
            _a2TabRoot[tab] = node;
            _a2TabFace[tab] = face;
            _a2TabText[tab] = label;
        }

        // ------------------------------------------------------------------ refresh

        private void OnShowA2()
        {
            RefreshA2();
        }

        private void RefreshA2()
        {
            HeroDef def = CurrentDef;
            if (def == null) return;
            GameSession session = App.Session;
            bool locked = _hero == null;
            bool acts = !locked && !_prologueView;

            string key = locked ? "L:" + def.Id : _hero.Uid;
            if (key != _a2HeroKey)
            {
                _a2HeroKey = key;
                _a2Tab = HeroDetailPick.StartTab(locked);
                _a2Ability = 0;
            }
            if (!HeroDetailPick.TabAvailable(_a2Tab, locked)) _a2Tab = HeroDetailPick.StartTab(locked);

            // The stat line the old page printed: relic and armour folded in, the worn skin last. The gear bonus is
            // the armour's own share, read before the skin is applied so it names gear and nothing else.
            UnitStats now;
            float[] bare = null;
            float[] geared = null;
            int cap = 0;
            if (locked)
            {
                now = StatMath.ForHero(def, 1);
            }
            else
            {
                RelicDef relic = session.Profile.EquippedRelicOf(_hero, session.Content);
                List<ArmorPiece> armor = session.Profile.EquippedArmorOf(_hero);
                ArmorConfig armorConfig = session.Content.Armor;
                now = StatMath.ForHero(def, _hero.Level, relic, armor, armorConfig);
                UnitStats without = StatMath.ForHero(def, _hero.Level, relic, null, armorConfig);
                geared = new float[] { now.MaxHp, now.Attack, now.Defense, now.Speed };
                bare = new float[] { without.MaxHp, without.Attack, without.Defense, without.Speed };
                HeroSkins.ApplyWorn(now, session.Profile, _hero, session.Content);
                cap = session.HeroLevelCapOf(_hero);
            }

            RebuildA2Identity(def, now, cap, locked, acts, session);
            RefreshA2Tabs(locked);
            RebuildA2Pane(def, now, geared, bare, locked, session);
            RefreshA2Bar(def, locked, acts, cap, session);
            RefreshA2Links(acts, session);

            // The talent door lives on SKILLS only, and only for a hero the player owns.
            RefreshTalentDoor();
            bool talentsHere = acts && _a2Tab == HeroDetailPick.TabSkills;
            _openTalents.Root.gameObject.SetActive(talentsHere);
            _talentRunning.gameObject.SetActive(talentsHere && FeatureGates.Allows(App, "herodetail.Talents"));
            _talentRunning.color = _talentRunning.text.StartsWith("RUNNING", StringComparison.Ordinal) ? A2Kit.GoodStroke : A2Kit.InkSoft;
        }

        // ------------------------------------------------------------------ the left card

        private static void A2Place(RectTransform rt, float top, float height, float side)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(side, -(top + height));
            rt.offsetMax = new Vector2(-side, -top);
        }

        private static void A2Clear(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                GameObject child = host.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void RebuildA2Identity(HeroDef def, UnitStats now, int cap, bool locked, bool acts, GameSession session)
        {
            RectTransform host = _a2LeftHost;
            A2Clear(host);

            // The portrait: a square window on the aspect colour. No medallion ring, nothing round it but the card.
            Image back = UIBuilder.Panel("PortraitBack", host, A2Kit.AspectFill(def.Aspect), 16);
            back.raycastTarget = false;
            RectTransform backRect = back.rectTransform;
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(1f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.offsetMin = new Vector2(16f, -(16f + A2PortraitBackHeight));
            backRect.offsetMax = new Vector2(-16f, -16f);
            UIBuilder.Frame("Edge", backRect, A2Kit.Ink, 16, 4);

            RectTransform faceBox = UIBuilder.Node("Face", backRect);
            UIBuilder.Box(faceBox, 0f, -14f, A2PortraitWindow, A2PortraitWindow, new Vector2(0.5f, 1f));
            Image outer = A2Kit.CutPanel("Outer", faceBox, A2Kit.InkDeep, 20, 36);
            UIBuilder.FillParent(outer.rectTransform, 0f);
            Image window = A2Kit.CutPanel("Window", faceBox, A2Kit.Ink, 14, 26);
            UIBuilder.FillParent(window.rectTransform, 6f);
            Mask mask = window.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            FormationScreen.BuildCrop(window.rectTransform, def, FormationScreen.A2SlotCrop, FormationScreen.A2SlotFocus, Color.white);
            if (!locked) SkinArt.Dress(window.rectTransform, session, _hero);
            else
            {
                Transform art = window.rectTransform.Find("Badge");
                if (art != null) art.GetComponent<Image>().color = A2DetailLockedFace;
            }

            // The name.
            Text name = A2Kit.Label("Name", host, def.Name, 44, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.BestFit(name, 30, 44);
            A2Place(name.rectTransform, A2PortraitBackHeight + 26f, 52f, 16f);

            // Role and aspect on one line.
            RectTransform line = UIBuilder.Node("RoleLine", host);
            A2Place(line, A2PortraitBackHeight + 84f, 34f, 0f);
            Image roleGlyph = UIBuilder.Img("RoleGlyph", line, FormationScreen.RoleGlyph(def.Role, 48), A2Kit.InkSoft);
            UIBuilder.Box(roleGlyph.rectTransform, -190f, 0f, 30f, 30f, new Vector2(0.5f, 0.5f));
            Text role = A2Kit.Label("Role", line, UITheme.RoleName(def.Role), 26, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(role.rectTransform, -150f, 0f, 160f, 34f, new Vector2(0.5f, 0.5f));
            role.rectTransform.pivot = new Vector2(0f, 0.5f);
            role.rectTransform.anchoredPosition = new Vector2(-170f, 0f);
            RectTransform emblem = A2Kit.Emblem("AspectEmblem", line, def.Aspect, 30f);
            UIBuilder.Box(emblem, 28f, 0f, 30f, 30f, new Vector2(0.5f, 0.5f));
            Text aspect = A2Kit.Label("Aspect", line, AspectChart.DisplayName(def.Aspect), 26, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(aspect.rectTransform, 56f, 0f, 160f, 34f, new Vector2(0.5f, 0.5f));
            aspect.rectTransform.pivot = new Vector2(0f, 0.5f);
            aspect.rectTransform.anchoredPosition = new Vector2(52f, 0f);
            role.rectTransform.anchoredPosition = new Vector2(-150f, 0f);
            roleGlyph.rectTransform.anchoredPosition = new Vector2(-174f, 0f);
            emblem.anchoredPosition = new Vector2(30f, 0f);

            // Level and stars.
            RectTransform levelRow = UIBuilder.Node("LevelRow", host);
            A2Place(levelRow, A2PortraitBackHeight + 126f, 40f, 0f);
            if (!locked)
            {
                string levelText = _prologueView
                    ? "LEVEL " + _hero.Level.ToString(CultureInfo.InvariantCulture)
                    : "LEVEL " + _hero.Level.ToString(CultureInfo.InvariantCulture) + "<color=#7A5A3E> / " + cap.ToString(CultureInfo.InvariantCulture) + "</color>";
                Text level = A2Kit.Label("Level", levelRow, levelText, 30, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
                UIBuilder.Box(level.rectTransform, 24f, 0f, 330f, 40f, new Vector2(0f, 0.5f));
            }
            RectTransform stars = A2Kit.Stars("Stars", levelRow, def.Stars, 26f, 2f);
            UIBuilder.Box(stars, -24f, 0f, 26f * 5 + 8f, 26f, new Vector2(1f, 0.5f));

            // Power: one big number.
            RectTransform powerRow = UIBuilder.Node("PowerRow", host);
            A2Place(powerRow, A2PortraitBackHeight + 172f, 68f, 0f);
            if (locked)
            {
                Text lockedText = A2Kit.Label("Locked", powerRow, "LOCKED", 56, A2Kit.InkSoft, TextAnchor.MiddleCenter, A2Kit.Display);
                UIBuilder.FillParent(lockedText.rectTransform, 0f);
            }
            else
            {
                Text powerLabel = A2Kit.Label("PowerLabel", powerRow, "POWER", 26, A2Kit.InkSoft, TextAnchor.MiddleLeft, A2Kit.BodyBold);
                UIBuilder.Box(powerLabel.rectTransform, 24f, 0f, 200f, 40f, new Vector2(0f, 0.5f));
                Text power = A2Kit.Label("Power", powerRow, now.Power.ToString("N0", CultureInfo.InvariantCulture), 62, A2Kit.Ink,
                                         TextAnchor.MiddleRight, A2Kit.Display);
                UIBuilder.Box(power.rectTransform, -24f, 0f, 400f, 68f, new Vector2(1f, 0.5f));
            }

            // Where the hero stands in the squad.
            if (acts)
            {
                int slot = session.Profile.TeamSlotOf(_hero.Uid);
                Text standing = A2Kit.Label("SlotStatus", host, HeroDetailPick.StandingLine(slot), 24,
                                            slot >= 0 ? A2Kit.GoodStroke : A2Kit.InkSoft, TextAnchor.MiddleCenter, A2Kit.BodyBold);
                A2Place(standing.rectTransform, A2PortraitBackHeight + 246f, 32f, 16f);
            }
        }

        private void RefreshA2Links(bool acts, GameSession session)
        {
            _openFormation.Root.gameObject.SetActive(acts);
            bool skins = acts && SkinsSwitch.Enabled && session.Content.Skins.SkinsOf(_hero.DefId).Count > 0
                         && HeroSkins.OwnedFor(session.Profile, _hero.DefId, session.Content).Count > 0;
            _skinButton.Root.gameObject.SetActive(skins);
        }

        /// <summary>SKINS: puts on the next skin the hero owns (CycleSkin) and says which look is worn.</summary>
        private void OnSkinsA2()
        {
            if (_hero == null) return;
            CycleSkin();
            App.Notify(SkinArt.Line(App.Session, _hero));
        }

        // ------------------------------------------------------------------ the tab rail

        private void OnTabA2(int tab)
        {
            App.Audio.Tap();
            if (tab == _a2Tab) return;
            _a2Tab = tab;
            RefreshA2();
        }

        private void RefreshA2Tabs(bool locked)
        {
            for (int i = 0; i < HeroDetailPick.TabCount; i++)
            {
                bool available = HeroDetailPick.TabAvailable(i, locked);
                _a2TabRoot[i].gameObject.SetActive(available);
                bool lit = i == _a2Tab;
                _a2TabFace[i].color = lit ? A2Kit.Teal : A2Kit.Card;
                _a2TabText[i].color = lit ? A2Kit.TextOnDark : A2Kit.Ink;
            }
        }

        private void RebuildA2Pane(HeroDef def, UnitStats now, float[] geared, float[] bare, bool locked, GameSession session)
        {
            A2Clear(_a2Pane);
            if (_a2Tab == HeroDetailPick.TabSkills) BuildA2Skills(def, now, locked);
            else if (_a2Tab == HeroDetailPick.TabGear && !locked) BuildA2Gear(session);
            else BuildA2Stats(def, now, geared, bare, locked);
        }

        // ------------------------------------------------------------------ STATS

        private void BuildA2Stats(HeroDef def, UnitStats now, float[] geared, float[] bare, bool locked)
        {
            RectTransform pane = _a2Pane;

            Text caption = A2Kit.Label("Caption", pane, locked ? "At level 1, no gear" : "Small green numbers come from gear",
                                       24, A2Kit.InkSoft, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            A2Place(caption.rectTransform, 0f, 36f, 8f);

            string[] names = { "Health", "Attack", "Defense", "Speed" };
            float[] values = { now.MaxHp, now.Attack, now.Defense, now.Speed };
            int rows = Mathf.Min(HeroDetailPick.MaxStatRows, names.Length);
            for (int i = 0; i < rows; i++)
            {
                float top = 44f + i * A2StatPitch;
                RectTransform row = UIBuilder.Node("StatRow" + i, pane);
                A2Place(row, top, A2StatPitch, 0f);

                Image rule = UIBuilder.Panel("Rule", row, A2Kit.WithAlpha(A2Kit.Ink, 0.18f), 0);
                rule.raycastTarget = false;
                UIBuilder.Box(rule.rectTransform, 0f, 0f, 4000f, 2f, new Vector2(0.5f, 1f));
                rule.rectTransform.anchorMin = new Vector2(0f, 1f);
                rule.rectTransform.anchorMax = new Vector2(1f, 1f);
                rule.rectTransform.sizeDelta = new Vector2(-16f, 2f);

                Text name = A2Kit.Label("Name", row, names[i], 32, A2Kit.InkSoft, TextAnchor.MiddleLeft, A2Kit.BodyBold);
                UIBuilder.Box(name.rectTransform, 8f, -2f, 260f, A2StatPitch - 4f, new Vector2(0f, 0.5f));
                Text figure = A2Kit.Label("Value", row, Mathf.RoundToInt(values[i]).ToString("N0", CultureInfo.InvariantCulture), 54, A2Kit.Ink,
                                          TextAnchor.MiddleLeft, A2Kit.Display);
                UIBuilder.Box(figure.rectTransform, 290f, -2f, 260f, A2StatPitch - 4f, new Vector2(0f, 0.5f));
                if (geared != null && bare != null)
                {
                    string bonus = HeroDetailPick.GearBonus(geared[i], bare[i]);
                    if (bonus.Length > 0)
                    {
                        Text gear = A2Kit.Label("Gear", row, bonus + " gear", 28, A2Kit.GoodStroke, TextAnchor.MiddleLeft, A2Kit.BodyBold);
                        UIBuilder.Box(gear.rectTransform, 560f, -2f, 220f, A2StatPitch - 4f, new Vector2(0f, 0.5f));
                    }
                }
            }

            float moreTop = 44f + rows * A2StatPitch + 8f;
            Text more = A2Kit.Label("More", pane, HeroDetailPick.MoreLine(now.EffectHit, now.EffectResist), 26, A2Kit.Ink,
                                    TextAnchor.MiddleLeft, A2Kit.BodyBold);
            A2Place(more.rectTransform, moreTop, 40f, 8f);

            BuildA2Matchups(def, moreTop + 56f);
        }

        /// <summary>
        /// The six aspect matchups: how this hero's aspect fares against each, read from AspectChart (the table the
        /// fight uses): three dots for an advantage, two for neutral, one for a disadvantage.
        /// </summary>
        private void BuildA2Matchups(HeroDef def, float top)
        {
            RectTransform pane = _a2Pane;
            Text caption = A2Kit.Label("MatchCaption", pane, "Against each aspect: more dots, more damage", 24, A2Kit.InkSoft,
                                       TextAnchor.MiddleLeft, A2Kit.BodyBold);
            A2Place(caption.rectTransform, top, 32f, 8f);

            RectTransform strip = UIBuilder.Node("Matchups", pane);
            A2Place(strip, top + 40f, 76f, 0f);
            Image bg = A2Kit.Well("Bg", strip, 14);
            UIBuilder.FillParent(bg.rectTransform, 0f);
            for (int i = 0; i < 6; i++)
            {
                Aspect other = (Aspect)i;
                float cx = (i + 0.5f) / 6f;
                RectTransform cell = UIBuilder.Node("M" + i, strip);
                cell.anchorMin = new Vector2(cx, 0.5f);
                cell.anchorMax = new Vector2(cx, 0.5f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.sizeDelta = new Vector2(100f, 70f);
                cell.anchoredPosition = Vector2.zero;

                RectTransform emblem = A2Kit.Emblem("Emblem", cell, other, 40f);
                UIBuilder.Box(emblem, 0f, -4f, 40f, 40f, new Vector2(0.5f, 1f));

                float multiplier = AspectChart.Multiplier(def.Aspect, other);
                int pips = multiplier > 1.001f ? 3 : (multiplier < 0.999f ? 1 : 2);
                for (int p = 0; p < 3; p++)
                {
                    Image dot = UIBuilder.Img("D" + p, cell, TextureLab.Circle(32),
                                              p < pips ? A2Kit.Ink : A2Kit.WithAlpha(A2Kit.Ink, 0.22f));
                    UIBuilder.Box(dot.rectTransform, (p - 1) * 18f, 8f, 12f, 12f, new Vector2(0.5f, 0f));
                }
            }
        }

        // ------------------------------------------------------------------ SKILLS

        private static List<A2Skill> A2SkillsOf(HeroDef def)
        {
            List<A2Skill> list = new List<A2Skill>();
            if (def.BasicAttack != null)
            {
                A2Skill s = new A2Skill();
                s.Ability = def.BasicAttack; s.Kind = "BASIC"; s.Tint = UITheme.TextMuted;
                list.Add(s);
            }
            if (def.Active != null)
            {
                A2Skill s = new A2Skill();
                s.Ability = def.Active; s.Kind = "ACTIVE"; s.Tint = UITheme.Energy;
                list.Add(s);
            }
            for (int i = 0; i < def.Passives.Count; i++)
            {
                A2Skill s = new A2Skill();
                s.Ability = AbilityMath.AsAbility(def.Passives[i]);
                s.Passive = def.Passives[i];
                s.Kind = "PASSIVE";
                s.Tint = UITheme.StarColor(def.Stars);
                list.Add(s);
            }
            return list;
        }

        private void BuildA2Skills(HeroDef def, UnitStats now, bool locked)
        {
            RectTransform pane = _a2Pane;
            List<A2Skill> skills = A2SkillsOf(def);
            if (skills.Count == 0) return;
            if (_a2Ability < 0 || _a2Ability >= skills.Count) _a2Ability = 0;

            // The tiles: icon and name only.
            RectTransform row = UIBuilder.Node("Tiles", pane);
            A2Place(row, 0f, A2SkillTileHeight, 0f);
            const float gap = 10f;
            for (int i = 0; i < skills.Count; i++)
            {
                float share = 1f / skills.Count;
                RectTransform cell = UIBuilder.Node("Ability" + i, row);
                cell.anchorMin = new Vector2(i * share, 0f);
                cell.anchorMax = new Vector2((i + 1) * share, 1f);
                cell.offsetMin = new Vector2(i == 0 ? 0f : gap * 0.5f, 0f);
                cell.offsetMax = new Vector2(i == skills.Count - 1 ? 0f : -gap * 0.5f, 0f);

                bool selected = i == _a2Ability;
                Image face = UIBuilder.Panel("Face", cell, A2Kit.Card, 14);
                face.raycastTarget = true;
                UIBuilder.FillParent(face.rectTransform, 0f);
                UIBuilder.Frame("Edge", face.rectTransform, selected ? A2Kit.Teal : A2Kit.Ink, 14, selected ? 6 : 3);

                Sprite sprite = UiKit.Ability(def.Id, skills[i].Ability.Id);
                if (sprite != null)
                {
                    Image icon = UIBuilder.Img("Icon", cell, sprite, Color.white);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    UIBuilder.Box(icon.rectTransform, 0f, -12f, 64f, 64f, new Vector2(0.5f, 1f));
                }
                else
                {
                    RectTransform emblem = A2Kit.Emblem("Icon", cell, def.Aspect, 56f);
                    UIBuilder.Box(emblem, 0f, -14f, 56f, 56f, new Vector2(0.5f, 1f));
                }

                Text name = A2Kit.Label("Name", cell, skills[i].Ability.Name, 22, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.BodyBold);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                UIBuilder.Box(name.rectTransform, 0f, 8f, 140f, 56f, new Vector2(0.5f, 0f));
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(1f, 0f);
                name.rectTransform.pivot = new Vector2(0.5f, 0f);
                name.rectTransform.offsetMin = new Vector2(6f, 8f);
                name.rectTransform.offsetMax = new Vector2(-6f, 64f);

                Button press = cell.gameObject.AddComponent<Button>();
                press.targetGraphic = face;
                press.transition = Selectable.Transition.None;
                int captured = i;
                press.onClick.AddListener(delegate { OnAbilityA2(captured); });
            }

            BuildA2AbilityDetail(def, skills[_a2Ability], now, locked);
        }

        private void OnAbilityA2(int index)
        {
            App.Audio.Tap();
            if (index == _a2Ability) return;
            _a2Ability = index;
            RefreshA2();
        }

        /// <summary>
        /// The selected ability: its name and kind, the sentence, the headline figure with the line that says how it
        /// was built, up to two facts, and the one caveat (every figure is before the target's Defence). The whole
        /// card is a button that opens the shared ability sheet, where every other number lives.
        /// </summary>
        private void BuildA2AbilityDetail(HeroDef def, A2Skill skill, UnitStats now, bool locked)
        {
            RectTransform card = UIBuilder.Node("Detail", _a2Pane);
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.offsetMin = new Vector2(0f, A2FooterHeight + 8f);
            card.offsetMax = new Vector2(0f, -(A2SkillTileHeight + 12f));

            Image face = UIBuilder.Panel("Face", card, A2Kit.Card, 16);
            face.raycastTarget = true;
            UIBuilder.FillParent(face.rectTransform, 0f);
            UIBuilder.Frame("Edge", face.rectTransform, A2Kit.Ink, 16, 3);

            GameSession session = App.Session;
            AbilityDef ability = skill.Ability;
            List<AbilityValue> values = AbilityMath.Values(ability, now, session.Balance);

            Text name = A2Kit.Label("Name", card, ability.Name, 36, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.Display);
            UIBuilder.BestFit(name, 26, 36);
            A2Place(name.rectTransform, 8f, 44f, 20f);
            name.rectTransform.offsetMax = new Vector2(-160f, name.rectTransform.offsetMax.y);
            Text kind = A2Kit.Label("Kind", card, skill.Kind, 24, A2Kit.InkSoft, TextAnchor.MiddleRight, A2Kit.BodyBold);
            UIBuilder.Box(kind.rectTransform, -20f, -8f, 140f, 44f, new Vector2(1f, 1f));

            string prose = session.Content.AbilityTexts != null
                ? session.Content.AbilityTexts.Prose(ability.Id, ability.Description)
                : (ability.Description ?? "");
            Text sentence = A2Kit.Label("Prose", card, prose, 26, A2Kit.Ink, TextAnchor.UpperLeft, A2Kit.Body);
            sentence.horizontalOverflow = HorizontalWrapMode.Wrap;
            sentence.verticalOverflow = VerticalWrapMode.Truncate;
            A2Place(sentence.rectTransform, 56f, 90f, 20f);

            AbilityValue headline = AbilityMath.Headline(values);
            string main;
            string secondary;
            Color tint;
            if (headline != null)
            {
                main = AbilityText.Headline(headline);
                secondary = AbilityText.Secondary(headline, ability.Effects[headline.EffectIndex]);
                tint = A2ValueTint(headline.Kind);
            }
            else if (values.Count > 0)
            {
                main = AbilityText.Figure(values[0], true);
                secondary = AbilityText.Secondary(values[0], ability.Effects[values[0].EffectIndex]);
                tint = A2ValueTint(values[0].Kind);
            }
            else
            {
                main = "";
                secondary = "No damage of its own";
                tint = A2Kit.InkSoft;
            }
            float y = 150f;
            if (main.Length > 0)
            {
                Text headlineText = A2Kit.Label("Headline", card, main, 40, tint, TextAnchor.MiddleLeft, A2Kit.Display);
                A2Place(headlineText.rectTransform, y, 48f, 20f);
                y += 50f;
            }
            Text under = A2Kit.Label("Secondary", card, secondary, 24, A2Kit.InkSoft, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            A2Place(under.rectTransform, y, 30f, 20f);
            y += 34f;

            List<AbilityFact> facts = AbilityFacts.For(ability, skill.Passive, now);
            int shownFacts = Mathf.Min(2, facts.Count);
            for (int f = 0; f < shownFacts; f++)
            {
                Text fact = A2Kit.Label("Fact" + f, card, facts[f].Label + "   " + facts[f].Text, 24, A2Kit.Ink,
                                        TextAnchor.MiddleLeft, A2Kit.BodyBold);
                A2Place(fact.rectTransform, y, 29f, 20f);
                y += 29f;
            }
            int more = Mathf.Max(0, values.Count - 1) + Mathf.Max(0, facts.Count - shownFacts);
            if (more > 0)
            {
                Text moreText = A2Kit.Label("More", card, "+" + more.ToString(CultureInfo.InvariantCulture) + " more, tap for all", 22,
                                            A2Kit.InkSoft, TextAnchor.MiddleLeft, A2Kit.BodyBold);
                A2Place(moreText.rectTransform, y, 26f, 20f);
                y += 28f;
            }

            Text caveat = A2Kit.Label("Caveat", card, locked ? AbilityText.CaveatLocked : AbilityText.Caveat, 22, A2Kit.InkSoft,
                                      TextAnchor.LowerLeft, A2Kit.Body);
            caveat.horizontalOverflow = HorizontalWrapMode.Wrap;
            caveat.verticalOverflow = VerticalWrapMode.Truncate;
            caveat.rectTransform.anchorMin = new Vector2(0f, 0f);
            caveat.rectTransform.anchorMax = new Vector2(1f, 0f);
            caveat.rectTransform.pivot = new Vector2(0.5f, 0f);
            caveat.rectTransform.offsetMin = new Vector2(20f, 8f);
            caveat.rectTransform.offsetMax = new Vector2(-20f, 62f);

            AbilityCardSheet.Entry entry = new AbilityCardSheet.Entry();
            entry.HeroId = def.Id;
            entry.Kind = skill.Kind;
            entry.Tint = skill.Tint;
            entry.Ability = ability;
            entry.Passive = skill.Passive;
            UnitStats capturedStats = now;
            bool capturedOwned = !locked;
            Button press = card.gameObject.AddComponent<Button>();
            press.targetGraphic = face;
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(delegate { ShowAbilityCard(entry, capturedStats, capturedOwned); });
        }

        private static Color A2ValueTint(AbilityValueKind kind)
        {
            switch (kind)
            {
                case AbilityValueKind.Heal: return A2Kit.GoodStroke;
                case AbilityValueKind.Shield: return A2Kit.TealBevel;
                default: return A2Kit.EmberBevel;
            }
        }

        // ------------------------------------------------------------------ GEAR

        private void BuildA2Gear(GameSession session)
        {
            RectTransform pane = _a2Pane;
            float startX = 0f;
            float total = 3f * A2GearTile + 2f * A2GearGap;
            float rowPitch = A2GearTile + 50f;

            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
            {
                ArmorSlot slot = ArmorInfo.AllSlots[i];
                int col = i % 3;
                int rowIndex = i / 3;

                RectTransform host = UIBuilder.Node("Slot_" + slot, pane);
                host.anchorMin = new Vector2(0.5f, 1f);
                host.anchorMax = new Vector2(0.5f, 1f);
                host.pivot = new Vector2(0.5f, 1f);
                host.sizeDelta = new Vector2(A2GearTile, A2GearTile);
                host.anchoredPosition = new Vector2(startX - total * 0.5f + A2GearTile * 0.5f + col * (A2GearTile + A2GearGap), -(4f + rowIndex * rowPitch));

                ArmorPiece piece = session.Profile.ArmorInSlot(_hero, slot);
                RectTransform tile = piece != null
                    ? ItemCard.ForArmor("Card", host, A2GearTile, piece)
                    : ItemCard.Vacant("Card", host, A2GearTile, slot);
                UIBuilder.FillParent(tile, 0f);

                Text label = A2Kit.Label("Label", host, ItemCard.SlotName(slot), 24, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.BodyBold);
                UIBuilder.Box(label.rectTransform, 0f, -A2GearTile - 2f, A2GearTile + A2GearGap - 4f, 30f, new Vector2(0.5f, 1f));

                // Tapping a slot routes to the Inventory, which owns every equip action in the game.
                Image target = host.gameObject.AddComponent<Image>();
                target.color = new Color(1f, 1f, 1f, 0.003f);
                target.rectTransform.SetAsFirstSibling();
                Button press = host.gameObject.AddComponent<Button>();
                press.transition = Selectable.Transition.None;
                press.targetGraphic = target;
                press.onClick.AddListener(OpenArmorSlot);
                FeatureLockView.Attach(host, "herodetail.ArmorSlot", label, Vector2.zero,
                                       FeatureLockView.TagSide.Below, i == 0).Refresh(App);
            }

            // The attunement line, in the words the old page used.
            PlayerProfile profile = session.Profile;
            int matching = profile.AttunementCount(_hero);
            string aspectName = AspectChart.DisplayName(_hero.Def.Aspect);
            List<ArmorAttunementDef> met = session.Content.Armor.AttunementsFor(_hero.Def.Aspect, matching);
            string text;
            Color color;
            if (met.Count == 0)
            {
                text = "Attunement  " + matching + "/6 " + aspectName + "  -  2 matching pieces unlocks the first bonus.";
                color = A2Kit.InkSoft;
            }
            else
            {
                string names = "";
                for (int i = 0; i < met.Count; i++)
                    names += (names.Length > 0 ? ", " : "") + met[i].Name + " (" + met[i].Pieces + ")";
                text = "Attunement  " + matching + "/6 " + aspectName + "  -  " + names;
                color = A2Kit.GoodStroke;
            }
            Text attunement = A2Kit.Label("Attunement", pane, text, 24, color, TextAnchor.UpperLeft, A2Kit.BodyBold);
            attunement.horizontalOverflow = HorizontalWrapMode.Wrap;
            attunement.verticalOverflow = VerticalWrapMode.Truncate;
            A2Place(attunement.rectTransform, 4f + 2f * rowPitch + 4f, 64f, 8f);
        }

        // ------------------------------------------------------------------ the bottom bar

        /// <summary>
        /// One Primary. LEVEL UP leads; the moment ASCEND is ready it takes the Primary and LEVEL UP steps down to a
        /// Secondary. A LEVEL UP that cannot be bought stays orange but Disabled (the hint says why). The door to the
        /// fusion page is a Quiet button while LEVEL UP leads. x10 is a small Quiet button beside LEVEL UP.
        /// </summary>
        private void RefreshA2Bar(HeroDef def, bool locked, bool acts, int cap, GameSession session)
        {
            _obtainButton.Root.gameObject.SetActive(locked);
            _levelUpButton.Root.gameObject.SetActive(acts);
            _levelUpTenButton.Root.gameObject.SetActive(acts);
            _fusionButton.Root.gameObject.SetActive(acts);

            if (_prologueView)
            {
                _capNote.text = "A thousand years of levels. None of them yours to spend.";
                return;
            }
            if (locked)
            {
                _capNote.text = "Not yet summoned. Open the Rift and try for " + def.Name + ".";
                return;
            }

            bool atCap = _hero.Level >= cap;
            int cost = session.LevelUpCost(_hero);
            bool haveGold = session.Profile.Gold >= cost;
            bool enabled = HeroDetailPick.LevelUpEnabled(atCap, haveGold);
            bool ascendReady = def.Stars >= 5 && session.AscensionBlocker(_hero) == null;
            bool ascendLeads = HeroDetailPick.PrimaryOf(ascendReady) == HeroDetailPick.PrimaryAscend;

            _levelUpButton.SetText(atCap ? LevelCapText.ButtonLabel : HeroDetailPick.LevelUpLabel(false, cost));
            _fusionButton.SetText(HeroDetailPick.FusionLabel(def.Stars));
            _levelUpButton.Interactable = enabled;
            _levelUpTenButton.Interactable = enabled;
            _fusionButton.Interactable = true;

            UIButton primary = ascendLeads ? _fusionButton : _levelUpButton;
            UIButton second = ascendLeads ? _levelUpButton : _fusionButton;
            A2Kit.SetKind(primary, A2ButtonKind.Primary);
            A2Kit.SetKind(second, ascendLeads ? A2ButtonKind.Secondary : A2ButtonKind.Quiet);
            A2Kit.SetDisabledMix(_levelUpButton, ascendLeads ? A2ButtonSkin.DefaultDisabledMix : 0.3f);

            primary.Label.fontSize = 38;
            second.Label.fontSize = 30;
            UIBuilder.Box(primary.Root, 0f, 0f, A2PrimaryWidth, A2PrimaryHeight, new Vector2(1f, 0.5f));
            float tenX;
            float secondX;
            if (ascendLeads)
            {
                secondX = -(A2PrimaryWidth + A2ButtonGap + 4f);
                tenX = secondX - A2SecondaryWidth - A2ButtonGap;
            }
            else
            {
                tenX = -(A2PrimaryWidth + A2ButtonGap);
                secondX = tenX - A2TenWidth - A2ButtonGap;
            }
            UIBuilder.Box(second.Root, secondX, 0f, A2SecondaryWidth, A2SecondaryHeight, new Vector2(1f, 0.5f));
            UIBuilder.Box(_levelUpTenButton.Root, tenX, 0f, A2TenWidth, A2TenHeight, new Vector2(1f, 0.5f));

            // The hint line: why LEVEL UP is grey, what the next level costs, or what the cap asks for.
            int copiesNeeded = StatMath.AscensionCopiesFor(session.Balance, _hero.Ascension);
            if (atCap && ascendLeads) _capNote.text = "Level cap reached. " + def.Name + " is ready to ascend.";
            else if (atCap) _capNote.text = LevelCapText.Note(def.Stars, copiesNeeded, _hero.Copies - 1, def.Name, session.FodderPoints(def));
            else if (!haveGold) _capNote.text = HeroDetailPick.NeedGold(cost);
            else _capNote.text = HeroDetailPick.NextLevelLine(cost, session.Profile.Gold);
        }

        // ------------------------------------------------------------------ capture hook

        /// <summary>
        /// For the capture tooling only: shows a tab (a HeroDetailPick tab) and selects an ability tile on SKILLS,
        /// then redraws. Not called by anything at run time.
        /// </summary>
        public void ApplyCaptureStateA2(int tab, int abilityIndex)
        {
            if (!_a2) return;
            _a2Tab = tab;
            _a2Ability = abilityIndex;
            RefreshA2();
        }
    }
}
