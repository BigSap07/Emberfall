using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R26 UI checks: the uiKit default and its rollback, the Marketplace and its gating, the kit polish files,
    /// and the Hero screen level cap words. Presentation only: nothing here reads a real save, runs a fight
    /// or moves shared content, and the kit switch is put back as it was found.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR26Ui(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("R26 UI (uiKit default, Marketplace, kit polish, hero cap)");
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            bool was = UiKitSwitch.Enabled;
            try
            {
                CheckR26UiKitDefault(root);
                CheckR26Marketplace(db, root);
                CheckR26KitPolish(root);
                CheckR26HeroCap(db, root);
                CheckR26Round2(root);
                CheckR26Round3(root);
            }
            finally
            {
                UiKitSwitch.Enabled = was;
            }
            Check(UiKitSwitch.Enabled == was, "R26 UI: the kit switch is put back as the run found it");
        }

        private static string R26Read(string root, params string[] parts)
        {
            string[] all = new string[parts.Length + 1];
            all[0] = root;
            Array.Copy(parts, 0, all, 1, parts.Length);
            string path = Path.Combine(all);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        // ------------------------------------------------------------------ item 1: uiKit on by default

        private static void CheckR26UiKitDefault(string root)
        {
            string sw = R26Read(root, "Assets", "Scripts", "Core", "Session", "UiKitSwitch.cs");
            Check(UiKitSwitch.ShippedDefault && sw.Contains("#if EMBERFALL_UIKIT_OFF") && !Regex.IsMatch(sw, @"#if EMBERFALL_UIKIT\s*[\r\n]"),
                  "uiKit DEFAULT: this compile has no define, so ShippedDefault is true; the only define is the opt-out EMBERFALL_UIKIT_OFF");
            UiKitSwitch.Enabled = UiKitSwitch.ShippedDefault;
            Check(UiKitMap.ForgeSlot("forge_panel") != null && UiKitMap.HudSlot("hud_chest") != null,
                  "uiKit DEFAULT: with the shipped default the kit answers pieces for the panel and the chest");
            UiKitSwitch.Enabled = false;
            Check(UiKitMap.ForgeSlot("forge_panel") == null && UiKitMap.HudSlot("hud_chest") == null && UiKitMap.PassHeader() == null,
                  "uiKit ROLLBACK: with the field off every lookup answers null again, the old look");
            string shot = R26Read(root, "Assets", "Editor", "PlayModeScreenshot.cs");
            Check(shot.Contains("SessionState.GetBool(KitWasKey, UiKitSwitch.ShippedDefault)") && !shot.Contains("GetBool(KitWasKey, false)"),
                  "uiKit DEFAULT: the capture tool restores to UiKitSwitch.ShippedDefault, not to a literal");
            string bc = R26Read(root, "Tools", "build-check.ps1");
            Check(bc.Contains("[define-probe]") && bc.Contains("EMBERFALL_UIKIT_OFF;EMBERFALL_SKIN_A2_OFF;EMBERFALL_TESTSTORE") && bc.Contains("UiKitSwitch.Enabled=True"),
                  "uiKit DEFAULT: build-check runs the define probe once with no defines and once with the opt-out and test defines");
            string rsp = R26Read(root, "Assets", "csc.rsp");
            Check(!rsp.Contains("EMBERFALL_UIKIT"), "uiKit DEFAULT: Assets/csc.rsp sets no kit define, so a normal build ships the kit");
        }

        // ------------------------------------------------------------------ item 2: Marketplace

        private static void CheckR26Marketplace(ContentDatabase db, string root)
        {
            string[] names = new string[MarketplaceShops.All.Length];
            for (int i = 0; i < names.Length; i++) names[i] = MarketplaceShops.All[i].Name;
            Check(string.Join("|", names) == "RELIC SHOP|QUARTERMASTER|PROVING ARMORY|RECITAL ARCHIVE",
                  "Marketplace ORDER: Relic Shop, Quartermaster, Proving Armory, Recital Archive");
            Check(MarketplaceShops.CountLine(0) == "0 of 4 open" && MarketplaceShops.CountLine(3) == "3 of 4 open" && MarketplaceShops.CountLine(4) == "4 of 4 open",
                  "Marketplace COUNT: the line reads exactly \"N of 4 open\"");

            FeatureUnlockDef row = db.Onboarding.UnlockOf(Feature.Marketplace);
            Check(row != null && row.StageId == "ch2_4" && db.GetStage("ch2_4") != null && FeatureGate.LockLine(db, Feature.Marketplace) == "UNLOCKS AT " + db.GetStage("ch2_4").DisplayCode,
                  "Marketplace GATING: onboarding.json opens the Marketplace at ch2_4, the first shop to open (" + FeatureGate.LockLine(db, Feature.Marketplace) + ")");

            bool ids = FeatureEntryPoints.Find("hub.map.MARKETPLACE") != null && FeatureEntryPoints.Find("hub.map.MARKETPLACE").Feature == Feature.Marketplace
                       && FeatureEntryPoints.Find("hub.map.RELICS") == null
                       && FeatureEntryPoints.Find("screen:Marketplace") != null && FeatureEntryPoints.Find("screen:Marketplace").Feature == Feature.Marketplace;
            for (int i = 0; i < MarketplaceShops.All.Length; i++)
            {
                FeatureEntryPoint door = FeatureEntryPoints.Find(MarketplaceShops.All[i].EntryId);
                if (door == null || door.Feature != MarketplaceShops.All[i].Feature) ids = false;
            }
            bool spelled = true;
            string doorSrc = R26Read(root, "Assets", "Scripts", "UI", "ShopDoors.cs");
            for (int i = 0; i < MarketplaceShops.All.Length; i++)
                if (!doorSrc.Contains("return \"" + MarketplaceShops.All[i].EntryId + "\"")) spelled = false;
            Check(ids && spelled, "Marketplace GATING: hub.map.MARKETPLACE and screen:Marketplace are gated by Feature.Marketplace, the old hub.map.RELICS is gone, and each card has its own door id bound to its shop's feature");

            // The count and each card's lock, walked through the campaign with the real table.
            string[] atStages = { "ch2_4", "ch2_8", "ch3_1", "ch3_12" };
            int[] wantOpen = { 1, 2, 3, 4 };
            PlayerProfile fresh = GameSession.CreateNewProfile(db, 1760000000L);
            bool freshLocked = !FeatureEntryPoints.Allows(db, fresh, "hub.map.MARKETPLACE") && MarketplaceShops.OpenByTable(db, fresh) == 0;
            bool walked = true;
            string trail = "";
            for (int k = 0; k < atStages.Length; k++)
            {
                PlayerProfile p = GameSession.CreateNewProfile(db, 1760000000L);
                StageDef target = db.GetStage(atStages[k]);
                for (int s = 0; s < db.AllStages.Count && db.AllStages[s].GlobalIndex <= target.GlobalIndex; s++) p.ClearedStageIds.Add(db.AllStages[s].Id);
                int open = MarketplaceShops.OpenByTable(db, p);
                trail += atStages[k] + "=" + open + " ";
                if (open != wantOpen[k] || !FeatureEntryPoints.Allows(db, p, "hub.map.MARKETPLACE")) walked = false;
                bool relicOpen = FeatureEntryPoints.Allows(db, p, "marketplace.RelicShop");
                if (relicOpen != (k >= 2)) walked = false;
            }
            Check(freshLocked && walked,
                  "Marketplace GATING: a fresh profile has the stall locked and 0 of 4 open; after 2-4, 2-8, 3-1, 3-12 it is 1, 2, 3, 4 of 4 and the Relic Shop card stays locked until 3-1 (" + trail.Trim() + ")");

            string store = R26Read(root, "Assets", "Scripts", "UI", "Screens", "StoreScreen.cs");
            string market = R26Read(root, "Assets", "Scripts", "UI", "Screens", "MarketplaceScreen.cs");
            string doors = R26Read(root, "Assets", "Scripts", "UI", "ShopDoors.cs");
            string router = R26Read(root, "Assets", "Scripts", "UI", "ScreenRouter.cs");
            string hub = R26Read(root, "Assets", "Scripts", "UI", "Screens", "MainMenuScreen.cs");
            Check(store.Contains("ShopDoors.Build(") && market.Contains("ShopDoors.Build(") && !store.Contains("struct ShopDoor"),
                  "Marketplace SHARED: the Store's SHOPS tab and the Marketplace screen draw the same ShopDoors, and the Store no longer carries its own copy");
            Check(doors.Contains("NOT IN THIS BUILD") && doors.Contains("FeatureLockView.Attach(") && doors.Contains("EntryOf(shop)") && doors.Contains("\"OPEN\""),
                  "Marketplace CARDS: each card gates by its own door id, carries the lock and the new dot, says NOT IN THIS BUILD for a missing shop and OPEN otherwise");
            Check(router.Contains("Marketplace = Create<Screens.MarketplaceScreen>()") && Regex.IsMatch(router, @"LiveEvents,\s*Marketplace\s*[,\}]"),
                  "Marketplace ROUTER: ScreenId.Marketplace exists and the router builds the screen");
            Check(hub.Contains("AddMapNode(\"MARKETPLACE\", MarketX, MarketY, OpenMarketplace, \"hub.map.MARKETPLACE\")") && !hub.Contains("AddMapNode(\"RELICS\""),
                  "Marketplace HUB: the market stall's label is MARKETPLACE and it opens the Marketplace, not the Relic Shop");
            Check(market.Contains("\"MARKETPLACE\"") && market.Contains("CountLine("),
                  "Marketplace SCREEN: titled MARKETPLACE with the open count as its subtitle");
            string shots = R26Read(root, "Assets", "Editor", "PlayModeScreenshot.cs");
            Check(shots.Contains("case \"Marketplace\"") && shots.Contains("case \"MarketplaceLocked\"") && shots.Contains("case \"HeroDetailCap\""),
                  "Marketplace CAPTURE: PlayModeScreenshot has the Marketplace (all open and fresh locks) and Hero-at-cap targets");
            bool metas = true;
            string[] files = { "Assets/Scripts/Core/Progression/MarketplaceShops.cs", "Assets/Scripts/UI/ShopDoors.cs", "Assets/Scripts/UI/Screens/MarketplaceScreen.cs" };
            for (int i = 0; i < files.Length; i++) if (!File.Exists(Path.Combine(root, files[i] + ".meta"))) metas = false;
            Check(metas, "Marketplace FILES: the three new scripts each ship a .meta");
        }

        // ------------------------------------------------------------------ item 3: kit polish

        /// <summary>One decoded pixel of an <see cref="R26Png"/>.</summary>
        private struct R26Pixel { public int A, R, G, B; }

        /// <summary>A minimal PNG reader (8-bit RGB or RGBA, not interlaced, every filter), so the kit checks need no
        /// System.Drawing: that assembly is Windows-only, and the ubuntu CI job has no reference to it.</summary>
        private sealed class R26Png : IDisposable
        {
            public readonly int Width, Height;
            private readonly byte[] px; // RGBA, row by row

            public R26Png(string path)
            {
                byte[] f = File.ReadAllBytes(path);
                int pos = 8, w = 0, h = 0, ct = 0;
                MemoryStream idat = new MemoryStream();
                while (pos + 8 <= f.Length)
                {
                    int len = (f[pos] << 24) | (f[pos + 1] << 16) | (f[pos + 2] << 8) | f[pos + 3];
                    string type = System.Text.Encoding.ASCII.GetString(f, pos + 4, 4);
                    int data = pos + 8;
                    if (type == "IHDR")
                    {
                        w = (f[data] << 24) | (f[data + 1] << 16) | (f[data + 2] << 8) | f[data + 3];
                        h = (f[data + 4] << 24) | (f[data + 5] << 16) | (f[data + 6] << 8) | f[data + 7];
                        if (f[data + 8] != 8 || f[data + 12] != 0) throw new InvalidDataException(path + ": only 8-bit, non-interlaced PNG is supported");
                        ct = f[data + 9];
                        if (ct != 2 && ct != 6) throw new InvalidDataException(path + ": only RGB or RGBA PNG is supported");
                    }
                    else if (type == "IDAT") idat.Write(f, data, len);
                    else if (type == "IEND") break;
                    pos = data + len + 4;
                }
                Width = w; Height = h;
                int bpp = ct == 6 ? 4 : 3, stride = w * bpp;
                byte[] raw = new byte[(stride + 1) * h];
                idat.Position = 2; // the zlib header
                using (DeflateStream z = new DeflateStream(idat, CompressionMode.Decompress))
                {
                    int got = 0;
                    while (got < raw.Length) { int n = z.Read(raw, got, raw.Length - got); if (n <= 0) break; got += n; }
                }
                px = new byte[w * h * 4];
                byte[] prev = new byte[stride], cur = new byte[stride];
                for (int y = 0; y < h; y++)
                {
                    int filter = raw[y * (stride + 1)];
                    Buffer.BlockCopy(raw, y * (stride + 1) + 1, cur, 0, stride);
                    for (int i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0, add = 0;
                        if (filter == 1) add = a;
                        else if (filter == 2) add = b;
                        else if (filter == 3) add = (a + b) / 2;
                        else if (filter == 4)
                        {
                            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                            add = (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                        }
                        cur[i] = (byte)(cur[i] + add);
                    }
                    for (int x = 0; x < w; x++)
                    {
                        int o = (y * w + x) * 4;
                        px[o] = cur[x * bpp]; px[o + 1] = cur[x * bpp + 1]; px[o + 2] = cur[x * bpp + 2];
                        px[o + 3] = bpp == 4 ? cur[x * bpp + 3] : (byte)255;
                    }
                    byte[] t = prev; prev = cur; cur = t;
                }
            }

            public R26Pixel GetPixel(int x, int y)
            {
                int o = (y * Width + x) * 4;
                R26Pixel p; p.R = px[o]; p.G = px[o + 1]; p.B = px[o + 2]; p.A = px[o + 3];
                return p;
            }

            public void Dispose() { }
        }

        private static readonly string[] R26PolishPieces = { "panel_large", "panel_small", "btn_primary", "btn_secondary", "btn_disabled" };

        /// <summary>Mean colour of a centre block of a PNG (the plate, never the rim): the share of the width and
        /// height given, centred.</summary>
        private static double[] R26MeanColour(string png, double shareX, double shareY)
        {
            using (R26Png bmp = new R26Png(png))
            {
                int w = bmp.Width, h = bmp.Height;
                int x0 = (int)(w * (1 - shareX) / 2), x1 = (int)(w * (1 + shareX) / 2);
                int y0 = (int)(h * (1 - shareY) / 2), y1 = (int)(h * (1 + shareY) / 2);
                double r = 0, g = 0, b = 0; int n = 0;
                for (int y = y0; y < y1; y += 2)
                    for (int x = x0; x < x1; x += 2)
                    {
                        R26Pixel c = bmp.GetPixel(x, y);
                        if (c.A < 200) continue;
                        r += c.R; g += c.G; b += c.B; n++;
                    }
                return n == 0 ? new double[] { 0, 0, 0 } : new double[] { r / n, g / n, b / n };
            }
        }

        private static double R26Luma(double[] c)
        {
            return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2];
        }

        private static double R26RelLum(double[] c)
        {
            double[] l = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double v = c[i] / 255.0;
                l[i] = v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * l[0] + 0.7152 * l[1] + 0.0722 * l[2];
        }

        private static double R26Contrast(double[] a, double[] b)
        {
            double la = R26RelLum(a), lb = R26RelLum(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        private static double[] R26Hex(string src, string name)
        {
            Match m = Regex.Match(src, name + @"\s*=\s*UITheme\.Hex\(""([0-9A-Fa-f]{6})""\)");
            if (!m.Success) return new double[] { 0, 0, 0 };
            string h = m.Groups[1].Value;
            return new double[] { Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16) };
        }

        /// <summary>Mean brightness of the rim: a thin band along the left edge, where the frame is gold.</summary>
        private static double R26RimLuma(string png)
        {
            using (R26Png bmp = new R26Png(png))
            {
                double total = 0; int n = 0;
                int x0 = (int)(bmp.Width * 0.015), x1 = (int)(bmp.Width * 0.05);
                for (int y = bmp.Height / 4; y < bmp.Height * 3 / 4; y += 2)
                    for (int x = x0; x <= x1; x++)
                    {
                        R26Pixel c = bmp.GetPixel(x, y);
                        if (c.A < 200) continue;
                        total += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B; n++;
                    }
                return n == 0 ? 0 : total / n;
            }
        }

        private static void CheckR26KitPolish(string root)
        {
            string kit = Path.Combine(root, "Assets", "Resources", "Art", "UIKit");
            string src = Path.Combine(root, "Tools", "uikit-polish-src");
            UiKitSwitch.Enabled = true;
            string ornamentKey = UiKitMap.ForgeSlot("forge_ornament");
            UiKitSwitch.Enabled = false;
            Check(ornamentKey == "corner_ornament" && UiKitMap.ForgeSlot("forge_ornament") == null,
                  "kit POLISH: forge_ornament maps to corner_ornament with the kit on and to nothing with it off (the old diamond)");
            string ornamentMeta = R26Read(kit, "corner_ornament.png.meta").Replace("\r\n", "\n");
            Check(File.Exists(Path.Combine(kit, "corner_ornament.png")) && Regex.IsMatch(ornamentMeta, @"buildTarget: Standalone\n(?:    .*\n)*?    textureFormat: 25\n")
                  && Regex.IsMatch(ornamentMeta, @"buildTarget: Android\n(?:    .*\n)*?    textureFormat: 48\n") && ornamentMeta.Contains("spriteMode: 1"),
                  "kit POLISH: corner_ornament.png ships with a sprite meta that has BC7 (Windows) and ASTC 4x4 (Android)");

            // sizes and borders unchanged: the 9-slice metas of the five re-exported pieces are as they were
            string[] borders = { "x: 101, y: 104, z: 101, w: 104", "x: 61, y: 43, z: 61, w: 43", "x: 44, y: 83, z: 44, w: 83", "x: 40, y: 73, z: 40, w: 73", "x: 40, y: 71, z: 40, w: 71" };
            bool sameShape = true;
            for (int i = 0; i < R26PolishPieces.Length; i++)
            {
                string png = Path.Combine(kit, R26PolishPieces[i] + ".png");
                string pristine = Path.Combine(src, R26PolishPieces[i] + ".png");
                if (!File.Exists(png) || !File.Exists(pristine)) { sameShape = false; continue; }
                using (R26Png a = new R26Png(png))
                using (R26Png b = new R26Png(pristine))
                    if (a.Width != b.Width || a.Height != b.Height) sameShape = false;
                if (!R26Read(kit, R26PolishPieces[i] + ".png.meta").Contains("spriteBorder: {" + borders[i] + "}")) sameShape = false;
            }
            Check(sameShape, "kit POLISH: the five re-exported pieces keep their size and their 9-slice borders, so no layout moves");

            double[] plate = R26MeanColour(Path.Combine(kit, "btn_primary.png"), 0.7, 0.5);
            double mx = Math.Max(plate[0], Math.Max(plate[1], plate[2])), mn = Math.Min(plate[0], Math.Min(plate[1], plate[2]));
            double hue = 0;
            if (mx > mn)
            {
                if (mx == plate[0]) hue = 60.0 * ((plate[1] - plate[2]) / (mx - mn));
                else if (mx == plate[1]) hue = 60.0 * (2 + (plate[2] - plate[0]) / (mx - mn));
                else hue = 60.0 * (4 + (plate[0] - plate[1]) / (mx - mn));
                if (hue < 0) hue += 360;
            }
            double sat = mx > 0 ? (mx - mn) / mx : 0, val = mx / 255.0;
            Check(hue >= 18 && hue <= 40 && sat >= 0.70 && val >= 0.80,
                  "kit POLISH: btn_primary is a clear warm orange (hue " + hue.ToString("0") + ", saturation " + sat.ToString("0.00") + ", value " + val.ToString("0.00") + ")");

            double[] white = { 255, 255, 255 };
            string forge = R26Read(root, "Assets", "Scripts", "UI", "Emberforge.cs");
            double[] disabledLabel = R26Hex(forge, "TextDisabled");
            double[] secPlate = R26MeanColour(Path.Combine(kit, "btn_secondary.png"), 0.7, 0.5);
            double[] secOld = R26MeanColour(Path.Combine(src, "btn_secondary.png"), 0.7, 0.5);
            double[] disPlate = R26MeanColour(Path.Combine(kit, "btn_disabled.png"), 0.7, 0.5);
            double[] panelPlate = R26MeanColour(Path.Combine(kit, "panel_large.png"), 0.5, 0.5);
            Check(R26Luma(secPlate) > R26Luma(secOld) + 25 && R26Luma(secPlate) > R26Luma(panelPlate) + 25 && R26Contrast(white, secPlate) >= 4.5,
                  "kit POLISH: btn_secondary is lighter than before (" + R26Luma(secOld).ToString("0") + " to " + R26Luma(secPlate).ToString("0") + ") and than the panel it sits on ("
                  + R26Luma(panelPlate).ToString("0") + "), and its white label keeps contrast " + R26Contrast(white, secPlate).ToString("0.0") + ":1");
            Check(R26Luma(disPlate) > R26Luma(panelPlate) + 15 && R26Contrast(disabledLabel, disPlate) >= 3.5 && R26Contrast(white, disPlate) > R26Contrast(disabledLabel, disPlate),
                  "kit POLISH: btn_disabled is lighter than the panel and its dimmed label (TextDisabled) reads at " + R26Contrast(disabledLabel, disPlate).ToString("0.0")
                  + ":1, dimmer than the " + R26Contrast(white, disPlate).ToString("0.0") + ":1 of an enabled label");

            // a stronger gold rim: the rim's mean brightness against the pristine file
            double rimNow = R26RimLuma(Path.Combine(kit, "panel_large.png")), rimWas = R26RimLuma(Path.Combine(src, "panel_large.png"));
            double rimSmallNow = R26RimLuma(Path.Combine(kit, "panel_small.png")), rimSmallWas = R26RimLuma(Path.Combine(src, "panel_small.png"));
            Check(rimNow > rimWas * 1.08 && rimSmallNow > rimSmallWas * 1.08,
                  "kit POLISH: the gilded frame is stronger (rim brightness " + rimWas.ToString("0") + " to " + rimNow.ToString("0") + " large, " + rimSmallWas.ToString("0") + " to " + rimSmallNow.ToString("0") + " small)");

            string gen = R26Read(root, "Tools", "uikit-polish-gen.py");
            Check(gen.Contains("from PIL import") && !gen.Contains("ui-gen") && !gen.Contains("concepts") && !gen.Contains("picked") && Directory.Exists(src),
                  "kit POLISH: Tools/uikit-polish-gen.py is PIL code-drawing from the repo's own pristine pieces and never names the reference image folder");

            string ui = R26Read(root, "Assets", "Scripts", "UI", "EmberforgeUI.cs");
            Check(ui.Contains("AddKitOrnament(") && ui.Contains("Emberforge.KitOrnamentSize") && forge.Contains("KitOrnamentSize = 48f") && forge.Contains("UiKit.On ? KitOrnamentSize"),
                  "kit POLISH: panels draw the filigree corner (mirrored into the four corners) with the kit on, and OrnamentClearance grows with it");
            Check(!ui.Contains("WarmTint") && !ui.Contains("\"Warm\""),
                  "kit POLISH: the Warm overlay on the primary button is gone, the plate itself is orange");
            Check(ui.Contains("Emberforge.ButtonDisabledText") && ui.Contains("!_interactable && UiKit.On"),   // R53: the dimmer label of the dark disabled plate
                  "kit POLISH: a disabled button dims its label, with the kit on only");
            string hero = R26Read(root, "Assets", "Scripts", "UI", "Screens", "HeroDetailScreen.cs");
            string fusion = R26Read(root, "Assets", "Scripts", "UI", "Screens", "FusionScreen.cs");
            string menu = R26Read(root, "Assets", "Scripts", "UI", "Screens", "MainMenuScreen.cs");
            string claim = R26Read(root, "Assets", "Scripts", "UI", "Hud", "IdleClaimPopup.cs");
            string store = R26Read(root, "Assets", "Scripts", "UI", "Screens", "StoreScreen.cs");
            Check(Regex.IsMatch(hero, @"_levelUpButton = EmberforgeUI\.Button\([^;]*ForgeButtonKind\.Primary") && Regex.IsMatch(hero, @"_fusionButton = EmberforgeUI\.Button\([^;]*ForgeButtonKind\.Secondary") && hero.Contains("EmberforgeUI.SetKind(_fusionButton")
                  && Regex.IsMatch(fusion, @"_actButton = EmberforgeUI\.Button\([^;]*ForgeButtonKind\.Primary")
                  && Regex.IsMatch(menu, @"_continueButton = EmberforgeUI\.Button\([^;]*ForgeButtonKind\.Primary") && Regex.IsMatch(claim, @"_claim = EmberforgeUI\.Button\([^;]*ForgeButtonKind\.Primary")
                  && Regex.IsMatch(store, @"Button\(""Claim"", tile, text, UITheme\.FontTiny,\s*EmberforgeUI\.LoopCardKind\(") && Regex.IsMatch(store, @"Button\(""Buy"" \+ o\.Id, parent, text, font, ForgeButtonKind\.Secondary\)"),
                  "kit POLISH: the orange primary is on Continue Campaign, Level Up (and Ascend on the hero page only when Level Up is not available, R53), Ascend (fusion page) and Claim (idle popup; the pass tiles give it to the first claimable one only); a paid Buy is a Secondary (R53)");
        }

        // ------------------------------------------------------------------ item 4: the hero at the level cap

        private static void CheckR26HeroCap(ContentDatabase db, string root)
        {
            Check(LevelCapText.Opening == "Level cap reached, raise your rank." && LevelCapText.ButtonLabel == "LEVEL UP",
                  "hero CAP: the owner's sentence is exactly \"Level cap reached, raise your rank.\" and the button keeps its name LEVEL UP");
            string five1 = LevelCapText.Note(5, 1, 0, "Kael", 0);
            string five3 = LevelCapText.Note(5, 3, 2, "Kael", 0);
            string maxed = LevelCapText.Note(5, -1, 0, "Kael", 0);
            string low = LevelCapText.Note(3, -1, 0, "Kael", 12);
            Check(five1 == "Level cap reached, raise your rank. Ascend with 1 more copy of Kael to lift it  -  you hold 0 to spare."
                  && five3 == "Level cap reached, raise your rank. Ascend with 3 more copies of Kael to lift it  -  you hold 2 to spare.",
                  "hero CAP: a five-star's note opens with the sentence, then the existing ascend guidance (one copy, several copies)");
            Check(maxed == "Level cap reached, raise your rank. This is the highest ascension this game authors."
                  && low == "Level cap reached, raise your rank. Below five stars a hero stops here and never ascends; it is worth 12 points towards converting fodder into a five-star.",
                  "hero CAP: the highest-ascension note and the below-five-stars note both open with the sentence and keep their guidance after it");
            Check(five1.StartsWith(LevelCapText.Opening, StringComparison.Ordinal) && maxed.StartsWith(LevelCapText.Opening, StringComparison.Ordinal)
                  && low.StartsWith(LevelCapText.Opening, StringComparison.Ordinal) && !five1.Contains("\n"),
                  "hero CAP: the owner's sentence is first in every variant and the note stays one line");

            // the real rule: at the cap a level up does nothing and costs nothing, one or ten
            HeroDef def = null;
            for (int i = 0; i < db.Heroes.Count && def == null; i++) if (db.Heroes[i].Stars >= 5) def = db.Heroes[i];
            if (def == null) { Check(false, "hero CAP: the content has a five-star hero"); return; }
            PlayerProfile p = GameSession.CreateNewProfile(db, 1760000000L);
            GameSession session = new GameSession(db, p);
            HeroInstance h = session.GrantHeroDirect(def, 1760000000L);
            h.Level = session.HeroLevelCapOf(h);
            p.Gold = 100000000L;
            long gold = p.Gold;
            LevelUpResult one = session.LevelUpHero(h, 1, 1760000000L);
            LevelUpResult ten = session.LevelUpHero(h, 10, 1760000000L);
            Check(h.Level >= session.HeroLevelCapOf(h) && !session.CanLevelUp(h) && !one.Success && !ten.Success && p.Gold == gold && one.LevelsGained == 0 && ten.LevelsGained == 0,
                  "hero CAP: at the cap (level " + h.Level + ") Level Up and x10 both do nothing and spend no gold even with " + gold + " in hand");

            string hero = R26Read(root, "Assets", "Scripts", "UI", "Screens", "HeroDetailScreen.cs");
            Check(hero.Contains("bool affordable = !atCap && session.Profile.Gold >= cost;") && hero.Contains("_levelUpButton.Interactable = affordable;") && hero.Contains("_levelUpTenButton.Interactable = affordable;")
                  && hero.Contains("LevelCapText.Note(") && hero.Contains("LevelCapText.ButtonLabel") && !hero.Contains("\"LEVEL CAP\""),
                  "hero CAP: the page disables both buttons from the cap flag, shows the sentence through LevelCapText and keeps the label LEVEL UP");
            Check(hero.Contains("int cap = session.HeroLevelCapOf(_hero);"),
                  "hero CAP: the cap is the hero's own (stars and ascension), not the account rank");
        }

        // ------------------------------------------------------------------ round 2: the tester's findings

        private static void CheckR26Round2(string root)
        {
            Check(KitOrnamentRule.SizeFor(1750f, 150f) == 0f && KitOrnamentRule.SizeFor(440f, 44f) == 0f && KitOrnamentRule.SizeFor(160f, 100f) == 0f
                  && KitOrnamentRule.SizeFor(179f, 600f) == 0f,
                  "ornament RULE: a tile, a quest row, a hero card or any panel under 180 on its short side carries no corner");
            Check(KitOrnamentRule.SizeFor(420f, 600f) == 48f && KitOrnamentRule.SizeFor(180f, 400f) == 48f * 0f + System.Math.Min(48f, 0.30f * 180f)
                  && KitOrnamentRule.SizeFor(1800f, 700f) == 48f && KitOrnamentRule.SizeFor(200f, 200f) <= 0.30f * 200f + 0.001f,
                  "ornament RULE: a panel that can carry one gets min(48, 30 percent of its short side)");
            string ui = R26Read(root, "Assets", "Scripts", "UI", "EmberforgeUI.cs");
            Check(ui.Contains("class KitOrnamentFit") && ui.Contains("KitOrnamentRule.SizeFor(rt.rect.width, rt.rect.height)") && ui.Contains("AddComponent<KitOrnamentFit>()"),
                  "ornament RULE: every kit panel fits its corners to its real size, so a layout-sized tile or row is handled too");
            string store = R26Read(root, "Assets", "Scripts", "UI", "Screens", "StoreScreen.cs");
            Check(store.Contains("PURCHASES\\nOPEN WITH THE STORE") && store.Contains("-top, 300f, HubStyle.TabHeight") && store.Contains("\"TEST STORE  -  FREE\", Emberforge.TextGold"),
                  "Store BANNER: the coming soon line is two lines in a 300 wide box clear of the SKINS tab, and the TEST STORE chip is gold, not red");
            string hero = R26Read(root, "Assets", "Scripts", "UI", "Screens", "HeroDetailScreen.cs");
            Check(hero.Contains("UiKit.On ? Emberforge.OrnamentClearance + UITheme.Space8"),
                  "hero PAGE: with the kit on, LEVEL starts right of the corner and the slot line sits above the frame");
            string doors = R26Read(root, "Assets", "Scripts", "UI", "ShopDoors.cs");
            Check(doors.Contains("lockLine.ToUpperInvariant(), Emberforge.TextBody,"),
                  "Marketplace LOCKS: the UNLOCKS AT line is body-bright, not quiet grey");
        }

        // ------------------------------------------------------------------ round 3: the frame's thickness

        private static void CheckR26Round3(string root)
        {
            float x = KitFrameRule.SmallDrawnX, y = KitFrameRule.SmallDrawnY;
            Check(System.Math.Abs(KitFrameRule.Drawn(61, 234.615f, 1f) - 26f) < 0.1f && System.Math.Abs(KitFrameRule.Drawn(43, 234.615f, 1f) - 18.3f) < 0.1f
                  && x < 13.1f && y < 9.2f && x > 12.9f && y > 9.1f,
                  "frame RULE: panel_small draws 26 by 18 units at full size and half of that (" + x.ToString("0.0") + " by " + y.ToString("0.0") + ") with the small multiplier");
            Check(KitFrameRule.ContentPad(6f, y) > y && KitFrameRule.ContentPad(6f, y) >= 11f && KitFrameRule.ContentPad(30f, y) == 30f && KitFrameRule.ContentPad(6f, 0f) == 6f,
                  "frame RULE: content at an edge keeps the drawn frame plus air from it, never less than it asked for, and exactly what it asked for with the kit off");
            string meta = R26Read(root, "Assets", "Resources", "Art", "UIKit", "panel_small.png.meta");
            Check(meta.Contains("spriteBorder: {x: " + KitFrameRule.SmallBorderX + ", y: " + KitFrameRule.SmallBorderY + ", z: " + KitFrameRule.SmallBorderX + ", w: " + KitFrameRule.SmallBorderY + "}")
                  && meta.Contains("spritePixelsToUnits: 234.615"),
                  "frame RULE: the numbers in KitFrameRule are the ones in panel_small's meta");
            string ui = R26Read(root, "Assets", "Scripts", "UI", "EmberforgeUI.cs");
            Check(ui.Contains("pixelsPerUnitMultiplier = KitFrameRule.SmallPanelMultiplier") && ui.Contains("card.Plate.pixelsPerUnitMultiplier = KitFrameRule.SmallPanelMultiplier"),
                  "frame WIRING: small panels, plain panels and small cards draw the thinner frame, with the kit on only");
            string events = R26Read(root, "Assets", "Scripts", "UI", "Screens", "LiveEventsScreen.cs");
            Check(events.Contains("-Emberforge.SmallPadY(6f), width - 8f") && events.Contains("SmallPadX(16f)") && events.Contains("Emberforge.SmallPadY(6f), width - 8f, 22f"),
                  "frame WIRING: the Events tiles and quest rows inset their text from the frame (points, track locked, quest line)");
            string forge = R26Read(root, "Assets", "Scripts", "UI", "Emberforge.cs");
            Check(forge.Contains("UiKit.On ? KitFrameRule.ContentPad(wanted, KitFrameRule.SmallDrawnX) : wanted"),
                  "frame WIRING: the content inset applies with the kit on and is exactly the old value with it off");
            string hero = R26Read(root, "Assets", "Scripts", "UI", "Screens", "HeroDetailScreen.cs");
            Check(hero.Contains("_slotStatus.alignment = TextAnchor.UpperLeft") && !hero.Contains("UiKit.On ? 40f : 8f"),
                  "hero PAGE: with the kit on the slot line sits under the LEVEL label, no longer over the attunement line");
            string toast = R26Read(root, "Assets", "Scripts", "UI", "Overlays.cs");
            Check(toast.Contains("UIBuilder.Box(_root, 0f, 300f, MinWidth, MinHeight, new Vector2(0.5f, 0f))"),
                  "toast: the message strip keeps its bottom anchor (a header-band anchor covered the Store title ribbon in the R26 captures; the bottom one covering price buttons is a known low item)");
        }
    }
}
