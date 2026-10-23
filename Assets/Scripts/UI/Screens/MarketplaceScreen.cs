using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Progression;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Marketplace (R26): the Hub's market stall, an overview of the four shops (Relic Shop, Quartermaster,
    /// Proving Armory, Recital Archive) as the doors ShopDoors draws for the Store's SHOPS tab. It sells
    /// nothing itself and decides nothing: each card asks the gate with its own door id, so the stall opens at
    /// 2-4 and the Relic Shop card inside stays locked until 3-1.
    /// </summary>
    public sealed class MarketplaceScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Marketplace; } }

        private Text _headerSubtitle;
        private RectTransform _body;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "MARKETPLACE", Router, out _headerSubtitle);
            _body = UIBuilder.Node("Body", Root);
            UIBuilder.FillParent(_body, 0f);
        }

        public override void OnShow()
        {
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _body.childCount - 1; i >= 0; i--) Object.Destroy(_body.GetChild(i).gameObject);
            int open = ShopDoors.Build(App, Router, _body, -(HeaderBottom));
            _headerSubtitle.text = MarketplaceShops.CountLine(open).ToUpperInvariant();
        }

        /// <summary>Below the header and the shared top bar, which this screen keeps.</summary>
        private static float HeaderBottom { get { return ScreenChrome.HeaderTop + 108f + Emberforge.Space16; } }
    }
}
