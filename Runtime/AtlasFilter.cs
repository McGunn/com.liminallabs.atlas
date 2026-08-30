using System;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Which markers a view will draw.
    ///
    /// M2's answer to crowding, and half of it. The other half is
    /// <see cref="MinimumImportance"/> — the zoom LOD the design named as the correct fix,
    /// on the grounds that a map showing everything at every zoom is a map that shows
    /// nothing at the zoom where it mattered.
    ///
    /// A struct on the projection rather than a list the presenter walks: filtering after
    /// the registry has already truncated to <c>MaxMarkers</c> means the markers that were
    /// dropped for being low priority were dropped before anyone asked whether they were
    /// the ones being looked for. Filtering here means "show me only fast travel points"
    /// gets the nearest fast travel points, not whatever fast travel points survived a
    /// priority cut against everything else.
    /// </summary>
    [Serializable]
    public struct AtlasFilter
    {
        /// <summary>
        /// Bit per <see cref="AtlasMarkerKind"/>. Zero means every kind, so a default
        /// filter shows everything and a game that never filters never notices this exists.
        /// </summary>
        public int Kinds;

        /// <summary>
        /// Markers below this are dropped. Zero means no floor.
        ///
        /// This is the LOD control: a map raises it as it zooms out, so a region shows its
        /// cities and a street shows its shops. Importance is authored per marker and has
        /// been carried since M0 for exactly this.
        /// </summary>
        public float MinimumImportance;

        /// <summary>Nothing filtered. What a projection has until someone sets otherwise.</summary>
        public static AtlasFilter All => default;

        /// <summary>A filter for one kind. Combine with <see cref="Including"/>.</summary>
        public static AtlasFilter Only(AtlasMarkerKind kind) =>
            new AtlasFilter { Kinds = 1 << (int)kind };

        /// <summary>This filter plus one more kind.</summary>
        public AtlasFilter Including(AtlasMarkerKind kind)
        {
            AtlasFilter copy = this;
            copy.Kinds |= 1 << (int)kind;
            return copy;
        }

        /// <summary>This filter without one kind. Note that removing the last kind returns
        /// to showing every kind, because zero means unfiltered - which is the behaviour a
        /// legend's checkboxes want when the last box is unticked.</summary>
        public AtlasFilter Excluding(AtlasMarkerKind kind)
        {
            AtlasFilter copy = this;
            copy.Kinds &= ~(1 << (int)kind);
            return copy;
        }

        /// <summary>This filter at a different LOD floor.</summary>
        public AtlasFilter AtImportance(float minimum)
        {
            AtlasFilter copy = this;
            copy.MinimumImportance = minimum;
            return copy;
        }

        /// <summary>Whether a kind passes. True for every kind when nothing is selected.</summary>
        public bool AllowsKind(AtlasMarkerKind kind) =>
            Kinds == 0 || (Kinds & (1 << (int)kind)) != 0;

        /// <summary>Whether a marker passes both halves.</summary>
        public bool Allows(in AtlasMarker marker) =>
            AllowsKind(marker.Kind) && marker.Importance >= MinimumImportance;

        /// <summary>Whether this filter does anything at all. Lets a presenter skip the
        /// per-marker test entirely in the common case of no filtering.</summary>
        public bool IsUnfiltered => Kinds == 0 && MinimumImportance <= 0f;
    }
}
