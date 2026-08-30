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

        /// <summary>Somewhere worth finding: a landmark, a vista, a locked door you will
        /// come back to. Distinct from <see cref="Objective"/> because discovery is a
        /// state the player changes and an objective is one the game hands out.</summary>
        Discovery,

        /// <summary>A place that can be travelled to. Its own kind rather than a
        /// discovery, because it is the one marker players look for deliberately and
        /// therefore the one worth filtering to on its own.</summary>
        FastTravel,

        /// <summary>Something happening, now, that will stop happening. Time-bounded,
        /// which is what separates it from every other kind here.</summary>
        Event,

        Custom,
    }
}
