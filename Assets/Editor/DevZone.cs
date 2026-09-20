#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Game;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// THE DEV ZONE. A workbench for looking at heroes and at fights, on one screen, with nothing
    /// running unless a person makes it run.
    ///
    /// WHY IT IS HERE. Assets/Editor is excluded from player builds by Unity itself, so this is
    /// structurally unreachable from a build rather than merely flag-guarded - the same guarantee
    /// DebugCheatPanel already leans on, and the reason no other location was considered.
    ///
    /// WHY IT HAS NO MODES. Four kinds of work happen in one sitting here: does the clip read,
    /// why did the engine do that, does the kit match its text, are the numbers sane. Anything
    /// that made you switch tools between those questions would be answering the wrong problem,
    /// so the board is always live, the roster is always to hand, and the inspector always shows
    /// every section at once. Fold a section if you want the room; nothing is hidden behind a tab.
    ///
    /// WHAT IT DOES NOT DO, AND WHY. The simulation is a pure function: CombatSimulation.Run takes
    /// a request and returns the whole finished fight as an ordered event list. There is no live
    /// fight to reach into. So this drives the REAL simulation and steps its RECORD - which is
    /// exactly what BattleScreen does, and is the only reading that cannot disagree with the game.
    /// Everything that would need to reach into a fight mid-flight, or to watch the engine think,
    /// needs a change inside Assets/Scripts and is listed on the screen itself under "What this
    /// cannot tell you" rather than being faked here. A workbench that quietly approximated the
    /// engine would be worse than no workbench.
    /// </summary>
    public sealed class DevZone : EditorWindow
    {
        // ------------------------------------------------------------------ board model

        [Serializable]
        private sealed class Slot
        {
            public string DefId = "";
            public int Level = 40;
            public int Talent;          // 0 = none, 1..3
            public string RelicId = "";
            public bool Mirror;         // flip horizontally, for reading a pose both ways

            public bool Empty { get { return string.IsNullOrEmpty(DefId); } }
        }

        private const int SlotsPerSide = 5;
        private const float RosterWidth = 300f;
        private const float InspectorWidth = 420f;
        private readonly Slot[] _ally = new Slot[SlotsPerSide];
        private readonly Slot[] _foe = new Slot[SlotsPerSide];

        private bool _selectedIsAlly = true;
        private int _selectedSlot;

        // ------------------------------------------------------------------ animation model

        private static readonly string[] Clips =
            { "idle", "basic", "basic2", "skill", "hit_reaction", "death", "controlled" };

        private string _clipId = "idle";
        private bool _loop = true;
        private bool _playing = true;
        private float _speed = 1f;
        private double _lastTick;
        private float _clock;           // seconds into the clip
        private int _manualFrame = -1;  // >=0 pins a frame (frame stepping)
        private bool _showGround = true;
        private bool _showFoot = true;
        private int _compareAlly = -1;  // index into the ally row, -1 = off
        private bool _solo;             // draw the selection alone, as large as the view allows

        /// <summary>How far apart bodies stand, as a fraction of a body's drawn width. Under 1 they
        /// overlap, which is what a five-a-side line looks like in the game.</summary>
        private const float OverlapStep = 0.62f;

        /// <summary>Pixels reserved under the ground line for the name and frame counter.</summary>
        private const float GroundBand = 48f;

        // the hit test: one struck unit, a clock that runs with the animation clock so slow
        // motion slows the whole reaction together, and the number that floats off it
        private float _hitClock = -1f;
        private bool _hitAlly = true;
        private int _hitSlot = -1;
        private int _hitValue;
        private bool _hitCrit;
        private const float FlashSeconds = 0.36f;   // UnitActor decays _flash at 5.5/s from 1
        private const float NumberSeconds = 1.1f;

        // ------------------------------------------------------------------ combat model

        private ContentDatabase _content;
        private int _seed = 12345;
        private string _stageId = "";
        private BattleResult _result;
        private BattleResult _previous;     // the run before the last one, for the diff
        private int _cursor;                // index into _result.Events
        private string _runError = "";

        // ------------------------------------------------------------------ chrome

        private Vector2 _rosterScroll, _inspectorScroll, _logScroll;
        private string _search = "";
        private bool _fUnit = true, _fAnim = true, _fCombat = true, _fExplain = true, _fLimits = true;
        private bool _help = true;

        [MenuItem("Emberfall/Dev Zone", false, 20)]
        public static void Open()
        {
            DevZone w = GetWindow<DevZone>("Dev Zone");
            w.minSize = new Vector2(1100, 620);
        }

        private void OnEnable()
        {
            for (int i = 0; i < SlotsPerSide; i++)
            {
                if (_ally[i] == null) _ally[i] = new Slot();
                if (_foe[i] == null) _foe[i] = new Slot();
            }
            _lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            SeedDefaultBoard();
        }

        /// <summary>
        /// A board on first open, rather than ten empty slots and a guess about what to do.
        /// Five heroes who all have sprite clips, so the viewport shows the thing the window is
        /// for the moment it appears; anyone can clear it in one click. Silent if the ids are not
        /// in content - this is a convenience, not a dependency.
        /// </summary>
        private void SeedDefaultBoard()
        {
            for (int i = 0; i < SlotsPerSide; i++)
                if (!_ally[i].Empty || !_foe[i].Empty) return;   // never clobber a live board

            string[] allies = { "kael_the_unyielding", "kiva_the_forsaken_oath", "seraphine_the_closed_volume" };
            string[] foes = { "hale_the_relentless_hunter", "marrow_the_irrevocable_vow" };
            ContentDatabase c = Content;
            if (c == null) return;
            for (int i = 0; i < allies.Length && i < SlotsPerSide; i++)
                if (c.GetHero(allies[i]) != null) _ally[i].DefId = allies[i];
            for (int i = 0; i < foes.Length && i < SlotsPerSide; i++)
                if (c.GetHero(foes[i]) != null) _foe[i].DefId = foes[i];
        }

        private void OnDisable() { EditorApplication.update -= Tick; }

        /// <summary>
        /// The only thing in here that moves on its own, and it moves nothing but the animation
        /// clock. The simulation never advances without a click - see the step bar.
        /// </summary>
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTick);
            _lastTick = now;
            if (!_playing || _manualFrame >= 0) return;
            _clock += dt * _speed;
            // the hit reaction rides the same clock, so slowing the animation slows the flash and
            // the number with it - which is the only way to read impact timing frame by frame
            if (_hitClock >= 0f)
            {
                _hitClock += dt * _speed;
                if (_hitClock > NumberSeconds) _hitClock = -1f;
            }
            Repaint();
        }

        private ContentDatabase Content
        {
            get
            {
                if (_content == null) _content = ContentService.Load();
                return _content;
            }
        }

        // ================================================================== GUI

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawRosterAndBoard();
            DrawViewportColumn();
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(Content != null ? Content.Heroes.Count + " heroes loaded" : "no content",
                            EditorStyles.miniLabel, GUILayout.Width(120));
            if (GUILayout.Button("Reload content", EditorStyles.toolbarButton, GUILayout.Width(100)))
            { _content = null; _result = null; }
            GUILayout.Space(12);
            if (GUILayout.Button("Save board", EditorStyles.toolbarButton, GUILayout.Width(80))) SaveBoard();
            if (GUILayout.Button("Load board", EditorStyles.toolbarButton, GUILayout.Width(80))) LoadBoard();
            if (GUILayout.Button("Clear board", EditorStyles.toolbarButton, GUILayout.Width(84))) ClearBoard();
            GUILayout.FlexibleSpace();
            _help = GUILayout.Toggle(_help, "Show hints", EditorStyles.toolbarButton, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            if (_help)
                EditorGUILayout.HelpBox(
                    "Click a BOARD slot to select it, then click a hero in the ROSTER to place them there. " +
                    "The selected slot is what the INSPECTOR on the right is about. Nothing here runs by " +
                    "itself: the animation clock ticks, the fight does not - press Run, then step it.",
                    MessageType.None);
        }

        // ------------------------------------------------------------------ left column

        private void DrawRosterAndBoard()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RosterWidth));

            EditorGUILayout.LabelField("BOARD", EditorStyles.boldLabel);
            if (_help) EditorGUILayout.LabelField("front slot is 1; enemies mirror the ally line",
                                                  EditorStyles.miniLabel);
            DrawSide("Allies", _ally, true);
            DrawSide("Enemies (heroes, mirrored)", _foe, false);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("ROSTER", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("Filter", _search);
            if (_help) EditorGUILayout.LabelField("click a hero to put them in the selected slot",
                                                  EditorStyles.miniLabel);

            _rosterScroll = EditorGUILayout.BeginScrollView(_rosterScroll, GUILayout.Width(RosterWidth));
            if (Content != null)
            {
                for (int i = 0; i < Content.Heroes.Count; i++)
                {
                    HeroDef h = Content.Heroes[i];
                    if (!string.IsNullOrEmpty(_search) &&
                        h.Id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                        h.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    bool hasArt = BattleAssets.UnitAnimClip(h.Visual.Sprite, "idle") != null;
                    string label = (hasArt ? "* " : "  ") + h.Name + "   " + h.Id;
                    if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(RosterWidth - 28f)))
                    {
                        Slot s = Selected();
                        s.DefId = h.Id;
                        _clipId = "idle";
                        _clock = 0f;
                        _manualFrame = -1;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (_help) EditorGUILayout.LabelField("* = has sprite clips", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawSide(string title, Slot[] row, bool allySide)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            for (int i = 0; i < SlotsPerSide; i++)
            {
                bool isSel = (_selectedIsAlly == allySide) && _selectedSlot == i;
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = isSel ? new Color(0.5f, 0.8f, 1f) : Color.white;
                string name = row[i].Empty ? "(empty)" : ShortName(row[i].DefId);
                if (GUILayout.Button(i + 1 + "  " + name, EditorStyles.miniButton,
                                     GUILayout.Width(RosterWidth - 86f)))
                { _selectedIsAlly = allySide; _selectedSlot = i; _clock = 0f; _manualFrame = -1; }
                GUI.backgroundColor = Color.white;
                if (!row[i].Empty)
                {
                    GUILayout.Label("L" + row[i].Level, EditorStyles.miniLabel, GUILayout.Width(32));
                    if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20)))
                        row[i] = new Slot();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private Slot Selected() { return (_selectedIsAlly ? _ally : _foe)[_selectedSlot]; }

        private string ShortName(string defId)
        {
            HeroDef d = Content != null ? Content.GetHero(defId) : null;
            return d != null ? d.Name : defId;
        }

        // ------------------------------------------------------------------ centre column

        private void DrawViewportColumn()
        {
            // Explicit width, not "whatever is left". A GetRect that may expand to 4000 claims more
            // than the window has and pushes the inspector off the right edge, which is exactly how
            // a three-column editor window fails.
            // The centre takes WHAT IS LEFT. It is the only expanding child of the row, so IMGUI
            // hands it the remainder after the two fixed columns - which is right even when a long
            // hero id has made the roster a few pixels wider than it asked to be. The arithmetic
            // below is only an ESTIMATE, and only ever feeds a height, where being a little out
            // costs nothing.
            float centre = Mathf.Max(260f, position.width - RosterWidth - InspectorWidth - 24f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // Ask for the height the board actually NEEDS at this width. A board scaled down to
            // fit across does not become taller by being given more room, so a rect that always
            // took everything left nothing for the step bar and hung the heroes under a ceiling
            // of empty sky.
            float tallest, widest;
            int perSide;
            MeasureBoard(out tallest, out widest, out perSide);
            float byWidth = (centre * 0.5f) / Mathf.Max(0.01f, widest * (OverlapStep * perSide + 0.55f));
            float want = tallest * byWidth + GroundBand + 24f;
            float avail = Mathf.Max(240f, position.height - 190f);
            float h = Mathf.Clamp(want, 240f, avail);

            Rect view = GUILayoutUtility.GetRect(10f, 10000f, h, h, GUILayout.ExpandWidth(true));
            DrawViewport(view);
            DrawStepBar();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws both lines standing on one ground line, using the SAME arithmetic UnitRig.BuildSprite
        /// uses - canvasHeight = silhouetteTop*scale / bodyFraction, and the frame placed so the
        /// footFraction slice of it hangs below the line. That is the point: if a hero floats or
        /// sinks here, they float or sink in the game, because this is not a second opinion about
        /// where feet go, it is the same formula fed the same numbers.
        ///
        /// It is not the game camera. It draws the sheets flat, so a shader, a sorting order or a
        /// camera framing problem will NOT show up here - see "What this cannot tell you".
        /// </summary>
        private void DrawViewport(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.16f));
            if (Content == null) return;

            // A fixed band under the line, not a fraction of it: what lives down there is two rows
            // of fixed-size text, and a fraction gave a short view no room and a tall view a field.
            float groundY = r.yMax - GroundBand;

            // Pixels per world unit, sized so the TALLEST canvas on the board fits above the line
            // with room to spare - not off a fixed constant. A canvas is silhouetteTop/bodyFraction
            // tall, so a hero drawn holding a raised weapon has a much taller canvas than his body,
            // and picking the scale off body height alone would push him off the top of the view.
            float tallest, widest;
            int perSide;
            MeasureBoard(out tallest, out widest, out perSide);

            // TWO constraints, not one. Height alone was letting a five-a-side board draw bodies
            // wider than the spacing between them, which is how ten heroes became one smear.
            // Across: each side gets half the width, a line of perSide bodies overlapping by a
            // little, plus half a body of margin for the outermost one.
            float byHeight = (groundY - r.yMin - 24f) / tallest;
            float byWidth = (r.width * 0.5f) / (widest * (OverlapStep * perSide + 0.55f));
            float ppu = Mathf.Max(8f, Mathf.Min(byHeight, byWidth));

            if (_showGround)
            {
                EditorGUI.DrawRect(new Rect(r.x, groundY, r.width, 1f), new Color(0.4f, 0.9f, 0.5f, 0.8f));
                Rect tag = new Rect(r.x + 4, groundY + 2, 120, 14);
                GUI.Label(tag, "ground y = 0", EditorStyles.miniLabel);
                float headY = groundY - UnitRig.HumanoidTop * ppu;
                EditorGUI.DrawRect(new Rect(r.x, headY, r.width, 1f), new Color(1f, 1f, 1f, 0.18f));
                GUI.Label(new Rect(r.x + 4, headY - 14, 200, 14),
                          "humanoid top = " + UnitRig.HumanoidTop.ToString("0.00"), EditorStyles.miniLabel);
            }

            // Ally line left of centre, enemy line right, front slot nearest the middle. Spacing
            // comes off the drawn width so a board of five never overlaps into unreadability.
            float mid = r.center.x;
            float step = widest * ppu * OverlapStep;
            float selX = _solo ? mid
                               : (_selectedIsAlly ? mid - step * (_selectedSlot + 0.6f)
                                                  : mid + step * (_selectedSlot + 0.6f));

            // The scale comparison is an OVERLAY, not a second body further along the line. Two
            // heroes standing apart can only be compared by eye; drawn ghosted on the same x, on
            // the same ground line, at the same pixels-per-unit, a head-height difference of a few
            // percent is visible - and that is the size of the differences that matter here.
            if (_compareAlly >= 0 && _compareAlly < SlotsPerSide && !_ally[_compareAlly].Empty)
            {
                DrawUnit(_ally[_compareAlly], selX, groundY, ppu, false, false, 0.4f, false);
                GUI.Label(new Rect(r.x + 4, r.y + 4, 460, 16),
                          "ghosted behind the selection for scale: " + ShortName(_ally[_compareAlly].DefId),
                          EditorStyles.miniLabel);
            }

            if (_solo)
            {
                DrawUnit(Selected(), mid, groundY, ppu, !_selectedIsAlly, true, 1f,
                         _hitAlly == _selectedIsAlly && _hitSlot == _selectedSlot);
                GUI.Label(new Rect(r.x + 4, r.y + 20, 460, 16),
                          "solo: only the selected slot is drawn", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < SlotsPerSide; i++)
                {
                    DrawUnit(_ally[i], mid - step * (i + 0.6f), groundY, ppu, false,
                             _selectedIsAlly && _selectedSlot == i, 1f, _hitAlly && _hitSlot == i);
                    DrawUnit(_foe[i], mid + step * (i + 0.6f), groundY, ppu, true,
                             !_selectedIsAlly && _selectedSlot == i, 1f, !_hitAlly && _hitSlot == i);
                }
            }
        }

        /// <summary>The canvas a slot's art is drawn on, in world units, or 0 for an empty slot.
        /// Same expression BuildSprite uses; the viewport scale is derived from it.</summary>
        /// <summary>
        /// The board's extent in world units: the tallest canvas, the widest canvas, and how many
        /// bodies deep the longer line is. Both the rect the viewport asks for and the scale it
        /// draws at come off these, so they are measured once, in one place.
        /// </summary>
        private void MeasureBoard(out float tallest, out float widest, out int perSide)
        {
            tallest = 1f; widest = 0.5f; perSide = 1;
            for (int i = 0; i < SlotsPerSide; i++)
            {
                tallest = Mathf.Max(tallest, CanvasHeight(_ally[i]));
                tallest = Mathf.Max(tallest, CanvasHeight(_foe[i]));
                widest = Mathf.Max(widest, CanvasWidth(_ally[i]));
                widest = Mathf.Max(widest, CanvasWidth(_foe[i]));
                if (!_ally[i].Empty) perSide = Mathf.Max(perSide, i + 1);
                if (!_foe[i].Empty) perSide = Mathf.Max(perSide, i + 1);
            }
            if (_solo) perSide = 1;
        }

        /// <summary>The canvas WIDTH in world units - height times the sheet's frame aspect. The
        /// sheets are not square, so spacing derived from height alone is wrong by that ratio.</summary>
        private float CanvasWidth(Slot slot)
        {
            float h = CanvasHeight(slot);
            if (h <= 0f) return 0f;
            HeroDef d = Content.GetHero(slot.DefId);
            SpriteAnimClip c = d != null ? BattleAssets.UnitAnimClip(d.Visual.Sprite, "idle") : null;
            float aspect = (c != null && c.FrameHeight > 0) ? (float)c.FrameWidth / c.FrameHeight : 0.6f;
            return h * aspect;
        }

        private float CanvasHeight(Slot slot)
        {
            if (slot == null || slot.Empty || Content == null) return 0f;
            HeroDef d = Content.GetHero(slot.DefId);
            if (d == null) return 0f;
            return UnitRig.SilhouetteTop(d.Visual.Silhouette) * Mathf.Max(0.05f, d.Visual.Scale)
                   / Mathf.Max(0.05f, d.Visual.SpriteBodyFraction);
        }

        private void DrawUnit(Slot slot, float centreX, float groundY, float ppu, bool faceLeft,
                              bool selected, float alpha, bool struck)
        {
            if (slot.Empty || Content == null) return;
            HeroDef def = Content.GetHero(slot.DefId);
            if (def == null) return;

            SpriteAnimClip clip = BattleAssets.UnitAnimClip(def.Visual.Sprite, _clipId);
            if (clip == null) clip = BattleAssets.UnitAnimClip(def.Visual.Sprite, "idle");

            float top = UnitRig.SilhouetteTop(def.Visual.Silhouette) * Mathf.Max(0.05f, def.Visual.Scale);
            float body = Mathf.Max(0.05f, def.Visual.SpriteBodyFraction);
            float canvasH = top / body;

            if (clip == null)
            {
                // No art: say so plainly rather than drawing nothing, because "nothing appeared"
                // is exactly the ambiguity this window exists to remove.
                float h = top * ppu;
                Rect box = new Rect(centreX - 20f, groundY - h, 40f, h);
                EditorGUI.DrawRect(box, new Color(0.35f, 0.3f, 0.4f, 0.8f));
                GUI.Label(new Rect(centreX - 46f, groundY + 2f, 100f, 14f), "no clips", EditorStyles.miniLabel);
                GUI.Label(new Rect(centreX - 46f, groundY + 14f, 120f, 14f), def.Name, EditorStyles.miniLabel);
                return;
            }

            float foot = clip.FootFraction >= 0f ? clip.FootFraction : def.Visual.SpriteFootFraction;
            float aspect = clip.FrameHeight > 0 ? (float)clip.FrameWidth / clip.FrameHeight : 0.6f;
            float drawH = canvasH * ppu;
            float drawW = drawH * aspect;
            // bottom of the canvas sits footFraction BELOW the ground line, so the feet land on it
            float bottom = groundY + foot * drawH;
            Rect dst = new Rect(centreX - drawW * 0.5f, bottom - drawH, drawW, drawH);

            int frame = CurrentFrame(clip);
            int col = frame % Mathf.Max(1, clip.Columns);
            int row = frame / Mathf.Max(1, clip.Columns);
            // Unity texture coords are bottom-up; sheets are laid out top-down
            float uw = 1f / clip.Columns, vh = 1f / clip.Rows;
            Rect uv = new Rect(col * uw, 1f - (row + 1) * vh, uw, vh);
            bool flip = faceLeft ^ slot.Mirror ^ def.Visual.SpriteMirror;
            if (flip) { uv.x += uv.width; uv.width = -uv.width; }

            Color was = GUI.color;
            if (alpha < 1f) GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(dst, clip.Texture, uv, true);
            GUI.color = was;

            if (struck && _hitClock >= 0f) DrawHitFeedback(dst, centreX, groundY);
            if (selected) DrawOutline(dst, new Color(0.4f, 0.8f, 1f, 0.9f));
            if (_showFoot)
            {
                EditorGUI.DrawRect(new Rect(centreX - 14f, groundY - 1f, 28f, 3f),
                                   new Color(1f, 0.6f, 0.2f, 0.95f));
                EditorGUI.DrawRect(new Rect(dst.x, dst.yMax - 1f, dst.width, 1f),
                                   new Color(1f, 0.3f, 0.3f, 0.5f));
            }
            GUI.Label(new Rect(centreX - 46f, groundY + 4f, 120f, 14f), def.Name, EditorStyles.miniLabel);
            GUI.Label(new Rect(centreX - 46f, groundY + 16f, 140f, 14f),
                      "f " + frame + "/" + clip.EndFrame, EditorStyles.miniLabel);
        }

        /// <summary>
        /// The struck unit's reaction, on the same clock as the clip so the three land together.
        ///
        /// The white flash itself is a shader uniform (_Flash on the sprite material, decayed at
        /// 5.5/s by UnitActor), and this viewport draws sheets flat with no material, so what is
        /// drawn here is the flash's TIMING and DURATION rather than its colour - a bright frame
        /// around the body for exactly as long as the game whitens it. That is the half worth
        /// measuring: frame counts, not hue. The hint under the buttons says so on screen.
        /// </summary>
        private void DrawHitFeedback(Rect dst, float centreX, float groundY)
        {
            if (_hitClock <= FlashSeconds)
            {
                float k = 1f - _hitClock / FlashSeconds;
                DrawOutline(dst, new Color(1f, 1f, 1f, 0.25f + 0.75f * k));
                Rect inner = new Rect(dst.x + 1, dst.y + 1, dst.width - 2, dst.height - 2);
                DrawOutline(inner, new Color(1f, 1f, 1f, 0.5f * k));
            }
            float t = Mathf.Clamp01(_hitClock / NumberSeconds);
            float y = groundY - dst.height * 0.55f - t * 46f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 1f - t * t);
            GUI.Label(new Rect(centreX - 40f, y, 120f, 18f),
                      (_hitValue > 0 ? _hitValue.ToString() : "hit") + (_hitCrit ? "!" : ""),
                      EditorStyles.whiteLabel);
            GUI.color = prev;
        }

        /// <summary>
        /// Strike the selected unit. If the cursor is parked on a Damage event, the number that
        /// floats is THAT number off the real fight, and it carries that blow's crit flag - so the
        /// reaction being watched is the one the simulation produced, not a made-up one. With no
        /// fight run, it plays the reaction with no number.
        /// </summary>
        private void TriggerHit()
        {
            _hitAlly = _selectedIsAlly;
            _hitSlot = _selectedSlot;
            _hitValue = 0;
            _hitCrit = false;
            if (_result != null && _result.Events.Count > 0)
            {
                BattleEvent e = _result.Events[Mathf.Clamp(_cursor, 0, _result.Events.Count - 1)];
                if (e.Type == BattleEventType.Damage) { _hitValue = e.Value; _hitCrit = e.Critical; }
            }
            _clipId = "hit_reaction";
            _clock = 0f;
            _manualFrame = -1;
            _playing = true;
            _hitClock = 0f;
        }

        private static void DrawOutline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }

        private int CurrentFrame(SpriteAnimClip clip)
        {
            int count = Mathf.Max(1, clip.FrameCount);
            if (_manualFrame >= 0) return clip.StartFrame + Mathf.Clamp(_manualFrame, 0, count - 1);
            float fps = clip.Fps * clip.PlaybackSpeed;
            int n = Mathf.FloorToInt(_clock * fps);
            if (_loop) n = ((n % count) + count) % count;
            else n = Mathf.Clamp(n, 0, count - 1);
            return clip.StartFrame + n;
        }

        // ------------------------------------------------------------------ step bar

        private void DrawStepBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.enabled = HasAnyAlly();
            if (GUILayout.Button("Run fight", GUILayout.Width(80))) RunFight();
            GUI.enabled = _result != null;
            if (GUILayout.Button("|<", GUILayout.Width(30))) _cursor = 0;
            if (GUILayout.Button("< event", GUILayout.Width(66))) _cursor = Mathf.Max(0, _cursor - 1);
            if (GUILayout.Button("event >", GUILayout.Width(66))) StepEvent(1);
            if (GUILayout.Button("action >", GUILayout.Width(72))) StepAction();
            if (GUILayout.Button(">|", GUILayout.Width(30)))
                _cursor = _result != null ? _result.Events.Count - 1 : 0;
            GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label("seed", GUILayout.Width(32));
            _seed = EditorGUILayout.IntField(_seed, GUILayout.Width(70));
            if (GUILayout.Button("re-roll", EditorStyles.miniButton, GUILayout.Width(52)))
                _seed = UnityEngine.Random.Range(1, 999999);
            GUILayout.FlexibleSpace();
            if (_result != null)
                GUILayout.Label("event " + (_cursor + 1) + " / " + _result.Events.Count +
                                "   round " + _result.Events[Mathf.Clamp(_cursor, 0, _result.Events.Count - 1)].Round,
                                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_runError)) EditorGUILayout.HelpBox(_runError, MessageType.Warning);
        }

        private bool HasAnyAlly()
        {
            for (int i = 0; i < SlotsPerSide; i++) if (!_ally[i].Empty) return true;
            return false;
        }

        private void StepEvent(int dir)
        {
            if (_result == null) return;
            _cursor = Mathf.Clamp(_cursor + dir, 0, _result.Events.Count - 1);
        }

        /// <summary>Next boundary that starts an action: a blow, a cast, or a new turn.</summary>
        private void StepAction()
        {
            if (_result == null) return;
            for (int i = _cursor + 1; i < _result.Events.Count; i++)
            {
                BattleEventType t = _result.Events[i].Type;
                if (t == BattleEventType.BasicAttack || t == BattleEventType.AbilityCast ||
                    t == BattleEventType.TurnStart || t == BattleEventType.RoundStart)
                { _cursor = i; return; }
            }
            _cursor = _result.Events.Count - 1;
        }

        // ------------------------------------------------------------------ running

        private void RunFight()
        {
            _runError = "";
            try
            {
                BattleRequest req = new BattleRequest();
                req.Content = Content;
                req.Seed = _seed;
                for (int i = 0; i < SlotsPerSide; i++)
                {
                    if (_ally[i].Empty) continue;
                    HeroDef def = Content.GetHero(_ally[i].DefId);
                    if (def == null) continue;
                    HeroInstance h = new HeroInstance();
                    h.Uid = "dz" + i;
                    h.DefId = def.Id;
                    h.Def = def;
                    h.Level = _ally[i].Level;
                    h.Copies = 1;
                    h.SelectedTalent = _ally[i].Talent;
                    h.EquippedRelicId = _ally[i].RelicId ?? "";
                    req.Team.Add(h);
                }

                // The far side as heroes, through the same door The Proving uses. This is why
                // enemies render as heroes rather than as enemy defs: they ARE heroes to the
                // simulation, not a special case this window invented.
                BattleLoadout line = null;
                for (int i = 0; i < SlotsPerSide; i++)
                {
                    if (_foe[i].Empty) continue;
                    HeroDef def = Content.GetHero(_foe[i].DefId);
                    if (def == null) continue;
                    if (line == null) { line = new BattleLoadout(); line.Label = "Dev Zone line"; }
                    LoadoutUnit u = new LoadoutUnit();
                    u.DefId = def.Id;
                    u.Level = _foe[i].Level;
                    u.Stars = def.Stars;
                    u.RelicId = _foe[i].RelicId ?? "";
                    line.Units.Add(u);
                }
                req.Opponent = line;

                StageDef stage = null;
                if (!string.IsNullOrEmpty(_stageId)) stage = Content.GetStage(_stageId);
                if (stage == null && Content.AllStages.Count > 0) stage = Content.AllStages[0];
                req.Stage = stage;
                if (stage == null) { _runError = "No stage in content to fight on."; return; }
                if (line == null && (stage.Enemies == null || stage.Enemies.Count == 0))
                { _runError = "Put a hero on the enemy side, or pick a stage that has enemies."; return; }

                _previous = _result;
                _result = CombatSimulation.Run(req);
                _cursor = 0;
            }
            catch (Exception ex)
            {
                _runError = "Run failed: " + ex.Message;
                _result = null;
            }
        }

        // ------------------------------------------------------------------ right column

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(InspectorWidth));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll,
                                                               GUILayout.Width(InspectorWidth));

            DrawUnitSection();
            DrawAnimSection();
            DrawCombatSection();
            DrawExplainSection();
            DrawLimitsSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawUnitSection()
        {
            _fUnit = EditorGUILayout.Foldout(_fUnit, "UNIT  —  " +
                (Selected().Empty ? "(empty slot)" : ShortName(Selected().DefId)), true);
            if (!_fUnit) return;
            Slot s = Selected();
            if (s.Empty)
            {
                EditorGUILayout.HelpBox("This slot is empty. Click a hero in the roster to fill it.",
                                        MessageType.None);
                return;
            }
            HeroDef def = Content.GetHero(s.DefId);
            if (def == null) { EditorGUILayout.HelpBox("Hero id not in content.", MessageType.Warning); return; }

            EditorGUI.indentLevel++;
            s.Level = EditorGUILayout.IntSlider("Level", s.Level, 1, 80);
            s.Talent = EditorGUILayout.IntPopup("Talent", s.Talent,
                new[] { "none", "I", "II", "III" }, new[] { 0, 1, 2, 3 });
            if (s.Talent > 0)
            {
                PassiveDef t = def.TalentAt(s.Talent);
                EditorGUILayout.LabelField(" ", t != null ? t.Name : "(this hero has no talent " + s.Talent + ")",
                                           EditorStyles.miniLabel);
            }
            s.RelicId = EditorGUILayout.TextField("Relic id", s.RelicId ?? "");
            s.Mirror = EditorGUILayout.Toggle("Mirror", s.Mirror);

            EditorGUILayout.LabelField("Derived", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("silhouette", def.Visual.Silhouette + "  top " +
                UnitRig.SilhouetteTop(def.Visual.Silhouette).ToString("0.000") +
                "  scale " + def.Visual.Scale.ToString("0.00"));
            EditorGUILayout.LabelField("bodyFraction", def.Visual.SpriteBodyFraction.ToString("0.000"));
            EditorGUILayout.LabelField("footFraction", def.Visual.SpriteFootFraction.ToString("0.000") +
                                       "   (hero level, used when a clip omits its own)");
            for (int i = 0; i < Clips.Length; i++)
            {
                SpriteAnimClip c = BattleAssets.UnitAnimClip(def.Visual.Sprite, Clips[i]);
                EditorGUILayout.LabelField(Clips[i], c == null ? "—" :
                    "frames " + c.FrameCount + "   fps " + c.Fps.ToString("0.#") +
                    " x" + c.PlaybackSpeed.ToString("0.00") +
                    "   foot " + (c.FootFraction >= 0f ? c.FootFraction.ToString("0.000") : "(hero)") +
                    "   impact " + c.ImpactFraction.ToString("0.00"));
            }
            EditorGUI.indentLevel--;
        }

        private void DrawAnimSection()
        {
            _fAnim = EditorGUILayout.Foldout(_fAnim, "ANIMATION", true);
            if (!_fAnim) return;
            EditorGUI.indentLevel++;
            if (_help)
                EditorGUILayout.LabelField("clips play on every unit on the board at once, so two "
                                           + "heroes can be compared in the same pose",
                                           EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < Clips.Length; i++)
            {
                GUI.backgroundColor = _clipId == Clips[i] ? new Color(0.5f, 0.8f, 1f) : Color.white;
                if (GUILayout.Button(Clips[i], EditorStyles.miniButton))
                { _clipId = Clips[i]; _clock = 0f; _manualFrame = -1; }
                if (i == 3) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _playing = GUILayout.Toggle(_playing, _playing ? "pause" : "play", EditorStyles.miniButton, GUILayout.Width(52));
            _loop = GUILayout.Toggle(_loop, "loop", EditorStyles.miniButton, GUILayout.Width(44));
            if (GUILayout.Button("|<<", EditorStyles.miniButton, GUILayout.Width(36)))
            { _manualFrame = 0; }
            if (GUILayout.Button("< f", EditorStyles.miniButton, GUILayout.Width(36)))
            { _manualFrame = Mathf.Max(0, (_manualFrame < 0 ? 0 : _manualFrame) - 1); }
            if (GUILayout.Button("f >", EditorStyles.miniButton, GUILayout.Width(36)))
            { _manualFrame = (_manualFrame < 0 ? 0 : _manualFrame) + 1; }
            if (GUILayout.Button("live", EditorStyles.miniButton, GUILayout.Width(40)))
            { _manualFrame = -1; _clock = 0f; }
            EditorGUILayout.EndHorizontal();
            if (_manualFrame >= 0)
                EditorGUILayout.LabelField(" ", "frame pinned at " + _manualFrame + " — press live to resume",
                                           EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !Selected().Empty;
            if (GUILayout.Button("Trigger hit on selected", GUILayout.Width(170))) TriggerHit();
            GUI.enabled = true;
            GUILayout.Label(_result != null && _result.Events.Count > 0 &&
                            _result.Events[Mathf.Clamp(_cursor, 0, _result.Events.Count - 1)].Type
                                == BattleEventType.Damage
                            ? "uses the number at the cursor" : "no damage at the cursor - no number",
                            EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (_help)
                EditorGUILayout.LabelField(" ", "reaction clip, flash window and number run on the "
                                           + "animation clock - slow the speed to count the frames "
                                           + "between the hit and the recoil. The flash is drawn as "
                                           + "a bright frame for the same duration the game whitens "
                                           + "the body; this view has no material, so the timing is "
                                           + "true and the colour is not.", EditorStyles.miniLabel);

            _speed = EditorGUILayout.Slider("Speed", _speed, 0.05f, 2f);
            if (_help && _speed < 0.3f)
                EditorGUILayout.LabelField(" ", "slow motion: impact timing is measured in frames",
                                           EditorStyles.miniLabel);
            _solo = EditorGUILayout.Toggle("Solo the selection", _solo);
            _showGround = EditorGUILayout.Toggle("Ground line", _showGround);
            _showFoot = EditorGUILayout.Toggle("Foot anchor", _showFoot);
            if (_help && _showFoot)
                EditorGUILayout.LabelField(" ", "orange = where the engine puts the feet; red = the "
                                           + "frame's own bottom edge", EditorStyles.miniLabel);

            string[] names = new string[SlotsPerSide + 1];
            int[] vals = new int[SlotsPerSide + 1];
            names[0] = "off"; vals[0] = -1;
            for (int i = 0; i < SlotsPerSide; i++)
            { names[i + 1] = (i + 1) + " " + (_ally[i].Empty ? "(empty)" : ShortName(_ally[i].DefId)); vals[i + 1] = i; }
            _compareAlly = EditorGUILayout.IntPopup("Compare with", _compareAlly, names, vals);
            EditorGUI.indentLevel--;
        }

        private void DrawCombatSection()
        {
            _fCombat = EditorGUILayout.Foldout(_fCombat, "COMBAT", true);
            if (!_fCombat) return;
            EditorGUI.indentLevel++;

            if (Content != null && Content.AllStages.Count > 0)
            {
                string[] ids = new string[Content.AllStages.Count];
                for (int i = 0; i < ids.Length; i++) ids[i] = Content.AllStages[i].Id;
                int idx = Mathf.Max(0, Array.IndexOf(ids, _stageId));
                idx = EditorGUILayout.Popup("Stage", idx, ids);
                _stageId = ids[idx];
                if (_help)
                    EditorGUILayout.LabelField(" ", "the stage supplies the environment; the enemy "
                                               + "line above replaces its enemies when it has one",
                                               EditorStyles.miniLabel);
            }

            if (_result == null)
            {
                EditorGUILayout.HelpBox("Press Run fight. The whole fight resolves at once — "
                                        + "CombatSimulation is a pure function — and then you step "
                                        + "its record here. Same seed, same fight, every time.",
                                        MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.LabelField("Outcome",
                (_result.Victory ? "victory" : "defeat") + "   rounds " + _result.Rounds +
                "   events " + _result.Events.Count);
            if (_previous != null)
            {
                int diff = FirstDivergence(_previous, _result);
                EditorGUILayout.LabelField("vs previous run",
                    diff < 0 ? "identical, event for event"
                             : "diverges at event " + (diff + 1) + " of " + _result.Events.Count);
                if (_help)
                    EditorGUILayout.LabelField(" ", "change one thing, run again, and this says where "
                                               + "the two fights parted", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private static int FirstDivergence(BattleResult a, BattleResult b)
        {
            int n = Mathf.Min(a.Events.Count, b.Events.Count);
            for (int i = 0; i < n; i++)
                if (Describe(a, a.Events[i]) != Describe(b, b.Events[i])) return i;
            return a.Events.Count == b.Events.Count ? -1 : n;
        }

        // ------------------------------------------------------------------ the explain pane

        private void DrawExplainSection()
        {
            _fExplain = EditorGUILayout.Foldout(_fExplain, "WHAT THE RECORD SAYS", true);
            if (!_fExplain || _result == null) return;
            EditorGUI.indentLevel++;

            int at = Mathf.Clamp(_cursor, 0, _result.Events.Count - 1);
            EditorGUILayout.LabelField("this event", Describe(_result, _result.Events[at]));

            EditorGUILayout.LabelField("the action around it", EditorStyles.miniBoldLabel);
            int from = at;
            while (from > 0)
            {
                BattleEventType t = _result.Events[from].Type;
                if (t == BattleEventType.BasicAttack || t == BattleEventType.AbilityCast ||
                    t == BattleEventType.TurnStart) break;
                from--;
            }
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(190));
            for (int i = from; i < _result.Events.Count; i++)
            {
                BattleEventType t = _result.Events[i].Type;
                if (i > from && (t == BattleEventType.BasicAttack || t == BattleEventType.AbilityCast ||
                                 t == BattleEventType.TurnStart)) break;
                GUIStyle st = i == at ? EditorStyles.whiteLabel : EditorStyles.label;
                EditorGUILayout.LabelField((i == at ? "> " : "  ") + Describe(_result, _result.Events[i]), st);
            }
            EditorGUILayout.EndScrollView();
            EditorGUI.indentLevel--;
        }

        /// <summary>One event as a sentence. The record is the only honest source here.</summary>
        private static string Describe(BattleResult r, BattleEvent e)
        {
            string src = Name(r, e.SourceId), tgt = Name(r, e.TargetId);
            switch (e.Type)
            {
                case BattleEventType.RoundStart: return "— round " + e.Value + " —";
                case BattleEventType.TurnStart: return src + " takes a turn";
                case BattleEventType.BasicAttack: return src + " uses " + (e.AbilityName ?? "a basic");
                case BattleEventType.AbilityCast: return src + " casts " + (e.AbilityName ?? "an active");
                case BattleEventType.PassiveTrigger: return "    passive fired: " + (e.AbilityName ?? e.AbilityId);
                case BattleEventType.Damage:
                    return "    " + src + " hits " + tgt + " for " + e.Value +
                           (e.Critical ? " (crit)" : "") +
                           (Math.Abs(e.AspectFactor - 1f) > 0.001f ? "  aspect x" + e.AspectFactor.ToString("0.##") : "") +
                           (string.IsNullOrEmpty(e.AbilityName) ? "" : "  [" + e.AbilityName + "]");
                case BattleEventType.Heal: return "    " + src + " heals " + tgt + " for " + e.Value;
                case BattleEventType.ShieldGain: return "    " + tgt + " gains a " + e.Value + " shield";
                case BattleEventType.ShieldAbsorb: return "    " + tgt + "'s shield absorbs " + e.Value;
                case BattleEventType.StatusApplied: return "    " + tgt + " gains " + e.Status + " (" + e.Duration + "r)";
                case BattleEventType.StatusResisted: return "    " + tgt + " RESISTED " + e.Status;
                case BattleEventType.StatusExpired: return "    " + tgt + " loses " + e.Status;
                case BattleEventType.StatusSpread: return "    " + e.Status + " moves from " + src + " to " + tgt;
                case BattleEventType.EnergyChanged: return "    " + tgt + " energy -> " + e.Value;
                case BattleEventType.Death: return "    " + tgt + " dies";
                case BattleEventType.CastCancelled: return "    " + tgt + "'s " + e.AbilityName + " was cancelled";
                case BattleEventType.TurnOrderShift: return "    " + tgt + " turn shifted " + e.Value;
                case BattleEventType.PositionSwapped: return "    " + tgt + " swaps position";
                case BattleEventType.BattleEnd: return "— battle end: " + (e.Victory ? "victory" : "defeat") + " —";
                default: return "    " + e.Type;
            }
        }

        private static string Name(BattleResult r, int id)
        {
            if (id < 0) return "nobody";
            UnitSnapshot u = r.FindUnit(id);
            return u != null ? u.Name : ("#" + id);
        }

        // ------------------------------------------------------------------ the honest limits

        private void DrawLimitsSection()
        {
            _fLimits = EditorGUILayout.Foldout(_fLimits, "WHAT THIS CANNOT TELL YOU", true);
            if (!_fLimits) return;
            EditorGUILayout.HelpBox(
                "Everything above is read off the finished fight's event list, which is the only " +
                "record the simulation produces. These questions are NOT in it, and answering them " +
                "needs a change inside Assets/Scripts rather than another editor window:\n\n" +
                "• which TargetMode ran, who the candidates were, and what vetoed them. " +
                "Concealment, target bans and the Taunt anchor all redirect silently.\n" +
                "• every condition evaluated, its subject, the value it read, and whether it passed.\n" +
                "• which passives were refused by a limit or a budget, and which never heard the " +
                "trigger at all. Only the ones that FIRED leave a record.\n" +
                "• the damage arithmetic step by step: base, scaling, mitigation, funnels, " +
                "redirection. Only the final number is recorded.\n" +
                "• effects that emit no event — ExtendStatus, RecordFact, LedgerIncrement — are " +
                "invisible here whether they ran or not.\n\n" +
                "Nor can a fight be reached into once it starts: manual targeting, choosing a unit's " +
                "ability for a turn, setting HP or energy mid-fight, applying a status by hand and " +
                "swapping a unit mid-fight all need the simulation to accept a starting state and " +
                "expose a decision trace. It accepts neither today.",
                MessageType.Info);
        }

        // ------------------------------------------------------------------ board save / load

        private void SaveBoard()
        {
            string path = EditorUtility.SaveFilePanel("Save board", Application.dataPath + "/..", "board", "txt");
            if (string.IsNullOrEmpty(path)) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Emberfall dev-zone board");
            sb.AppendLine("seed\t" + _seed);
            sb.AppendLine("stage\t" + _stageId);
            for (int i = 0; i < SlotsPerSide; i++) WriteSlot(sb, "ally", i, _ally[i]);
            for (int i = 0; i < SlotsPerSide; i++) WriteSlot(sb, "foe", i, _foe[i]);
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSlot(StringBuilder sb, string side, int i, Slot s)
        {
            if (s.Empty) return;
            sb.AppendLine(side + "\t" + i + "\t" + s.DefId + "\t" + s.Level + "\t" + s.Talent +
                          "\t" + (s.RelicId ?? "") + "\t" + (s.Mirror ? 1 : 0));
        }

        private void LoadBoard()
        {
            string path = EditorUtility.OpenFilePanel("Load board", Application.dataPath + "/..", "txt");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            ClearBoard();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('\t');
                if (p[0] == "seed" && p.Length > 1) { int.TryParse(p[1], out _seed); continue; }
                if (p[0] == "stage" && p.Length > 1) { _stageId = p[1]; continue; }
                if (p.Length < 7) continue;
                Slot[] row = p[0] == "ally" ? _ally : _foe;
                int idx; if (!int.TryParse(p[1], out idx) || idx < 0 || idx >= SlotsPerSide) continue;
                Slot s = new Slot();
                s.DefId = p[2];
                int.TryParse(p[3], out s.Level);
                int.TryParse(p[4], out s.Talent);
                s.RelicId = p[5];
                s.Mirror = p[6] == "1";
                row[idx] = s;
            }
            _result = null; _previous = null;
        }

        private void ClearBoard()
        {
            for (int i = 0; i < SlotsPerSide; i++) { _ally[i] = new Slot(); _foe[i] = new Slot(); }
            _result = null; _previous = null; _cursor = 0;
            _hitClock = -1f; _hitSlot = -1; _compareAlly = -1;
        }
    }
}
#endif
