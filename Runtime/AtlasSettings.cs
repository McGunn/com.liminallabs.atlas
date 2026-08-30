namespace LiminalLabs.Atlas
{
    /// <summary>
    /// What one registry is allowed to do.
    ///
    /// A plain class, not a ScriptableObject. The house rule puts per-project
    /// configuration in settings assets, and it is the right rule for a bus layout or a
    /// material taxonomy - configuration the whole team shares and version-controls.
    ///
    /// This is not that. A registry is constructed by the game, split-screen constructs
    /// two with different limits, and a test constructs one with no scene at all. An
    /// asset would make the last of those need an editor API to build, which is exactly
    /// the coupling the no-singleton rule exists to avoid.
    /// </summary>
    public sealed class AtlasSettings
    {
        /// <summary>
        /// How many markers reach a presenter in one frame.
        ///
        /// Also the size of every presenter's pool, allocated once at Awake - so this is
        /// a hard ceiling rather than a hint, and raising it after presenters exist does
        /// nothing until they are rebuilt. Twenty markers within ten degrees is a
        /// legibility problem long before it is a performance one.
        /// </summary>
        public int MaxMarkers = 32;

        /// <summary>
        /// A cull distance applied to markers that do not set their own. Zero means no
        /// limit, which is the right default for a package that cannot know the scale of
        /// the world it is dropped into.
        /// </summary>
        public float DefaultMaxDistance;

        /// <summary>Exclude markers in a space other than the viewer's. M1 gives
        /// projections the option of showing them as an edge hint instead.</summary>
        public bool CullOtherSpaces = true;

        /// <summary>
        /// How many markers past the largest view's limit are solved each frame.
        ///
        /// Solving is the expensive half, so the registry only solves the top slice by
        /// priority - but projections filter further, by space, by AtlasFilter and by
        /// fade, so solving exactly the limit would leave a heavily filtered view short of
        /// markers it would have drawn. Four is generous for a HUD and still bounded; a
        /// game whose views filter hard wants more, and one that never filters can drop it
        /// to 1 and pay for exactly what it draws.
        /// </summary>
        public int CandidateSlack = 4;

        /// <summary>
        /// Metres either side of the viewer that still count as level.
        ///
        /// A band rather than a comparison, because the honest answer near zero is "level"
        /// and a strict greater-than would flicker an up-chevron on and off as a player
        /// walked a ramp. Roughly a storey by default, so a marker on the next floor reads
        /// as above and one on a kerb does not.
        /// </summary>
        public float ElevationBand = 2.5f;
    }
}
