using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Model;

namespace Emberfall.UI.Screens
{
    /// <summary>The collection. Every owned hero, sorted strongest first.</summary>
    public sealed class HeroesScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Heroes; } }

        private RectTransform _grid;
        private Text _countLabel;
        private Text _emptyLabel;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "YOUR ALLIES", Router, out _countLabel);

            ScrollRect scroll;
            _grid = UIBuilder.ScrollGrid("Collection", Root, out scroll,
                                         new Vector2(470f, 300f), new Vector2(20f, 20f), 2,
                                         new RectOffset(0, 0, 8, 200));
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

            List<HeroInstance> heroes = new List<HeroInstance>(App.Session.Profile.Heroes);
            heroes.Sort(CompareHeroes);

            for (int i = 0; i < heroes.Count; i++)
            {
                HeroInstance hero = heroes[i];
                HeroInstance captured = hero;
                Widgets.HeroCard("Card" + i, _grid, hero, App.Session.Profile.IsDeployed(hero.Uid),
                                 delegate { OpenDetail(captured); });
            }

            _countLabel.text = heroes.Count + " OWNED  /  " + App.Session.Content.Heroes.Count + " DISCOVERED";
            _emptyLabel.gameObject.SetActive(heroes.Count == 0);
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

        private void OpenDetail(HeroInstance hero)
        {
            App.Audio.Tap();
            Router.HeroDetail.SetHero(hero);
            Router.Show(ScreenId.HeroDetail);
        }
    }
}
