using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>
    /// The one currency readout, built once and reused verbatim by the Relic Shop, the Rift, the
    /// Deep Recital and Events.
    ///
    /// ONE COMPONENT, NOT FOUR. The four screens it serves each used to state their own purse in
    /// their own way - the shop wrote "RELIC DUST 240 (legendary 150 - mythic 600)" into a banner,
    /// the Rift wrote "YOU HOLD 90 EMBER SHARDS" into a label, the Recital put Echoes in a StatRow
    /// beside two unrelated numbers, and Events buried each event's currency in a section heading.
    /// Four spellings of the same sentence, drifting independently. This is the sentence.
    ///
    /// WHY IT DOES NOT REPLACE HudTopBar. The persistent top bar carries three currencies as part
    /// of the global chrome and is shared with screens outside this pass's scope, so it stays
    /// exactly as it is. This strip sits below the page header, carries all six, and is the
    /// screen's own readout rather than the app's. The overlap between them is real and worth a
    /// look in a later pass that owns both; it is called out in the report rather than papered over
    /// by quietly restyling a bar six other screens depend on.
    ///
    /// WHAT "WHERE RELEVANT" MEANS FOR THE EVENT SLOT. The sixth cell appears when content says an
    /// event with a currency is running, not when a screen asks for it. Driving it off data rather
    /// than off a per-screen flag is what lets the strip be genuinely identical in all four places:
    /// there is no configuration to get wrong, and no screen can end up showing a different set of
    /// currencies than its neighbour on the same day.
    /// </summary>
    public sealed class CurrencyStrip
    {
        /// <summary>Strip height. Screens lay their content out from <see cref="BottomOf"/>.</summary>
        public const float Height = 60f;

        private const float Width = 1500f;
        // 228, not 236. Six cells at 236 plus five 12px gaps is 1476, and the row inside a
        // 1500-wide strip with 12px padding each side is exactly 1476 - a perfect fit with zero
        // slack, where any rounding in the layout pass clips the last cell's plate. 228 leaves 48px
        // of slack, which is the difference between "fits" and "fits reliably".
        private const float CellWidth = 228f;
        private const float CellGap = 12f;
        private const float IconSize = 34f;

        public RectTransform Root;

        private Text _gold;
        private Text _shards;
        private Text _dust;
        private Text _slag;
        private Text _echo;
        private Text _event;
        private RectTransform _eventCell;

        /// <summary>
        /// Y of the first pixel below the strip, given the Y it was built at. Screens position
        /// their content from this rather than from a literal, so moving the strip moves the page.
        /// </summary>
        public static float BottomOf(float top) { return top + Height + UITheme.Space16; }

        /// <summary>The Y every screen in this pass seats the strip at: directly under the header.</summary>
        public static float DefaultTop { get { return ScreenChrome.HeaderTop + 108f + UITheme.Space8; } }

        public void Build(RectTransform parent)
        {
            Build(parent, DefaultTop);
        }

        public void Build(RectTransform parent, float top)
        {
            Root = UIBuilder.Node("CurrencyStrip", parent);
            UIBuilder.Box(Root, 0f, -top, Width, Height, new Vector2(0.5f, 1f));

            Image plate = Root.gameObject.AddComponent<Image>();
            plate.sprite = TextureLab.NotchedRect(14);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.94f);
            plate.raycastTarget = false;
            UIBuilder.NotchedFrame("Edge", Root,
                                   UITheme.WithAlpha(UITheme.ChromeBrass, HubStyle.BorderAlphaStrong), 14, 2);

            RectTransform row = UIBuilder.Node("Row", Root);
            UIBuilder.FillParent(row, UITheme.Space12, 0f, UITheme.Space12, 0f);
            UIBuilder.HStack(row, CellGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            _gold   = Cell(row, "Gold",   TextureLab.CoinIcon(64),      UITheme.Gold);
            _shards = Cell(row, "Shards", TextureLab.CrystalIcon(64),   UITheme.Shard);
            _dust   = Cell(row, "Dust",   TextureLab.SpiralIcon(64),    UITheme.Dust);
            _slag   = Cell(row, "Slag",   TextureLab.IngotIcon(64),     UITheme.Slag);
            _echo   = Cell(row, "Echoes", TextureLab.HourglassIcon(64), UITheme.Echo);

            _event  = Cell(row, "Event",  TextureLab.GemIcon(64),       UITheme.EventToken);
            _eventCell = _event.rectTransform.parent.parent as RectTransform;
            _eventCell.gameObject.SetActive(false);
        }

        /// <summary>
        /// One currency: a flat tinted icon, then the value on its own sunken plate.
        ///
        /// The value sits on a plate rather than free on the strip because six numbers in a row at
        /// six different lengths need a common left edge to scan down, and because a four-digit
        /// balance and a single digit otherwise read as different-sized UI.
        /// </summary>
        private static Text Cell(RectTransform parent, string name, Sprite icon, Color tint)
        {
            RectTransform cell = UIBuilder.Node(name, parent);
            UIBuilder.Sized(cell, CellWidth, 0f);

            Image glyph = UIBuilder.Img("Icon", cell, icon, tint);
            UIBuilder.Box(glyph.rectTransform, 0f, 0f, IconSize, IconSize, new Vector2(0f, 0.5f));
            glyph.raycastTarget = false;

            RectTransform plate = UIBuilder.Node("Plate", cell);
            plate.anchorMin = new Vector2(0f, 0.5f);
            plate.anchorMax = new Vector2(1f, 0.5f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.offsetMin = new Vector2(IconSize + UITheme.Space8, -17f);
            plate.offsetMax = new Vector2(0f, 17f);

            Image plateBg = plate.gameObject.AddComponent<Image>();
            plateBg.sprite = UISkin.Panel(8);
            plateBg.type = Image.Type.Sliced;
            plateBg.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            plateBg.raycastTarget = false;

            Text value = UIBuilder.Label("Value", plate, "0", HubStyle.FontValue,
                                         UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(value.rectTransform, UITheme.Space8, 0f, UITheme.Space8, 0f);
            value.raycastTarget = false;
            return value;
        }

        /// <summary>
        /// Repaints all six from the session. Every screen calls exactly this, with no arguments of
        /// its own to get wrong - which is the point of the component.
        /// </summary>
        public void Refresh(GameSession session, long nowUnix)
        {
            if (session == null || Root == null) return;
            PlayerProfile profile = session.Profile;

            _gold.text   = HudTopBar.Format(profile.Gold);
            _shards.text = HudTopBar.Format(profile.Shards);
            _dust.text   = HudTopBar.Format(profile.RelicDust);
            _slag.text   = HudTopBar.Format(profile.Slag);

            // Echoes are capped, so they are shown against their cap here as well as on the
            // Recital's own meter. A bare "3" would be the one number in the strip whose meaning
            // depends on a maximum the player cannot see from this screen.
            RecitalEchoesStatus echoes = session.PeekRecitalEchoes(nowUnix);
            _echo.text = echoes.Banked + " / " + echoes.Max;

            EventDef currencyEvent = ActiveCurrencyEvent(session, nowUnix);
            if (currencyEvent == null)
            {
                _eventCell.gameObject.SetActive(false);
                return;
            }

            EventProgressEntry entry = profile.FindEventProgress(currencyEvent.Id);
            _eventCell.gameObject.SetActive(true);
            _event.text = HudTopBar.Format(entry != null ? entry.Currency : 0L);
        }

        /// <summary>
        /// The running event whose currency the strip should show, or null.
        ///
        /// Deliberately the first ACTIVE event carrying a currency, not merely the first that has a
        /// balance: an ended event's leftover balance is still spendable on the Events screen, but
        /// it is not what the top of a shop screen should be advertising. If two currency events
        /// ever run at once this shows the first, which is a real limitation - see UITheme.EventToken,
        /// where the same content gap (no per-event colour) is written up.
        /// </summary>
        private static EventDef ActiveCurrencyEvent(GameSession session, long nowUnix)
        {
            List<EventDef> events = session.Content.Events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].HasCurrency && events[i].IsActive(nowUnix)) return events[i];
            return null;
        }
    }
}
