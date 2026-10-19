using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.UI
{
    /// <summary>
    /// THE ABILITY CARD: one layer over whatever screen asked, saying what each ability does.
    ///
    /// Opened from two places and the same either way: a tap on an ability on the hero page, and a
    /// tap on a unit in battle (BattleScreen pauses the fight while it is up). It holds no game
    /// logic. The sentence is AbilityTextConfig.Prose, the figures are AbilityMath and AbilityFacts,
    /// and the harness proves the figures equal to what CombatSimulation does with the same effect
    /// (CheckAbilityNumbersMatchTheSimulation, CheckAbilityFactsMatchTheSimulation).
    ///
    /// Per ability, in this order: the sentence in the game's voice (Docs/ABILITY-TEXT-STYLE.md),
    /// then the facts (Energy cost, when it fires, how often, how long a status lasts and how big
    /// it is), then one row per damage, healing or ward figure. The list scrolls, so an ability
    /// with eight figures and a unit with five abilities are the same card.
    ///
    /// ON THE APP'S OVERLAY LAYER, not on the asking screen's Root, for the reason the hero page's
    /// own sheet always was: the top bar is a sibling of the screen and is drawn over it, so a
    /// sheet parented to the screen slides under it at 20:9.
    /// </summary>
    public sealed class AbilityCardSheet
    {
        /// <summary>One ability to show: the kind chip's words and tint, the ability, and the
        /// PassiveDef it was read from when it is a passive (its trigger and limit are facts).</summary>
        public sealed class Entry
        {
            public string Kind = "";
            public Color Tint = Color.white;
            public AbilityDef Ability;
            public PassiveDef Passive;
        }

        private const float SheetWidth = 900f;
        private const float SheetHeight = 860f;
        private const float RowHeight = 48f;
        private const float TallRowHeight = 84f;
        private const int WrapAfter = 44;
        private const float HeaderHeight = 44f;
        private const float FactLabelShare = 0.22f;
        private const float NumberFigureShare = 0.4f;

        private readonly RectTransform _sheet;
        private readonly CanvasGroup _group;
        private readonly Text _title;
        private readonly RectTransform _list;
        private readonly Text _note;
        private readonly UiLayerStack _layers;
        private readonly Action _tap;
        private UiLayerStack.Layer _layer;
        private Action _onClosed;

        public bool IsOpen { get { return _sheet != null && _sheet.gameObject.activeSelf; } }

        public AbilityCardSheet(Transform overlayLayer, UiLayerStack layers, Action tap)
        {
            _layers = layers;
            _tap = tap;

            _sheet = UIBuilder.Node("AbilitySheet", overlayLayer);
            UIBuilder.FillParent(_sheet, 0f);
            _group = _sheet.gameObject.AddComponent<CanvasGroup>();

            Image dim = _sheet.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimModal;

            RectTransform panel = UIBuilder.Node("Panel", _sheet);
            UIBuilder.Box(panel, 0f, 0f, SheetWidth, SheetHeight, new Vector2(0.5f, 0.5f));
            EmberforgeUI.PanelOn(panel, true);

            _title = EmberforgeUI.TitleRibbon("Title", panel, "", SheetWidth - Emberforge.RibbonOverhang * 2f);

            ScrollRect scroll;
            _list = UIBuilder.ScrollList("List", panel, out scroll, 6f, new RectOffset(0, 0, 4, 4));
            // Clear of the ribbon at the top and of the note and button at the bottom, and inset
            // past the corner ornaments - four screens have hung a full-width control off a framed
            // panel and printed it over the two lower diamonds.
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 Emberforge.OrnamentClearance, 96f, Emberforge.OrnamentClearance, 190f);

            Image rule = EmberforgeUI.Rule("Rule", panel);
            UIBuilder.Box(rule.rectTransform, 0f, 172f, SheetWidth - Emberforge.OrnamentClearance * 2f,
                          Emberforge.RuleHeight, new Vector2(0.5f, 0f));

            _note = EmberforgeUI.Body("Note", panel, AbilityText.Caveat, UITheme.FontTiny,
                                      Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_note.rectTransform, 0f, 128f,
                          SheetWidth - Emberforge.OrnamentClearance * 2f, 40f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_note, Emberforge.Outline, Emberforge.StrokeThin);

            UIButton close = EmberforgeUI.Button("Close", panel, "CLOSE", UITheme.FontBody,
                                                 ForgeButtonKind.Primary);
            UIBuilder.Box(close.Root, 0f, Emberforge.OrnamentClearance, 400f, 88f, new Vector2(0.5f, 0f));
            close.OnClick(Close);

            _sheet.gameObject.SetActive(false);
        }

        /// <summary>
        /// Fills the card and opens it. <paramref name="owned"/> only picks the caveat line: a
        /// locked hero's numbers are a level-1 hero with no gear. <paramref name="onClosed"/> runs
        /// once however the card goes away, which is how a battle resumes.
        /// </summary>
        public void Show(string title, List<Entry> entries, UnitStats stats, BalanceConfig balance,
                         AbilityTextConfig texts, bool owned, Action onClosed)
        {
            if (_tap != null) _tap();
            for (int i = _list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_list.GetChild(i).gameObject);

            _onClosed = onClosed;
            _title.text = title.ToUpperInvariant();
            _note.text = owned ? AbilityText.Caveat : AbilityText.CaveatLocked;

            bool several = entries.Count > 1;
            for (int e = 0; e < entries.Count; e++)
            {
                Entry entry = entries[e];
                if (entry.Ability == null) continue;
                if (several) AddHeader(entry);

                string prose = texts != null ? texts.Prose(entry.Ability.Id, entry.Ability.Description)
                                             : (entry.Ability.Description ?? "");
                if (prose.Length > 0) AddProse(prose);

                List<AbilityFact> facts = AbilityFacts.For(entry.Ability, entry.Passive, stats);
                for (int f = 0; f < facts.Count; f++)
                    AddRow(facts[f].Label, Emberforge.TextQuiet, facts[f].Text, Emberforge.TextBody, FactLabelShare);

                List<AbilityValue> values = AbilityMath.Values(entry.Ability, stats, balance);
                if (values.Count == 0 && facts.Count == 0 && prose.Length == 0)
                    AddRow("", Emberforge.TextQuiet, AbilityText.NoNumbers, Emberforge.TextQuiet, NumberFigureShare);

                // Rows that say exactly the same thing (four equal hits of one effect list) are one row
                // with a count, so a kit that repeats itself does not push the rest off the card.
                List<string> lefts = new List<string>(), rights = new List<string>();
                List<Color> tints = new List<Color>();
                List<int> counts = new List<int>();
                List<AbilityValueKind> kinds = new List<AbilityValueKind>();
                for (int i = 0; i < values.Count; i++)
                {
                    AbilityValue v = values[i];
                    EffectDef effect = entry.Ability.Effects[v.EffectIndex];
                    string secondary = AbilityText.Secondary(v, effect);
                    string riders = AbilityText.Riders(v);
                    if (riders.Length > 0)
                        secondary = secondary.Length > 0 ? secondary + ", " + riders : riders;
                    // An effect with no caster-side term of its own has no figure to lead with: its whole
                    // payout is a share of somebody else's health, so the card leads with the most it can
                    // deal and the workings are its line.
                    string left = AbilityText.Figure(v, false);
                    int found = -1;
                    for (int k = 0; k < lefts.Count; k++)
                        if (lefts[k] == left && rights[k] == secondary && kinds[k] == v.Kind) { found = k; break; }
                    if (found >= 0) { counts[found]++; continue; }
                    lefts.Add(left); rights.Add(secondary); tints.Add(AbilityText.Tint(v.Kind)); kinds.Add(v.Kind); counts.Add(1);
                }
                for (int k = 0; k < lefts.Count; k++)
                    AddRow(counts[k] > 1 ? lefts[k] + "  x" + counts[k] : lefts[k], tints[k], rights[k],
                           Emberforge.TextQuiet, NumberFigureShare);
            }

            _sheet.gameObject.SetActive(true);
            _sheet.SetAsLastSibling();
            _group.alpha = 1f;
            if (_layer == null && _layers != null)
                _layer = _layers.Push("popup:ability", Close);
        }

        public void Close()
        {
            if (!IsOpen) return;
            UiLayerStack.Layer layer = _layer;
            _layer = null;
            if (layer != null && _layers != null) _layers.Dismiss(layer);
            _sheet.gameObject.SetActive(false);
            Action closed = _onClosed;
            _onClosed = null;
            if (closed != null) closed();
        }

        private void AddHeader(Entry entry)
        {
            RectTransform row = UIBuilder.Node("Header", _list);
            UIBuilder.Sized(row, 0f, HeaderHeight);

            Text kind = EmberforgeUI.Label("Kind", row, entry.Kind, UITheme.FontSmall, entry.Tint,
                                           TextAnchor.MiddleLeft);
            kind.rectTransform.anchorMin = new Vector2(0f, 0f);
            kind.rectTransform.anchorMax = new Vector2(0.2f, 1f);
            kind.rectTransform.offsetMin = new Vector2(Emberforge.Space8, 0f);
            kind.rectTransform.offsetMax = new Vector2(0f, 0f);
            UIBuilder.Outlined(kind, Emberforge.Outline, Emberforge.StrokeThin);

            Text name = EmberforgeUI.Label("Name", row, entry.Ability.Name, UITheme.FontBody, Emberforge.TextGold,
                                           TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0.2f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(0f, 0f);
            name.rectTransform.offsetMax = new Vector2(-Emberforge.Space8, 0f);
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void AddProse(string prose)
        {
            // About 62 characters a line at this width and size. A short estimate is cheaper than
            // measuring text and errs tall, which only costs a little empty space in the row.
            int lines = Mathf.Max(1, Mathf.CeilToInt(prose.Length / 62f));
            float height = Mathf.Max(RowHeight, 26f * lines + 22f);

            RectTransform row = UIBuilder.Node("Prose", _list);
            UIBuilder.Sized(row, 0f, height);
            EmberforgeUI.SunkenOn(row);

            Text text = EmberforgeUI.Body("Text", row, prose, UITheme.FontSmall, Emberforge.TextBody,
                                          TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(Emberforge.Space24, 0f);
            text.rectTransform.offsetMax = new Vector2(-Emberforge.Space24, 0f);
            UIBuilder.Outlined(text, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void AddRow(string left, Color leftTint, string right, Color rightTint, float split)
        {
            RectTransform row = UIBuilder.Node("Row", _list);
            UIBuilder.Sized(row, 0f, right.Length > WrapAfter && split >= 0.3f ? TallRowHeight : RowHeight);
            EmberforgeUI.SunkenOn(row);

            Text value = EmberforgeUI.Label("Value", row, left, UITheme.FontBody, leftTint,
                                            TextAnchor.MiddleLeft);
            value.rectTransform.anchorMin = new Vector2(0f, 0f);
            value.rectTransform.anchorMax = new Vector2(split, 1f);
            value.rectTransform.offsetMin = new Vector2(Emberforge.Space24, 0f);
            value.rectTransform.offsetMax = new Vector2(-Emberforge.Space8, 0f);
            UIBuilder.Outlined(value, Emberforge.Outline, Emberforge.StrokeThin);

            Text label = EmberforgeUI.Body("Terms", row, right, UITheme.FontSmall, rightTint,
                                           split < 0.3f ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            label.rectTransform.anchorMin = new Vector2(split, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(Emberforge.Space8, 0f);
            label.rectTransform.offsetMax = new Vector2(-Emberforge.Space24, 0f);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
        }
    }
}
