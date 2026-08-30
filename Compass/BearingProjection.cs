using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>World to a signed bearing. What a compass bar needs and nothing else.</summary>
    public sealed class BearingProjection : IAtlasProjection
    {
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

                // Filtered here, before anything is copied - see Filter.
                if (!Filter.IsUnfiltered && !Filter.Allows(candidate.Marker)) continue;

                // Nothing is computed. A bearing is what a compass draws and the registry
                // already worked it out for every view at once; this projection exists to
                // say which of the shared numbers the bar cares about, and no more.
                into.Add(new AtlasSolve(candidate, default, false));
            }
        }
    }
}
