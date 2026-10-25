#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.UI;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// THE BUTTON HIERARCHY CHECK (R53): counts the ember orange buttons a capture shows.
    ///
    /// The rule (DESIGN-ART-BIBLE.md, section 6): at most one Primary button is on a screen at once.
    /// PlayModeScreenshot calls Report when it requests a PNG, so every capture carries one
    /// BUTTONCHECK line in the editor log, and Tools/button-check-table.ps1 turns those lines into the
    /// table a run reads. Editor only: this file is wrapped in UNITY_EDITOR, so no player build holds it
    /// and a release build cannot fail on it.
    ///
    /// What counts as a Primary: an active, interactable EmberforgeButtonSkin whose kind is Primary,
    /// and any other active, interactable Button whose face is painted ember (the few hand-built
    /// buttons the kit does not own). A Primary whose centre is off screen (a row scrolled out of
    /// its list) is not counted, because the player does not see it. Disabled buttons never count: their
    /// art is grey.
    /// </summary>
    public static class ButtonHierarchyCheck
    {
        public sealed class Result
        {
            public int Primary;
            public int Secondary;
            public int Quiet;
            public int Disabled;
            public int LegacyOrange;
            public readonly List<string> PrimaryNames = new List<string>();
        }

        public static Result Scan()
        {
            Result r = new Result();
            EmberforgeButtonSkin[] skins = Object.FindObjectsByType<EmberforgeButtonSkin>(FindObjectsSortMode.None);
            for (int i = 0; i < skins.Length; i++)
            {
                EmberforgeButtonSkin skin = skins[i];
                if (skin == null || !OnScreen(skin.transform as RectTransform)) continue;
                if (!skin.IsInteractable) { r.Disabled++; continue; }
                switch (skin.Kind)
                {
                    case ForgeButtonKind.Primary:
                        r.Primary++;
                        r.PrimaryNames.Add(Describe(skin.gameObject));
                        break;
                    case ForgeButtonKind.Quiet: r.Quiet++; break;
                    default: r.Secondary++; break;   // Secondary, and a Disabled kind that has since been enabled
                }
            }

            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null || !b.interactable) continue;
                if (b.GetComponent<EmberforgeButtonSkin>() != null) continue;
                Image face = b.targetGraphic as Image;
                if (face == null || !IsEmber(face.color)) continue;
                if (!OnScreen(b.transform as RectTransform)) continue;
                r.LegacyOrange++;
                r.Primary++;
                r.PrimaryNames.Add("legacy:" + Describe(b.gameObject));
            }
            return r;
        }

        /// <summary>Logs the BUTTONCHECK line for one capture. The file name is the key the table groups by.</summary>
        public static void Report(string captureFile)
        {
            Result r = Scan();
            StringBuilder names = new StringBuilder();
            for (int i = 0; i < r.PrimaryNames.Count; i++)
            {
                if (i > 0) names.Append(" | ");
                names.Append(r.PrimaryNames[i]);
            }
            string line = "BUTTONCHECK " + captureFile + " primary=" + r.Primary + " secondary=" + r.Secondary
                          + " quiet=" + r.Quiet + " disabled=" + r.Disabled + " legacy=" + r.LegacyOrange
                          + " names=" + names;
            if (r.Primary > 1) Debug.LogWarning(line + " MORE THAN ONE PRIMARY");
            else Debug.Log(line);
        }

        private static bool IsEmber(Color c)
        {
            // Ember E8642F and EmberDeep A83C16, with a little room for the tint a pressed face gets.
            return Near(c, UITheme.Ember) || Near(c, UITheme.EmberDeep);
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.06f && Mathf.Abs(a.g - b.g) < 0.06f && Mathf.Abs(a.b - b.b) < 0.06f && a.a > 0.5f;
        }

        private static bool OnScreen(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            CanvasGroup group = rt.GetComponentInParent<CanvasGroup>();
            if (group != null && group.alpha < 0.02f) return false;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 centre = (corners[0] + corners[2]) * 0.5f;
            return centre.x >= 0f && centre.x <= Screen.width && centre.y >= 0f && centre.y <= Screen.height;
        }

        private static string Describe(GameObject go)
        {
            Text label = go.GetComponentInChildren<Text>();
            string text = label != null ? label.text : "";
            if (text != null && text.Length > 24) text = text.Substring(0, 24);
            string owner = go.transform.parent != null ? go.transform.parent.name : "";
            return go.name + "[" + (text ?? "").Replace("\n", " ") + "]@" + owner;
        }
    }
}
#endif
