using System;
using UnityEngine;

namespace Emberfall.Game
{
    /// <summary>
    /// The player's audio settings: three volume sliders and one mute, kept on the device in
    /// PlayerPrefs like SummonSettings, never in the save. A volume is stored as a whole percent so
    /// the slider and the stored value can never disagree by a rounding step.
    ///
    /// Defaults are music 60, sfx 80, ui 70 (R10-AUDIO). AudioService.Init overwrites them from
    /// audio.json's "volumes", so the numbers live in the content file; the constants here are only
    /// what a build with no audio.json falls back to.
    ///
    /// Values are read from PlayerPrefs once and cached, because the service reads them every frame.
    /// </summary>
    public static class AudioPrefs
    {
        private const string MusicKey = "Emberfall.Audio.Music";
        private const string SfxKey = "Emberfall.Audio.Sfx";
        private const string UiKey = "Emberfall.Audio.Ui";
        private const string MutedKey = "Emberfall.Audio.Muted";

        public static int DefaultMusic = 60;
        public static int DefaultSfx = 80;
        public static int DefaultUi = 70;

        /// <summary>Raised after any setting changes, so live music can follow a slider drag.</summary>
        public static event Action Changed;

        private static bool _loaded;
        private static bool _hasMusic, _hasSfx, _hasUi;
        private static int _music, _sfx, _ui;
        private static bool _muted;

        /// <summary>Whole percent, 0 to 100.</summary>
        public static int MusicPercent
        {
            get { Load(); return _hasMusic ? _music : DefaultMusic; }
            set { Load(); int v = Clamp(value); if (_hasMusic && v == _music) return; _music = v; _hasMusic = true; Save(MusicKey, v); }
        }

        public static int SfxPercent
        {
            get { Load(); return _hasSfx ? _sfx : DefaultSfx; }
            set { Load(); int v = Clamp(value); if (_hasSfx && v == _sfx) return; _sfx = v; _hasSfx = true; Save(SfxKey, v); }
        }

        public static int UiPercent
        {
            get { Load(); return _hasUi ? _ui : DefaultUi; }
            set { Load(); int v = Clamp(value); if (_hasUi && v == _ui) return; _ui = v; _hasUi = true; Save(UiKey, v); }
        }

        public static bool Muted
        {
            get { Load(); return _muted; }
            set { Load(); _muted = value; Save(MutedKey, value ? 1 : 0); Flush(); }
        }

        /// <summary>0 to 1, and 0 while muted.</summary>
        public static float Music { get { return Muted ? 0f : MusicPercent / 100f; } }
        public static float Sfx { get { return Muted ? 0f : SfxPercent / 100f; } }
        public static float Ui { get { return Muted ? 0f : UiPercent / 100f; } }

        /// <summary>Forgets the cached values so the next read goes back to PlayerPrefs. For the
        /// capture tools and the rollback check, which change the stored values from outside.</summary>
        public static void Reload()
        {
            _loaded = false;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _hasMusic = PlayerPrefs.HasKey(MusicKey);
            _hasSfx = PlayerPrefs.HasKey(SfxKey);
            _hasUi = PlayerPrefs.HasKey(UiKey);
            _music = Clamp(PlayerPrefs.GetInt(MusicKey, DefaultMusic));
            _sfx = Clamp(PlayerPrefs.GetInt(SfxKey, DefaultSfx));
            _ui = Clamp(PlayerPrefs.GetInt(UiKey, DefaultUi));
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        }

        /// <summary>Writes pending values to disk. A slider calls this when it is let go, not on every
        /// step of a drag; mute flushes itself.</summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        private static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            if (Changed != null) Changed();
        }

        private static int Clamp(int percent)
        {
            return percent < 0 ? 0 : (percent > 100 ? 100 : percent);
        }
    }
}
