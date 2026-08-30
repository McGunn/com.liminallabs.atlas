using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>World to viewport. What a floating on-screen indicator needs.</summary>
    public sealed class ScreenProjection : IAtlasProjection
    {
        /// <summary>How far inside the viewport a marker must be to count as on screen.
        /// Matches the presenter's edge inset so an icon does not flicker between
        /// "on screen" and "clamped to the edge" while it sits on the boundary.</summary>
        public float EdgeMargin = 0.05f;

        /// <summary>
        /// Which markers this view draws. Unfiltered by default.
        ///
        /// Applied here rather than in the presenter because the registry has
        /// already truncated to MaxMarkers by the time a presenter sees anything:
        /// filtering afterwards would show whatever passed a priority cut against
        /// every other kind, rather than the nearest markers of the kind asked for.
        /// </summary>
        public AtlasFilter Filter { get; set; }

        public void Solve(in AtlasViewer viewer, AtlasSpaceRegistry spaces,
                          IReadOnlyList<IAtlasTrackable> targets,
                          List<AtlasSolve> into)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                IAtlasTrackable target = targets[i];
                Vector3 position = target.Position;
                AtlasMarker marker = target.Marker;

                // Filtered here, before anything is solved for it - see Filter.
                if (!Filter.IsUnfiltered && !Filter.Allows(marker)) continue;

                float distance = Vector3.Distance(viewer.Position, position);
                Vector3 viewport = AtlasMath.Viewport(viewer, position);

                into.Add(new AtlasSolve(
                    target,
                    marker,
                    AtlasMath.Bearing(viewer, position),
                    distance,
                    AtlasMath.Fade(distance, marker.MaxDistance),
                    viewport,
                    default,
                    AtlasMath.IsOnScreen(viewport, EdgeMargin),
                    target.Space == viewer.Space));
            }
        }
    }
}
