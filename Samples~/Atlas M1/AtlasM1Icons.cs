namespace LiminalLabs.Atlas.SampleM1
{
    /// <summary>
    /// The demo's icon ids, named.
    ///
    /// <see cref="AtlasMarker.IconId"/> is a bare int on purpose - the package never learns
    /// what an asset reference is. The cost of that seam is that an id means nothing on its
    /// own, so the two halves agree by hand: these are indices into the AtlasIcons asset
    /// the scene builder writes, in the order it writes them.
    ///
    /// Public rather than internal: the builder that writes that asset lives in a different
    /// assembly, and an assembly boundary is exactly where internal stops.
    /// </summary>
    public static class AtlasM1Icons
    {
        public const int Objective = 0;
        public const int Discovery = 1;
        public const int Signal = 2;

        /// <summary>File names under the shared demo assets package, in id order. The
        /// builder resolves them and tolerates their absence, because that package is
        /// optional.</summary>
        public static readonly string[] SpriteNames =
        {
            "UI_Flag",           // Objective
            "UI_Star",           // Discovery
            "UI_LightningBolt",  // Signal
        };

        /// <summary>Points right at zero degrees, which is what an off-screen angle
        /// expects before the arrow is rotated.</summary>
        public const string ArrowSpriteName = "UI_SmallArrowRIght";

        /// <summary>The player marker at the centre of the map. Points up before it is
        /// rotated, so the presenter's arrow rotation is applied to art that starts north.</summary>
        public const string ViewerSpriteName = "UI_SmallArrowUp";
    }
}
