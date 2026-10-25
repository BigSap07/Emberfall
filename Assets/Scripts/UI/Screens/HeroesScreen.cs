using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The collection. Every hero in the game, owned or not - owned ones sorted strongest first, locked ones after.
    /// R57: while HeroesA2Switch is on this is drawn by HeroesScreenA2 (the other half of this partial class);
    /// the build below is the screen as it was and is what EMBERFALL_HEROES_A2_OFF gives back.
    /// </summary>
    public sealed partial class HeroesScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Heroes; } }

        private RectTransform _grid;
        private Text _countLabel;
        private Text _emptyLabel;

        /// <summary>
        /// THE GRID CELL, and the one number the cards and this screen both have to agree about.
        ///
        /// Three columns in landscape. The cell is as tall as Widgets.CardHeight rather than as
        /// tall as a guess: a grid whose cell is shorter than its card clips the bottom off every
        /// one of them, and that is a fault nothing but a capture catches.
        /// </summary>
        private const float CellWidth = 470f;
        private const float GridGap = 24f;

        protected override void Build()
        {
            if (HeroesA2Switch.Enabled) { BuildA2(); return; }

            // The page's own ground. The shared backdrop is the near-black violet every
            // unconverted screen sits on, and a grid of warm brown cards on it reads as two
            // different pieces of software. One Image, on one screen, rather than changing the
            // global colour and restyling twenty pages that have not had a visual pass.
            Image ground = UIBuilder.Img("Ground", Root, TextureLab.White, Emberforge.PanelLow);
            UIBuilder.FillParent(ground.rectTransform, 0f);
            ground.raycastTarget = false;

            ScreenChrome.ForgedHeader(Root, "YOUR ALLIES", Router, out _countLabel);

            ScrollRect scroll;
            _grid = UIBuilder.ScrollGrid("Collection", Root, out scroll,
                                         new Vector2(CellWidth, Widgets.CardHeight),
                                         new Vector2(GridGap, GridGap), 3,
                                         new RectOffset(0, 0, 8, 48));
            RectTransform scrollRoot = scroll.GetComponent<RectTransform>();
            UIBuilder.FillParent(scrollRoot, UITheme.ScreenPadding,
                                 UITheme.TopBarHeight + 130f, UITheme.ScreenPadding, 0f);

            _emptyLabel = EmberforgeUI.Label("Empty", Root,
                                             "No heroes yet - visit the Rift to summon one.",
                                             UITheme.FontBody, Emberforge.TextQuiet,
                                             TextAnchor.MiddleCenter);
            UIBuilder.Box(_emptyLabel.rectTransform, 0f, 0f, 800f, 60f, new Vector2(0.5f, 0.5f));
            _emptyLabel.gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            if (_a2) { App.RefreshHud(); OnShowA2(); return; }
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);

            List<HeroInstance> owned = new List<HeroInstance>(App.Session.Profile.Heroes);
            owned.Sort(CompareHeroes);

            int cardIndex = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                HeroInstance hero = owned[i];
                HeroInstance captured = hero;
                Widgets.HeroCard("Card" + cardIndex++, _grid, hero, App.Session.Profile.IsDeployed(hero.Uid),
                                 delegate { OpenDetail(captured); });
            }

            // this is a collection screen - every hero in the game appears, so the player can see
            // what they're missing, not just what they have
            List<HeroDef> locked = new List<HeroDef>();
            List<HeroDef> allHeroes = App.Session.Content.Heroes;
            for (int i = 0; i < allHeroes.Count; i++)
                if (!App.Session.Profile.Owns(allHeroes[i].Id)) locked.Add(allHeroes[i]);
            locked.Sort(CompareLockedHeroes);

            for (int i = 0; i < locked.Count; i++)
            {
                HeroDef def = locked[i];
                HeroDef captured = def;
                Widgets.LockedHeroCard("Card" + cardIndex++, _grid, def, delegate { OpenLocked(captured); });
            }

            _countLabel.text = owned.Count + " OWNED  /  " + App.Session.Content.Heroes.Count + " DISCOVERED";
            _emptyLabel.gameObject.SetActive(owned.Count == 0 && locked.Count == 0);
        }

        private int CompareHeroes(HeroInstance a, HeroInstance b)
        {
            bool aDeployed = App.Session.Profile.IsDeployed(a.Uid);
            bool bDeployed = App.Session.Profile.IsDeployed(b.Uid);
            if (aDeployed != bDeployed) return aDeployed ? -1 : 1;
            int byPower = b.Power.CompareTo(a.Power);
            if (byPower != 0) return byPower;
            return string.CompareOrdinal(a.DefId, b.DefId);
        }

        private static int CompareLockedHeroes(HeroDef a, HeroDef b)
        {
            int byStars = b.Stars.CompareTo(a.Stars); // descending: higher star first
            if (byStars != 0) return byStars;
            return string.CompareOrdinal(a.Id, b.Id);
        }

        private void OpenDetail(HeroInstance hero)
        {
            App.Audio.Tap();
            Router.HeroDetail.SetHero(hero);
            Router.Show(ScreenId.HeroDetail);
        }

        private void OpenLocked(HeroDef def)
        {
            App.Audio.Tap();
            Router.HeroDetail.SetLockedHero(def);
            Router.Show(ScreenId.HeroDetail);
        }
    }
}
