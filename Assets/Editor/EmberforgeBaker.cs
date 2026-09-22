#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Emberfall.UI;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// TURNS THE TOKENS INTO THE KIT.
    ///
    /// Runs every recipe in EmberforgeArt's catalogue, writes each result to
    /// Assets/Resources/Sprites/Emberforge/&lt;slot&gt;.png, and sets that PNG's importer up as a
    /// nine-sliced sprite with the border the recipe declared. Those PNGs are what the game
    /// draws; EmberforgeSkin explains at length why the kit is baked rather than computed in a
    /// shader, and the short version is draw calls, fragments and load-time CPU on a phone.
    ///
    /// THE IMPORTER SETTINGS ARE PART OF THE ART, not a convenience. A nine-sliced sprite whose
    /// border was never set stretches its corners, which is the single easiest thing to get wrong
    /// in this pipeline and is called out in UISkin's note to whoever delivers painted art. Doing
    /// it here from the recipe means the generated kit cannot be the one that gets it wrong.
    ///
    /// IT IS IDEMPOTENT AND IT IS THE ONLY WRITER OF THAT FOLDER. Re-running it after editing
    /// Emberforge reproduces the whole kit from scratch; nothing else in the project writes there,
    /// so a file in it that this tool would not produce is a file somebody put there by hand, and
    /// the right place for a hand-made surface is the painted-override folder instead - see
    /// EmberforgeSkin's resolution order.
    ///
    /// THREE WAYS TO RUN IT, because this project has needed all three:
    ///
    ///   the menu item, for a person sitting at the editor;
    ///   Bake as an -executeMethod target, for a fresh batch-mode editor with no project lock;
    ///   a trigger file at Tools/Screenshots/.forge-trigger, for an ALREADY-OPEN editor that an
    ///   external process cannot reach any other way. That is the same mechanism and the same
    ///   reasoning as RefreshOnDemand and HubVerification next door: EditorApplication.update
    ///   ticks whether or not the window has OS focus, which nothing else about the editor does.
    /// </summary>
    [InitializeOnLoad]
    public static class EmberforgeBaker
    {
        /// <summary>Under Resources so the kit ships inside the APK without an addressable or a
        /// bundle, which is how every other sprite in this game is loaded.</summary>
        private const string OutputFolder = "Assets/Resources/Sprites/Emberforge";

        private const string TriggerPath = "Tools/Screenshots/.forge-trigger";

        static EmberforgeBaker()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string path = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (!File.Exists(path)) return;
            File.Delete(path);

            Debug.Log("EmberforgeBaker: trigger found, baking the kit.");
            Bake();
        }

        [MenuItem("Emberfall/Design System/Bake Emberforge Sprites")]
        public static void Bake()
        {
            Directory.CreateDirectory(OutputFolder);

            string[] slots = EmberforgeArt.AllSlots();
            int written = 0;

            try
            {
                // One import block around the whole run. Without it Unity reimports and reloads
                // after every single file, which for twenty-seven sprites is twenty-seven asset
                // database round trips and turns a two-second bake into a visible pause.
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < slots.Length; i++)
                {
                    ForgeSpec spec = EmberforgeArt.SpecFor(slots[i]);
                    Texture2D tex = EmberforgeArt.Render(spec);
                    byte[] png = tex.EncodeToPNG();
                    Object.DestroyImmediate(tex);

                    string file = OutputFolder + "/" + slots[i] + ".png";
                    File.WriteAllBytes(file, png);
                    written++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // The importer pass is SEPARATE and runs after the refresh, not inside the write loop.
            // AssetImporter.GetAtPath returns null for a file the asset database has not seen yet,
            // so setting a border on a PNG written moments earlier inside a StartAssetEditing
            // block silently does nothing - the border ends up zero, the sprite stretches its
            // corners, and nothing says so.
            int sliced = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                ForgeSpec spec = EmberforgeArt.SpecFor(slots[i]);
                if (ApplyImportSettings(OutputFolder + "/" + slots[i] + ".png", spec)) sliced++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log("EmberforgeBaker: baked " + written + " sprite(s) into " + OutputFolder
                    + ", " + sliced + " of them nine-sliced from their own recipe. Commit the "
                    + "folder: it is what ships, and an unbaked clone falls back to generating "
                    + "the same art on the device at load.");
        }

        /// <summary>
        /// Makes the PNG a sprite the kit can actually use, and reports rather than guesses when
        /// it cannot.
        /// </summary>
        private static bool ApplyImportSettings(string assetPath, ForgeSpec spec)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("EmberforgeBaker: '" + assetPath + "' has no TextureImporter, so its "
                    + "nine-slice border could not be set and it will stretch its corners. The file "
                    + "was written; the asset database has not picked it up.");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            // UNCOMPRESSED, and this is not a default worth leaving alone. Every surface in this
            // kit is a smooth gradient behind a hard dark outline, which is the exact content a
            // block compressor is worst at: ETC2 and ASTC both band the ramp and fringe the
            // outline. The whole kit is a few hundred kilobytes of RGBA at these sizes, and it
            // atlases into one page, so the memory this costs is not worth the artefacts.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;

            bool nineSlice = spec.BorderLeft > 0 || spec.BorderTop > 0
                          || spec.BorderRight > 0 || spec.BorderBottom > 0;

            // Unity's own order: left, bottom, right, top. Getting it wrong is invisible on a
            // symmetric border and obvious on this kit's buttons, whose bottom border carries the
            // lip and is deliberately deeper than the top.
            settings.spriteBorder = nineSlice
                ? new Vector4(spec.BorderLeft, spec.BorderBottom, spec.BorderRight, spec.BorderTop)
                : Vector4.zero;

            if (nineSlice && (spec.BorderLeft + spec.BorderRight >= spec.Width
                              || spec.BorderTop + spec.BorderBottom >= spec.Height))
            {
                Debug.LogError("EmberforgeBaker: slot '" + spec.Slot + "' declares borders that "
                    + "leave no middle region (" + spec.BorderLeft + "+" + spec.BorderRight
                    + " across " + spec.Width + ", " + spec.BorderTop + "+" + spec.BorderBottom
                    + " down " + spec.Height + "). A nine-slice needs a stretchable centre, so "
                    + "either the sprite is too small or the borders are too wide. Fix the recipe "
                    + "in EmberforgeArt.");
                return false;
            }

            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return nineSlice;
        }

        /// <summary>
        /// Says which source every slot is resolving from, without building a screen.
        ///
        /// The three sources - a painted override, the baked kit, the runtime generator - are
        /// indistinguishable once a sprite is on screen, which is the whole reason EmberforgeSkin
        /// logs its painted hits. This answers the same question for the whole catalogue at once,
        /// which is what somebody wants after dropping a folder of art in.
        /// </summary>
        [MenuItem("Emberfall/Design System/Report Emberforge Sources")]
        public static void Report()
        {
            string[] slots = EmberforgeArt.AllSlots();
            int painted = 0, baked = 0, generated = 0;
            string lines = "";

            for (int i = 0; i < slots.Length; i++)
            {
                string source;
                if (EmberforgeSkin.IsPainted(slots[i])) { source = "PAINTED"; painted++; }
                else if (EmberforgeSkin.IsBaked(slots[i])) { source = "baked"; baked++; }
                else { source = "GENERATED AT RUNTIME"; generated++; }
                lines += "\n  " + slots[i].PadRight(24) + source;
            }

            Debug.Log("Emberforge sources: " + painted + " painted, " + baked + " baked, "
                    + generated + " generated at runtime." + lines);
        }
    }
}
#endif
