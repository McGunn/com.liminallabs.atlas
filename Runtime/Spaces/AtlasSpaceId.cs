using System;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Which map plane something belongs to: the overworld, a tower interior, a
    /// basement, a separate region.
    ///
    /// <b>This is content, and that is why it ships in M0 even though multi-space
    /// behaviour is M1.</b> The moment markers carry a space, the representation is
    /// baked into every save file that mentions one - changing it later is a data
    /// migration rather than a refactor. Settling it now costs a file; settling it at M2
    /// costs everyone's save games.
    ///
    /// Stored as a hash of a name rather than an index or a reference. An index reorders
    /// when someone adds a space; a reference cannot be serialized into a save without
    /// dragging the asset system in. A name hashed with FNV-1a is stable across builds,
    /// platforms and Unity versions, which is the only property that actually matters.
    ///
    /// <c>default</c> is <see cref="Default"/>, so a marker nobody assigned a space to
    /// lands somewhere real instead of nowhere.
    /// </summary>
    [Serializable]
    public readonly struct AtlasSpaceId : IEquatable<AtlasSpaceId>
    {
        private readonly uint value;

        private AtlasSpaceId(uint value) => this.value = value;

        /// <summary>The space everything is in until a game says otherwise. Zero, so an
        /// unassigned marker is a Default marker rather than an invalid one.</summary>
        public static AtlasSpaceId Default => default;

        public bool IsDefault => value == 0u;

        public uint Value => value;

        /// <summary>
        /// An id from a stable name. Case-sensitive, because "Tower" and "tower" being
        /// the same space is a coincidence nobody should rely on.
        /// </summary>
        public AtlasSpaceId(string name)
        {
            if (string.IsNullOrEmpty(name)) { value = 0u; return; }

            // FNV-1a: same answer on every platform and every run, which a
            // string.GetHashCode is explicitly not.
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < name.Length; i++)
                {
                    hash ^= name[i];
                    hash *= 16777619u;
                }

                // Zero is Default's, and a named space must never collide with it.
                value = hash == 0u ? 1u : hash;
            }
        }

        public bool Equals(AtlasSpaceId other) => value == other.value;
        public override bool Equals(object obj) => obj is AtlasSpaceId other && Equals(other);
        public override int GetHashCode() => (int)value;

        public static bool operator ==(AtlasSpaceId a, AtlasSpaceId b) => a.value == b.value;
        public static bool operator !=(AtlasSpaceId a, AtlasSpaceId b) => a.value != b.value;

        public override string ToString() => IsDefault ? "Default" : "Space#" + value;
    }
}
