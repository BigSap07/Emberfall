using System;
using System.IO;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R57 HEROES: the star shape (step 0) and, further down, the A2 Heroes roster, proved without Unity.
    /// The UI is not compiled into the harness, so geometry is called directly (StarShape is plain C# in Core)
    /// and the wiring is read out of the source, the way R55FormationChecks and R56SkinChecks do it.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR57Heroes(string contentDir)
        {
            Line("");
            Line("R57 HEROES - the five point star shape (StarShape, TextureLab.Star) and the A2 Heroes roster");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));

            // Step 0: the old sprite was a pentagon (inner corner radius over tip radius was 0.81).
            float tip = StarShape.EdgeRadius(0f);
            float inner = StarShape.EdgeRadius(36f);
            float ratio = inner / tip;
            Check(StarShape.CornerCount == 10, "STAR: ten corners, five tips and five inner corners");
            Check(ratio > 0.30f && ratio < 0.55f, "STAR: the inner corner sits at " + ratio.ToString("0.000") + " of the tip radius, a star (a pentagon is 0.809)");
            bool symmetric = true;
            for (int k = 1; k < 5; k++)
            {
                if (Math.Abs(StarShape.EdgeRadius(72f * k) - tip) > 0.01f) symmetric = false;
                if (Math.Abs(StarShape.EdgeRadius(36f + 72f * k) - inner) > 0.01f) symmetric = false;
            }
            Check(symmetric, "STAR: the five tips match and the five inner corners match (five fold symmetry)");
            Check(StarShape.Distance(0f, StarShape.CentreY) < 0f, "STAR: the centre is inside");
            Check(StarShape.Distance(0.95f, 0.95f) > 0f && StarShape.Distance(-0.95f, -0.95f) > 0f, "STAR: the square's corners are outside");
            Check(StarShape.Distance(0f, StarShape.CentreY + tip * 0.97f) < 0f, "STAR: the top tip is filled just short of its end");
            // Tips up, bounding box centred: the top tip and the two lower tips sit the same distance from the square's edge.
            float top = StarShape.CornerY(0);
            float low = StarShape.CornerY(6);
            Check(Math.Abs(top + low) < 0.01f && top > 0.85f && top < 1.0f, "STAR: tips up, centred on its bounding box and inside the -1..1 square");

            string lab = Read(root, "Assets/Scripts/UI/TextureLab.cs");
            Check(lab.Contains("StarShape.Distance") && !lab.Contains("StarSdf"), "WIRING: TextureLab.Star draws StarShape, the pentagon distance field is gone");
            Check(lab.Contains("StarSuper = 4") && lab.Contains("StarEmpty"), "WIRING: the star is supersampled and has an empty outline twin");
            string widgets = Read(root, "Assets/Scripts/UI/Widgets.cs");
            string a2kit = Read(root, "Assets/Scripts/UI/A2Kit.cs");
            Check(a2kit.Contains("StarGold = Hex(0xF4A62A)") && a2kit.Contains("public static RectTransform Stars("), "WIRING: the shared A2Kit.Stars row in star gold #F4A62A");
            Check(widgets.Contains("A2Kit.StarGold") && widgets.Contains("TextureLab.StarEmpty"), "WIRING: Widgets.StarRow uses the same gold and outline under A2");
            string formation = Read(root, "Assets/Scripts/UI/Screens/FormationScreenA2.cs");
            Check(formation.Contains("A2Kit.Stars("), "WIRING: the Formation screen draws its stars through A2Kit.Stars");

            string[] files = { "Assets/Scripts/Core/Session/StarShape.cs", "Tools/DevHarness/R57HeroesChecks.cs" };
            bool dashes = false;
            for (int i = 0; i < files.Length; i++)
            {
                string t = Read(root, files[i]);
                if (t.IndexOf((char)0x2014) >= 0 || t.IndexOf((char)0x2013) >= 0) dashes = true;
            }
            Check(!dashes, "no em or en dashes in the R57 files");
        }
    }
}
