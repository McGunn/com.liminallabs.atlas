using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Which way up a map is drawn.
    /// </summary>
    public enum AtlasMapRotation
    {
        /// <summary>North is always up. Easier to build a mental model from, which is why
        /// every world map works this way.</summary>
        NorthUp,

        /// <summary>The viewer's facing is up. Easier to steer by, which is why most
        /// minimaps work this way.</summary>
        ViewerUp,
    }

    /// <summary>
    /// Where the frame is centred.
    /// </summary>
    public enum AtlasMapCentre
    {
        /// <summary>Follows the viewer. A minimap.</summary>
        Viewer,

        /// <summary>Sits still at <see cref="MapProjection.FixedCentre"/>. A world map,
        /// and from M2 the thing pan and zoom move.</summary>
        Fixed,

        /// <summary>Frames the whole space's bounds. A world map that needs no numbers
        /// authored, because the space already knows how big it is.</summary>
        SpaceBounds,
    }

    /// <summary>
    /// World positions onto a map plane, framed for one view.
    ///
    /// The third projection, alongside bearing and screen, and the one the space model was
    /// built for: a marker's map point is its position <i>on a plane</i>, and which plane
    /// is the space's business rather than this projection's.
    ///
    /// <b>A minimap and a world map are two instances of this</b>, differing only in
    /// <see cref="Centre"/>, <see cref="Radius"/> and <see cref="Rotation"/>. That is the
    /// design's central claim about maps, and it is what stops one objective being
    /// registered twice and drifting.
    ///
    /// Markers in another space are excluded here rather than drawn faintly, because a
    /// marker on a different floor at the same XZ is exactly on top of you on a map and
    /// says something false. The registry's <c>CullOtherSpaces</c> usually removes them
    /// first; this handles the case where it is switched off because the compass wants
    /// them.
    /// </summary>
    public sealed class MapProjection : IAtlasProjection
    {
        /// <summary>How the frame is centred. Viewer for a minimap, Fixed or SpaceBounds
        /// for a world map.</summary>
        public AtlasMapCentre Centre { get; set; } = AtlasMapCentre.Viewer;

        /// <summary>Used when <see cref="Centre"/> is Fixed. In map-plane units.</summary>
        public Vector2 FixedCentre { get; set; }

        /// <summary>Half the visible span, in map units. A radius of 50 shows 100 across.</summary>
        public float Radius { get; set; } = 60f;

        public AtlasMapRotation Rotation { get; set; } = AtlasMapRotation.NorthUp;

        /// <summary>
        /// Markers further than this fraction of the radius are dropped entirely.
        ///
        /// Above 1 so that a presenter pinning to the edge has something to pin: a marker
        /// culled the instant it left the circle would pop out of an edge indicator
        /// rather than settle into one.
        /// </summary>
        public float CullRadiusFraction { get; set; } = 4f;

        /// <summary>The frame the last <see cref="Solve"/> produced. A presenter needs it
        /// to draw the background image and the compass rose the same way up as the
        /// markers, and recomputing it there is how the two come to disagree.</summary>
        public AtlasMapFrame LastFrame { get; private set; }

        public void Solve(in AtlasViewer viewer, AtlasSpaceRegistry spaces,
                          IReadOnlyList<IAtlasTrackable> targets, List<AtlasSolve> into)
        {
            AtlasSpace space = spaces != null ? spaces.GetOrDefault(viewer.Space) : null;

            AtlasMapFrame frame = BuildFrame(viewer, space);
            LastFrame = frame;

            for (int i = 0; i < targets.Count; i++)
            {
                IAtlasTrackable target = targets[i];
                if (target == null || !target.IsTracked) continue;

                bool sameSpace = target.Space == viewer.Space;
                if (!sameSpace) continue;

                Vector3 world = target.Position;
                Vector2 onPlane = space != null ? space.ToMap(world) : Flatten(world);

                float radiusFraction = AtlasMath.MapRadiusFraction(frame, onPlane);
                if (radiusFraction > CullRadiusFraction) continue;

                Vector2 mapPoint = AtlasMath.MapPoint(frame, onPlane);

                float distance = Vector3.Distance(viewer.Position, world);
                float fade = AtlasMath.Fade(distance, target.Marker.MaxDistance);
                if (fade <= 0f) continue;

                // OnScreen means "inside the frame" here. The word is the solve's, shared
                // across projections on purpose: a presenter asking "do I draw this where
                // it is, or pin it to an edge?" is asking one question whichever view it
                // is, and it should not have to know which projection filled the struct.
                bool inside = radiusFraction <= 1f;

                // The real camera viewport, not the map point repeated with the radius
                // fraction stuffed into z. Filling a documented field with a different
                // quantity because it was free is how a struct stops meaning what it says,
                // and the fraction is recoverable from MapPoint exactly:
                // radiusFraction == 2 * |MapPoint - (0.5, 0.5)|.
                into.Add(new AtlasSolve(
                    target,
                    target.Marker,
                    AtlasMath.Bearing(viewer, world),
                    distance,
                    fade,
                    AtlasMath.Viewport(viewer, world),
                    mapPoint,
                    inside,
                    true));
            }
        }

        /// <summary>
        /// The frame for this tick.
        ///
        /// Public so a world map's UI can ask what it is currently showing without
        /// re-deriving it - which, at M2, is what pan and zoom will move.
        /// </summary>
        public AtlasMapFrame BuildFrame(in AtlasViewer viewer, AtlasSpace space)
        {
            Vector2 centre;
            float radius = Radius;

            switch (Centre)
            {
                case AtlasMapCentre.Fixed:
                    centre = FixedCentre;
                    break;

                case AtlasMapCentre.SpaceBounds when space != null:
                    Bounds bounds = space.WorldBounds;
                    centre = space.ToMap(bounds.center);

                    // Half the larger side, so nothing is framed out. Squaring it up here
                    // rather than in the presenter keeps a rectangular map rect from
                    // silently cropping a space that is wider than it is tall.
                    Vector2 extentOnPlane = space.ToMap(bounds.center + bounds.extents) - centre;
                    float fromBounds = Mathf.Max(Mathf.Abs(extentOnPlane.x), Mathf.Abs(extentOnPlane.y));
                    if (fromBounds > AtlasMath.Epsilon) radius = fromBounds;
                    break;

                default:
                    centre = space != null ? space.ToMap(viewer.Position) : Flatten(viewer.Position);
                    break;
            }

            // Negated, and the negation is the whole of viewer-up. BearingOfDirection
            // says where north is relative to the viewer; the map has to turn by the
            // opposite of that to put the viewer's facing at the top. Face east and north
            // reads -90, so the map turns +90 and north ends up on your left, which is
            // where it is. Getting this backwards produces a minimap that turns the wrong
            // way, which looks like a control-inversion bug rather than a sign error.
            float rotation = Rotation == AtlasMapRotation.ViewerUp
                ? -AtlasMath.BearingOfDirection(viewer, Vector3.forward)
                : 0f;

            return new AtlasMapFrame(centre, radius, rotation, viewer.Space);
        }

        /// <summary>The fallback plane when there is no space at all: world XZ, which is
        /// what <see cref="AtlasSpace.XZ"/> would have given.</summary>
        private static Vector2 Flatten(Vector3 world) => new Vector2(world.x, world.z);
    }
}
