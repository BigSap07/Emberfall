using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE DAILY ORDERS (DESIGN-DAILY-LOOP.md): today's five orders, the daily and weekly chests and
    /// the seven day login calendar, on one page. Reached from the hub's rail, and only when
    /// DailyLoopSwitch is on: with it off the router never builds this screen and the hub draws no
    /// door to it.
    ///
    /// A PLACEHOLDER LAYOUT IN THE EMBERFORGE KIT, and ALL of it lives in this file. Panels, rows,
    /// chips and buttons are the kit's own; nothing here is a new component. It decides nothing: every
    /// figure, every state and every reward is read off the LoopBoard that DailyLoop.Board returns,
    /// and every button calls a DailyLoop claim and repaints from what it answers. The countdown, the
    /// progress counts and the previewed rewards are all on the board already.
    ///
    /// Rewards are shown as minutes or hours of idle income with the gold and shards they are worth
    /// at the player's frontier today, because that is what the rules pay.
    /// </summary>
    public sealed class OrdersScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Orders; } }

        /// <summary>The shared top bar stays, because it carries the purses a claim pays into and
        /// the page needs the height a strip of its own would take: at 2400x1080 the logical canvas
        /// is about 966 tall, and this page is 652 tall under a header that ends at 260.</summary>
        public override bool HidesTopBar { get { return false; } }

        private const float ContentWidth = 1760f;
        private const float OrdersWidth = 1160f;
        private const float OrdersHeight = 446f;
        private const float SideX = 1180f;
        private const float SideWidth = 580f;
        private const float CardHeight = 215f;
        private const float CardGap = 16f;
        private const float CalendarTop = OrdersHeight + 14f;
        private const float CalendarHeight = 192f;

        private const float PanelPad = 28f;
        private const float TitleBlock = 60f;
        private const float RowHeight = 68f;
        private const float RowGap = 8f;
        private const float RowPad = 20f;

        private const float CellWidth = 234f;
        private const float CellHeight = 112f;
        private const float CellTop = 64f;
        private const float CellGap = 12f;

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 MidLeft = new Vector2(0f, 0.5f);

        private Text _headerSubtitle;

        private RectTransform _ordersBody;
        private RectTransform _dailyBody;
        private RectTransform _weeklyBody;
        private RectTransform _calendarBody;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "DAILY ORDERS", Router, out _headerSubtitle);
            if (WayfarersRoadSwitch.Enabled) BuildRoadDoor();

            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space8;
            RectTransform content = UIBuilder.Node("Content", Root);
            UIBuilder.Box(content, 0f, -top, ContentWidth, CalendarTop + CalendarHeight, new Vector2(0.5f, 1f));

            _ordersBody = Panel(content, "Orders", 0f, 0f, OrdersWidth, OrdersHeight);
            _dailyBody = Panel(content, "DailyChest", SideX, 0f, SideWidth, CardHeight);
            _weeklyBody = Panel(content, "WeeklyChest", SideX, CardHeight + CardGap, SideWidth, CardHeight);
            _calendarBody = Panel(content, "Calendar", 0f, CalendarTop, ContentWidth, CalendarHeight);
        }

        /// <summary>
        /// WAYFARER'S ROAD'S DOOR (Docs/DESIGN-WAYFARERS-ROAD.md): top right, in the header band beside
        /// the ribbon, because the pass counts this page's claims. Only with the switch on.
        /// </summary>
        private void BuildRoadDoor()
        {
            UIButton door = EmberforgeUI.Button("WayfarersRoad", Root, "WAYFARER'S ROAD", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(door.Root, -UITheme.ScreenPadding, -(ScreenChrome.HeaderTop + 24f), 320f, 56f, new Vector2(1f, 1f));
            door.OnClick(delegate
            {
                App.Audio.Tap();
                Router.Show(ScreenId.WayfarersRoad);
            });
        }

        /// <summary>A framed panel at a top-left offset, and the empty node a repaint fills.</summary>
        private static RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h)
        {
            RectTransform panel = UIBuilder.Node(name, parent);
            UIBuilder.Box(panel, x, -y, w, h, TopLeft);
            EmberforgeUI.PanelOn(panel, true);
            RectTransform body = UIBuilder.Node("Body", panel);
            UIBuilder.FillParent(body, 0f);
            return body;
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            LoopBoard board = DailyLoop.Board(App.Session, now);

            Clear(_ordersBody);
            Clear(_dailyBody);
            Clear(_weeklyBody);
            Clear(_calendarBody);

            if (!board.Active)
            {
                _headerSubtitle.text = "NOT OPEN";
                Text closed = EmberforgeUI.Label("Closed", _ordersBody, "Daily orders are not open in this build.",
                                                 UITheme.FontBody, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(closed.rectTransform, 0f);
                return;
            }

            _headerSubtitle.text = "NEW ORDERS IN " + Countdown(board.ResetInSeconds);
            BuildOrders(board);
            BuildDailyChest(board);
            BuildWeeklyChest(board);
            BuildCalendar(board);
        }

        private static void Clear(RectTransform body)
        {
            for (int i = body.childCount - 1; i >= 0; i--) Object.Destroy(body.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ the orders

        private void BuildOrders(LoopBoard board)
        {
            int claimed = 0;
            for (int i = 0; i < board.Orders.Count; i++) if (board.Orders[i].Claimed) claimed++;

            Text title = EmberforgeUI.Title("Title", _ordersBody, "TODAY'S ORDERS", UITheme.FontBody,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, PanelPad + 8f, -16f, 600f, 40f, TopLeft);
            EmberforgeUI.ClearCornerOrnament(title.rectTransform);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text count = EmberforgeUI.Label("Count", _ordersBody, claimed + " OF " + board.Orders.Count + " CLAIMED",
                                            UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.UpperRight);
            UIBuilder.Box(count.rectTransform, OrdersWidth - PanelPad - 8f - 400f, -20f, 400f, 32f, TopLeft);

            for (int i = 0; i < board.Orders.Count; i++) BuildOrderRow(board.Orders[i], i);
        }

        private void BuildOrderRow(OrderView o, int index)
        {
            float y = TitleBlock + index * (RowHeight + RowGap);
            RectTransform row = UIBuilder.Node("Order_" + o.Id, _ordersBody);
            float rowWidth = OrdersWidth - PanelPad * 2f;
            UIBuilder.Box(row, PanelPad, -y, rowWidth, RowHeight, TopLeft);
            if (o.Claimed) EmberforgeUI.SunkenOn(row); else EmberforgeUI.PlainPanelOn(row);

            Color text = o.Claimed ? Emberforge.TextQuiet : Emberforge.TextBody;
            Text line = EmberforgeUI.Body("Line", row, o.Line, UITheme.FontSmall, text, TextAnchor.MiddleLeft);
            UIBuilder.Box(line.rectTransform, RowPad, 0f, 500f, RowHeight - 8f, MidLeft);

            Text progress = EmberforgeUI.Label("Progress", row, o.Progress + " / " + o.Target, UITheme.FontTiny,
                                               o.Done ? UITheme.Success : Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(progress.rectTransform, 540f, 16f, 170f, 26f, MidLeft);
            Image bar = EmberforgeUI.ProgressBar("Bar", row);
            UIBuilder.Box(bar.rectTransform.parent as RectTransform, 540f, -14f, 170f, 22f, MidLeft);
            EmberforgeUI.SetProgress(bar, o.Target > 0 ? o.Progress / (float)o.Target : 0f);

            Text hours = EmberforgeUI.Label("Reward", row, FormatHours(o.Hours), UITheme.FontSmall,
                                            o.Claimed ? Emberforge.TextQuiet : Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(hours.rectTransform, 730f, 16f, 190f, 30f, MidLeft);
            Text worth = EmberforgeUI.Label("Worth", row, Worth(o.Gold, o.Shards), UITheme.FontTiny,
                                            Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(worth.rectTransform, 730f, -14f, 190f, 26f, MidLeft);

            string id = o.Id;
            if (o.Claimed)
            {
                RectTransform chip = EmberforgeUI.Chip("Claimed", row, "CLAIMED", UITheme.Success, UITheme.FontTiny);
                UIBuilder.Box(chip, 934f, 0f, 150f, 44f, MidLeft);
            }
            else
            {
                UIButton claim = EmberforgeUI.Button("Claim", row, "CLAIM", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(claim.Root, 934f, 0f, 150f, 56f, MidLeft);
                claim.Interactable = o.CanClaim;
                claim.OnClick(delegate { OnClaimOrder(id); });
            }
        }

        // ------------------------------------------------------------------ the chests

        private void BuildDailyChest(LoopBoard board)
        {
            int claimed = 0;
            for (int i = 0; i < board.Orders.Count; i++) if (board.Orders[i].Claimed) claimed++;
            ChestCard(_dailyBody, "DAILY CHEST",
                      claimed + " of " + board.Orders.Count + " orders",
                      board.Orders.Count > 0 ? claimed / (float)board.Orders.Count : 0f,
                      board.ChestHours, board.ChestGold, board.ChestShards,
                      board.ChestClaimed, board.ChestCanClaim, "OPEN CHEST", OnClaimDaily);
        }

        private void BuildWeeklyChest(LoopBoard board)
        {
            ChestCard(_weeklyBody, "WEEKLY CHEST",
                      board.WeekDays + " of " + board.WeekRequired + " days",
                      board.WeekRequired > 0 ? Mathf.Min(1f, board.WeekDays / (float)board.WeekRequired) : 0f,
                      board.WeeklyHours, board.WeeklyGold, board.WeeklyShards,
                      board.WeeklyClaimed, board.WeeklyCanClaim, "OPEN CHEST", OnClaimWeekly);
        }

        private void ChestCard(RectTransform body, string title, string counted, float ratio, double hours,
                               int gold, int shards, bool opened, bool canOpen, string buttonText,
                               System.Action action)
        {
            Text name = EmberforgeUI.Title("Title", body, title, UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, PanelPad + 8f, -16f, 400f, 40f, TopLeft);
            EmberforgeUI.ClearCornerOrnament(name.rectTransform);
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            Text line = EmberforgeUI.Label("Counted", body, counted, UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(line.rectTransform, PanelPad + 8f, -58f, 230f, 30f, TopLeft);
            Image bar = EmberforgeUI.ProgressBar("Bar", body);
            UIBuilder.Box(bar.rectTransform.parent as RectTransform, PanelPad + 250f, -62f, SideWidth - PanelPad * 2f - 258f, 22f, TopLeft);
            EmberforgeUI.SetProgress(bar, ratio);

            Text reward = EmberforgeUI.Label("Reward", body, FormatHours(hours) + " OF IDLE INCOME", UITheme.FontSmall,
                                             Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(reward.rectTransform, PanelPad + 8f, -92f, SideWidth - PanelPad * 2f, 30f, TopLeft);
            Text worth = EmberforgeUI.Label("Worth", body, Worth(gold, shards), UITheme.FontTiny, Emberforge.TextQuiet,
                                            TextAnchor.UpperLeft);
            UIBuilder.Box(worth.rectTransform, PanelPad + 8f, -120f, SideWidth - PanelPad * 2f, 26f, TopLeft);

            if (opened)
            {
                RectTransform chip = EmberforgeUI.Chip("Opened", body, "OPENED", UITheme.Success, UITheme.FontSmall);
                UIBuilder.Box(chip, PanelPad, -(CardHeight - 12f - 46f), SideWidth - PanelPad * 2f, 46f, TopLeft);
                return;
            }
            UIButton open = EmberforgeUI.Button("Open", body, buttonText, UITheme.FontTiny, ForgeButtonKind.Primary);
            UIBuilder.Box(open.Root, PanelPad, -(CardHeight - 12f - 50f), SideWidth - PanelPad * 2f, 50f, TopLeft);
            open.Interactable = canOpen;
            open.OnClick(action);
        }

        // ------------------------------------------------------------------ the calendar

        private void BuildCalendar(LoopBoard board)
        {
            Text title = EmberforgeUI.Title("Title", _calendarBody, "LOGIN CALENDAR", UITheme.FontBody,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, PanelPad + 8f, -16f, 420f, 40f, TopLeft);
            EmberforgeUI.ClearCornerOrnament(title.rectTransform);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text note = EmberforgeUI.Label("Note", _calendarBody, "ONE REWARD A DAY. IT REPEATS AFTER DAY 7.",
                                           UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(note.rectTransform, PanelPad + 440f, -24f, 640f, 28f, TopLeft);

            int next = 0;
            for (int i = 0; i < board.Calendar.Count; i++) if (board.Calendar[i].Next) next = board.Calendar[i].Day;
            UIButton claim = EmberforgeUI.Button("ClaimDay", _calendarBody,
                                                 board.CalendarCanClaim ? "CLAIM DAY " + next : "COME BACK TOMORROW",
                                                 UITheme.FontTiny, ForgeButtonKind.Primary);
            UIBuilder.Box(claim.Root, ContentWidth - PanelPad - 360f, -10f, 360f, 50f, TopLeft);
            claim.Interactable = board.CalendarCanClaim;
            claim.OnClick(OnClaimCalendar);

            float startX = (ContentWidth - (CellWidth * 7f + CellGap * 6f)) * 0.5f;
            for (int i = 0; i < board.Calendar.Count; i++)
                BuildCalendarCell(board.Calendar[i], startX + i * (CellWidth + CellGap));
        }

        private void BuildCalendarCell(CalendarView c, float x)
        {
            RectTransform cell = UIBuilder.Node("Day_" + c.Day, _calendarBody);
            UIBuilder.Box(cell, x, -CellTop, CellWidth, CellHeight, TopLeft);
            if (c.Next) EmberforgeUI.PanelOn(cell, false);
            else if (c.Claimed) EmberforgeUI.SunkenOn(cell);
            else EmberforgeUI.PlainPanelOn(cell);

            string caption = c.Label.Length > 0 ? c.Label.ToUpper() : "DAY " + c.Day;
            Text day = EmberforgeUI.Label("Day", cell, caption, UITheme.FontTiny,
                                          c.Claimed ? Emberforge.TextQuiet : Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(day.rectTransform, 16f, -10f, CellWidth - 32f, 26f, TopLeft);

            Text hours = EmberforgeUI.Title("Hours", cell, FormatHours(c.Hours), UITheme.FontBody,
                                            c.Claimed ? Emberforge.TextQuiet : Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(hours.rectTransform, 16f, -36f, CellWidth - 32f, 38f, TopLeft);

            Text worth = EmberforgeUI.Label("Worth", cell, Worth(c.Gold, c.Shards), UITheme.FontTiny,
                                            Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(worth.rectTransform, 16f, -76f, CellWidth - 32f, 26f, TopLeft);

            if (c.Claimed || c.Next)
            {
                RectTransform chip = EmberforgeUI.Chip("State", cell, c.Claimed ? "DONE" : "NEXT",
                                                       c.Claimed ? UITheme.Success : Emberforge.TextGold, UITheme.FontTiny);
                UIBuilder.Box(chip, CellWidth - 16f - 92f, -10f, 92f, 32f, TopLeft);
            }
        }

        // ------------------------------------------------------------------ actions

        private void OnClaimOrder(string id)
        {
            Paid(DailyLoop.ClaimOrder(App.Session, id, TimeUtil.NowUnix()));
        }

        private void OnClaimDaily()
        {
            Paid(DailyLoop.ClaimDailyChest(App.Session, TimeUtil.NowUnix()));
        }

        private void OnClaimWeekly()
        {
            Paid(DailyLoop.ClaimWeeklyChest(App.Session, TimeUtil.NowUnix()));
        }

        private void OnClaimCalendar()
        {
            Paid(DailyLoop.ClaimCalendar(App.Session, TimeUtil.NowUnix()));
        }

        /// <summary>Says what a claim paid, or why it did not, and repaints from the rules.</summary>
        private void Paid(LoopPayout payout)
        {
            if (payout.Success)
            {
                App.Audio.Reward();
                App.Notify("+" + HudTopBar.Format(payout.Gold) + " gold, +" + HudTopBar.Format(payout.Shards) + " shards");
                App.RequestSave();
                App.RefreshHud();
            }
            else if (payout.Message.Length > 0) App.Notify(payout.Message);
            Rebuild();
        }

        // ------------------------------------------------------------------ words

        /// <summary>Hours of idle income as a player reads them: 15 MIN, 45 MIN, 1 H, 1.5 H.</summary>
        private static string FormatHours(double hours)
        {
            double minutes = hours * 60.0;
            if (minutes < 59.5) return Mathf.RoundToInt((float)minutes) + " MIN";
            return hours.ToString("0.##") + " H";
        }

        private static string Worth(int gold, int shards)
        {
            return HudTopBar.Format(gold) + " gold   " + HudTopBar.Format(shards) + " shards";
        }

        private static string Countdown(long seconds)
        {
            long minutes = seconds / 60L;
            return (minutes / 60L) + "H " + (minutes % 60L).ToString("00") + "M";
        }
    }
}
