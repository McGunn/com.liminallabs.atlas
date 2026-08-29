using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// The camera, flattened into numbers, once per frame.
    ///
    /// This type lives outside <c>Solve/</c> on purpose, and it is the only file in the
    /// package that mentions <c>Camera</c>. Capturing the camera here is what lets every
    /// line of maths downstream be a pure function of plain values - testable with no
    /// scene, no camera, and no rendered frame. Let <c>Camera</c> leak one folder deeper
    /// and every test in the suite needs a scene to run, which is the same as not having
    /// them.
    /// </summary>
    public readonly struct AtlasViewer
    {
        public readonly Vector3 Position;
        public readonly Vector3 Forward;

        /// <summary>
        /// The reference up that defines "horizontal" for bearing - <b>not</b> the
        /// camera's own up.
        ///
        /// That distinction is the whole of pitch independence. Bearing is measured in
        /// the plane perpendicular to this, so with world up here, tilting the camera
        /// down does not swing the compass. A game on a spherical world passes the local
        /// gravity up instead and everything else follows.
        /// </summary>
        public readonly Vector3 Up;

        /// <summary>The viewer's right. Supplies the sign of every bearing, which is why
        /// the convention survives pitch.</summary>
        public readonly Vector3 Right;

        public readonly float FieldOfView;
        public readonly float Aspect;

        /// <summary>Projection times world-to-camera. Behind the viewer its w is
        /// negative, which is where the mirrored coordinates come from.</summary>
        public readonly Matrix4x4 WorldToViewport;

        public readonly AtlasSpaceId Space;

        public AtlasViewer(Vector3 position, Vector3 forward, Vector3 up, Vector3 right,
                           float fieldOfView, float aspect, Matrix4x4 worldToViewport,
                           AtlasSpaceId space)
        {
            Position = position;
            Forward = forward;
            Up = up;
            Right = right;
            FieldOfView = fieldOfView;
            Aspect = aspect;
            WorldToViewport = worldToViewport;
            Space = space;
        }

        /// <summary>
        /// Captures a camera.
        /// </summary>
        /// <param name="camera">The camera to read. Not retained.</param>
        /// <param name="space">Which space the viewer is looking at.</param>
        /// <param name="referenceUp">
        /// What counts as up for bearing. World up by default, which is what makes the
        /// compass ignore pitch; pass the character's up on a spherical world.
        /// </param>
        public static AtlasViewer FromCamera(Camera camera, AtlasSpaceId space,
                                             Vector3 referenceUp = default)
        {
            if (camera == null) return default;

            Transform transform = camera.transform;
            Vector3 up = referenceUp.sqrMagnitude > 1e-6f ? referenceUp.normalized : Vector3.up;

            return new AtlasViewer(
                transform.position,
                transform.forward,
                up,
                transform.right,
                camera.fieldOfView,
                camera.aspect,
                camera.projectionMatrix * camera.worldToCameraMatrix,
                space);
        }
    }
}
