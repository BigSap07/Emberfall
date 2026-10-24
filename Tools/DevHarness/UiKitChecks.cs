using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE UI KIT (R20): the generated art kit behind UiKitSwitch. One named check per rule: the switch
    /// ships ON (R26; EMBERFALL_UIKIT_OFF is the opt-out), OFF IS THE OLD LOOK (every lookup answers null, so every call site draws what it always drew),
    /// ON maps every slot, relic and ability the kit is supposed to cover, every key it can answer with has
    /// a file and an import setting that make it a sprite, the art references stay out of the eight hashed
    /// content files, and the engine half is wired where the map says.
    ///
    /// Presentation only: nothing here reads a save, runs a fight or changes shared content. The switch is
    /// put back as it was found.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckUiKit(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("THE UI KIT (R20: art kit behind UiKitSwitch, off restores today's look)");
            string kitDir = Path.GetFullPath(Path.Combine(contentDir, "..", "Art", "UIKit"));
            string scriptsDir = Path.GetFullPath(Path.Combine(contentDir, "..", "..", "Scripts"));
            bool was = UiKitSwitch.Enabled;
            try
            {
                Check(UiKitSwitch.ShippedDefault && was, "SHIPPED: UiKitSwitch is ON by default (R26); only a build defining EMBERFALL_UIKIT_OFF ships the old look");
                CheckUiKitOffIsToday(db);
                CheckUiKitOnMapsEverything(db);
                CheckUiKitFilesAndImports(db, kitDir);
                CheckUiKitNotHashed(db, contentDir);
                CheckUiKitWiring(scriptsDir);
                CheckUiKitSplashAndIcon(Path.GetFullPath(Path.Combine(contentDir, "..", "..", "..")), scriptsDir);
            }
            finally
            {
                UiKitSwitch.Enabled = was;
            }
            Check(UiKitSwitch.Enabled == was, "the switch is put back as the run found it (" + (was ? "on" : "off") + ")");
        }

        /// <summary>Every id and slot the kit might be asked about, off: nothing. Null is what makes each
        /// call site keep its old drawing, so null everywhere is the rollback.</summary>
        private static void CheckUiKitOffIsToday(ContentDatabase db)
        {
            UiKitSwitch.Enabled = false;
            int asked = 0, answered = 0;
            foreach (string s in UiKitMap.AllHudSlots()) { asked++; if (UiKitMap.HudSlot(s) != null) answered++; }
            foreach (string s in UiKitMap.AllForgeSlots()) { asked++; if (UiKitMap.ForgeSlot(s) != null) answered++; }
            foreach (string s in UiKitMap.AllIcons()) { asked++; if (UiKitMap.Icon(s) != null) answered++; }
            foreach (string s in UiKitMap.AllOfferIds()) { asked++; if (UiKitMap.OfferCard(s) != null) answered++; }
            foreach (string s in UiKitMap.AllCurrencies()) { asked++; if (UiKitMap.Currency(s) != null) answered++; }
            asked++; if (UiKitMap.PassHeader() != null) answered++;
            for (int i = 0; i < db.Relics.Count; i++) { asked++; if (UiKitMap.RelicIcon(db.Relics[i].Id) != null) answered++; }
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                HeroDef h = db.Heroes[i];
                asked++; if (UiKitMap.AbilityIcon(h.Id, h.BasicAttack != null ? h.BasicAttack.Id : "x") != null) answered++;
                asked++; if (UiKitMap.AspectBadge(h.Aspect.ToString()) != null) answered++;
                asked++; if (UiKitMap.CardFrame(h.Stars) != null) answered++;
            }
            Check(asked > 100 && answered == 0,
                  "OFF IS TODAY: with the switch off, all " + asked + " kit lookups (hud and forge slots, offers, purses, header, relics, abilities, badges, frames) answer null, so every screen draws what it drew before");
        }

        /// <summary>On, the tables cover what the brief names: one piece per relic id, one icon for the basic,
        /// the active and passive1 of every hero, one card per Store offer, every aspect, every star tier.</summary>
        private static void CheckUiKitOnMapsEverything(ContentDatabase db)
        {
            UiKitSwitch.Enabled = true;
            int missing = 0;
            foreach (string s in UiKitMap.AllHudSlots()) if (UiKitMap.HudSlot(s) == null) missing++;
            foreach (string s in UiKitMap.AllForgeSlots()) if (UiKitMap.ForgeSlot(s) == null) missing++;
            foreach (string s in UiKitMap.AllIcons()) if (UiKitMap.Icon(s) == null) missing++;
            foreach (string s in UiKitMap.AllOfferIds()) if (UiKitMap.OfferCard(s) == null) missing++;
            Check(missing == 0 && UiKitMap.PassHeader() != null && UiKitMap.HudSlot("not_a_slot") == null && UiKitMap.ForgeSlot("") == null,
                  "ON: every slot in the tables answers a piece, and a slot the kit does not cover answers null");

            int relicMissing = 0;
            for (int i = 0; i < db.Relics.Count; i++) if (UiKitMap.RelicIcon(db.Relics[i].Id) == null) relicMissing++;
            Check(db.Relics.Count > 0 && relicMissing == 0, "ON: one icon key per relic id in relics.json (" + db.Relics.Count + " relics)");

            int abilities = 0, abilityMissing = 0;
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                HeroDef h = db.Heroes[i];
                List<string> ids = new List<string>();
                if (h.BasicAttack != null) ids.Add(h.BasicAttack.Id);
                if (h.Active != null) ids.Add(h.Active.Id);
                for (int p = 0; p < h.Passives.Count; p++) if (h.Passives[p].SlotKey == "passive1") ids.Add(h.Passives[p].Id);
                for (int a = 0; a < ids.Count; a++) { abilities++; if (UiKitMap.AbilityIcon(h.Id, ids[a]) == null) abilityMissing++; }
            }
            Check(abilities > 0 && abilityMissing == 0, "ON: one icon key per basic, active and passive1 (the passive whose SlotKey is passive1) of every hero (" + abilities + " abilities)");

            Check(UiKitMap.CardFrame(0) == "card_frame_1" && UiKitMap.CardFrame(9) == "card_frame_5" && UiKitMap.CardFrame(3) == "card_frame_3"
                  && UiKitMap.AspectBadge("Gale") == "aspect_gale",
                  "ON: card frames clamp to the five tiers, aspect badges are named by aspect");
            UiKitSwitch.Enabled = false;
        }

        /// <summary>Every key the tables can answer with and every relic resolves to a PNG with a meta that imports
        /// it as a single sprite; every ability a hero has an icon for is a cell of the ability atlas, inside the
        /// texture. Abilities are one texture, so there is no per-ability file to find.</summary>
        private static void CheckUiKitFilesAndImports(ContentDatabase db, string kitDir)
        {
            Check(Directory.Exists(kitDir), "the kit folder exists at Resources/Art/UIKit");
            if (!Directory.Exists(kitDir)) return;

            List<string> keys = UiKitMap.AllTableKeys();
            UiKitSwitch.Enabled = true;
            for (int i = 0; i < db.Relics.Count; i++) keys.Add(UiKitMap.RelicIcon(db.Relics[i].Id));
            List<string> abilityKeys = new List<string>();
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                HeroDef h = db.Heroes[i];
                if (h.BasicAttack != null) abilityKeys.Add(UiKitMap.AbilityIcon(h.Id, h.BasicAttack.Id));
                if (h.Active != null) abilityKeys.Add(UiKitMap.AbilityIcon(h.Id, h.Active.Id));
                for (int p = 0; p < h.Passives.Count; p++)
                    if (h.Passives[p].SlotKey == "passive1") abilityKeys.Add(UiKitMap.AbilityIcon(h.Id, h.Passives[p].Id));
            }
            UiKitSwitch.Enabled = false;

            int noFile = 0, badMeta = 0, dupGuid = 0;
            List<string> firstMiss = new List<string>();
            Dictionary<string, string> guids = new Dictionary<string, string>();
            List<string> files = new List<string>(keys);
            files.Add("ability_atlas");
            for (int i = 0; i < files.Count; i++)
            {
                string key = files[i];
                string png = Path.Combine(kitDir, key.Replace('/', Path.DirectorySeparatorChar) + ".png");
                string meta = png + ".meta";
                if (!File.Exists(png) || !File.Exists(meta))
                {
                    noFile++;
                    if (firstMiss.Count < 5) firstMiss.Add(key);
                    continue;
                }
                string m = File.ReadAllText(meta).Replace("\r\n", "\n");   // a fresh checkout (autocrlf) lands CRLF
                Match g = Regex.Match(m, @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                bool sprite = m.Contains("textureType: 8") && m.Contains("spriteMode: 1") && m.Contains("alphaIsTransparency: 1");
                // the Windows and Android entries must be explicit: DefaultTexturePlatform ships RGBA32
                bool compressed = Regex.IsMatch(m, @"buildTarget: Standalone\n(?:    .*\n)*?    textureFormat: 25")
                                  && Regex.IsMatch(m, @"buildTarget: Android\n(?:    .*\n)*?    textureFormat: 48");
                if (!g.Success || !sprite || !compressed) { badMeta++; continue; }
                string other;
                if (guids.TryGetValue(g.Groups[1].Value, out other) && other != png) dupGuid++;
                guids[g.Groups[1].Value] = png;
            }
            Check(noFile == 0, "every key the kit can answer with has a PNG and a meta (" + files.Count + " files, " + noFile + " missing"
                  + (firstMiss.Count > 0 ? ", first: " + string.Join(", ", firstMiss.ToArray()) : "") + ")");
            Check(badMeta == 0 && dupGuid == 0,
                  "every kit meta imports a single sprite with transparency, its own guid, and BC7 (Windows) and ASTC 4x4 (Android) stated, so none ships uncompressed ("
                  + badMeta + " bad, " + dupGuid + " duplicate guids)");

            // the atlas: the map names a cell for every ability, and every cell lies inside the texture
            string mapPath = Path.Combine(kitDir, "ability_atlas_map.json");
            Check(File.Exists(mapPath), "the ability atlas map exists");
            if (!File.Exists(mapPath)) return;
            Emberfall.Core.Json.JsonValue map = Emberfall.Core.Json.JsonValue.Parse(File.ReadAllText(mapPath));
            int atlasW = map["size"][0].AsInt(0), atlasH = map["size"][1].AsInt(0);
            Emberfall.Core.Json.JsonValue cells = map["map"];
            int noCell = 0, outside = 0;
            for (int i = 0; i < abilityKeys.Count; i++)
            {
                string name = abilityKeys[i].Substring("abilities/".Length);
                if (!cells.Has(name)) { noCell++; continue; }
                Emberfall.Core.Json.JsonValue c = cells[name];
                if (c[0].AsInt(-1) < 0 || c[1].AsInt(-1) < 0 || c[0].AsInt(0) + c[2].AsInt(0) > atlasW || c[1].AsInt(0) + c[3].AsInt(0) > atlasH) outside++;
            }
            Check(abilityKeys.Count > 0 && noCell == 0 && outside == 0,
                  "every basic, active and passive1 icon is a cell inside the ability atlas (" + abilityKeys.Count + " abilities, " + noCell + " without a cell, " + outside + " outside the " + atlasW + "x" + atlasH + " texture)");

            // A sliced piece needs a border or its corners stretch; the pieces the map draws sliced are these.
            string[] sliced = { "panel_large", "panel_small", "btn_primary", "btn_secondary", "btn_disabled", "tab_on", "tab_off", "pill_currency" };
            int unsliced = 0;
            for (int i = 0; i < sliced.Length; i++)
            {
                string meta = Path.Combine(kitDir, sliced[i] + ".png.meta");
                if (!File.Exists(meta)) { unsliced++; continue; }
                Match b = Regex.Match(File.ReadAllText(meta).Replace("\r\n", "\n"), @"spriteBorder: \{x: (\d+), y: (\d+), z: (\d+), w: (\d+)\}");
                if (!b.Success || (b.Groups[1].Value == "0" && b.Groups[2].Value == "0" && b.Groups[3].Value == "0" && b.Groups[4].Value == "0")) unsliced++;
            }
            Check(unsliced == 0, "every piece the map draws sliced (panels, buttons, tabs, pill) carries a sprite border (" + unsliced + " without)");
        }

        /// <summary>The kit's art references never enter a hashed content file: ContentVersion would move and
        /// every recorded guild replay would retire.</summary>
        private static void CheckUiKitNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            int mentions = 0;
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
                if (texts[i] != null && texts[i].IndexOf("UIKit", StringComparison.OrdinalIgnoreCase) >= 0) mentions++;
            }
            Check(mentions == 0 && ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "THE KIT IS NOT HASHED: no fight file names it and ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion + ")");
        }

        /// <summary>R21: the boot splash is behind the switch (null off, "splash" on), the splash is built last in
        /// GameApp only when the kit is on, and the default app icon in ProjectSettings is the delivered file,
        /// imported compressed. A source and file scan: the splash draws only in the editor.</summary>
        private static void CheckUiKitSplashAndIcon(string root, string scriptsDir)
        {
            UiKitSwitch.Enabled = false;
            bool offNull = UiKitMap.Splash() == null;
            UiKitSwitch.Enabled = true;
            bool onNamed = UiKitMap.Splash() == "splash" && UiKitMap.AllTableKeys().Contains("splash");
            UiKitSwitch.Enabled = false;
            Check(offNull && onNamed, "SPLASH: the boot splash answers null with the switch off and the key splash with it on, and the file check covers it");

            string splash = File.ReadAllText(Path.Combine(scriptsDir, "UI", "UiKitSplash.cs"));
            string app = File.ReadAllText(Path.Combine(scriptsDir, "Game", "GameApp.cs"));
            int show = app.IndexOf("UiKitSplash.Show(");
            int complete = app.IndexOf("BootStage = \"complete\"");
            bool gated = show > 0 && complete > show && app.Substring(Math.Max(0, show - 200), Math.Min(200, show)).Contains("UiKit.On");
            Check(splash.Contains("UiKit.Splash()") && gated,
                  "SPLASH WIRING: UiKitSplash asks the kit for its art, and GameApp builds it only when UiKit.On, after the first screen and before boot completes");

            string meta = Path.Combine(root, "Assets", "AppIcon", "app_icon.png.meta");
            string settings = Path.Combine(root, "ProjectSettings", "ProjectSettings.asset");
            bool icon = File.Exists(Path.Combine(root, "Assets", "AppIcon", "app_icon.png")) && File.Exists(meta) && File.Exists(settings);
            if (icon)
            {
                string m = File.ReadAllText(meta).Replace("\r\n", "\n");
                Match g = Regex.Match(m, @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                string ps = File.ReadAllText(settings).Replace("\r\n", "\n");
                bool named = g.Success && ps.Contains("m_BuildTargetIcons:\n  - m_BuildTarget: \n    m_Icons:\n    - serializedVersion: 2\n      m_Icon: {fileID: 2800000, guid: " + g.Groups[1].Value + ", type: 3}");
                bool compressed = Regex.IsMatch(m, @"buildTarget: Standalone\n(?:    .*\n)*?    textureFormat: 25")
                                  && Regex.IsMatch(m, @"buildTarget: Android\n(?:    .*\n)*?    textureFormat: (?:48|50)");
                icon = named && compressed;
            }
            Check(icon, "APP ICON: ProjectSettings names Assets/AppIcon/app_icon.png as the default icon, and its import is compressed (BC7 Windows, ASTC Android)");
        }

        /// <summary>The engine half reads the map where the map says it should, and nothing in Core calls the
        /// engine. A source scan, cheap and exact about what it claims.</summary>
        private static void CheckUiKitWiring(string scriptsDir)
        {
            string skin = Path.Combine(scriptsDir, "UI", "EmberforgeSkin.cs");
            string hud = Path.Combine(scriptsDir, "UI", "Hud", "HudSkin.cs");
            string loader = Path.Combine(scriptsDir, "UI", "UiKit.cs");
            bool ok = File.Exists(skin) && File.Exists(hud) && File.Exists(loader)
                      && File.ReadAllText(skin).Contains("UiKit.Forge(slot)")
                      && File.ReadAllText(hud).Contains("UiKit.Hud(slot)")
                      && File.ReadAllText(loader).Contains("UiKitSwitch.Enabled");
            Check(ok, "WIRING: EmberforgeSkin.Get and HudSkin.Authored ask the kit first, and UiKit answers null while the switch is off");

            string core = File.ReadAllText(Path.Combine(scriptsDir, "Core", "Session", "UiKitSwitch.cs"));
            Check(!core.Contains("UnityEngine"), "the switch and the map are engine-free, so the harness proves them");
        }
    }
}
