using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Archive (DESIGN-SINKS.md): behind the Deep Recital, sells a hero copy for Coda.
    /// Everything it does is SinkShopScreen's; this says which shop it is.
    /// </summary>
    public sealed class ArchiveScreen : SinkShopScreen
    {
        public override ScreenId Id { get { return ScreenId.Archive; } }

        protected override SinkShopId Shop { get { return SinkShopId.Archive; } }
        protected override string Title { get { return "THE ARCHIVE"; } }
        protected override string PurseLabel { get { return "CODA"; } }
        protected override Sprite PurseIcon { get { return TextureLab.GemIcon(64); } }
        protected override Color PurseTint { get { return UITheme.Coda; } }
        protected override string EarnedFrom { get { return "EARNED IN THE DEEP RECITAL"; } }
    }
}
