namespace LiminalLabs.Atlas
{
    /// <summary>
    /// What a marker is, broadly.
    ///
    /// An enum plus <see cref="Custom"/> rather than an open id: an enum is what makes
    /// the common cases pleasant to author and filter on, and the escape hatch keeps a
    /// game's own categories from needing a fork. The distinction a game actually draws
    /// between two custom markers lives in <see cref="AtlasMarker.IconId"/>, which is
    /// already open.
    /// </summary>
    public enum AtlasMarkerKind
    {
        Point,
        Objective,
        Waypoint,
        Hostile,
        Ally,
        Custom,
    }
}
