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

        /// <summary>
        /// A sprite for this one marker, bypassing <see cref="IconId"/> and the provider.
        ///
        /// The id and its provider are the right shape for a game with a fixed icon set,
        /// and the wrong one for the handful of markers that are genuinely one-off - a
        /// contract's portrait, a photographed landmark, an icon a player chose. Those
        /// would otherwise each need an id, permanently, in an array whose order is a
        /// contract with save data.
        ///
        /// A plain Sprite rather than an asset reference: whoever sets this already holds
        /// the sprite, and making the package learn about Addressables to receive
        /// something the caller has in hand would be the tail wagging the dog.
        /// </summary>
        public Sprite IconOverride;

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
            IconOverride = null,
            Tint = Color.white,
        };
    }
}
