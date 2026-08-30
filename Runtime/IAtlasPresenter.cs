using System.Collections.Generic;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Draws a solve list. That is the whole contract, and the narrowness is the point.
    ///
    /// <b>A presenter never queries the world.</b> No <c>Camera</c>, no <c>Transform</c>
    /// belonging to anything it is drawing. It receives the frame's viewer - already
    /// flattened to a struct, already frozen - and a list of solves computed from it.
    ///
    /// The viewer is there for what a compass needs beyond its markers: N, E, S and W are
    /// directions with no position, so no solve can carry them. A presenter may run the
    /// pure functions in <see cref="AtlasMath"/> over that struct, and may do nothing
    /// else - which keeps the guarantee that matters, since both views are then reading
    /// the same frozen input through the same functions.
    ///
    /// That is what makes the compass and the screen indicators agree about what is
    /// behind you: they are not two implementations that happen to match, they are two
    /// views of one answer. It is also why a studio can throw both shipped presenters
    /// away and keep the registry.
    /// </summary>
    public interface IAtlasPresenter
    {
        /// <summary>
        /// Draw. The list is reused between frames and must not be retained - copy
        /// anything that needs to outlive the call.
        /// </summary>
        void Present(in AtlasViewer viewer, IReadOnlyList<AtlasSolve> solves);
    }

    /// <summary>
    /// World to something a presenter can draw.
    ///
    /// Bearing, screen and (from M1) map are separate projections rather than one solve
    /// with every field filled, because they genuinely differ - and a projection nobody
    /// added costs nothing per frame.
    /// </summary>
    public interface IAtlasProjection
    {
        /// <summary>
        /// Solve every target into <paramref name="into"/>, which arrives cleared and is
        /// reused between frames.
        /// </summary>
        void Solve(in AtlasViewer viewer, IReadOnlyList<IAtlasTrackable> targets, List<AtlasSolve> into);
    }
}
