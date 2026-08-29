using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// The spaces one <see cref="AtlasRegistry"/> knows about.
    ///
    /// An instance, owned by the registry, for the same reason the registry is an
    /// instance: split-screen wants two, and a test wants one with no scene.
    ///
    /// The Default space exists from construction. A game that never thinks about spaces
    /// therefore never has to - every marker is in Default, every viewer is in Default,
    /// and the space machinery is invisible until the day someone builds an interior.
    /// </summary>
    public sealed class AtlasSpaceRegistry
    {
        private readonly Dictionary<AtlasSpaceId, AtlasSpace> spaces =
            new Dictionary<AtlasSpaceId, AtlasSpace>();

        private readonly List<AtlasSpace> ordered = new List<AtlasSpace>();

        public AtlasSpaceRegistry()
        {
            Add(new AtlasSpace { Id = AtlasSpaceId.Default, Name = "Default" });
        }

        public IReadOnlyList<AtlasSpace> All => ordered;

        public int Count => ordered.Count;

        /// <summary>The Default space, which always exists.</summary>
        public AtlasSpace Default => spaces[AtlasSpaceId.Default];

        public bool TryGet(AtlasSpaceId id, out AtlasSpace space) => spaces.TryGetValue(id, out space);

        /// <summary>The space for an id, or Default if it was never registered - so an
        /// id from stale saved data degrades to a visible marker rather than a
        /// null.</summary>
        public AtlasSpace GetOrDefault(AtlasSpaceId id) =>
            spaces.TryGetValue(id, out AtlasSpace space) ? space : Default;

        public void Add(AtlasSpace space)
        {
            if (space == null) return;

            if (spaces.TryGetValue(space.Id, out AtlasSpace existing) && !ReferenceEquals(existing, space))
            {
                // Two different names hashing to one id is astronomically unlikely and
                // completely silent when it happens - one space's markers simply appear
                // on the other's map. Cheap to check, impossible to debug otherwise.
                Debug.LogError(
                    $"[Atlas] Space '{space.Name}' has the same id as '{existing.Name}'. " +
                    "Rename one; markers in these two spaces would be indistinguishable.");
                return;
            }

            spaces[space.Id] = space;
            if (!ordered.Contains(space)) ordered.Add(space);
        }

        /// <summary>Creates and registers a space from a name.</summary>
        public AtlasSpace Create(string name)
        {
            var space = new AtlasSpace { Id = new AtlasSpaceId(name), Name = name };
            Add(space);
            return space;
        }

        public bool Remove(AtlasSpaceId id)
        {
            // Default is what an unassigned marker resolves to, so removing it would
            // turn every such marker into a null lookup.
            if (id.IsDefault) return false;
            if (!spaces.TryGetValue(id, out AtlasSpace space)) return false;

            spaces.Remove(id);
            ordered.Remove(space);
            return true;
        }
    }
}
