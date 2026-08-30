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
                          IReadOnlyList<IAtlasTrackable> targets,
                          List<AtlasSolve> into)
        {
            // Indexed rather than foreach: iterating an IReadOnlyList<T> with foreach
            // boxes the enumerator, once per projection per frame, forever. Test 15.
            for (int i = 0; i < targets.Count; i++)
            {
                IAtlasTrackable target = targets[i];
                Vector3 position = target.Position;
                AtlasMarker marker = target.Marker;

                // Filtered here, before anything is solved for it - see Filter.
                if (!Filter.IsUnfiltered && !Filter.Allows(marker)) continue;

                float distance = Vector3.Distance(viewer.Position, position);

                into.Add(new AtlasSolve(
                    target,
                    marker,
                    AtlasMath.Bearing(viewer, position),
                    distance,
                    AtlasMath.Fade(distance, marker.MaxDistance),
                    default,
                    default,
                    false,
                    target.Space == viewer.Space));
            }
        }
    }
}
