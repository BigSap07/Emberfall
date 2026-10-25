using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;

namespace Emberfall.UI
{
    /// <summary>
    /// Two saves, side by side, and a player choosing between them.
    ///
    /// The only screen in this game whose answer can destroy a week of somebody's progress, which
    /// is why it is a purpose-built overlay rather than a ModalPopup with a longer body string.
    /// ModalPopup draws a title, a paragraph and two buttons; a paragraph cannot put two saves
    /// beside each other, and a player comparing "level 24, 31 stages, 11 heroes" against "level
    /// 24, 33 stages, 11 heroes" needs the two columns aligned or they are not comparing, they are
    /// remembering. That alignment is the whole feature.
    ///
    /// WHAT IS DELIBERATELY ABSENT, because each would be a way of answering the question without
    /// a person:
    ///   - no default, and no button styled as the safe one. Both are Gold, both are the same
    ///     size, and neither says "recommended" - the game does not know which save the player
    ///     wants and must not pretend to.
    ///   - no close button, no backdrop tap, no Escape. A dismissed conflict is an unanswered
    ///     conflict, and the next sync would ask again - so dismissing would be a way to sit
    ///     between two saves indefinitely while continuing to play on one of them.
    ///   - no "keep both", which this game has no meaning for, and no merge, which would lose
    ///     work more quietly than picking a side does.
    ///
    /// WHAT IT SHOWS, AND WHY THAT (Docs/CLOUD-GAPS.md P9). It used to show four numbers per side,
    /// and two saves a week and two million gold apart rendered as the same four rows. The player
    /// in this card is asking "which one is mine", anxious and one tap from an irreversible
    /// answer, so the card reads top to bottom as glance, then evidence:
    ///   - under each heading, when it was saved and, on the cloud side, whether this device wrote
    ///     it. The name only when the two names differ, because then the name is the answer.
    ///   - a VERDICT per side, in one line: what that save has that the other does not. "Ahead on
    ///     gold, shards, hero levels and the Recital" against "Nothing the other save lacks" is
    ///     the commonest conflict answered without reading a number.
    ///   - the ROWS, which are the evidence: campaign position, rank, heroes owned, the three best
    ///     heroes by name, gold and ember shards, and the Recital and Proving once either save has
    ///     been there. The side ahead on a row is lit and carries its lead ("+2M"); the side
    ///     behind is dimmed; a row where they agree is plain on both, so the eye goes to the
    ///     differences and skips the rest.
    ///   - an ALSO line for the smaller purses and collections (relic dust, slag, coda, valor,
    ///     armor, relics, summons, battles won): named when one side has more, never tabled.
    /// All of it is SaveComparison's, in Core, so the harness holds it to the audit's own pair. It
    /// is read out of each document without deserialising it, so a save from a build this one
    /// cannot fully read is still described rather than shown as a blank.
    ///
    /// Nothing on the card says which one to keep. More gold is not more progress when the other
    /// save is further in the campaign; the verdicts are facts about each save, never advice.
    /// </summary>
    public sealed class ConflictChooser
    {
        private readonly RectTransform _root;
        private readonly Text _reason;
        private readonly Side _local;
        private readonly Side _cloud;
        private readonly Text[] _labels;
        private readonly UIButton _keepLocal;
        private readonly UIButton _keepCloud;

        private Action<ConflictChoice> _answer;

        /// <summary>This card's entry in the one back stack, registered as BLOCKING.
        ///
        /// It is the single thing in the game the back gesture may not dismiss. The card is open
        /// because two saves disagree and only the player can say which one survives; there is no
        /// third answer, and a back press that closed it would leave the sync unresolved with
        /// nothing on screen to say so. Registered rather than ignored so the press is SWALLOWED
        /// here instead of falling through to the screen underneath, which would navigate away
        /// from a decision the game is waiting on.</summary>
        private UiLayerStack.Layer _layer;

        /// <summary>One column: everything drawn for one of the two saves.</summary>
        private sealed class Side
        {
            public Text When;
            public Text Verdict;
            public Text Also;
            public Cell[] Cells;
        }

        /// <summary>One value in one column. The wash and the bar show only on the side that is
        /// ahead, so a row lights on exactly one side or on neither.</summary>
        private sealed class Cell
        {
            public RectTransform Root;
            public Image Wash;
            public Image Bar;
            public Text Value;
            public Text Lead;
        }

        private const float CardWidth = 1180f;

        /// <summary>
        /// Sized for the 20:9 canvas, which is the short one: the scaler blends width and height,
        /// so a 2400x1080 phone gets a canvas about 966 units tall, and this card and its ribbon
        /// have to sit inside that with a margin. 880 leaves roughly 40 above and below.
        /// </summary>
        private const float CardHeight = 880f;
        private const float ColumnWidth = 480f;
        private const float ColumnTop = 148f;

        /// <summary>Where each block starts inside a column, from the column's top edge.</summary>
        private const float WhenTop = 56f;
        private const float VerdictTop = 88f;
        private const float VerdictHeight = 64f;
        private const float RowsTop = 160f;
        private const float RowHeight = 45f;
        private const float ColumnHeight = RowsTop + SaveComparison.MaxRows * RowHeight + 36f;

        /// <summary>
        /// Distance from the card's centre to each column's centre.
        ///
        /// It was ColumnWidth/2 + 60, which put the two columns 120 apart and left the row labels
        /// between them overlapping both. "EMBER SHARDS" is wider than 120 at any size worth
        /// reading, so the widest label decided this rather than a round number: 335 leaves a 190
        /// gap, which the labels fit inside, and still leaves 15 units of card margin either side
        /// (335 + 240 = 575, against a half-width of 590).
        /// </summary>
        private const float ColumnOffset = 335f;

        /// <summary>The label strip down the middle, sized to the gap the columns leave rather
        /// than to the longest word - so a longer label in future overflows visibly into a gap
        /// instead of invisibly onto a column.</summary>
        private const float LabelWidth = 190f;

        /// <summary>The right-hand strip of a cell that holds the lead ("+2M", "+30 levels").</summary>
        private const float LeadWidth = 128f;

        /// <summary>The colour that means "this side has more": the kit's success green. Gold is
        /// structure on this card and ember is the two buttons, so a third warm colour would read
        /// as one more frame rather than as a difference.</summary>
        private static readonly Color Ahead = UITheme.Success;

        public ConflictChooser(RectTransform layer)
        {
            _root = UIBuilder.Node("ConflictChooser", layer);
            UIBuilder.FillParent(_root, 0f);

            Image dim = _root.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimReveal;

            RectTransform card = UIBuilder.Node("Card", _root);
            UIBuilder.Box(card, 0f, 0f, CardWidth, CardHeight, new Vector2(0.5f, 0.5f));
            // The kit's framed panel with its ornaments. The warning-coloured border is gone and
            // is not replaced by a differently coloured frame: every panel in Emberforge is gold,
            // and a card that framed itself in amber to say "be careful" was saying it with the
            // one channel the kit uses for structure. The warning is still on the card, in words,
            // above the two buttons - which is where a player reads it.
            EmberforgeUI.PanelOn(card, true);

            // A ribbon, because this card has exactly one subject.
            EmberforgeUI.TitleRibbon("Title", card, "TWO SAVES", CardWidth - Emberforge.RibbonOverhang * 2f);

            _reason = EmberforgeUI.Body("Reason", card, "", UITheme.FontTiny, Emberforge.TextQuiet,
                                        TextAnchor.UpperCenter);
            UIBuilder.Box(_reason.rectTransform, 0f, -84f, CardWidth - 120f, 56f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_reason, Emberforge.Outline, Emberforge.StrokeThin);

            _local = BuildColumn(card, "Local", "THIS DEVICE", -ColumnOffset);
            _cloud = BuildColumn(card, "Cloud", "THE CLOUD", ColumnOffset);

            // The row labels run down the middle, between the two columns, so each value is read
            // against its own name rather than against the value opposite it.
            _labels = new Text[SaveComparison.MaxRows];
            for (int i = 0; i < _labels.Length; i++)
            {
                _labels[i] = EmberforgeUI.Label("Row" + i, card, "", UITheme.FontTiny - 2,
                                                Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Box(_labels[i].rectTransform, 0f, -(ColumnTop + RowsTop + i * RowHeight),
                              LabelWidth, RowHeight, new Vector2(0.5f, 1f));
            }

            Text warning = EmberforgeUI.Body("Warning", card,
                "Whichever you keep replaces the other. There is no way back, and nothing is merged.",
                UITheme.FontTiny, UITheme.Warning, TextAnchor.UpperCenter);
            UIBuilder.Box(warning.rectTransform, 0f, 132f, CardWidth - 140f, 32f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(warning, Emberforge.Outline, Emberforge.StrokeThin);

            // BOTH SECONDARY, both the same size. Neither is the recommended one because there is no
            // recommendation to make - see the class note - and orange means "the thing to press now",
            // which a choice the game does not favour has none of (R53). There is no way out on this card.
            _keepLocal = EmberforgeUI.Button("KeepLocal", card, "KEEP THIS DEVICE", UITheme.FontBody,
                                             ForgeButtonKind.Secondary);
            UIBuilder.Box(_keepLocal.Root, -ColumnOffset, 32f, ColumnWidth, 88f, new Vector2(0.5f, 0f));
            _keepLocal.OnClick(delegate { Answer(ConflictChoice.KeepLocal); });

            _keepCloud = EmberforgeUI.Button("KeepCloud", card, "KEEP THE CLOUD", UITheme.FontBody,
                                             ForgeButtonKind.Secondary);
            UIBuilder.Box(_keepCloud.Root, ColumnOffset, 32f, ColumnWidth, 88f, new Vector2(0.5f, 0f));
            _keepCloud.OnClick(delegate { Answer(ConflictChoice.KeepCloud); });

            _root.gameObject.SetActive(false);
        }

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        private Side BuildColumn(RectTransform card, string name, string heading, float x)
        {
            Side side = new Side();
            RectTransform column = UIBuilder.Node(name, card);
            UIBuilder.Box(column, x, -ColumnTop, ColumnWidth, ColumnHeight, new Vector2(0.5f, 1f));
            // A RECESS, because a column is one of two things set into the card rather than a
            // second card on top of it - and because two sunken columns either side of the label
            // strip is what makes the strip read as running BETWEEN them.
            EmberforgeUI.SunkenOn(column);

            float inner = ColumnWidth - 32f;

            Text title = EmberforgeUI.Title("Heading", column, heading, UITheme.FontHeading,
                                            Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(title.rectTransform, 0f, -14f, inner, 40f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            side.When = EmberforgeUI.Label("When", column, "", UITheme.FontTiny, Emberforge.TextBody,
                                           TextAnchor.UpperCenter);
            UIBuilder.Box(side.When.rectTransform, 0f, -WhenTop, inner, 28f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(side.When, Emberforge.Outline, Emberforge.StrokeThin);

            side.Verdict = EmberforgeUI.Body("Verdict", column, "", UITheme.FontSmall, Emberforge.TextQuiet,
                                             TextAnchor.MiddleCenter);
            UIBuilder.Box(side.Verdict.rectTransform, 0f, -VerdictTop, inner, VerdictHeight, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(side.Verdict, Emberforge.Outline, Emberforge.StrokeThin);

            side.Cells = new Cell[SaveComparison.MaxRows];
            for (int i = 0; i < side.Cells.Length; i++) side.Cells[i] = BuildCell(column, i);

            side.Also = EmberforgeUI.Label("Also", column, "", UITheme.FontTiny - 2, Emberforge.TextQuiet,
                                           TextAnchor.UpperCenter);
            UIBuilder.Box(side.Also.rectTransform, 0f, -(RowsTop + SaveComparison.MaxRows * RowHeight + 4f),
                          inner, 28f, new Vector2(0.5f, 1f));
            BestFit(side.Also, UITheme.FontTiny - 6, UITheme.FontTiny - 2);

            return side;
        }

        private static Cell BuildCell(RectTransform column, int i)
        {
            Cell cell = new Cell();
            cell.Root = UIBuilder.Node("Cell" + i, column);
            UIBuilder.Box(cell.Root, 0f, -(RowsTop + i * RowHeight + 2f), ColumnWidth - 20f, RowHeight - 4f,
                          new Vector2(0.5f, 1f));

            cell.Wash = cell.Root.gameObject.AddComponent<Image>();
            cell.Wash.sprite = TextureLab.White;
            cell.Wash.color = UITheme.WithAlpha(Ahead, 0.14f);
            cell.Wash.raycastTarget = false;

            RectTransform bar = UIBuilder.Node("Bar", cell.Root);
            UIBuilder.Box(bar, 0f, 0f, 5f, RowHeight - 4f, new Vector2(0f, 0.5f));
            bar.pivot = new Vector2(0f, 0.5f);
            cell.Bar = bar.gameObject.AddComponent<Image>();
            cell.Bar.sprite = TextureLab.White;
            cell.Bar.color = Ahead;
            cell.Bar.raycastTarget = false;

            // Best fit, because BEST HEROES carries three names and a hero's name is content: a
            // long one shrinks on its own row rather than running under the lead.
            cell.Value = EmberforgeUI.Label("Value", cell.Root, "", UITheme.FontBody, Emberforge.TextBody,
                                            TextAnchor.MiddleCenter);
            UIBuilder.Box(cell.Value.rectTransform, 8f - LeadWidth / 2f, 0f, ColumnWidth - 48f - LeadWidth,
                          RowHeight - 4f, new Vector2(0.5f, 0.5f));
            BestFit(cell.Value, UITheme.FontTiny - 4, UITheme.FontBody);
            UIBuilder.Outlined(cell.Value, Emberforge.Outline, Emberforge.StrokeThin);

            cell.Lead = EmberforgeUI.Label("Lead", cell.Root, "", UITheme.FontTiny, Ahead, TextAnchor.MiddleRight);
            UIBuilder.Box(cell.Lead.rectTransform, -12f, 0f, LeadWidth, RowHeight - 4f, new Vector2(1f, 0.5f));
            cell.Lead.rectTransform.pivot = new Vector2(1f, 0.5f);
            BestFit(cell.Lead, UITheme.FontTiny - 6, UITheme.FontTiny);
            UIBuilder.Outlined(cell.Lead, Emberforge.Outline, Emberforge.StrokeThin);
            return cell;
        }

        /// <summary>Shrink to fit on one line. Best fit only shrinks a label that may wrap - see
        /// EmberforgeUI.TitleRibbon for why - so wrapping goes on and vertical overflow comes off.</summary>
        private static void BestFit(Text label, int min, int max)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = min;
            label.resizeTextMaxSize = max;
        }

        /// <summary>
        /// Opens the card on a decided conflict. `outcome` carries both summaries already - see
        /// SyncOutcome, which fills them for every plan and not only this one, precisely so this
        /// method has nothing to fetch and nothing to guess.
        /// </summary>
        public void Ask(SyncOutcome outcome, Action<ConflictChoice> answer)
        {
            if (outcome == null || answer == null) return;
            _answer = answer;

            _reason.text = outcome.Reason;

            Game.GameApp app = Game.GameApp.Instance;
            SaveComparison comparison = SaveComparison.Of(outcome.Local, outcome.Cloud,
                                                          app != null ? app.Content : null);

            for (int i = 0; i < _labels.Length; i++)
                _labels[i].text = i < comparison.Rows.Count ? comparison.Rows[i].Label : "";

            bool namesDiffer = outcome.Local != null && outcome.Cloud != null
                               && outcome.Local.Readable && outcome.Cloud.Readable
                               && NameOf(outcome.Local) != NameOf(outcome.Cloud);

            Fill(_local, comparison, SaveSide.Local, outcome.Local, namesDiffer, "");
            Fill(_cloud, comparison, SaveSide.Cloud, outcome.Cloud, namesDiffer,
                 outcome.Record != null ? outcome.Record.DeviceId : "");

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            if (app != null && app.Layers != null && _layer == null)
                _layer = app.Layers.PushBlocking("popup:conflict", null);
        }

        private static void Fill(Side side, SaveComparison comparison, SaveSide which, SaveSummary summary,
                                 bool namesDiffer, string deviceId)
        {
            bool readable = summary != null && summary.Readable;
            bool leads = which == SaveSide.Local ? comparison.LocalLeads : comparison.CloudLeads;

            side.When.text = readable ? When(summary, namesDiffer, deviceId) : "";
            side.Verdict.text = which == SaveSide.Local ? comparison.LocalVerdict : comparison.CloudVerdict;
            side.Verdict.color = !readable ? UITheme.Warning : (leads ? Ahead : Emberforge.TextQuiet);
            side.Also.text = which == SaveSide.Local ? comparison.LocalAlso : comparison.CloudAlso;

            for (int i = 0; i < side.Cells.Length; i++)
            {
                Cell cell = side.Cells[i];
                bool used = i < comparison.Rows.Count;
                cell.Root.gameObject.SetActive(used);
                if (!used) continue;

                SaveComparisonRow row = comparison.Rows[i];
                bool ahead = row.Ahead == which;
                bool behind = row.Ahead != SaveSide.Neither && !ahead;

                cell.Value.text = which == SaveSide.Local ? row.LocalText : row.CloudText;
                cell.Value.color = ahead ? Emberforge.GoldLit : (behind ? Emberforge.TextQuiet : Emberforge.TextBody);
                cell.Lead.text = ahead ? row.Lead : "";
                cell.Wash.enabled = ahead;
                cell.Bar.enabled = ahead;
            }
        }

        private static string NameOf(SaveSummary summary)
        {
            return string.IsNullOrEmpty(summary.PlayerName) ? "Wayfarer" : summary.PlayerName;
        }

        /// <summary>
        /// "Saved 3 hours ago on another device". The writing device is named only for the cloud
        /// side, where it answers a real question - "was that me on my tablet?" - and only as "this
        /// device" or "another device", never as the id itself: the id is a random GUID. The cloud
        /// revision used to sit beside it; it answers nothing a player asks, and the reason line at
        /// the top already carries it. The name leads only when the two saves disagree on it.
        /// </summary>
        private static string When(SaveSummary summary, bool namesDiffer, string deviceId)
        {
            string text = summary.LastSaveUnix > 0 ? "Saved " + Ago(summary.LastSaveUnix) : "Never saved";
            if (namesDiffer) text = NameOf(summary) + ", " + char.ToLowerInvariant(text[0]) + text.Substring(1);
            if (string.IsNullOrEmpty(deviceId)) return text;
            bool mine = Game.GameApp.Instance != null && Game.GameApp.Instance.Cloud != null
                        && deviceId == Game.GameApp.Instance.Cloud.State.DeviceId;
            return text + (mine ? " on this device" : " on another device");
        }

        /// <summary>
        /// How long ago, in the coarsest unit that is still true. A wall-clock time would be
        /// worse here than it looks: the two saves may have been written on devices whose clocks
        /// disagree, and printing "14:32" against "14:29" invites a comparison the numbers cannot
        /// support. "3 hours ago" against "just now" says the same thing without implying the
        /// clocks were ever synchronised.
        /// </summary>
        private static string Ago(long unix)
        {
            long seconds = TimeUtil.NowUnix() - unix;
            if (seconds < 0) return "just now";
            if (seconds < 90) return "just now";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + " minute" + (minutes == 1 ? "" : "s") + " ago";
            long hours = minutes / 60;
            if (hours < 48) return hours + " hour" + (hours == 1 ? "" : "s") + " ago";
            long days = hours / 24;
            return days + " day" + (days == 1 ? "" : "s") + " ago";
        }

        private void Answer(ConflictChoice choice)
        {
            Action<ConflictChoice> answer = _answer;
            _answer = null;

            UiLayerStack.Layer layer = _layer;
            _layer = null;
            if (layer != null && Game.GameApp.Instance != null && Game.GameApp.Instance.Layers != null)
                Game.GameApp.Instance.Layers.Dismiss(layer);

            _root.gameObject.SetActive(false);
            if (Game.GameApp.Instance != null && Game.GameApp.Instance.Audio != null)
                Game.GameApp.Instance.Audio.Tap();
            if (answer != null) answer(choice);
        }
    }
}
