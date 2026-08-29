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
    }
}
