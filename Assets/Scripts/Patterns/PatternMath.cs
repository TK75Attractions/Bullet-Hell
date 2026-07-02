using Unity.Mathematics;

/// <summary>
/// Pure helpers shared by the bullet patterns: beat/ground/gravity math, the
/// stone palette (design v2 §6), a deterministic hash, and a canonical BulletData
/// builder. Everything here is side-effect free and unit-testable.
/// </summary>
public static class PatternMath
{
    // ---- default stone palette (design v2 §6), premultiplied nowhere ----
    public static readonly float4 ColorBlock  = new float4(0.025f, 0.04f, 0.095f, 0.98f);
    public static readonly float4 ColorWarn   = new float4(0.22f, 0.76f, 1f, 0.6f);
    public static readonly float4 ColorCutter = new float4(0.5f, 0.78f, 1f, 1f);
    public static readonly float4 ColorShard  = new float4(0.38f, 0.72f, 1f, 1f);
    public static readonly float4 ColorBurst  = new float4(0.4f, 0.85f, 1f, 0.85f);
    public static readonly float4 ColorFlash  = new float4(0.3f, 0.5f, 0.95f, 0.8f);
    public static readonly float4 ColorDust   = new float4(0.35f, 0.6f, 0.95f, 0.85f);
    public static readonly float4 ColorHammer = new float4(0.55f, 0.68f, 0.95f, 1f);

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
}
