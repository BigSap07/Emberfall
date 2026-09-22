using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.UI.Summon;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Rift. Spend Ember Shards, get an ally.
    ///
    /// ---- visual pass ----
    /// ONE DOMINANT IMAGE. The screen used to be a spinning portal on the left and a stack of odds,
    /// purse and two buttons on the right - two competing halves, neither of them a subject. It is
    /// now a single banner stage carrying the featured hero at size, with the action beneath it.
    ///
    /// WHICH HERO IS "FEATURED", AND WHY IT IS DERIVED. There is no banner concept in content:
    /// heroes.json has no featured flag and no banner definition, and content was out of scope for
    /// this pass. So the face of the banner is DERIVED - the highest-star hero in the summon pool,
    /// resolved in content order so it is stable between sessions and between players. That is a
    /// stand-in for a real rotating banner, not an implementation of one, and it is written up in
    /// the report rather than left to look like a feature.
    ///
    /// THE PITY BAR SHOWS THE GUARANTEE THAT ACTUALLY EXISTS. The mockup puts a progress bar and a
    /// chest under the pull button, which in most games of this shape means a recurring pity
    /// counter. Emberfall has no recurring pity. What it has is GameSession.PityGuaranteedSummons -
    /// the first three pulls of an account draw only from heroes not already owned - and that is
    /// what this bar tracks. Once it is spent the bar says so plainly instead of resetting, because
    /// a bar that refilled would be describing a mechanic the game does not have. Inventing one
    /// here would have been a game-logic change wearing a visual pass's clothes.
    ///
    /// PULL X10 SURVIVES, SUBORDINATED. The mockup shows a single button and the brief asks for
    /// one; deleting the ten-pull would have removed a working action during a pass that was told
    /// to leave behaviour alone. So x1 is the dominant button the mockup calls for and x10 is
    /// clearly secondary beside it - a hierarchy rather than an amputation. Also flagged.
    ///
    /// Everything below the visual layer is untouched: DoSummon still loops the single
    /// GameSession.Summon, and neither the weight table nor the duplicate conversion was changed.
    ///
    /// ---- the summon sequence ----
    /// What happens AFTER the roll is SummonSequence's: the button sinks, the room goes dark, a
    /// light builds and commits to the rolled tier's colour, a flash, and the hero. A ten-pull
    /// plays the full buildup once and a short tell for each later pull, then a summary grid.
    /// It replaced a fade-in portrait popup and a text list of ten rows. The screen hands the
    /// sequence outcomes that are already rolled, charged and saved, so the show can be skipped,
    /// aborted or interrupted by leaving the screen without anything being lost. See
    /// DESIGN-SUMMON-PRESENTATION for the beats, the timings and the skip rules.
    /// </summary>
    public sealed class SummonScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Summon; } }

        /// <summary>
        /// This screen carries its own CurrencyStrip, so the shared top bar's Gold/Shards/Dust
        /// pills would state three of the same six numbers a few pixels above it. One readout, not
        /// two. Same mechanism BattleScreen already uses, and ScreenRouter re-applies it on every
        /// Show, so the bar returns intact on any screen that does not opt out.
        /// </summary>
        public override bool HidesTopBar { get { return true; } }

        private const float StageWidth = 1500f;
        private const float PullWidth = 640f;

        /// <summary>
        /// THE STAGE STRETCHES between the currency strip and the action block, and that is a fix
        /// rather than a preference.
        ///
        /// It was a fixed 500 tall, which put the pity bar at y 972 and the odds line below that -
        /// on a 20:9 phone, whose logical canvas is about 966 tall, the whole action block was off
        /// the bottom of the screen. It fitted at 16:9 because that canvas is 1080. Same fault as
        /// the hero page's, found the same way, and fixed the same way: the fixed element is the
        /// FURNITURE and the stage takes whatever is left.
        ///
        ///   966 short canvas - 336 header and strip - 226 action block = 404 of stage.
        /// </summary>
        private const float ShortestCanvasHeight = 966f;

        /// <summary>Everything under the stage, measured from the stage's own bottom edge: the
        /// gap, the pull buttons, the guarantee bar and the two lines of small print.</summary>
        private const float PullHeight = 88f;
        private const float PityRowHeight = 36f;
        private const float ActionBlockHeight = UITheme.Space16 + PullHeight + UITheme.Space12
                                              + PityRowHeight + 6f + 24f + 4f + 24f + UITheme.Space16;

        /// <summary>The band at the bottom of the stage the name and the title occupy, which is
        /// the space the featured portrait is centred ABOVE rather than in.</summary>
        private const float NameBand = 130f;

        private Text _headerSubtitle;
        private readonly CurrencyStrip _currencies = new CurrencyStrip();

        private RectTransform _portraitHost;
        private Image _portalRingOuter;
        private Image _portalRingInner;
        private Image _portalCore;
        private Text _featuredName;
        private Text _featuredTitle;

        private UIButton _pullOne;
        private UIButton _pullTen;
        private Image _pityFill;
        private Text _pityLabel;
        private Image _pityChest;
        private Text _oddsLabel;

        /// <summary>The summon itself: press, buildup, tell, flash, reveal, and the ten-pull
        /// summary. Built on the app's overlay layer rather than inside this screen, so the dim
        /// covers every other layer of UI too. See DESIGN-SUMMON-PRESENTATION.</summary>
        private SummonSequence _sequence;

        private float _spin;
        private bool _busy;
        private string _featuredId = "";

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);

            ScreenChrome.ForgedHeader(Root, "THE RIFT", Router, out _headerSubtitle);
            _headerSubtitle.text = "BIND A NEW ALLY TO YOUR BANNER";
            _currencies.Build(Root);

            float top = CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop);
            BuildStage(top);

            // Says out loud when the stage has been squeezed to nothing on the shortest canvas
            // this game targets, for the reason DesignGallery.CheckFits exists: the failure is
            // silent at 16:9 and only shows on the 20:9 capture.
            float shortest = ShortestCanvasHeight - top - ActionBlockHeight;
            if (shortest < NameBand + 200f)
                Debug.LogError("SummonScreen: the banner stage is only " + shortest + " units tall"
                    + " on the shortest canvas this game targets, which is not enough for the"
                    + " featured hero and their name. Shorten the action block.");

            BuildActions(UITheme.Space16);

            _sequence = SummonSequence.Build(App.OverlayLayer);
            // Keeps the synthesised tones this screen already played, until real SFX replace it.
            if (SummonAudio.Hook == null) SummonAudio.Hook = new SfxSummonAudio(App.Audio);
        }

        /// <summary>The banner stage: the rift behind, the featured hero in front of it.</summary>
        private void BuildStage(float top)
        {
            RectTransform stage = UIBuilder.Node("Stage", Root);
            // Anchored top AND bottom, so the stage is exactly the room left between the strip
            // above it and the action block below it, at any canvas height.
            stage.anchorMin = new Vector2(0.5f, 0f);
            stage.anchorMax = new Vector2(0.5f, 1f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(StageWidth, -(top + ActionBlockHeight));
            stage.anchoredPosition = new Vector2(0f, (ActionBlockHeight - top) * 0.5f);

            EmberforgeUI.PanelOn(stage, true);

            // The featured hero is centred in the space ABOVE the name band rather than in the
            // stage, so a shorter stage moves the portrait up into the room it still has instead
            // of printing the name across the bottom of the art.
            RectTransform focus = UIBuilder.Node("Focus", stage);
            focus.anchorMin = new Vector2(0.5f, 0f);
            focus.anchorMax = new Vector2(0.5f, 1f);
            focus.pivot = new Vector2(0.5f, 0.5f);
            focus.sizeDelta = new Vector2(StageWidth, -NameBand);
            focus.anchoredPosition = new Vector2(0f, NameBand * 0.5f);

            // The rift itself, kept but demoted: it is the light the hero stands in now rather than
            // the subject of the screen. Same rings, same Update-driven spin, smaller and behind.
            // Lifted off centre and tightened to 340, so it reads as an aura around the hero
            // rather than as a hoop the layout has to work around.
            RectTransform portal = UIBuilder.Node("Portal", focus);
            UIBuilder.Box(portal, 0f, 0f, PortalSize, PortalSize, new Vector2(0.5f, 0.5f));

            Image halo = UIBuilder.Img("Halo", portal, TextureLab.Glow(256, 1.6f),
                                       UITheme.WithAlpha(UITheme.StarColor(5), 0.22f));
            UIBuilder.FillParent(halo.rectTransform, -40f);

            _portalRingOuter = UIBuilder.Img("RingOuter", portal, TextureLab.Ring(256, 8),
                                             UITheme.WithAlpha(UITheme.Shard, 0.55f));
            UIBuilder.FillParent(_portalRingOuter.rectTransform, 0f);

            _portalRingInner = UIBuilder.Img("RingInner", portal, TextureLab.Ring(256, 12),
                                             UITheme.WithAlpha(UITheme.Ember, 0.45f));
            UIBuilder.FillParent(_portalRingInner.rectTransform, 62f);

            _portalCore = UIBuilder.Img("Core", portal, TextureLab.Glow(192, 2.4f),
                                        UITheme.WithAlpha(UITheme.StarColor(5), 0.4f));
            UIBuilder.FillParent(_portalCore.rectTransform, 110f);

            // The featured hero, filled in on OnShow once the session is readable.
            _portraitHost = UIBuilder.Node("Featured", focus);
            UIBuilder.Box(_portraitHost, 0f, 0f, FeaturedSize, FeaturedSize, new Vector2(0.5f, 0.5f));

            _featuredName = EmberforgeUI.Title("FeaturedName", stage, "", UITheme.FontTitle,
                                               Emberforge.TextGold, TextAnchor.LowerCenter);
            UIBuilder.Box(_featuredName.rectTransform, 0f, 62f, StageWidth - 120f, 54f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_featuredName, Emberforge.Outline, Emberforge.StrokeThick);

            _featuredTitle = EmberforgeUI.Label("FeaturedTitle", stage, "", UITheme.FontSmall,
                                                Emberforge.TextQuiet, TextAnchor.LowerCenter);
            UIBuilder.Box(_featuredTitle.rectTransform, 0f, 24f, StageWidth - 120f, 34f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_featuredTitle, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>
        /// The action block: one dominant pull, the ten-pull beside it, the guarantee bar under
        /// both, and the odds reduced to a single line.
        ///
        /// The odds used to occupy a 880x170 card. They are one sentence, and this screen now has a
        /// subject that deserves the room, so they are one line - the same sentence BuildRates
        /// composed, with the same single-tier special case, just not framed like a feature.
        /// </summary>
        /// <summary>The featured badge, and the rift ring behind it. The ring is 1.13 times the
        /// badge so a ribbon of it shows past the rim rather than being buried by it.</summary>
        private const float FeaturedSize = 250f;
        private const float PortalSize = 283f;

        /// <summary>
        /// The action block, anchored to the BOTTOM of the screen rather than measured down from
        /// the top. Every y below is a distance up from the bottom edge, which is what keeps the
        /// block on screen at a canvas height nobody laid it out against.
        /// </summary>
        private void BuildActions(float bottom)
        {
            // ---- the dominant pull.
            //
            // Label LEFT, cost RIGHT, each anchored to its own edge. Centring both and nudging them
            // apart by hand is what the first pass did, and at "NEED 1122 MORE" the cost ran back
            // under the word PULL. Anchoring them to opposite edges means the cost can grow to
            // whatever the shortfall needs without ever reaching the label.
            float pullY = bottom + 24f + 4f + 24f + 6f + PityRowHeight + UITheme.Space12;

            _pullOne = EmberforgeUI.Button("PullOne", Root, "PULL", UITheme.FontHeading,
                                           ForgeButtonKind.Primary);
            UIBuilder.Box(_pullOne.Root, -150f, pullY, PullWidth, PullHeight, new Vector2(0.5f, 0f));

            Image shard = UIBuilder.Img("Shard", _pullOne.Root, TextureLab.CrystalIcon(64), UITheme.Shard);
            UIBuilder.Box(shard.rectTransform, 34f, 0f, 32f, 32f, new Vector2(0f, 0.5f));

            _pullOne.Label.alignment = TextAnchor.MiddleLeft;
            UIBuilder.Box(_pullOne.Label.rectTransform, 82f, 0f, 220f, 48f, new Vector2(0f, 0.5f));

            _pullOne.SubLabel = EmberforgeUI.Label("Cost", _pullOne.Root, "", UITheme.FontSmall,
                                                   Emberforge.TextOnColor, TextAnchor.MiddleRight);
            UIBuilder.Box(_pullOne.SubLabel.rectTransform, -28f, 0f, 300f, 36f, new Vector2(1f, 0.5f));
            UIBuilder.Outlined(_pullOne.SubLabel, Emberforge.Outline, Emberforge.StrokeThin);
            _pullOne.SubLabel.raycastTarget = false;
            _pullOne.OnClick(delegate { DoSummon(1); });

            // Secondary, and shaped to read that way: narrower, shorter, and the panel fill rather
            // than gold. Same action it always had. Seated clear of PullOne's right edge - the two
            // overlapped by 20px in the first pass, measured at x[190,210].
            _pullTen = EmberforgeUI.Button("PullTen", Root, "PULL X10", UITheme.FontSmall,
                                           ForgeButtonKind.Secondary);
            UIBuilder.Box(_pullTen.Root, PullWidth * 0.5f + 20f, pullY, 300f, PullHeight, new Vector2(0.5f, 0f));
            UIBuilder.Box(_pullTen.Label.rectTransform, 0f, 14f, 260f, 34f, new Vector2(0.5f, 0.5f));

            _pullTen.SubLabel = EmberforgeUI.Label("Cost", _pullTen.Root, "", UITheme.FontTiny,
                                                   Emberforge.TextOnColor, TextAnchor.MiddleCenter);
            UIBuilder.Box(_pullTen.SubLabel.rectTransform, 0f, -18f, 260f, 30f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_pullTen.SubLabel, Emberforge.Outline, Emberforge.StrokeThin);
            _pullTen.SubLabel.raycastTarget = false;
            _pullTen.OnClick(delegate { DoSummon(10); });

            BuildPityBar(bottom);
        }

        /// <summary>
        /// The opening guarantee, as a slim track with the reward at its end.
        ///
        /// Tick marks sit at each pull boundary rather than being decorative: with a three-pull
        /// guarantee the segments ARE the pulls, so a player can count what they have left without
        /// reading the label.
        /// </summary>
        private void BuildPityBar(float bottom)
        {
            float rowY = bottom + 24f + 4f + 24f + 6f;

            RectTransform row = UIBuilder.Node("Pity", Root);
            UIBuilder.Box(row, -34f, rowY, PullWidth + 300f, PityRowHeight, new Vector2(0.5f, 0f));

            // THE KIT'S BAR, and the row grew from 22 to 36 to hold it. The trough and the fill
            // are nine-sliced with most of their height in the top and bottom borders, so an
            // 18-unit track has no middle left to stretch and both render as a squashed pair of
            // corners - a lens rather than a bar. Both captures of this screen showed exactly
            // that, first with a chamfered plate and then with the kit's own sprite scaled by a
            // FILLED image, which is the other way to lose the nine-slice: Image.Type.Filled
            // ignores the borders entirely and stretches the whole sprite.
            //
            // Anchor-driven instead, which is what EmberforgeUI.ProgressBar is for. The ticks
            // are added to the same root AFTER the fill, so they draw over it and stay put while
            // the fill's right edge moves under them.
            _pityFill = EmberforgeUI.ProgressBar("Track", row);
            _pityFill.raycastTarget = false;

            RectTransform track = _pityFill.rectTransform.parent as RectTransform;
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(0f, -PityRowHeight * 0.5f);
            track.offsetMax = new Vector2(-44f, PityRowHeight * 0.5f);

            // A tick at each pull boundary inside the guarantee.
            for (int i = 1; i < GameSession.PityGuaranteedSummons; i++)
            {
                float t = i / (float)GameSession.PityGuaranteedSummons;
                Image tick = UIBuilder.Img("Tick" + i, track, TextureLab.Diamond(32),
                                           Emberforge.Outline);
                RectTransform tickRect = tick.rectTransform;
                tickRect.anchorMin = new Vector2(t, 0.5f);
                tickRect.anchorMax = new Vector2(t, 0.5f);
                tickRect.pivot = new Vector2(0.5f, 0.5f);
                tickRect.sizeDelta = new Vector2(16f, 16f);
                tickRect.anchoredPosition = Vector2.zero;
            }

            _pityChest = UIBuilder.Img("Chest", row, TextureLab.Chest(64), Emberforge.TextGold);
            UIBuilder.Box(_pityChest.rectTransform, 0f, 0f, 34f, 34f, new Vector2(1f, 0.5f));

            _pityLabel = EmberforgeUI.Label("PityLabel", Root, "", UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.LowerCenter);
            UIBuilder.Box(_pityLabel.rectTransform, -34f, bottom + 28f, 1000f, 24f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_pityLabel, Emberforge.Outline, Emberforge.StrokeThin);

            _oddsLabel = EmberforgeUI.Label("Odds", Root, "", UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.LowerCenter);
            UIBuilder.Box(_oddsLabel.rectTransform, 0f, bottom, 1200f, 24f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(_oddsLabel, Emberforge.Outline, Emberforge.StrokeThin);
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            App.RefreshHud();
            _busy = false;
            _sequence.Abort();
            RefreshFeatured();
            Refresh();
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _spin += Time.unscaledDeltaTime;

            if (_portalRingOuter != null)
                _portalRingOuter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _spin * 22f);
            if (_portalRingInner != null)
                _portalRingInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_spin * 34f);
            if (_portalCore != null)
            {
                float pulse = 1f + Mathf.Sin(_spin * 2.2f) * 0.06f;
                _portalCore.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        /// <summary>
        /// The banner's face. Rebuilt only when the featured hero CHANGES, which in practice means
        /// once - the portrait is a real sprite load and tearing it down every OnShow would be
        /// waste for a subject that cannot move without a content change.
        /// </summary>
        private void RefreshFeatured()
        {
            HeroDef featured = FeaturedHero();
            if (featured == null || featured.Id == _featuredId) return;
            _featuredId = featured.Id;

            for (int i = _portraitHost.childCount - 1; i >= 0; i--)
                Destroy(_portraitHost.GetChild(i).gameObject);

            RectTransform portrait = EmberforgeUI.PortraitFrame("Featured", _portraitHost, featured,
                                                                featured.Stars, FeaturedSize);
            UIBuilder.FillParent(portrait, 0f);

            _featuredName.text = featured.Name;
            _featuredName.color = UITheme.StarColor(featured.Stars);
            _featuredTitle.text = featured.Title.ToUpperInvariant() + "   -   "
                                + UITheme.RoleName(featured.Role) + "   -   "
                                + AspectChart.DisplayName(featured.Aspect).ToUpperInvariant();
        }

        /// <summary>
        /// The face of the banner: the highest-star hero WHO HAS BEEN DRAWN, in content order.
        ///
        /// A STAND-IN for a real banner, not one. There is no featured flag in heroes.json and no
        /// banner definition anywhere, and content was out of scope, so this picks a hero rather
        /// than reading one. It is deterministic - same hero every session, same hero for every
        /// player - which is the least misleading thing an undefined banner can be.
        ///
        /// The art check is the whole point of the two passes. Ranking on stars alone selected
        /// Orrin Kaal, who was awaiting art, so the single dominant image this screen is built
        /// around rendered as the procedural placeholder blob. Orrin has since been deleted and
        /// all 94 heroes now declare a profile badge, so the art pass changes nothing today - it
        /// is kept because the next roster addition arrives undrawn like every other one. A
        /// banner face
        /// with no face is worse than a lower-rarity hero, so heroes with art are preferred and
        /// rarity only breaks the tie among them. The fallback still returns SOMETHING if no hero
        /// has art at all, because a screen with no subject is worse than a placeholder one.
        /// </summary>
        private HeroDef FeaturedHero()
        {
            List<HeroDef> heroes = App.Session.Content.Heroes;
            HeroDef drawn = null;
            HeroDef any = null;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroDef h = heroes[i];
                if (any == null || h.Stars > any.Stars) any = h;
                if (!Widgets.HasPortraitArt(h)) continue;
                if (drawn == null || h.Stars > drawn.Stars) drawn = h;
            }
            return drawn != null ? drawn : any;
        }

        private void Refresh()
        {
            long now = TimeUtil.NowUnix();
            _currencies.Refresh(App.Session, now);

            long unit = App.Session.Balance.SummonCost;
            long shards = App.Session.Profile.Shards;

            RefreshPullButton(_pullOne, unit, shards);
            RefreshPullButton(_pullTen, unit * 10L, shards);
            RefreshPity();
            RefreshOdds();
        }

        /// <summary>
        /// The opening guarantee. Once TotalSummons passes it, the bar reads as SPENT and stays
        /// there - it does not reset, because there is nothing to reset to. See the class note.
        /// </summary>
        private void RefreshPity()
        {
            int cap = GameSession.PityGuaranteedSummons;
            int used = Mathf.Min(App.Session.Profile.TotalSummons, cap);
            int left = Mathf.Max(0, cap - App.Session.Profile.TotalSummons);

            EmberforgeUI.SetProgress(_pityFill, cap > 0 ? used / (float)cap : 1f);

            if (left > 0)
            {
                _pityFill.color = Color.white;
                _pityChest.color = UITheme.Gold;
                _pityLabel.color = UITheme.TextMuted;
                _pityLabel.text = "OPENING GUARANTEE  -  " + left
                                + (left == 1 ? " PULL LEFT THAT CANNOT DUPLICATE" : " PULLS LEFT THAT CANNOT DUPLICATE");
            }
            else
            {
                _pityFill.color = UITheme.WithAlpha(Color.white, 0.35f);
                _pityChest.color = UITheme.TextDim;
                _pityLabel.color = UITheme.TextDim;
                _pityLabel.text = "OPENING GUARANTEE SPENT  -  PULLS NOW DRAW FROM THE FULL POOL";
            }
        }

        /// <summary>
        /// The odds, as one line.
        ///
        /// Same rule the old RIFT ODDS card used: when exactly one tier is offered, state the
        /// outcome outright, because a lone "100%" reads as "100% chance of a good pull" rather
        /// than "every pull is 5-star". Those mean the same thing today and only one of them
        /// survives the pool being widened.
        /// </summary>
        private void RefreshOdds()
        {
            BalanceConfig balance = App.Session.Balance;
            float[] w = { balance.SummonWeight1, balance.SummonWeight2, balance.SummonWeight3,
                          balance.SummonWeight4, balance.SummonWeight5 };
            float total = 0f;
            for (int i = 0; i < w.Length; i++) total += w[i];
            if (total <= 0f) total = 1f;

            int tiersOffered = 0;
            int onlyTier = 0;
            for (int s = 1; s <= 5; s++)
                if (w[s - 1] > 0f) { tiersOffered++; onlyTier = s; }

            if (tiersOffered == 1)
            {
                _oddsLabel.text = "RIFT ODDS  -  EVERY PULL IS " + onlyTier + "-STAR";
                return;
            }

            string line = "RIFT ODDS  -  ";
            bool first = true;
            for (int s = 1; s <= 5; s++)
            {
                if (w[s - 1] <= 0f) continue;
                if (!first) line += "   ";
                line += s + "-STAR " + (Mathf.Round(w[s - 1] / total * 1000f) / 10f) + "%";
                first = false;
            }
            _oddsLabel.text = line;
        }

        /// <summary>
        /// Prices one pull button and enables it on that price alone. When it cannot be afforded
        /// the cost line becomes the SHORTFALL rather than the price, which is the number a player
        /// needs and the one they would otherwise have to work out - the same rule the Forge's
        /// disabled actions follow.
        /// </summary>
        private void RefreshPullButton(UIButton button, long cost, long shards)
        {
            bool affordable = shards >= cost;
            button.Interactable = affordable && !_busy;
            if (button.SubLabel != null)
                button.SubLabel.text = affordable
                    ? cost.ToString()
                    : "NEED " + (cost - shards) + " MORE";
        }

        /// <summary>Runs a batch of <paramref name="count"/> pulls. Wired directly to each
        /// button, so a tap is the action rather than a mode change that a later tap acts on.</summary>
        private void DoSummon(int count)
        {
            if (_busy) return;

            long unit = App.Session.Balance.SummonCost;
            long cost = unit * count;
            if (App.Session.Profile.Shards < cost)
            {
                App.Notify("You need " + cost + " Ember Shards for " + count + " pulls.");
                return;
            }

            // Busy without a Refresh: a Refresh here would swap the pull buttons to their disabled
            // art, and the pressed one has to stay visibly sunk through the press beat. The dim
            // blocks every other tap until the sequence hands back.
            _busy = true;

            // The batch is a LOOP over the existing single Summon, deliberately, and not a new
            // multi-pull path in GameSession. Everything that makes a pull what it is - the weight
            // table, the first-three unowned guarantee, duplicate conversion, the event activity
            // record - already lives in there and is driven off Profile.TotalSummons, which each
            // call advances. Looping gets all of it for free and keeps the odds this change was
            // told not to touch literally untouched. It also means pull 4 of a batch sees the state
            // pulls 1-3 left behind, which is what the pity rule expects.
            long now = TimeUtil.NowUnix();
            List<SummonOutcome> outcomes = new List<SummonOutcome>();
            for (int i = 0; i < count; i++)
            {
                // Same seed formula as the single pull, plus the index. TotalSummons and Shards
                // both move between iterations so the seed would vary anyway; i is there so two
                // pulls cannot collide if a duplicate refund happens to restore the exact shard
                // count a previous pull started from.
                int seed = App.Session.Profile.TotalSummons * 7919
                           + (int)(App.Session.Profile.Shards & 0xFFFF)
                           + Mathf.RoundToInt(Time.unscaledTime * 1000f)
                           + i * 104729;

                SummonOutcome outcome = App.Session.Summon(seed, now);
                if (!outcome.Success)
                {
                    // Affordability was checked for the whole batch above, so this is a content
                    // fault rather than a purse that ran dry. Keep whatever did land instead of
                    // discarding it, and say what went wrong.
                    App.Notify(outcome.Message);
                    break;
                }
                outcomes.Add(outcome);
            }

            App.RefreshHud();
            App.RequestSave();

            if (outcomes.Count == 0)
            {
                _busy = false;
                Refresh();
                return;
            }

            // One path for one pull and for ten. The sequence is a player for what has already
            // been rolled and banked above: nothing it does can change a result.
            _sequence.Play(outcomes, SummonSettings.Current, count == 1 ? _pullOne : _pullTen, false,
                           OnSequenceDone);
        }

        public override void OnHide()
        {
            // Everything is already banked, so leaving mid-summon loses nothing but the show.
            if (_sequence != null) _sequence.Abort();
            _busy = false;
#if UNITY_EDITOR
            _previewing = false;
#endif
        }

        /// <summary>
        /// Back to the Rift, not away from it.
        ///
        /// This used to push HeroDetail for the hero just pulled, on the reasoning that the pull's
        /// meaning is the choice it forces. The cost of that was the summon loop: a player who
        /// wanted a second pull was three taps and two screens from it, and one who pulled a
        /// duplicate got sent to a hero page they already knew about. Staying here also means the
        /// shard counter they are spending stays on screen the whole time.
        /// </summary>
        private void OnSequenceDone()
        {
            App.Audio.Tap();
            _busy = false;
#if UNITY_EDITOR
            _previewing = false;
#endif
            App.RefreshHud();
            Refresh();
        }

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY. Plays the summon for heroes of the given tiers WITHOUT ROLLING: no
        /// GameSession.Summon call, no shards spent, no hero granted, nothing saved. The outcomes
        /// are built here and marked, and the sequence prints EDITOR PREVIEW over itself.
        ///
        /// It exists because the live weights make every pull a 5-star, so the 3-star and 4-star
        /// paths cannot otherwise be seen at all, and the 5-star path only by spending shards. It
        /// does not force the ROLL, deliberately: a forced roll would be a change to the summon
        /// logic, and this pass is presentation only.
        ///
        /// Compiled out of a player build entirely by the UNITY_EDITOR guard. Reached from the
        /// cheat panel and from SummonSequenceShots.
        /// </summary>
        public void DebugPreview(int[] tiers)
        {
            if (tiers == null || tiers.Length == 0) return;
            // A preview may replace one already running; a real summon mid-show is left alone.
            if (_busy && !_previewing) return;
            _sequence.Abort();
            List<SummonOutcome> outcomes = new List<SummonOutcome>();
            Dictionary<int, int> taken = new Dictionary<int, int>();
            for (int i = 0; i < tiers.Length; i++)
            {
                int used;
                taken.TryGetValue(tiers[i], out used);
                HeroDef hero = PreviewHero(tiers[i], used);
                taken[tiers[i]] = used + 1;
                if (hero == null) continue;

                SummonOutcome outcome = new SummonOutcome();
                outcome.Success = true;
                outcome.Hero = hero;
                // Alternating new and duplicate, so both treatments show in a preview batch.
                outcome.IsNew = i % 3 != 2;
                if (!outcome.IsNew)
                {
                    outcome.GoldRefund = App.Session.Balance.DuplicateGold;
                    outcome.ShardRefund = App.Session.Balance.DuplicateShards;
                }
                outcomes.Add(outcome);
            }
            if (outcomes.Count == 0) return;

            _busy = true;
            _previewing = true;
            _sequence.Play(outcomes, SummonSettings.Current, tiers.Length == 1 ? _pullOne : _pullTen, true,
                           OnSequenceDone);
        }

        private bool _previewing;

        /// <summary>The nth drawn hero of a tier in content order, so a preview is the same heroes
        /// every time. Falls back to an undrawn one only if the tier has no art at all.</summary>
        private HeroDef PreviewHero(int stars, int nth)
        {
            List<HeroDef> drawn = new List<HeroDef>();
            List<HeroDef> any = new List<HeroDef>();
            List<HeroDef> heroes = App.Session.Content.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i].Stars != stars) continue;
                any.Add(heroes[i]);
                if (Widgets.HasPortraitArt(heroes[i])) drawn.Add(heroes[i]);
            }
            List<HeroDef> pool = drawn.Count > 0 ? drawn : any;
            return pool.Count > 0 ? pool[nth % pool.Count] : null;
        }

        /// <summary>The live sequence, for the capture harness to freeze on a beat.</summary>
        public SummonSequence DebugSequence { get { return _sequence; } }
#endif
    }
}
