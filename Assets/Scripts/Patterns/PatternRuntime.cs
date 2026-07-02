using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// A single bullet a pattern wants spawned, at <see cref="TimeOffset"/> seconds
/// after the pattern event fires. The executor turns these into absolute-timed
/// emissions and feeds the existing enemy-bullet pipeline unchanged.
/// </summary>
public struct PatternEmission
{
    public float TimeOffset;
    public BulletData Bullet;

    public PatternEmission(float timeOffset, BulletData bullet)
    {
        TimeOffset = timeOffset;
        Bullet = bullet;
    }
}

/// <summary>
/// Everything a pattern needs to turn its parameters into bullets, without
/// touching Unity singletons. Type-name resolution is injected so patterns stay
/// unit-testable (tests pass a stub resolver; the runtime passes the real DB).
/// </summary>
public struct PatternContext
{
    /// <summary>Seconds per beat, from the stage BPM.</summary>
    public float BeatSeconds;

    /// <summary>Resolves a bullet type name to a typeId (negative if unknown).</summary>
    public Func<string, int> ResolveTypeId;

    public float BeatsToSeconds(float beats) => beats * BeatSeconds;

    public int Resolve(string typeName)
    {
        if (ResolveTypeId == null || string.IsNullOrEmpty(typeName)) return -1;
        return ResolveTypeId(typeName);
    }
}

/// <summary>A parameter-driven bullet generator. Pure: no global state, no side
/// effects beyond appending to <paramref name="output"/>.</summary>
public interface IBulletPattern
{
    /// <summary>Registry key, matched against <c>PatternEventData.patternType</c>.</summary>
    string TypeName { get; }

    void Emit(PatternParamsJson args, in PatternContext ctx, List<PatternEmission> output);
}

/// <summary>
/// Registry + dispatcher for pattern generators. Deterministic: given the same
/// args, context and seed, <see cref="Expand"/> appends the same emissions.
/// </summary>
public static class PatternExecutor
{
    private static readonly Dictionary<string, IBulletPattern> Patterns =
        new Dictionary<string, IBulletPattern>(StringComparer.Ordinal);

    static PatternExecutor()
    {
        Register(new FallingBlockPattern());
        Register(new RadialBurstPattern());
        Register(new CutterSweepPattern());
        Register(new BeatPulseWarnPattern());
        Register(new GhostPreviewPattern());
    }

    public static void Register(IBulletPattern pattern)
    {
        if (pattern == null || string.IsNullOrEmpty(pattern.TypeName)) return;
        Patterns[pattern.TypeName] = pattern;
    }

    public static bool IsRegistered(string typeName) =>
        !string.IsNullOrEmpty(typeName) && Patterns.ContainsKey(typeName);

    public static IEnumerable<string> RegisteredTypeNames => Patterns.Keys;

    /// <summary>
    /// Appends the emissions for one pattern event to <paramref name="output"/>.
    /// Bullets whose type name fails to resolve are skipped (never emitted with an
    /// invalid typeId, which would crash the renderer). Returns false if the
    /// pattern type is unknown.
    /// </summary>
    public static bool Expand(string patternType, PatternParamsJson args, in PatternContext ctx, List<PatternEmission> output)
    {
        if (string.IsNullOrEmpty(patternType) || !Patterns.TryGetValue(patternType, out IBulletPattern pattern))
        {
            return false;
        }

        int before = output.Count;
        pattern.Emit(args ?? new PatternParamsJson(), ctx, output);

        // Drop any emission that resolved to an invalid type so downstream systems
        // (renderer indexes BTDB.types[typeId]) never see a bad id.
        for (int i = output.Count - 1; i >= before; i--)
        {
            if (output[i].Bullet.typeId < 0)
            {
                output.RemoveAt(i);
            }
        }
        return true;
    }
}
