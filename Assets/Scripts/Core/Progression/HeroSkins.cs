using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// HERO SKINS (R24, Docs/DESIGN-STORE.md): the rules, engine free. A skin is a look for ONE hero plus a small stat
    /// bonus on that hero only (SkinsConfig). A hero wears at most one skin (HeroInstance.EquippedSkinId is one field),
    /// so bonuses cannot stack, and a skin can be worn only by the hero it was made for and only once the player owns it
    /// (PlayerProfile.OwnedSkins).
    ///
    /// WHERE A FIGHT READS IT. CombatSimulation.BuildUnits calls ApplyWorn once per player unit, after the stat line is
    /// built, and nowhere else in the simulation knows skins exist. The Hero screen and the team power call the same
    /// ApplyWorn, so the number a player reads is by construction the number the fight uses (CheckSkinBuffMatchesSimulation).
    /// It is skipped for a fight against a snapshot opponent (The Proving, friend duels), so two identical lines can never
    /// differ by a purchase, and a request with no profile (the guild dungeon's scratch heroes, every synthetic harness
    /// request) wears nothing: the replays that are recorded cannot read a skin.
    ///
    /// NOTHING HERE DRAWS: a skin is a fixed bonus read from skins.json, bought for a fixed price (StoreConfig derives its
    /// offer), or granted by a reward through PurchaseGrants.Apply.
    /// </summary>
    public static class HeroSkins
    {
        /// <summary>The skin this hero wears, or null: the switch is off, nothing is worn, the profile does not own it, it
        /// is not this hero's, it left skins.json, or it is a test skin in a release build.</summary>
        public static SkinDef Worn(PlayerProfile profile, HeroInstance hero, ContentDatabase content)
        {
            if (!SkinsSwitch.Enabled || profile == null || hero == null || content == null) return null;
            if (string.IsNullOrEmpty(hero.EquippedSkinId) || !profile.OwnsSkin(hero.EquippedSkinId)) return null;
            SkinDef skin = content.Skins.Skin(hero.EquippedSkinId);
            return skin != null && skin.HeroId == hero.DefId ? skin : null;
        }

        /// <summary>Adds a skin's bonus to a stat line in place: each percent of the line as it stands, rounded the way
        /// StatMath rounds (half away from zero). A null skin is an exact no-op.</summary>
        public static void Apply(UnitStats stats, SkinDef skin)
        {
            if (stats == null || skin == null) return;
            if (skin.HpPct > 0.0) stats.MaxHp = Round(stats.MaxHp * (1f + (float)(skin.HpPct / 100.0)));
            if (skin.AttackPct > 0.0) stats.Attack = Round(stats.Attack * (1f + (float)(skin.AttackPct / 100.0)));
            if (skin.DefensePct > 0.0) stats.Defense = Round(stats.Defense * (1f + (float)(skin.DefensePct / 100.0)));
        }

        /// <summary>Finds the hero's worn skin and applies it. Returns the skin, or null when nothing was applied. THE ONE
        /// CALL the fight, the Hero screen and the team power all make.</summary>
        public static SkinDef ApplyWorn(UnitStats stats, PlayerProfile profile, HeroInstance hero, ContentDatabase content)
        {
            SkinDef skin = Worn(profile, hero, content);
            Apply(stats, skin);
            return skin;
        }

        private static float Round(float v)
        {
            return (float)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        // ------------------------------------------------------------------ owning and wearing

        /// <summary>The skins of this hero the player owns, in file order.</summary>
        public static List<SkinDef> OwnedFor(PlayerProfile profile, string heroDefId, ContentDatabase content)
        {
            List<SkinDef> owned = new List<SkinDef>();
            if (profile == null || content == null) return owned;
            List<SkinDef> all = content.Skins.SkinsOf(heroDefId);
            for (int i = 0; i < all.Count; i++) if (profile.OwnsSkin(all[i].Id)) owned.Add(all[i]);
            return owned;
        }

        /// <summary>Puts an owned skin on its own hero, replacing whatever the hero wore: one skin at a time, so nothing
        /// stacks. False (nothing changes) for a skin the player does not own, one that is not this hero's, an unknown one,
        /// or with the switch off.</summary>
        public static bool Equip(GameSession session, HeroInstance hero, string skinId)
        {
            if (session == null || hero == null || !SkinsSwitch.Enabled || string.IsNullOrEmpty(skinId)) return false;
            if (session.Profile.FindHeroByUid(hero.Uid) != hero) return false;
            SkinDef skin = session.Content.Skins.Skin(skinId);
            if (skin == null || skin.HeroId != hero.DefId || !session.Profile.OwnsSkin(skinId)) return false;
            hero.EquippedSkinId = skinId;
            return true;
        }

        /// <summary>Takes the hero's skin off. The skin stays owned.</summary>
        public static bool Unequip(GameSession session, HeroInstance hero)
        {
            if (session == null || hero == null || string.IsNullOrEmpty(hero.EquippedSkinId)) return false;
            if (session.Profile.FindHeroByUid(hero.Uid) != hero) return false;
            hero.EquippedSkinId = "";
            return true;
        }

        // ------------------------------------------------------------------ the look

        /// <summary>A copy of the hero's look with the skin's art folded in, for the battle rig: the skin's own battle sprite
        /// when it has one, else the hero's own sprite with the skin's hue as a tint (the TEST skins), and the palette of a
        /// hero drawn procedurally rotated by the same hue. The fight's unit carries this so the battle screen only plays
        /// what the record says.</summary>
        public static VisualDef VisualFor(VisualDef baseVisual, SkinDef skin)
        {
            if (baseVisual == null || skin == null) return baseVisual;
            VisualDef v = baseVisual.Copy();
            if (!string.IsNullOrEmpty(skin.Sprite)) { v.Sprite = skin.Sprite; v.SpriteStatus = ""; }
            if (!string.IsNullOrEmpty(skin.Portrait)) v.Portrait = skin.Portrait;
            if (skin.Hue != 0f)
            {
                v.Primary = HueShiftHex(v.Primary, skin.Hue);
                v.Secondary = HueShiftHex(v.Secondary, skin.Hue);
                v.Accent = HueShiftHex(v.Accent, skin.Hue);
                v.SkinTint = TintHex(skin.Hue);
            }
            return v;
        }

        /// <summary>The colour a sprite or portrait is multiplied by to read as hue-shifted: the hue at a gentle saturation,
        /// full value. "RRGGBB".</summary>
        public static string TintHex(float hueDegrees)
        {
            return FromHsv(hueDegrees, 0.55f, 1f);
        }

        /// <summary>Rotates the hue of an "RRGGBB" colour. Anything that does not parse is returned unchanged.</summary>
        public static string HueShiftHex(string hex, float degrees)
        {
            if (string.IsNullOrEmpty(hex)) return hex;
            string h = hex.TrimStart('#');
            if (h.Length < 6) return hex;
            int r, g, b;
            if (!int.TryParse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out r)
                || !int.TryParse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out g)
                || !int.TryParse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out b))
                return hex;
            float fr = r / 255f, fg = g / 255f, fb = b / 255f;
            float max = Math.Max(fr, Math.Max(fg, fb)), min = Math.Min(fr, Math.Min(fg, fb));
            float delta = max - min;
            float hue = 0f;
            if (delta > 0f)
            {
                if (max == fr) hue = 60f * (((fg - fb) / delta) % 6f);
                else if (max == fg) hue = 60f * ((fb - fr) / delta + 2f);
                else hue = 60f * ((fr - fg) / delta + 4f);
            }
            float sat = max <= 0f ? 0f : delta / max;
            return FromHsv(hue + degrees, sat, max);
        }

        private static string FromHsv(float hue, float sat, float val)
        {
            hue = ((hue % 360f) + 360f) % 360f;
            float c = val * sat;
            float x = c * (1f - Math.Abs((hue / 60f) % 2f - 1f));
            float m = val - c;
            float r, g, b;
            if (hue < 60f) { r = c; g = x; b = 0f; }
            else if (hue < 120f) { r = x; g = c; b = 0f; }
            else if (hue < 180f) { r = 0f; g = c; b = x; }
            else if (hue < 240f) { r = 0f; g = x; b = c; }
            else if (hue < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }
            int ri = (int)Math.Round((r + m) * 255f), gi = (int)Math.Round((g + m) * 255f), bi = (int)Math.Round((b + m) * 255f);
            return ri.ToString("X2") + gi.ToString("X2") + bi.ToString("X2");
        }
    }
}
