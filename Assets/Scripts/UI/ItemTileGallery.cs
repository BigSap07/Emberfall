using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;

namespace Emberfall.UI
{
    /// <summary>
    /// ONE TILE PER TIER, in a row (R47). A reference sheet for the capture tooling and for anyone checking the
    /// rarity palette by eye: the four armor tempers side by side at a large and a small size, a worn tile, an
    /// empty slot, and the two relic grades. It is drawn only by the ItemTiers capture target; nothing in the
    /// game opens it.
    /// </summary>
    public static class ItemTileGallery
    {
        private const float Big = 150f;
        private const float Small = 76f;
        private const float Gap = 28f;

        public static RectTransform Build(RectTransform parent, IList<RelicDef> relics)
        {
            RectTransform sheet = UIBuilder.Node("ItemTileGallery", parent);
            UIBuilder.FillParent(sheet, 0f);
            Image back = sheet.gameObject.AddComponent<Image>();
            back.color = Emberforge.PanelLow;

            Text title = EmberforgeUI.Label("Title", sheet, "ITEM TILE: ONE PER TIER", UITheme.FontBody, Emberforge.TextGold,
                                            TextAnchor.MiddleCenter);
            UIBuilder.Box(title.rectTransform, 0f, -40f, 900f, 40f, new Vector2(0.5f, 1f));

            ArmorSlot[] rowSlots = { ArmorSlot.Weapon, ArmorSlot.Plate };
            Aspect[] aspects = { Aspect.Ember, Aspect.Tide, Aspect.Verdant, Aspect.Gale };
            float y = -110f;
            for (int r = 0; r < rowSlots.Length; r++)
            {
                for (int t = 0; t < ItemTilePalette.ArmorTiers; t++)
                {
                    RectTransform host = Cell(sheet, t, 4, Big, y);
                    ItemTile.ForSlot("Tile", host, Big, rowSlots[r], (ArmorTemper)t, aspects[(t + r) % aspects.Length],
                                     r == 1 && t == 2);
                    Text name = EmberforgeUI.Label("Name", host, ItemTilePalette.ArmorName(t).ToUpperInvariant(),
                                                   UITheme.FontTiny, UITheme.ArmorTemperColor((ArmorTemper)t), TextAnchor.UpperCenter);
                    UIBuilder.Box(name.rectTransform, 0f, -Big - 8f, Big + Gap, 26f, new Vector2(0.5f, 1f));
                }
                y -= Big + 90f;
            }

            for (int t = 0; t < ItemTilePalette.ArmorTiers; t++)
                ItemTile.ForSlot("Small" + t, Cell(sheet, t, 7, Small, y), Small, ArmorSlot.Helm, (ArmorTemper)t,
                                 aspects[t % aspects.Length], false);
            ItemTile.Vacant("Vacant", Cell(sheet, 4, 7, Small, y), Small, ArmorSlot.Boots);

            RelicDef legendary = First(relics, RelicGrade.Legendary);
            RelicDef mythic = First(relics, RelicGrade.Mythic);
            if (legendary != null) ItemTile.ForRelic("Legendary", Cell(sheet, 5, 7, Small, y), Small, legendary, 2, 3, false);
            if (mythic != null) ItemTile.ForRelic("Mythic", Cell(sheet, 6, 7, Small, y), Small, mythic, 3, 3, false);
            return sheet;
        }

        private static RectTransform Cell(RectTransform sheet, int index, int count, float size, float y)
        {
            float total = count * size + (count - 1) * Gap;
            RectTransform host = UIBuilder.Node("Cell" + index, sheet);
            UIBuilder.Box(host, -total * 0.5f + size * 0.5f + index * (size + Gap), y - size * 0.5f, size, size,
                          new Vector2(0.5f, 1f));
            return host;
        }

        private static RelicDef First(IList<RelicDef> relics, RelicGrade grade)
        {
            if (relics == null) return null;
            for (int i = 0; i < relics.Count; i++)
                if (relics[i].Grade == grade) return relics[i];
            return null;
        }
    }
}
