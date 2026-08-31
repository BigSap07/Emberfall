#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Forces the Game view to a fixed pixel resolution, so a verification capture is taken at an
    /// aspect ratio somebody chose rather than at whatever the editor window happens to be.
    ///
    /// This matters more than it sounds. Every layout decision on the Hub is expressed against a
    /// CanvasScaler in MatchWidthOrHeight mode, so the LOGICAL canvas size is a function of the
    /// screen's aspect - logical height is sqrt(1920*1080 / aspect). At 16:9 that is 1080 units; at
    /// a 2.78:1 editor window it is 864. A layout can be clean in the editor and clip badly on a
    /// phone purely because the editor window was dragged wider at some point, which is exactly
    /// what happened on this branch: the Game view drifted from 2.25:1 to 2.78:1 between captures
    /// and the bottom row started clipping without a single line of layout code changing.
    ///
    /// Unity exposes none of this publicly - GameViewSizes, GameViewSize, GameViewSizeGroup and
    /// GameView are all internal - so this is reflection against internal API and WILL break on
    /// some future Unity. It is written to fail loudly and return false rather than silently leave
    /// the Game view at the wrong size, because a capture at an unknown aspect is worse than no
    /// capture: it looks like evidence.
    /// </summary>
    public static class GameViewSize
    {
        private const string Label = "Emberfall Verify";

        private static Type Ed(string name)
        {
            return typeof(Editor).Assembly.GetType(name);
        }

        public static bool Apply(int width, int height)
        {
            try
            {
                Type sizesType = Ed("UnityEditor.GameViewSizes");
                Type sizeType = Ed("UnityEditor.GameViewSize");
                Type sizeKind = Ed("UnityEditor.GameViewSizeType");
                Type groupType = Ed("UnityEditor.GameViewSizeGroup");
                Type viewType = Ed("UnityEditor.GameView");
                if (sizesType == null || sizeType == null || sizeKind == null || groupType == null || viewType == null)
                {
                    Debug.LogError("GameViewSize: internal Game view types not found on this Unity version.");
                    return false;
                }

                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                object group = sizesType.GetProperty("currentGroup").GetValue(sizes);

                MethodInfo getTotal = groupType.GetMethod("GetTotalCount");
                MethodInfo getSize = groupType.GetMethod("GetGameViewSize");

                int index = -1;
                int total = (int)getTotal.Invoke(group, null);
                for (int i = 0; i < total; i++)
                {
                    object entry = getSize.Invoke(group, new object[] { i });
                    int w = (int)sizeType.GetProperty("width").GetValue(entry);
                    int h = (int)sizeType.GetProperty("height").GetValue(entry);
                    if (w == width && h == height) { index = i; break; }
                }

                if (index < 0)
                {
                    ConstructorInfo ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                    // GameViewSizeType.FixedResolution == 1
                    object created = ctor.Invoke(new object[] { Enum.ToObject(sizeKind, 1), width, height, Label });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { created });
                    index = (int)getTotal.Invoke(group, null) - 1;
                }

                EditorWindow view = EditorWindow.GetWindow(viewType, false, null, false);
                PropertyInfo selected = viewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selected == null)
                {
                    Debug.LogError("GameViewSize: selectedSizeIndex not found; cannot set the size.");
                    return false;
                }
                selected.SetValue(view, index);
                view.Repaint();

                Debug.Log("GameViewSize: Game view set to " + width + "x" + height
                        + " (aspect " + (width / (float)height).ToString("0.000") + ")");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("GameViewSize: failed to set " + width + "x" + height + " - " + e.Message);
                return false;
            }
        }
    }
}
#endif
