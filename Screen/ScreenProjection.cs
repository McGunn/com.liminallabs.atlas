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

        public void Solve(in AtlasViewer viewer, IReadOnlyList<IAtlasTrackable> targets,
                          List<AtlasSolve> into)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                IAtlasTrackable target = targets[i];
                Vector3 position = target.Position;
                AtlasMarker marker = target.Marker;

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
