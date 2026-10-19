using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.UI;

namespace Emberfall.Battle
{
    /// <summary>
    /// Hit feedback for the battle view: flash, hit stop, knockback, camera shake, a particle burst
    /// and, once a sheet exists, a flipbook. All of it is presentation. The director reads the
    /// BattleEvent the host is playing (source, target, crit, AspectFactor, status, shield
    /// absorbed, death) and never computes damage, picks a target or decides an outcome.
    ///
    /// PLAYBACK, NEVER RESOLUTION. Nothing here runs when a battle resolves. It runs when the host
    /// PLAYS the event, which for a Damage is the moment SpriteAnimator's ImpactFraction fires it,
    /// so a battle that resolves without being watched spends nothing on effects.
    ///
    /// TWO WAYS IN, ONE SET OF EFFECTS.
    ///
    ///   Play(BattleEvent) is the exact feed: a host that has the event in hand calls it as the
    ///   event plays and sets ExactFeed, which turns the taps below off so nothing fires twice.
    ///   The Dev Arena does this.
    ///
    ///   The taps (NoteHit, NoteDeath, NoteShield) are how a host that cannot be edited still gets
    ///   effects. UnitActor reports the three things the host already makes it do at those
    ///   moments: PlayHit, PlayDeath and a fall in DisplayShield. The director then reads the
    ///   matching event off the BattleResult it was bound to, in order, per event type, matching on
    ///   target (and on crit for a hit). That is a lookup into the record, not a derivation, and a
    ///   tap that finds no match still plays a plain, neutral effect rather than a wrong one.
    ///   Nothing in the game uses the taps now: BattleScreen and the Dev Arena both feed Play. A
    ///   StatusApplied has no tap, because nothing in the actor is touched when one plays; it
    ///   needs the exact feed.
    ///
    /// ART BIBLE. No element-literal effects: no flames because the attacker is Ember, nothing
    /// smoke-like for Umbra. Every shape is a neutral chip, streak or ring, tinted by the
    /// attacker's aspect accent (UITheme.AspectAccent, the bible's own values).
    ///
    /// BUDGET. Three particle systems are built once and reused, capped at MaxChips + MaxStreaks +
    /// MaxRings alive in total, so ten units fighting cannot grow it. A hit that would overrun the
    /// cap is thinned; a crit or a kill is never thinned.
    /// </summary>
    public sealed class VfxDirector : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning

        /// <summary>Seconds both units freeze for on a blow. A crit holds longer.</summary>
        public const float HitStopSeconds = 0.05f;
        public const float CritHitStopSeconds = 0.09f;

        private const int MaxChips = 96;
        private const int MaxStreaks = 40;
        private const int MaxRings = 16;
        private const int ThinAbove = 70;
        private const int MaxFlipbooks = 8;

        // ------------------------------------------------------------------ state

        /// <summary>True once a host feeds events through Play. Silences the taps.</summary>
        public bool ExactFeed;

        private BattleStage _stage;
        private BattleResult _result;
        private readonly int[] _cursor = new int[64];

        /// <summary>R12: the unit whose cast is the current action, or -1. Mirrors BattleScreen's
        /// _skillActive from the events Play is fed: set by AbilityCast, cleared by BasicAttack and
        /// TurnStart. A Damage from this source with no status is the cast's strike.</summary>
        private int _skillSourceId = -1;

        /// <summary>R12: true once the current cast has had its skill moment (dim and ring), which
        /// lands on the cast's first impact, so it is shown with the blow and not before it.</summary>
        private bool _skillMomentDone;

        private ParticleSystem _chips;
        private ParticleSystem _streaks;
        private ParticleSystem _rings;
        private bool _built;

        private readonly List<Material> _materials = new List<Material>();

        private sealed class FlipQuad
        {
            public GameObject Go;
            public Transform T;
            public Material Material;
            public bool Additive;
        }

        private readonly Stack<FlipQuad> _quadPool = new Stack<FlipQuad>();
        private readonly List<FlipQuad> _quadLive = new List<FlipQuad>();

        /// <summary>R15 v2: one effect quad alive on one unit. The per-unit cap counts these.</summary>
        private sealed class FxHandle
        {
            public FlipQuad Quad;
            public int Unit;
            public int Priority;
            public float Born;
            public bool Retired;
        }

        private readonly Dictionary<int, List<FxHandle>> _unitFx = new Dictionary<int, List<FxHandle>>();
        private static readonly List<int> ScratchPriorities = new List<int>();
        private static readonly List<float> ScratchBorn = new List<float>();
        private static Texture2D _arrowTexture;
        private static Texture2D _shellTexture;

        // vfx.json, read once. Static so a second battle does not re-parse it; the file is
        // presentation only and cannot change under a running game.
        private static VfxConfig _config;
        private static readonly Dictionary<string, Texture2D> SheetCache = new Dictionary<string, Texture2D>();

        private static Texture2D _chipTexture;
        private static Texture2D _streakTexture;
        private static Texture2D _ringTexture;

        public static VfxConfig Config
        {
            get
            {
                if (_config == null)
                {
                    TextAsset asset = Resources.Load<TextAsset>("Content/vfx");
                    // Absent is fine and silent: the director falls back to its built-in defaults.
                    _config = VfxConfig.FromJson(asset != null ? asset.text : null);
                    for (int i = 0; i < _config.Warnings.Count; i++)
                        Debug.LogWarning("Emberfall content: " + _config.Warnings[i]);
                }
                return _config;
            }
        }

        // ------------------------------------------------------------------ wiring

        public void Attach(BattleStage stage)
        {
            _stage = stage;
        }

        /// <summary>Binds the record the host is about to play and rewinds the per-type cursors.</summary>
        public void Bind(BattleResult result)
        {
            _result = result;
            _skillSourceId = -1;
            for (int i = 0; i < _cursor.Length; i++) _cursor[i] = 0;
        }

        public void Clear()
        {
            StopAllCoroutines();
            for (int i = _quadLive.Count - 1; i >= 0; i--)
            {
                _quadLive[i].Go.SetActive(false);
                _quadPool.Push(_quadLive[i]);
            }
            _quadLive.Clear();
            _unitFx.Clear();
            if (_chips != null) _chips.Clear();
            if (_streaks != null) _streaks.Clear();
            if (_rings != null) _rings.Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++) if (_materials[i] != null) Destroy(_materials[i]);
            _materials.Clear();
        }

        // ------------------------------------------------------------------ entry: exact feed

        /// <summary>
        /// Plays the effects for one event, as the host plays it. Call it BEFORE the host mutates
        /// the unit's display state for the event (shield break is read off DisplayShield against
        /// the absorbed amount, so a host that has already subtracted it would read a break one
        /// absorb late). Events the director has no effect for are ignored.
        /// </summary>
        public void Play(BattleEvent e)
        {
            if (e == null || _stage == null) return;
            UnitActor target = e.TargetId >= 0 ? _stage.Find(e.TargetId) : null;
            switch (e.Type)
            {
                case BattleEventType.AbilityCast:
                    _skillSourceId = e.SourceId;
                    _skillMomentDone = false;
                    if (Feel.V2) PlayCastGlow(e.SourceId >= 0 ? _stage.Find(e.SourceId) : null);
                    break;
                case BattleEventType.BasicAttack:
                case BattleEventType.TurnStart:
                    _skillSourceId = -1;
                    break;
                case BattleEventType.Damage:
                    PlayDamage(e, target);
                    break;
                case BattleEventType.ShieldAbsorb:
                    if (target != null && Feel.V2) PlayShell(target, target.DisplayShield <= e.Value);
                    else if (target != null) PlayShield(e, target, target.DisplayShield <= e.Value);
                    break;
                case BattleEventType.ShieldGain:
                    if (Feel.V2) PlayShellGain(target);
                    break;
                case BattleEventType.Heal:
                    if (e.Value > 0 && Feel.V2) PlayHealLook(target);
                    else if (e.Value > 0) PlayBenefit(target, "heal", 0.35f);
                    break;
                case BattleEventType.StatusApplied:
                    if (Feel.V2) PlayStatusLook(e, target);
                    else PlayStatus(e, target);
                    break;
                case BattleEventType.StatusSpread:
                    if (Feel.V2) PlayStatusLook(e, target);
                    break;
                case BattleEventType.PassiveTrigger:
                    if (Feel.V2) PlayPassiveGlow(e.SourceId >= 0 ? _stage.Find(e.SourceId) : null);
                    break;
                case BattleEventType.Death:
                    PlayKill(e, target);
                    break;
            }
        }

        // ------------------------------------------------------------------ entry: taps

        internal void NoteHit(UnitActor target, bool critical)
        {
            if (ExactFeed || target == null || target.Snapshot == null) return;
            BattleEvent e = Take(BattleEventType.Damage, target.Snapshot.Id, true, critical);
            if (e == null)
            {
                // No matching record (a host playing a hand-built list). Nothing is invented about
                // who hit whom: an event with no source plays neutral, untinted and un-nudged.
                e = BattleEvent.Make(BattleEventType.Damage, 0);
                e.Critical = critical;
                e.TargetId = target.Snapshot.Id;
            }
            PlayDamage(e, target);
        }

        internal void NoteDeath(UnitActor target)
        {
            if (ExactFeed || target == null || target.Snapshot == null) return;
            BattleEvent e = Take(BattleEventType.Death, target.Snapshot.Id, false, false);
            if (e == null)
            {
                e = BattleEvent.Make(BattleEventType.Death, 0);
                e.TargetId = target.Snapshot.Id;
            }
            PlayKill(e, target);
        }

        internal void NoteShield(UnitActor target, float before, float after)
        {
            if (ExactFeed || target == null || target.Snapshot == null) return;
            BattleEvent e = Take(BattleEventType.ShieldAbsorb, target.Snapshot.Id, false, false);
            if (e == null)
            {
                e = BattleEvent.Make(BattleEventType.ShieldAbsorb, 0);
                e.TargetId = target.Snapshot.Id;
                e.Value = Mathf.RoundToInt(before - after);
            }
            PlayShield(e, target, after <= 0f && before > 0f);
        }

        /// <summary>The next unplayed event of a type aimed at a unit. Per-type cursors, so one
        /// type running ahead of another in the record cannot starve it.</summary>
        private BattleEvent Take(BattleEventType type, int targetId, bool matchCrit, bool crit)
        {
            if (_result == null) return null;
            List<BattleEvent> list = _result.Events;
            int slot = (int)type;
            if (slot < 0 || slot >= _cursor.Length) return null;
            for (int i = _cursor[slot]; i < list.Count; i++)
            {
                BattleEvent e = list[i];
                if (e.Type != type || e.TargetId != targetId) continue;
                if (matchCrit && e.Critical != crit) continue;
                _cursor[slot] = i + 1;
                return e;
            }
            return null;
        }

        // ------------------------------------------------------------------ the reads

        private void PlayDamage(BattleEvent e, UnitActor target)
        {
            if (target == null || !target.Alive || _stage == null) return;
            UnitActor source = e.SourceId >= 0 && e.SourceId != e.TargetId ? _stage.Find(e.SourceId) : null;

            bool crit = e.Critical;
            // A damage-over-time tick is damage with a status on it. It earns the flash and a few
            // motes, not a freeze and a shove: the host does not give it an impact burst either.
            bool tick = e.Status != StatusKind.None;

            target.SetFlashColor(Color.white);

            if (tick)
            {
                Color dot = StatusInfo.IsHarmful(e.Status) ? UITheme.Danger : UITheme.Success;
                EmitChips(FxPoint(target), dot, 3, 1.1f, 0.17f, 0.34f, Direction.Fall);
                return;
            }

            // Hit stop on both ends of the blow, scaled down at 2x and 4x so it stays 50ms of
            // watching rather than 50ms of game.
            float stop = (crit ? CritHitStopSeconds : HitStopSeconds) / PlaybackSpeed();
            target.HitStop(stop);
            if (source != null && source.Alive) source.HitStop(stop);

            // R12: the strike of a cast holds longer than a basic blow. HitStop keeps the larger
            // request, so this only ever lengthens the freeze above.
            bool skill = Juice.On && _skillSourceId >= 0 && e.SourceId == _skillSourceId;
            // The skill moment lands with the cast's first impact: dim, ring, hit-stop and shake together.
            if (Juice.On && skill && !_skillMomentDone && source != null)
            {
                _skillMomentDone = true;
                SkillMoment(source);
            }
            if (Juice.On)
            {
                float juiceStop = JuiceMath.HitStopSeconds(Juice.Profile, crit, skill) / PlaybackSpeed();
                target.HitStop(juiceStop);
                if (source != null && source.Alive) source.HitStop(juiceStop);
            }

            // Knockback along the attacker-to-target line. A crit pushes harder.
            if (source != null)
                target.SetKnock(target.transform.position - source.transform.position, crit ? 1.5f : 1f);

            // R12: the knock is a fixed number of screen pixels per kind of hit.
            if (Juice.On && _stage.Camera != null)
            {
                float px = JuiceMath.KnockPx(Juice.Profile, crit, skill);
                if (px > 0f) target.SetKnockDistance(JuiceMath.PixelsToWorld(px, _stage.Camera.rect.height, _stage.Camera.orthographicSize));
            }

            // Camera shake on crits only. The host shakes on a crit too; ShakeAtLeast never
            // weakens a shake already running, so the two do not fight.
            // R12: the director owns every shake when the flag is on (the host's two calls step
            // aside): small on a crit, medium on a skill, none on a plain hit.
            if (Juice.On)
            {
                float shakeStrength, shakeSeconds;
                JuiceMath.ShakeFor(Juice.Profile, crit, skill, false, out shakeStrength, out shakeSeconds);
                if (shakeStrength > 0f) _stage.ShakeAtLeast(shakeStrength, shakeSeconds);
            }
            else if (crit) _stage.ShakeAtLeast(0.14f, 0.2f);

            // R15 v2: one look per class replaces everything below (the sheet or shape, the crit ring
            // and streaks): a small puff for a hit, a bigger one for a crit, the burst sheet for a skill.
            if (Feel.V2)
            {
                PlayHitLook(e, source, target, crit, skill);
                return;
            }

            Color tint = Accent(source);
            VfxEntry entry = Resolve(e);
            Vector3 point = FxPoint(target);

            // R12: a soft additive glow behind the unit, in the attacker's accent, under the flipbook.
            if (Juice.On && Juice.Profile.GlowRadius > 0f && _stage.Vfx != null && _stage.Camera != null)
            {
                JuiceProfile jp = Juice.Profile;
                Vector3 behind = target.CenterPosition + _stage.Camera.transform.forward * 0.8f;
                _stage.Vfx.Glow(behind, tint, jp.GlowRadius * (crit ? jp.GlowCritScale : 1f), jp.GlowSeconds, jp.GlowAlpha, crit, false);
            }

            // The record's own aspect factor scales the burst: a weakness reads bigger, a resist
            // smaller. Read as given, no threshold of our own.
            float power = Mathf.Clamp(e.AspectFactor, 0.7f, 1.4f);

            Texture2D sheet = entry != null && entry.HasSheet ? LoadSheet(entry.Sheet) : null;
            if (sheet != null)
            {
                Color sheetTint = entry.TintByAspect ? tint : Color.white;
                StartCoroutine(Flipbook(sheet, entry, point, sheetTint, power * (crit ? 1.25f : 1f)));
            }
            else
            {
                EmitShape(entry, e, source, target, point, tint, power, crit);
            }

            if (crit)
            {
                // The crit read, on top of whichever of the two above played: a wide ring and a
                // spray of fast streaks.
                EmitRing(point, tint, 1.8f, 0.30f);
                EmitStreaks(point, tint, 9, 4.2f, 0.26f, 0.30f, Direction.Radial, null);
            }
        }

        private void PlayShield(BattleEvent e, UnitActor target, bool broke)
        {
            if (target == null || !target.Alive) return;
            Color shield = UITheme.Shield;
            Vector3 point = FxPoint(target);

            if (broke)
            {
                // Shield break: the bigger ring, a scatter of fast chips and streaks, a hard
                // shield-coloured flash and a short freeze on the unit that lost it.
                target.Flash(0.95f, shield);
                target.HitStop(0.07f / PlaybackSpeed());
                EmitRing(point, shield, 2.0f, 0.34f);
                EmitChips(point, shield, 12, 3.4f, 0.27f, 0.46f, Direction.Radial);
                EmitStreaks(point, shield, 6, 3.6f, 0.24f, 0.32f, Direction.Radial, null);
            }
            else
            {
                // Absorb: the shield took the blow. A soft ring and a few slow chips, a faint flash.
                target.Flash(0.45f, shield);
                EmitRing(point, shield, 1.05f, 0.24f);
                EmitChips(point, shield, 3, 1.4f, 0.19f, 0.34f, Direction.Radial);
            }
        }

        private void PlayStatus(BattleEvent e, UnitActor target)
        {
            if (target == null || !target.Alive) return;
            bool harmful = StatusInfo.IsHarmful(e.Status);
            Color tint = harmful ? UITheme.Danger : UITheme.Success;
            Vector3 point = FxPoint(target);

            // A beneficial status plays the buff entry from vfx.json, as a heal plays the heal one.
            if (!harmful)
            {
                PlayBenefit(target, "buff", 0.30f);
                return;
            }

            // A harmful status sinks, a beneficial one rises. Direction is the whole difference
            // between them besides colour, so it reads without the colour.
            target.Flash(0.30f, tint);
            EmitRing(point, tint, harmful ? 0.9f : 1.1f, 0.30f);
            EmitChips(point, tint, 5, 1.2f, 0.19f, 0.5f, harmful ? Direction.Fall : Direction.Rise);
        }

        /// <summary>A heal or a beneficial status: the vfx.json types entry of that name (a flipbook
        /// if its sheet exists, else the procedural numbers), in the success colour, with the chips
        /// rising. Not tinted by the source's aspect: a regen tick has no source, and a heal should
        /// read as a heal whoever cast it.</summary>
        private void PlayBenefit(UnitActor target, string typeKey, float flash)
        {
            if (target == null || !target.Alive) return;
            Color tint = UITheme.Success;
            Vector3 point = FxPoint(target);
            target.Flash(flash, tint);

            VfxEntry entry;
            if (!Config.Types.TryGetValue(typeKey, out entry)) entry = BuiltInEntry;

            Texture2D sheet = entry.HasSheet ? LoadSheet(entry.Sheet) : null;
            if (sheet != null) StartCoroutine(Flipbook(sheet, entry, point, tint, 1f));
            else EmitRing(point, tint, Mathf.Max(1.1f, entry.Ring), 0.30f);

            EmitChips(point, tint, Mathf.Max(1, entry.Chips), entry.Speed * 0.75f, entry.Size, entry.Life * 1.3f, Direction.Rise);
        }

        private void PlayKill(BattleEvent e, UnitActor target)
        {
            if (target == null || _stage == null) return;
            UnitActor killer = e.SourceId >= 0 && e.SourceId != e.TargetId ? _stage.Find(e.SourceId) : null;
            Color tint = Accent(killer != null ? killer : target);   // the killer's accent, else the fallen unit's own
            Vector3 point = FxPoint(target);

            // A kill never thins: ring, a full radial spray and streaks, and the camera moves.
            EmitRing(point, tint, 1.7f, 0.36f);
            EmitChips(point, tint, 12, 3.0f, 0.27f, 0.5f, Direction.Radial);
            EmitStreaks(point, tint, 7, 3.6f, 0.26f, 0.34f, Direction.Radial, null);
            // R12: no shake of its own; the killing blow's crit or skill shake already covers it.
            if (Juice.On)
            {
                float killStrength, killSeconds;
                JuiceMath.ShakeFor(Juice.Profile, false, false, true, out killStrength, out killSeconds);
                if (killStrength > 0f) _stage.ShakeAtLeast(killStrength, killSeconds);
            }
            else _stage.ShakeAtLeast(0.12f, 0.22f);
        }

        // ------------------------------------------------------------------ skill moment

        /// <summary>
        /// R12: the beat a cast lands on. The painted backdrop dims, and a ring of the caster's
        /// accent flashes behind them. Called by PlayDamage on a cast's first impact, the moment the
        /// skill's impact sound plays, so dim, shake, hit-stop and audio arrive together.
        /// </summary>
        public void SkillMoment(UnitActor caster)
        {
            if (!Juice.On) return;
            if (caster == null || _stage == null || _stage.Vfx == null) return;
            JuiceProfile p = Juice.Profile;
            _stage.DimBackdrop(PlaybackSpeed());
            // R14: the music steps back for as long as the backdrop is dimmed. Audio decides how far.
            Game.GameApp app = Game.GameApp.Instance;
            if (app != null && app.Audio != null) app.Audio.Duck("skill", JuiceMath.DimSeconds(p) / PlaybackSpeed());
            Camera cam = _stage.Camera;
            Vector3 behind = caster.CenterPosition + (cam != null ? cam.transform.forward * 0.6f : Vector3.zero);
            Color accent = Accent(caster);
            // R15 v2: the geometric ring gives way to one soft glow in the caster's accent.
            if (Feel.V2)
            {
                FeelProfile fp = Feel.Profile;
                _stage.Vfx.Glow(behind, accent, fp.SkillGlowRadius, p.RingSeconds, fp.SkillGlowAlpha, true, false);
                return;
            }
            _stage.Vfx.Glow(behind, accent, p.RingRadius * 0.75f, p.RingSeconds, 1f, true, true);
            _stage.Vfx.Glow(behind, accent, p.RingRadius * 0.45f, p.RingSeconds, 0.55f, true, false);
        }

        // ------------------------------------------------------------------ R15 v2: one look per class

        // Every method here starts with if (!Feel.V2) return, so with the preset at v1 none of it
        // runs (FeelChecks proves the guard). Each effect is one quad under the caps: the per-unit
        // cap (FeelMath.CapDecision, two by default) and the live cap, on top of the R12 pool cap.

        /// <summary>A body measured against the humanoid the sizes were authored for.</summary>
        private static float BodyUnit(UnitActor actor)
        {
            float h = actor.HeadPosition.y - actor.transform.position.y;
            return Mathf.Max(0.5f, h) / UnitRig.HumanoidTop;
        }

        /// <summary>Where a blow lands: the target centre, a quarter of a body toward the attacker.</summary>
        private Vector3 StrikePoint(UnitActor source, UnitActor target)
        {
            Vector3 p = FxPoint(target);
            if (source == null) return p;
            Vector3 d = source.transform.position - target.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return p;
            return p + d.normalized * (0.3f * BodyUnit(target));
        }

        /// <summary>Starts one effect quad on a unit, or returns null when a cap drops it. A stronger
        /// class retires the oldest weaker effect on a full unit.</summary>
        private FxHandle BeginFx(UnitActor target, string cls, Texture2D texture, Color tint, bool additive)
        {
            if (!Feel.V2 || target == null || target.Snapshot == null || texture == null) return null;
            FeelProfile fp = Feel.Profile;
            if (fp.VfxMaxLive > 0 && _quadLive.Count >= fp.VfxMaxLive) return null;
            int unit = target.Snapshot.Id;
            List<FxHandle> mine;
            if (!_unitFx.TryGetValue(unit, out mine))
            {
                mine = new List<FxHandle>();
                _unitFx[unit] = mine;
            }
            int priority = FeelMath.ClassPriority(cls);
            ScratchPriorities.Clear();
            ScratchBorn.Clear();
            for (int i = 0; i < mine.Count; i++)
            {
                ScratchPriorities.Add(mine[i].Priority);
                ScratchBorn.Add(mine[i].Born);
            }
            int decision = FeelMath.CapDecision(fp.VfxMaxPerUnit, ScratchPriorities, ScratchBorn, priority);
            if (decision == -2) return null;
            if (decision >= 0)
            {
                mine[decision].Retired = true;
                mine.RemoveAt(decision);
            }
            FxHandle h = new FxHandle();
            h.Quad = TakeQuad(texture, tint);
            h.Quad.Material.mainTextureScale = Vector2.one;
            h.Quad.Material.mainTextureOffset = Vector2.zero;
            h.Quad.Material.SetFloat("_DstBlend", additive ? 1f : 10f);
            h.Unit = unit;
            h.Priority = priority;
            h.Born = Time.time;
            mine.Add(h);
            return h;
        }

        private void EndFx(FxHandle h)
        {
            List<FxHandle> mine;
            if (_unitFx.TryGetValue(h.Unit, out mine)) mine.Remove(h);
            _quadLive.Remove(h.Quad);
            h.Quad.Go.SetActive(false);
            _quadPool.Push(h.Quad);
        }

        private void FaceCameraQuad(FlipQuad quad, float roll)
        {
            Camera cam = _stage != null ? _stage.Camera : null;
            if (cam != null) quad.T.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>A soft blob that grows to size and fades: the basic impact puff, and the glows.</summary>
        private IEnumerator PuffRoutine(FxHandle h, Vector3 point, Color tint, float size, float seconds, float startFrac, float alpha)
        {
            if (h == null) yield break;
            FlipQuad q = h.Quad;
            q.T.position = point;
            FaceCameraQuad(q, 0f);
            float duration = Mathf.Max(0.02f, seconds) / PlaybackSpeed();
            float t = 0f;
            while (t < duration && !h.Retired)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = size * Mathf.Lerp(startFrac, 1f, 1f - (1f - k) * (1f - k));
                q.T.localScale = new Vector3(s, s, s);
                q.Material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, alpha * (1f - k)));
                yield return null;
            }
            EndFx(h);
        }

        /// <summary>The skill burst: a sheet played once at a point, larger and slower than a hit.</summary>
        private IEnumerator SheetRoutine(FxHandle h, int columns, int rows, int frames, float fps, Vector3 point, float size, Color tint)
        {
            if (h == null) yield break;
            FlipQuad q = h.Quad;
            q.T.position = point;
            FaceCameraQuad(q, 0f);
            q.T.localScale = new Vector3(size, size, size);
            Vector2 cell = new Vector2(1f / columns, 1f / rows);
            q.Material.mainTextureScale = cell;
            q.Material.SetColor("_Color", tint);
            float duration = Mathf.Max(1, frames) / Mathf.Max(1f, fps) / PlaybackSpeed();
            float t = 0f;
            while (t < duration && !h.Retired)
            {
                t += Time.deltaTime;
                int frame = Mathf.Min(frames - 1, (int)(t / duration * frames));
                int col = frame % columns;
                int row = frame / columns;
                q.Material.mainTextureOffset = new Vector2(col * cell.x, 1f - (row + 1) * cell.y);
                yield return null;
            }
            EndFx(h);
        }

        /// <summary>A band that travels up or down the body, bell shaped in alpha.</summary>
        private IEnumerator PulseRoutine(FxHandle h, UnitActor target, Color tint, float size, float seconds, float travel, bool up, float alpha)
        {
            if (h == null) yield break;
            FlipQuad q = h.Quad;
            FaceCameraQuad(q, 0f);
            float body = target.HeadPosition.y - target.transform.position.y;
            Vector3 basePoint = FxPoint(target);
            float lo = target.transform.position.y + body * 0.30f - basePoint.y;
            float hi = lo + body * travel;
            float duration = Mathf.Max(0.02f, seconds) / PlaybackSpeed();
            float t = 0f;
            while (t < duration && !h.Retired)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float along = up ? k : 1f - k;
                q.T.position = basePoint + Vector3.up * Mathf.Lerp(lo, hi, 1f - (1f - along) * (1f - along));
                q.T.localScale = new Vector3(size * 0.9f, size * 1.25f, 1f);
                q.Material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, alpha * Mathf.Sin(k * Mathf.PI)));
                yield return null;
            }
            EndFx(h);
        }

        /// <summary>The arrow chip: a badge with an arrow that drifts up (buff) or down (debuff) off the head.</summary>
        private IEnumerator ArrowRoutine(FxHandle h, UnitActor target, Color tint, float size, float seconds, float travel, bool up)
        {
            if (h == null) yield break;
            FlipQuad q = h.Quad;
            FaceCameraQuad(q, up ? 0f : 180f);
            q.T.localScale = new Vector3(size, size, size);
            float body = target.HeadPosition.y - target.transform.position.y;
            Vector3 start = FxPoint(target) + Vector3.up * (body * 0.62f);
            float drift = body * travel * 0.35f * (up ? 1f : -1f);
            float duration = Mathf.Max(0.02f, seconds) / PlaybackSpeed();
            float t = 0f;
            while (t < duration && !h.Retired)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                q.T.position = start + Vector3.up * (drift * (1f - (1f - k) * (1f - k)));
                float a = k < 0.15f ? k / 0.15f : 1f - Mathf.Clamp01((k - 0.55f) / 0.45f);
                q.Material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, a));
                yield return null;
            }
            EndFx(h);
        }

        /// <summary>A shell around the body: an outline and a faint fill that swell a little and fade.</summary>
        private IEnumerator ShellRoutine(FxHandle h, UnitActor target, Color tint, float seconds, float alpha)
        {
            if (h == null) yield break;
            FlipQuad q = h.Quad;
            FaceCameraQuad(q, 0f);
            float body = target.HeadPosition.y - target.transform.position.y;
            q.T.position = FxPoint(target);
            float duration = Mathf.Max(0.02f, seconds) / PlaybackSpeed();
            float t = 0f;
            while (t < duration && !h.Retired)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float swell = Mathf.Lerp(0.86f, 1.04f, 1f - (1f - k) * (1f - k));
                q.T.localScale = new Vector3(body * 0.85f * swell, body * 1.25f * swell, 1f);
                float a = k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f;
                q.Material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, alpha * a));
                yield return null;
            }
            EndFx(h);
        }

        // ---- the looks, one per class

        private void PlayHitLook(BattleEvent e, UnitActor source, UnitActor target, bool crit, bool skill)
        {
            if (!Feel.V2) return;
            FeelProfile fp = Feel.Profile;
            Color accent = Accent(source);
            float u = BodyUnit(target);
            float power = Mathf.Clamp(e.AspectFactor, 0.8f, 1.3f);
            Texture2D dot = BattleAssets.SoftDot;
            Vector3 centre = FxPoint(target);
            Vector3 behind = _stage != null && _stage.Camera != null ? _stage.Camera.transform.forward * 0.4f : Vector3.zero;

            if (skill)
            {
                // The aspect-tinted burst, larger and slower, over a soft glow. The dim and the
                // caster's glow come from SkillMoment.
                Texture2D sheet = fp.SkillSheet.Length > 0 ? LoadSheet(fp.SkillSheet) : null;
                if (sheet != null)
                {
                    FxHandle burst = BeginFx(target, "skill", sheet, accent, true);
                    StartCoroutine(SheetRoutine(burst, fp.SkillColumns, fp.SkillRows, fp.SkillFrames, fp.SkillFps, centre, fp.SkillScale * u * power, accent));
                }
                FxHandle glow = BeginFx(target, "skill", dot, accent, true);
                StartCoroutine(PuffRoutine(glow, centre + behind, accent, fp.CritGlowRadius * 2f * u * power, 0.30f, 0.8f, 0.7f));
                return;
            }

            Color puff = new Color(FeelMath.TowardWhite(accent.r, fp.PuffWhite), FeelMath.TowardWhite(accent.g, fp.PuffWhite), FeelMath.TowardWhite(accent.b, fp.PuffWhite), 1f);
            Vector3 at = StrikePoint(source, target);
            if (crit)
            {
                // The basic impact, bigger, with a soft glow under it. The crit number is the other half.
                FxHandle glow = BeginFx(target, "crit", dot, accent, true);
                StartCoroutine(PuffRoutine(glow, centre + behind, accent, fp.CritGlowRadius * 2f * u * power, fp.CritPuffSeconds * 1.2f, 0.7f, 0.55f));
                FxHandle big = BeginFx(target, "crit", dot, puff, true);
                StartCoroutine(PuffRoutine(big, at, puff, fp.CritPuffSize * u * power, fp.CritPuffSeconds, fp.CritPuffStart, 1f));
                return;
            }
            FxHandle small = BeginFx(target, "hit", dot, puff, true);
            StartCoroutine(PuffRoutine(small, at, puff, fp.HitPuffSize * u * power, fp.HitPuffSeconds, 0.45f, 0.95f));
        }

        /// <summary>The cast itself: a soft glow on the caster in their accent, in place of the ring that swept up the body.</summary>
        private void PlayCastGlow(UnitActor caster)
        {
            if (!Feel.V2 || caster == null || !caster.Alive) return;
            FeelProfile fp = Feel.Profile;
            Color accent = Accent(caster);
            Vector3 behind = _stage != null && _stage.Camera != null ? _stage.Camera.transform.forward * 0.4f : Vector3.zero;
            FxHandle glow = BeginFx(caster, "buff", BattleAssets.SoftDot, accent, true);
            StartCoroutine(PuffRoutine(glow, FxPoint(caster) + behind, accent, fp.CastGlowRadius * 2f * BodyUnit(caster), fp.CastGlowSeconds, 0.6f, 0.6f));
        }

        /// <summary>Heal: a soft green glow and a few chips rising off the target. No ring, no sheet.</summary>
        private void PlayHealLook(UnitActor target)
        {
            if (!Feel.V2 || target == null || !target.Alive) return;
            FeelProfile fp = Feel.Profile;
            Color green = UITheme.Success;
            float u = BodyUnit(target);
            Vector3 centre = FxPoint(target);
            FxHandle glow = BeginFx(target, "heal", BattleAssets.SoftDot, green, true);
            StartCoroutine(PuffRoutine(glow, centre, green, fp.HealGlowRadius * 2f * u, fp.HealGlowSeconds, 0.6f, 0.55f));
            Vector3 belly = centre - Vector3.up * (0.55f * u);
            EmitChips(belly, new Color(0.78f, 1f, 0.84f, 1f), fp.HealSparks, fp.HealSparkRise * u, fp.HealSparkSize * u, fp.HealSparkSeconds, Direction.Rise);
        }

        /// <summary>Buff: a gold band rising through the body and an up arrow chip. Debuff: a dark band
        /// sinking and a down arrow chip. Neither plays the heal sparkle.</summary>
        private void PlayStatusLook(BattleEvent e, UnitActor target)
        {
            if (!Feel.V2 || target == null || !target.Alive) return;
            if (e.Status == StatusKind.None || e.Status == StatusKind.Shield) return;
            FeelProfile fp = Feel.Profile;
            bool harmful = StatusInfo.IsHarmful(e.Status);
            string cls = harmful ? "debuff" : "buff";
            Color tint = harmful ? new Color(0.50f, 0.15f, 0.34f, 1f) : UITheme.Gold;
            bool additive = !harmful;     // a darker tint only shows when drawn over the scene, not added to it
            float u = BodyUnit(target);
            if (_arrowTexture == null) _arrowTexture = MakeArrowChip();
            FxHandle band = BeginFx(target, cls, BattleAssets.SoftDot, tint, additive);
            StartCoroutine(PulseRoutine(band, target, tint, fp.PulseSize * u, fp.PulseSeconds, fp.PulseTravel, !harmful, harmful ? 0.7f : 0.8f));
            FxHandle chip = BeginFx(target, cls, _arrowTexture, tint, additive);
            StartCoroutine(ArrowRoutine(chip, target, tint, fp.ArrowSize * u, fp.ArrowSeconds, fp.PulseTravel, !harmful));
            // The darker tint of a debuff: the body darkens for a moment, the counterpart of a hit flash.
            if (harmful) target.TintFor(fp.DebuffTint, tint * 0.35f, fp.DebuffTintSeconds);
        }

        /// <summary>A passive firing: a soft gold glow on the unit, in place of the ring that swept up the body.</summary>
        private void PlayPassiveGlow(UnitActor unit)
        {
            if (!Feel.V2 || unit == null || !unit.Alive) return;
            Vector3 behind = _stage != null && _stage.Camera != null ? _stage.Camera.transform.forward * 0.4f : Vector3.zero;
            FxHandle glow = BeginFx(unit, "buff", BattleAssets.SoftDot, UITheme.Gold, true);
            StartCoroutine(PuffRoutine(glow, FxPoint(unit) + behind, UITheme.Gold, 0.9f * 2f * BodyUnit(unit), 0.35f, 0.6f, 0.6f));
        }

        /// <summary>Shield: a short shell flash around the body. An absorb is the lighter flash, a
        /// break the full one with a short freeze.</summary>
        private void PlayShell(UnitActor target, bool broke)
        {
            if (!Feel.V2 || target == null || !target.Alive) return;
            FeelProfile fp = Feel.Profile;
            if (broke)
            {
                target.Flash(0.5f, UITheme.Shield);
                target.HitStop(0.07f / PlaybackSpeed());
            }
            SpawnShell(target, broke ? fp.ShellAlpha : fp.ShellAbsorbAlpha);
        }

        private void PlayShellGain(UnitActor target)
        {
            if (!Feel.V2 || target == null || !target.Alive) return;
            SpawnShell(target, Feel.Profile.ShellAlpha);
        }

        private void SpawnShell(UnitActor target, float alpha)
        {
            if (!Feel.V2) return;
            if (_shellTexture == null) _shellTexture = MakeShell();
            FxHandle shell = BeginFx(target, "shield", _shellTexture, UITheme.Shield, true);
            StartCoroutine(ShellRoutine(shell, target, UITheme.Shield, Feel.Profile.ShellSeconds, alpha));
        }

        // ------------------------------------------------------------------ resolution

        /// <summary>abilities[abilityId], else types[the ability's vfx tag], else types.impact.</summary>
        private VfxEntry Resolve(BattleEvent e)
        {
            VfxConfig config = Config;
            VfxEntry entry = config.ForAbility(e.AbilityId);
            if (entry != null) return entry;
            entry = config.ForType(e.Vfx);
            return entry != null ? entry : BuiltInEntry;
        }

        private static readonly VfxEntry BuiltInEntry = new VfxEntry();

        private static Texture2D LoadSheet(string path)
        {
            Texture2D tex;
            if (SheetCache.TryGetValue(path, out tex)) return tex;
            // Resources.Load answers null for a file that is not there and logs nothing. A sheet
            // that has not been drawn yet is the expected state, so it is cached as a miss too.
            tex = Resources.Load<Texture2D>(path);
            SheetCache[path] = tex;
            return tex;
        }

        // ------------------------------------------------------------------ procedural shapes

        private enum Direction { Radial, Rise, Fall }

        private void EmitShape(VfxEntry entry, BattleEvent e, UnitActor source, UnitActor target,
                               Vector3 point, Color tint, float power, bool crit)
        {
            if (entry == null) entry = BuiltInEntry;
            float scale = crit ? 1.5f : 1f;
            int chips = Mathf.Max(1, Mathf.RoundToInt(entry.Chips * power * scale));
            int streaks = Mathf.RoundToInt(entry.Streaks * power * scale);
            float speed = entry.Speed * (crit ? 1.25f : 1f);

            // Thin an ordinary hit when the pool is already busy. Crits are never thinned.
            if (!crit && LiveParticles() > ThinAbove)
            {
                chips = Mathf.Max(1, chips / 2);
                streaks = streaks / 2;
            }

            // The attacker-to-target line, flattened into the camera's plane, for the directed
            // shapes. Falls back to horizontal when the source is unknown.
            Vector3 axis = PlaneAxis(source, target);

            string shape = entry.Shape ?? "shards";
            if (shape == "slash")
            {
                EmitStreaks(point, tint, streaks, speed, entry.Size, entry.Life, Direction.Radial, axis);
                EmitChips(point, tint, chips, speed * 0.7f, entry.Size * 0.8f, entry.Life, Direction.Radial);
            }
            else if (shape == "bolt")
            {
                EmitStreaks(point, tint, streaks, speed, entry.Size, entry.Life, Direction.Radial, axis);
                EmitChips(point, tint, chips, speed, entry.Size, entry.Life, Direction.Radial);
            }
            else if (shape == "implode")
            {
                EmitChips(point, tint, chips, -speed, entry.Size, entry.Life, Direction.Radial);
            }
            else
            {
                EmitChips(point, tint, chips, speed, entry.Size, entry.Life, Direction.Radial);
                if (streaks > 0) EmitStreaks(point, tint, streaks, speed, entry.Size, entry.Life, Direction.Radial, null);
            }

            if (entry.Ring > 0f && !crit) EmitRing(point, tint, entry.Ring, 0.26f);
        }

        private Vector3 PlaneAxis(UnitActor source, UnitActor target)
        {
            Camera cam = _stage.Camera;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            if (source == null || target == null) return right;
            Vector3 d = target.transform.position - source.transform.position;
            Vector3 a = right * Vector3.Dot(d, right) + up * Vector3.Dot(d, up);
            if (a.sqrMagnitude < 0.0001f)
            {
                // Same screen row: the line runs along the ground, so its screen-plane part is
                // the horizontal component.
                a = right * Vector3.Dot(d, right);
            }
            return a.sqrMagnitude < 0.0001f ? right : a.normalized;
        }

        /// <summary>Where an effect is drawn: the unit's centre, pulled toward the camera so the
        /// billboards and procedural bodies, which write depth, never hide it. The camera is
        /// orthographic, so moving along its view axis changes nothing on screen.</summary>
        private Vector3 FxPoint(UnitActor target)
        {
            Camera cam = _stage != null ? _stage.Camera : null;
            Vector3 toward = cam != null ? cam.transform.forward * -1.2f : Vector3.zero;
            return target.CenterPosition + toward;
        }

        private float PlaybackSpeed()
        {
            return _stage != null && _stage.Vfx != null ? Mathf.Max(1f, _stage.Vfx.SpeedScale) : 1f;
        }

        private static Color Accent(UnitActor actor)
        {
            if (actor == null || actor.Snapshot == null) return new Color(0.92f, 0.92f, 0.92f, 1f);
            return UITheme.AspectAccent(actor.Snapshot.Aspect);
        }

        private int LiveParticles()
        {
            return (_chips != null ? _chips.particleCount : 0)
                   + (_streaks != null ? _streaks.particleCount : 0)
                   + (_rings != null ? _rings.particleCount : 0);
        }

        // ------------------------------------------------------------------ particle emission

        private void EmitChips(Vector3 point, Color tint, int count, float speed, float size, float life, Direction dir)
        {
            EnsureBuilt();
            if (_chips == null || count <= 0) return;
            Vector3 right, up;
            CameraPlane(out right, out up);
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * 6.2832f + Random.Range(-0.35f, 0.35f);
                Vector3 radial = right * Mathf.Cos(a) + up * (Mathf.Sin(a) * 0.85f);
                Vector3 v;
                if (dir == Direction.Rise) v = up * Random.Range(0.8f, 1.4f) + right * Mathf.Cos(a) * 0.35f;
                else if (dir == Direction.Fall) v = up * -Random.Range(0.8f, 1.4f) + right * Mathf.Cos(a) * 0.35f;
                else v = radial;

                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
                bool inward = speed < 0f;
                float s = Mathf.Abs(speed) * Random.Range(0.6f, 1.2f);
                p.position = inward ? point + radial * 0.7f : point + radial * 0.08f;
                p.velocity = inward ? radial * -s * 1.3f : v * s;
                p.startLifetime = life * Random.Range(0.8f, 1.2f);
                p.startSize = size * Random.Range(0.7f, 1.3f);
                p.startColor = tint;
                p.rotation = Random.Range(0f, 360f);
                _chips.Emit(p, 1);
            }
        }

        /// <summary>Elongated shards along their own velocity. With an axis they fan about it
        /// (a slash or a bolt); without one they leave radially.</summary>
        private void EmitStreaks(Vector3 point, Color tint, int count, float speed, float size, float life,
                                 Direction dir, Vector3? axis)
        {
            EnsureBuilt();
            if (_streaks == null || count <= 0) return;
            Vector3 right, up;
            CameraPlane(out right, out up);
            // A random turn per burst, so four streaks are never a plus sign and a kill never
            // reads as a targeting reticle.
            float phase = Random.Range(0f, 6.2832f);
            for (int i = 0; i < count; i++)
            {
                Vector3 v;
                if (axis.HasValue)
                {
                    // A fan of about 40 degrees centred on the axis.
                    float spread = (count > 1 ? (i / (float)(count - 1)) - 0.5f : 0f) * 0.7f;
                    Vector3 a = axis.Value;
                    Vector3 perp = right * (-Vector3.Dot(a, up)) + up * Vector3.Dot(a, right);
                    v = (a + perp * spread).normalized;
                }
                else
                {
                    float ang = phase + (i / (float)count) * 6.2832f + Random.Range(-0.3f, 0.3f);
                    v = right * Mathf.Cos(ang) + up * (Mathf.Sin(ang) * 0.85f);
                }

                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
                float s = speed * Random.Range(0.75f, 1.15f);
                p.position = point + v * 0.1f;
                p.velocity = v * s;
                p.startLifetime = life * Random.Range(0.85f, 1.15f);
                p.startSize = size * Random.Range(0.8f, 1.2f);
                p.startColor = tint;
                _streaks.Emit(p, 1);
            }
        }

        private void EmitRing(Vector3 point, Color tint, float size, float life)
        {
            EnsureBuilt();
            if (_rings == null) return;
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = point;
            p.velocity = Vector3.zero;
            p.startLifetime = life;
            p.startSize = size * 0.9f;
            p.startColor = tint;
            _rings.Emit(p, 1);
        }

        private void CameraPlane(out Vector3 right, out Vector3 up)
        {
            Camera cam = _stage != null ? _stage.Camera : null;
            right = cam != null ? cam.transform.right : Vector3.right;
            up = cam != null ? cam.transform.up : Vector3.up;
        }

        // ------------------------------------------------------------------ flipbook

        /// <summary>
        /// Plays a sheet on a camera-facing quad: the same billboard quad and UV-rect approach
        /// SpriteAnimator uses (ApplyFrame), reading the grid from vfx.json instead of a sidecar.
        /// A real sheet dropping in later is a file change, nothing else.
        /// </summary>
        private IEnumerator Flipbook(Texture2D sheet, VfxEntry entry, Vector3 point, Color tint, float power)
        {
            if (_quadLive.Count >= MaxFlipbooks) yield break;

            FlipQuad quad = TakeQuad(sheet, tint);
            Camera cam = _stage != null ? _stage.Camera : null;
            quad.T.position = point;
            if (cam != null) quad.T.rotation = cam.transform.rotation;
            float size = entry.Scale * power;
            quad.T.localScale = new Vector3(size, size, size);

            int first = Mathf.Clamp(entry.Start, 0, entry.FrameCount - 1);
            int frames = entry.FrameCount - first;
            Vector2 cell = new Vector2(1f / entry.Columns, 1f / entry.Rows);
            quad.Material.mainTextureScale = cell;

            float duration = frames / entry.Fps / PlaybackSpeed();
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                int frame = first + Mathf.Min(frames - 1, (int)(t / duration * frames));
                int col = frame % entry.Columns;
                int row = frame / entry.Columns;
                quad.Material.mainTextureOffset = new Vector2(col * cell.x, 1f - (row + 1) * cell.y);
                yield return null;
            }

            _quadLive.Remove(quad);
            quad.Go.SetActive(false);
            _quadPool.Push(quad);
        }

        private FlipQuad TakeQuad(Texture2D sheet, Color tint)
        {
            FlipQuad quad;
            if (_quadPool.Count > 0)
            {
                quad = _quadPool.Pop();
                quad.Go.SetActive(true);
            }
            else
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Flipbook";
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                go.transform.SetParent(transform, false);

                quad = new FlipQuad();
                quad.Go = go;
                quad.T = go.transform;
                // Alpha-blended, not additive: a drawn sheet keeps its own darks. The blend values
                // are NewFx's (SrcAlpha, OneMinusSrcAlpha = 10; see the note in BattleVfx.Take).
                quad.Material = BattleAssets.NewFx(sheet, tint, false);
                _materials.Add(quad.Material);
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                renderer.material = quad.Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = 100;
            }
            quad.Material.SetTexture("_MainTex", sheet);
            quad.Material.SetColor("_Color", tint);
            // R12: sheets draw additively (One, as NewFx's additive flag sets it), so a spike or a
            // puff glows over the scene. A quad that was additive goes back to OneMinusSrcAlpha
            // (10) when the flag is off again, so a pooled quad never carries the old blend.
            if (Juice.On && Juice.Profile.Additive) { quad.Material.SetFloat("_DstBlend", 1f); quad.Additive = true; }
            else if (quad.Additive) { quad.Material.SetFloat("_DstBlend", 10f); quad.Additive = false; }
            _quadLive.Add(quad);
            return quad;
        }

        // ------------------------------------------------------------------ building the systems

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_chipTexture == null) _chipTexture = MakeChip();
            if (_streakTexture == null) _streakTexture = MakeStreak();
            if (_ringTexture == null) _ringTexture = BattleAssets.RingTexture;

            _chips = BuildSystem("Chips", _chipTexture, MaxChips, 0.35f, false,
                                 AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
            _streaks = BuildSystem("Streaks", _streakTexture, MaxStreaks, 0f, true,
                                   AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            _rings = BuildSystem("Rings", _ringTexture, MaxRings, 0f, false,
                                 AnimationCurve.Linear(0f, 0.3f, 1f, 1.5f));
        }

        private ParticleSystem BuildSystem(string name, Texture2D texture, int max, float gravity, bool stretch,
                                           AnimationCurve size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (ps == null || renderer == null) return null;

            // Everything is emitted by hand with EmitParams, so every module that would emit on
            // its own is off. The system loops forever so it stays alive to simulate what is put
            // into it.
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startLifetime = 0.4f;
            main.startSize = 0.2f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            // Fade out over the second half of life, shrink (or, for rings, grow) with age.
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, size);

            // Sprites/Default is in every build and answers to vertex colour, which is how each
            // particle carries its own tint. Drawn after everything else in the transparent queue.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = BattleAssets.FxShader;
            Material material = new Material(shader);
            material.mainTexture = texture;
            material.renderQueue = 3100;
            _materials.Add(material);

            renderer.material = material;
            renderer.sortingOrder = 100;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3.2f;
                renderer.velocityScale = 0.06f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ generated textures

        /// <summary>A soft-edged diamond: the neutral chip.</summary>
        private static Texture2D MakeChip()
        {
            const int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Abs(u) + Mathf.Abs(v);                  // 0 centre, 1 at the diamond's edge
                    float a = Mathf.Clamp01((1f - d) * 5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        /// <summary>R15: a badge with an arrow pointing up (the debuff turns it over). The disc is
        /// half strength and the arrow full, so the arrow reads on its own plate.</summary>
        private static Texture2D MakeArrowChip()
        {
            const int n = 64;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float arrow = 0f;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 3f) / n * 2f - 1f;
                            float v = (y + (sy + 0.5f) / 3f) / n * 2f - 1f;
                            bool head = v >= 0.0f && v <= 0.62f && Mathf.Abs(u) <= (0.62f - v) * 0.86f;
                            bool shaft = Mathf.Abs(u) <= 0.15f && v >= -0.5f && v <= 0.08f;
                            if (head || shaft) arrow += 1f / 9f;
                        }
                    }
                    float ux = (x + 0.5f) / n * 2f - 1f;
                    float vy = (y + 0.5f) / n * 2f - 1f;
                    float disc = Mathf.Clamp01((0.94f - Mathf.Sqrt(ux * ux + vy * vy)) * 12f) * 0.5f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(disc, arrow) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        /// <summary>R15: the shield shell, a bright rim and a faint fill. Stretched to the body it
        /// reads as an outline flash around the unit.</summary>
        private static Texture2D MakeShell()
        {
            const int n = 128;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float rim = Mathf.Exp(-Mathf.Pow((d - 0.9f) / 0.06f, 2f));
                    float fill = Mathf.Clamp01((0.92f - d) * 8f) * 0.16f * (0.6f + 0.4f * Mathf.Clamp01(v * 0.5f + 0.5f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Mathf.Max(rim, fill)) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        /// <summary>A tapered capsule along u: the neutral streak, stretched along its velocity.</summary>
        private static Texture2D MakeStreak()
        {
            const int w = 64;
            const int h = 16;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;                               // 0 tail, 1 head
                    float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);          // 0 on the axis
                    float width = Mathf.Lerp(0.05f, 0.26f, u);               // a thin line, a little heavier at the head
                    float a = Mathf.Clamp01((width - v) * 4f) * Mathf.Clamp01(u * 6f) * Mathf.Clamp01((1f - u) * 12f + 0.4f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }
    }
}
