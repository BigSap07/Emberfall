using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>
    /// THE BAG: every currency the account holds, in one list, with the one place each is spent.
    ///
    /// This exists because CurrencyStrip stopped carrying all of them. The strip now shows Gold
    /// and Shards - the two a player spends without going anywhere in particular - and every
    /// other purse lives on the screen that spends it. That trade is only honest while there is
    /// still somewhere that shows the whole account at once, and this is it: without the bag,
    /// "Valor is on the Proving screen" means a player who has forgotten the Proving exists has
    /// no way back to a number they own.
    ///
    /// WHY EACH ROW NAMES ITS SINK. A balance with no sentence under it is a number the player
    /// cannot act on - which is exactly the state four of these currencies were already in, named
    /// on one screen with nothing anywhere saying that screen was where they went. The sentence is
    /// the useful half of the row.
    ///
    /// AND WHY TWO OF THEM SAY THERE IS NO SINK. Valor and Coda genuinely buy nothing yet, and
    /// both say so deliberately rather than being left off the list or described as if they were
    /// spendable - see PlayerProfile.Valor and PlayerProfile.Coda, which record that holding
    /// position and why it is one. A player who is banking a currency towards nothing should be
    /// told, in the place they went to look.
    ///
    /// A SCROLLING LIST, not a fixed panel of rows, and that is the whole lesson of the strip it
    /// came from: this view must never be the reason a new currency cannot be shown.
    /// </summary>
    public sealed class BagPanel
    {
        private const float PanelWidth = 1080f;
        private const float PanelHeight = 760f;
        private const float RowHeight = 92f;
        private const float JewelSize = 44f;

        /// <summary>How much narrower than the panel the scrolling well is, and the padding inside
        /// it. Named because Row() measures its columns from the same two numbers the well is
        /// built with, and two copies of that arithmetic is one copy too many.</summary>
        private const float WellInset = 96f;
        private const float ListPad = 12f;

        private readonly RectTransform _root;
        private readonly RectTransform _panel;
        private readonly RectTransform _list;

        private UiLayerStack.Layer _layer;

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        public BagPanel(RectTransform layer)
        {
            _root = UIBuilder.Node("Bag", layer);
            UIBuilder.FillParent(_root, 0f);

            // The scrim is a control, not only a wash. Tapping beside a purse to put it away is
            // what a player will try first, and the close button below is for the ones who do not.
            Image dim = _root.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimModal;
            Button dismiss = _root.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = dim;
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(Close);

            _panel = UIBuilder.Node("Panel", _root);
            UIBuilder.Box(_panel, 0f, 0f, PanelWidth, PanelHeight, new Vector2(0.5f, 0.5f));
            Image plate = EmberforgeUI.PanelOn(_panel, true);
            // Eats the scrim's dismiss. Without this the panel is a hole in its own dialog: the
            // rows do not take raycasts, so a tap on the panel's background would fall through to
            // the scrim behind it and close the thing the player just reached for.
            plate.raycastTarget = true;
            Button swallow = _panel.gameObject.AddComponent<Button>();
            swallow.targetGraphic = plate;
            swallow.transition = Selectable.Transition.None;

            EmberforgeUI.TitleRibbon("Title", _panel, "BAG",
                                     PanelWidth - Emberforge.RibbonOverhang * 2f);

            Text note = EmberforgeUI.Body("Note", _panel,
                "Gold and Shards ride the top of every screen. Everything else is shown where it "
                + "is spent - and here.",
                UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(note.rectTransform, 0f, -96f, PanelWidth - 140f, 44f, new Vector2(0.5f, 1f));

            RectTransform well = UIBuilder.Node("Well", _panel);
            UIBuilder.Box(well, 0f, -148f, PanelWidth - WellInset, PanelHeight - 148f - 132f,
                          new Vector2(0.5f, 1f));
            EmberforgeUI.SunkenOn(well).raycastTarget = true;

            ScrollRect scroll;
            _list = UIBuilder.ScrollList("List", well, out scroll, UITheme.Space8,
                                         new RectOffset((int)ListPad, (int)ListPad,
                                                        (int)ListPad, (int)ListPad));
            UIBuilder.FillParent((RectTransform)scroll.transform, 0f);

            UIButton close = EmberforgeUI.Button("Close", _panel, "CLOSE", UITheme.FontBody,
                                                 ForgeButtonKind.Quiet);
            UIBuilder.Box(close.Root, 0f, 26f, 340f, 92f, new Vector2(0.5f, 0f));
            close.OnClick(Close);

            EmberforgeUI.CloseDisc(_panel, Close);

            _root.gameObject.SetActive(false);
        }

        public void Open()
        {
            Game.GameApp app = Game.GameApp.Instance;
            if (app == null || app.Session == null) return;

            Rebuild(app.Session, TimeUtil.NowUnix());

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            if (app.Layers != null && _layer == null)
                _layer = app.Layers.Push("panel:bag", Hide);
        }

        public void Close()
        {
            // Through the stack when there is an entry, so that the back gesture and the close
            // button leave it in the same state - the ModalPopup.Forget rule, for the same reason.
            UiLayerStack.Layer layer = _layer;
            if (layer != null)
            {
                _layer = null;
                Game.GameApp app = Game.GameApp.Instance;
                if (app != null && app.Layers != null)
                {
                    app.Layers.Dismiss(layer);
                    return;
                }
            }
            Hide();
        }

        private void Hide()
        {
            _layer = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the list

        private void Rebuild(GameSession session, long nowUnix)
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
                Object.Destroy(_list.GetChild(i).gameObject);

            PlayerProfile profile = session.Profile;

            // ON THE STRIP first, so the bag reads as the whole account rather than as a leftovers
            // drawer. A player looking for "all my money" should find all of it here, including
            // the two they can already see behind this panel.
            Row("GOLD", HudTopBar.Format(profile.Gold), UITheme.Gold, TextureLab.CoinIcon(64),
                "On every screen. Spent levelling heroes.", "gold");

            Row("SHARDS", HudTopBar.Format(profile.Shards), UITheme.Shard, TextureLab.CrystalIcon(64),
                "On every screen. Spent summoning at the Rift.", "shards");

            Row("RELIC DUST", HudTopBar.Format(profile.RelicDust), UITheme.Dust, TextureLab.SpiralIcon(64),
                "Spent in the Relic Shop.");

            Row("SLAG", HudTopBar.Format(profile.Slag), UITheme.Slag, TextureLab.IngotIcon(64),
                "Spent in the Forge, rerolling and combining armour.");

            // Capped, so it is shown against its cap. A bare "3" is the one figure in this list
            // whose meaning depends on a maximum, and the Recital's own meter is not on screen.
            RecitalEchoesStatus echoes = session.PeekRecitalEchoes(nowUnix);
            Row("ECHOES", echoes.Banked + " / " + echoes.Max, UITheme.Echo, TextureLab.HourglassIcon(64),
                "Spent entering the Deep Recital. Regenerates over time.");

            Row("CODA", HudTopBar.Format(profile.Coda), UITheme.Coda, TextureLab.GemIcon(64),
                SinksSwitch.Enabled ? "Paid by the Deep Recital. Spent in the Archive."
                                : "Paid by the Deep Recital. Nothing to spend it on yet.");

            Row("VALOR", HudTopBar.Format(profile.Valor), UITheme.Gold, TextureLab.Sigil(0, 64),
                SinksSwitch.Enabled ? "Paid by the Proving. Spent in the Armory."
                                : "Paid by the Proving. Nothing to spend it on yet.");

            Row("WAYMARKS", HudTopBar.Format(profile.Waymarks), UITheme.Waymark, TextureLab.Circle(64),
                "Paid by the guild dungeon. Spent on the Guild Path.");

            // Every running event with a purse, not just the first. The strip could afford one
            // cell and had to pick; a list does not have to pick, so the limitation the strip
            // documented - two currency events at once showing only one - does not follow it here.
            List<EventDef> events = session.Content.Events;
            for (int i = 0; i < events.Count; i++)
            {
                EventDef evt = events[i];
                if (!evt.HasCurrency || !evt.IsActive(nowUnix)) continue;
                EventProgressEntry entry = profile.FindEventProgress(evt.Id);
                Row(evt.CurrencyName.ToUpperInvariant(),
                    HudTopBar.Format(entry != null ? entry.Currency : 0L),
                    UITheme.EventToken, TextureLab.GemIcon(64),
                    "Spent on " + evt.Name + " rewards, while the event runs.");
            }
        }

        /// <summary>
        /// One currency: jewel, name, balance, and the sentence that says where it goes.
        ///
        /// The balance is right-aligned in its own column so the figures line up down the list -
        /// eight balances at eight lengths, each centred under its own name, is eight numbers a
        /// player has to hunt for one at a time.
        /// </summary>
        private void Row(string name, string balance, Color tint, Sprite icon, string spentWhere)
        {
            Row(name, balance, tint, icon, spentWhere, null);
        }

        /// <summary>The same row, with the purse key the UI kit's currency icon is looked up by (the way
        /// CurrencyStrip.Cell does it); the flat tinted icon is the fallback when the kit has none.</summary>
        private void Row(string name, string balance, Color tint, Sprite icon, string spentWhere, string purse)
        {
            RectTransform row = UIBuilder.Node(name, _list);
            // The well, less the list's own 12-per-side padding. The VStack force-expands width,
            // so this is what the row will actually BE and not merely what it asks for - which
            // matters because the columns below are measured off it.
            float width = PanelWidth - WellInset - ListPad * 2f;
            UIBuilder.Sized(row, width, RowHeight);
            EmberforgeUI.PlainPanelOn(row).raycastTarget = false;

            Sprite kitIcon = purse != null ? UiKit.Currency(purse) : null;
            Image glyph = UIBuilder.Img("Icon", row, kitIcon != null ? kitIcon : icon,
                                        kitIcon != null ? Color.white : tint);
            UIBuilder.Box(glyph.rectTransform, UITheme.Space16, 0f, JewelSize, JewelSize,
                          new Vector2(0f, 0.5f));
            glyph.raycastTarget = false;

            float textLeft = UITheme.Space16 + JewelSize + UITheme.Space16;
            float valueWidth = 220f;
            float textWidth = width - textLeft - valueWidth - UITheme.Space24;

            Text title = EmberforgeUI.Title("Name", row, name, UITheme.FontSmall,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, textLeft, -16f, textWidth, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text where = EmberforgeUI.Label("Where", row, spentWhere, UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(where.rectTransform, textLeft, -50f, textWidth, 26f, new Vector2(0f, 1f));
            UIBuilder.BestFit(where, UITheme.FontTiny - 6, UITheme.FontTiny - 2);

            Text value = EmberforgeUI.Label("Value", row, balance, HubStyle.FontValue,
                                            Emberforge.TextGold, TextAnchor.MiddleRight);
            UIBuilder.Box(value.rectTransform, -UITheme.Space24, 0f, valueWidth, 40f,
                          new Vector2(1f, 0.5f));
            UIBuilder.Outlined(value, Emberforge.Outline, Emberforge.StrokeThin);
        }
    }
}
