using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.UI;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE ITEM TILE (R47): the rarity colour language, proven from the same ItemTilePalette the tile draws with.
    /// One named check per rule: the tier counts are the real ones (the enum and armor.json), every tier has its
    /// own plate and its own rim, the ladder reads in greyscale (luminance, rim weight and lit pips all rise with the
    /// tier), the silhouette keeps its contrast on every plate, the top tier alone carries the gem, and the wiring
    /// is where the design says (ItemCard forwards to ItemTile, the theme reads the palette).
    ///
    /// Presentation only: nothing here reads a save, runs a fight or touches content.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckItemTile(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("THE ITEM TILE (R47: one tile, one rarity palette, readable without colour)");
            string scriptsDir = Path.GetFullPath(Path.Combine(contentDir, "..", "..", "Scripts"));

            CheckItemTileCounts(contentDir);
            CheckItemTileDistinct();
            CheckItemTileGreyscale();
            CheckItemTileContrast();
            CheckItemTileTopOnly();
            CheckItemTileWiring(scriptsDir);
        }

        private static void CheckItemTileCounts(string contentDir)
        {
            int enumTiers = Enum.GetNames(typeof(ArmorTemper)).Length;
            Check(enumTiers == ItemTilePalette.ArmorTiers,
                  "ITEM TILE: the palette has " + ItemTilePalette.ArmorTiers + " armor tiers and ArmorTemper has " + enumTiers);

            string armorJson = File.ReadAllText(Path.Combine(contentDir, "armor.json"));
            int start = armorJson.IndexOf("\"tempers\"", StringComparison.Ordinal);
            int end = start < 0 ? -1 : armorJson.IndexOf("]", start, StringComparison.Ordinal);
            int json = start < 0 || end < 0 ? -1 : Regex.Matches(armorJson.Substring(start, end - start), "\"temper\"\\s*:").Count;
            Check(json == ItemTilePalette.ArmorTiers,
                  "ITEM TILE: armor.json lists " + json + " tempers, the palette draws " + ItemTilePalette.ArmorTiers);

            int grades = Enum.GetNames(typeof(RelicGrade)).Length;
            Check(grades == ItemTilePalette.RelicTiers,
                  "ITEM TILE: the palette has " + ItemTilePalette.RelicTiers + " relic grades and RelicGrade has " + grades);
        }

        /// <summary>Every tier its own plate and its own rim, far enough apart to read as different colours.</summary>
        private static void CheckItemTileDistinct()
        {
            const double floor = 40.0;
            double minPlate = 1000.0;
            double minRim = 1000.0;
            string namesOk = "";
            for (int a = 0; a < ItemTilePalette.ArmorTiers; a++)
            {
                for (int b = a + 1; b < ItemTilePalette.ArmorTiers; b++)
                {
                    minPlate = Math.Min(minPlate, ItemTilePalette.Distance(ItemTilePalette.ArmorPlate(a), ItemTilePalette.ArmorPlate(b)));
                    minRim = Math.Min(minRim, ItemTilePalette.Distance(ItemTilePalette.ArmorRim(a), ItemTilePalette.ArmorRim(b)));
                }
                namesOk += ItemTilePalette.ArmorName(a) + (a < ItemTilePalette.ArmorTiers - 1 ? ", " : "");
            }
            Check(minPlate >= floor, "ITEM TILE: every armor tier has its own plate colour, closest pair " + F0d(minPlate) + " apart (floor " + F0d(floor) + ")");
            Check(minRim >= floor, "ITEM TILE: every armor tier has its own rim colour, closest pair " + F0d(minRim) + " apart (floor " + F0d(floor) + ")");
            Check(namesOk == "Rough, Honed, Gilded, Radiant", "ITEM TILE: the tier names are Rough, Honed, Gilded, Radiant in order (got " + namesOk + ")");

            double relics = ItemTilePalette.Distance(ItemTilePalette.RelicPlate(0), ItemTilePalette.RelicPlate(1));
            double relicRims = ItemTilePalette.Distance(ItemTilePalette.RelicRim(0), ItemTilePalette.RelicRim(1));
            Check(relics >= floor && relicRims >= floor, "ITEM TILE: the two relic grades have their own plate and rim (" + F0d(relics) + ", " + F0d(relicRims) + ")");
            Check(ItemTilePalette.Distance(ItemTilePalette.DimPlate, ItemTilePalette.ArmorPlate(0)) > 20.0
                  && ItemTilePalette.Luminance(ItemTilePalette.DimPlate) < ItemTilePalette.Luminance(ItemTilePalette.ArmorPlate(0)),
                  "ITEM TILE: a locked or empty slot is darker than the lowest tier, so it never reads as an item");
        }

        /// <summary>The ladder must rank without colour: plate luminance, plate grey, rim luminance, rim weight and
        /// lit pips each rise with every step, and the saturation of the plate rises too.</summary>
        private static void CheckItemTileGreyscale()
        {
            bool lum = true, grey = true, rimLum = true, frame = true, marks = true, sat = true;
            for (int t = 1; t < ItemTilePalette.ArmorTiers; t++)
            {
                lum &= ItemTilePalette.Luminance(ItemTilePalette.ArmorPlate(t)) > ItemTilePalette.Luminance(ItemTilePalette.ArmorPlate(t - 1));
                grey &= Grey(ItemTilePalette.ArmorPlate(t)) > Grey(ItemTilePalette.ArmorPlate(t - 1));
                rimLum &= ItemTilePalette.Luminance(ItemTilePalette.ArmorRim(t)) > ItemTilePalette.Luminance(ItemTilePalette.ArmorRim(t - 1));
                frame &= ItemTilePalette.ArmorFrame(t) > ItemTilePalette.ArmorFrame(t - 1);
                marks &= ItemTilePalette.ArmorMarks(t) > ItemTilePalette.ArmorMarks(t - 1);
                sat &= Saturation(ItemTilePalette.ArmorPlate(t)) > Saturation(ItemTilePalette.ArmorPlate(t - 1));
            }
            Check(lum, "ITEM TILE: plate luminance rises with every tier (greyscale order Rough < Honed < Gilded < Radiant)");
            Check(grey, "ITEM TILE: plate grey (Rec.601 luma) rises with every tier");
            Check(rimLum, "ITEM TILE: rim luminance rises with every tier");
            Check(frame, "ITEM TILE: the rim gets thicker with every tier (" + ItemTilePalette.ArmorFrame(0) + " to " + ItemTilePalette.ArmorFrame(ItemTilePalette.ArmorTiers - 1) + ")");
            Check(marks, "ITEM TILE: one more pip is lit with every tier (" + ItemTilePalette.ArmorMarks(0) + " to " + ItemTilePalette.ArmorMarks(ItemTilePalette.ArmorTiers - 1) + ")");
            Check(sat, "ITEM TILE: plate saturation rises with every tier (quiet at Rough, strongest at Radiant)");
            Check(ItemTilePalette.ArmorMarks(ItemTilePalette.ArmorTiers - 1) == ItemTilePalette.ArmorTiers,
                  "ITEM TILE: the top tier lights every pip, a tile never needs more than " + ItemTilePalette.ArmorTiers + " pips");
            bool relicOrder = ItemTilePalette.RelicFrame(1) > ItemTilePalette.RelicFrame(0)
                              && ItemTilePalette.Luminance(ItemTilePalette.RelicPlate(1)) != ItemTilePalette.Luminance(ItemTilePalette.RelicPlate(0))
                              && ItemTilePalette.RelicHasGem(1) && !ItemTilePalette.RelicHasGem(0);
            Check(relicOrder, "ITEM TILE: Mythic is told from Legendary without colour too (thicker rim, corner gem)");
        }

        /// <summary>The light neutral silhouette has to read on every plate, and the dim slot must keep its
        /// own faint mark readable but quiet.</summary>
        private static void CheckItemTileContrast()
        {
            double worst = 100.0;
            for (int t = 0; t < ItemTilePalette.ArmorTiers; t++)
                worst = Math.Min(worst, ItemTilePalette.Contrast(ItemTilePalette.Silhouette, ItemTilePalette.ArmorPlate(t)));
            for (int g = 0; g < ItemTilePalette.RelicTiers; g++)
                worst = Math.Min(worst, ItemTilePalette.Contrast(ItemTilePalette.Silhouette, ItemTilePalette.RelicPlate(g)));
            Check(worst >= 4.5, "ITEM TILE: the silhouette keeps a contrast ratio of at least 4.5 on every plate (worst " + F1d(worst) + ")");

            double rimOnPlate = 100.0;
            for (int t = 0; t < ItemTilePalette.ArmorTiers; t++)
                rimOnPlate = Math.Min(rimOnPlate, ItemTilePalette.Contrast(ItemTilePalette.ArmorRim(t), ItemTilePalette.ArmorPlate(t)));
            Check(rimOnPlate >= 1.8, "ITEM TILE: every rim separates from its own plate (worst " + F1d(rimOnPlate) + ")");

            double textOnDark = 100.0;
            for (int t = 0; t < ItemTilePalette.ArmorTiers; t++)
                textOnDark = Math.Min(textOnDark, ItemTilePalette.Contrast(ItemTilePalette.ArmorRim(t), ItemTilePalette.Outline));
            Check(textOnDark >= 3.0, "ITEM TILE: the tier colour reads as a line of text on the dark chrome (worst " + F1d(textOnDark) + ")");
        }

        /// <summary>The glow ring and corner gem belong to the top tier only.</summary>
        private static void CheckItemTileTopOnly()
        {
            bool onlyTop = true;
            for (int t = 0; t < ItemTilePalette.ArmorTiers; t++)
                onlyTop &= ItemTilePalette.ArmorHasGem(t) == (t == ItemTilePalette.ArmorTiers - 1);
            Check(onlyTop, "ITEM TILE: only the top armor tier carries the gem and glow ring");
            Check(ItemTilePalette.ArmorName(-5) == "Rough" && ItemTilePalette.ArmorName(99) == "Radiant"
                  && ItemTilePalette.ArmorPlate(99) == ItemTilePalette.ArmorPlate(ItemTilePalette.ArmorTiers - 1),
                  "ITEM TILE: an out-of-range tier is clamped, never thrown");
        }

        private static void CheckItemTileWiring(string scriptsDir)
        {
            string ui = Path.Combine(scriptsDir, "UI");
            string card = File.ReadAllText(Path.Combine(ui, "ItemCard.cs"));
            string tile = File.ReadAllText(Path.Combine(ui, "ItemTile.cs"));
            string theme = File.ReadAllText(Path.Combine(ui, "UITheme.cs"));

            Check(card.Contains("ItemTile.ForArmor") && card.Contains("ItemTile.ForSlot") && card.Contains("ItemTile.ForRelic")
                  && card.Contains("ItemTile.Vacant"),
                  "ITEM TILE: ItemCard forwards armor, slot, relic and vacant to ItemTile (one drawing)");
            Check(!card.Contains("NotchedRect") && !card.Contains("BuildPips"),
                  "ITEM TILE: ItemCard no longer draws a plate or pips of its own");
            Check(theme.Contains("ItemTilePalette.ArmorRim"),
                  "ITEM TILE: UITheme.ArmorTemperColor reads the palette, so a tier is one colour on a tile and on a text row");
            Check(tile.Contains("UiKit.SetGlyph") && tile.Contains("UiKit.CheckImage") && tile.Contains("ItemCard.SlotIcon"),
                  "ITEM TILE: the tile reaches the kit only through UiKit (null on a miss, so the old drawing is the fallback)");

            // The plate colours live in ONE place: no hex literal for a tier in the tile or in the theme's tier function.
            Check(!Regex.IsMatch(tile, "Hex\\(\"[0-9A-Fa-f]{6}\"\\)"),
                  "ITEM TILE: the tile defines no colour of its own, the palette is the only table");

            // R47 phase 2: every screen that draws an item goes through the tile; the old odd icon paths are gone.
            string screens = Path.Combine(ui, "Screens");
            string qm = File.ReadAllText(Path.Combine(screens, "QuartermasterScreen.cs"));
            string sink = File.ReadAllText(Path.Combine(screens, "SinkShopScreen.cs"));
            string relicShop = File.ReadAllText(Path.Combine(screens, "RelicShopScreen.cs"));
            string battle = File.ReadAllText(Path.Combine(screens, "BattleScreen.cs"));
            string inv = File.ReadAllText(Path.Combine(screens, "InventoryScreen.cs"));
            Check(qm.Contains("ItemTile.ForSlot") && !qm.Contains("UiKit.SetGlyph"),
                  "ITEM TILE: the Quartermaster offers draw the shared tile, not an aspect glyph");
            Check(sink.Contains("ItemTile.ForSlot") && !sink.Contains("UiKit.SetGlyph"),
                  "ITEM TILE: the Armory and Archive armor rows draw the shared tile, not an aspect glyph");
            Check(relicShop.Contains("ItemTile.ForRelic") && !relicShop.Contains("SigilFor"),
                  "ITEM TILE: the relic shop draws the shared relic tile");
            Check(battle.Contains("ItemTile.ForArmor"),
                  "ITEM TILE: the battle debrief armor drop draws the shared tile");
            Check(inv.Contains("ItemTile.ForArmor") && inv.Contains("ItemTile.ForRelic") && !inv.Contains("MarkWorn"),
                  "ITEM TILE: the Inventory worn mark is the tile's own, in its fixed spot");
        }

        private static double Grey(int rgb)
        {
            return 0.299 * ((rgb >> 16) & 255) + 0.587 * ((rgb >> 8) & 255) + 0.114 * (rgb & 255);
        }

        private static double Saturation(int rgb)
        {
            double r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            return max <= 0.0 ? 0.0 : (max - min) / max;
        }

        private static string F0d(double v) { return v.ToString("0", CultureInfo.InvariantCulture); }
        private static string F1d(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
    }
}
