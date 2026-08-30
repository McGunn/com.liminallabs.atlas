namespace LiminalLabs.Atlas.SampleM0
{
    /// <summary>
    /// The demo's icon ids, named.
    ///
    /// <see cref="AtlasMarker.IconId"/> is a bare int on purpose - the package never
    /// learns what an asset reference is, and a project resolves ids from a sprite array,
    /// from Resources, from Addressables or from its own content system. The cost of that
    /// seam is that an id means nothing on its own, so the two halves have to agree by
    /// hand: these values are the indices into the AtlasIcons asset the scene builder
    /// writes, in the order it writes them.
    ///
    /// Naming them is the difference between changing an icon and hunting a magic number.
    /// </summary>
    internal static class AtlasM0Icons
    {
        public const int Objective = 0;
        public const int Waypoint = 1;
        public const int Signal = 2;

        /// <summary>
        /// The sprites the builder loads, in id order. Names are file names under the
        /// shared demo assets package; the builder resolves them and tolerates their
        /// absence, because that package is optional.
        /// </summary>
        public static readonly string[] SpriteNames =
        {
            "UI_Flag",           // Objective
            "UI_Star",           // Waypoint
            "UI_LightningBolt",  // Signal
        };

        /// <summary>Points right at zero degrees, which is what ScreenPresenter's
        /// off-screen angle expects before it rotates the arrow.</summary>
        public const string ArrowSpriteName = "UI_SmallArrowRIght";
    }
}
