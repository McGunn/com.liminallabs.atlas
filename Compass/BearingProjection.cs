using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>World to a signed bearing. What a compass bar needs and nothing else.</summary>
    public sealed class BearingProjection : IAtlasProjection
    {
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
