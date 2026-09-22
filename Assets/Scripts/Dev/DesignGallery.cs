#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Game;
using Emberfall.UI;

namespace Emberfall.Dev
{
    /// <summary>
    /// EVERY COMPONENT IN THE KIT, IN EVERY STATE, ON ONE SCREEN.
    ///
    /// A design system that can only be inspected by finding a screen that happens to use each
    /// piece is a design system nobody checks. This is the page somebody opens to answer "what
    /// does a disabled secondary button look like" without hunting for one, and it is the page a
    /// capture is taken of when a token changes, because a diff of two of these shots is the whole
    /// visual delta of a retune.
    ///
    /// IT IS A LAYER, NOT A SCREEN, and deliberately so. Adding a twenty-second ScreenId would put
    /// a development page in the router's shipped enum, and the enum is what a save file's
    /// last-screen field and the back stack are written against. A layer pushed onto
    /// UiLayerStack - the same thing the idle claim popup and the overflow menu are - costs the
    /// shipped build nothing, because this whole file compiles away.
    ///
    /// IT DRAWS NOTHING OF ITS OWN. Every surface on this page comes from EmberforgeUI, which is
    /// the property that makes it a gallery rather than a mockup: if the page looks right, the
    /// components are right, because they are the same objects the Hub is built from.
    ///
    /// THE PORTRAITS ARE REAL HEROES, picked out of the loaded content one per star tier and
    /// across as many aspects as the roster offers. A rarity frame drawn around a placeholder is a
    /// picture of the frame; drawn around the roster it is a picture of the screen the frame is
    /// for, including the case this project actually has - a roster where some heroes are painted
    /// and some are not.
    /// </summary>
    public sealed class DesignGallery
    {
        private const float Column = 1180f;
        private const float SectionGap = 26f;
        private const float RowHeight = 118f;
        private const float ButtonWidth = 300f;
        private const float ButtonHeight = 108f;

        private readonly GameApp _app;
        private readonly RectTransform _layer;

        private RectTransform _root;
        private UiLayerStack.Layer _layerHandle;
        private readonly List<ForgeTab> _tabs = new List<ForgeTab>();

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        public DesignGallery(GameApp app, RectTransform layer)
        {
            _app = app;
            _layer = layer;
        }

        public void Show()
        {
            Build();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            if (_layerHandle == null) _layerHandle = _app.Layers.Push("dev:gallery", CloseInternal);
        }

        public void Close()
        {
            if (_layerHandle != null) { _app.Layers.Dismiss(_layerHandle); return; }
            CloseInternal();
        }

        private void CloseInternal()
        {
            _layerHandle = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ construction

        private void Build()
        {
            if (_root != null) return;

            _root = UIBuilder.Node("DesignGallery", _layer);
            UIBuilder.FillParent(_root, 0f);

            // Opaque rather than a scrim. Every other layer in this game dims what is behind it
            // because what is behind it still matters; here it does not, and a gallery of surfaces
            // judged against a half-visible painting is a gallery judged against the wrong thing.
            Image backdrop = UIBuilder.Img("Backdrop", _root, TextureLab.White, Emberforge.PanelLow);
            UIBuilder.FillParent(backdrop.rectTransform, 0f);
            backdrop.raycastTarget = true;

            RectTransform safe = UIBuilder.Node("Safe", _root);
            UIBuilder.FillParent(safe, 0f);
            SafeAreaRect.Attach(safe);

            Text title = EmberforgeUI.Title("Title", safe, "EMBERFORGE", UITheme.FontTitle,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, Emberforge.Space32, -Emberforge.Space16, 700f, 56f,
                          new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThick);

            Text source = EmberforgeUI.Label("Source", safe, SourceLine(), UITheme.FontTiny,
                                             Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(source.rectTransform, Emberforge.Space32, -(Emberforge.Space16 + 54f),
                          900f, 30f, new Vector2(0f, 1f));

            UIButton close = EmberforgeUI.Button("Close", safe, "CLOSE", UITheme.FontSmall,
                                                 ForgeButtonKind.Secondary);
            UIBuilder.Box(close.Root, -Emberforge.Space32, -Emberforge.Space16, 240f, HubStyle.TouchMin,
                          new Vector2(1f, 1f));
            close.OnClick(Close);

            ScrollRect scroll;
            RectTransform list = UIBuilder.ScrollList("Sections", safe, out scroll, SectionGap,
                                                      new RectOffset(0, 0, 0, 64));
            RectTransform scrollRoot = scroll.GetComponent<RectTransform>();
            UIBuilder.FillParent(scrollRoot, Emberforge.Space32, 110f, Emberforge.Space32, 0f);

            BuildSurfaces(list);
            BuildButtons(list);
            BuildTabsAndPills(list);
            BuildBars(list);
            BuildPortraits(list);
            BuildPlateAndDiscs(list);

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Which of the three sources the kit is coming from, printed on the page.
        ///
        /// It is here because the three are indistinguishable by eye and the difference matters:
        /// a page that looks right while every slot is being generated on the device says the art
        /// is correct and says nothing at all about whether the bake ran.
        /// </summary>
        private static string SourceLine()
        {
            string[] slots = EmberforgeArt.AllSlots();
            int painted = 0, baked = 0, generated = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (EmberforgeSkin.IsPainted(slots[i])) painted++;
                else if (EmberforgeSkin.IsBaked(slots[i])) baked++;
                else generated++;
            }
            return slots.Length + " SLOTS  -  " + painted + " painted, " + baked + " baked, "
                 + generated + " generated at runtime";
        }

        // ------------------------------------------------------------------ sections

        /// <summary>A section is a heading ribbon over a plain panel, which is itself two of the
        /// components being shown. The page is built out of the kit all the way down.</summary>
        private RectTransform Section(RectTransform list, string heading, float height)
        {
            RectTransform block = UIBuilder.Node("Section" + heading, list);
            UIBuilder.Sized(block, Column, height + Emberforge.RibbonHeight);

            RectTransform body = UIBuilder.Node("Body", block);
            UIBuilder.Box(body, 0f, 0f, Column, height, new Vector2(0.5f, 0f));
            EmberforgeUI.PanelOn(body, true);

            EmberforgeUI.TitleRibbon("Heading", block, heading, Column * 0.5f);
            return body;
        }

        private void BuildSurfaces(RectTransform list)
        {
            RectTransform body = Section(list, "SURFACES", 230f);
            float w = 320f;
            float h = 150f;
            float gap = Emberforge.Space24;
            float x = Emberforge.Space32;

            Sample(body, "PANEL", x, w, h, 0);
            Sample(body, "PLAIN", x + (w + gap), w, h, 1);
            Sample(body, "SUNKEN", x + (w + gap) * 2f, w, h, 2);
        }

        private void Sample(RectTransform body, string caption, float x, float w, float h, int kind)
        {
            RectTransform cell = UIBuilder.Node("Sample" + caption, body);
            UIBuilder.Box(cell, x, -Emberforge.Space32, w, h, new Vector2(0f, 1f));
            if (kind == 0) EmberforgeUI.PanelOn(cell, true);
            else if (kind == 1) EmberforgeUI.PlainPanelOn(cell);
            else EmberforgeUI.SunkenOn(cell);

            Text label = EmberforgeUI.Label("Caption", cell, caption, UITheme.FontSmall,
                                            Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(label.rectTransform, Emberforge.Space16);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>
        /// Both voices in all three states. The PRESSED column is a real button held down rather
        /// than a picture of one: EmberforgeButtonSkin is driven straight so the page shows the
        /// same object the finger produces, including the face having dropped into the lip.
        /// </summary>
        private void BuildButtons(RectTransform list)
        {
            RectTransform body = Section(list, "BUTTONS", 330f);

            Header(body, "PRIMARY", 0);
            Header(body, "SECONDARY", 1);

            string[] states = new string[] { "NORMAL", "PRESSED", "DISABLED" };
            for (int row = 0; row < 2; row++)
            {
                ForgeButtonKind kind = row == 0 ? ForgeButtonKind.Primary : ForgeButtonKind.Secondary;
                for (int col = 0; col < states.Length; col++)
                {
                    UIButton button = EmberforgeUI.Button("B" + row + col, body, states[col],
                                                          UITheme.FontSmall, kind);
                    UIBuilder.Box(button.Root,
                                  Emberforge.Space32 + col * (ButtonWidth + Emberforge.Space24),
                                  -(Emberforge.Space32 + 34f + row * (ButtonHeight + 56f)),
                                  ButtonWidth, ButtonHeight, new Vector2(0f, 1f));

                    if (states[col] == "DISABLED") button.Interactable = false;
                    if (states[col] == "PRESSED") HoldDown(button);
                }
            }

            Text note = EmberforgeUI.Label("Note", body,
                "A press is the lip shrinking from " + Emberforge.ButtonLip + " to "
                + Emberforge.ButtonLipPressed + " and the face dropping "
                + Emberforge.ButtonPressDrop + " into the space it frees. The outer silhouette "
                + "never moves.", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.Box(note.rectTransform, Emberforge.Space32, Emberforge.Space16, Column - 64f,
                          30f, new Vector2(0f, 0f));
        }

        private void Header(RectTransform body, string text, int row)
        {
            Text label = EmberforgeUI.Label("H" + text, body, text, UITheme.FontTiny,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, Emberforge.Space32,
                          -(Emberforge.Space32 + row * (ButtonHeight + 56f)), 400f, 28f,
                          new Vector2(0f, 1f));
        }

        /// <summary>
        /// Holds a button in its pressed art without a pointer.
        ///
        /// Through the same component the pointer drives rather than by setting a sprite here: a
        /// gallery that reaches past the component to paint the state it wants is a gallery that
        /// can show a state the component cannot actually produce.
        /// </summary>
        private static void HoldDown(UIButton button)
        {
            EmberforgeButtonSkin skin = button.Root.GetComponent<EmberforgeButtonSkin>();
            if (skin != null) skin.OnPointerDown(null);
            button.Button.interactable = false;   // so a click cannot release it mid-capture
        }

        private void BuildTabsAndPills(RectTransform list)
        {
            RectTransform body = Section(list, "TABS AND CURRENCY", 250f);

            _tabs.Clear();
            string[] captions = new string[] { "PROFILE", "ACCOUNT", "FRIENDS", "SETTINGS" };
            for (int i = 0; i < captions.Length; i++)
            {
                ForgeTab tab = EmberforgeUI.Tab(captions[i], body, 230f, 76f, null);
                UIBuilder.Box(tab.Root, Emberforge.Space32 + i * (230f + Emberforge.Space12),
                              -Emberforge.Space32, 230f, 76f, new Vector2(0f, 1f));
                EmberforgeUI.SetTabSelected(tab, i == 0);
                _tabs.Add(tab);
            }

            Color[] tints = new Color[] { UITheme.Gold, UITheme.Shard, UITheme.Dust };
            string[] values = new string[] { "128,400", "2,140", "860" };
            for (int i = 0; i < tints.Length; i++)
            {
                // Gold carries no plus, because gold is earned rather than bought. Passing null is
                // what suppresses it, so this row shows both shapes of the same component.
                System.Action onPlus = i == 0 ? null : new System.Action(DoNothing);
                ForgePill pill = EmberforgeUI.CurrencyPill("Pill" + i, body, 250f, 68f, tints[i],
                                                           onPlus);
                UIBuilder.Box(pill.Root, Emberforge.Space32 + i * (250f + 56f),
                              -(Emberforge.Space32 + 110f), 250f, 68f, new Vector2(0f, 1f));
                pill.Value.text = values[i];
            }

            Text note = EmberforgeUI.Label("Note", body,
                "The first pill carries no plus, because gold is earned rather than bought. A pill "
                + "is a dark plate with a gold line inside it: the line is what stops it reading "
                + "as a hole in the panel.", UITheme.FontTiny, Emberforge.TextQuiet,
                TextAnchor.LowerLeft);
            UIBuilder.Box(note.rectTransform, Emberforge.Space32, Emberforge.Space16, Column - 64f,
                          30f, new Vector2(0f, 0f));
        }

        private static void DoNothing() { }

        private void BuildBars(RectTransform list)
        {
            RectTransform body = Section(list, "PROGRESS", 200f);

            float[] ratios = new float[] { 0f, 0.18f, 0.55f, 1f };
            for (int i = 0; i < ratios.Length; i++)
            {
                Image fill = EmberforgeUI.ProgressBar("Bar" + i, body);
                RectTransform root = fill.rectTransform.parent as RectTransform;
                UIBuilder.Box(root, Emberforge.Space32, -(Emberforge.Space32 + i * 40f),
                              Column - 260f, 26f, new Vector2(0f, 1f));
                EmberforgeUI.SetProgress(fill, ratios[i]);

                Text value = EmberforgeUI.Label("V" + i, body,
                                                Mathf.RoundToInt(ratios[i] * 100f) + "%",
                                                UITheme.FontTiny, Emberforge.TextGold,
                                                TextAnchor.MiddleLeft);
                UIBuilder.Box(value.rectTransform, -Emberforge.Space32,
                              -(Emberforge.Space32 + i * 40f), 160f, 26f, new Vector2(1f, 1f));
            }
        }

        /// <summary>
        /// One portrait per star tier, and across as many aspects as the roster can supply.
        ///
        /// The row deliberately does NOT pick five heroes of one aspect: the frame carries the
        /// tier and the glow carries the aspect, and a row where every glow is the same colour
        /// proves only half of that.
        /// </summary>
        private void BuildPortraits(RectTransform list)
        {
            RectTransform body = Section(list, "PORTRAIT FRAMES", 300f);

            List<HeroDef> picks = PickOnePerTier();
            if (picks.Count == 0)
            {
                Text empty = EmberforgeUI.Label("Empty", body, "No hero content is loaded.",
                                                UITheme.FontBody, Emberforge.TextQuiet,
                                                TextAnchor.MiddleCenter);
                UIBuilder.FillParent(empty.rectTransform, 0f);
                return;
            }

            float size = 170f;
            for (int i = 0; i < picks.Count; i++)
            {
                HeroDef def = picks[i];
                float x = Emberforge.Space32 + i * (size + 48f);

                RectTransform frame = EmberforgeUI.PortraitFrame("P" + i, body, def, def.Stars, size);
                UIBuilder.Box(frame, x, -Emberforge.Space32, size, size, new Vector2(0f, 1f));

                Text caption = EmberforgeUI.Label("C" + i, body,
                                                  def.Stars + "*  " + def.Aspect.ToString().ToUpperInvariant(),
                                                  UITheme.FontTiny, Emberforge.TextGold,
                                                  TextAnchor.UpperCenter);
                UIBuilder.Box(caption.rectTransform, x + size * 0.5f, -(Emberforge.Space32 + size + 8f),
                              size + 40f, 26f, new Vector2(0f, 1f));
                UIBuilder.Outlined(caption, Emberforge.Outline, Emberforge.StrokeThin);

                Text who = EmberforgeUI.Label("N" + i, body, def.Name.ToUpperInvariant(),
                                              UITheme.FontTiny - 4, Emberforge.TextQuiet,
                                              TextAnchor.UpperCenter);
                UIBuilder.Box(who.rectTransform, x + size * 0.5f, -(Emberforge.Space32 + size + 34f),
                              size + 40f, 24f, new Vector2(0f, 1f));
            }
        }

        /// <summary>
        /// One hero per star tier, preferring one whose aspect no earlier pick already used.
        ///
        /// Greedy rather than exhaustive: with six aspects and five tiers a perfect spread always
        /// exists on a full roster, and on a partial one the greedy pass still gets as many
        /// distinct aspects as the content allows. Falling back to any hero of the tier means a
        /// roster missing a whole aspect shows a tier rather than a gap.
        /// </summary>
        private List<HeroDef> PickOnePerTier()
        {
            List<HeroDef> picks = new List<HeroDef>();
            if (_app == null || _app.Session == null || _app.Session.Content == null) return picks;

            List<HeroDef> all = _app.Session.Content.Heroes;
            List<Aspect> used = new List<Aspect>();

            for (int stars = 1; stars <= 5; stars++)
            {
                HeroDef fallback = null;
                HeroDef chosen = null;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Stars != stars) continue;
                    if (fallback == null) fallback = all[i];
                    if (used.Contains(all[i].Aspect)) continue;
                    chosen = all[i];
                    break;
                }
                if (chosen == null) chosen = fallback;
                if (chosen == null) continue;
                used.Add(chosen.Aspect);
                picks.Add(chosen);
            }
            return picks;
        }

        private void BuildPlateAndDiscs(RectTransform list)
        {
            RectTransform body = Section(list, "PLATE AND RAIL", 260f);

            ForgePlate plate = EmberforgeUI.PlayerPlate("Plate", body, 520f, 92f, null);
            UIBuilder.Box(plate.Root, Emberforge.Space32, -Emberforge.Space32, 520f, 92f,
                          new Vector2(0f, 1f));
            plate.Rank.text = "42";
            plate.Name.text = "WAYFARER  -  RANK 42";
            plate.Xp.text = "1,240 / 3,000 XP";
            EmberforgeUI.SetProgress(plate.XpFill, 0.41f);

            // The placeholder face, not a portrait: the plate on a fresh account has no chosen
            // hero, and that is the state worth showing beside the components rather than the one
            // that only appears after a player has been to the profile screen.
            Image mark = UIBuilder.Img("Mark", plate.AvatarHost, TextureLab.CinderFragment(64),
                                       UITheme.WithAlpha(UITheme.CinderGlass, 0.95f));
            UIBuilder.FillParent(mark.rectTransform, Emberforge.Space8);
            mark.transform.SetAsFirstSibling();

            Sprite[] glyphs = new Sprite[]
            {
                TextureLab.Heart(64), TextureLab.Envelope(64),
                TextureLab.Sigil(8, 64), TextureLab.Ellipsis(64)
            };
            Color[] tints = new Color[]
            {
                Emberforge.TextBody, Emberforge.TextBody, UITheme.Success, Emberforge.TextBody
            };

            for (int i = 0; i < glyphs.Length; i++)
            {
                ForgeDisc disc = EmberforgeUI.IconButton("Disc" + i, body, "", glyphs[i], tints[i],
                                                         HubStyle.RailDisc, null);
                UIBuilder.Box(disc.Root, Emberforge.Space32 + i * (HubStyle.TouchMin + Emberforge.Space16),
                              -(Emberforge.Space32 + 120f), HubStyle.TouchMin, HubStyle.TouchMin,
                              new Vector2(0f, 1f));
                if (i == 3) disc.Button.Interactable = false;
            }

            UIButton plus = EmberforgeUI.PlusButton("Plus", body, null);
            UIBuilder.Box(plus.Root, Emberforge.Space32 + 4f * (HubStyle.TouchMin + Emberforge.Space16) + 40f,
                          -(Emberforge.Space32 + 120f), HubStyle.TouchMin, HubStyle.TouchMin,
                          new Vector2(0f, 1f));

            Text note = EmberforgeUI.Label("Note", body,
                "The fourth disc is disabled. A rail disc is 76 of art inside a "
                + HubStyle.TouchMin + "-unit hit rect, because a target's size is a property of "
                + "the control rather than of the drawing.", UITheme.FontTiny,
                Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.Box(note.rectTransform, Emberforge.Space32, Emberforge.Space16, Column - 64f,
                          30f, new Vector2(0f, 0f));
        }
    }
}
#endif
