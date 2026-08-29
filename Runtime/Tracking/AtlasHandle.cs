using System;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// What <see cref="AtlasRegistry.Track"/> gives back.
    ///
    /// Index plus generation rather than a bare index. The generation is what makes a
    /// released handle stay dead: reuse the slot, bump the generation, and the old
    /// handle no longer matches. Without it, releasing one tracked object and tracking
    /// another silently hands the first caller control of the second - a bug that only
    /// shows up under load, which is exactly when it is hardest to find.
    /// </summary>
    public readonly struct AtlasHandle : IEquatable<AtlasHandle>
    {
        internal readonly int Index;
        internal readonly int Generation;

        internal AtlasHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public static AtlasHandle None => default;

        /// <summary>Generation zero is never issued, so <c>default</c> is always invalid.</summary>
        public bool IsValid => Generation != 0;

        public bool Equals(AtlasHandle other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AtlasHandle other && Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;

        public static bool operator ==(AtlasHandle a, AtlasHandle b) => a.Equals(b);
        public static bool operator !=(AtlasHandle a, AtlasHandle b) => !a.Equals(b);

        public override string ToString() => IsValid ? $"Atlas#{Index}.{Generation}" : "Atlas#none";
    }
}
