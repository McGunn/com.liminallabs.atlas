using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// §7.1. No scene, no camera, no rendered frame - which is the property the package
    /// is arranged around. If any of these ever needs a MonoBehaviour, the solve has
    /// been written in the wrong place.
    /// </summary>
    public class MathTests
    {
        /// <summary>
        /// A viewer built by hand, looking down +Z from the origin.
        ///
        /// The projection matrix is written out rather than taken from
        /// <c>Matrix4x4.Perspective</c>, because that one is native-backed - and a test
        /// suite whose whole claim is "runs without an engine" cannot call into the
        /// engine to set itself up.
        /// </summary>
        private static AtlasViewer Viewer(Vector3 position = default, float yaw = 0f, float pitch = 0f)
        {
            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            var forward = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                -Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)).normalized;

            // Right stays horizontal under pitch, which is exactly why the sign
            // convention is derived from it.
            var right = new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad)).normalized;

            return new AtlasViewer(position, forward, Vector3.up, right, 60f, 16f / 9f,
                ViewProjection(position, forward, right), AtlasSpaceId.Default);
        }

        private static Matrix4x4 ViewProjection(Vector3 position, Vector3 forward, Vector3 right)
        {
            Vector3 up = Vector3.Cross(forward, right).normalized;

            // World to camera: Unity's camera looks down -Z, so the forward row is negated.
            var view = Matrix4x4.identity;
            view.m00 = right.x;    view.m01 = right.y;    view.m02 = right.z;    view.m03 = -Vector3.Dot(right, position);
            view.m10 = up.x;       view.m11 = up.y;       view.m12 = up.z;       view.m13 = -Vector3.Dot(up, position);
            view.m20 = -forward.x; view.m21 = -forward.y; view.m22 = -forward.z; view.m23 = Vector3.Dot(forward, position);

            const float near = 0.3f, far = 1000f, fov = 60f, aspect = 16f / 9f;
            float f = 1f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            var projection = new Matrix4x4();
            projection.m00 = f / aspect;
            projection.m11 = f;
            projection.m22 = -(far + near) / (far - near);
            projection.m23 = -(2f * far * near) / (far - near);
            projection.m32 = -1f;

            return projection * view;
        }

        // 1
        [Test]
        public void AheadIsZero()
        {
            Assert.AreEqual(0f, AtlasMath.Bearing(Viewer(), new Vector3(0f, 0f, 10f)), 0.01f);
        }

        // 2
        [Test]
        public void RightIsPositiveAndLeftIsNegative()
        {
            AtlasViewer viewer = Viewer();

            Assert.AreEqual(90f, AtlasMath.Bearing(viewer, new Vector3(10f, 0f, 0f)), 0.01f,
                "90 degrees right must be +90");
            Assert.AreEqual(-90f, AtlasMath.Bearing(viewer, new Vector3(-10f, 0f, 0f)), 0.01f,
                "90 degrees left must be -90");
        }

        // 3
        [Test]
        public void BehindIsOneEightyEitherWay()
        {
            float bearing = AtlasMath.Bearing(Viewer(), new Vector3(0f, 0f, -10f));
            Assert.AreEqual(180f, Mathf.Abs(bearing), 0.01f);
        }

        // 4
        [Test]
        public void PitchDoesNotChangeBearing()
        {
            var target = new Vector3(0f, 8f, 10f);   // ahead and well above

            Assert.AreEqual(0f, AtlasMath.Bearing(Viewer(pitch: 0f), target), 0.01f);
            Assert.AreEqual(0f, AtlasMath.Bearing(Viewer(pitch: 45f), target), 0.01f,
                "a compass that swings when you look down is the bug this convention prevents");
            Assert.AreEqual(0f, AtlasMath.Bearing(Viewer(pitch: -60f), target), 0.01f);
        }

        // 4b - the gimbal case a hand-rolled compass spins in
        [Test]
        public void LookingStraightDownStillGivesABearing()
        {
            float bearing = AtlasMath.Bearing(Viewer(pitch: 89.999f), new Vector3(10f, 0f, 0f));
            Assert.AreEqual(90f, bearing, 1f, "straight down must not make the bearing meaningless");
        }

        // 5
        [Test]
        public void TargetElevationDoesNotChangeBearing()
        {
            AtlasViewer viewer = Viewer();

            float low = AtlasMath.Bearing(viewer, new Vector3(10f, -50f, 10f));
            float high = AtlasMath.Bearing(viewer, new Vector3(10f, 50f, 10f));

            Assert.AreEqual(45f, low, 0.01f);
            Assert.AreEqual(low, high, 0.01f);
        }

        // 6
        [Test]
        public void ViewportDepthSignsFrontAndBack()
        {
            AtlasViewer viewer = Viewer();

            Assert.Greater(AtlasMath.Viewport(viewer, new Vector3(0f, 0f, 10f)).z, 0f);
            Assert.Less(AtlasMath.Viewport(viewer, new Vector3(0f, 0f, -10f)).z, 0f);
        }

        // 7
        [Test]
        public void OnScreenTargetIsInsideTheViewport()
        {
            Vector3 viewport = AtlasMath.Viewport(Viewer(), new Vector3(0.5f, 0.2f, 10f));

            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 1f));
            Assert.IsTrue(AtlasMath.IsOnScreen(viewport));
        }

        // 8 - the one this package exists to stop shipping
        [Test]
        public void BehindAndLeftClampsToTheLeftEdge()
        {
            AtlasViewer viewer = Viewer();
            var target = new Vector3(-10f, 0f, -10f);   // behind and to the left

            Vector3 viewport = AtlasMath.Viewport(viewer, target);
            Assert.Less(viewport.z, 0f, "the target really is behind");

            Assert.Greater(viewport.x, 0.5f,
                "the raw projection mirrors it to the right - if this ever fails, the " +
                "test below is no longer testing anything");

            Vector2 edge = AtlasMath.ClampToEdge(viewport, 0.05f, out float angle);

            Assert.Less(edge.x, 0.5f,
                "mirrored through the centre before clamping, so it pins to the LEFT edge");
            Assert.That(Mathf.Abs(angle), Is.GreaterThan(90f),
                "and the arrow points left, not right");
        }

        // 8b - the same case on the other side, so a sign flip cannot pass both
        [Test]
        public void BehindAndRightClampsToTheRightEdge()
        {
            Vector3 viewport = AtlasMath.Viewport(Viewer(), new Vector3(10f, 0f, -10f));
            Vector2 edge = AtlasMath.ClampToEdge(viewport, 0.05f, out float angle);

            Assert.Greater(edge.x, 0.5f);
            Assert.That(Mathf.Abs(angle), Is.LessThan(90f));
        }

        // 9
        [Test]
        public void ClampRespectsTheMarginAndStaysInsideTheViewport()
        {
            const float margin = 0.1f;

            var samples = new[]
            {
                new Vector3(5f, 5f, 10f),      // far off screen, in front
                new Vector3(-4f, 0.5f, 10f),
                new Vector3(0.5f, 0.5f, -5f),  // behind, dead centre
                new Vector3(2f, -3f, -5f),     // behind, off to a corner
            };

            foreach (Vector3 sample in samples)
            {
                Vector2 edge = AtlasMath.ClampToEdge(sample, margin, out _);

                Assert.That(edge.x, Is.InRange(margin - 0.001f, 1f - margin + 0.001f), $"x for {sample}");
                Assert.That(edge.y, Is.InRange(margin - 0.001f, 1f - margin + 0.001f), $"y for {sample}");
            }
        }

        // 9b
        [Test]
        public void AnOnScreenPointIsLeftWhereItIs()
        {
            var inside = new Vector3(0.6f, 0.45f, 10f);
            Vector2 result = AtlasMath.ClampToEdge(inside, 0.05f, out _);

            Assert.AreEqual(inside.x, result.x, 0.0001f);
            Assert.AreEqual(inside.y, result.y, 0.0001f);
        }

        [Test]
        public void FadeIsOneInsideTheLimitAndZeroBeyondIt()
        {
            Assert.AreEqual(1f, AtlasMath.Fade(10f, 0f), 0.0001f, "no limit means no fade");
            Assert.AreEqual(1f, AtlasMath.Fade(10f, 100f), 0.0001f);
            Assert.AreEqual(0f, AtlasMath.Fade(100f, 100f), 0.0001f);
            Assert.AreEqual(0f, AtlasMath.Fade(150f, 100f), 0.0001f);

            float halfway = AtlasMath.Fade(90f, 100f);
            Assert.That(halfway, Is.InRange(0.4f, 0.6f), "linear across the last fifth");
        }
    }
}
