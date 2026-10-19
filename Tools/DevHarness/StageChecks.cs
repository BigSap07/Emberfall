using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Emberfall.Battle;
using Emberfall.Core.Data;
using Emberfall.UI.Battle;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE R15 "v2" STAGE LOOK (Assets/Resources/Content/stage-v2.json, StagePresentation, BattleFraming's
    /// v2 numbers), PROVED WITHOUT UNITY.
    ///
    /// Four things are proved, each against the real content and the real art's measured silhouettes
    /// (Tools/DevHarness/sprite-extents.txt, written by Tools/sprite_extents.py):
    ///
    ///   S1 stand-ins    every chapter 1 and 2 placeholder is mapped to a finished enemy whose sheet exists on
    ///                   disk, and no mapped art stands twice in one fight;
    ///   S2 resolution   v1 hands back the very object the snapshot carries, v2 never mutates a definition and
    ///                   copies every field, and stats, ids and the ContentVersion hash are untouched;
    ///   S3 facing       every sheet's drawn facing (Tools/DevHarness/facing-audit.txt) lands facing its opponent
    ///                   on both sides in v2, and the count of units that did not in v1 is printed;
    ///   S4 framing      units are about 20 percent larger on screen, and at 1920x1080 and 2400x1080 (and the
    ///                   shapes between) no two bodies, plates or bars overlap and everything is inside the field.
    ///
    /// How it looks is for the capture batch (Screen:RosterSheet and Screen:Battle with and without the .v1
    /// suffix). This file is about everything that can be computed.
    /// </summary>
    internal static partial class Program
    {
        private const float StageScreenFore = 0.8660254f;   // cos(UnitRig.ThreeQuarterTurn): what a turned flat quad keeps of its width
        private const float StageCos = 0.88294759f;         // cos(28)
        private const float StageSin = 0.46947156f;         // sin(28)

        private sealed class StageExtent
        {
            public float Fw, Fh, X0, X1, YBot, YTop;
        }

        private sealed class StageBody
        {
            public string Name;
            public float X, Z;        // slot
            public float HalfW;       // world, across the screen, already foreshortened
            public float Top, Bot;    // world, above the feet
        }

        private static void CheckStage(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("STAGE (R15 v2: stand-ins, facing, scale and framing; presentation only)");

            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string cfgPath = Path.Combine(contentDir, "stage-v2.json");
            if (!File.Exists(cfgPath)) { Check(false, "S0 stage-v2.json is where the harness expects it"); return; }
            StagePresentation cfg = StagePresentation.FromJson(File.ReadAllText(cfgPath));
            Check(cfg.Warnings.Count == 0, "S0 stage-v2.json loads with no warnings (" + cfg.StandIns.Count + " stand-ins, "
                  + cfg.FaceFlip.Count + " face flips)");

            string unitsDir = Path.Combine(root, "Assets", "Resources", "Sprites", "Units");
            CheckStageStandIns(db, cfg, unitsDir);
            CheckStageResolution(db, cfg, contentDir);
            CheckStageFacing(db, cfg, root);
            CheckStageFraming(db, cfg, root);
            CheckStageWiring(root);
        }

        // ------------------------------------------------------------------ S1 stand-ins

        private static bool StageSheetOnDisk(string unitsDir, string sprite)
        {
            return File.Exists(Path.Combine(unitsDir, sprite + "_idle.png")) && File.Exists(Path.Combine(unitsDir, sprite + "_idle.json"));
        }

        private static string StageDrawnAs(ContentDatabase db, StagePresentation cfg, string enemyId)
        {
            EnemyDef e = db.GetEnemy(enemyId);
            if (e == null) return "?" + enemyId;
            VisualDef v = cfg.Resolve(enemyId, e.Visual, delegate(string id) { EnemyDef s = db.GetEnemy(id); return s != null ? s.Visual : null; }, true);
            return v.SpritePending ? "mannequin:" + enemyId : v.Sprite;
        }

        private static void CheckStageStandIns(ContentDatabase db, StagePresentation cfg, string unitsDir)
        {
            List<string> bad = new List<string>();
            foreach (KeyValuePair<string, string> pair in cfg.StandIns)
            {
                EnemyDef unit = db.GetEnemy(pair.Key);
                EnemyDef stand = db.GetEnemy(pair.Value);
                if (unit == null || !unit.Visual.SpritePending) bad.Add(pair.Key + " is not a pending enemy");
                else if (stand == null) bad.Add(pair.Value + " is not an enemy");
                else if (!StagePresentation.IsFinishedArt(stand.Visual)) bad.Add(pair.Value + " has no finished art");
                else if (!StageSheetOnDisk(unitsDir, stand.Visual.Sprite)) bad.Add(pair.Value + "'s sheet " + stand.Visual.Sprite + "_idle is not on disk");
                else if (cfg.StandIns.ContainsKey(pair.Value)) bad.Add(pair.Value + " is itself a stand-in");
            }
            Check(bad.Count == 0, "S1 every stand-in is a pending enemy shown as a finished enemy whose sheet exists on disk"
                  + (bad.Count > 0 ? ": " + string.Join("; ", bad.ToArray()) : " (" + cfg.StandIns.Count + " mapped)"));

            // Every chapter 1 and 2 placeholder has one.
            List<string> unmapped = new List<string>();
            int pendingInOneAndTwo = 0;
            int stagesWithMapped = 0;
            int clashes = 0;
            for (int c = 0; c < db.Chapters.Count && c < 2; c++)
            {
                for (int s = 0; s < db.Chapters[c].Stages.Count; s++)
                {
                    StageDef stage = db.Chapters[c].Stages[s];
                    List<string> drawn = new List<string>();
                    bool hasMapped = false;
                    for (int i = 0; i < stage.Enemies.Count; i++)
                    {
                        string id = stage.Enemies[i].EnemyId;
                        EnemyDef e = db.GetEnemy(id);
                        if (e != null && e.Visual.SpritePending)
                        {
                            pendingInOneAndTwo++;
                            if (!cfg.StandIns.ContainsKey(id) && unmapped.IndexOf(id) < 0) unmapped.Add(id);
                            else hasMapped = true;
                        }
                        drawn.Add(StageDrawnAs(db, cfg, id));
                    }
                    if (hasMapped) stagesWithMapped++;
                    for (int i = 0; i < drawn.Count; i++)
                        for (int j = i + 1; j < drawn.Count; j++)
                            if (drawn[i] == drawn[j]) clashes++;
                }
            }
            Check(unmapped.Count == 0, "S1 every placeholder that appears in chapter 1 or 2 has a stand-in ("
                  + pendingInOneAndTwo + " appearances over " + stagesWithMapped + " stages)"
                  + (unmapped.Count > 0 ? ", unmapped: " + string.Join(", ", unmapped.ToArray()) : ""));
            Check(clashes == 0, "S1 no two units in a chapter 1 or 2 fight draw the same art, stand-ins included (" + clashes + " clashes)");
        }

        // ------------------------------------------------------------------ S2 resolution

        private static string StageVisualDump(VisualDef v)
        {
            string s = "";
            FieldInfo[] fields = typeof(VisualDef).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++) s += fields[i].Name + "=" + fields[i].GetValue(v) + ";";
            return s;
        }

        private static void CheckStageResolution(ContentDatabase db, StagePresentation cfg, string contentDir)
        {
            Func<string, VisualDef> look = delegate(string id) { EnemyDef s = db.GetEnemy(id); return s != null ? s.Visual : null; };

            int units = 0, sameObject = 0, mutated = 0, wrongScale = 0, wrongArt = 0;
            List<KeyValuePair<string, VisualDef>> all = new List<KeyValuePair<string, VisualDef>>();
            for (int i = 0; i < db.Heroes.Count; i++) all.Add(new KeyValuePair<string, VisualDef>(db.Heroes[i].Id, db.Heroes[i].Visual));
            for (int i = 0; i < db.Enemies.Count; i++) all.Add(new KeyValuePair<string, VisualDef>(db.Enemies[i].Id, db.Enemies[i].Visual));
            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].Key;
                VisualDef own = all[i].Value;
                string before = StageVisualDump(own);
                units++;
                if (object.ReferenceEquals(cfg.Resolve(id, own, look, false), own)) sameObject++;
                VisualDef v2 = cfg.Resolve(id, own, look, true);
                if (StageVisualDump(own) != before || object.ReferenceEquals(v2, own)) mutated++;

                string standId;
                VisualDef source = own;
                if (own.SpritePending && cfg.StandIns.TryGetValue(id, out standId)) source = look(standId);
                float mul;
                if (!cfg.ScaleMul.TryGetValue(id, out mul)) mul = 1f;
                if (Math.Abs(v2.Scale - Math.Max(0.3f, source.Scale) * BattleFraming.UnitScaleV2 * mul) > 1e-5f) wrongScale++;
                if (source != own && (v2.Sprite != source.Sprite || v2.SpritePending)) wrongArt++;
                if (source == own && own.Sprite != v2.Sprite) wrongArt++;
            }
            Check(sameObject == units, "S2 v1 is exactly R12: Resolve(v2 off) returns the snapshot's own VisualDef, the same object, for all "
                  + units + " heroes and enemies");
            Check(mutated == 0, "S2 v2 builds from a copy and never changes a definition (" + units + " units, " + mutated + " mutated)");
            Check(wrongScale == 0 && wrongArt == 0, "S2 v2 draws every unit " + F2(BattleFraming.UnitScaleV2) + "x its own scale (a stand-in at the stand-in's, " + cfg.ScaleMul.Count + " unit with its own scaleMul), and a stand-in's art is a sheet"
                  + " (" + wrongScale + " wrong scales, " + wrongArt + " wrong art)");

            // Clone copies every public field: fill each with a value no default holds, then compare.
            VisualDef probe = new VisualDef();
            FieldInfo[] fields = typeof(VisualDef).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                Type t = fields[i].FieldType;
                if (t == typeof(string)) fields[i].SetValue(probe, "probe" + i);
                else if (t == typeof(float)) fields[i].SetValue(probe, 0.5f + i);
                else if (t == typeof(int)) fields[i].SetValue(probe, 100 + i);
                else if (t == typeof(bool)) fields[i].SetValue(probe, true);
            }
            Check(StageVisualDump(StagePresentation.Clone(probe)) == StageVisualDump(probe),
                  "S2 StagePresentation.Clone copies all " + fields.Length + " public fields of VisualDef");

            // The ids, stats and hash are not touched: the file is not one of the eight hashed ones.
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "S2 stage-v2.json IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion + ")");
        }

        // ------------------------------------------------------------------ S3 facing

        private static List<string[]> StageFacingAudit(string root)
        {
            List<string[]> rows = new List<string[]>();
            string path = Path.Combine(root, "Tools", "DevHarness", "facing-audit.txt");
            if (!File.Exists(path)) return rows;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == '#') continue;
                string[] parts = lines[i].Split('|');
                if (parts.Length >= 4) rows.Add(parts);
            }
            return rows;
        }

        /// <summary>Where a unit looks on screen: the sheet is shown as drawn when the flip is true and
        /// mirrored when it is false (UnitRig.SpriteFlipped is mirror XOR enemy side). 1 is right, -1 left.</summary>
        private static int StageScreenFacing(int drawn, bool spriteMirror, bool facingForward)
        {
            bool flip = spriteMirror ^ !facingForward;
            return flip ? drawn : -drawn;
        }

        private static void CheckStageFacing(ContentDatabase db, StagePresentation cfg, string root)
        {
            Func<string, VisualDef> look = delegate(string id) { EnemyDef s = db.GetEnemy(id); return s != null ? s.Visual : null; };
            List<string[]> rows = StageFacingAudit(root);
            Check(rows.Count > 0, "S3 the facing audit file is found (" + rows.Count + " rows)");

            int decided = 0, undecided = 0, v1Wrong = 0, v2Wrong = 0;
            List<string> v1WrongNames = new List<string>();
            List<string> v2WrongNames = new List<string>();
            for (int r = 0; r < rows.Count; r++)
            {
                string kind = rows[r][0], id = rows[r][1], raw = rows[r][2];
                if (raw != "R" && raw != "L") { undecided++; continue; }
                int drawn = raw == "R" ? 1 : -1;
                decided++;
                VisualDef own = null;
                bool pendingKind = kind == "mannequin";
                if (kind == "hero") { HeroDef h = db.GetHero(id); own = h != null ? h.Visual : null; }
                else if (kind == "enemy") { EnemyDef e = db.GetEnemy(id); own = e != null ? e.Visual : null; }
                if (!pendingKind && own == null) { v2Wrong++; v2WrongNames.Add(id + " (unknown unit)"); continue; }

                // Both sides, v1 and v2. The mannequin is judged through every pending unit that draws it.
                List<KeyValuePair<string, VisualDef>> subjects = new List<KeyValuePair<string, VisualDef>>();
                if (pendingKind)
                {
                    for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Visual.SpritePending) subjects.Add(new KeyValuePair<string, VisualDef>(db.Heroes[i].Id, db.Heroes[i].Visual));
                    for (int i = 0; i < db.Enemies.Count; i++) if (db.Enemies[i].Visual.SpritePending && !cfg.StandIns.ContainsKey(db.Enemies[i].Id)) subjects.Add(new KeyValuePair<string, VisualDef>(db.Enemies[i].Id, db.Enemies[i].Visual));
                }
                else subjects.Add(new KeyValuePair<string, VisualDef>(id, own));

                for (int s = 0; s < subjects.Count; s++)
                {
                    VisualDef v1 = subjects[s].Value;
                    VisualDef v2 = cfg.Resolve(subjects[s].Key, v1, look, true);
                    for (int side = 0; side < 2; side++)
                    {
                        bool ally = side == 0;
                        int want = ally ? 1 : -1;
                        if (StageScreenFacing(drawn, v1.SpriteMirror, ally) != want) { v1Wrong++; if (v1WrongNames.Count < 400 && v1WrongNames.IndexOf(subjects[s].Key) < 0) v1WrongNames.Add(subjects[s].Key); }
                        if (StageScreenFacing(drawn, v2.SpriteMirror, ally) != want) { v2Wrong++; v2WrongNames.Add(subjects[s].Key + (ally ? " ally" : " enemy")); }
                    }
                }
            }
            Check(decided > 0 && v2Wrong == 0,
                  "S3 FACING: in v2 all " + decided + " audited sheets (and every mannequin) face their opponent on the ally line and on the enemy line"
                  + (v2WrongNames.Count > 0 ? ", wrong: " + string.Join(", ", v2WrongNames.ToArray()) : ""));
            Line("  S3 in v1 " + v1WrongNames.Count + " units face the wrong way on at least one side (the six listed heroes and every mannequin enemy);"
                 + " " + undecided + " sheets are undecided in the audit and left to the roster sheet capture");

            // The rule the model above uses is the rule the rig uses.
            string rigPath = Path.Combine(root, "Assets", "Scripts", "Battle", "UnitRig.cs");
            Check(File.Exists(rigPath) && File.ReadAllText(rigPath).Contains("return spriteMirror ^ !facingForward;"),
                  "S3 UnitRig.SpriteFlipped is still spriteMirror XOR enemy side, the rule this model reads");
        }

        // ------------------------------------------------------------------ S4 framing

        private static Dictionary<string, StageExtent> StageExtents(string root)
        {
            Dictionary<string, StageExtent> map = new Dictionary<string, StageExtent>();
            string path = Path.Combine(root, "Tools", "DevHarness", "sprite-extents.txt");
            if (!File.Exists(path)) return map;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == '#') continue;
                string[] p = lines[i].Split('|');
                if (p.Length < 7) continue;
                StageExtent e = new StageExtent();
                e.Fw = float.Parse(p[1], CultureInfo.InvariantCulture); e.Fh = float.Parse(p[2], CultureInfo.InvariantCulture);
                e.X0 = float.Parse(p[3], CultureInfo.InvariantCulture); e.X1 = float.Parse(p[4], CultureInfo.InvariantCulture);
                e.YBot = float.Parse(p[5], CultureInfo.InvariantCulture); e.YTop = float.Parse(p[6], CultureInfo.InvariantCulture);
                map[p[0]] = e;
            }
            return map;
        }

        /// <summary>UnitRig.SilhouetteTop and its five constants, restated; the wiring check reads UnitRig.cs for them.</summary>
        private static float StageSilhouetteTop(string silhouette)
        {
            switch (silhouette)
            {
                case "brute": return 1.74f;
                case "beast": return 1.08f;
                case "wisp": return 1.75f;
                case "golem": return 1.90f;
                default: return 1.71f;
            }
        }

        /// <summary>
        /// The visible silhouette of one unit as UnitRig.BuildSprite sizes it: the quad is
        /// Height / bodyFraction tall and as wide as its frame's aspect says, the feet sit footFraction up it,
        /// and the visible pixels are the extent file's. Null for a unit with no sprite (a procedural rig).
        /// </summary>
        private static StageBody StageBodyOf(string name, VisualDef v, Dictionary<string, StageExtent> extents)
        {
            bool pending = v.SpritePending;
            string sprite = pending ? "placeholder_dummy" : v.Sprite;
            StageExtent e;
            if (string.IsNullOrEmpty(sprite) || !extents.TryGetValue(sprite, out e)) return null;
            float bodyFraction = pending ? 0.558f : v.SpriteBodyFraction;
            float footFraction = pending ? 0.219f : v.SpriteFootFraction;
            float canvasH = StageSilhouetteTop(v.Silhouette) / Math.Max(0.05f, bodyFraction);
            float canvasW = canvasH * e.Fw / e.Fh;
            float scale = Math.Max(0.3f, v.Scale);
            StageBody b = new StageBody();
            b.Name = name;
            b.HalfW = Math.Max(Math.Abs(e.X0 - 0.5f), Math.Abs(e.X1 - 0.5f)) * canvasW * scale * StageScreenFore;
            b.Top = (e.YTop - footFraction) * canvasH * scale;
            b.Bot = (e.YBot - footFraction) * canvasH * scale;
            return b;
        }

        private static void CheckStageFraming(ContentDatabase db, StagePresentation cfg, string root)
        {
            Func<string, VisualDef> look = delegate(string id) { EnemyDef s = db.GetEnemy(id); return s != null ? s.Visual : null; };
            Dictionary<string, StageExtent> extents = StageExtents(root);
            Check(extents.Count > 50, "S4 the sprite extent file is found (" + extents.Count + " sprites)");

            // v1 is pinned: the R12 slots and camera, so the rollback is the numbers it always was.
            bool v1Pinned = Math.Abs(BattleFraming.OrthographicSize - 3.65f) < 1e-6f && Math.Abs(BattleFraming.CameraY - 5.95f) < 1e-6f
                            && BattleFraming.AllySlots[0].X == -1.4f && BattleFraming.AllySlots[0].Z == -3.25f
                            && BattleFraming.AllySlots[2].X == -3.95f && BattleFraming.AllySlots[2].Z == -4.95f
                            && BattleFraming.AllySlots[3].X == -3.95f && BattleFraming.AllySlots[3].Z == 0f
                            && BattleFraming.EnemySlots[4].X == 3.95f && BattleFraming.EnemySlots[4].Z == 4.95f;
            Check(v1Pinned, "S4 the v1 camera and slots are still R12's (orthographic size 3.65, camera y 5.95, slots at 1.4, 3.95, 3.25, 4.95)");
            Check(BattleFraming.UnitScaleV2 >= 1.19f && BattleFraming.UnitScaleV2 <= 1.21f, "S4 v2 draws units " + F2(BattleFraming.UnitScaleV2) + "x larger");
            bool v2Mirrored = true;
            for (int i = 0; i < 5; i++)
            {
                BattleFraming.SlotPoint a = BattleFraming.AllySlotsV2[i], e = BattleFraming.EnemySlotsV2[i];
                if (Math.Abs(a.X + e.X) > 1e-5f || Math.Abs(a.Z - e.Z) > 1e-5f) v2Mirrored = false;
            }
            Check(v2Mirrored, "S4 the v2 enemy slots mirror the v2 ally slots across x = 0");

            // The allies are any heroes in any slots, so every ally slot is judged by the widest and the tallest
            // hero there is (a mannequin included). Enemies are judged by each authored composition, slot by slot.
            StageBody envelope = new StageBody();
            envelope.Name = "hero envelope";
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                VisualDef v = cfg.Resolve(db.Heroes[i].Id, db.Heroes[i].Visual, look, true);
                StageBody b = StageBodyOf(db.Heroes[i].Id, v, extents);
                if (b == null) continue;
                if (b.HalfW > envelope.HalfW) { envelope.HalfW = b.HalfW; envelope.Name = db.Heroes[i].Id; }
                if (b.Top > envelope.Top) envelope.Top = b.Top;
                if (b.Bot < envelope.Bot) envelope.Bot = b.Bot;
            }
            Line("  S4 hero envelope in v2: half-width " + F2(envelope.HalfW) + " across the screen (" + envelope.Name + "), " + F2(envelope.Top) + " tall");

            int[][] screens = { new[] { 1920, 1080 }, new[] { 2160, 1080 }, new[] { 2400, 1080 } };
            int[] insets = { 0, 24 };
            float worstBody = float.MaxValue, worstPlate = float.MaxValue, worstBar = float.MaxValue, worstPill = float.MaxValue;
            string worstBodyAt = "", worstPlateAt = "", worstBarAt = "", worstPillAt = "", outsideAt = "";
            int outside = 0;
            int stagesChecked = 0;
            float gain1920 = 0f, gain2400 = 0f;

            for (int s = 0; s < screens.Length; s++)
            {
                for (int k = 0; k < insets.Length; k++)
                {
                    int width = screens[s][0], height = screens[s][1], inset = insets[k];
                    float scale = BattleHudLayout.CanvasScale(width, height);
                    float canvasW = width / scale, canvasH = height / scale;
                    float safeBottom = inset / scale, safeTop = height / scale;
                    BattleFraming.CanvasRect viewport = BattleHudLayout.ToCanvas(BattleHudLayout.Viewport(width, height, inset, height, scale), canvasW, canvasH);
                    float aspect = viewport.Width / viewport.Height;
                    float ortho = BattleFraming.OrthographicSizeFor(true, aspect);
                    float perUnit = viewport.Height / (2f * ortho);
                    string tag = width + "x" + height + " inset" + inset;
                    if (width == 1920 && inset == 0) gain1920 = BattleFraming.UnitScaleV2 * BattleFraming.OrthographicSize / ortho;
                    if (width == 2400 && inset == 0) gain2400 = BattleFraming.UnitScaleV2 * BattleFraming.OrthographicSize / ortho;

                    BattleFraming.CanvasRect callout = BattleHudLayout.Callout(canvasW, safeTop, BattleHudMetrics.CalloutMaxWidth);
                    BattleFraming.CanvasRect next = BattleHudLayout.Next(canvasW, safeBottom, true, BattleHudMetrics.NextMaxUpcoming);
                    float shakePx = 0.18f * StageCos * perUnit;

                    // The authored compositions, plus the one all-hero-envelope line for the ally side.
                    List<List<StageBody>> lines = new List<List<StageBody>>();
                    lines.Add(null);   // the envelope case: allies = envelope in every slot, enemies = envelope in every slot
                    for (int a = 0; a < db.AllStages.Count; a++)
                    {
                        StageDef stage = db.AllStages[a];
                        List<StageBody> enemies = new List<StageBody>();
                        bool ok = true;
                        for (int i = 0; i < stage.Enemies.Count && i < 5; i++)
                        {
                            EnemyDef e = db.GetEnemy(stage.Enemies[i].EnemyId);
                            if (e == null) { ok = false; break; }
                            VisualDef v = cfg.Resolve(e.Id, e.Visual, look, true);
                            StageBody b = StageBodyOf(e.Id + "@" + stage.Id, v, extents);
                            if (b == null) { ok = false; break; }
                            enemies.Add(b);
                        }
                        if (ok && enemies.Count > 0) lines.Add(enemies);
                    }
                    if (s == 0 && k == 0) stagesChecked = lines.Count - 1;

                    for (int li = 0; li < lines.Count; li++)
                    {
                        List<StageBody> bodies = new List<StageBody>();
                        for (int i = 0; i < 5; i++)
                        {
                            BattleFraming.SlotPoint ap = BattleFraming.Slot(true, i, true);
                            StageBody ab = Copy(envelope, "ally" + i);
                            ab.X = ap.X; ab.Z = ap.Z;
                            bodies.Add(ab);
                        }
                        for (int i = 0; i < 5; i++)
                        {
                            BattleFraming.SlotPoint ep = BattleFraming.Slot(false, i, true);
                            StageBody eb;
                            if (lines[li] == null) eb = Copy(envelope, "enemy" + i);
                            else if (i < lines[li].Count) eb = Copy(lines[li][i], lines[li][i].Name);
                            else continue;
                            eb.X = ep.X; eb.Z = ep.Z;
                            bodies.Add(eb);
                        }

                        // World boxes to canvas boxes.
                        int n = bodies.Count;
                        BattleFraming.CanvasRect[] body = new BattleFraming.CanvasRect[n];
                        BattleFraming.CanvasRect[] plate = new BattleFraming.CanvasRect[n];
                        BattleFraming.CanvasRect[] bar = new BattleFraming.CanvasRect[n];
                        for (int i = 0; i < n; i++)
                        {
                            BattleFraming.CanvasPoint feet = BattleFraming.Project(bodies[i].X, 0f, bodies[i].Z, viewport, true, ortho);
                            body[i] = new BattleFraming.CanvasRect(feet.X - bodies[i].HalfW * perUnit, feet.Y + bodies[i].Bot * StageCos * perUnit,
                                                                   2f * bodies[i].HalfW * perUnit, (bodies[i].Top - bodies[i].Bot) * StageCos * perUnit);
                            plate[i] = BattleHudLayout.PlateBox(feet);
                            bar[i] = new BattleFraming.CanvasRect(feet.X - BattleHudMetrics.PlateBarWidth * 0.5f, feet.Y - BattleHudMetrics.PlateBelowFeet,
                                                                  BattleHudMetrics.PlateBarWidth, BattleHudMetrics.PlateBelowFeet);
                        }

                        for (int i = 0; i < n; i++)
                        {
                            for (int j = i + 1; j < n; j++)
                            {
                                float bg = BattleHudLayout.Clearance(body[i], body[j]) / perUnit;
                                if (bg < worstBody) { worstBody = bg; worstBodyAt = tag + " " + bodies[i].Name + "/" + bodies[j].Name; }
                                float pg = BattleHudLayout.Clearance(plate[i], plate[j]);
                                if (pg < worstPlate) { worstPlate = pg; worstPlateAt = tag + " " + bodies[i].Name + "/" + bodies[j].Name; }
                            }
                            for (int j = 0; j < n; j++)
                            {
                                if (i == j) continue;
                                float ng = BattleHudLayout.Clearance(bar[i], body[j]);
                                if (ng < worstBar) { worstBar = ng; worstBarAt = tag + " " + bodies[i].Name + " bars/" + bodies[j].Name; }
                            }

                            // inside the field with the shake, clear of the pills
                            bool inside = body[i].X - shakePx >= viewport.X && body[i].XMax + shakePx <= viewport.XMax
                                          && body[i].YMax + shakePx + 0.25f * StageCos * perUnit <= viewport.YMax
                                          && plate[i].X >= viewport.X && plate[i].XMax <= viewport.XMax
                                          && Math.Min(body[i].Y, plate[i].Y) - shakePx >= viewport.Y;
                            float pills = Math.Min(BattleHudLayout.Clearance(plate[i], callout), BattleHudLayout.Clearance(plate[i], next));
                            if (pills < worstPill) { worstPill = pills; worstPillAt = tag + " " + bodies[i].Name; }
                            if (!inside) { outside++; if (outsideAt.Length == 0) outsideAt = tag + " " + bodies[i].Name; }
                        }
                    }
                }
            }

            Check(worstBody >= 0.05f, "S4 NO TWO BODIES OVERLAP in v2: worst body-to-body gap " + F2(worstBody) + " world at " + worstBodyAt
                  + " (min 0.05), over the hero envelope and " + stagesChecked + " authored enemy lines, at 1920x1080, 2160x1080 and 2400x1080 with and without a bottom inset");
            Check(worstPlate >= BattleHudMetrics.PlateMinClearance, "S4 NO TWO NAME PLATES OVERLAP in v2: worst plate-to-plate clearance " + F1(worstPlate)
                  + " at " + worstPlateAt + " (min " + F1(BattleHudMetrics.PlateMinClearance) + ")");
            Check(worstBar >= 0f, "S4 NO HP BAR SITS UNDER ANOTHER UNIT in v2: worst bar-to-body clearance " + F1(worstBar) + " canvas units at " + worstBarAt);
            Check(worstPill >= BattleHudMetrics.PlateMinClearance, "S4 NO PLATE TOUCHES THE ABILITY CALLOUT OR THE NEXT PILL in v2: worst clearance " + F1(worstPill) + " at " + worstPillAt
                  + " (min " + F1(BattleHudMetrics.PlateMinClearance) + ")");
            Check(outside == 0, "S4 EVERY BODY AND PLATE IS INSIDE THE FIELD in v2, shake and cast lift included (" + outside + " outside"
                  + (outside > 0 ? ", first " + outsideAt : "") + ")");
            Check(gain1920 >= 1.15f && gain2400 >= 1.15f, "S4 units are about 20 percent larger on screen: x" + F2(gain1920) + " at 1920x1080 and x" + F2(gain2400)
                  + " at 2400x1080 (unit scale " + F2(BattleFraming.UnitScaleV2) + " at orthographic size " + F2(BattleFraming.OrthographicSizeV2) + " against R12's "
                  + F2(BattleFraming.OrthographicSize) + ")");

            // The camera height is the one the slots' vertical centre needs, for the tallest unit that can
            // stand in each slot (a hero on the ally side, the tallest authored enemy of that slot on the other).
            float[] tallestEnemy = new float[5];
            for (int a = 0; a < db.AllStages.Count; a++)
            {
                StageDef stage = db.AllStages[a];
                for (int i = 0; i < stage.Enemies.Count && i < 5; i++)
                {
                    EnemyDef e = db.GetEnemy(stage.Enemies[i].EnemyId);
                    if (e == null) continue;
                    StageBody b = StageBodyOf(e.Id, cfg.Resolve(e.Id, e.Visual, look, true), extents);
                    if (b != null && b.Top > tallestEnemy[i]) tallestEnemy[i] = b.Top;
                }
            }
            float vTop = float.MinValue, vBottom = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                BattleFraming.SlotPoint p = BattleFraming.AllySlotsV2[i];
                float tallest = Math.Max(envelope.Top, tallestEnemy[i]);
                vTop = Math.Max(vTop, p.Z * StageSin + tallest * StageCos + 0.159f + 0.25f);
                vBottom = Math.Min(vBottom, p.Z * StageSin - 0.79f * BattleFraming.UnitScaleV2 * StageSin - 0.159f);
            }
            float centre = (vTop + vBottom) * 0.5f;
            float camY = (centre + 9.5f * StageSin) / StageCos;
            Check(Math.Abs(camY - BattleFraming.CameraYV2) < 0.12f && (vTop - vBottom) * 0.5f <= BattleFraming.OrthographicSizeV2 + 0.001f,
                  "S4 the v2 camera is centred on its formation: needs y " + F2(camY) + " (has " + F2(BattleFraming.CameraYV2) + ") and a half-height of "
                  + F2((vTop - vBottom) * 0.5f) + " (has " + F2(BattleFraming.OrthographicSizeV2) + ")");
        }

        private static StageBody Copy(StageBody b, string name)
        {
            StageBody c = new StageBody();
            c.Name = name; c.HalfW = b.HalfW; c.Top = b.Top; c.Bot = b.Bot; c.X = b.X; c.Z = b.Z;
            return c;
        }

        // ------------------------------------------------------------------ S5 wiring

        private static void CheckStageWiring(string root)
        {
            string battle = Path.Combine(root, "Assets", "Scripts", "Battle");
            string actor = File.ReadAllText(Path.Combine(battle, "UnitActor.cs"));
            string stage = File.ReadAllText(Path.Combine(battle, "BattleStage.cs"));
            string feel = File.ReadAllText(Path.Combine(battle, "StageFeel.cs"));
            Check(actor.Contains("StageFeel.Resolve(snapshot)") && stage.Contains("StageFeel.V2") && stage.Contains("OrthographicSizeFor(")
                  && stage.Contains("BattleFraming.Slot(snapshot.IsPlayer, snapshot.Slot, _framingV2)"),
                  "S5 the rig is built from StageFeel.Resolve and the stage reads the preset for its camera and its slots");
            Check(feel.Contains("public static bool V2 { get { return Feel.V2; } }") && !feel.Contains("GetEnvironmentVariable") && !feel.Contains("GetString(\"feelPreset\""),
                  "S5 there is ONE switch: StageFeel.V2 is Feel.V2 (vfx.json feelPreset, EMBERFALL_FEEL, the .v1 and .v2 suffixes and the juice rollback), and the stage keeps no preset of its own");
            Check(feel.Contains("if (!V2) return snapshot.Visual;"), "S5 stage-v2.json is read only once the switch says v2 (Resolve returns the snapshot's own VisualDef before it touches the config)");

            // Simulation untouched: no Core file and no hashed content knows about any of it.
            string core = Path.Combine(root, "Assets", "Scripts", "Core");
            List<string> mentions = new List<string>();
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.IndexOf("StageFeel", StringComparison.Ordinal) >= 0 || text.IndexOf("StagePresentation", StringComparison.Ordinal) >= 0
                    || text.IndexOf("stage-v2", StringComparison.Ordinal) >= 0) mentions.Add(Path.GetFileName(file));
            }
            List<string> globs = new List<string>();
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.IndexOf("GetFiles(", StringComparison.Ordinal) >= 0 || text.IndexOf("EnumerateFiles(", StringComparison.Ordinal) >= 0
                    || text.IndexOf("GetAssets(", StringComparison.Ordinal) >= 0) globs.Add(Path.GetFileName(file));
            }
            Check(globs.Count == 0, "S5 ContentDatabase and the rest of Core never glob the Content folder, so stage-v2.json cannot be hashed or loaded by accident"
                  + " (the stage, hero and talent fingerprints and the eight-file ContentVersion in this same run are measured with the file present)"
                  + (globs.Count > 0 ? ": " + string.Join(", ", globs.ToArray()) : ""));
            Check(mentions.Count == 0, "S5 SIMULATION UNTOUCHED (reads source): no Core file mentions the v2 stage look"
                  + (mentions.Count > 0 ? ": " + string.Join(", ", mentions.ToArray()) : ""));

            // The five rendered tops this file restates are UnitRig's.
            string rig = File.ReadAllText(Path.Combine(battle, "UnitRig.cs"));
            Check(rig.Contains("HumanoidTop = 1.71f") && rig.Contains("BruteTop = 1.74f") && rig.Contains("BeastTop = 1.08f")
                  && rig.Contains("WispTop = 1.75f") && rig.Contains("GolemTop = 1.90f") && rig.Contains("ThreeQuarterTurn = 30f"),
                  "S5 the silhouette heights and the three-quarter turn this model restates are UnitRig's");
        }

    }
}
