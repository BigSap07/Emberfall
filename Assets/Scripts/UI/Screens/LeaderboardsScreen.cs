using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Screens;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE LEADERBOARDS (Docs/ui-specs/R28-SCREENS.md, "Leaderboards"): four boards, four tabs, the caller's own row
    /// highlighted, and their rank pinned under the list when it falls below the top.
    ///
    /// IT DECIDES NOTHING. Every word, every tab, every row, whether REFRESH is drawn and whether it can be tapped
    /// is a field of the LeaderboardsView the controller hands over; the screen lays that view out and rebuilds
    /// from it on every Changed and once a second while a countdown is on it. A tap calls one controller method
    /// (Open, Refresh, SelectTab) and nothing else: no backend, no save, no copy of its own. The harness reads
    /// this file to hold it to that.
    ///
    /// NO MEDALS. Boards pay nothing, so ranks 1 to 3 are drawn like every other rank; a medal would read as a prize.
    /// A row is not a button either: the server sends no user id, so no profile opens from a board.
    ///
    /// WHERE THINGS SIT is LeaderboardsLayout (Core), so the harness can ask the same question the screen does.
    /// </summary>
    public sealed class LeaderboardsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Leaderboards; } }

        private Text _headerSubtitle;
        private UIButton _refresh;
        private Text _refreshReason;
        private RectTransform _body;

        private LeaderboardsController _controller;
        private string _key;
        private LeaderboardsView _view;
        private float _tick;

        // The three lines that tick without a rebuild, so the list keeps its scroll position.
        private Text _resetText;
        private Text _updatedText;
        private Text _bannerText;

        private bool _hasPending;
        private LeaderboardBoard _pending;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            RectTransform header = ScreenChrome.ForgedHeader(Root, new LeaderboardsView().Title, Router, out _headerSubtitle);

            _refresh = EmberforgeUI.Button("Refresh", header, "", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_refresh.Root, 0f, 0f, LeaderboardsLayout.RefreshWidth, LeaderboardsLayout.TabHeight, new Vector2(1f, 0.5f));
            _refresh.OnClick(delegate
            {
                App.Audio.Tap();
                if (_controller != null) _controller.Refresh();
            });

            _refreshReason = EmberforgeUI.Label("RefreshReason", Root, "", UITheme.FontTiny, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIBuilder.Box(_refreshReason.rectTransform, -UITheme.ScreenPadding, -(ScreenChrome.HeaderTop + 108f), 560f, 26f, new Vector2(1f, 1f));

            _body = UIBuilder.Node("Body", Root);
            UIBuilder.FillParent(_body, 0f);
        }

        // ------------------------------------------------------------------ doors

        /// <summary>
        /// The doors on the Recital and the Proving open the screen with that board selected: SelectTab before Open,
        /// both in OnShow. The Profile's door opens it on the default tab and passes nothing.
        /// </summary>
        public void OpenBoard(LeaderboardBoard board)
        {
            _pending = board;
            _hasPending = true;
            Router.Show(ScreenId.Leaderboards);
        }

        // ------------------------------------------------------------------ life of the screen

        public override void OnShow()
        {
            App.RefreshHud();
            Disconnect();
            _controller = new LeaderboardsController(LeaderboardBackends.For(App), App, App.Session);
            _controller.Changed += OnChanged;
            _key = null;
            _tick = 0f;
            if (_hasPending && _pending != _controller.Presenter.Selected) _controller.SelectTab(_pending);
            _hasPending = false;
            _controller.Open();
            Refresh();
        }

        public override void OnHide()
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (_controller != null) _controller.Changed -= OnChanged;
            _controller = null;
        }

        private void OnChanged()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            Refresh();
        }

        private void Update()
        {
            if (_controller == null || Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            // Only a view with something that counts needs the refresh; the rest changes through Changed.
            if (_view != null && (_view.ResetInSeconds >= 0 || _view.UpdatedLine.Length > 0 || _view.Banner.Length > 0)) Refresh();
        }

        /// <summary>For the capture tool: the same tap REFRESH makes.</summary>
        public void RefreshForCapture()
        {
            if (_controller != null) _controller.Refresh();
        }

        private void Refresh()
        {
            if (_controller == null) return;
            _view = _controller.View;
            string key = LeaderboardsLayout.StructureKey(_view);
            if (key != _key)
            {
                _key = key;
                Rebuild(_view);
                return;
            }
            if (_resetText != null) _resetText.text = _view.ResetLine;
            if (_updatedText != null) _updatedText.text = _view.UpdatedLine;
            if (_bannerText != null) _bannerText.text = _view.Banner;
        }

        // ------------------------------------------------------------------ drawing

        private void Rebuild(LeaderboardsView v)
        {
            for (int i = _body.childCount - 1; i >= 0; i--)
            {
                GameObject old = _body.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            _resetText = null;
            _updatedText = null;
            _bannerText = null;

            _refresh.Root.gameObject.SetActive(v.Refresh.Visible);
            _refresh.SetText(v.Refresh.Label);
            _refresh.Interactable = v.Refresh.Enabled;
            _refreshReason.text = v.Refresh.Visible && !v.Refresh.Enabled ? v.Refresh.Reason : "";

            float canvasHeight = Root.rect.height > 200f ? Root.rect.height : UITheme.ReferenceResolution.y;
            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space8;
            bool status = v.Phase != ScreenPhase.Ready && v.Phase != ScreenPhase.Loading;
            bool pinned = v.Phase == ScreenPhase.Ready && v.PinnedSelf != null;
            LeaderboardsPlan plan = LeaderboardsLayout.Plan(top, canvasHeight, v.Banner.Length > 0, v.LockLine.Length > 0, pinned);

            BuildTabs(v, plan);
            BuildCaption(v, plan);
            if (v.Banner.Length > 0) BuildBanner(v, plan);
            if (v.LockLine.Length > 0) BuildLock(v, plan);

            if (status)
            {
                BuildStatus(v, plan, canvasHeight);
                return;
            }
            if (v.Phase == ScreenPhase.Loading)
            {
                BuildSpinner(plan, canvasHeight, true);
                return;
            }

            BuildColumnHeader(v, plan);
            BuildList(v, plan);
            BuildBottom(v, plan);
            if (v.ShowSpinner) BuildSpinner(plan, canvasHeight, false);
        }

        private void BuildTabs(LeaderboardsView v, LeaderboardsPlan plan)
        {
            float x = UITheme.ScreenPadding;
            for (int i = 0; i < v.Tabs.Count; i++)
            {
                LeaderboardBoard board = v.Tabs[i].Board;
                ForgeTab tab = EmberforgeUI.Tab(v.Tabs[i].Label, _body, LeaderboardsLayout.TabWidth, LeaderboardsLayout.TabHeight, delegate
                {
                    App.Audio.Tap();
                    if (_controller != null) _controller.SelectTab(board);
                });
                UIBuilder.Box(tab.Root, x, -plan.TabsTop, LeaderboardsLayout.TabWidth, LeaderboardsLayout.TabHeight, new Vector2(0f, 1f));
                EmberforgeUI.SetTabSelected(tab, v.Tabs[i].Selected);
                x += LeaderboardsLayout.TabWidth + LeaderboardsLayout.TabGap;
            }

            _resetText = EmberforgeUI.Label("Reset", _body, v.ResetLine, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIBuilder.Box(_resetText.rectTransform, -UITheme.ScreenPadding, -(plan.TabsTop + LeaderboardsLayout.TabHeight * 0.5f - 15f), 520f, 30f, new Vector2(1f, 1f));
        }

        private void BuildCaption(LeaderboardsView v, LeaderboardsPlan plan)
        {
            Text caption = EmberforgeUI.Label("Caption", _body, v.Caption, UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleLeft);
            UIBuilder.Box(caption.rectTransform, UITheme.ScreenPadding, -plan.CaptionTop, 1100f, LeaderboardsLayout.CaptionHeight, new Vector2(0f, 1f));

            _updatedText = EmberforgeUI.Label("Updated", _body, v.UpdatedLine, UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleRight);
            UIBuilder.Box(_updatedText.rectTransform, -UITheme.ScreenPadding, -plan.CaptionTop, 520f, LeaderboardsLayout.CaptionHeight, new Vector2(1f, 1f));
        }

        private void BuildBanner(LeaderboardsView v, LeaderboardsPlan plan)
        {
            RectTransform strip = UIBuilder.Node("Banner", _body);
            UIBuilder.TopStrip(strip, LeaderboardsLayout.BannerHeight, plan.BannerTop, UITheme.ScreenPadding);
            Image plate = strip.gameObject.AddComponent<Image>();
            plate.sprite = UISkin.Panel((int)UITheme.CornerRadiusSmall);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(UITheme.PanelSunken, HubStyle.PlateAlpha);
            plate.raycastTarget = false;
            UIBuilder.Frame("Edge", strip, UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlpha),
                            (int)UITheme.CornerRadiusSmall, HubStyle.BorderWeight);

            _bannerText = EmberforgeUI.Label("Text", strip, v.Banner, UITheme.FontTiny, UITheme.Warning, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_bannerText.rectTransform, UITheme.Space16, 0f, UITheme.Space16, 0f);
        }

        private void BuildLock(LeaderboardsView v, LeaderboardsPlan plan)
        {
            Text line = EmberforgeUI.Label("Lock", _body, v.LockLine, UITheme.FontTiny, UITheme.ChromeBrass, TextAnchor.MiddleLeft);
            UIBuilder.Box(line.rectTransform, UITheme.ScreenPadding, -plan.LockTop, 900f, LeaderboardsLayout.LockHeight, new Vector2(0f, 1f));
        }

        private void BuildColumnHeader(LeaderboardsView v, LeaderboardsPlan plan)
        {
            Text header = EmberforgeUI.Label("ValueHeader", _body, v.ValueHeader, UITheme.FontTiny, UITheme.ChromeBrass, TextAnchor.MiddleRight);
            UIBuilder.Box(header.rectTransform, -(UITheme.ScreenPadding + ValueInset), -plan.ColumnHeaderTop, LeaderboardsLayout.ValueColumn,
                          LeaderboardsLayout.ColumnHeaderHeight, new Vector2(1f, 1f));
        }

        /// <summary>How far in from the list's right edge a row's value ends: the row's own padding.</summary>
        private const float ValueInset = 24f;

        private void BuildList(LeaderboardsView v, LeaderboardsPlan plan)
        {
            ScrollRect scroll;
            RectTransform content = UIBuilder.ScrollList("List", _body, out scroll, LeaderboardsLayout.RowGap, new RectOffset(0, 0, 0, 8));
            RectTransform rect = scroll.GetComponent<RectTransform>();
            UIBuilder.FillParent(rect, UITheme.ScreenPadding, plan.ListTop, UITheme.ScreenPadding, plan.ListBottomInset);

            for (int i = 0; i < v.Rows.Count; i++)
            {
                RectTransform row = UIBuilder.Node("Row" + i, content);
                UIBuilder.Sized(row, 0f, LeaderboardsLayout.RowHeight);
                BuildRow(row, v.Rows[i]);
            }
        }

        private void BuildBottom(LeaderboardsView v, LeaderboardsPlan plan)
        {
            if (v.PinnedSelf != null)
            {
                Image divider = UIBuilder.Divider("Divider", _body, UITheme.ChromeBrass);
                UIBuilder.BottomStrip(divider.rectTransform, LeaderboardsLayout.DividerHeight,
                                      plan.PinnedInset + LeaderboardsLayout.RowHeight + LeaderboardsLayout.Gap, UITheme.ScreenPadding);

                RectTransform pinned = UIBuilder.Node("Pinned", _body);
                UIBuilder.BottomStrip(pinned, LeaderboardsLayout.RowHeight, plan.PinnedInset, UITheme.ScreenPadding);
                BuildRow(pinned, v.PinnedSelf);
            }

            RectTransform line = UIBuilder.Node("RankLine", _body);
            UIBuilder.BottomStrip(line, LeaderboardsLayout.RankLineHeight, plan.RankLineInset, UITheme.ScreenPadding);

            Text mine = EmberforgeUI.Label("MyRank", line, v.MyRankLine, UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleLeft);
            UIBuilder.Box(mine.rectTransform, UITheme.Space8, 0f, 640f, LeaderboardsLayout.RankLineHeight, new Vector2(0f, 0.5f));

            Text submit = EmberforgeUI.Label("Submit", line, v.SubmitLine, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIBuilder.Box(submit.rectTransform, -UITheme.Space8, 0f, 1100f, LeaderboardsLayout.RankLineHeight, new Vector2(1f, 0.5f));
        }

        /// <summary>One row, ordinary or pinned. The caller's own is the same plate with a gold frame and a gold name.</summary>
        private static void BuildRow(RectTransform row, LeaderboardRowView r)
        {
            // The spec's row: UITheme.PanelRaised, CornerRadiusSmall. The kit's plain panel carries a gold rim of its own,
            // which put a gold frame round EVERY row and left the caller's row told apart by its name colour alone; so
            // an ordinary row is a flat raised plate with a soft hairline, and only the caller's has the gold frame.
            Image plate = row.gameObject.AddComponent<Image>();
            plate.sprite = UISkin.Panel((int)UITheme.CornerRadiusSmall);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.PanelRaised;
            plate.raycastTarget = false;
            if (r.IsMe)
                UIBuilder.Frame("Mine", row, UITheme.Gold, (int)UITheme.CornerRadiusSmall, HubStyle.BorderWeightHeavy);
            else
                UIBuilder.Frame("Edge", row, UITheme.BorderSoft, (int)UITheme.CornerRadiusSmall, HubStyle.BorderWeight);

            Text rank = EmberforgeUI.Label("Rank", row, r.RankText, UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIBuilder.Box(rank.rectTransform, ValueInset, 0f, LeaderboardsLayout.RankColumn, LeaderboardsLayout.RowHeight, new Vector2(0f, 0.5f));

            Text name = EmberforgeUI.Label("Name", row, r.Name, UITheme.FontSmall, r.IsMe ? UITheme.Gold : UITheme.TextPrimary, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(name.rectTransform, ValueInset + LeaderboardsLayout.RankColumn, 0f, ValueInset + LeaderboardsLayout.ValueColumn, 0f);
            UIBuilder.BestFit(name, UITheme.FontTiny - 4, UITheme.FontSmall);

            Text value = EmberforgeUI.Label("Value", row, r.ValueText, HubStyle.FontRankValue, HubStyle.ValueGold, TextAnchor.MiddleRight);
            UIBuilder.Box(value.rectTransform, -ValueInset, 0f, LeaderboardsLayout.ValueColumn, LeaderboardsLayout.RowHeight, new Vector2(1f, 0.5f));
        }

        /// <summary>The status block: the view's Status centred in the body, and under it the view's Retry when it is visible.</summary>
        private void BuildStatus(LeaderboardsView v, LeaderboardsPlan plan, float canvasHeight)
        {
            const float statusHeight = 120f;
            float blockHeight = statusHeight + (v.Retry.Visible ? UITheme.Space24 + LeaderboardsLayout.TabHeight : 0f);
            float areaTop = plan.ColumnHeaderTop;
            float areaHeight = canvasHeight - areaTop - LeaderboardsLayout.BottomMargin;
            float y = areaTop + Mathf.Max(0f, (areaHeight - blockHeight) * 0.5f);

            Text text = EmberforgeUI.Body("Status", _body, v.Status, UITheme.FontBody, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(text.rectTransform, 0f, -y, 1200f, statusHeight, new Vector2(0.5f, 1f));

            if (!v.Retry.Visible) return;
            UIButton retry = EmberforgeUI.Button("Retry", _body, v.Retry.Label, HubStyle.FontButton, ForgeButtonKind.Secondary);
            UIBuilder.Box(retry.Root, 0f, -(y + statusHeight + UITheme.Space24), LeaderboardsLayout.RetryWidth, LeaderboardsLayout.TabHeight, new Vector2(0.5f, 1f));
            retry.Interactable = v.Retry.Enabled;
            retry.OnClick(delegate
            {
                App.Audio.Tap();
                if (_controller != null) _controller.Refresh();
            });
            if (!v.Retry.Enabled && v.Retry.Reason.Length > 0)
            {
                Text why = EmberforgeUI.Label("RetryReason", _body, v.Retry.Reason, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperCenter);
                UIBuilder.Box(why.rectTransform, 0f, -(y + statusHeight + UITheme.Space24 + LeaderboardsLayout.TabHeight + UITheme.Space4), 900f, 26f, new Vector2(0.5f, 1f));
            }
        }

        /// <summary>The spinner: alone in the body while nothing is cached, over the list while rows are still shown.</summary>
        private void BuildSpinner(LeaderboardsPlan plan, float canvasHeight, bool alone)
        {
            const float size = 120f;
            float areaTop = alone ? plan.ColumnHeaderTop : plan.ListTop;
            float areaHeight = canvasHeight - areaTop - (alone ? LeaderboardsLayout.BottomMargin : plan.ListBottomInset);
            float y = areaTop + Mathf.Max(0f, (areaHeight - size) * 0.5f);

            RectTransform spin = UIBuilder.Node("Spinner", _body);
            UIBuilder.Box(spin, 0f, -y, size, size, new Vector2(0.5f, 1f));
            if (!alone)
            {
                Image plate = spin.gameObject.AddComponent<Image>();
                plate.sprite = UISkin.Panel((int)UITheme.CornerRadius);
                plate.type = Image.Type.Sliced;
                plate.color = UITheme.WithAlpha(UITheme.PanelSunken, HubStyle.PlateAlpha);
                plate.raycastTarget = false;
            }
            spin.gameObject.AddComponent<ForgeSpinner>();
        }
    }

    /// <summary>
    /// Ten dots round a circle, the lit one walking round. No art, no sprite sheet and no rotation of one image
    /// (a full ring turning looks like nothing): the dots are TextureLab circles and the walk is their alpha.
    /// </summary>
    internal sealed class ForgeSpinner : MonoBehaviour
    {
        private const int Dots = 10;
        private const float Speed = 9f;

        private readonly List<Image> _dots = new List<Image>();

        private void Awake()
        {
            RectTransform host = GetComponent<RectTransform>();
            float radius = Mathf.Min(host.sizeDelta.x, host.sizeDelta.y) * 0.32f;
            float dot = radius * 0.34f;
            for (int i = 0; i < Dots; i++)
            {
                float a = (float)i / Dots * Mathf.PI * 2f;
                Image d = UIBuilder.Img("Dot" + i, host, TextureLab.Circle(32), UITheme.Gold);
                UIBuilder.Box(d.rectTransform, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, dot, dot, new Vector2(0.5f, 0.5f));
                d.raycastTarget = false;
                _dots.Add(d);
            }
        }

        private void Update()
        {
            float head = Time.unscaledTime * Speed;
            for (int i = 0; i < _dots.Count; i++)
            {
                float behind = Mathf.Repeat(head - i, Dots) / Dots;
                Color c = _dots[i].color;
                c.a = Mathf.Lerp(1f, 0.12f, behind);
                _dots[i].color = c;
            }
        }
    }
}
