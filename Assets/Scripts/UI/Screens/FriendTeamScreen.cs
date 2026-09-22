using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// A friend's five heroes, as they were the last time that friend's device published them, and
    /// the one thing you can do about it.
    ///
    /// "AS LAST SYNCED" IS SAID OUT LOUD ON THE SCREEN. A snapshot is not a live read of somebody
    /// else's save - it is a record their game uploaded when their line last changed, and it can
    /// be a week old. Every other number here (levels, gear, power) is exact and frozen; implying
    /// it is current would be the same class of lie DESIGN-PVP section 7 rules out for standing,
    /// and it is easier to tell by accident because the data is real.
    ///
    /// What the player is NOT shown, and cannot be: anything about their friend's save. A
    /// snapshot carries five heroes and their gear. Gold, collection, campaign position and
    /// everything else stays where it is, and the schema enforces that rather than this screen
    /// choosing not to display it - a friendship makes `team_snapshots` readable and makes `saves`
    /// no more readable than it was.
    /// </summary>
    public sealed class FriendTeamScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.FriendTeam; } }

        private const float CardWidth = 1180f;

        private Text _headerSubtitle;
        private Text _name;
        private Text _power;
        private RectTransform _lineRow;
        private Text _freshness;
        private Text _note;
        private UIButton _duelButton;

        private string _userId = "";
        private string _name0 = "";
        private TeamSnapshot _snapshot;
        private bool _loading;

        /// <summary>Set by the friends list before this is shown. Two values, not a FriendEntry,
        /// because the row that opened this is a list item that will be destroyed on the next
        /// refresh and holding it would outlive what it describes.</summary>
        public void SetFriend(string userId, string displayName)
        {
            _userId = userId;
            _name0 = displayName;
            _snapshot = null;
        }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "THEIR LINE", Router, out _headerSubtitle);

            float top = ScreenChrome.HeaderTop + 108f + 16f;
            RectTransform card = UIBuilder.Node("Line", Root);
            UIBuilder.Box(card, 0f, -top, CardWidth, 560f, new Vector2(0.5f, 1f));

            EmberforgeUI.PanelOn(card, true);

            _name = EmberforgeUI.Title("Name", card, "", UITheme.FontTitle, Emberforge.TextGold,
                                       TextAnchor.UpperLeft);
            UIBuilder.Box(_name.rectTransform, 34f, -26f, 700f, 56f, new Vector2(0f, 1f));

            _power = EmberforgeUI.Label("Power", card, "", UITheme.FontSmall, Emberforge.TextGold,
                                        TextAnchor.UpperLeft);
            UIBuilder.Box(_power.rectTransform, 34f, -86f, 700f, 32f, new Vector2(0f, 1f));

            _freshness = EmberforgeUI.Label("Freshness", card, "", UITheme.FontTiny, Emberforge.TextQuiet,
                                         TextAnchor.UpperRight);
            UIBuilder.Box(_freshness.rectTransform, -34f, -32f, 440f, 30f, new Vector2(1f, 1f));

            Image divider = EmberforgeUI.Rule("Divider", card);
            UIBuilder.Box(divider.rectTransform, 0f, -134f, CardWidth - 68f, Emberforge.RuleHeight,
                          new Vector2(0.5f, 1f));

            _lineRow = UIBuilder.Node("Units", card);
            UIBuilder.Box(_lineRow, 0f, -156f, CardWidth - 68f, 230f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_lineRow, 20f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            _note = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny, Emberforge.TextQuiet,
                                           TextAnchor.UpperCenter);
            UIBuilder.Box(_note.rectTransform, 0f, -392f, CardWidth - 120f, 46f, new Vector2(0.5f, 1f));

            _duelButton = EmberforgeUI.Button("Duel", card, "DUEL", UITheme.FontHeading, ForgeButtonKind.Primary);
            UIBuilder.Box(_duelButton.Root, 0f, 26f, CardWidth - 68f, 92f, new Vector2(0.5f, 0f));
            _duelButton.OnClick(OnDuel);

            Text stakes = EmberforgeUI.Label("Stakes", Root,
                "A friendly duel. Nothing is at stake - no rewards, no standing, nothing saved.",
                UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(stakes.rectTransform, 0f, -(top + 560f + UITheme.Space16), CardWidth, 30f,
                          new Vector2(0.5f, 1f));
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            _name.text = string.IsNullOrEmpty(_name0) ? "WAYFARER" : _name0.ToUpperInvariant();
            _headerSubtitle.text = "";
            _power.text = "";
            _freshness.text = "";
            _duelButton.Interactable = false;
            ClearLine();

            if (App.Cloud == null || !App.Cloud.SignedIn)
            {
                _note.text = "You are offline, so their line cannot be loaded.";
                return;
            }
            if (string.IsNullOrEmpty(_userId))
            {
                _note.text = "No friend selected.";
                return;
            }

            _note.text = "Loading their line...";
            if (!_loading) StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            _loading = true;
            TeamSnapshot fetched = null;
            string error = "";
            yield return App.Cloud.Social.FetchSnapshot(_userId,
                delegate (TeamSnapshot snapshot, string message) { fetched = snapshot; error = message; });
            _loading = false;

            _snapshot = fetched;
            if (fetched == null || fetched.Units.Count == 0)
            {
                // Two causes look identical from the client and both are true statements, so the
                // message says both rather than guessing. RLS filters rather than refuses - which
                // is what makes it RLS and not a permission error - so an unaccepted friendship
                // and an unpublished line arrive as the same empty result.
                _note.text = string.IsNullOrEmpty(error)
                    ? "They have not published a line yet."
                    : error;
                yield break;
            }

            if (!string.IsNullOrEmpty(fetched.Label)) _name.text = fetched.Label.ToUpperInvariant();
            _power.text = "LINE POWER  " + fetched.PowerScore;
            _headerSubtitle.text = fetched.Units.Count + (fetched.Units.Count == 1 ? " HERO" : " HEROES");

            // Not a timestamp, a statement about what this IS. The row carries updated_at, but the
            // honest version of that number for a player is not "14:32" - it is that what they are
            // looking at is a record rather than a live read of somebody else's game.
            _freshness.text = "AS LAST SYNCED FROM THEIR DEVICE";

            _note.text = "This is their line as their game last published it. It does not change while "
                       + "you look at it, and fighting it does not tell them anything.";
            RenderLine(fetched);
            _duelButton.Interactable = true;
        }

        private void ClearLine()
        {
            for (int i = _lineRow.childCount - 1; i >= 0; i--) Destroy(_lineRow.GetChild(i).gameObject);
        }

        private void RenderLine(TeamSnapshot snapshot)
        {
            ClearLine();
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                SnapshotUnit unit = snapshot.Units[i];
                HeroDef def = App.Session.Content.GetHero(unit.DefId);

                RectTransform cell = UIBuilder.Node("Unit" + i, _lineRow);
                UIBuilder.Sized(cell, 180f, 220f);

                if (def == null)
                {
                    // A hero this build does not have. The simulation skips it rather than faulting
                    // the fight, so the cell says so rather than leaving a hole the player has to
                    // interpret - they are one body up, and they should know it before they press
                    // Duel rather than after.
                    Text missing = EmberforgeUI.Label("Missing", cell, "UNKNOWN\nHERO", UITheme.FontTiny,
                                                   Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                    UIBuilder.FillParent(missing.rectTransform, 0f);
                    continue;
                }

                RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", cell, def,
                                                                    unit.Stars, 130f);
                UIBuilder.Box(portrait, 0f, 0f, 130f, 130f, new Vector2(0.5f, 1f));

                Text name = EmberforgeUI.Label("Name", cell, def.Name, UITheme.FontTiny - 4,
                                            Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -136f, 180f, 26f, new Vector2(0.5f, 1f));

                // Level and gear, because the two together are the whole of what a snapshot says
                // about a hero's strength and a player judging a fight needs both.
                Text level = EmberforgeUI.Label("Level", cell, "LV " + unit.Level, UITheme.FontTiny - 2,
                                             Emberforge.TextBody, TextAnchor.UpperCenter);
                UIBuilder.Box(level.rectTransform, 0f, -164f, 180f, 26f, new Vector2(0.5f, 1f));

                string extras = unit.Armor.Count + (unit.Armor.Count == 1 ? " PIECE" : " PIECES");
                if (!string.IsNullOrEmpty(unit.RelicId)) extras += "  +RELIC";
                if (unit.SelectedTalent > 0) extras += "  +TALENT";
                Text gear = EmberforgeUI.Label("Gear", cell, extras, UITheme.FontTiny - 6,
                                            Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(gear.rectTransform, 0f, -190f, 180f, 24f, new Vector2(0.5f, 1f));
            }
        }

        // ------------------------------------------------------------------ the duel

        /// <summary>
        /// Hands the snapshot to the battle screen as a BattleLoadout and nothing else happens.
        ///
        /// This is the whole of what a friendly duel needed building. The engine is a pure
        /// function of its request, so a line rebuilt from `team_snapshots` resolves the same
        /// fight on either device - there is no server logic, no match record, no result to
        /// report, and nothing to reconcile. The harness measures that rather than assuming it: a
        /// line published to the column and read back fights the same fight, event for event, as
        /// the same line built locally.
        /// </summary>
        private void OnDuel()
        {
            if (_snapshot == null || _snapshot.Units.Count == 0) return;
            App.Audio.Tap();

            BattleLoadout line = _snapshot.ToLoadout();
            if (line.Units.Count == 0)
            {
                App.Notify("None of their heroes exist in this build.");
                return;
            }
            Router.Battle.SetDuelOpponent(line, string.IsNullOrEmpty(_name0) ? line.Label : _name0);
            Router.Show(ScreenId.Battle);
        }
    }
}
