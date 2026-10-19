using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;

namespace Emberfall.Battle
{
    /// <summary>
    /// WHAT THE R15 "v2" LOOK CHANGES ABOUT WHO A UNIT LOOKS LIKE, as data and arithmetic.
    ///
    /// Three presentation decisions, all of them about the picture and none about the fight:
    ///
    ///   Stand-ins.  A unit whose art is not drawn yet (spriteStatus "pending") draws the flat
    ///               mannequin. In v2 a unit named in stage-v2.json "standIns" draws a finished
    ///               enemy's art instead, as that enemy looks (its sprite, its fractions, its
    ///               scale). The unit's id, stats and fight are untouched: this only chooses
    ///               which VisualDef the rig is built from.
    ///   Facing.     The sprite renders FLIPPED AS DRAWN when UnitRig.SpriteFlipped says true and
    ///               MIRRORED when it says false (the quad is seen from behind), and every hero
    ///               sheet is drawn facing right, every enemy sheet facing left. A hero whose
    ///               spriteMirror is wrong for that is listed in "faceFlip" and its mirror is
    ///               inverted here, and the mannequin (drawn facing right, with its eye on the
    ///               right) is mirrored on both sides so it faces its opponent.
    ///   Scale.      Every unit is drawn BattleFraming.UnitScaleV2 larger (a unit named in "scaleMul"
    ///               by its own factor on top).
    ///
    /// The file is NOT part of ContentVersion: ContentDatabase never reads it, so editing it
    /// retires no recorded replay, and the three fingerprints cannot move. heroes.json and
    /// enemies.json are never edited for any of this.
    ///
    /// Free of UnityEngine so the harness compiles it and proves the map without the editor.
    /// </summary>
    public sealed class StagePresentation
    {
        /// <summary>The file, relative to Resources.</summary>
        public const string ResourcePath = "Content/stage-v2";

        /// <summary>A unit's def id to the enemy def id whose art it shows in v2.</summary>
        public readonly Dictionary<string, string> StandIns = new Dictionary<string, string>();

        /// <summary>Def ids whose spriteMirror is inverted in v2.</summary>
        public readonly HashSet<string> FaceFlip = new HashSet<string>();

        /// <summary>A unit's own multiplier on the v2 scale, by def id. One unit is wider than the
        /// formation was solved for (the Carrion Vulture's wings span 2.7 world units at 1.2x), and
        /// shrinking it is cheaper than widening every slot for it.</summary>
        public readonly Dictionary<string, float> ScaleMul = new Dictionary<string, float>();

        /// <summary>The mannequin faces its opponent in v2 (see the class comment).</summary>
        public bool MannequinFacesOpponent = true;

        public readonly List<string> Warnings = new List<string>();

        public static readonly StagePresentation Empty = new StagePresentation();

        public static StagePresentation FromJson(string text)
        {
            StagePresentation c = new StagePresentation();
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err) || !root.IsObject)
            {
                c.Warnings.Add("stage-v2.json: " + (err ?? "not an object"));
                return c;
            }

            JsonValue stand = root["standIns"];
            if (stand.IsObject)
            {
                foreach (string key in stand.Keys)
                {
                    string value = stand[key].AsString("");
                    if (key.Length == 0 || value.Length == 0) { c.Warnings.Add("stage-v2.json: a stand-in has an empty id"); continue; }
                    c.StandIns[key] = value;
                }
            }

            JsonValue flip = root["faceFlip"];
            if (flip.IsArray)
            {
                for (int i = 0; i < flip.Count; i++)
                {
                    string id = flip[i].AsString("");
                    if (id.Length == 0) { c.Warnings.Add("stage-v2.json: a faceFlip entry is empty"); continue; }
                    c.FaceFlip.Add(id);
                }
            }

            JsonValue mul = root["scaleMul"];
            if (mul.IsObject)
            {
                foreach (string key in mul.Keys)
                {
                    float value = mul[key].AsFloat(1f);
                    if (key.Length == 0 || value < 0.3f || value > 1.5f) { c.Warnings.Add("stage-v2.json: scaleMul for '" + key + "' is outside 0.3 to 1.5"); continue; }
                    c.ScaleMul[key] = value;
                }
            }

            c.MannequinFacesOpponent = root.GetBool("mannequinFacesOpponent", true);
            return c;
        }

        /// <summary>A field for field copy. VisualDef is Core's and a rig must never mutate the
        /// one the snapshot shares with the definition, so v2 builds from a copy. The harness
        /// compares every public field by reflection, so a field added to VisualDef later cannot
        /// be silently dropped here.</summary>
        public static VisualDef Clone(VisualDef v)
        {
            VisualDef c = new VisualDef();
            c.Primary = v.Primary;
            c.Secondary = v.Secondary;
            c.Accent = v.Accent;
            c.Silhouette = v.Silhouette;
            c.Weapon = v.Weapon;
            c.Scale = v.Scale;
            c.Sigil = v.Sigil;
            c.Sprite = v.Sprite;
            c.SpriteBodyFraction = v.SpriteBodyFraction;
            c.SpriteFootFraction = v.SpriteFootFraction;
            c.SpriteMirror = v.SpriteMirror;
            c.SpriteStatus = v.SpriteStatus;
            c.Portrait = v.Portrait;
            return c;
        }

        /// <summary>True when a def is something a stand-in may borrow: it names a sprite and is
        /// not itself pending.</summary>
        public static bool IsFinishedArt(VisualDef v)
        {
            return v != null && !v.SpritePending && !string.IsNullOrEmpty(v.Sprite);
        }

        /// <summary>
        /// The VisualDef a rig is built from. v1 returns `own` itself, the very object, so the
        /// R12 look is not merely equal but identical. v2 returns a fresh copy: the stand-in's
        /// look for a mapped pending unit, the mannequin mirrored to face its opponent for an
        /// unmapped one, the inverted mirror for a faceFlip unit, and in every case the scale
        /// times BattleFraming.UnitScaleV2.
        /// </summary>
        public VisualDef Resolve(string defId, VisualDef own, Func<string, VisualDef> standInLook, bool v2)
        {
            if (!v2 || own == null) return own;

            VisualDef v;
            string standId;
            VisualDef borrowed = null;
            if (own.SpritePending && defId != null && StandIns.TryGetValue(defId, out standId) && standInLook != null)
                borrowed = standInLook(standId);

            if (own.SpritePending && IsFinishedArt(borrowed))
            {
                v = Clone(borrowed);
            }
            else
            {
                v = Clone(own);
                if (own.SpritePending && MannequinFacesOpponent) v.SpriteMirror = true;
                else if (defId != null && FaceFlip.Contains(defId)) v.SpriteMirror = !v.SpriteMirror;
            }

            float own_mul;
            v.Scale = Math.Max(0.3f, v.Scale) * BattleFraming.UnitScaleV2
                      * (defId != null && ScaleMul.TryGetValue(defId, out own_mul) ? own_mul : 1f);
            return v;
        }
    }
}
