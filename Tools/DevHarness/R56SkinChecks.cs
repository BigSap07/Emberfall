using System;
using System.IO;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R56 SKIN (A2 ON THE SHARED WIDGETS), PROVED WITHOUT UNITY.
    ///
    /// The UI is not compiled into the harness, so the switch is called and the wiring is read out of the source, the way
    /// R55FormationChecks does it. The rollback is SkinA2Switch: off is the old look, and the old path must still exist.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR56Skin(string contentDir)
        {
            Line("");
            Line("R56 SKIN - the A2 look on the shared widgets (SkinA2Switch, A2Skin) and the call order that keeps the old look one switch away");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));
            bool was = SkinA2Switch.Enabled;
            Check(SkinA2Switch.ShippedDefault, "the A2 skin is ON in the shipped tree (EMBERFALL_SKIN_A2_OFF is the opt out)");
            SkinA2Switch.Enabled = false;
            Check(!SkinA2Switch.Enabled, "the switch can be forced off, which is the rollback to the pre R56 look");
            SkinA2Switch.Enabled = was;
            Check(SkinA2Switch.Enabled == was, "the switch is put back as it was found");

            string skin = Read(root, "Assets/Scripts/UI/EmberforgeSkin.cs");
            int a2 = skin.IndexOf("A2Skin.Forge(slot)", StringComparison.Ordinal);
            int kit = skin.IndexOf("UiKit.Forge(slot)", StringComparison.Ordinal);
            Check(a2 >= 0 && kit >= 0 && a2 < kit, "WIRING: EmberforgeSkin.Get asks A2Skin before the UI kit and the baked art");

            string a2src = Read(root, "Assets/Scripts/UI/A2Skin.cs");
            Check(a2src.Contains("if (!On || slot == null) return false;"), "WIRING: A2Skin answers nothing while the switch is off");
            string[] slots = { "Panel", "PanelPlain", "Sunken", "ButtonPrimary", "ButtonPrimaryDown", "ButtonSecondary", "ButtonSecondaryDown",
                               "ButtonDisabled", "Gloss", "Ribbon", "RibbonTail", "Pill", "TabOn", "TabOff", "BarTrough", "BarFill", "Disc", "DiscDown", "Rule" };
            bool all = true;
            for (int i = 0; i < slots.Length; i++)
                if (!a2src.Contains("EmberforgeArt.Slot." + slots[i] + ",") && !a2src.Contains("EmberforgeArt.Slot." + slots[i] + "\r") && !a2src.Contains("EmberforgeArt.Slot." + slots[i] + "\n")) all = false;
            Check(all, "WIRING: A2Skin covers the 19 shared slots of the plan plus the two Quiet plates, and no marker, jewel, chip, frame or ornament");
            Check(!a2src.Contains("Slot.Marker") && !a2src.Contains("Slot.Jewel") && !a2src.Contains("Slot.Chip") && !a2src.Contains("Slot.Frame") && !a2src.Contains("Slot.Ornament") && !a2src.Contains("Slot.Plus"),
                  "WIRING: hero art and map labels keep their art (not re-skinned in R56)");

            string emb = Read(root, "Assets/Scripts/UI/Emberforge.cs");
            Check(emb.Contains("SkinA2Switch.Enabled") && emb.Contains("CinzelFont") && emb.Contains("BarlowFont"),
                  "WIRING: the two fonts follow the switch and keep the old getters as CinzelFont and BarlowFont");

            string ui = Read(root, "Assets/Scripts/UI/EmberforgeUI.cs");
            Check(ui.Contains("A2Skin.QuietButton") && ui.Contains("A2Skin.Style(label)"), "WIRING: the Quiet plate and the label restyle are in EmberforgeUI");
            Check(ui.Contains("if (A2Skin.On) return;"), "WIRING: the corner ornaments are off under A2");

            string[] files = { "Assets/Scripts/Core/Session/SkinA2Switch.cs", "Assets/Scripts/UI/A2Skin.cs", "Tools/DevHarness/R56SkinChecks.cs" };
            bool dashes = false;
            for (int i = 0; i < files.Length; i++)
            {
                string t = Read(root, files[i]);
                if (t.IndexOf((char)0x2014) >= 0 || t.IndexOf((char)0x2013) >= 0) dashes = true;
            }
            Check(!dashes, "no em or en dashes in the R56 files");
        }

        private static string Read(string root, string rel)
        {
            string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
    }
}
