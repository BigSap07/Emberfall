using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI.Screens
{
    /// <summary>The collection. Every hero in the game, owned or not - owned ones sorted strongest first, locked ones after.</summary>
    public sealed class HeroesScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Heroes; } }

        private RectTransform _grid;
        private Text _countLabel;
        private Text _emptyLabel;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "YOUR ALLIES", Router, out _countLabel);

            // three columns in landscape (3 x 470 + 2 x 20 = 1450 across 1856 usable); the grid
            // centres the block. Was two columns in portrait.
            ScrollRect scroll;
            _grid = UIBuilder.ScrollGrid("Collection", Root, out scroll,
                                         new Vector2(470f, 300f), new Vector2(20f, 20f), 3,
                                         new RectOffset(0, 0, 8, 48));
            RectTransform scrollRoot = scroll.GetComponent<RectTransform>();
            UIBuilder.FillParent(scrollRoot, UITheme.ScreenPadding, UITheme.TopBarHeight + 130f, UITheme.ScreenPadding, 0f);

            _emptyLabel = UIBuilder.Label("Empty", Root, "No heroes yet - visit the Rift to summon one.",
                                          UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleCenter);
            UIBuilder.Box(_emptyLabel.rectTransform, 0f, 0f, 800f, 60f, new Vector2(0.5f, 0.5f));
            _emptyLabel.gameObject.SetActive(false);
        }

        public override void OnShow()
        {
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
            int byRarity = a.Rarity.CompareTo(b.Rarity);
            if (byRarity != 0) return byRarity;
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
