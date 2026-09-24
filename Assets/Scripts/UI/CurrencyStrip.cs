using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>
    /// The one currency readout, built once and reused verbatim by the Relic Shop, the Rift, the
    /// Deep Recital and Events - the four screens that hide the shared top bar and so have no
    /// other purse on them.
    ///
    /// ONE COMPONENT, NOT FOUR. The four screens it serves each used to state their own purse in
    /// their own way - the shop wrote "RELIC DUST 240 (legendary 150 - mythic 600)" into a banner,
    /// the Rift wrote "YOU HOLD 90 EMBER SHARDS" into a label, the Recital put Echoes in a StatRow
    /// beside two unrelated numbers, and Events buried each event's currency in a section heading.
    /// Four spellings of the same sentence, drifting independently. This is the sentence.
    ///
    /// WHAT IT CARRIES, AND THE RULE THAT DECIDES IT.
    ///
    ///     A currency on the strip is one you spend ANYWHERE.
    ///     A currency off the strip LIVES WHERE IT IS SPENT.
    ///
    /// So: Gold and Shards, always, and nothing else - including whatever is added next. Relic
    /// Dust, Slag, Valor, Waymarks, Coda, Echoes and any event currency are each shown on the one
    /// screen that spends them, and all of them together in the BAG.
    ///
    /// WHY A RULE RATHER THAN A WIDER STRIP. This component carried six cells and was already at
    /// its limit: the build order recorded that a seventh - Coda - would not fit, and while that
    /// note sat there an eighth arrived in Waymarks. A strip sized to hold every currency has to
    /// be resized every time the economy grows, and it spends the top of four screens naming five
    /// purses the player cannot spend from where they are standing. Two cells and a door is a
    /// shape that has no next size.
    ///
    /// THE BAG IS THE OTHER HALF OF THE RULE and is not optional. Taking a currency off the strip
    /// is only honest while one place still shows all of them at once; otherwise a balance nobody
    /// is currently looking at quietly stops existing. See BagPanel.
    /// </summary>
    public sealed class CurrencyStrip
    {
        /// <summary>Strip height. Screens lay their content out from <see cref="BottomOf"/>.</summary>
        public const float Height = 60f;

        private const float CellWidth = 228f;
        private const float CellGap = 12f;
        private const float BagWidth = 196f;
        private const float IconSize = 34f;
        private const float Pad = 12f;

        // Two cells, a bag and the row's padding, written as that sum rather than as a literal:
        // the only thing that decides this strip's width is what is actually standing in it.
        private const float Width = Pad + CellWidth + CellGap + CellWidth + CellGap + BagWidth + Pad;

        public RectTransform Root;

        private Text _gold;
        private Text _shards;

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

            // A FRAMED PANEL, not a chamfered plate. The chamfer was the shop/Rift/Recital/Events
            // dialect, which is the thing that pass retired: those four screens draw the same
            // panel every other screen does, and a strip that kept its own corner treatment would
            // be the one element on four pages still speaking the old language.
            Image plate = EmberforgeUI.PanelOn(Root, false);

            // THE WHOLE STRIP OPENS THE BAG, not only the 196-unit button in it. This is a
            // touch-first build and HubStyle.TouchMin is 108, which nothing 60 units tall can
            // satisfy on its own; a purse the width of the whole readout can. The button stays
            // because a target nobody can see is not an affordance - it says where to press and
            // gives the press its art back. Two ways in, one door.
            plate.raycastTarget = true;
            Button open = Root.gameObject.AddComponent<Button>();
            open.targetGraphic = plate;
            open.transition = Selectable.Transition.None;
            open.onClick.AddListener(OpenBag);

            RectTransform row = UIBuilder.Node("Row", Root);
            UIBuilder.FillParent(row, Pad, 0f, Pad, 0f);
            UIBuilder.HStack(row, CellGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            _gold   = Cell(row, "Gold",   TextureLab.CoinIcon(64),    UITheme.Gold);
            _shards = Cell(row, "Shards", TextureLab.CrystalIcon(64), UITheme.Shard);

            UIButton bag = EmberforgeUI.Button("Bag", row, "BAG", UITheme.FontTiny,
                                               ForgeButtonKind.Secondary);
            UIBuilder.Sized(bag.Root, BagWidth, Height);
            bag.OnClick(OpenBag);
        }

        private static void OpenBag()
        {
            Game.GameApp app = Game.GameApp.Instance;
            if (app == null || app.Bag == null) return;
            app.Audio.Tap();
            app.Bag.Open();
        }

        /// <summary>
        /// One currency: a flat tinted icon, then the value in its own recess.
        ///
        /// The value sits on a plate rather than free on the strip because numbers of different
        /// lengths need a common left edge to scan down, and because a four-digit balance and a
        /// single digit otherwise read as different-sized UI. A RECESS rather than a raised plate:
        /// the strip is already a panel, and a second raised surface on top of one is two objects
        /// where the design has one object with wells cut into it.
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

            Image plateBg = EmberforgeUI.SunkenOn(plate);
            plateBg.raycastTarget = false;

            Text value = EmberforgeUI.Label("Value", plate, "0", HubStyle.FontValue,
                                            Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(value.rectTransform, Emberforge.Space8, 0f, Emberforge.Space8, 0f);
            UIBuilder.Outlined(value, Emberforge.Outline, Emberforge.StrokeThin);
            value.raycastTarget = false;
            return value;
        }

        /// <summary>
        /// Repaints the strip from the session. Every screen calls exactly this, with no arguments
        /// of its own to get wrong - which is the point of the component.
        ///
        /// It still takes the clock although neither figure on the strip is time-dependent any
        /// more. The four callers pass the same `now` they already computed for their own page,
        /// the BAG behind this strip genuinely needs one for the Echo bank, and changing four call
        /// sites to drop an argument is churn that buys nothing.
        /// </summary>
        public void Refresh(GameSession session, long nowUnix)
        {
            if (session == null || Root == null) return;
            PlayerProfile profile = session.Profile;

            _gold.text   = HudTopBar.Format(profile.Gold);
            _shards.text = HudTopBar.Format(profile.Shards);
        }
    }
}
