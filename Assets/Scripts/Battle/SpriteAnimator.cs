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

        private SpriteAnimClip _current;
        private float _frameTime;
        private bool _looping;
        private bool _returnToIdleOnFinish;

        public Texture2D Texture { get { return _current != null ? _current.Texture : null; } }
        public int FrameWidth { get { return _current != null ? _current.FrameWidth : 0; } }
        public int FrameHeight { get { return _current != null ? _current.FrameHeight : 0; } }

        /// <summary>
        /// Nominal (1x speed) seconds a single play-through of this clip takes, or 0 if this unit
        /// never shipped it. This is the number BattleScreen's event pacing was missing entirely -
        /// it was holding each event on screen for a fixed, clip-agnostic duration that happened to
        /// match an instant snap/flash, and had no way to ask "how long does this unit's own basic/
        /// skill/death clip actually take" before this existed.
        /// </summary>
        public float ClipDuration(string clipId)
        {
            SpriteAnimClip clip = GetOrLoad(clipId);
            return clip != null ? clip.FrameCount / clip.Fps : 0f;
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

        /// <summary>Called once by UnitRig.BuildSprite right after it creates the quad's material.</summary>
        public void Bind(Material material)
        {
            _material = material;
            ApplyFrame(0);
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
            if (clip == null) return;
            _current = clip;
            _frameTime = 0f;
            _looping = false;
            _returnToIdleOnFinish = !holdOnFinish;
            ApplyFrame(0);
        }

        public void Update(float deltaTime)
        {
            if (_current == null || _material == null) return;

            int frameCount = Math.Max(1, _current.FrameCount);
            _frameTime += deltaTime * _current.Fps;
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
