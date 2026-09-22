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
    /// The four facts per side are SaveSummary's, read out of each document without deserialising
    /// it - so a save from a build this one cannot fully read is still described rather than shown
    /// as a blank the player is asked to gamble on.
    /// </summary>
    public sealed class ConflictChooser
    {
        private readonly RectTransform _root;
        private readonly Text _reason;
        private readonly Text _localWhen;
        private readonly Text _cloudWhen;
        private readonly Text _localDevice;
        private readonly Text _cloudDevice;
        private readonly Text[] _localValues;
        private readonly Text[] _cloudValues;
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

        /// <summary>The rows, in the order the card lists them. One array so the two columns
        /// cannot drift out of step with each other or with the labels between them.</summary>
        private static readonly string[] RowLabels = { "WAYFARER", "RANK", "STAGES CLEARED", "HEROES OWNED" };

        private const float CardWidth = 1180f;
        private const float CardHeight = 720f;
        private const float ColumnWidth = 480f;
        private const float RowHeight = 54f;

        /// <summary>
        /// Distance from the card's centre to each column's centre.
        ///
        /// It was ColumnWidth/2 + 60, which put the two columns 120 apart and left the row labels
        /// between them overlapping both. "STAGES CLEARED" is wider than 120 at any size worth
        /// reading, so the widest label decided this rather than a round number: 335 leaves a 190
        /// gap, which the labels fit inside, and still leaves 15 units of card margin either side
        /// (335 + 240 = 575, against a half-width of 590).
        /// </summary>
        private const float ColumnOffset = 335f;

        /// <summary>The label strip down the middle, sized to the gap the columns leave rather
        /// than to the longest word - so a longer label in future overflows visibly into a gap
        /// instead of invisibly onto a column.</summary>
        private const float LabelWidth = 190f;

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
            Text title = EmberforgeUI.TitleRibbon("Title", card, "TWO SAVES",
                                                  CardWidth - Emberforge.RibbonOverhang * 2f);

            _reason = EmberforgeUI.Body("Reason", card, "", UITheme.FontSmall, Emberforge.TextBody,
                                        TextAnchor.UpperCenter);
            UIBuilder.Box(_reason.rectTransform, 0f, -88f, CardWidth - 120f, 68f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_reason, Emberforge.Outline, Emberforge.StrokeThin);

            float columnTop = 172f;

            _localValues = new Text[RowLabels.Length];
            _cloudValues = new Text[RowLabels.Length];

            RectTransform local = BuildColumn(card, "Local", "THIS DEVICE", -ColumnOffset,
                                              columnTop, _localValues, out _localWhen, out _localDevice);
            RectTransform cloud = BuildColumn(card, "Cloud", "THE CLOUD", ColumnOffset,
                                              columnTop, _cloudValues, out _cloudWhen, out _cloudDevice);

            // The row labels run down the middle, between the two columns, so each number is read
            // against its own name rather than against the number opposite it.
            for (int i = 0; i < RowLabels.Length; i++)
            {
                Text label = EmberforgeUI.Label("Row" + i, card, RowLabels[i], UITheme.FontTiny - 2,
                                                Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Box(label.rectTransform, 0f, -(columnTop + 58f + i * RowHeight),
                              LabelWidth, RowHeight, new Vector2(0.5f, 1f));
            }

            Text warning = EmberforgeUI.Body("Warning", card,
                "Whichever you keep replaces the other. There is no way back, and nothing is merged.",
                UITheme.FontTiny, UITheme.Warning, TextAnchor.UpperCenter);
            UIBuilder.Box(warning.rectTransform, 0f, 148f, CardWidth - 140f, 32f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(warning, Emberforge.Outline, Emberforge.StrokeThin);

            // BOTH PRIMARY, both the same size. Neither is the recommended one because there is no
            // recommendation to make - see the class note. This is the one place in the game where
            // two ember buttons sit side by side, and it is the exception the two-voice rule is
            // worth making: teal means "the way back out", and on this card there is no way out.
            _keepLocal = EmberforgeUI.Button("KeepLocal", card, "KEEP THIS DEVICE", UITheme.FontBody,
                                             ForgeButtonKind.Primary);
            UIBuilder.Box(_keepLocal.Root, -ColumnOffset, 40f, ColumnWidth, 92f, new Vector2(0.5f, 0f));
            _keepLocal.OnClick(delegate { Answer(ConflictChoice.KeepLocal); });

            _keepCloud = EmberforgeUI.Button("KeepCloud", card, "KEEP THE CLOUD", UITheme.FontBody,
                                             ForgeButtonKind.Primary);
            UIBuilder.Box(_keepCloud.Root, ColumnOffset, 40f, ColumnWidth, 92f, new Vector2(0.5f, 0f));
            _keepCloud.OnClick(delegate { Answer(ConflictChoice.KeepCloud); });

            _root.gameObject.SetActive(false);
        }

        public bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }

        private RectTransform BuildColumn(RectTransform card, string name, string heading, float x, float top,
                                          Text[] values, out Text when, out Text device)
        {
            RectTransform column = UIBuilder.Node(name, card);
            UIBuilder.Box(column, x, -top, ColumnWidth, CardHeight - top - 200f, new Vector2(0.5f, 1f));
            // A RECESS, because a column is one of two things set into the card rather than a
            // second card on top of it - and because two sunken columns either side of the label
            // strip is what makes the strip read as running BETWEEN them.
            EmberforgeUI.SunkenOn(column);

            Text title = EmberforgeUI.Title("Heading", column, heading, UITheme.FontHeading,
                                            Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(title.rectTransform, 0f, -16f, ColumnWidth - 32f, 40f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            for (int i = 0; i < RowLabels.Length; i++)
            {
                values[i] = EmberforgeUI.Label("Value" + i, column, "", UITheme.FontBody,
                                               Emberforge.TextBody, TextAnchor.MiddleCenter);
                UIBuilder.Box(values[i].rectTransform, 0f, -(58f + i * RowHeight), ColumnWidth - 32f,
                              RowHeight, new Vector2(0.5f, 1f));
                UIBuilder.Outlined(values[i], Emberforge.Outline, Emberforge.StrokeThin);
            }

            when = EmberforgeUI.Label("When", column, "", UITheme.FontTiny, Emberforge.TextGold,
                                      TextAnchor.UpperCenter);
            UIBuilder.Box(when.rectTransform, 0f, -(58f + RowLabels.Length * RowHeight + 10f),
                          ColumnWidth - 32f, 30f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(when, Emberforge.Outline, Emberforge.StrokeThin);

            device = EmberforgeUI.Label("Device", column, "", UITheme.FontTiny - 2,
                                        Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(device.rectTransform, 0f, -(58f + RowLabels.Length * RowHeight + 42f),
                          ColumnWidth - 32f, 28f, new Vector2(0.5f, 1f));

            return column;
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

            Fill(_localValues, _localWhen, _localDevice, outcome.Local, "", 0L);
            Fill(_cloudValues, _cloudWhen, _cloudDevice, outcome.Cloud,
                 outcome.Record != null ? outcome.Record.DeviceId : "",
                 outcome.Record != null ? outcome.Record.Revision : 0);

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            Game.GameApp app = Game.GameApp.Instance;
            if (app != null && app.Layers != null && _layer == null)
                _layer = app.Layers.PushBlocking("popup:conflict", null);
        }

        private void Fill(Text[] values, Text when, Text device, SaveSummary summary,
                          string deviceId, long revision)
        {
            if (summary == null || !summary.Readable)
            {
                for (int i = 0; i < values.Length; i++) values[i].text = "-";
                values[0].text = "UNREADABLE";
                when.text = "";
                device.text = "this save cannot be described";
                return;
            }

            values[0].text = string.IsNullOrEmpty(summary.PlayerName) ? "Wayfarer" : summary.PlayerName;
            values[1].text = summary.PlayerLevel.ToString();
            values[2].text = summary.StagesCleared.ToString();
            values[3].text = summary.HeroesOwned.ToString();

            when.text = summary.LastSaveUnix > 0 ? "SAVED " + Ago(summary.LastSaveUnix) : "NEVER SAVED";

            // The writing device is shown only for the cloud side, where it answers a real
            // question - "was that me on my tablet?" - and only as "this device" or "another
            // device", never as the id itself. The id is a random GUID; printing it would be 32
            // characters that mean nothing to anybody.
            if (string.IsNullOrEmpty(deviceId)) { device.text = ""; return; }
            bool mine = Game.GameApp.Instance != null && Game.GameApp.Instance.Cloud != null
                        && deviceId == Game.GameApp.Instance.Cloud.State.DeviceId;
            device.text = (mine ? "written by this device" : "written by another device")
                        + (revision > 0 ? "   -   revision " + revision : "");
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
