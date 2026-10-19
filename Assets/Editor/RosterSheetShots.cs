#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Game;
using Emberfall.UI;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// THE ROSTER SHEET (R15, task 4b): every unit of one side in one picture, in one clip, standing
    /// the way a battle stands it, so a tester can see at a glance which of them faces the wrong way.
    ///
    ///   Screen:RosterSheet=allies            the first twelve heroes, idle
    ///   Screen:RosterSheet=allies.b          the next twelve, and allies.c the rest
    ///   Screen:RosterSheet=enemies.skill.d   the last enemies, at the strike frame of their skill
    ///   Screen:RosterSheet=allies.death.v1   the R12 look, the last frame of the death clip
    ///
    /// A sheet holds twelve units, six across and two deep, each in a cell of its own, so no body
    /// overlaps another and every name is readable. The log says how many sheets a side has (allies
    /// three, enemies four) and, per unit, what was applied: the mirror, the stand-in, and the flip
    /// of every clip, so the log carries the facing as well as the picture.
    ///
    /// The text after the = is "side[.a|b|c|d][.clip][.preset]". side is allies or enemies. clip is idle (the
    /// default), basic, skill, hit or death. preset is v1 or v2 and forces the stage look for the
    /// shot; without it the look is whatever vfx.json "feelPreset" says. The dots are dots because the text becomes
    /// part of the PNG's name and a colon cannot be in a Windows file name.
    ///
    /// Each unit is built through UnitActor.Setup, the one door a battle uses, so the stand-ins, the
    /// facing and the scale of the v2 look are exactly what a fight draws, on the side the roster is
    /// standing on: allies are built as the player's line (facing forward, turned toward +x) and
    /// enemies as the enemy line. Under each unit is its name and three arrows, ">>>" for an ally
    /// (who must face right) and "<<<" for an enemy (who must face left). A unit whose body points
    /// the other way from its arrows is a unit that faces the wrong way in that clip.
    ///
    /// The clip frame is set, not played: the unit is placed on the strike frame of the clip (the
    /// clip's own ImpactFraction), and its component is then switched off so no Update moves it on.
    /// A unit with no sheet (a mannequin, a retired 3D rig, a static painting) simply stands.
    /// One line per unit goes to the log ("RosterSheet: unit ...") with what it resolved to, so the
    /// picture can be read against the data.
    ///
    /// Has no menu item and no literal Begin call, like Screen:AudioFight, so the nightly sweep does
    /// not run it. It shows no UI: the canvas is switched off for the shot.
    /// </summary>
    public static class RosterSheetShots
    {
        // One unit to a cell and no cell touches another: a cell is wider than the widest unit (a
        // scaled vulture is about 2.7 across) and deep enough that a unit's head clears the name of
        // the unit behind it (2.3 tall x cos 28 = 2.0, plus the label, over sin 28 = 5.5 of z).
        private const int Columns = 6;
        private const int Rows = 2;
        private const float CellWidth = 3.4f;
        private const float CellDepth = 5.8f;

        private static GameObject _root;

        public static void Build(GameApp app, string spec)
        {
            string[] parts = (spec ?? "").Split('.');
            bool allies = parts.Length == 0 || parts[0] != "enemies";
            string clip = "idle";
            int page = 0;
            bool? v2 = null;
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 1 && parts[i][0] >= 'a' && parts[i][0] <= 'd') page = parts[i][0] - 'a';
                else if (parts[i] == "v1") v2 = false;
                else if (parts[i] == "v2") v2 = true;
                else if (parts[i] == "idle" || parts[i] == "basic" || parts[i] == "skill" || parts[i] == "hit" || parts[i] == "death") clip = parts[i];
                else Debug.LogWarning("RosterSheet: '" + parts[i] + "' is not a clip (idle, basic, skill, hit, death) or a preset (v1, v2), ignored.");
            }
            Feel.ForceForCapture(v2);

            if (app.RootCanvas != null) app.RootCanvas.gameObject.SetActive(false);
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = new GameObject("RosterSheet");

            List<UnitSnapshot> everyone = allies ? AllyUnits(app.Content) : EnemyUnits(app.Content);
            int perSheet = Columns * Rows;
            int sheets = (everyone.Count + perSheet - 1) / perSheet;
            if (page >= sheets) { Debug.LogWarning("RosterSheet: " + (allies ? "allies" : "enemies") + " has " + sheets + " sheets, so '" + (char)('a' + page) + "' is shown as the last."); page = sheets - 1; }
            List<UnitSnapshot> units = everyone.GetRange(page * perSheet, Math.Min(perSheet, everyone.Count - page * perSheet));
            int rows = (units.Count + Columns - 1) / Columns;

            float width = Columns * CellWidth;
            float depth = rows * CellDepth;
            GameObject cameraObject = new GameObject("RosterCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.30f, 0.31f, 0.36f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 120f;
            camera.depth = 50f;
            camera.transform.localRotation = Quaternion.Euler(BattleFraming.CameraPitch, 0f, 0f);
            float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float byWidth = width * 0.5f / aspect;
            float byHeight = ((rows - 1) * CellDepth * Mathf.Sin(BattleFraming.CameraPitch * Mathf.Deg2Rad) + 3.4f) * 0.5f;
            camera.orthographicSize = Mathf.Max(byWidth, byHeight);
            Vector3 centre = new Vector3(0f, 0.6f, (rows - 1) * CellDepth * 0.5f);
            camera.transform.localPosition = centre - camera.transform.forward * 30f;

            StringBuilder log = new StringBuilder();
            for (int i = 0; i < units.Count; i++)
            {
                UnitSnapshot snapshot = units[i];
                int row = i / Columns, column = i % Columns;
                // Row 0 is the nearest row (lowest on screen); later rows stand behind it.
                Vector3 at = new Vector3((column - (Columns - 1) * 0.5f) * CellWidth, 0f, row * CellDepth);

                GameObject host = new GameObject((allies ? "Ally_" : "Enemy_") + snapshot.Name);
                host.transform.SetParent(_root.transform, false);
                UnitActor actor = host.AddComponent<UnitActor>();
                actor.Setup(snapshot, at, snapshot.IsPlayer);
                Pose(actor, clip);
                actor.enabled = false;
                if (actor.Rig != null && actor.Rig.Nameplate != null) actor.Rig.Nameplate.gameObject.SetActive(false);

                Label(host.transform, at, snapshot, allies);
                log.AppendLine(UnitLine(actor, snapshot, allies));
            }

            Debug.Log("RosterSheet: " + (allies ? "allies" : "enemies") + " sheet " + (char)('a' + page) + " of " + sheets + ", " + clip + " " + (StageFeel.V2 ? "v2" : "v1") + ", " + units.Count
                      + " units in " + rows + " rows of " + Columns + ", orthographic size " + camera.orthographicSize.ToString("0.00"));
            Debug.Log(log.ToString());
        }

        /// <summary>One log line per unit: what the stage resolved it to and every clip's flip, as the
        /// rig applies it (UnitRig.SpriteFlipped of the mirror, inverted for the unit's clipFlip clips).</summary>
        private static string UnitLine(UnitActor actor, UnitSnapshot snapshot, bool allies)
        {
            VisualDef own = snapshot.Visual;
            VisualDef drawn = StageFeel.Resolve(snapshot);
            HashSet<string> inverted = StageFeel.ClipFlips(snapshot.DefId);
            bool baseFlip = UnitRig.SpriteFlipped(drawn.SpriteMirror, allies);
            string standIn = own.SpritePending && !drawn.SpritePending ? drawn.Sprite : "-";
            StringBuilder sb = new StringBuilder();
            sb.Append("RosterSheet: unit ").Append(snapshot.DefId)
              .Append(" side=").Append(allies ? "ally" : "enemy")
              .Append(" mirror=").Append(drawn.SpriteMirror ? "true" : "false")
              .Append(" (own ").Append(own.SpriteMirror ? "true" : "false").Append(")")
              .Append(" standIn=").Append(standIn)
              .Append(" scale=").Append(drawn.Scale.ToString("0.00"))
              .Append(actor.Rig != null && actor.Rig.Animation != null ? " sheet" : " still-or-rig")
              .Append(" flip(");
            string[] clips = { "idle", "basic", "basic2", "skill", "hit_reaction", "death" };
            for (int i = 0; i < clips.Length; i++)
            {
                bool flip = baseFlip != (inverted != null && inverted.Contains(clips[i]));
                sb.Append(i > 0 ? " " : "").Append(clips[i]).Append("=").Append(flip ? "asDrawn" : "mirrored");
            }
            sb.Append(") clip=").Append(actor.Rig != null && actor.Rig.Animation != null ? actor.Rig.Animation.CurrentClip : "none");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the roster

        private static List<UnitSnapshot> AllyUnits(ContentDatabase content)
        {
            List<UnitSnapshot> list = new List<UnitSnapshot>();
            int pendingShown = 0;
            for (int i = 0; i < content.Heroes.Count; i++)
            {
                HeroDef h = content.Heroes[i];
                if (h.Visual.SpritePending) { if (pendingShown >= 3) continue; pendingShown++; }
                list.Add(Snapshot(list.Count, true, h.Id, h.Name, h.Aspect, h.Visual));
            }
            // Drawn heroes first, then the three mannequins, which are all the same figure.
            list.Sort(delegate(UnitSnapshot a, UnitSnapshot b)
            {
                bool pa = a.Visual.SpritePending, pb = b.Visual.SpritePending;
                if (pa != pb) return pa ? 1 : -1;
                return string.CompareOrdinal(a.DefId, b.DefId);
            });
            return list;
        }

        private static List<UnitSnapshot> EnemyUnits(ContentDatabase content)
        {
            List<UnitSnapshot> list = new List<UnitSnapshot>();
            int pendingShown = 0;
            for (int i = 0; i < content.Enemies.Count; i++)
            {
                EnemyDef e = content.Enemies[i];
                bool mapped = StageFeel.Config.StandIns.ContainsKey(e.Id);
                if (e.Visual.SpritePending && !mapped) { if (pendingShown >= 3) continue; pendingShown++; }
                list.Add(Snapshot(list.Count, false, e.Id, e.Name, e.Aspect, e.Visual));
            }
            // Mapped placeholders and drawn enemies first, then the retired rigs, then three mannequins.
            list.Sort(delegate(UnitSnapshot a, UnitSnapshot b)
            {
                int ra = Rank(a), rb = Rank(b);
                if (ra != rb) return ra.CompareTo(rb);
                return string.CompareOrdinal(a.DefId, b.DefId);
            });
            return list;
        }

        private static int Rank(UnitSnapshot s)
        {
            if (s.Visual.SpritePending) return StageFeel.Config.StandIns.ContainsKey(s.DefId) ? 0 : 3;
            return string.IsNullOrEmpty(s.Visual.Sprite) ? 2 : 0;
        }

        private static UnitSnapshot Snapshot(int id, bool player, string defId, string name, Aspect aspect, VisualDef visual)
        {
            UnitSnapshot s = new UnitSnapshot();
            s.Id = id + 1;
            s.IsPlayer = player;
            s.Slot = 0;
            s.DefId = defId;
            s.Name = name;
            s.Aspect = aspect;
            s.Visual = visual;
            return s;
        }

        // ------------------------------------------------------------------ the pose

        /// <summary>Puts the unit's animator on one frame of one clip. The clip's own timing is used
        /// (SpriteAnimator.ClipDuration and ImpactFraction), so "basic" is the strike frame the battle
        /// syncs its damage to, not a frame picked here.</summary>
        private static void Pose(UnitActor actor, string clip)
        {
            if (actor.Rig == null || actor.Rig.Animation == null) return;
            SpriteAnimator an = actor.Rig.Animation;
            switch (clip)
            {
                case "basic":
                {
                    an.PlayOnce(BattleClips.Basic, false);
                    string played = an.Resolve(BattleClips.Basic);
                    if (played != null) an.Update(an.ClipDuration(played) * an.ImpactFraction(played));
                    break;
                }
                case "skill":
                {
                    an.PlayOnce(BattleClips.Skill, false);
                    string played = an.Resolve(BattleClips.Skill);
                    if (played != null) an.Update(an.ClipDuration(played) * an.ImpactFraction(played));
                    break;
                }
                case "hit":
                {
                    an.PlayOnce(BattleClips.HitReaction, false);
                    string played = an.Resolve(BattleClips.HitReaction);
                    if (played != null) an.Update(an.ClipDuration(played) * 0.35f);
                    break;
                }
                case "death":
                {
                    an.PlayOnce(BattleClips.Death, true);
                    string played = an.Resolve(BattleClips.Death);
                    if (played != null) an.Update(an.ClipDuration(played) * 1.5f);
                    break;
                }
                default:
                    an.PlayLoop(BattleClips.Idle);
                    break;
            }
        }

        // ------------------------------------------------------------------ the label

        private static void Label(Transform parent, Vector3 at, UnitSnapshot snapshot, bool ally)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            // Square to the camera, a little in front of the feet (toward the camera is down the screen).
            go.transform.rotation = Quaternion.Euler(BattleFraming.CameraPitch, 0f, 0f);
            go.transform.position = at + new Vector3(0f, 0.02f, -0.55f);

            TextMesh text = go.AddComponent<TextMesh>();
            string arrows = ally ? "> > >" : "< < <";
            text.text = snapshot.Name + "\n" + arrows;
            text.font = UITheme.Font;
            text.fontSize = 48;
            text.characterSize = 0.038f;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Center;
            text.color = ally ? new Color(0.65f, 1f, 0.65f, 1f) : new Color(1f, 0.72f, 0.65f, 1f);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && text.font != null) renderer.sharedMaterial = text.font.material;
        }
    }
}
#endif
