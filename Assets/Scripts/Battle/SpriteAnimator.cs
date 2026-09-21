using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.Battle
{
    /// <summary>One parsed sprite-sheet clip: a texture, its frame grid, and the range/fps to play.</summary>
    public sealed class SpriteAnimClip
    {
        public Texture2D Texture;
        public int FrameWidth;
        public int FrameHeight;
        public int Columns = 1;
        public int Rows = 1;
        public int StartFrame;
        public int EndFrame;
        public float Fps = 30f;

        /// <summary>
        /// Fraction of this clip's own duration, from its start, where the strike actually
        /// connects - an authored animation-timing value, not gameplay data, which is why it
        /// lives on the clip sidecar rather than in heroes.json. There is no impact-frame data in
        /// the export (a single flat clip, no sections), so this is a tunable approximation
        /// rather than a derived value; 0.5 (roughly windup, then reach) is the default for a clip
        /// that doesn't set it. Only meaningful for an attack-shaped clip (basic, skill) - idle,
        /// hit_reaction and death never read it.
        /// </summary>
        public float ImpactFraction = 0.5f;

        /// <summary>
        /// THE GAME'S CLIP PACE, in one number.
        ///
        /// Every clip plays at its sheet's own fps times this. It is a PRESENTATION decision, not
        /// animation data: the exports are all 30 fps and say nothing about speed, so whatever the
        /// game plays them at is a choice the game makes. Having one place to make it means the
        /// whole roster can be quickened or slowed by editing this line, instead of by rewriting a
        /// number into two hundred sidecars.
        ///
        /// 1 means "play the sheet at the rate it was exported at", which is what 25 of the 31
        /// animated heroes always did. The other six were wired by a later importer that stamped
        /// 1.30 into each of their 36 clips, so those heroes ran 30% fast against the rest of the
        /// roster for as long as they have existed - Kael's basic came out at 1.85s where Aeron's
        /// identical 72-frame clip took 2.40s. Those stamps are gone and the six now read off this.
        /// </summary>
        public const float DefaultPlaybackSpeed = 1f;

        /// <summary>
        /// This clip's own multiplier on effective fps. Defaults to DefaultPlaybackSpeed and is
        /// overridden only by a sidecar that names `playbackSpeed` explicitly - so a single clip
        /// that genuinely needs its own pace can still have one, and saying nothing means "the
        /// game's pace", not "1".
        ///
        /// Applied as a straight multiplier on effective fps (see SpriteAnimator.Update), so
        /// ClipDuration already reports the SCALED duration - everything that reads ClipDuration
        /// (ImpactFraction sync, the death hold, the pending-tail return) sees the adjusted number
        /// automatically rather than needing its own copy of this value.
        /// </summary>
        public float PlaybackSpeed = DefaultPlaybackSpeed;

        /// <summary>
        /// Fraction of the frame height that is empty below the character's feet. -1 means
        /// "not authored" and leaves the quad wherever BuildSprite put it; any value >= 0
        /// repositions the quad so that margin sits underground. Derived per-clip by the same
        /// alpha scan that produced the hero-level SpriteFootFraction, and written into each
        /// clip's sidecar JSON so a hit-reaction frame that shifts the character vertically
        /// keeps the feet at ground level rather than inheriting idle's margin.
        /// </summary>
        public float FootFraction = -1f;

        public int FrameCount { get { return EndFrame - StartFrame + 1; } }
    }

    /// <summary>
    /// Plays one unit's sprite-sheet clips on the quad UnitRig.BuildSprite already built, by
    /// writing the active frame's cell into the quad's own material via mainTextureOffset /
    /// mainTextureScale - EmberfallFx already samples _MainTex through TRANSFORM_TEX, so no
    /// shader change is needed to turn a static billboard into a flipbook.
    ///
    /// Scoped to whichever hero actually ships clip data (Kael today): TryCreate probes for an
    /// "idle" clip only, through BattleAssets.UnitAnimClip, and returns null for every other unit.
    /// That null is the single point where "this hero has no animation set" folds back to
    /// UnitRig's existing static-image path with zero behaviour change - nothing else in the rig
    /// or in UnitActor needs to know which of the two a given hero is.
    ///
    /// Looping is a property of WHICH call the caller made, not of what a clip's own JSON
    /// authors: PlayLoop always repeats, PlayOnce always plays exactly once, and its holdOnFinish
    /// flag decides whether it then freezes on the final frame (death) or falls back to idle
    /// (basic attack, skill cast, hit reaction). This is deliberately not read from each clip's
    /// own "loop" field - Kael's own export authors loop:true on every clip including death,
    /// correct for a preview tool cycling one clip in isolation, wrong for a battle unit playing
    /// a death once. The caller names what kind of moment this is; the JSON only supplies frames.
    /// </summary>
    public sealed class SpriteAnimator
    {
        private const string IdleClip = "idle";

        private readonly string _spriteId;
        private readonly Dictionary<string, SpriteAnimClip> _clips = new Dictionary<string, SpriteAnimClip>();
        private Material _material;
        private Transform _quad;
        private float _canvasHeight;

        private SpriteAnimClip _current;
        private float _frameTime;
        private bool _looping;
        private bool _returnToIdleOnFinish;

        public Texture2D Texture { get { return _current != null ? _current.Texture : null; } }
        public int FrameWidth { get { return _current != null ? _current.FrameWidth : 0; } }
        public int FrameHeight { get { return _current != null ? _current.FrameHeight : 0; } }

        /// <summary>
        /// Nominal (1x battle speed) seconds a single play-through of this clip takes at its own
        /// authored PlaybackSpeed, or 0 if this unit never shipped it. This is the SCALED duration,
        /// not the raw frameCount/fps - every caller (BattleScreen's ImpactFraction sync, the
        /// pending-tail return, UnitActor's death hold) must see the sped-up number or their timing
        /// drifts out of sync with what's actually on screen the moment a clip's PlaybackSpeed
        /// stops being 1. This is the one place that division happens, so nothing downstream needs
        /// its own copy of PlaybackSpeed to stay correct.
        /// </summary>
        public float ClipDuration(string clipId)
        {
            SpriteAnimClip clip = GetOrLoad(clipId);
            if (clip == null) return 0f;
            float playbackSpeed = clip.PlaybackSpeed > 0f ? clip.PlaybackSpeed : 1f;
            return clip.FrameCount / clip.Fps / playbackSpeed;
        }

        /// <summary>
        /// Where the strike connects, as a fraction of ClipDuration(clipId) from its start. See
        /// SpriteAnimClip.ImpactFraction for why this is authored per-clip rather than derived.
        /// Meaningless (and unread) for a clip with no attack shape - callers only ever ask this
        /// for "basic"/"skill".
        /// </summary>
        public float ImpactFraction(string clipId)
        {
            SpriteAnimClip clip = GetOrLoad(clipId);
            return clip != null ? clip.ImpactFraction : 0.5f;
        }

        public static SpriteAnimator TryCreate(string spriteId)
        {
            SpriteAnimClip idle = BattleAssets.UnitAnimClip(spriteId, IdleClip);
            if (idle == null) return null;

            SpriteAnimator animator = new SpriteAnimator(spriteId);
            animator._clips[IdleClip] = idle;
            animator.PlayLoop(IdleClip);
            return animator;
        }

        private SpriteAnimator(string spriteId) { _spriteId = spriteId; }

        /// <summary>Called once by UnitRig.BuildSprite after it creates the quad and its material.
        /// quad and canvasHeight allow per-clip foot repositioning — see ApplyFootPosition.</summary>
        public void Bind(Material material, Transform quad, float canvasHeight)
        {
            _material = material;
            _quad = quad;
            _canvasHeight = canvasHeight;
            ApplyFrame(0);
            ApplyFootPosition();
        }

        private SpriteAnimClip GetOrLoad(string clipId)
        {
            SpriteAnimClip clip;
            if (_clips.TryGetValue(clipId, out clip)) return clip;
            // Cache a miss too (null), so a hero that ships fewer than five clips isn't re-probed
            // through Resources.Load on every single trigger of the one it never authored.
            clip = BattleAssets.UnitAnimClip(_spriteId, clipId);
            _clips[clipId] = clip;
            return clip;
        }

        public void PlayLoop(string clipId)
        {
            SpriteAnimClip clip = GetOrLoad(clipId);
            if (clip == null) return;
            _current = clip;
            _frameTime = 0f;
            _looping = true;
            _returnToIdleOnFinish = false;
            ApplyFrame(0);
            ApplyFootPosition();
        }

        /// <summary>
        /// holdOnFinish true freezes on the clip's last frame forever (death - there is no idle
        /// to return to). False falls back to idle the moment the clip completes (basic attack,
        /// skill cast, hit reaction - all momentary overlays on top of the idle loop). A clip this
        /// unit never shipped (GetOrLoad returns null) is a silent no-op: whatever is already
        /// playing keeps playing, which for every trigger here is idle.
        /// </summary>
        public void PlayOnce(string clipId, bool holdOnFinish)
        {
            SpriteAnimClip clip = GetOrLoad(clipId);
            if (clip == null)
            {
                PlayLoop(IdleClip);
                return;
            }
            _current = clip;
            _frameTime = 0f;
            _looping = false;
            _returnToIdleOnFinish = !holdOnFinish;
            ApplyFrame(0);
            ApplyFootPosition();
        }

        public void Update(float deltaTime)
        {
            if (_current == null || _material == null) return;

            int frameCount = Math.Max(1, _current.FrameCount);
            // PlaybackSpeed multiplies effective fps directly, so the frames actually advance
            // faster - the ONLY thing that makes ClipDuration's scaled-down number true on screen
            // rather than just true on paper.
            float playbackSpeed = _current.PlaybackSpeed > 0f ? _current.PlaybackSpeed : 1f;
            _frameTime += deltaTime * _current.Fps * playbackSpeed;
            int local = (int)_frameTime;
            bool finished = false;

            if (_looping)
            {
                local %= frameCount;
            }
            else if (local >= frameCount - 1)
            {
                local = frameCount - 1;
                finished = true;
            }

            ApplyFrame(local);

            if (finished && _returnToIdleOnFinish) PlayLoop(IdleClip);
        }

        private void ApplyFootPosition()
        {
            if (_quad == null || _current == null || _current.FootFraction < 0f) return;
            Vector3 pos = _quad.localPosition;
            pos.y = _canvasHeight * (0.5f - _current.FootFraction);
            _quad.localPosition = pos;
        }

        /// <summary>
        /// Frame numbering matches the export's own convention: 0-based, left-to-right then
        /// top-to-bottom across the sheet's column/row grid. UV space is used rather than pixel
        /// space, so this stays correct even if Unity's texture importer later downsizes the
        /// sheet (maxTextureSize) - the fractional offset/scale of one cell is unchanged either
        /// way, only its absolute pixel size is.
        /// </summary>
        private void ApplyFrame(int localFrame)
        {
            if (_current == null || _material == null) return;
            int frameIndex = _current.StartFrame + localFrame;
            int col = frameIndex % _current.Columns;
            int row = frameIndex / _current.Columns;
            Vector2 scale = new Vector2(1f / _current.Columns, 1f / _current.Rows);
            Vector2 offset = new Vector2(col * scale.x, 1f - (row + 1) * scale.y);

            _material.mainTexture = _current.Texture;
            _material.mainTextureScale = scale;
            _material.mainTextureOffset = offset;
        }
    }
}
