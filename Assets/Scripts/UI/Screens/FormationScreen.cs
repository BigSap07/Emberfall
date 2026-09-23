using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The banner: five slots, the roster that fills them, and the only place either is edited.
    ///
    /// WHAT THIS REPLACES. There was no formation screen. The Hub's team entry opened the
    /// collection ("YOUR ALLIES"), which shows every hero and deploys none of them, and the only
    /// working deploy control in the game was a row of five buttons inside each individual hero's
    /// detail page, labelled with four OTHER heroes' names. Changing your line-up meant knowing
    /// that page could do it, and doing it one hero at a time from five different pages.
    ///
    /// SLOT ORDER IS NOT DECORATION, which is why this shows a row rather than a list of names.
    /// CombatSimulation resolves TargetMode.FrontEnemy by picking the living unit with the LOWEST
    /// Slot, and BuildBattleRequest hands it Profile.TeamHeroes() - so the left-to-right order of
    /// the banner is the order front-targeting abilities eat through it. Slot 1 is genuinely the
    /// most dangerous place to stand. That is also the whole reason this screen offers reordering
    /// and not just "who is in".
    ///
    /// WHAT IT DELIBERATELY DOES NOT SHOW: rows. The sim has exactly one positional concept, the
    /// linear Slot index, and no notion of a front rank versus a back rank. A 2x3 grid would have
    /// looked more like a formation screen and would have been inventing a mechanic the combat
    /// model does not have.
    ///
    /// INTERACTION is tap-to-select, tap-to-place, with the prompt line under the banner always
    /// naming the next tap. Nothing here is drag-only or long-press, because the complaint this
    /// screen answers is discoverability, and a control whose affordance is invisible is the same
    /// bug in a new place.
    /// </summary>
    public sealed class FormationScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Formation; } }

        private const int SlotCount = PlayerProfile.TeamSize;

        // One vertical rhythm for the whole screen, in logical units below the top bar. Written as
        // a run of offsets rather than per-widget literals because every one of them depends on the
        // band above it: the first draft put the power readout inside the banner's own footprint
        // and it printed straight across slot 3's numbers.
        private const float BannerTop = 118f;
        private const float BannerHeight = 262f;                       // -> 380
        private const float PowerTop = BannerTop + BannerHeight + 12f; // 392
        private const float PowerHeight = 44f;                         // -> 436
        private const float PromptTop = PowerTop + PowerHeight + 8f;   // 444
        private const float PromptHeight = 38f;                        // -> 482
        private const float RosterLabelTop = PromptTop + PromptHeight + 10f; // 492
        private const float RosterTop = RosterLabelTop + 58f;          // 550, list starts here

        private const float RowHeight = 104f;

        private Text _headerSubtitle;
        private RectTransform _bannerRow;
        private Text _prompt;
        private RectTransform _rosterList;
        private Text _powerLabel;

        /// <summary>The Guild Path's saved lines, one tab each, rebuilt on refresh because an
        /// unlock can add one. Hidden entirely for a player who owns only the one line.</summary>
        private RectTransform _lineTabs;
        private static readonly string[] LineNumerals = { "I", "II", "III", "IV", "V", "VI" };

        private readonly ForgeCard[] _slotButtons = new ForgeCard[SlotCount];
        private readonly RectTransform[] _slotBodies = new RectTransform[SlotCount];

        /// <summary>
        /// The hero waiting to be placed, or empty. Held as a UID rather than a HeroInstance
        /// because the roster is destroyed and rebuilt on every refresh - an instance reference
        /// would survive that and a UID re-resolves against the profile, which is the thing that
        /// actually changed.
        /// </summary>
        private string _selectedUid = "";

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "YOUR BANNER", Router, out _headerSubtitle);

            BuildBanner();
            BuildPrompt();
            BuildRoster();
        }

        /// <summary>
        /// Five slot cards in a row, left to right, front to back. Built once; only their contents
        /// are repainted, so a tap never rebuilds the thing it landed on.
        /// </summary>
        private void BuildBanner()
        {
            _bannerRow = UIBuilder.Node("Banner", Root);
            UIBuilder.Box(_bannerRow, 0f, -(UITheme.TopBarHeight + BannerTop), 1824f, BannerHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_bannerRow, 18f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            for (int i = 0; i < SlotCount; i++)
            {
                int slot = i;
                // A SELECTABLE CARD, not a button. A banner slot is a CONTAINER that happens to
                // be pressable - the same shape as a hero card in the collection - and the kit's
                // two button voices are ember for the thing to press and teal for the way back
                // out, neither of which a slot is. Five slot cards and thirty roster rows built
                // as buttons came out as a page of solid teal. See EmberforgeUI.SelectableCard.
                ForgeCard card = EmberforgeUI.SelectableCard("Slot" + i, _bannerRow,
                                                             delegate { OnSlotTapped(slot); });
                UIBuilder.Flexible(card.Root, 1f, 1f);

                _slotButtons[i] = card;
                _slotBodies[i] = card.Body;
            }
        }

        private void BuildPrompt()
        {
            _powerLabel = EmberforgeUI.Title("Power", Root, "", UITheme.FontHeading,
                                             Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_powerLabel.rectTransform, 0f, -(UITheme.TopBarHeight + PowerTop), 1824f, PowerHeight, new Vector2(0.5f, 1f));

            _prompt = EmberforgeUI.Label("Prompt", Root, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(_prompt.rectTransform, 0f, -(UITheme.TopBarHeight + PromptTop), 1824f, PromptHeight, new Vector2(0.5f, 1f));
        }

        private void BuildRoster()
        {
            Text heading = EmberforgeUI.Title("RosterHeading", Root, "YOUR ROSTER", UITheme.FontSmall,
                                              Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(heading.rectTransform, UITheme.ScreenPadding, -(UITheme.TopBarHeight + RosterLabelTop),
                          260f, 30f, new Vector2(0f, 1f));

            // The saved lines sit on the roster's own row, between its heading and VIEW ALL: a
            // line is a choice of who from this roster stands in the banner above.
            _lineTabs = UIBuilder.Node("Lines", Root);
            UIBuilder.Box(_lineTabs, UITheme.ScreenPadding + 280f, -(UITheme.TopBarHeight + RosterLabelTop - 12f),
                          6 * 158f, 54f, new Vector2(0f, 1f));

            // The collection survives as a separate screen and is reached from here. See the class
            // note in HeroesScreen for what it is FOR - it answers "what am I missing", which is a
            // different question from "who is fighting", and folding it in would have made this
            // screen do both jobs badly.
            UIButton viewAll = EmberforgeUI.Button("ViewAll", Root, "VIEW ALL HEROES", UITheme.FontTiny,
                                                   ForgeButtonKind.Secondary);
            UIBuilder.Box(viewAll.Root, -UITheme.ScreenPadding, -(UITheme.TopBarHeight + RosterLabelTop - 12f),
                          320f, 54f, new Vector2(1f, 1f));
            viewAll.OnClick(OpenCollection);

            // The Guild Path sells the lines this row switches between, and it is the player's own
            // whatever guild they are in or have left - so its door is here, on a screen every
            // player has, rather than only inside the guild.
            UIButton path = EmberforgeUI.Button("GuildPath", Root, "GUILD PATH", UITheme.FontTiny,
                                                ForgeButtonKind.Secondary);
            UIBuilder.Box(path.Root, -(UITheme.ScreenPadding + 340f), -(UITheme.TopBarHeight + RosterLabelTop - 12f),
                          260f, 54f, new Vector2(1f, 1f));
            path.OnClick(delegate { App.Audio.Tap(); Router.Show(ScreenId.GuildPath); });

            ScrollRect scroll;
            _rosterList = UIBuilder.ScrollList("Roster", Root, out scroll, 10f, new RectOffset(0, 0, 4, 32));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + RosterTop, UITheme.ScreenPadding, 20f);
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            App.RefreshHud();
            _selectedUid = "";
            Refresh();
        }

        private void Refresh()
        {
            RefreshLineTabs();
            RefreshBanner();
            RefreshRoster();
            RefreshPrompt();

            PlayerProfile profile = App.Session.Profile;
            int deployed = profile.TeamHeroes().Count;

            // Bare TeamPower, matching CampaignScreen and every other power readout in the game.
            // It is arguably the wrong measure - recommendedPower was rebased onto EFFECTIVE power
            // (relics and armor folded in) earlier in this project - but showing a different number
            // here than Campaign shows for the same team would be a new disagreement, and aligning
            // every power display is its own pass.
            _powerLabel.text = "BANNER POWER   " + profile.TeamPower();
            _headerSubtitle.text = (App.Session.LinesAllowed > 1 && profile.ActiveLine < LineNumerals.Length
                                        ? "LINE " + LineNumerals[profile.ActiveLine] + "   -   " : "")
                                   + deployed + " OF " + SlotCount + " SLOTS FILLED"
                                   + "   -   SLOT 1 IS THE FRONT OF THE LINE";
        }

        private void RefreshLineTabs()
        {
            for (int i = _lineTabs.childCount - 1; i >= 0; i--) Destroy(_lineTabs.GetChild(i).gameObject);
            int lines = App.Session.LinesAllowed;
            _lineTabs.gameObject.SetActive(lines > 1);
            for (int i = 0; i < lines && i < LineNumerals.Length; i++)
            {
                int index = i;
                ForgeTab tab = EmberforgeUI.Tab("LINE " + LineNumerals[i], _lineTabs, 146f, 54f, delegate { OnLine(index); });
                UIBuilder.Box(tab.Root, i * 158f, 0f, 146f, 54f, new Vector2(0f, 0.5f));
                EmberforgeUI.SetTabSelected(tab, i == App.Session.Profile.ActiveLine);
            }
        }

        /// <summary>Switches the banner to another saved line (GameSession.SwitchLine). The line
        /// being left keeps what it was, so nothing edited here is lost by switching.</summary>
        private void OnLine(int index)
        {
            if (index == App.Session.Profile.ActiveLine) return;
            App.Audio.Tap();
            _selectedUid = "";
            if (!App.Session.SwitchLine(index)) return;
            App.SaveNow();
            Refresh();
        }

        private void RefreshBanner()
        {
            PlayerProfile profile = App.Session.Profile;
            bool placing = !string.IsNullOrEmpty(_selectedUid);

            for (int i = 0; i < SlotCount; i++)
            {
                HeroInstance occupant = profile.FindHeroByUid(profile.Team[i]);
                bool isSelected = placing && occupant != null && occupant.Uid == _selectedUid;

                ForgeCard card = _slotButtons[i];
                RectTransform body = _slotBodies[i];
                for (int c = body.childCount - 1; c >= 0; c--) Destroy(body.GetChild(c).gameObject);

                // While a hero is waiting to be placed every slot is a live target, so every slot
                // lights. Highlighting only the plausible ones would be guessing at intent.
                EmberforgeUI.SetCardSelected(card, isSelected || placing);

                Text position = EmberforgeUI.Label("Pos", body, SlotTitle(i), UITheme.FontTiny,
                                                   i == 0 ? Emberforge.TextGold : Emberforge.TextQuiet,
                                                   TextAnchor.UpperCenter);
                UIBuilder.Box(position.rectTransform, 0f, -12f, 280f, 24f, new Vector2(0.5f, 1f));
                UIBuilder.Outlined(position, Emberforge.Outline, Emberforge.StrokeThin);

                if (occupant != null && occupant.Def != null)
                {
                    RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", body, occupant.Def,
                                                                        occupant.Def.Stars, 118f);
                    UIBuilder.Box(portrait, 0f, -38f, 118f, 118f, new Vector2(0.5f, 1f));

                    RectTransform stars = Widgets.StarRow("Stars", body, occupant.Def.Stars, 13f);
                    UIBuilder.Box(stars, 0f, -160f, 110f, 15f, new Vector2(0.5f, 1f));

                    Text power = EmberforgeUI.Label("Power", body, occupant.Power.ToString(),
                                                    UITheme.FontSmall, Emberforge.TextGold,
                                                    TextAnchor.LowerCenter);
                    UIBuilder.Box(power.rectTransform, 0f, 8f, 280f, 28f, new Vector2(0.5f, 0f));
                    UIBuilder.Outlined(power, Emberforge.Outline, Emberforge.StrokeThin);

                    Text who = EmberforgeUI.Label("Name", body, occupant.Def.Name.ToUpperInvariant(),
                                                  UITheme.FontSmall, Emberforge.TextBody,
                                                  TextAnchor.LowerCenter);
                    UIBuilder.Box(who.rectTransform, 0f, 40f, 300f, 30f, new Vector2(0.5f, 0f));
                    UIBuilder.Outlined(who, Emberforge.Outline, Emberforge.StrokeThin);
                }
                else
                {
                    Image ghost = UIBuilder.Img("Ghost", body, TextureLab.Ring(160, 3),
                                                UITheme.WithAlpha(Emberforge.Outline, 0.7f));
                    UIBuilder.Box(ghost.rectTransform, 0f, -38f, 118f, 118f, new Vector2(0.5f, 1f));

                    Text who = EmberforgeUI.Label("Name", body, "EMPTY", UITheme.FontSmall,
                                                  Emberforge.TextQuiet, TextAnchor.LowerCenter);
                    UIBuilder.Box(who.rectTransform, 0f, 40f, 300f, 30f, new Vector2(0.5f, 0f));
                    UIBuilder.Outlined(who, Emberforge.Outline, Emberforge.StrokeThin);
                }
            }
        }

        /// <summary>Names the ends of the line, because those are the two positions that mean
        /// something. The middle three are just order.</summary>
        private static string SlotTitle(int slot)
        {
            if (slot == 0) return "1  -  FRONT";
            if (slot == SlotCount - 1) return SlotCount + "  -  BACK";
            return (slot + 1).ToString();
        }

        private void RefreshRoster()
        {
            for (int i = _rosterList.childCount - 1; i >= 0; i--)
                Destroy(_rosterList.GetChild(i).gameObject);

            GameSessionRosterView view = BuildRosterView();
            for (int i = 0; i < view.Owned.Count; i++) AddOwnedRow(view.Owned[i]);
            for (int i = 0; i < view.Locked.Count; i++) AddLockedRow(view.Locked[i]);
        }

        private sealed class GameSessionRosterView
        {
            public readonly List<HeroInstance> Owned = new List<HeroInstance>();
            public readonly List<HeroDef> Locked = new List<HeroDef>();
        }

        /// <summary>
        /// Deployed heroes first and in slot order, then the bench strongest-first, then the locked
        /// ones. Slot order rather than power for the deployed block, because on this screen those
        /// five rows are a readout of the banner above them and sorting them differently would put
        /// the same five heroes in two different orders on one screen.
        /// </summary>
        private GameSessionRosterView BuildRosterView()
        {
            PlayerProfile profile = App.Session.Profile;
            GameSessionRosterView view = new GameSessionRosterView();

            for (int i = 0; i < SlotCount; i++)
            {
                HeroInstance h = profile.FindHeroByUid(profile.Team[i]);
                if (h != null) view.Owned.Add(h);
            }

            List<HeroInstance> bench = new List<HeroInstance>();
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                if (!profile.IsDeployed(h.Uid)) bench.Add(h);
            }
            bench.Sort(delegate (HeroInstance a, HeroInstance b) { return b.Power.CompareTo(a.Power); });
            view.Owned.AddRange(bench);

            List<HeroDef> all = App.Session.Content.Heroes;
            for (int i = 0; i < all.Count; i++)
                if (!profile.Owns(all[i].Id)) view.Locked.Add(all[i]);

            return view;
        }

        private void AddOwnedRow(HeroInstance hero)
        {
            PlayerProfile profile = App.Session.Profile;
            int slot = profile.TeamSlotOf(hero.Uid);
            bool selected = _selectedUid == hero.Uid;

            // A SELECTABLE CARD for the same reason the banner slots are. Framed while this is
            // the hero being placed, plain otherwise; where a hero already stands is said by the
            // STATE word on the right, which is where a player reads it.
            ForgeCard row = EmberforgeUI.SelectableCard("Hero_" + hero.Uid, _rosterList,
                                                        delegate { OnHeroTapped(hero); });
            UIBuilder.Sized(row.Root, 0f, RowHeight);
            EmberforgeUI.SetCardSelected(row, selected);

            RectTransform content = row.Body;

            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", content, hero.Def,
                                                                hero.Def.Stars, 76f);
            UIBuilder.Box(portrait, 26f, 0f, 76f, 76f, new Vector2(0f, 0.5f));

            // Name above the centre line, detail below it. The first draft anchored both near the
            // middle and they printed on top of each other.
            Text name = EmberforgeUI.Label("Name", content, hero.Def.Name, UITheme.FontSmall,
                                           UITheme.StarColor(hero.Def.Stars), TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 124f, 19f, 620f, 34f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            Text sub = EmberforgeUI.Label("Sub", content,
                                       "LV " + hero.Level + "   -   " + UITheme.RoleName(hero.Def.Role)
                                       + "   -   " + AspectChart.DisplayName(hero.Def.Aspect),
                                       UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(sub.rectTransform, 124f, -18f, 620f, 28f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(sub, Emberforge.Outline, Emberforge.StrokeThin);

            Text power = EmberforgeUI.Label("Power", content, hero.Power.ToString(), UITheme.FontSmall,
                                            Emberforge.TextGold, TextAnchor.MiddleRight);
            UIBuilder.Box(power.rectTransform, -230f, 0f, 190f, 34f, new Vector2(1f, 0.5f));
            UIBuilder.Outlined(power, Emberforge.Outline, Emberforge.StrokeThin);

            string status = selected ? "PLACING" : slot >= 0 ? "SLOT " + (slot + 1) : "BENCH";
            Text state = EmberforgeUI.Label("State", content, status, UITheme.FontTiny,
                                           selected ? Emberforge.TextGold
                                           : slot >= 0 ? UITheme.Success : Emberforge.TextQuiet,
                                           TextAnchor.MiddleRight);
            UIBuilder.Box(state.rectTransform, -24f, 0f, 150f, 30f, new Vector2(1f, 0.5f));
            UIBuilder.Outlined(state, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>
        /// A locked hero is shown - this is a roster, and a player deciding a line-up should see
        /// what they do not have yet - but it is NOT a button. Interactable false rather than a
        /// tap that explains itself, because the row has nothing to offer and a disabled control
        /// that dims on sight is a cheaper answer than one that has to be pressed to say no.
        /// </summary>
        private void AddLockedRow(HeroDef def)
        {
            ForgeCard row = EmberforgeUI.SelectableCard("Locked_" + def.Id, _rosterList, null);
            UIBuilder.Sized(row.Root, 0f, RowHeight);
            row.Button.Interactable = false;

            // A locked row is the same plate at reduced strength rather than a different plate:
            // the list stays one list, and "you do not have this one" is a fade rather than a
            // second design.
            row.Root.gameObject.AddComponent<CanvasGroup>().alpha = 0.7f;

            RectTransform content = row.Body;

            // allowArt false, the same rule the collection screen follows: a locked hero is a
            // silhouette so an owned one is never out-presented by a hero you cannot field.
            // allowArt false is still the rule here, so this goes through Widgets directly
            // rather than through the kit's PortraitFrame: a locked hero is a silhouette so an
            // owned one can never be out-presented by a hero you cannot field.
            RectTransform portrait = Widgets.Portrait("Portrait", content, def, def.Stars, 76f, false, true, false);
            UIBuilder.Box(portrait, 26f, 0f, 76f, 76f, new Vector2(0f, 0.5f));

            Text name = EmberforgeUI.Label("Name", content, def.Name, UITheme.FontSmall,
                                        Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 124f, 19f, 620f, 34f, new Vector2(0f, 0.5f));

            Text sub = EmberforgeUI.Label("Sub", content, "NOT YET SUMMONED", UITheme.FontTiny,
                                       Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(sub.rectTransform, 124f, -18f, 620f, 28f, new Vector2(0f, 0.5f));

            Text state = EmberforgeUI.Label("State", content, "LOCKED", UITheme.FontTiny,
                                         Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(state.rectTransform, -24f, 0f, 150f, 30f, new Vector2(1f, 0.5f));
        }

        private void RefreshPrompt()
        {
            PlayerProfile profile = App.Session.Profile;

            if (!string.IsNullOrEmpty(_selectedUid))
            {
                HeroInstance selected = profile.FindHeroByUid(_selectedUid);
                string who = selected != null && selected.Def != null
                    ? selected.Def.Name.ToUpperInvariant() : "THIS HERO";
                _prompt.text = profile.IsDeployed(_selectedUid)
                    ? "TAP A SLOT TO MOVE " + who + " THERE   -   TAP THEM AGAIN TO CANCEL"
                    : "TAP THE SLOT " + who + " SHOULD TAKE   -   TAP THEM AGAIN TO CANCEL";
                _prompt.color = UITheme.Gold;
                return;
            }

            _prompt.text = "TAP A HERO BELOW TO DEPLOY THEM, OR A SLOT TO MOVE WHO IS IN IT";
            _prompt.color = Emberforge.TextQuiet;
        }

        // ------------------------------------------------------------------ actions

        private void OnHeroTapped(HeroInstance hero)
        {
            if (hero == null) return;
            App.Audio.Tap();

            if (_selectedUid == hero.Uid) { _selectedUid = ""; Refresh(); return; }

            PlayerProfile profile = App.Session.Profile;

            // Already on the banner: this is a reorder, and slot order decides who front-targeting
            // abilities reach first, so it is a real move rather than a no-op.
            if (profile.IsDeployed(hero.Uid)) { _selectedUid = hero.Uid; Refresh(); return; }

            int empty = FirstEmptySlot();
            if (empty >= 0)
            {
                Place(empty, hero.Uid);
                return;
            }

            _selectedUid = hero.Uid;
            Refresh();
        }

        private void OnSlotTapped(int slot)
        {
            App.Audio.Tap();
            PlayerProfile profile = App.Session.Profile;

            if (!string.IsNullOrEmpty(_selectedUid)) { Place(slot, _selectedUid); return; }

            HeroInstance occupant = profile.FindHeroByUid(profile.Team[slot]);
            if (occupant != null) { _selectedUid = occupant.Uid; Refresh(); return; }

            // An empty slot with nothing selected has no action of its own; say what would give it
            // one rather than doing nothing silently.
            _prompt.text = "SLOT " + (slot + 1) + " IS EMPTY   -   TAP A HERO BELOW TO DEPLOY THEM";
            _prompt.color = UITheme.Gold;
        }

        /// <summary>
        /// Commits one placement. AssignToTeam already handles the swap case - if the hero was
        /// deployed elsewhere, whoever was in the destination takes their old slot - so this does
        /// not need to know whether it is placing, swapping or reordering.
        /// </summary>
        private void Place(int slot, string uid)
        {
            PlayerProfile profile = App.Session.Profile;
            HeroInstance hero = profile.FindHeroByUid(uid);
            if (hero == null) { _selectedUid = ""; Refresh(); return; }

            profile.AssignToTeam(slot, uid);
            _selectedUid = "";

            App.RefreshHud();
            App.RequestSave();
            Refresh();

            App.Notify((hero.Def != null ? hero.Def.Name : "Hero") + " now stands in slot " + (slot + 1) + ".");
        }

        private int FirstEmptySlot()
        {
            PlayerProfile profile = App.Session.Profile;
            for (int i = 0; i < SlotCount; i++)
                if (profile.FindHeroByUid(profile.Team[i]) == null) return i;
            return -1;
        }

        private void OpenCollection()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Heroes);
        }
    }
}
