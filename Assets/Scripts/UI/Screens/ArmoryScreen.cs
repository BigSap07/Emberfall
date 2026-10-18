using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Armory (DESIGN-SINKS.md): behind the Proving, sells armor presets, Brands, Echoes and a
    /// hero copy for Valor. Everything it does is SinkShopScreen's; this says which shop it is.
    /// </summary>
    public sealed class ArmoryScreen : SinkShopScreen
    {
        public override ScreenId Id { get { return ScreenId.Armory; } }

        protected override SinkShopId Shop { get { return SinkShopId.Armory; } }
        protected override string Title { get { return "THE ARMORY"; } }
        protected override string PurseLabel { get { return "VALOR"; } }
        protected override Sprite PurseIcon { get { return TextureLab.Sigil(0, 64); } }
        protected override Color PurseTint { get { return UITheme.Gold; } }
        protected override string EarnedFrom { get { return "EARNED IN THE PROVING"; } }
    }
}
