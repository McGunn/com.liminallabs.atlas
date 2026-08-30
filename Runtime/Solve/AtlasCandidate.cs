using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Where a marker is relative to the viewer, worked out once for the whole frame.
    ///
    /// <b>This is what makes "one solve, several views" true about cost and not only about
    /// correctness.</b> Before it existed, every projection independently read the target's
    /// position and marker and recomputed distance, bearing, fade and the viewport point —
    /// so with a compass, indicators and a map registered, the same square root ran three
    /// times and the same bearing four, per marker, per frame. The views agreed because
    /// they ran identical arithmetic, not because they shared it.
    ///
    /// Now the registry computes this once and hands it to every projection. A projection's
    /// job shrinks to what is genuinely its own: a bearing becomes a bar position, a
    /// viewport point becomes a screen point, a world position becomes a map point.
    /// </summary>
    public readonly struct AtlasCandidate
    {
        public readonly IAtlasTrackable Target;

        /// <summary>The marker as it was when the frame started. Read once, so a projection
        /// cannot see it change halfway through and two views cannot disagree about it.</summary>
        public readonly AtlasMarker Marker;

        public readonly Vector3 Position;

        /// <summary>Metres from the viewer. One square root for the whole frame.</summary>
        public readonly float Distance;

        /// <summary>Signed degrees from the viewer's facing. Negative is left.</summary>
        public readonly float Bearing;

        /// <summary>Alpha from distance, before any view's own curve.</summary>
        public readonly float Fade;

        /// <summary>x and y in 0..1, z the distance along the viewer's forward axis.
        /// <b>z &lt; 0 means behind the viewer.</b></summary>
        public readonly Vector3 ViewportPoint;

        /// <summary>Metres above the viewer; negative is below.</summary>
        public readonly float Elevation;

        /// <summary>Above, below, or near enough to level. Banded so a marker a step up a
        /// kerb does not claim to be on another floor.</summary>
        public readonly AtlasElevation Level;

        /// <summary>Whether something solid stands between the viewer and this marker.
        /// Always false unless an <see cref="IAtlasOcclusion"/> is wired up.</summary>
        public readonly bool Occluded;

        public readonly bool SameSpace;

        public AtlasCandidate(IAtlasTrackable target, in AtlasMarker marker, Vector3 position,
                              float distance, float bearing, float fade, Vector3 viewportPoint,
                              float elevation, AtlasElevation level, bool occluded, bool sameSpace)
        {
            Target = target;
            Marker = marker;
            Position = position;
            Distance = distance;
            Bearing = bearing;
            Fade = fade;
            ViewportPoint = viewportPoint;
            Elevation = elevation;
            Level = level;
            Occluded = occluded;
            SameSpace = sameSpace;
        }

        /// <summary>True when the viewer is behind the marker's projected plane.</summary>
        public bool Behind => ViewportPoint.z < 0f;
    }

    /// <summary>
    /// Whether a marker is above the viewer, below, or level with it.
    ///
    /// A band rather than a comparison, because the honest answer near zero is "level" and
    /// a strict <c>&gt;</c> would flicker a chevron on and off as the player walked up a
    /// ramp. Where the band sits is <see cref="AtlasSettings.ElevationBand"/>.
    /// </summary>
    public enum AtlasElevation
    {
        Level,
        Above,
        Below,
    }
}
