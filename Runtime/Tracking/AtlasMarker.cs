using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// What to draw, and how to rank it. Never where it is - that is
    /// <see cref="IAtlasTrackable.Position"/>, and keeping them apart is what lets one
    /// marker describe ten thousand units.
    /// </summary>
    [System.Serializable]
    public struct AtlasMarker
    {
        public AtlasMarkerKind Kind;

        /// <summary>Who survives when there are more markers than room. Higher wins.</summary>
        public float Priority;

        /// <summary>Zoom LOD threshold - fewer markers as a map zooms out. Carried in M0
        /// and used in M2, because it is a field on a struct that saved data will refer
        /// to and adding it later is a migration.</summary>
        public float Importance;

        public string Label;

        /// <summary>Beyond this, culled. Zero means no limit.</summary>
        public float MaxDistance;

        /// <summary>
        /// Resolved through <see cref="IAtlasIconProvider"/> - an int, never an asset
        /// reference.
        ///
        /// That is what keeps a HUD package free of Addressables. A project supplies
        /// sprites directly, from Resources, from Addressables or from content, and the
        /// package depends on none of them.
        /// </summary>
        public int IconId;

        public Color Tint;

        /// <summary>A marker with sensible values, since <c>default</c> would be a
        /// transparent one at priority zero.</summary>
        public static AtlasMarker Point(string label = null, int iconId = 0) => new AtlasMarker
        {
            Kind = AtlasMarkerKind.Point,
            Priority = 0f,
            Importance = 0f,
            Label = label,
            MaxDistance = 0f,
            IconId = iconId,
            Tint = Color.white,
        };
    }
}
