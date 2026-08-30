using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// What part of a map plane is being looked at, and which way up.
    ///
    /// <b>This is the whole difference between a minimap and a world map.</b> One follows
    /// the viewer at a small radius and may rotate; the other sits still at a large one
    /// and does not. They are two framings of one projection rather than two systems, and
    /// keeping that true is what stops the same objective being registered twice, drawn by
    /// two code paths, and drifting apart on which one knows it was completed.
    ///
    /// A readonly struct of four numbers, so a frame can be built per view per frame with
    /// no allocation, compared, logged, and tested with no scene.
    /// </summary>
    public readonly struct AtlasMapFrame
    {
        /// <summary>The map-plane point drawn at the centre of the view.</summary>
        public readonly Vector2 Centre;

        /// <summary>Half the width of the framed area, in map units. The visible span is
        /// twice this, so a radius of 50 shows 100 units across.</summary>
        public readonly float Radius;

        /// <summary>
        /// Degrees to rotate the map by, counter-clockwise.
        ///
        /// Zero is north-up. A minimap that turns with the player passes the viewer's
        /// bearing from north, so its facing points up the screen.
        /// </summary>
        public readonly float Rotation;

        /// <summary>Which plane these coordinates are on. Carried so a presenter handed a
        /// frame can find the space's image without being handed the space too.</summary>
        public readonly AtlasSpaceId Space;

        public AtlasMapFrame(Vector2 centre, float radius, float rotation = 0f,
                             AtlasSpaceId space = default)
        {
            Centre = centre;
            // A zero or negative radius divides by zero downstream and puts every marker
            // at the centre, which reads as "the map is broken" rather than "the radius
            // is zero" - so it is clamped here, once, rather than guarded at each use.
            Radius = Mathf.Max(radius, AtlasMath.Epsilon);
            Rotation = rotation;
            Space = space;
        }

        /// <summary>The full span across the frame, in map units.</summary>
        public float Span => Radius * 2f;

        public override string ToString() =>
            $"map {Centre} r{Radius:0.#} {Rotation:0.#}deg";
    }
}
