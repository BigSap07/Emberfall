using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// Every number of the R12 hit-feedback pass, in one object, so the flag and the rollback are
    /// one value. Presentation only: it lives in vfx.json's "juice" block, which is NOT part of
    /// ContentVersion, and nothing in a fight reads it.
    ///
    /// TWO PROFILES.
    ///   Juiced   the numbers the pass ships. FromJson starts here and lets the file override.
    ///   Legacy   what the battle view did before the pass. "juice": { "enabled": false }, or no
    ///            juice block at all, resolves to exactly this (CheckJuice pins both, field by
    ///            field). A zero in Legacy means "no new behaviour, the old code path runs".
    ///
    /// The legacy CODE is left where it was, line for line. Legacy here is the record of what those
    /// lines hold, so the harness can prove the rollback is the old numbers and not a refactor of
    /// them. See JuiceMath for the decisions that read this.
    /// </summary>
    public sealed class JuiceProfile
    {
        /// <summary>False is the rollback: every consumer takes its old path.</summary>
        public bool On;

        // ---- hit-stop, seconds both units freeze for. Skill is the cast's strike; a critical
        // skill takes the larger of skill and crit. 0 means "no skill kind, use normal or crit".
        public float HitStopNormal = 0.05f;
        public float HitStopCrit = 0.09f;
        public float HitStopSkill = 0.12f;

        // ---- hit flash. Peak is the white lerp the hit sets; FlashSeconds is how long it takes
        // to fall to zero after the freeze. FlashSeconds 0 keeps the old rate.
        public float FlashPeakNormal = 1f;
        public float FlashPeakCrit = 1f;
        public float FlashSeconds = 0.08f;
        public const float LegacyFlashRate = 5.5f;

        // ---- screen shake (strength in world units, seconds). Every strength is at or under
        // BattleFraming's 0.18 reserve. Zero strength is no shake.
        public float ShakeCritStrength = 0.09f;
        public float ShakeCritSeconds = 0.16f;
        public float ShakeSkillStrength = 0.15f;
        public float ShakeSkillSeconds = 0.22f;
        public float ShakeKillStrength = 0f;
        public float ShakeKillSeconds = 0f;

        // ---- knockback, screen pixels at 1080p. 0 keeps the old world-unit distance.
        public float KnockPxNormal = 10f;
        public float KnockPxSkill = 13f;
        public float KnockPxCrit = 16f;

        // ---- damage numbers.
        /// <summary>Scale at spawn, easing out to 1 over PopSeconds. 1 or PopSeconds 0 is no pop.</summary>
        public float PopScale = 1.4f;
        public float PopSeconds = 0.15f;
        /// <summary>Canvas pixels one number needs above or below another on the same unit.</summary>
        public float NumberLineHeight = 36f;
        /// <summary>Seconds a live number takes to move up one line when a newer one spawns under it.</summary>
        public float NumberPushSeconds = 0.08f;
        /// <summary>Live numbers on one unit. 0 means unlimited (legacy).</summary>
        public int MaxNumbersPerTarget = 4;
        /// <summary>Live numbers overall. 0 means unlimited (legacy).</summary>
        public int MaxNumbers = 14;

        // ---- skill moment: the backdrop dims toward DimTo (1 is no dim) in, hold, out seconds.
        public float DimTo = 0.70f;
        public float DimInSeconds = 0.05f;
        public float DimHoldSeconds = 0.15f;
        public float DimOutSeconds = 0.10f;
        public float RingRadius = 2.2f;
        public float RingSeconds = 0.28f;

        // ---- glow behind an impact.
        public float GlowRadius = 1.1f;
        public float GlowSeconds = 0.18f;
        public float GlowAlpha = 0.8f;
        public float GlowCritScale = 1.3f;

        // ---- death.
        public float DeathFlashSeconds = 0.08f;
        public float DeathSeconds = 0.55f;
        public float DeathFadeStart = 0.10f;
        public float DeathSink = 0.30f;
        public float SpriteDeathSink = 0.25f;
        /// <summary>Seconds the main HP fill takes to reach a new value. 0 snaps (legacy).</summary>
        public float HpFillSeconds = 0.20f;

        // ---- the effect cap. 0 means no cap (legacy).
        public int MaxQuads = 40;
        public int MaxFlipbooks = 8;
        /// <summary>Everything that draws a quad at once: MaxQuads + MaxFlipbooks + MaxNumbers.</summary>
        public int MaxTotal = 62;

        /// <summary>True draws the director's flipbooks additively.</summary>
        public bool Additive = true;

        public static JuiceProfile Juiced
        {
            get { JuiceProfile p = new JuiceProfile(); p.On = true; return p; }
        }

        /// <summary>What the battle did before R12. The literals are the ones still in UnitActor,
        /// VfxDirector and BattleScreen; JuiceChecks reads those files to prove it.</summary>
        public static JuiceProfile Legacy
        {
            get
            {
                JuiceProfile p = new JuiceProfile();
                p.On = false;
                p.HitStopNormal = 0.05f;
                p.HitStopCrit = 0.09f;
                p.HitStopSkill = 0f;
                p.FlashPeakNormal = 0.75f;
                p.FlashPeakCrit = 1f;
                p.FlashSeconds = 0f;
                p.ShakeCritStrength = 0.14f;
                p.ShakeCritSeconds = 0.2f;
                p.ShakeSkillStrength = 0.18f;
                p.ShakeSkillSeconds = 0.26f;
                p.ShakeKillStrength = 0.12f;
                p.ShakeKillSeconds = 0.22f;
                p.KnockPxNormal = 0f;
                p.KnockPxSkill = 0f;
                p.KnockPxCrit = 0f;
                p.PopScale = 1f;
                p.PopSeconds = 0f;
                p.NumberLineHeight = 0f;
                p.NumberPushSeconds = 0f;
                p.MaxNumbersPerTarget = 0;
                p.MaxNumbers = 0;
                p.DimTo = 1f;
                p.DimInSeconds = 0f;
                p.DimHoldSeconds = 0f;
                p.DimOutSeconds = 0f;
                p.RingRadius = 0f;
                p.RingSeconds = 0f;
                p.GlowRadius = 0f;
                p.GlowSeconds = 0f;
                p.GlowAlpha = 0f;
                p.GlowCritScale = 1f;
                p.DeathFlashSeconds = 0f;
                p.DeathSeconds = 0.75f;
                p.DeathFadeStart = 0.25f;
                p.DeathSink = 0.18f;
                p.SpriteDeathSink = 0f;
                p.HpFillSeconds = 0f;
                p.MaxQuads = 0;
                p.MaxFlipbooks = 8;
                p.MaxTotal = 0;
                p.Additive = false;
                return p;
            }
        }

        /// <summary>Reads vfx.json's "juice" object. Absent, not an object or "enabled": false is
        /// Legacy. Otherwise every field starts at Juiced and the file overrides it.</summary>
        public static JuiceProfile FromJson(JsonValue j)
        {
            if (j == null || !j.IsObject) return Legacy;
            if (!j.GetBool("enabled", true)) return Legacy;

            JuiceProfile p = Juiced;

            JsonValue hs = j["hitStop"];
            p.HitStopNormal = hs.GetFloat("normal", p.HitStopNormal);
            p.HitStopCrit = hs.GetFloat("crit", p.HitStopCrit);
            p.HitStopSkill = hs.GetFloat("skill", p.HitStopSkill);

            JsonValue fl = j["flash"];
            p.FlashPeakNormal = fl.GetFloat("peakNormal", p.FlashPeakNormal);
            p.FlashPeakCrit = fl.GetFloat("peakCrit", p.FlashPeakCrit);
            p.FlashSeconds = fl.GetFloat("seconds", p.FlashSeconds);

            JsonValue sh = j["shake"];
            p.ShakeCritStrength = sh["crit"].GetFloat("strength", p.ShakeCritStrength);
            p.ShakeCritSeconds = sh["crit"].GetFloat("seconds", p.ShakeCritSeconds);
            p.ShakeSkillStrength = sh["skill"].GetFloat("strength", p.ShakeSkillStrength);
            p.ShakeSkillSeconds = sh["skill"].GetFloat("seconds", p.ShakeSkillSeconds);
            p.ShakeKillStrength = sh["kill"].GetFloat("strength", p.ShakeKillStrength);
            p.ShakeKillSeconds = sh["kill"].GetFloat("seconds", p.ShakeKillSeconds);

            JsonValue kn = j["knockPx"];
            p.KnockPxNormal = kn.GetFloat("normal", p.KnockPxNormal);
            p.KnockPxSkill = kn.GetFloat("skill", p.KnockPxSkill);
            p.KnockPxCrit = kn.GetFloat("crit", p.KnockPxCrit);

            JsonValue nu = j["numbers"];
            p.PopScale = nu.GetFloat("pop", p.PopScale);
            p.PopSeconds = nu.GetFloat("popSeconds", p.PopSeconds);
            p.NumberLineHeight = nu.GetFloat("lineHeight", p.NumberLineHeight);
            p.NumberPushSeconds = nu.GetFloat("pushSeconds", p.NumberPushSeconds);
            p.MaxNumbersPerTarget = nu.GetInt("maxPerTarget", p.MaxNumbersPerTarget);
            p.MaxNumbers = nu.GetInt("max", p.MaxNumbers);

            JsonValue dm = j["dim"];
            p.DimTo = dm.GetFloat("to", p.DimTo);
            p.DimInSeconds = dm.GetFloat("in", p.DimInSeconds);
            p.DimHoldSeconds = dm.GetFloat("hold", p.DimHoldSeconds);
            p.DimOutSeconds = dm.GetFloat("out", p.DimOutSeconds);
            p.RingRadius = dm.GetFloat("ringRadius", p.RingRadius);
            p.RingSeconds = dm.GetFloat("ringSeconds", p.RingSeconds);

            JsonValue gl = j["glow"];
            p.GlowRadius = gl.GetFloat("radius", p.GlowRadius);
            p.GlowSeconds = gl.GetFloat("seconds", p.GlowSeconds);
            p.GlowAlpha = gl.GetFloat("alpha", p.GlowAlpha);
            p.GlowCritScale = gl.GetFloat("critScale", p.GlowCritScale);
            p.Additive = gl.GetBool("additive", p.Additive);

            JsonValue de = j["death"];
            p.DeathFlashSeconds = de.GetFloat("flashSeconds", p.DeathFlashSeconds);
            p.DeathSeconds = de.GetFloat("seconds", p.DeathSeconds);
            p.DeathFadeStart = de.GetFloat("fadeStart", p.DeathFadeStart);
            p.DeathSink = de.GetFloat("sink", p.DeathSink);
            p.SpriteDeathSink = de.GetFloat("spriteSink", p.SpriteDeathSink);
            p.HpFillSeconds = de.GetFloat("hpFillSeconds", p.HpFillSeconds);

            JsonValue cap = j["cap"];
            p.MaxQuads = cap.GetInt("quads", p.MaxQuads);
            p.MaxFlipbooks = cap.GetInt("flipbooks", p.MaxFlipbooks);
            p.MaxTotal = cap.GetInt("total", p.MaxTotal);

            p.On = true;
            return p;
        }
    }
}
