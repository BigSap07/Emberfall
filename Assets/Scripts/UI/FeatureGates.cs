using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// THE UI'S SIDE OF THE UNLOCK TABLE (DESIGN-ONBOARDING.md, "Locked, not hidden").
    ///
    /// Decides nothing. Every door the UI draws passes its FeatureEntryPoints id here, this looks
    /// the feature up and asks FeatureGate, and a locked door says its one line - "UNLOCKS AT 1-6"
    /// - and nothing else. The router asks here for every screen it shows, which is the door a
    /// deep link would use.
    /// </summary>
    public static class FeatureGates
    {
#if UNITY_EDITOR
        /// <summary>
        /// Editor captures of a feature's own screen photograph that screen on a fresh scratch
        /// profile that has cleared nothing, so without this every Forge or Recital capture would
        /// be a toast over the Hub. Every capture tool already runs with the first run suppressed
        /// (EditorScratchIdentity), and that switch opens the gate too; PlayModeScreenshot also
        /// sets this for its own targets and clears it for the onboarding ones, which leave the
        /// first run on and are what photograph the locks. Never compiled into a player.
        /// </summary>
        public static bool CaptureBypass;

        /// <summary>R26: keeps the gate live for a capture even with the first run suppressed, so a target can
        /// photograph the locks on a profile it seeded itself (Marketplace, fresh profile with locks).</summary>
        public static bool CaptureForceLocks;
#endif

#if EMBERFALL_PLAYLOG
        /// <summary>R27: the cloud smoke test (-emberfallSmoke) opens the Friends and Guild screens on whatever profile
        /// the device has, locked or not. It only opens a page; it grants nothing and NoteOpened still checks the real
        /// unlock. Compiled only with EMBERFALL_PLAYLOG.</summary>
        public static bool SmokeBypass;
#endif

        private static bool Bypassed
        {
            get
            {
#if EMBERFALL_PLAYLOG
                if (SmokeBypass) return true;
#endif
#if UNITY_EDITOR
                if (CaptureForceLocks) return false;
                return CaptureBypass || UnityEditor.EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false);
#else
                return false;
#endif
            }
        }

        public static bool Allows(GameApp app, string entryId)
        {
            if (Bypassed || app == null || app.Session == null) return true;
            return FeatureEntryPoints.Allows(app.Session.Content, app.Session.Profile, entryId);
        }

        /// <summary>The lock line for this entry, or "" when it is open.</summary>
        public static string LockLine(GameApp app, string entryId)
        {
            if (Allows(app, entryId)) return "";
            FeatureEntryPoint entry = FeatureEntryPoints.Find(entryId);
            return entry == null ? "" : FeatureGate.LockLine(app.Session.Content, entry.Feature);
        }

        /// <summary>The tap on a door: true lets it through; false has already said why.</summary>
        public static bool TryEnter(GameApp app, string entryId)
        {
            if (Allows(app, entryId)) return true;
            if (app.Audio != null) app.Audio.Locked();
            app.Notify(LockLine(app, entryId));
            return false;
        }

        public static string ScreenEntry(ScreenId id)
        {
            return "screen:" + id;
        }

        /// <summary>
        /// Records that the feature behind this screen has been opened, which retires its one-time
        /// "new" dot. Only once the feature is unlocked, so a bypassed capture cannot spend it.
        /// True when it recorded something, so the caller knows to save.
        /// </summary>
        public static bool NoteOpened(GameApp app, ScreenId id)
        {
            if (app == null || app.Session == null) return false;
            Feature feature;
            if (!FeatureEntryPoints.TryFeatureOfScreen(id.ToString(), out feature)) return false;
            if (FeatureGate.UnlockStage(app.Session.Content, feature) == null) return false;
            if (!FeatureGate.IsUnlocked(app.Session.Content, app.Session.Profile, feature)) return false;
            return Onboarding.MarkSeen(app.Session.Profile, FeatureGate.OpenedKey(feature));
        }
    }

    /// <summary>
    /// WHAT A LOCKED DOOR LOOKS LIKE, and the one-time dot on a door that has just opened.
    ///
    /// A locked door keeps its place and its name: the label goes grey, a padlock sits beside it
    /// and a gold tag says "UNLOCKS AT 1-6". It stays tappable, because tapping it is how the
    /// player hears that line. An unlocked door that has not been opened since it unlocked carries
    /// a small ember dot until it is.
    ///
    /// LAYOUT ONLY. Where the tag and the padlock go is passed in by the door's builder; colours
    /// and sizes come from UITheme and HubStyle, so the designer's art pass restyles every lock in
    /// the game from here.
    /// </summary>
    public sealed class FeatureLockView : MonoBehaviour
    {
        public enum TagSide { Above, Below, Right }

        public const float PadlockSize = 34f;
        public const float TagHeight = 34f;
        public const float TagWidth = 210f;
        public const float DotSize = 22f;

        private static readonly List<FeatureLockView> Live = new List<FeatureLockView>();

        public string EntryId { get; private set; }
        private Text _label;
        private Color _labelColor;
        private RectTransform _padlock;
        private RectTransform _tag;
        private Text _tagText;
        private RectTransform _dot;

        /// <summary>
        /// Attaches the lock dress to a door. <paramref name="label"/> is the text that greys,
        /// <paramref name="padlockAt"/> is the padlock's centre in the host's own space (anchored
        /// at its centre), and the tag sits on <paramref name="side"/> of the host.
        /// </summary>
        public static FeatureLockView Attach(RectTransform host, string entryId, Text label,
                                             Vector2 padlockAt, TagSide side)
        {
            return Attach(host, entryId, label, padlockAt, side, true);
        }

        /// <summary>The same, optionally without the tag: a row of doors into one feature (the
        /// hero page's six armor slots) carries one tag between them, not six.</summary>
        public static FeatureLockView Attach(RectTransform host, string entryId, Text label,
                                             Vector2 padlockAt, TagSide side, bool withTag)
        {
            FeatureLockView view = host.gameObject.AddComponent<FeatureLockView>();
            view.EntryId = entryId;
            view._label = label;
            view._labelColor = label != null ? label.color : Color.white;

            view._padlock = UIBuilder.Node("Padlock", host);
            UIBuilder.Box(view._padlock, padlockAt.x, padlockAt.y, PadlockSize, PadlockSize, new Vector2(0.5f, 0.5f));
            Image plate = view._padlock.gameObject.AddComponent<Image>();
            plate.sprite = TextureLab.Circle(64);
            plate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            plate.raycastTarget = false;
            Image glyph = UIBuilder.Img("Glyph", view._padlock, TextureLab.Padlock(64), UITheme.Gold);
            UIBuilder.FillParent(glyph.rectTransform, 5f);
            glyph.raycastTarget = false;

            if (withTag) BuildTag(view, host, side);
            BuildDot(view, host);
            view.Apply(false, "", false);
            return view;
        }

        private static void BuildTag(FeatureLockView view, RectTransform host, TagSide side)
        {
            view._tag = UIBuilder.Node("LockTag", host);
            switch (side)
            {
                case TagSide.Below:
                    UIBuilder.Box(view._tag, 0f, -TagHeight - Emberforge.Space4, TagWidth, TagHeight, new Vector2(0.5f, 0f));
                    break;
                case TagSide.Right:
                    UIBuilder.Box(view._tag, TagWidth + Emberforge.Space8, 0f, TagWidth, TagHeight, new Vector2(1f, 0.5f));
                    break;
                default:
                    UIBuilder.Box(view._tag, 0f, TagHeight + Emberforge.Space4, TagWidth, TagHeight, new Vector2(0.5f, 1f));
                    break;
            }
            Image tagPlate = view._tag.gameObject.AddComponent<Image>();
            tagPlate.sprite = TextureLab.RoundedRect(Emberforge.ChipRadius);
            tagPlate.type = Image.Type.Sliced;
            tagPlate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            tagPlate.raycastTarget = false;
            Image tagRim = UIBuilder.Img("Rim", view._tag, TextureLab.RoundedOutline(Emberforge.ChipRadius, 2), UITheme.Gold);
            tagRim.type = Image.Type.Sliced;
            UIBuilder.FillParent(tagRim.rectTransform, 0f);
            tagRim.raycastTarget = false;
            view._tagText = EmberforgeUI.Label("Text", view._tag, "", UITheme.FontTiny - 2,
                                               Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(view._tagText.rectTransform, 2f);
            view._tagText.raycastTarget = false;
        }

        private static void BuildDot(FeatureLockView view, RectTransform host)
        {
            view._dot = UIBuilder.Node("NewDot", host);
            UIBuilder.Box(view._dot, DotSize * 0.25f, DotSize * 0.25f, DotSize, DotSize, new Vector2(1f, 1f));
            Image dot = view._dot.gameObject.AddComponent<Image>();
            dot.sprite = TextureLab.Circle(64);
            dot.color = UITheme.Ember;
            dot.raycastTarget = false;
            Image dotRim = UIBuilder.Img("Rim", view._dot, TextureLab.Ring(64, Emberforge.OutlineWeight * 2), Emberforge.Outline);
            UIBuilder.FillParent(dotRim.rectTransform, 0f);
            dotRim.raycastTarget = false;
        }

        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        private void OnDestroy() { Live.Remove(this); }

        public void Refresh(GameApp app)
        {
            if (app == null || app.Session == null) return;
            string line = FeatureGates.LockLine(app, EntryId);
            bool dot = false;
            FeatureEntryPoint entry = FeatureEntryPoints.Find(EntryId);
            if (line.Length == 0 && entry != null)
                dot = FeatureGate.ShowsNewDot(app.Session.Content, app.Session.Profile, entry.Feature);
            Apply(line.Length > 0, line, dot);
        }

        private void Apply(bool locked, string line, bool dot)
        {
            if (_label != null) _label.color = locked ? Emberforge.TextQuiet : _labelColor;
            if (_padlock != null) _padlock.gameObject.SetActive(locked);
            if (_tag != null) _tag.gameObject.SetActive(locked);
            if (_tagText != null) _tagText.text = line;
            if (_dot != null) _dot.gameObject.SetActive(dot);
            if (_tag != null) _tag.SetAsLastSibling();
            if (_padlock != null) _padlock.SetAsLastSibling();
            if (_dot != null) _dot.SetAsLastSibling();
        }

        /// <summary>Repaints every lock under <paramref name="root"/>, or every live one when root
        /// is null. Screens call it from OnShow, and the router after a stage clear changes the
        /// table's answer.</summary>
        public static void RefreshAll(GameApp app, Transform root)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                FeatureLockView view = Live[i];
                if (view == null) { Live.RemoveAt(i); continue; }
                if (root != null && !view.transform.IsChildOf(root)) continue;
                view.Refresh(app);
            }
        }
    }
}
