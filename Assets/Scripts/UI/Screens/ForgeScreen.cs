using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Forge: one piece on the anvil, what it would become beside it, and the three actions
    /// over the owned-armor pool - Salvage, Reroll, Combine (DESIGN-ARMOR section 7).
    ///
    /// Equip is deliberately NOT here. It is a per-hero decision made in the presence of the hero
    /// it affects, so it stays on InventoryScreen's picker. Reroll, Combine and Salvage need no
    /// hero in context at all - they are bulk decisions over the whole pool.
    ///
    /// ---- visual pass ----
    /// ONE PIECE AT A TIME, NOT A LIST OF EVERY PIECE. This screen used to be a scroll of every
    /// owned piece with three buttons on each row, which meant the number of controls on screen
    /// grew with the size of the collection and no single piece was ever the subject. It is now an
    /// anvil: a slot filter down the left, a tray of the pieces in that slot, ONE of them focused
    /// at size, and the three actions acting on that one. Same three calls into GameSession, same
    /// preconditions, same refusal text - what changed is that the player picks the subject first
    /// and the actions are unambiguous about what they will act on.
    ///
    /// COMBINE'S PARTNERS ARE VISIBLE. The two matching pieces a Combine would consume flank the
    /// focused piece, drawn with the same ItemCard component at a smaller size. Combine's real
    /// precondition - three pieces sharing a slot AND a temper - was previously a sentence under a
    /// disabled button; it is now the picture.
    ///
    /// WHAT THE COMPARISON PANEL CAN HONESTLY SHOW. Combine's output temper and substat count are
    /// deterministic (ArmorInfo.NextTemper, ArmorInfo.SubStatCount), so those are previewed. The
    /// ROLLS are not - they are rolled at the anvil - and the panel says so rather than drawing
    /// invented numbers. Reroll and Salvage have no previewable "after" at all and the panel states
    /// what they do instead. Nothing here simulates anything; predicting a roll would have meant
    /// reaching into logic this pass was told not to touch.
    /// </summary>
    public sealed class ForgeScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Forge; } }

        private const float RailX = -872f;
        private const float RailWidth = 116f;
        private const float AnvilX = -232f;
        private const float FocusSize = 196f;
        private const float PartnerSize = 108f;
        private const float TraySize = 92f;
        private const float PanelX = 620f;
        private const float PanelWidth = 560f;

        /// <summary>
        /// Heading plus a tray tall enough for TWO full rows: 30 + 8 + 92 + 12 + 92 + 8. At the
        /// first pass's 178 the second row was sliced through the middle by the viewport mask,
        /// which reads as a rendering fault rather than as an invitation to scroll.
        /// </summary>
        private const float TrayBlock = 242f;

        private Text _headerSubtitle;
        private Text _slagValue;

        private RectTransform _rail;
        private RectTransform _anvil;
        private RectTransform _tray;
        private RectTransform _panel;
        private RectTransform _actions;

        private readonly List<ForgeTab> _railButtons = new List<ForgeTab>();

        /// <summary>Presentation state only. -1 is "every slot"; nothing here reaches the profile.</summary>
        private int _slotFilter = -1;
        private string _selectedUid = "";

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "FORGE", Router, out _headerSubtitle);

            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space8;
            BuildSlagPlate(top);

            top += 56f + UITheme.Space16;
            BuildRail(top);
            BuildAnvil(top);
            BuildTray(top + FocusSize + UITheme.Space32);
            BuildActions(top + FocusSize + UITheme.Space32 + TrayBlock + UITheme.Space24);
            BuildPanel(top);
        }

        private void BuildSlagPlate(float top)
        {
            RectTransform plate = UIBuilder.Node("SlagPlate", Root);
            UIBuilder.Box(plate, AnvilX, -top, 460f, 56f, new Vector2(0.5f, 1f));

            // A recess, like every other readout of a number in the kit: the currency strip's
            // cells and the top bar's band are the same surface, so a purse reads as a purse
            // wherever the game shows one.
            Image bg = EmberforgeUI.SunkenOn(plate);
            bg.raycastTarget = false;

            Image icon = UIBuilder.Img("Icon", plate, TextureLab.IngotIcon(64), UITheme.Slag);
            UIBuilder.Box(icon.rectTransform, UITheme.Space16, 0f, 30f, 30f, new Vector2(0f, 0.5f));

            _slagValue = EmberforgeUI.Label("Slag", plate, "", UITheme.FontSmall,
                                            Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_slagValue.rectTransform, UITheme.Space16 + 30f + UITheme.Space12, 0f,
                                 UITheme.Space16, 0f);
            UIBuilder.Outlined(_slagValue, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>The slot filter, down the left, as the mockup draws it.</summary>
        private void BuildRail(float top)
        {
            _rail = UIBuilder.Node("Rail", Root);
            UIBuilder.Box(_rail, RailX, -top, RailWidth, 7f * 82f, new Vector2(0.5f, 1f));

            Image bg = EmberforgeUI.SunkenOn(_rail);
            bg.raycastTarget = false;

            AddRailButton(0, -1, TextureLab.Chest(64), "ALL");
            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
                AddRailButton(i + 1, i, ItemCard.SlotIcon(ArmorInfo.AllSlots[i]),
                              ItemCard.ShortSlotName(ArmorInfo.AllSlots[i]));
        }

        /// <summary>
        /// One slot on the filter rail, as a TAB. The inventory's rail is the same control for the
        /// same reason: seven buttons in a column is seven things to press and no statement about
        /// which one you are in, and the selected state is a different plate rather than a tint on
        /// a baked bevel.
        /// </summary>
        private void AddRailButton(int index, int slotFilter, Sprite icon, string tip)
        {
            int captured = slotFilter;
            ForgeTab tab = EmberforgeUI.Tab("Rail" + index, _rail, 92f, 74f,
                                            delegate { SetFilter(captured); });
            UIBuilder.Box(tab.Root, 0f, -(8f + index * 82f), 92f, 74f, new Vector2(0.5f, 1f));
            tab.Caption.gameObject.SetActive(false);

            Image glyph = UIBuilder.Img("Glyph", tab.Root, icon, Emberforge.TextQuiet);
            UIBuilder.Box(glyph.rectTransform, 0f, 6f, 34f, 34f, new Vector2(0.5f, 0.5f));

            Text label = EmberforgeUI.Label("Tip", tab.Root, tip, UITheme.FontTiny - 8,
                                            Emberforge.TextQuiet, TextAnchor.LowerCenter);
            UIBuilder.Box(label.rectTransform, 0f, 4f, 88f, 18f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);

            _railButtons.Add(tab);
        }

        /// <summary>The anvil: the focused piece, and the two a Combine would consume beside it.</summary>
        private void BuildAnvil(float top)
        {
            _anvil = UIBuilder.Node("Anvil", Root);
            UIBuilder.Box(_anvil, AnvilX, -top, 700f, FocusSize + 8f, new Vector2(0.5f, 1f));
        }

        private void BuildTray(float top)
        {
            Text heading = EmberforgeUI.Label("TrayHeading", Root, "", UITheme.FontTiny,
                                           Emberforge.TextQuiet, TextAnchor.UpperLeft);
            // Centred on the tray with left-aligned TEXT. See InventoryScreen's own note: Box's
            // anchor doubles as its pivot, so a (0,1) anchor measures x from the canvas edge.
            UIBuilder.Box(heading.rectTransform, AnvilX, -top, 700f, 26f, new Vector2(0.5f, 1f));
            heading.name = "TrayHeading";

            ScrollRect scroll;
            _tray = UIBuilder.ScrollGrid("Tray", Root, out scroll,
                                         new Vector2(TraySize, TraySize),
                                         new Vector2(UITheme.Space12, UITheme.Space12), 7,
                                         new RectOffset(8, 8, 8, 8));
            RectTransform shell = scroll.GetComponent<RectTransform>();
            UIBuilder.Box(shell, AnvilX, -(top + 30f), 700f, TrayBlock - 30f, new Vector2(0.5f, 1f));
        }

        private void BuildActions(float top)
        {
            _actions = UIBuilder.Node("Actions", Root);
            UIBuilder.Box(_actions, AnvilX, -top, 700f, 132f, new Vector2(0.5f, 1f));
        }

        private void BuildPanel(float top)
        {
            _panel = UIBuilder.Node("Compare", Root);
            UIBuilder.Box(_panel, PanelX, -top, PanelWidth, 628f, new Vector2(0.5f, 1f));

            Image bg = EmberforgeUI.PanelOn(_panel, true);
            bg.raycastTarget = false;
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            Rebuild();
        }

        private void SetFilter(int slotFilter)
        {
            if (_slotFilter == slotFilter) return;
            _slotFilter = slotFilter;
            _selectedUid = "";
            App.Audio.Tap();
            Rebuild();
        }

        private void Select(string uid)
        {
            if (_selectedUid == uid) return;
            _selectedUid = uid;
            App.Audio.Tap();
            Rebuild();
        }

        /// <summary>The pool this screen is currently looking at, sorted so a Combine trio clusters.</summary>
        private List<ArmorPiece> Filtered()
        {
            List<ArmorPiece> result = new List<ArmorPiece>();
            List<ArmorPiece> owned = App.Session.Profile.OwnedArmor;
            for (int i = 0; i < owned.Count; i++)
            {
                if (_slotFilter >= 0 && owned[i].Slot != ArmorInfo.AllSlots[_slotFilter]) continue;
                result.Add(owned[i]);
            }

            // Stable insertion sort by slot then temper, so three matching pieces sit together
            // rather than scattering across acquisition order - the same reason the old list was
            // sorted, kept because it is what makes a Combine findable at a glance.
            for (int i = 1; i < result.Count; i++)
            {
                ArmorPiece key = result[i];
                int keyRank = (int)key.Slot * 8 + (int)key.Temper;
                int j = i - 1;
                while (j >= 0 && (int)result[j].Slot * 8 + (int)result[j].Temper > keyRank)
                {
                    result[j + 1] = result[j];
                    j--;
                }
                result[j + 1] = key;
            }
            return result;
        }

        private void Rebuild()
        {
            PlayerProfile profile = App.Session.Profile;
            _slagValue.text = "SLAG   " + profile.Slag;

            int owned = profile.OwnedArmor.Count;
            _headerSubtitle.text = owned + (owned == 1 ? " PIECE OWNED" : " PIECES OWNED");

            for (int i = 0; i < _railButtons.Count; i++)
            {
                bool active = (i - 1) == _slotFilter;
                EmberforgeUI.SetTabSelected(_railButtons[i], active);
                Transform glyph = _railButtons[i].Root.Find("Glyph");
                if (glyph != null)
                    glyph.GetComponent<Image>().color = active ? Emberforge.TextGold : Emberforge.TextQuiet;
                Transform tip = _railButtons[i].Root.Find("Tip");
                if (tip != null)
                    tip.GetComponent<Text>().color = active ? Emberforge.TextGold : Emberforge.TextQuiet;
            }

            List<ArmorPiece> pool = Filtered();
            if (_selectedUid == "" && pool.Count > 0) _selectedUid = pool[0].Uid;

            ArmorPiece focus = profile.FindArmor(_selectedUid);
            if (focus != null && !pool.Contains(focus)) focus = pool.Count > 0 ? pool[0] : null;
            if (focus != null) _selectedUid = focus.Uid;

            RebuildTray(pool, focus);
            RebuildAnvil(focus);
            RebuildActions(focus);
            RebuildPanel(focus);
        }

        private void RebuildTray(List<ArmorPiece> pool, ArmorPiece focus)
        {
            Transform heading = Root.Find("TrayHeading");
            if (heading != null)
                heading.GetComponent<Text>().text = _slotFilter < 0
                    ? "ALL PIECES  (" + pool.Count + ")"
                    : ArmorInfo.SlotLabel(ArmorInfo.AllSlots[_slotFilter]).ToUpperInvariant()
                      + "  (" + pool.Count + ")";

            for (int i = _tray.childCount - 1; i >= 0; i--) Object.Destroy(_tray.GetChild(i).gameObject);

            for (int i = 0; i < pool.Count; i++)
            {
                ArmorPiece piece = pool[i];
                RectTransform cell = UIBuilder.Node("Tray_" + piece.Uid, _tray);

                RectTransform card = ItemCard.ForArmor("Card", cell, TraySize, piece);
                UIBuilder.FillParent(card, 0f);

                // The selected piece is lifted rather than recoloured: recolouring would fight the
                // temper border, which is the one thing the card's frame is for.
                if (focus != null && piece.Uid == focus.Uid)
                {
                    Image halo = UIBuilder.Img("Selected", cell, TextureLab.Glow(96, 1.6f),
                                               UITheme.WithAlpha(UITheme.Gold, 0.30f));
                    UIBuilder.FillParent(halo.rectTransform, -12f);
                    halo.rectTransform.SetAsFirstSibling();
                }

                if (App.Session.Profile.HeroWearingArmor(piece.Uid) != null)
                {
                    Image worn = UIBuilder.Img("Worn", cell, TextureLab.Check(32), UITheme.Success);
                    UIBuilder.Box(worn.rectTransform, -4f, -4f, 20f, 20f, new Vector2(1f, 1f));
                }

                Button press = cell.gameObject.AddComponent<Button>();
                press.transition = Selectable.Transition.None;
                Image target = cell.gameObject.AddComponent<Image>();
                target.color = new Color(1f, 1f, 1f, 0.003f);   // needs a graphic to take the tap
                press.targetGraphic = target;
                target.rectTransform.SetAsFirstSibling();

                string captured = piece.Uid;
                press.onClick.AddListener(delegate { Select(captured); });
            }
        }

        /// <summary>The focused piece, flanked by the two a Combine would consume.</summary>
        private void RebuildAnvil(ArmorPiece focus)
        {
            for (int i = _anvil.childCount - 1; i >= 0; i--) Object.Destroy(_anvil.GetChild(i).gameObject);

            if (focus == null)
            {
                Text none = EmberforgeUI.Label("None", _anvil, "NOTHING ON THE ANVIL",
                                            UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }

            List<ArmorPiece> partners = App.Session.Profile.ArmorCombineCandidates(focus);

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -252f : 252f;
                RectTransform host = UIBuilder.Node("Partner" + i, _anvil);
                UIBuilder.Box(host, x, 0f, PartnerSize, PartnerSize, new Vector2(0.5f, 0.5f));

                if (i < partners.Count)
                {
                    RectTransform card = ItemCard.ForArmor("Card", host, PartnerSize, partners[i]);
                    UIBuilder.FillParent(card, 0f);
                }
                else
                {
                    RectTransform card = ItemCard.Vacant("Card", host, PartnerSize, focus.Slot);
                    UIBuilder.FillParent(card, 0f);
                    Text q = EmberforgeUI.Label("Need", host, "NEEDED", UITheme.FontTiny - 6,
                                             Emberforge.TextQuiet, TextAnchor.UpperCenter);
                    UIBuilder.Box(q.rectTransform, 0f, -PartnerSize * 0.5f - 14f, 160f, 20f, new Vector2(0.5f, 0.5f));
                }

                // The elbow joining a partner to the anvil, as the mockup draws it.
                Image link = UIBuilder.Img("Link" + i, _anvil, TextureLab.White,
                                           UITheme.WithAlpha(UITheme.ChromeBrass, 0.7f));
                UIBuilder.Box(link.rectTransform, x * 0.5f, 0f, 140f, 3f, new Vector2(0.5f, 0.5f));
                link.rectTransform.SetAsFirstSibling();
            }

            RectTransform focusHost = UIBuilder.Node("Focus", _anvil);
            UIBuilder.Box(focusHost, 0f, 0f, FocusSize, FocusSize, new Vector2(0.5f, 0.5f));
            RectTransform focusCard = ItemCard.ForArmor("Card", focusHost, FocusSize, focus);
            UIBuilder.FillParent(focusCard, 0f);

            Text name = EmberforgeUI.Label("Name", _anvil, focus.Name, UITheme.FontSmall,
                                        Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(name.rectTransform, 0f, -FocusSize * 0.5f - 6f, 560f, 30f, new Vector2(0.5f, 0.5f));
        }

        private void RebuildActions(ArmorPiece focus)
        {
            for (int i = _actions.childCount - 1; i >= 0; i--) Object.Destroy(_actions.GetChild(i).gameObject);
            if (focus == null) return;

            HeroInstance wearer = App.Session.Profile.HeroWearingArmor(focus.Uid);
            bool equipped = wearer != null;

            // ---- SALVAGE
            int salvageValue = App.Session.SalvageValue(focus);
            UIButton salvage = Action("Salvage", -234f, "SALVAGE", "+" + salvageValue + " SLAG",
                                      UITheme.Danger, TextureLab.IngotIcon(64));
            salvage.Interactable = !equipped;
            string uid = focus.Uid;
            salvage.OnClick(delegate { OnSalvage(uid); });
            Reason(_actions, "ReasonSalvage", -234f, equipped ? "Unequip it first" : "");

            // ---- REROLL
            int rerollCost = App.Session.RerollCost(focus);
            UIButton reroll = Action("Reroll", 0f, "REROLL", rerollCost + " SLAG",
                                     UITheme.Gold, TextureLab.SpiralIcon(64));
            reroll.Interactable = App.Session.Profile.FreeSlag >= rerollCost && rerollCost > 0 && !focus.Sealed;
            reroll.OnClick(delegate { OnReroll(uid); });
            Reason(_actions, "ReasonReroll", 0f,
                   rerollCost <= 0 ? "Cannot be rerolled" : SlagShortfall(rerollCost));

            // ---- COMBINE. Its edge previews the OUTPUT temper's colour.
            ArmorTemper? nextTemper = ArmorInfo.NextTemper(focus.Temper);
            List<ArmorPiece> partners = App.Session.Profile.ArmorCombineCandidates(focus);
            int combineCost = nextTemper.HasValue ? App.Session.CombineCost(nextTemper.Value) : 0;
            Color combineEdge = nextTemper.HasValue ? UITheme.ArmorTemperColor(nextTemper.Value) : UITheme.Border;

            UIButton combine = Action("Combine", 234f, "COMBINE",
                                      nextTemper.HasValue ? combineCost + " SLAG" : "MAX TIER",
                                      combineEdge, TextureLab.Swords(64));
            // !equipped is required here, not just on the two partners ArmorCombineCandidates
            // already filters: GameSession.CombineArmor refuses ALL THREE inputs if any one of them
            // is worn, including this piece itself.
            combine.Interactable = !equipped && nextTemper.HasValue && partners.Count == 2
                                 && App.Session.Profile.FreeSlag >= combineCost;
            string uidB = partners.Count > 0 ? partners[0].Uid : "";
            string uidC = partners.Count > 1 ? partners[1].Uid : "";
            combine.OnClick(delegate { OnCombine(uid, uidB, uidC); });
            Reason(_actions, "ReasonCombine", 234f,
                   CombineReason(equipped, nextTemper.HasValue, partners.Count, combineCost));
        }

        /// <summary>
        /// One anvil action. TEAL, all three, and the tint the caller passes reaches only the
        /// GLYPH now.
        ///
        /// It used to colour each button's own border - green for salvage, amber for reroll, gold
        /// for combine - which is three button voices on one row, and the kit has two. The row's
        /// job is to offer three equal choices rather than to say which is the thing to press, so
        /// none of them is the ember one; what tells them apart is the mark and the word on each.
        /// </summary>
        private UIButton Action(string name, float x, string title, string cost, Color edge, Sprite icon)
        {
            UIButton button = EmberforgeUI.Button(name, _actions, "", UITheme.FontSmall,
                                                  ForgeButtonKind.Secondary);
            UIBuilder.Box(button.Root, x, 0f, 220f, 96f, new Vector2(0.5f, 1f));

            Image glyph = UIBuilder.Img("Glyph", button.Root, icon, edge);
            UIBuilder.Box(glyph.rectTransform, UITheme.Space16, 0f, 34f, 34f, new Vector2(0f, 0.5f));

            // The title sits ABOVE the centre line and the cost below it, with real space
            // between. Both used to be within seven units of each other and the cost's box ran
            // through the title's baseline, which printed a line across the word - the first
            // capture of this screen shows SALVAGE with a rule through it.
            button.Label.alignment = TextAnchor.LowerLeft;
            UIBuilder.Box(button.Label.rectTransform, 62f, 12f, 148f, 30f, new Vector2(0f, 0.5f));
            button.Label.text = title;

            UIBuilder.Outlined(button.Label, Emberforge.Outline, Emberforge.StrokeThin);

            button.SubLabel = EmberforgeUI.Label("Cost", button.Root, cost, UITheme.FontTiny - 2,
                                                 Emberforge.TextOnColor, TextAnchor.UpperLeft);
            UIBuilder.Box(button.SubLabel.rectTransform, 62f, -22f, 148f, 26f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(button.SubLabel, Emberforge.Outline, Emberforge.StrokeThin);
            button.SubLabel.raycastTarget = false;
            return button;
        }

        /// <summary>
        /// One line under a disabled action saying why it is disabled, or nothing at all when the
        /// action is available.
        ///
        /// A first visit to this screen presents three priced buttons and no way to press any of
        /// them: Combine and Reroll want Slag the player has none of, and Salvage - the one action
        /// that PAYS Slag - is refused because the piece is worn. Three dead buttons with prices on
        /// them read as a broken screen rather than a locked one.
        /// </summary>
        private static void Reason(RectTransform parent, string name, float x, string text)
        {
            Text label = EmberforgeUI.Label(name, parent, text, UITheme.FontTiny - 4, Emberforge.TextQuiet,
                                         TextAnchor.UpperCenter);
            UIBuilder.Box(label.rectTransform, x, -102f, 232f, 28f, new Vector2(0.5f, 1f));
        }

        /// <summary>
        /// Before, and what a Combine would make of it.
        ///
        /// Only Combine gets an "after" because only Combine has one that can be stated without
        /// guessing: ArmorInfo.NextTemper gives the rung and ArmorInfo.SubStatCount gives how many
        /// substats it will carry, both deterministic. The VALUES are rolled at the anvil, and the
        /// panel says that in words rather than drawing numbers it would have had to invent.
        /// </summary>
        private void RebuildPanel(ArmorPiece focus)
        {
            for (int i = _panel.childCount - 1; i >= 0; i--)
            {
                Transform child = _panel.GetChild(i);
                if (child.name == "Edge") continue;
                Object.Destroy(child.gameObject);
            }

            if (focus == null)
            {
                Text none = EmberforgeUI.Label("None", _panel, "SELECT A PIECE",
                                            UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }

            float top = UITheme.Space24;
            top = BuildCompareBlock("Before", focus, focus.Temper, top, false);

            ArmorTemper? next = ArmorInfo.NextTemper(focus.Temper);

            Image arrow = UIBuilder.Img("Arrow", _panel, TextureLab.Diamond(48),
                                        UITheme.WithAlpha(next.HasValue ? UITheme.Gold : Emberforge.TextQuiet, 0.9f));
            UIBuilder.Box(arrow.rectTransform, 0f, -top, 26f, 26f, new Vector2(0.5f, 1f));
            top += 26f + UITheme.Space16;

            if (!next.HasValue)
            {
                Text max = EmberforgeUI.Label("Max", _panel, "ALREADY THE TOP RUNG",
                                           UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(max.rectTransform, 0f, -top, PanelWidth - 40f, 30f, new Vector2(0.5f, 1f));
                return;
            }

            top = BuildCompareBlock("After", focus, next.Value, top, true);

            Text note = EmberforgeUI.Body("Note", _panel,
                "Rung and substat count are fixed. The rolls themselves are rolled at the anvil.",
                UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(note.rectTransform, 0f, -top, PanelWidth - 56f, 42f, new Vector2(0.5f, 1f));
        }

        private float BuildCompareBlock(string name, ArmorPiece piece, ArmorTemper temper, float top, bool preview)
        {
            RectTransform block = UIBuilder.Node(name, _panel);
            UIBuilder.Box(block, 0f, -top, PanelWidth - 40f, CompareBlock, new Vector2(0.5f, 1f));

            Color tint = UITheme.ArmorTemperColor(temper);

            RectTransform host = UIBuilder.Node("CardHost", block);
            UIBuilder.Box(host, UITheme.Space16, -UITheme.Space8, 116f, 116f, new Vector2(0f, 1f));

            // The SHARED component, at a third size, on a third screen. Same frame, same pip row.
            RectTransform card = ItemCard.Build("Card", host, 116f, ItemCard.SlotIcon(piece.Slot),
                                                tint, (int)temper + 1, 4,
                                                UITheme.WithAlpha(UITheme.AspectAccent(piece.Aspect), 0.16f));
            UIBuilder.FillParent(card, 0f);

            Text rung = EmberforgeUI.Label("Rung", block, ArmorInfo.TemperLabel(temper).ToUpperInvariant(),
                                        UITheme.FontSmall, tint, TextAnchor.UpperLeft);
            UIBuilder.Box(rung.rectTransform, 116f + UITheme.Space32, -UITheme.Space8, 300f, 30f, new Vector2(0f, 1f));

            int subs = ArmorInfo.SubStatCount(temper);
            Text detail = EmberforgeUI.Label("Detail", block,
                                          subs + (subs == 1 ? " SUBSTAT" : " SUBSTATS"),
                                          UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(detail.rectTransform, 116f + UITheme.Space32, -42f, 300f, 26f, new Vector2(0f, 1f));

            // The piece's actual lines. On the preview block these are the CURRENT rolls, dimmed and
            // labelled as such, because the new ones do not exist until the anvil rolls them.
            // The stat rows sit BELOW the card, not beside it. Beside it they shared the card's
            // x range and were drawn straight across the artwork, which is what the first pass
            // shipped: the bars started at the block's left edge and so did the card.
            List<ArmorStatRoll> rolls = piece.AllRolls();
            float rowTop = 132f;
            int shown = Mathf.Min(rolls.Count, 3);
            for (int i = 0; i < shown; i++)
            {
                ArmorStatRoll roll = rolls[i];
                RectTransform row = ItemCard.StatBar("Stat" + i, block, StatIcon(roll.Stat),
                                                     preview ? UITheme.WithAlpha(tint, 0.35f) : tint,
                                                     DisplayRatio(roll), 6, 24f);
                UIBuilder.Box(row, UITheme.Space16, -rowTop,
                              PanelWidth - 40f - UITheme.Space32 - 190f, 24f, new Vector2(0f, 1f));

                Text value = EmberforgeUI.Label("Value", block, ArmorInfo.StatLabel(roll.Stat) + "  " + RollText(roll),
                                             UITheme.FontTiny - 6,
                                             preview ? Emberforge.TextQuiet : Emberforge.TextQuiet, TextAnchor.MiddleRight);
                UIBuilder.Box(value.rectTransform, -UITheme.Space16, -rowTop, 182f, 24f, new Vector2(1f, 1f));
                rowTop += 30f;
            }

            if (preview)
            {
                Text pending = EmberforgeUI.Label("Pending", block, "CURRENT ROLLS SHOWN - THESE REROLL",
                                               UITheme.FontTiny - 8, Emberforge.TextQuiet, TextAnchor.UpperLeft);
                UIBuilder.Box(pending.rectTransform, UITheme.Space16, -rowTop, 420f, 22f, new Vector2(0f, 1f));
            }

            return top + CompareBlock;
        }

        /// <summary>Card (116) plus its 8px inset, then three 30px stat rows and a pending note.</summary>
        private const float CompareBlock = 238f;

        private static Sprite StatIcon(ArmorStat stat)
        {
            switch (stat)
            {
                case ArmorStat.MaxHp: return TextureLab.Heart(64);
                case ArmorStat.Defense: return TextureLab.Shield(64);
                case ArmorStat.Speed: return TextureLab.Boot(64);
                case ArmorStat.EffectHit: return TextureLab.Spark(64);
                case ArmorStat.EffectResist: return TextureLab.SpiralIcon(64);
            }
            return TextureLab.Sword(64);
        }

        private static string RollText(ArmorStatRoll roll)
        {
            return ArmorInfo.IsPercentStat(roll.Stat)
                ? "+" + Mathf.RoundToInt(roll.Value * 100f) + "%"
                : "+" + Mathf.RoundToInt(roll.Value);
        }

        /// <summary>
        /// How full to draw a roll's bar.
        ///
        /// DISPLAY ONLY, and the printed number beside it is the authority. A roll has no natural
        /// maximum to divide by - there is no "max possible Attack roll" exposed anywhere - so
        /// these reference scales are chosen for legibility, not read from balance. They exist so
        /// two pieces sitting side by side in the comparison panel are comparable at a glance; they
        /// are not a percentage of anything and must not be read as one.
        /// </summary>
        private static float DisplayRatio(ArmorStatRoll roll)
        {
            if (ArmorInfo.IsPercentStat(roll.Stat)) return roll.Value / 0.45f;
            if (roll.Stat == ArmorStat.MaxHp) return roll.Value / 1200f;
            return roll.Value / 260f;
        }

        /// <summary>
        /// Why COMBINE is unavailable, in the order the player would hit the walls: a top-tier piece
        /// can never combine, a worn one is refused outright by GameSession.CombineArmor, then the
        /// two things they can actually go and fix - finding partners, and affording it.
        /// </summary>
        private string CombineReason(bool equipped, bool hasNextTemper, int partnerCount, int cost)
        {
            if (!hasNextTemper) return "Already the top tier";
            if (equipped) return "Unequip it first";
            if (partnerCount < 2) return "Needs " + (2 - partnerCount) + " more matching";
            return SlagShortfall(cost);
        }

        /// <summary>How much more Slag is needed, or nothing when it is already affordable. Names
        /// the SHORTFALL rather than the price, because the price is already on the button.</summary>
        private string SlagShortfall(int cost)
        {
            long have = App.Session.Profile.FreeSlag;
            if (have >= cost) return "";
            return "Needs " + (cost - have) + " more Slag";
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Re-resolves the piece and its next temper here rather than trusting
        /// closure-captured values - the profile can only have changed since the button was built by
        /// another action from THIS screen, and Rebuild runs after every one of those, but
        /// re-resolving costs nothing and removes the assumption entirely.</summary>
        private void OnCombine(string armorUidA, string armorUidB, string armorUidC)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUidA);
            if (piece == null) { Rebuild(); return; }

            ArmorTemper? next = ArmorInfo.NextTemper(piece.Temper);
            int cost = next.HasValue ? App.Session.CombineCost(next.Value) : 0;
            string slot = ArmorInfo.SlotLabel(piece.Slot);

            if (!App.Session.CombineArmor(armorUidA, armorUidB, armorUidC, TimeUtil.NowUnix()))
            {
                App.Notify(next.HasValue
                    ? "Combining needs three matching " + slot + " pieces and " + cost + " Slag."
                    : "Already at the top of the ladder.");
                return;
            }

            App.Audio.Tap();
            App.Notify("Forged a " + (next.HasValue ? ArmorInfo.TemperLabel(next.Value) + " " : "") + slot + ".");
            // The three inputs are consumed, so the focus must let go of a uid that no longer
            // resolves; Rebuild re-seats it on whatever the filter still holds.
            _selectedUid = "";
            App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }

        private void OnReroll(string armorUid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUid);
            if (piece == null) { Rebuild(); return; }

            int cost = App.Session.RerollCost(piece);
            if (!App.Session.RerollArmor(armorUid, TimeUtil.NowUnix()))
            {
                App.Notify("Not enough Slag - the Forge wants " + cost + ".");
                return;
            }

            App.Audio.Tap();
            App.Notify(piece.Name + " reforged: " + piece.DescribeStats());
            App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }

        private void OnSalvage(string armorUid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUid);
            if (piece == null) { Rebuild(); return; }

            string name = piece.Name;
            int value = App.Session.SalvageValue(piece);
            if (!App.Session.SalvageArmor(armorUid))
            {
                App.Notify("Unequip " + name + " before salvaging it.");
                return;
            }

            App.Audio.Tap();
            App.Notify(name + " salvaged for " + value + " Slag.");
            _selectedUid = "";
            App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }
    }
}
