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
    /// Everything about one hero, and the only place gold turns into power.
    ///
    /// ---- visual pass ----
    /// TWO PANELS, NOT A SCROLL. The screen used to be a vertical scroll of four stacked cards -
    /// identity, stats, armor, abilities - which meant the hero and their numbers were never on
    /// screen at the same time and the six armor slots were six lines of text. It is now the
    /// mockup's shape: the hero centred with their six equip slots around them on the left, and
    /// everything numeric on the right, all of it visible at once.
    ///
    /// THE SLOTS ARE ITEM CARDS. All six use the shared ItemCard component, the same one the
    /// Inventory grid and the Forge anvil draw, so a Gilded helm looks identical in all three
    /// places. An empty slot draws ItemCard.Vacant rather than collapsing, because a slot a hero
    /// has not filled is information rather than absence - the same rule this screen's old
    /// six-fixed-rows block already followed.
    ///
    /// STILL READ-ONLY ABOUT EQUIPPING. Tapping a slot routes to the Inventory, which owns every
    /// equip action in the game. A second equip path would mean two screens that could disagree
    /// about what is worn.
    ///
    /// THE ASPECT ACCENT APPEARS EXACTLY TWICE: the glow behind the portrait and the hero's own
    /// badge, both tied to this hero's own aspect. Nowhere else - the stat bars are keyed to their
    /// stat, not to the hero, so a Tide hero's Attack bar is the same colour as an Ember hero's.
    ///
    /// NO RING ROUND THE PORTRAIT. It used to have one, tinted to the aspect, drawn outside a
    /// Widgets.Portrait - which put a second frame around a badge that arrives from the artist
    /// already framed, with its four cardinal spikes clipped against the ring's inner edge.
    /// EmberforgeUI.PortraitFrame is what draws the hero now: the tier is a plate BEHIND the
    /// badge and the aspect is the glow outside it, which is two readings where the ring had one.
    /// </summary>
    public sealed class HeroDetailScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.HeroDetail; } }

        // THE PANELS STRETCH BETWEEN THE HEADER AND THE ACTION BAR, and that is a fix rather
        // than a preference.
        //
        // They used to be a fixed 636 tall hung off the top, which meant the page needed
        // 252 + 636 + 168 = 1056 units of height. The 16:9 editor canvas is 1080 and it fitted;
        // a 20:9 phone gets a logical canvas about 966 tall, and on that one the action bar was
        // drawn 90 units UP THROUGH both panels - the hero's title, their attunement line and
        // every ability card were behind it. It is the same fault DESIGN-EMBERFORGE records the
        // component gallery hitting, and it was caught the same way: by photographing the page at
        // the other aspect.
        //
        // Anchored top AND bottom, the panel is exactly the space left between the two whatever
        // the canvas is. The contents then only have to fit the SHORTEST case, which is what the
        // budget below is measured against.
        //
        //   966 short canvas - 252 header - 140 action bar - 8 gap = 566 of panel.
        //   24 pad + 72 badge band + 10 divider + 6*44 stats + 8 + 66 aspects + 8 + 96 abilities
        //   = 548, inside 566 with 18 to spare.
        private const float PanelTop = 252f;
        private const float ActionBarHeight = 140f;
        private const float PanelBottomGap = 8f;
        private const float PanelWidth = 920f;

        /// <summary>The shortest logical canvas this game targets - a 20:9 phone, not the 16:9
        /// editor window. Every height on this page is checked against it.</summary>
        private const float ShortestCanvasHeight = 966f;

        /// <summary>What the panel is at its shortest, which is what its contents are laid out
        /// against. At 16:9 the panel is taller than this and the contents simply have air under
        /// them; at 20:9 this is exactly what there is.</summary>
        private const float PanelHeight = ShortestCanvasHeight - PanelTop - ActionBarHeight - PanelBottomGap;
        private const float LeftX = -480f;
        private const float RightX = 480f;
        private const float SlotSize = 112f;

        /// <summary>
        /// Width of the CHANGE BANNER button, and the room the power readout reserves for it.
        ///
        /// Both sit on the same header row anchored to the panel's top-right corner. They used to
        /// be positioned independently, at the same anchor and the same offsets, and the button
        /// - added second, drawn second, opaque - covered the readout completely. It rendered at
        /// full alpha and active the whole time, so nothing errored and nothing looked broken;
        /// the number was simply never on screen. Naming the width once and having the readout
        /// offset itself by it makes the two positions dependent, so the next edit to either has
        /// to move both.
        /// </summary>
        private const float BannerButtonWidth = 260f;
        private const float PortraitSize = 300f;

        /// <summary>Stat row pitch: a 14px name, a 26px bar, and breathing room.</summary>
        private const float RowPitch = 44f;
        private const float StatBarHeight = 26f;

        private HeroInstance _hero;
        private HeroDef _lockedDef;

        private Text _headerSubtitle;

        private RectTransform _left;
        private RectTransform _right;
        private RectTransform _portraitHost;
        private RectTransform _slotHost;

        private Text _nameLabel;
        private Text _titleLabel;
        private Text _powerLabel;
        private Text _levelLabel;
        private Text _attunementLabel;
        private Text _slotStatus;

        private Image _aspectBadge;
        private Image _aspectBadgeGlyph;
        private RectTransform _starHost;
        private RectTransform _statHost;
        private RectTransform _aspectRow;
        private RectTransform _abilityHost;

        private UIButton _openFormation;
        private UIButton _levelUpButton;
        private UIButton _levelUpTenButton;
        private UIButton _obtainButton;
        private Text _capNote;

        /// <summary>True while this screen is showing a prologue hero. Cleared by SetHero and
        /// SetLockedHero, the same discipline BattleScreen's mode setters use, so the three ways
        /// in cannot leave a stale one set.</summary>
        private bool _prologueView;

        /// <summary>The definition backing the screen right now, whichever mode it's in.</summary>
        private HeroDef CurrentDef
        {
            get { return _hero != null ? _hero.Def : _lockedDef; }
        }

        public void SetHero(HeroInstance hero)
        {
            _hero = hero;
            _lockedDef = null;
            _prologueView = false;
        }

        /// <summary>
        /// A hero the player is being SHOWN rather than one they own: the prologue's fixed squad.
        ///
        /// Everything that reads stays - identity, role and aspect chips, abilities, the stat
        /// block, the armor slots. Everything that ACTS is hidden, and that is the entire point of
        /// this mode existing rather than SetHero being reused. A prologue hero is not in
        /// Profile.Heroes, so LEVEL UP would charge the player real gold to raise a hero that
        /// ceases to exist eight cards later; CHANGE BANNER would walk them into FormationScreen
        /// mid-story to deploy someone who is not on their roster.
        /// </summary>
        public void SetPrologueHero(HeroInstance hero)
        {
            _hero = hero;
            _lockedDef = null;
            _prologueView = true;
        }

        /// <summary>A hero the player hasn't summoned yet - shows identity and abilities, not stats or deploy/level actions.</summary>
        public void SetLockedHero(HeroDef def)
        {
            _lockedDef = def;
            _hero = null;
            _prologueView = false;
        }

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            // The page's own ground. The shared backdrop is the near-black violet an unconverted
            // screen sits on, and two warm brown panels on it read as two different pieces of
            // software - the same one Image, on one screen, that HeroesScreen lays down.
            EmberforgeUI.Ground(Root);

            ScreenChrome.ForgedHeader(Root, "HERO", Router, out _headerSubtitle);
            BuildLeftPanel();
            BuildRightPanel();
            BuildActionBar();
        }

        private void BuildLeftPanel()
        {
            _left = Panel("Hero", LeftX);

            // THE HERO, AND NO RING ROUND THEM.
            //
            // This used to draw Widgets.Portrait inside a separate brass ring tinted to the
            // hero's aspect. Every portrait Pinoy delivers is a finished badge with its own gold
            // frame and four spikes reaching the edge of the image, so that ring was a second
            // frame on one object with the spikes clipped against its inner edge - the exact
            // fault EmberforgeUI.PortraitFrame was written to answer. The tier is a plate BEHIND
            // the badge and the aspect is the glow outside it, which is two channels where the
            // ring had one.
            _portraitHost = UIBuilder.Node("PortraitHost", _left);
            UIBuilder.Box(_portraitHost, 0f, 34f, PortraitSize, PortraitSize, new Vector2(0.5f, 0.5f));

            _nameLabel = EmberforgeUI.Title("Name", _left, "", UITheme.FontTitle,
                                            Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(_nameLabel.rectTransform, 0f, 96f, 620f, 52f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_nameLabel, Emberforge.Outline, Emberforge.StrokeThick);

            _titleLabel = EmberforgeUI.Label("Title", _left, "", UITheme.FontTiny,
                                             Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_titleLabel.rectTransform, 0f, 70f, 620f, 28f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_titleLabel, Emberforge.Outline, Emberforge.StrokeThin);

            _levelLabel = EmberforgeUI.Label("Level", _left, "", UITheme.FontSmall,
                                             Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(_levelLabel.rectTransform, UITheme.Space24, -UITheme.Space16, 400f, 32f,
                          new Vector2(0f, 1f));
            UIBuilder.Outlined(_levelLabel, Emberforge.Outline, Emberforge.StrokeThin);

            _powerLabel = EmberforgeUI.Label("Power", _left, "", UITheme.FontSmall,
                                             Emberforge.TextGold, TextAnchor.UpperRight);
            // Right-aligned, so it grows leftward from its own right edge - which is parked one
            // button-width plus a gutter clear of the panel edge, leaving the corner to the button.
            UIBuilder.Box(_powerLabel.rectTransform,
                          -(UITheme.Space24 + BannerButtonWidth + UITheme.Space16), -UITheme.Space16,
                          400f, 32f, new Vector2(1f, 1f));
            UIBuilder.Outlined(_powerLabel, Emberforge.Outline, Emberforge.StrokeThin);

            // The six slots: three down each side of the hero, in ArmorInfo's own order.
            _slotHost = UIBuilder.Node("Slots", _left);
            UIBuilder.FillParent(_slotHost, 0f);

            _attunementLabel = EmberforgeUI.Body("Attunement", _left, "", UITheme.FontTiny - 4,
                                                 Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_attunementLabel.rectTransform, 0f, 34f, PanelWidth - 60f, 34f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_attunementLabel, Emberforge.Outline, Emberforge.StrokeThin);

            _slotStatus = EmberforgeUI.Label("SlotStatus", _left, "", UITheme.FontTiny,
                                             Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_slotStatus.rectTransform, 0f, 8f, PanelWidth - 60f, 26f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_slotStatus, Emberforge.Outline, Emberforge.StrokeThin);

            // Teal, because it is the way OUT of this page and into the formation. The one thing
            // this screen wants pressed is LEVEL UP, which is the ember one on the action bar.
            _openFormation = EmberforgeUI.Button("Formation", _left, "CHANGE BANNER",
                                                 UITheme.FontTiny, ForgeButtonKind.Secondary);
            UIBuilder.Box(_openFormation.Root, -UITheme.Space24, -UITheme.Space16,
                          BannerButtonWidth, 48f, new Vector2(1f, 1f));
            _openFormation.OnClick(OpenFormation);
        }

        private void BuildRightPanel()
        {
            _right = Panel("Sheet", RightX);

            float top = UITheme.Space24;

            // The hero's own badge: their aspect, on a diamond, exactly as the mockup draws it.
            RectTransform badgeHost = UIBuilder.Node("Badge", _right);
            UIBuilder.Box(badgeHost, UITheme.Space24, -top, 68f, 68f, new Vector2(0f, 1f));

            // THE KIT'S JEWEL, in two pieces, for the reason a currency pill's is in two: the
            // body is white and takes the aspect tint, the edge is an untinted gold rim over it.
            // A one-piece tinted diamond would tint its own dark outline and every aspect would
            // carry a differently coloured one.
            _aspectBadge = UIBuilder.Img("Plate", badgeHost,
                                         EmberforgeSkin.Get(EmberforgeArt.Slot.JewelBody), Color.white);
            UIBuilder.FillParent(_aspectBadge.rectTransform, 0f);

            Image badgeEdge = UIBuilder.Img("Edge", badgeHost,
                                            EmberforgeSkin.Get(EmberforgeArt.Slot.JewelEdge), Color.white);
            UIBuilder.FillParent(badgeEdge.rectTransform, 0f);

            _aspectBadgeGlyph = UIBuilder.Img("Glyph", badgeHost, TextureLab.Sigil(13, 128),
                                              Emberforge.TextOnGold);
            UIBuilder.Box(_aspectBadgeGlyph.rectTransform, 0f, 0f, 38f, 38f, new Vector2(0.5f, 0.5f));

            _starHost = UIBuilder.Node("Stars", _right);
            UIBuilder.Box(_starHost, UITheme.Space24 + 88f, -(top + 20f), 300f, 30f, new Vector2(0f, 1f));

            top += 72f;
            Divider(top);
            top += 10f;

            _statHost = UIBuilder.Node("Stats", _right);
            UIBuilder.Box(_statHost, 0f, -top, PanelWidth - UITheme.Space48, 6f * RowPitch, new Vector2(0.5f, 1f));

            top += 6f * RowPitch + UITheme.Space8;

            _aspectRow = UIBuilder.Node("Aspects", _right);
            UIBuilder.Box(_aspectRow, 0f, -top, PanelWidth - UITheme.Space48, 66f, new Vector2(0.5f, 1f));

            Image aspectBg = EmberforgeUI.SunkenOn(_aspectRow);
            aspectBg.raycastTarget = false;

            top += 66f + UITheme.Space8;

            _abilityHost = UIBuilder.Node("Abilities", _right);
            UIBuilder.Box(_abilityHost, 0f, -top, PanelWidth - UITheme.Space48, 96f, new Vector2(0.5f, 1f));

            // Says out loud when this page has outgrown the shortest canvas, for the reason
            // DesignGallery.CheckFits exists: the failure is SILENT on the machine it is authored
            // on. At 16:9 everything fits and the section that runs under the action bar only
            // appears on the 20:9 capture.
            float used = top + 96f;
            if (used > PanelHeight)
                Debug.LogError("HeroDetailScreen: the right panel needs " + used + " units and the"
                    + " shortest canvas this game targets leaves only " + PanelHeight
                    + ". It fits at 16:9 and runs under the action bar at 20:9.");
        }

        private RectTransform Panel(string name, float x)
        {
            RectTransform panel = UIBuilder.Node(name, Root);
            // Stretched top to bottom rather than Boxed at a fixed height - see the note on
            // PanelTop. x is still the panel's own centre on a 0.5 horizontal anchor.
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, -(PanelTop + ActionBarHeight + PanelBottomGap));
            panel.anchoredPosition = new Vector2(x, (ActionBarHeight + PanelBottomGap - PanelTop) * 0.5f);

            Image bg = EmberforgeUI.PanelOn(panel, true);
            bg.raycastTarget = false;
            return panel;
        }

        private void Divider(float top)
        {
            Image rule = EmberforgeUI.Rule("Rule" + top, _right);
            UIBuilder.Box(rule.rectTransform, 0f, -top, PanelWidth - UITheme.Space48,
                          Emberforge.RuleHeight, new Vector2(0.5f, 1f));
        }

        private void BuildActionBar()
        {
            RectTransform bar = UIBuilder.Node("Actions", Root);
            UIBuilder.BottomStrip(bar, ActionBarHeight, 0f, 0f);

            // A RECESS across the foot of the page, which is the same thing the top bar's
            // backing is and for the same reason: the two buttons sit ON this, and a framed panel
            // behind a framed button is two frames deep for one row of controls.
            Image backing = EmberforgeUI.SunkenOn(bar);
            backing.raycastTarget = false;

            _capNote = EmberforgeUI.Label("CapNote", bar, "", UITheme.FontTiny, Emberforge.TextQuiet,
                                          TextAnchor.UpperCenter);
            UIBuilder.Box(_capNote.rectTransform, 0f, -6f, 1200f, 24f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_capNote, Emberforge.Outline, Emberforge.StrokeThin);

            _levelUpButton = EmberforgeUI.Button("LevelUp", bar, "LEVEL UP", UITheme.FontHeading,
                                                 ForgeButtonKind.Primary);
            UIBuilder.Box(_levelUpButton.Root, -190f, 14f, 640f, 88f, new Vector2(0.5f, 0f));
            _levelUpButton.OnClick(delegate { LevelUp(1); });

            _levelUpTenButton = EmberforgeUI.Button("LevelUpTen", bar, "x10", UITheme.FontHeading,
                                                    ForgeButtonKind.Secondary);
            UIBuilder.Box(_levelUpTenButton.Root, 320f, 14f, 340f, 88f, new Vector2(0.5f, 0f));
            _levelUpTenButton.OnClick(delegate { LevelUp(10); });

            _obtainButton = EmberforgeUI.Button("Obtain", bar, "OPEN THE RIFT", UITheme.FontHeading,
                                                ForgeButtonKind.Primary);
            UIBuilder.Box(_obtainButton.Root, 0f, 14f, 980f, 88f, new Vector2(0.5f, 0f));
            _obtainButton.OnClick(GoToSummon);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            if (_hero == null && _lockedDef == null && App.Session.Profile.Heroes.Count > 0)
                _hero = App.Session.Profile.Heroes[0];
            App.RefreshHud();
            RebuildStatic();
            Refresh();
        }

        /// <summary>Rebuilds the parts that only change when the selected hero changes.</summary>
        private void RebuildStatic()
        {
            HeroDef def = CurrentDef;
            if (def == null) return;
            bool locked = _hero == null;

            Color accent = UITheme.AspectAccent(def.Aspect);

            for (int i = _portraitHost.childCount - 1; i >= 0; i--) Destroy(_portraitHost.GetChild(i).gameObject);
            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", _portraitHost, def,
                                                                def.Stars, PortraitSize);
            UIBuilder.FillParent(portrait, 0f);
            if (locked)
            {
                // dimmed silhouette - same identity below, but the portrait itself stays a shape,
                // not a reveal. Over the badge rather than under the plate, so the tier behind it
                // still reads: not owning a hero does not make them a different rarity.
                Image scrim = UIBuilder.Img("Scrim", portrait, TextureLab.Circle(160), UITheme.ScrimModal);
                UIBuilder.FillParent(scrim.rectTransform, PortraitSize * 0.07f);
            }

            _aspectBadge.color = accent;
            _aspectBadgeGlyph.sprite = TextureLab.Sigil(AspectSigil(def.Aspect), 128);
            _aspectBadgeGlyph.color = accent;

            for (int i = _starHost.childCount - 1; i >= 0; i--) Destroy(_starHost.GetChild(i).gameObject);
            Widgets.StarRow("Stars", _starHost, def.Stars, 26f);

            RebuildSlots(locked);
            RebuildAspectRow(def);
            RebuildAbilities(def);

            _nameLabel.text = def.Name;
            _titleLabel.text = def.Title.ToUpperInvariant() + "   -   " + UITheme.RoleName(def.Role)
                             + "   -   " + AspectChart.DisplayName(def.Aspect);
            _headerSubtitle.text = def.Lore;

            // Two independent axes, not three modes. `locked` is "the player does not have this
            // hero yet" and hides everything numeric; `_prologueView` is "this hero is not theirs to
            // change" and hides only what acts. The stat block and the slots stay up under the
            // prologue because reading them IS steps 4 and 5.
            bool acts = !locked && !_prologueView;
            _statHost.gameObject.SetActive(!locked);
            _slotHost.gameObject.SetActive(!locked);
            _attunementLabel.gameObject.SetActive(!locked);
            _slotStatus.gameObject.SetActive(acts);
            _openFormation.Root.gameObject.SetActive(acts);
            _levelUpButton.Root.gameObject.SetActive(acts);
            _levelUpTenButton.Root.gameObject.SetActive(acts);
            _obtainButton.Root.gameObject.SetActive(locked);
        }

        /// <summary>
        /// The six equip slots, three down each side of the hero.
        ///
        /// Positioned by hand around the portrait rather than laid out by a group, because the
        /// whole point of the arrangement is that they FRAME the hero: a layout group would decide
        /// their spacing from its own rules and the portrait would have to fit whatever gap was
        /// left. The x offsets are measured against the portrait's own half-width plus a margin, so
        /// the columns cannot creep into the art if the portrait size changes.
        /// </summary>
        private void RebuildSlots(bool locked)
        {
            for (int i = _slotHost.childCount - 1; i >= 0; i--) Destroy(_slotHost.GetChild(i).gameObject);
            if (locked) return;

            const float columnGap = 46f;
            float columnX = PortraitSize * 0.5f + columnGap + SlotSize * 0.5f;
            // 136 apart, not 126: the slot's name label sits BELOW its card, and at the old pitch
            // the 112px card plus a 20px label left 14px of gap - so every label straddled the top
            // edge of the card beneath it.
            float[] rowY = { 136f, 8f, -120f };

            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
            {
                ArmorSlot slot = ArmorInfo.AllSlots[i];
                bool leftSide = i < 3;
                float x = leftSide ? -columnX : columnX;
                float y = rowY[i % 3] + 34f;

                RectTransform host = UIBuilder.Node("Slot_" + slot, _slotHost);
                UIBuilder.Box(host, x, y, SlotSize, SlotSize, new Vector2(0.5f, 0.5f));

                ArmorPiece piece = App.Session.Profile.ArmorInSlot(_hero, slot);
                RectTransform card = piece != null
                    ? ItemCard.ForArmor("Card", host, SlotSize, piece)
                    : ItemCard.Vacant("Card", host, SlotSize, slot);
                UIBuilder.FillParent(card, 0f);

                Text label = EmberforgeUI.Label("Label", host, ItemCard.SlotName(slot),
                                                UITheme.FontTiny - 8, Emberforge.TextQuiet,
                                                TextAnchor.UpperCenter);
                UIBuilder.Box(label.rectTransform, 0f, -SlotSize * 0.5f - 13f, 170f, 20f, new Vector2(0.5f, 0.5f));
                UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);

                // Tapping a slot routes to the Inventory, which owns every equip action in the
                // game. Nothing is chosen here - choosing is what the screen being opened is for.
                Image target = host.gameObject.AddComponent<Image>();
                target.color = new Color(1f, 1f, 1f, 0.003f);
                target.rectTransform.SetAsFirstSibling();
                Button press = host.gameObject.AddComponent<Button>();
                press.transition = Selectable.Transition.None;
                press.targetGraphic = target;
                press.onClick.AddListener(OpenInventory);
            }
        }

        /// <summary>
        /// The six-aspect row: how this hero's own aspect fares against each.
        ///
        /// The pips are read from AspectChart, the same table the simulation uses - three for an
        /// advantage, two for neutral, one for a disadvantage - so the row is a real matchup
        /// readout rather than decoration. Nothing here computes a matchup itself; it asks
        /// AspectChart.Multiplier and counts.
        /// </summary>
        private void RebuildAspectRow(HeroDef def)
        {
            for (int i = _aspectRow.childCount - 1; i >= 0; i--) Destroy(_aspectRow.GetChild(i).gameObject);

            float width = PanelWidth - UITheme.Space48;
            float step = width / 6f;

            for (int i = 0; i < 6; i++)
            {
                Aspect other = (Aspect)i;
                Color accent = UITheme.AspectAccent(other);
                float x = -width * 0.5f + step * (i + 0.5f);

                Image icon = UIBuilder.Img("A" + i, _aspectRow, TextureLab.Sigil(AspectSigil(other), 128), accent);
                UIBuilder.Box(icon.rectTransform, x, 12f, 30f, 30f, new Vector2(0.5f, 0.5f));

                float multiplier = AspectChart.Multiplier(def.Aspect, other);
                int pips = multiplier > 1.001f ? 3 : (multiplier < 0.999f ? 1 : 2);

                for (int p = 0; p < 3; p++)
                {
                    Image dot = UIBuilder.Img("D" + i + "_" + p, _aspectRow, TextureLab.Circle(32),
                                              p < pips ? accent : Emberforge.TextQuiet);
                    UIBuilder.Box(dot.rectTransform, x - 18f + p * 18f, -17f, 12f, 12f, new Vector2(0.5f, 0.5f));
                }

                if (i < 5)
                {
                    Image sep = UIBuilder.Img("Sep" + i, _aspectRow, TextureLab.White,
                                              Emberforge.GoldDeep);
                    UIBuilder.Box(sep.rectTransform, x + step * 0.5f, 0f, 2f, 42f, new Vector2(0.5f, 0.5f));
                }
            }
        }

        private void RebuildAbilities(HeroDef def)
        {
            for (int i = _abilityHost.childCount - 1; i >= 0; i--) Destroy(_abilityHost.GetChild(i).gameObject);

            // Names and kinds rather than the ability objects: BasicAttack and Active are
            // AbilityDef but Passive is a PassiveDef, and the two share no base type. The card only
            // ever draws a label and a tint, so collecting those is both sufficient and honest
            // about the fact that these are three different kinds of thing.
            List<string> names = new List<string>();
            List<string> kinds = new List<string>();
            List<Color> tints = new List<Color>();

            if (def.BasicAttack != null) { names.Add(def.BasicAttack.Name); kinds.Add("BASIC"); tints.Add(UITheme.TextMuted); }
            if (def.Active != null) { names.Add(def.Active.Name); kinds.Add("ACTIVE"); tints.Add(UITheme.Energy); }
            // One card per passive rather than one card for "the passive": a hero can now carry
            // more than one, and the row already sizes itself from names.Count. No talent card -
            // talents are selected, not innate, and the screen that picks them does not exist yet.
            for (int i = 0; i < def.Passives.Count; i++)
            { names.Add(def.Passives[i].Name); kinds.Add("PASSIVE"); tints.Add(UITheme.StarColor(def.Stars)); }
            if (names.Count == 0) return;

            float width = PanelWidth - UITheme.Space48;
            float step = width / names.Count;
            float cardWidth = Mathf.Min(step - UITheme.Space16, 260f);

            for (int i = 0; i < names.Count; i++)
            {
                float x = -width * 0.5f + step * (i + 0.5f);
                RectTransform cell = UIBuilder.Node("Ability" + i, _abilityHost);
                UIBuilder.Box(cell, x, 0f, cardWidth, 96f, new Vector2(0.5f, 0.5f));

                Image bg = EmberforgeUI.SunkenOn(cell);
                bg.raycastTarget = false;

                // Glyph, kind, name - stacked with real gaps between them. The first pass anchored
                // the kind at the card's centre and the name off its bottom edge, and the two
                // overlapped by 11px: "BASIC" was printed across "Ashen Cleave".
                Image glyph = UIBuilder.Img("Glyph", cell, TextureLab.Sigil(def.Visual.Sigil, 128), tints[i]);
                UIBuilder.Box(glyph.rectTransform, 0f, 27f, 34f, 34f, new Vector2(0.5f, 0.5f));

                // The KIND is a chip now rather than a bare word, which is what the ability's
                // tint is left carrying on this card - the fill and the border it used to tint as
                // well are the kit's recess and the kit's outline, the same on all five cells.
                RectTransform kindChip = EmberforgeUI.Chip("Kind", cell, kinds[i], tints[i],
                                                           UITheme.FontTiny - 8);
                UIBuilder.Box(kindChip, 0f, 2f, Mathf.Min(cardWidth - 12f, 150f), 22f,
                              new Vector2(0.5f, 0.5f));

                Text nameLabel = EmberforgeUI.Body("Name", cell, names[i],
                                                   UITheme.FontTiny - 6, Emberforge.TextBody,
                                                   TextAnchor.UpperCenter);
                UIBuilder.Box(nameLabel.rectTransform, 0f, 4f, cardWidth - 16f, 32f,
                              new Vector2(0.5f, 0f));
                UIBuilder.Outlined(nameLabel, Emberforge.Outline, Emberforge.StrokeThin);
            }
        }

        private void Refresh()
        {
            if (_hero == null)
            {
                RefreshLocked();
                return;
            }
            GameSession session = App.Session;
            HeroDef def = _hero.Def;

            // The equipped relic is folded into the preview, not just into the battle. A common
            // relic's whole contribution is these numbers, so a card that showed the unrelicced
            // line would be showing a stat block the simulation does not use. Both calls take it,
            // so the per-level deltas stay honest too.
            RelicDef relic = string.IsNullOrEmpty(_hero.EquippedRelicId)
                           ? null : session.Content.GetRelic(_hero.EquippedRelicId);

            // Armor is folded in for exactly the reason the relic above it is.
            List<ArmorPiece> armor = session.Profile.EquippedArmorOf(_hero);
            ArmorConfig armorConfig = session.Content.Armor;

            UnitStats now = StatMath.ForHero(def, _hero.Level, relic, armor, armorConfig);
            int cap = session.HeroLevelCap;
            bool atCap = _hero.Level >= cap;
            UnitStats next = atCap ? now : StatMath.ForHero(def, _hero.Level + 1, relic, armor, armorConfig);

            RefreshArmorBlock(session);

            // The "/ cap" half is a progress readout against the player's Wayfarer rank, which
            // means nothing for a prologue hero and reads as broken next to one: a brand-new
            // account caps at 12, so Kael at 42 rendered as "LEVEL 42 / 12".
            _levelLabel.text = _prologueView
                ? "LEVEL " + _hero.Level
                : "LEVEL " + _hero.Level + "<color=#6C6382> / " + cap + "</color>";
            _powerLabel.text = "POWER " + now.Power;

            // The green "+n" previews answer "what does one more level buy?", which is a question
            // the prologue does not let you act on - so under the prologue they are suppressed the
            // same way hitting the level cap suppresses them.
            bool hideDeltas = atCap || _prologueView;
            RebuildStats(now, next, hideDeltas);

            int cost = StatMath.LevelUpCost(session.Balance, _hero.Level);
            bool affordable = !atCap && session.Profile.Gold >= cost;

            _levelUpButton.SetText(atCap ? "LEVEL CAP" : "LEVEL UP   " + cost + "G");
            _levelUpButton.Interactable = affordable;
            _levelUpTenButton.Interactable = affordable;

            // A gold price under a prologue hero would be a lie twice over: the player cannot buy
            // it, and the gold quoted is an account that does not exist yet in the story.
            _capNote.text = _prologueView
                ? "A thousand years of levels. None of them yours to spend."
                : atCap
                ? "Raise your Wayfarer rank in the campaign to lift the level cap."
                : "Next level costs " + cost + " gold  -  you hold " + session.Profile.Gold;

            int deployedSlot = session.Profile.TeamSlotOf(_hero.Uid);
            _slotStatus.text = deployedSlot >= 0
                ? "Standing in slot " + (deployedSlot + 1)
                  + (deployedSlot == 0 ? "  -  the front of the line" : "")
                : "On the bench";
            _slotStatus.color = deployedSlot >= 0 ? UITheme.Success : Emberforge.TextQuiet;
        }

        /// <summary>
        /// The six stat bars, as the mockup draws them: a glyph, a segmented track, the number.
        ///
        /// Six rows, not five, and Effect Hit and Effect Resist replace the old combined CRITICAL
        /// row - those two are what the mockup asks for and they are what armor and relics actually
        /// roll, so they are the two a player is comparing gear against.
        /// </summary>
        private void RebuildStats(UnitStats now, UnitStats next, bool hideDeltas)
        {
            for (int i = _statHost.childCount - 1; i >= 0; i--) Destroy(_statHost.GetChild(i).gameObject);

            StatRow(0, "HEALTH", TextureLab.Heart(64), UITheme.HealthAlly, now.MaxHp, next.MaxHp,
                    now.MaxHp / 14000f, false, hideDeltas);
            StatRow(1, "ATTACK", TextureLab.Sword(64), UITheme.Gold, now.Attack, next.Attack,
                    now.Attack / 1400f, false, hideDeltas);
            StatRow(2, "GUARD", TextureLab.Shield(64), UITheme.Shard, now.Defense, next.Defense,
                    now.Defense / 1000f, false, hideDeltas);
            StatRow(3, "SPEED", TextureLab.Boot(64), UITheme.Success, now.Speed, next.Speed,
                    now.Speed / 220f, false, hideDeltas);
            StatRow(4, "EFFECT HIT", TextureLab.Spark(64), UITheme.TextPrimary,
                    now.EffectHit, next.EffectHit, now.EffectHit, true, hideDeltas);
            StatRow(5, "EFFECT RESIST", TextureLab.SpiralIcon(64), UITheme.Mythic,
                    now.EffectResist, next.EffectResist, now.EffectResist, true, hideDeltas);
        }

        /// <summary>
        /// One stat row. `ratio` is DISPLAY ONLY - the printed number is the authority.
        ///
        /// A hero stat has no natural maximum to divide by, so the reference scales these ratios
        /// are built from are chosen for legibility rather than read from balance. They exist so
        /// six rows are comparable to each other at a glance and so a level-up visibly moves them;
        /// they are not a percentage of anything and must not be read as one. The percent stats are
        /// the exception - Effect Hit and Effect Resist are already 0..1 and are passed straight
        /// through.
        /// </summary>
        private void StatRow(int index, string label, Sprite icon, Color tint,
                             float value, float nextValue, float ratio, bool percent, bool hideDeltas)
        {
            // The NAME sits above the bar, not on it. In the first pass both were anchored to the
            // same y and the label was drawn straight through its own segments - "HEALTH" printed
            // across the health bar on every row.
            float rowTop = index * RowPitch;

            Text name = EmberforgeUI.Label("Name" + index, _statHost, label, UITheme.FontTiny - 8,
                                           Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.Box(name.rectTransform, 44f, -rowTop, 300f, 14f, new Vector2(0f, 1f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform row = ItemCard.StatBar("Stat" + index, _statHost, icon, tint, ratio, 8, StatBarHeight);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, -(rowTop + 16f));
            row.sizeDelta = new Vector2(PanelWidth - UITheme.Space48 - 250f, StatBarHeight);

            string shown = percent ? Pct(value) : Whole(value);
            Text valueLabel = EmberforgeUI.Label("Value" + index, _statHost, shown, UITheme.FontSmall,
                                                 Emberforge.TextBody, TextAnchor.MiddleRight);
            UIBuilder.Box(valueLabel.rectTransform, 0f, -(rowTop + 6f), 150f, 34f, new Vector2(1f, 1f));
            UIBuilder.Outlined(valueLabel, Emberforge.Outline, Emberforge.StrokeThin);

            float delta = nextValue - value;
            if (!hideDeltas && delta > (percent ? 0.0005f : 0.5f))
            {
                Text deltaLabel = EmberforgeUI.Label("Delta" + index, _statHost,
                                                     percent ? "+" + Pct(delta) : "+" + Mathf.RoundToInt(delta),
                                                     UITheme.FontTiny - 6, UITheme.Success,
                                                     TextAnchor.MiddleRight);
                UIBuilder.Box(deltaLabel.rectTransform, -152f, -(rowTop + 6f), 92f, 34f, new Vector2(1f, 1f));
                UIBuilder.Outlined(deltaLabel, Emberforge.Outline, Emberforge.StrokeThin);
            }
        }

        /// <summary>
        /// Repaints the slot cards and the attunement line for the current hero.
        ///
        /// The attunement text is the same sentence and the same AttunementsFor lookup it always
        /// was - only where it sits moved.
        /// </summary>
        private void RefreshArmorBlock(GameSession session)
        {
            // Deliberately does NOT rebuild the slot cards. RebuildStatic already did, and calling
            // it from here too meant six cards were built twice per OnShow - which, because Destroy
            // defers to end of frame, left twelve live slot objects overlapping each other for the
            // rest of that frame. Harmless by the next frame and invisible in a screenshot, but it
            // is double the work and it made the layout unmeasurable mid-frame.
            PlayerProfile profile = session.Profile;
            int matching = profile.AttunementCount(_hero);
            string aspectName = AspectChart.DisplayName(_hero.Def.Aspect);
            List<ArmorAttunementDef> met = session.Content.Armor.AttunementsFor(_hero.Def.Aspect, matching);

            if (met.Count == 0)
            {
                _attunementLabel.text = "Attunement  " + matching + "/6 " + aspectName
                                      + "  -  2 matching pieces unlocks the first bonus.";
                _attunementLabel.color = Emberforge.TextQuiet;
            }
            else
            {
                string names = "";
                for (int i = 0; i < met.Count; i++)
                    names += (names.Length > 0 ? ", " : "") + met[i].Name + " (" + met[i].Pieces + ")";
                _attunementLabel.text = "Attunement  " + matching + "/6 " + aspectName + "  -  " + names;
                _attunementLabel.color = UITheme.AspectAccent(_hero.Def.Aspect);
            }
        }

        /// <summary>No HeroInstance exists for a locked hero, so there's no level/power/deploy state to show.</summary>
        private void RefreshLocked()
        {
            if (_lockedDef == null) return;
            _levelLabel.text = "";
            _powerLabel.text = "LOCKED";
            _attunementLabel.text = "";
            _capNote.text = "Not yet summoned. Open the Rift and try for " + _lockedDef.Name + ".";
        }

        /// <summary>
        /// Which of TextureLab's emblems stands for an aspect. Mapped by MEANING, not by casting
        /// the enum - the same table RecitalScreen and InventoryScreen use.
        /// </summary>
        private static int AspectSigil(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return 7;
                case Aspect.Tide: return 12;
                case Aspect.Verdant: return 2;
                case Aspect.Gale: return 8;
                case Aspect.Umbra: return 6;
                case Aspect.Lumen: return 13;
            }
            return 13;
        }

        private static string Whole(float value)
        {
            return Mathf.RoundToInt(value).ToString();
        }

        private static string Pct(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }

        // ------------------------------------------------------------------ actions

        private void OpenFormation()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Formation);
        }

        /// <summary>Routes to the Inventory, which owns every equip action in the game. Shows the
        /// screen and nothing else - no state is set and no piece is chosen here, because choosing
        /// is what the screen being opened is for.</summary>
        private void OpenInventory()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Inventory);
        }

        private void LevelUp(int times)
        {
            if (_hero == null) return;
            LevelUpResult result = App.Session.LevelUpHero(_hero, times);
            if (!result.Success)
            {
                App.Notify(result.Message);
                return;
            }
            App.Audio.Reward();
            App.Notify(_hero.Def.Name + " reached level " + result.NewLevel + ".");
            App.RefreshHud();
            App.RequestSave();
            Refresh();
        }

        private void GoToSummon()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Summon);
        }
    }
}
