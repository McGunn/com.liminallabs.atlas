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
                          IReadOnlyList<AtlasCandidate> candidates,
                          List<AtlasSolve> into)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                AtlasCandidate candidate = candidates[i];

                if (!Filter.IsUnfiltered && !Filter.Allows(candidate.Marker)) continue;

                // The one thing this view decides: whether the point is on screen. The
                // viewport point itself was computed once for the whole frame, which is
                // what stops this view and the compass reaching different conclusions
                // about the same marker.
                bool onScreen = AtlasMath.IsOnScreen(candidate.ViewportPoint);

                into.Add(new AtlasSolve(candidate, default, onScreen));
            }
        }
    }
}
