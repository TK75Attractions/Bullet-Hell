using System;

/// <summary>
/// A generation-stamped reference to an entry in QuadOrder's append-only
/// enemy-bullet list. The raw index alone is stable within a single play (the list
/// is never compacted), but a handle also carries the generation that was current
/// when the bullet was emitted. <see cref="QuadOrder.ClearManagedEnemyDanmaku"/>
/// bumps the generation, so any handle minted before a clear is recognised as stale
/// and ignored instead of mutating a faded/reused slot.
///
/// The struct is pure and unit-tested independently of the Unity runtime.
/// </summary>
[Serializable]
public readonly struct BulletHandle : IEquatable<BulletHandle>
{
    public readonly int Index;
    public readonly int Generation;

    public BulletHandle(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    public static readonly BulletHandle Invalid = new BulletHandle(-1, -1);

    public bool IsSlotAssigned => Index >= 0;

    /// <summary>
    /// True when this handle still points at a live slot: same generation and an
    /// in-range index for the current list length.
    /// </summary>
    public bool IsValidFor(int currentGeneration, int listLength)
    {
        return Index >= 0 && Index < listLength && Generation == currentGeneration;
    }

    public bool Equals(BulletHandle other) => Index == other.Index && Generation == other.Generation;
    public override bool Equals(object obj) => obj is BulletHandle other && Equals(other);
    public override int GetHashCode() => (Index * 397) ^ Generation;
    public override string ToString() => $"BulletHandle(index={Index}, gen={Generation})";
}
