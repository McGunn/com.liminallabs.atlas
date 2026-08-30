namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Whether something solid stands between the viewer and a marker.
    ///
    /// A seam rather than a raycast in the registry, for the same reason icons are a seam:
    /// the atlas has no business deciding what counts as solid. A shooter asks physics on
    /// a layer mask; a stealth game asks its own visibility grid; a strategy game asks its
    /// fog system and never touches physics at all. <see cref="AtlasPhysicsOcclusion"/> is
    /// the obvious implementation, not the only one.
    ///
    /// <b>Called once per candidate per frame, so it must be cheap.</b> A raycast per
    /// marker per frame is not cheap, which is why the shipped implementation answers from
    /// a cache and refills that cache a few markers at a time.
    /// </summary>
    public interface IAtlasOcclusion
    {
        /// <summary>
        /// Whether this target is blocked from the viewer right now.
        ///
        /// Answering "not occluded" when unsure is the right default: an indicator that
        /// wrongly dims is a bug players notice, and one that wrongly stays bright is the
        /// behaviour of every system that has no occlusion at all.
        /// </summary>
        bool IsOccluded(IAtlasTrackable target, in AtlasViewer viewer);

        /// <summary>
        /// Called once at the start of each tick, before any <see cref="IsOccluded"/>.
        /// Where a budgeted implementation does its work for the frame.
        /// </summary>
        void Tick(in AtlasViewer viewer, System.Collections.Generic.IReadOnlyList<IAtlasTrackable> targets);
    }
}
