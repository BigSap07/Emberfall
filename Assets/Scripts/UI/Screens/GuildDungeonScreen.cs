using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The guild dungeon's front door: the size-scaled ladder, how many attempts the member has left this cycle, how
    /// long is left, and a row for every floor (Docs/ui-specs/R28-SCREENS.md, "Guild dungeon: the size-scaled ladder").
    ///
    /// THIS SCREEN DECIDES NOTHING. It draws GuildDungeonLadderPresenter's view, through GuildDungeonLadderController,
    /// and every string, every count, every state chip and every button's label, enabled flag and Reason is a field of
    /// that view. A tap calls the controller (REFRESH, TRY AGAIN) or the router (ATTEMPT, BOARDS, FIND A GUILD, GUILD
    /// TECH) and nothing else. There is NO button that buys, refills or plays an ad for an attempt: attempts cannot
    /// be bought.
    ///
    /// ATTEMPT OPENS THE FLOOR'S LINE-UP SCREEN. GuildFloorScreen fights the line and files it through
    /// GuildDungeonFlow.Attempt, which stores the attempt BEFORE the fight is shown (a refused one shows no fight), so
    /// this screen never calls RecordAttempt itself: doing both would file one fight twice. When the player comes back
    /// OnShow reloads, which is the reload RecordAttempt would have done.
    ///
    /// A FLOOR ROW KEEPS ITS PEEK AT THE ENEMIES ON THE LINE-UP SCREEN. The row is 96 tall and the view carries no enemy
    /// list; the floor view (GuildFloorScreen) is where a floor is inspected, as it was.
    ///
    /// THE REASON UNDER A DISABLED ATTEMPT is printed inside the row, under the floor's name, left of the button: the
    /// button is a full touch minimum tall and overhangs its 96 tall row, so there is no room under it.
    /// </summary>
    public sealed class GuildDungeonScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.GuildDungeon; } }

        private const float RowSpacing = 14f;           // at least LadderLayout.MinRowSpacing(RowHeight, TouchMin)
        private const float HeaderBottom = ScreenChrome.HeaderTop + 108f;
        private const float BodyTop = HeaderBottom + UITheme.Space8;
        private const float BottomBarHeight = HubStyle.TouchMin + 32f;
        private const float InfoLineA = 36f;
        private const float InfoLineB = 30f;
        private const float PipHeight = 18f;
        private const float BannerHeight = 44f;
        private const float RightColumn = 560f;
        private const float DividerHeight = 56f;       // its own full height, so a caption is never a sliver
        private const float PipsAvailable = 520f;
        private const float ButtonWidth = 260f;
        private const float ListPadTop = 10f;
        private const float ListPadBottom = 24f;

        /// <summary>Shared with the floor, result and boards screens through the router.</summary>
        public readonly GuildDungeonState State = new GuildDungeonState();

        private GuildDungeonLadderController _controller;
        private string _signature = "";
        private float _tick;
        private bool _loading;

        private Text _headerSubtitle;
        private UIButton _refresh;
        private RectTransform _headerSpinner;

        private RectTransform _info;
        private ScrollRect _scroll;
        private RectTransform _content;
        private RectTransform _status;
        private RectTransform _centerSpinner;

        private UIButton _tech;
        private UIButton _boards;
        private Text _boardsReason;

        public bool Loading { get { return _loading; } }

        /// <summary>The controller of the current visit, for the capture tool and the checks. Null before the first OnShow.</summary>
        public GuildDungeonLadderController Controller { get { return _controller; } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            RectTransform header = ScreenChrome.ForgedHeader(Root, new GuildDungeonLadderView().Title, Router, out _headerSubtitle);

            // REFRESH at the header's right, a full touch minimum tall. Its label, state and visibility are the view's.
            _refresh = EmberforgeUI.Button("Refresh", header, "REFRESH", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_refresh.Root, 0f, 0f, ButtonWidth, HubStyle.TouchMin, new Vector2(1f, 0.5f));
            _refresh.OnClick(OnRefresh);
            _headerSpinner = MakeSpinner("RefreshSpinner", header, 44f);
            UIBuilder.Box(_headerSpinner, -(ButtonWidth + UITheme.Space24), 0f, 44f, 44f, new Vector2(1f, 0.5f));

            // The info block sits under the header; the list fills what is left between it and the bottom bar.
            _info = UIBuilder.Node("Info", Root);
            UIBuilder.Box(_info, 0f, -BodyTop, 100f, 100f, new Vector2(0.5f, 1f));
            _info.anchorMin = new Vector2(0f, 1f);
            _info.anchorMax = new Vector2(1f, 1f);
            _info.offsetMin = new Vector2(UITheme.ScreenPadding, -BodyTop - 200f);
            _info.offsetMax = new Vector2(-UITheme.ScreenPadding, -BodyTop);

            _content = UIBuilder.ScrollList("Floors", Root, out _scroll, RowSpacing, new RectOffset(0, 0, (int)ListPadTop, (int)ListPadBottom));
            RectTransform scrollRect = _scroll.GetComponent<RectTransform>();
            UIBuilder.FillParent(scrollRect, UITheme.ScreenPadding, BodyTop + 120f, UITheme.ScreenPadding, BottomBarHeight);

            BuildStatus();
            BuildBottomBar();
        }

        private void BuildStatus()
        {
            _status = UIBuilder.Node("Status", Root);
            UIBuilder.Box(_status, 0f, 0f, 1100f, 420f, new Vector2(0.5f, 0.5f));
            _centerSpinner = MakeSpinner("Spinner", Root, 96f);
            UIBuilder.Box(_centerSpinner, 0f, 0f, 96f, 96f, new Vector2(0.5f, 0.5f));
        }

        private void BuildBottomBar()
        {
            // GUILD TECH keeps the door the old screen had to the tree this dungeon pays into. BOARDS is the view's.
            _tech = EmberforgeUI.Button("Tech", Root, "GUILD TECH", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_tech.Root, UITheme.ScreenPadding, UITheme.Space16, ButtonWidth, HubStyle.TouchMin, new Vector2(0f, 0f));
            _tech.OnClick(delegate { App.Audio.Tap(); Router.Show(ScreenId.GuildTech); });

            _boards = EmberforgeUI.Button("Boards", Root, "BOARDS", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_boards.Root, -UITheme.ScreenPadding, UITheme.Space16, ButtonWidth, HubStyle.TouchMin, new Vector2(1f, 0f));
            _boards.OnClick(OnBoards);

            _boardsReason = EmberforgeUI.Label("BoardsReason", Root, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(_boardsReason.rectTransform, -(UITheme.ScreenPadding + ButtonWidth + UITheme.Space16), UITheme.Space16,
                          900f, HubStyle.TouchMin, new Vector2(1f, 0f));
        }

        // ================================================================== lifecycle

        public override void OnShow()
        {
            App.RefreshHud();
            _signature = "";
            _tick = 0f;
            Unbind();
            _controller = new GuildDungeonLadderController(GuildDungeonBackends.For(App), App.Cloud, this, App.Session);
            _controller.Changed += OnChanged;
            Render();
            _controller.Refresh();
            Reload();
        }

        public override void OnHide()
        {
            Unbind();
            // A coroutine dies with its screen's GameObject; forget that one was running, or the
            // next visit sees _loading still true and never loads. GuildScreen learnt this first.
            _loading = false;
        }

        private void Unbind()
        {
            if (_controller == null) return;
            _controller.Changed -= OnChanged;
            _controller = null;
        }

        private void OnChanged()
        {
            Render();
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            Spin(_centerSpinner);
            Spin(_headerSpinner);
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            Render();       // once a second, for the cycle countdown; a view that is unchanged draws nothing
        }

        /// <summary>The state the other three guild dungeon screens read (the line-up, the boards, the result).</summary>
        public void Reload()
        {
            if (_loading) return;
            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            yield return State.Load(GuildDungeonBackends.For(App));
            _loading = false;
        }

        /// <summary>
        /// CAPTURE ONLY: runs a step on the visit's presenter (an offline state, a failed refresh, a refused attempt
        /// that no editor backend can produce by itself) and draws it. The presenter is the one the controller drives,
        /// so the screen shows exactly what that state would show in play.
        /// </summary>
        public void ApplyForCapture(System.Action<GuildDungeonLadderPresenter> step)
        {
            if (_controller == null || step == null) return;
            step(_controller.Presenter);
            Render();
        }

        // ================================================================== render

        private void Render()
        {
            if (_controller == null) return;
            GuildDungeonLadderView v = _controller.View;
            string signature = Sig(v);
            if (signature == _signature) return;
            _signature = signature;

            _headerSubtitle.text = v.GuildName.ToUpperInvariant();
            bool ready = v.Phase == ScreenPhase.Ready;
            bool plain = !ready;

            // REFRESH: the view's label, state and visibility.
            _refresh.Root.gameObject.SetActive(v.Refresh.Visible);
            _refresh.SetText(v.Refresh.Label);
            _refresh.Interactable = v.Refresh.Enabled;
            _headerSpinner.gameObject.SetActive(ready && v.ShowSpinner);
            _centerSpinner.gameObject.SetActive(plain && v.ShowSpinner);

            _tech.Root.gameObject.SetActive(ready);
            _boards.Root.gameObject.SetActive(ready && v.Boards.Visible);
            _boardsReason.gameObject.SetActive(ready && v.Boards.Visible && !v.Boards.Enabled);
            if (ready && v.Boards.Visible)
            {
                _boards.SetText(v.Boards.Label);
                _boards.Interactable = v.Boards.Enabled;
                _boardsReason.text = v.Boards.Enabled ? "" : v.Boards.Reason;
            }

            _info.gameObject.SetActive(ready);
            _scroll.gameObject.SetActive(ready);
            _status.gameObject.SetActive(plain && !v.ShowSpinner);
            if (plain) DrawStatus(v);
            if (ready) DrawLadder(v);
        }

        /// <summary>Offline, Error, Unavailable and Loading: the sentence centred, and the button the view carries.</summary>
        private void DrawStatus(GuildDungeonLadderView v)
        {
            Clear(_status);
            if (v.ShowSpinner) return;     // Loading with nothing cached is a spinner and nothing else
            Text text = EmberforgeUI.Body("Text", _status, v.Status, UITheme.FontBody, Emberforge.TextQuiet, TextAnchor.LowerCenter);
            UIBuilder.Box(text.rectTransform, 0f, 130f, 1000f, 150f, new Vector2(0.5f, 0.5f));

            if (v.FindGuild.Visible)
            {
                UIButton find = EmberforgeUI.Button("FindGuild", _status, v.FindGuild.Label, UITheme.FontBody, ForgeButtonKind.Primary);
                UIBuilder.Box(find.Root, 0f, -40f, 420f, HubStyle.TouchMin, new Vector2(0.5f, 0.5f));
                find.Interactable = v.FindGuild.Enabled;
                find.OnClick(OnFindGuild);
            }
            else if (v.Retry.Visible)
            {
                UIButton retry = EmberforgeUI.Button("Retry", _status, v.Retry.Label, UITheme.FontBody, ForgeButtonKind.Secondary);
                UIBuilder.Box(retry.Root, 0f, -40f, 360f, HubStyle.TouchMin, new Vector2(0.5f, 0.5f));
                retry.Interactable = v.Retry.Enabled;
                retry.OnClick(OnRefresh);
            }
        }

        private void DrawLadder(GuildDungeonLadderView v)
        {
            Clear(_info);
            float savedY = _content.anchoredPosition.y;
            Clear(_content);

            // Line A: the ladder, and the attempts counter with its bar. Line B: the next-cycle line and the countdown.
            float y = 0f;
            Text ladder = EmberforgeUI.Label("LadderLine", _info, v.LadderLine, UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(ladder.rectTransform, 0f, y, 1200f, InfoLineA, new Vector2(0f, 1f));
            Text attempts = EmberforgeUI.Label("AttemptsLine", _info, v.AttemptsLine, UITheme.FontSmall, HubStyle.ValueGold, TextAnchor.UpperRight);
            UIBuilder.Box(attempts.rectTransform, 0f, y, RightColumn, 28f, new Vector2(1f, 1f));
            Image fill = EmberforgeUI.ProgressBar("AttemptsBar", _info);
            UIBuilder.Box((RectTransform)fill.rectTransform.parent, 0f, y - 30f, RightColumn, 6f, new Vector2(1f, 1f));
            EmberforgeUI.SetProgress(fill, v.CycleAttemptCap > 0 ? (float)v.AttemptsLeftThisCycle / v.CycleAttemptCap : 0f);
            y -= InfoLineA;

            if (v.NextCycleLine.Length > 0 || v.CycleLine.Length > 0)
            {
                if (v.NextCycleLine.Length > 0)
                {
                    Text next = EmberforgeUI.Label("NextCycleLine", _info, v.NextCycleLine, UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
                    UIBuilder.Box(next.rectTransform, 0f, y, 1200f, InfoLineB, new Vector2(0f, 1f));
                }
                if (v.CycleLine.Length > 0)
                {
                    Text cycle = EmberforgeUI.Label("CycleLine", _info, v.CycleLine, UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperRight);
                    UIBuilder.Box(cycle.rectTransform, 0f, y, RightColumn, InfoLineB, new Vector2(1f, 1f));
                }
                y -= InfoLineB;
            }

            y -= UITheme.Space8;
            DrawPips(v, y);
            y -= PipHeight + UITheme.Space12;

            if (v.Banner.Length > 0)
            {
                RectTransform banner = UIBuilder.Node("Banner", _info);
                UIBuilder.Box(banner, 0f, y, 100f, BannerHeight, new Vector2(0f, 1f));
                banner.anchorMin = new Vector2(0f, 1f);
                banner.anchorMax = new Vector2(1f, 1f);
                banner.offsetMin = new Vector2(0f, y - BannerHeight);
                banner.offsetMax = new Vector2(0f, y);
                EmberforgeUI.SunkenOn(banner);
                Text text = EmberforgeUI.Label("Text", banner, v.Banner, UITheme.FontTiny, UITheme.Warning, TextAnchor.MiddleLeft);
                UIBuilder.FillParent(text.rectTransform, UITheme.Space16, 0f, UITheme.Space16, 0f);
                y -= BannerHeight + UITheme.Space8;
            }

            // The list takes what is left under the info block.
            float infoHeight = -y;
            RectTransform scrollRect = _scroll.GetComponent<RectTransform>();
            float available = Root.rect.height - BodyTop - infoHeight - BottomBarHeight;
            float contentHeight = ListPadTop + ListPadBottom;
            for (int i = 0; i < v.Rows.Count; i++) contentHeight += LadderLayout.RowHeight + RowSpacing + (v.Rows[i].DividerBefore ? DividerHeight + RowSpacing : 0f);
            // At rest the list ends in the gap under a row, so no row or divider is the half cut last thing in view.
            float snapped = available > 0f ? LadderLayout.SnappedViewport(available, contentHeight, LadderLayout.RowHeight, RowSpacing, ListPadTop) : 0f;
            float slack = available > 0f ? available - snapped : 0f;
            UIBuilder.FillParent(scrollRect, UITheme.ScreenPadding, BodyTop + infoHeight, UITheme.ScreenPadding, BottomBarHeight + slack);

            for (int i = 0; i < v.Rows.Count; i++)
            {
                GuildFloorRowView row = v.Rows[i];
                if (row.DividerBefore) AddDivider(row.DividerCaption);
                AddRow(row);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, savedY);
        }

        /// <summary>PipCount pips, PipsCleared filled in Waymark, a gap after every fifth. LadderLayout shrinks the spacing at ten, never the pips.</summary>
        private void DrawPips(GuildDungeonLadderView v, float y)
        {
            float spacing = LadderLayout.PipSpacing(v.PipCount, PipsAvailable, LadderLayout.PipSize, LadderLayout.PipBlockGap);
            for (int i = 0; i < v.PipCount; i++)
            {
                bool lit = i < v.PipsCleared;
                Image pip = lit ? UIBuilder.Panel("Pip" + (i + 1), _info, UITheme.Waymark, 9)
                                : EmberforgeUI.Sunken("Pip" + (i + 1), _info);
                UIBuilder.Box(pip.rectTransform, LadderLayout.PipX(i, LadderLayout.PipSize, spacing, LadderLayout.PipBlockGap), y,
                              LadderLayout.PipSize, PipHeight, new Vector2(0f, 1f));
                pip.raycastTarget = false;
            }
        }

        /// <summary>The thin divider over the first floor of the second block, with its caption centred on it.</summary>
        private void AddDivider(string caption)
        {
            RectTransform node = UIBuilder.Node("Divider", _content);
            UIBuilder.Sized(node, 0f, DividerHeight);
            Image line = UIBuilder.Divider("Line", node, UITheme.ChromeBrass);
            line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.rectTransform.anchoredPosition = Vector2.zero;
            line.raycastTarget = false;

            Text text = EmberforgeUI.Label("Caption", node, caption, UITheme.FontTiny, UITheme.ChromeBrass, TextAnchor.MiddleCenter);
            float width = text.preferredWidth + UITheme.Space24 * 2f;
            RectTransform plate = UIBuilder.Node("Plate", node);
            UIBuilder.Box(plate, 0f, 0f, width, DividerHeight - 8f, new Vector2(0.5f, 0.5f));
            Image back = plate.gameObject.AddComponent<Image>();
            back.color = Emberforge.PanelLow;
            back.raycastTarget = false;
            text.transform.SetParent(plate, false);
            UIBuilder.FillParent(text.rectTransform, 0f);
        }

        /// <summary>One floor, 96 tall. Every string, chip, tick and button on it is a field of the row.</summary>
        private void AddRow(GuildFloorRowView r)
        {
            RectTransform row = UIBuilder.Node("Floor" + r.Floor, _content);
            UIBuilder.Sized(row, 0f, LadderLayout.RowHeight);
            // The Champion floor (the last of a block) keeps a framed plate; a locked floor is a recess; the rest are plain.
            if (r.Champion) EmberforgeUI.PanelOn(row, false);
            else if (r.State == GuildFloorState.Locked) EmberforgeUI.SunkenOn(row);
            else EmberforgeUI.PlainPanelOn(row);

            Color titleColor = r.State == GuildFloorState.Locked ? Emberforge.TextQuiet : Emberforge.TextBody;
            Text title = EmberforgeUI.Title("Title", row, r.Title, UITheme.FontBody, titleColor, TextAnchor.MiddleLeft);
            UIBuilder.Box(title.rectTransform, 28f, -10f, 190f, 40f, new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            if (r.ClearedByMe)
            {
                Image tick = UIBuilder.Img("Mine", row, TextureLab.Check(64), UITheme.Success);
                UIBuilder.Box(tick.rectTransform, 224f, -14f, 32f, 32f, new Vector2(0f, 1f));
                tick.raycastTarget = false;
            }

            Color chipTint = r.State == GuildFloorState.Cleared ? UITheme.Success
                           : r.State == GuildFloorState.Open ? UITheme.Gold : UITheme.TextDim;
            RectTransform chip = EmberforgeUI.Chip("State", row, r.StateText, chipTint, UITheme.FontTiny - 2);
            UIBuilder.Box(chip, 272f, -12f, 150f, 36f, new Vector2(0f, 1f));

            if (r.FirstClearText.Length > 0)
            {
                Text first = EmberforgeUI.Label("FirstClear", row, r.FirstClearText, UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleLeft);
                UIBuilder.Box(first.rectTransform, 446f, -14f, 500f, 32f, new Vector2(0f, 1f));
            }

            Text attemptsText = EmberforgeUI.Label("Attempts", row, r.AttemptsText, UITheme.FontSmall, Emberforge.TextBody, TextAnchor.MiddleLeft);
            UIBuilder.Box(attemptsText.rectTransform, 960f, -12f, 200f, 36f, new Vector2(0f, 1f));

            Text reward = EmberforgeUI.Label("Reward", row, r.RewardText, UITheme.FontTiny,
                                             r.RewardMuted ? Emberforge.TextQuiet : HubStyle.ValueGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(reward.rectTransform, 1170f, -14f, 320f, 32f, new Vector2(0f, 1f));

            // The Reason, verbatim, in the row's lower line, left of the button.
            if (r.Attempt.Visible && !r.Attempt.Enabled && r.Attempt.Reason.Length > 0)
            {
                Text reason = EmberforgeUI.Label("Reason", row, r.Attempt.Reason, UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleLeft);
                UIBuilder.Box(reason.rectTransform, 28f, 10f, 1380f, 32f, new Vector2(0f, 0f));
            }

            if (r.Attempt.Visible)
            {
                // A full touch minimum tall, so it overhangs the 96 tall row equally top and bottom (LadderLayout).
                float tap = LadderLayout.TapSize(LadderLayout.RowHeight, HubStyle.TouchMin);
                UIButton attempt = EmberforgeUI.Button("Attempt", row, r.Attempt.Label, UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(attempt.Root, -24f, 0f, ButtonWidth, tap, new Vector2(1f, 0.5f));
                attempt.Interactable = r.Attempt.Enabled;
                int floor = r.Floor;
                attempt.OnClick(delegate { OpenFloor(floor); });
            }
        }

        private static void Clear(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                Transform child = host.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        /// <summary>Every field the screen draws, as one string: equal strings are the same picture, so a tick that
        /// changes nothing rebuilds nothing (and never rebuilds a button under a thumb).</summary>
        private static string Sig(GuildDungeonLadderView v)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(v.Phase).Append('|').Append(v.Title).Append('|').Append(v.GuildName).Append('|').Append(v.LadderLine)
              .Append('|').Append(v.NextCycleLine).Append('|').Append(v.AttemptsLine).Append('|').Append(v.CycleLine)
              .Append('|').Append(v.PipCount).Append(',').Append(v.PipsCleared).Append('|').Append(v.CycleAttemptCap).Append(',').Append(v.AttemptsLeftThisCycle)
              .Append('|').Append(v.Status).Append('|').Append(v.Banner).Append('|').Append(v.ShowSpinner);
            Button(sb, v.Boards); Button(sb, v.Refresh); Button(sb, v.Retry); Button(sb, v.FindGuild);
            for (int i = 0; i < v.Rows.Count; i++)
            {
                GuildFloorRowView r = v.Rows[i];
                sb.Append('#').Append(r.Floor).Append(r.Title).Append(r.Champion).Append(r.DividerBefore).Append(r.DividerCaption)
                  .Append(r.StateText).Append(r.ClearedByMe).Append(r.FirstClearText).Append(r.AttemptsText)
                  .Append(r.RewardText).Append(r.RewardMuted);
                Button(sb, r.Attempt);
            }
            return sb.ToString();
        }

        private static void Button(StringBuilder sb, ButtonModel b)
        {
            sb.Append('[').Append(b.Visible).Append(b.Enabled).Append(b.Label).Append('/').Append(b.Reason).Append(']');
        }

        // ================================================================== spinner

        /// <summary>Eight dots in a ring, brightest at one end; Spin turns the ring. The only motion on the screen.</summary>
        private static RectTransform MakeSpinner(string name, Transform parent, float size)
        {
            RectTransform ring = UIBuilder.Node(name, parent);
            UIBuilder.Size(ring, size, size);
            const int dots = 8;
            for (int i = 0; i < dots; i++)
            {
                float angle = i * (360f / dots) * Mathf.Deg2Rad;
                float alpha = 0.15f + 0.85f * (i / (float)(dots - 1));
                Image dot = UIBuilder.Img("Dot" + i, ring, TextureLab.Circle(32), UITheme.WithAlpha(UITheme.ChromeBrass, alpha));
                float dotSize = size * 0.17f;
                UIBuilder.Box(dot.rectTransform, Mathf.Cos(angle) * size * 0.36f, Mathf.Sin(angle) * size * 0.36f, dotSize, dotSize, new Vector2(0.5f, 0.5f));
                dot.raycastTarget = false;
            }
            ring.gameObject.SetActive(false);
            return ring;
        }

        private static void Spin(RectTransform ring)
        {
            if (ring == null || !ring.gameObject.activeSelf) return;
            ring.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 360f);
        }

        // ================================================================== taps

        private void OnRefresh()
        {
            App.Audio.Tap();
            if (_controller != null) _controller.Refresh();
            Reload();
        }

        private void OnFindGuild()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Guild);
        }

        /// <summary>Opens a floor's line-up screen. Not from a replay, so the handover's memory of
        /// something watched is dropped: the floor view should not claim you just watched a fight
        /// you walked away from two screens ago.</summary>
        public void OpenFloor(int floor)
        {
            App.Audio.Tap();
            State.Floor = floor;
            State.Watched = null;
            Router.GuildFloor.ResetOnNextShow();
            Router.Show(ScreenId.GuildFloor);
        }

        private void OnBoards()
        {
            App.Audio.Tap();
            Router.GuildDungeonBoards.SetFloor(Mathf.Max(1, GuildDungeon.HighestCleared(State.Attempts, null, State.Cycle)));
            Router.Show(ScreenId.GuildDungeonBoards);
        }
    }
}
