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

        private readonly UIButton[] _slotButtons = new UIButton[SlotCount];
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
            ScreenChrome.Header(Root, "YOUR BANNER", Router, out _headerSubtitle);

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
                UIButton card = UIBuilder.MakeButton("Slot" + i, _bannerRow, "", UITheme.FontSmall,
                                                     UITheme.PanelSunken, UITheme.Border, UITheme.TextPrimary);
                UIBuilder.Flexible(card.Root, 1f, 1f);

                // MakeButton's own label fills the face and would sit on top of the portrait; it is
                // pushed to the bottom of the card and used for the occupant's name.
                UIBuilder.Box(card.Label.rectTransform, 0f, 40f, 300f, 30f, new Vector2(0.5f, 0f));

                // Everything below the title is parented to a body node so a repaint can clear it
                // without touching the button, its frame, or its label.
                RectTransform body = UIBuilder.Node("Body", card.Root);
                UIBuilder.FillParent(body, 0f);
                body.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

                _slotButtons[i] = card;
                _slotBodies[i] = body;
                card.OnClick(delegate { OnSlotTapped(slot); });
            }
        }

        private void BuildPrompt()
        {
            _powerLabel = UIBuilder.Label("Power", Root, "", UITheme.FontHeading, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_powerLabel.rectTransform, 0f, -(UITheme.TopBarHeight + PowerTop), 1824f, PowerHeight, new Vector2(0.5f, 1f));

            _prompt = UIBuilder.Label("Prompt", Root, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Box(_prompt.rectTransform, 0f, -(UITheme.TopBarHeight + PromptTop), 1824f, PromptHeight, new Vector2(0.5f, 1f));
        }

        private void BuildRoster()
        {
            Text heading = UIBuilder.Label("RosterHeading", Root, "YOUR ROSTER", UITheme.FontTiny,
                                           UITheme.TextDim, TextAnchor.MiddleLeft);
            UIBuilder.Box(heading.rectTransform, UITheme.ScreenPadding, -(UITheme.TopBarHeight + RosterLabelTop),
                          600f, 30f, new Vector2(0f, 1f));

            // The collection survives as a separate screen and is reached from here. See the class
            // note in HeroesScreen for what it is FOR - it answers "what am I missing", which is a
            // different question from "who is fighting", and folding it in would have made this
            // screen do both jobs badly.
            UIButton viewAll = UIBuilder.SecondaryButton("ViewAll", Root, "VIEW ALL HEROES", UITheme.FontTiny);
            UIBuilder.Box(viewAll.Root, -UITheme.ScreenPadding, -(UITheme.TopBarHeight + RosterLabelTop - 12f),
                          320f, 54f, new Vector2(1f, 1f));
            viewAll.OnClick(OpenCollection);

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
            _headerSubtitle.text = deployed + " OF " + SlotCount + " SLOTS FILLED"
                                   + "   -   SLOT 1 IS THE FRONT OF THE LINE";
        }

        private void RefreshBanner()
        {
            PlayerProfile profile = App.Session.Profile;
            bool placing = !string.IsNullOrEmpty(_selectedUid);

            for (int i = 0; i < SlotCount; i++)
            {
                HeroInstance occupant = profile.FindHeroByUid(profile.Team[i]);
                bool isSelected = placing && occupant != null && occupant.Uid == _selectedUid;

                UIButton card = _slotButtons[i];
                RectTransform body = _slotBodies[i];
                for (int c = body.childCount - 1; c >= 0; c--) Destroy(body.GetChild(c).gameObject);

                // While a hero is waiting to be placed every slot is a live target, so every slot
                // says so. Highlighting only the plausible ones would be guessing at intent.
                Color fill = isSelected ? UITheme.Ember
                           : placing ? UITheme.PanelRaised
                           : UITheme.PanelSunken;
                Color edge = isSelected ? UITheme.EmberDeep
                           : placing ? UITheme.Gold
                           : (occupant != null ? UITheme.Border : UITheme.WithAlpha(UITheme.BorderSoft, 0.6f));
                card.Background.color = fill;
                card.Frame.color = edge;

                Text position = UIBuilder.Label("Pos", body, SlotTitle(i), UITheme.FontTiny,
                                                i == 0 ? UITheme.Ember : UITheme.TextDim, TextAnchor.UpperCenter);
                UIBuilder.Box(position.rectTransform, 0f, -12f, 280f, 24f, new Vector2(0.5f, 1f));

                if (occupant != null && occupant.Def != null)
                {
                    RectTransform portrait = Widgets.Portrait("Portrait", body, occupant.Def, occupant.Def.Stars, 118f, true);
                    UIBuilder.Box(portrait, 0f, -38f, 118f, 118f, new Vector2(0.5f, 1f));

                    RectTransform stars = Widgets.StarRow("Stars", body, occupant.Def.Stars, 13f);
                    UIBuilder.Box(stars, 0f, -160f, 110f, 15f, new Vector2(0.5f, 1f));

                    Text power = UIBuilder.Label("Power", body, occupant.Power.ToString(), UITheme.FontSmall,
                                                 UITheme.Gold, TextAnchor.LowerCenter);
                    UIBuilder.Box(power.rectTransform, 0f, 8f, 280f, 28f, new Vector2(0.5f, 0f));

                    card.SetText(occupant.Def.Name.ToUpperInvariant());
                    card.Label.color = isSelected ? UITheme.TextOnAccent : UITheme.TextPrimary;
                }
                else
                {
                    Image ghost = UIBuilder.Img("Ghost", body, TextureLab.Ring(160, 3),
                                                UITheme.WithAlpha(UITheme.BorderSoft, 0.7f));
                    UIBuilder.Box(ghost.rectTransform, 0f, -38f, 118f, 118f, new Vector2(0.5f, 1f));

                    card.SetText("EMPTY");
                    card.Label.color = UITheme.TextDim;
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

            UIButton row = UIBuilder.MakeButton("Hero_" + hero.Uid, _rosterList, "", UITheme.FontSmall,
                                                selected ? UITheme.Ember
                                                : slot >= 0 ? UITheme.WithAlpha(UITheme.Success, 0.16f)
                                                : UITheme.PanelRaised,
                                                selected ? UITheme.EmberDeep : UITheme.Border,
                                                UITheme.TextPrimary);
            UIBuilder.Sized(row.Root, 0f, RowHeight);
            row.Label.gameObject.SetActive(false);   // this row lays its own text out

            RectTransform content = UIBuilder.Node("Body", row.Root);
            UIBuilder.FillParent(content, 0f);
            content.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            RectTransform portrait = Widgets.Portrait("Portrait", content, hero.Def, hero.Def.Stars, 76f, true);
            UIBuilder.Box(portrait, 26f, 0f, 76f, 76f, new Vector2(0f, 0.5f));

            // Name above the centre line, detail below it. The first draft anchored both near the
            // middle and they printed on top of each other.
            Text name = UIBuilder.Label("Name", content, hero.Def.Name, UITheme.FontSmall,
                                        UITheme.StarColor(hero.Def.Stars), TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 124f, 19f, 620f, 34f, new Vector2(0f, 0.5f));

            Text sub = UIBuilder.Label("Sub", content,
                                       "LV " + hero.Level + "   -   " + UITheme.RoleName(hero.Def.Role)
                                       + "   -   " + AspectChart.DisplayName(hero.Def.Aspect),
                                       UITheme.FontTiny, UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIBuilder.Box(sub.rectTransform, 124f, -18f, 620f, 28f, new Vector2(0f, 0.5f));

            Text power = UIBuilder.Label("Power", content, hero.Power.ToString(), UITheme.FontSmall,
                                         UITheme.Gold, TextAnchor.MiddleRight);
            UIBuilder.Box(power.rectTransform, -230f, 0f, 190f, 34f, new Vector2(1f, 0.5f));

            string status = selected ? "PLACING" : slot >= 0 ? "SLOT " + (slot + 1) : "BENCH";
            Text state = UIBuilder.Label("State", content, status, UITheme.FontTiny,
                                         selected ? UITheme.TextOnAccent
                                         : slot >= 0 ? UITheme.Success : UITheme.TextDim,
                                         TextAnchor.MiddleRight);
            UIBuilder.Box(state.rectTransform, -24f, 0f, 150f, 30f, new Vector2(1f, 0.5f));

            HeroInstance captured = hero;
            row.OnClick(delegate { OnHeroTapped(captured); });
        }

        /// <summary>
        /// A locked hero is shown - this is a roster, and a player deciding a line-up should see
        /// what they do not have yet - but it is NOT a button. Interactable false rather than a
        /// tap that explains itself, because the row has nothing to offer and a disabled control
        /// that dims on sight is a cheaper answer than one that has to be pressed to say no.
        /// </summary>
        private void AddLockedRow(HeroDef def)
        {
            UIButton row = UIBuilder.MakeButton("Locked_" + def.Id, _rosterList, "", UITheme.FontSmall,
                                                UITheme.PanelSunken, UITheme.WithAlpha(UITheme.BorderSoft, 0.5f),
                                                UITheme.TextDim);
            UIBuilder.Sized(row.Root, 0f, RowHeight);
            row.Label.gameObject.SetActive(false);
            row.Interactable = false;

            RectTransform content = UIBuilder.Node("Body", row.Root);
            UIBuilder.FillParent(content, 0f);
            content.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            // allowArt false, the same rule the collection screen follows: a locked hero is a
            // silhouette so an owned one is never out-presented by a hero you cannot field.
            RectTransform portrait = Widgets.Portrait("Portrait", content, def, def.Stars, 76f, true, true, false);
            UIBuilder.Box(portrait, 26f, 0f, 76f, 76f, new Vector2(0f, 0.5f));

            Text name = UIBuilder.Label("Name", content, def.Name, UITheme.FontSmall,
                                        UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 124f, 19f, 620f, 34f, new Vector2(0f, 0.5f));

            Text sub = UIBuilder.Label("Sub", content, "NOT YET SUMMONED", UITheme.FontTiny,
                                       UITheme.TextDim, TextAnchor.MiddleLeft);
            UIBuilder.Box(sub.rectTransform, 124f, -18f, 620f, 28f, new Vector2(0f, 0.5f));

            Text state = UIBuilder.Label("State", content, "LOCKED", UITheme.FontTiny,
                                         UITheme.TextDim, TextAnchor.MiddleRight);
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
            _prompt.color = UITheme.TextMuted;
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
