using Unity.Mathematics;

/// <summary>
/// Pure helpers shared by the bullet patterns: beat/ground/gravity math, the
/// stone palette (design v2 §6), a deterministic hash, and a canonical BulletData
/// builder. Everything here is side-effect free and unit-testable.
/// </summary>
public static class PatternMath
{
    // ---- default stone palette (design v2 §6.1, navy unification) ----
    // All attacks share the boss body's grayish-navy hue, kept bright enough for
    // black-background legibility. Only the block body stays near-black navy.
    public static readonly float4 ColorBlock  = new float4(0.025f, 0.04f, 0.095f, 0.98f);
    public static readonly float4 ColorWarn   = new float4(0.45f, 0.55f, 0.85f, 0.6f);
    public static readonly float4 ColorCutter = new float4(0.4f, 0.46f, 0.66f, 1f);
    public static readonly float4 ColorShard  = new float4(0.36f, 0.42f, 0.6f, 1f);
    public static readonly float4 ColorBurst  = new float4(0.42f, 0.48f, 0.7f, 0.85f);
    public static readonly float4 ColorFlash  = new float4(0.38f, 0.44f, 0.62f, 0.8f);
    public static readonly float4 ColorDust   = new float4(0.42f, 0.5f, 0.8f, 0.55f);
    public static readonly float4 ColorHammer = new float4(0.4f, 0.46f, 0.66f, 1f);

    // Dashed-frame defaults (design v2 §6, item 3): constant pitch/dash size so the
    // dotted preview stays crisp regardless of the framed region's size.
    public const float DashPitch = 0.6f;
    public const float DashScale = 0.4f;

    /// <summary>Landing centre y for a block of the given (uniform) scale so its
    /// base rests near y = 1.4 (design "接地式 1.4 + scale/2").</summary>
    public static float GroundY(float scale) => 1.4f + scale * 0.5f;

    /// <summary>Gravity that makes a body fall <paramref name="dropDistance"/> over
    /// exactly <paramref name="fallSeconds"/> from rest: g = 2Δy / t².</summary>
    public static float GravityForDrop(float dropDistance, float fallSeconds)
    {
        if (fallSeconds <= 0f) return 0f;
        return 2f * dropDistance / (fallSeconds * fallSeconds);
    }

    /// <summary>Deterministic hash in [0,1) from an integer key.</summary>
    public static float Hash01(int key)
    {
        uint h = (uint)key * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xFFFFFFu) / 16777216f;
    }

    /// <summary>Deterministic signed value in [-1,1).</summary>
    public static float HashSigned(int key) => Hash01(key) * 2f - 1f;

    /// <summary>Non-zero float, else fallback (treats 0 as "unset").</summary>
    public static float Or(float value, float fallback) => value != 0f ? value : fallback;

    public static int Or(int value, int fallback) => value != 0 ? value : fallback;

    public static float4 OrColor(float4 value, float4 fallback) =>
        (value.x == 0f && value.y == 0f && value.z == 0f && value.w == 0f) ? fallback : value;

    /// <summary>
    /// Canonical bullet builder. A bullet with <paramref name="speed"/> == 0 and no
    /// gravity sits at <paramref name="pos"/>; with gravity it falls straight down;
    /// with speed &gt; 0 it travels along <paramref name="dirRad"/>. Uses the existing
    /// BulletData constructor so startPos / nowCalculateVlc are set consistently.
    /// </summary>
    public static BulletData Make(
        int typeId, float2 pos, float dirRad, float speed, float gravity, float angleSpeed,
        float2 scale, float4 color, float life,
        float appearTime = 0f, float appearDuration = 0f, bool unCounterable = false)
    {
        return new BulletData(
            _pos: pos,
            _vlc: float2.zero,
            _s: speed,
            _g: gravity,
            _as: angleSpeed,
            _initialAngle: 0f,
            _polar: new float2(1f, dirRad),
            _absV: 0f,
            _theV: 0f,
            _start: 0f,
            _poly: float4.zero,
            type: typeId,
            _color: color,
            _scale: scale,
            _random: 0f,
            _appear: appearTime,
            _appearDuration: appearDuration,
            _life: life,
            _unCounterable: unCounterable,
            _playerInfluence: float2.zero);
    }

    /// <summary>
    /// GhostPreview modifier: makes a bullet blink as a beat-synced ghost at its
    /// origin for <paramref name="ghostSeconds"/> before it becomes solid and moves,
    /// extending its life to cover the ghost window. Ghost bullets never collide
    /// (appearTime &gt; time disables the collision job), so the preview is safe.
    /// </summary>
    public static BulletData WithGhost(BulletData bullet, float ghostSeconds)
    {
        if (ghostSeconds <= 0f) return bullet;
        bullet.appearTime = ghostSeconds;
        bullet.appearDuration = ghostSeconds;
        bullet.life = bullet.life > 0f ? bullet.life + ghostSeconds : bullet.life;
        return bullet;
    }

    /// <summary>Full-window beat-blink ghost (used by warnings): the bullet is a
    /// ghost for its whole life. Bumps alpha to offset the 0.2–0.5 ghost dimming.</summary>
    public static BulletData AsFullBlink(BulletData bullet)
    {
        bullet.appearTime = bullet.life;
        bullet.appearDuration = bullet.life;
        return bullet;
    }

    /// <summary>
    /// Builds a dotted rectangle outline centred at <paramref name="center"/> from
    /// small full-blink dash bullets. Dash pitch/size are constant (independent of
    /// w/h) so a big frame or a wide band keeps the same crisp dotted look. Appends
    /// each dash as a beat-blink warning bullet to <paramref name="output"/>.
    /// </summary>
    public static void BuildDashedFrame(
        System.Collections.Generic.List<BulletData> output,
        int warnTypeId, float2 center, float w, float h, float warnSeconds,
        float4 color, float pitch = DashPitch, float dashScale = DashScale,
        bool unCounterable = true)
    {
        float hw = w * 0.5f, hh = h * 0.5f;
        int nx = math.max(1, (int)math.round(w / pitch));
        int ny = math.max(1, (int)math.round(h / pitch));
        float2 s = new float2(dashScale, dashScale);

        void Dot(float x, float y)
        {
            BulletData b = Make(warnTypeId, new float2(x, y), 0f, 0f, 0f, 0f, s, color, warnSeconds,
                unCounterable: unCounterable);
            output.Add(AsFullBlink(b));
        }

        for (int i = 0; i <= nx; i++)
        {
            float x = center.x - hw + w * i / nx;
            Dot(x, center.y + hh);
            Dot(x, center.y - hh);
        }
        for (int j = 1; j < ny; j++)
        {
            float y = center.y - hh + h * j / ny;
            Dot(center.x - hw, y);
            Dot(center.x + hw, y);
        }
    }
}
