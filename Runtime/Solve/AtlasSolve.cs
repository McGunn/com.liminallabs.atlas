using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// One tracked thing, solved against one viewer, for one frame.
    ///
    /// Every presenter reads this and nothing else. A presenter that reaches past it to
    /// a <c>Transform</c> or a <c>Camera</c> has broken the architecture: the reason a
    /// compass bar and a screen indicator agree about what is behind you is that they
    /// are looking at the same numbers, computed once.
    ///
    /// Readonly, and a struct, because there is one of these per marker per projection
    /// per frame and they live in a reused list.
    /// </summary>
    public readonly struct AtlasSolve
    {
        /// <summary>What was solved. Presenters use it for identity, not for querying.</summary>
        public readonly IAtlasTrackable Target;

        /// <summary>The marker as it was when solved, so a presenter never has to go
        /// back and ask - and cannot see it change halfway through a frame.</summary>
        public readonly AtlasMarker Marker;

        /// <summary>Signed degrees from the viewer's facing. Negative is left.</summary>
        public readonly float Bearing;

        public readonly float Distance;

        /// <summary>Alpha from distance. Applied as-is; curves are M1.</summary>
        public readonly float Fade;

        /// <summary>x and y in 0..1, z the distance along the viewer's forward axis.
        /// <b>z &lt; 0 means behind the viewer</b>, and x and y are mirrored when it is -
        /// see <see cref="AtlasMath.ClampToEdge"/>.</summary>
        public readonly Vector3 ViewportPoint;

        /// <summary>Position on the target's map plane. Unset in M0; the map projection
        /// arrives in M1, and the field is here so that is additive rather than a
        /// change to a struct saved data already refers to.</summary>
        public readonly Vector2 MapPoint;

        public readonly bool OnScreen;

        /// <summary>Whether the target shares the viewer's space. False markers are
        /// excluded by the registry in M0; M1 gives projections the choice of surfacing
        /// them as an edge hint instead.</summary>
        public readonly bool SameSpace;

        /// <summary>Metres above the viewer; negative is below.</summary>
        public readonly float Elevation;

        /// <summary>Above, below, or level. Banded, so a kerb is not another floor.</summary>
        public readonly AtlasElevation Level;

        /// <summary>Whether something solid stands between the viewer and this marker.
        /// Always false unless the registry has an <see cref="IAtlasOcclusion"/>.</summary>
        public readonly bool Occluded;

        /// <summary>
        /// From a candidate, plus the one or two things a view works out for itself.
        ///
        /// The shape that keeps the shared arithmetic shared: everything a projection did
        /// not compute is copied across rather than recomputed, so a view cannot silently
        /// disagree with its siblings about a distance or a bearing.
        /// </summary>
        public AtlasSolve(in AtlasCandidate candidate, Vector2 mapPoint, bool onScreen)
        {
            Target = candidate.Target;
            Marker = candidate.Marker;
            Bearing = candidate.Bearing;
            Distance = candidate.Distance;
            Fade = candidate.Fade;
            ViewportPoint = candidate.ViewportPoint;
            MapPoint = mapPoint;
            OnScreen = onScreen;
            SameSpace = candidate.SameSpace;
            Elevation = candidate.Elevation;
            Level = candidate.Level;
            Occluded = candidate.Occluded;
        }

        public AtlasSolve(IAtlasTrackable target, in AtlasMarker marker, float bearing,
                          float distance, float fade, Vector3 viewportPoint, Vector2 mapPoint,
                          bool onScreen, bool sameSpace)
        {
            Elevation = 0f;
            Level = AtlasElevation.Level;
            Occluded = false;
            Target = target;
            Marker = marker;
            Bearing = bearing;
            Distance = distance;
            Fade = fade;
            ViewportPoint = viewportPoint;
            MapPoint = mapPoint;
            OnScreen = onScreen;
            SameSpace = sameSpace;
        }

        /// <summary>True when the viewer is behind the target's projected plane. Reads
        /// better at a call site than comparing a z component.</summary>
        public bool Behind => ViewportPoint.z < 0f;

        public override string ToString() =>
            $"{Marker.Label ?? Marker.Kind.ToString()} @ {Bearing:0.#}° {Distance:0.#}m" +
            (OnScreen ? " on-screen" : Behind ? " behind" : " off-screen");
    }
}
