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

        // ---- cardinal directions, for the compass letters -------------------

        /// <summary>
        /// A direction has no position, so it cannot be solved as a marker - and faking
        /// one by picking a point far to the north is wrong near the world origin and
        /// needless everywhere else.
        /// </summary>
        [Test]
        public void NorthIsDeadAheadWhenFacingNorth()
        {
            AtlasViewer viewer = Viewer(yaw: 0f);
            Assert.AreEqual(0f, AtlasMath.BearingOfDirection(viewer, Vector3.forward), 0.01f);
        }

        [Test]
        public void CardinalDirectionsSitWhereTheyShouldWhenFacingNorth()
        {
            AtlasViewer viewer = Viewer(yaw: 0f);

            Assert.AreEqual(0f, AtlasMath.BearingOfDirection(viewer, Vector3.forward), 0.01f, "N");
            Assert.AreEqual(90f, AtlasMath.BearingOfDirection(viewer, Vector3.right), 0.01f, "E is right");
            Assert.AreEqual(-90f, AtlasMath.BearingOfDirection(viewer, Vector3.left), 0.01f, "W is left");
            Assert.AreEqual(180f, Mathf.Abs(AtlasMath.BearingOfDirection(viewer, Vector3.back)), 0.01f, "S is behind");
        }

        /// <summary>
        /// Turning right moves the letters left, by the amount turned. The sign is the
        /// whole thing a compass gets wrong, and it is wrong in a way that looks fine
        /// standing still.
        /// </summary>
        [Test]
        public void TurningRightSlidesTheLettersLeft()
        {
            AtlasViewer viewer = Viewer(yaw: 30f);
            Assert.AreEqual(-30f, AtlasMath.BearingOfDirection(viewer, Vector3.forward), 0.01f);
        }

        /// <summary>
        /// Looking straight up flattens Forward to nothing. A hand-rolled compass spins
        /// wildly here; the letters have to stay put, since the direction you are facing
        /// on the ground has not changed.
        /// </summary>
        [Test]
        public void LookingStraightUpDoesNotSpinTheLetters()
        {
            AtlasViewer viewer = Viewer(yaw: 45f, pitch: 89.999f);
            float bearing = AtlasMath.BearingOfDirection(viewer, Vector3.forward);
            Assert.AreEqual(-45f, bearing, 1f);
        }

        // ---- idle activity ---------------------------------------------------

        [Test]
        public void AStillViewerIsNotBusy()
        {
            AtlasViewer viewer = Viewer();
            Assert.AreEqual(0f, AtlasMath.Activity(viewer, viewer, 0.016f), 0.0001f);
        }

        /// <summary>Turning counts as much as walking: a player sweeping the camera is
        /// looking for something, which is when a compass earns its space.</summary>
        [Test]
        public void TurningCountsAsActivity()
        {
            AtlasViewer before = Viewer(yaw: 0f);
            AtlasViewer after = Viewer(yaw: 20f);
            Assert.AreEqual(1f, AtlasMath.Activity(before, after, 0.016f), 0.0001f);
        }

        [Test]
        public void WalkingCountsAsActivity()
        {
            AtlasViewer before = Viewer(Vector3.zero);
            AtlasViewer after = Viewer(new Vector3(0f, 0f, 0.1f));
            Assert.Greater(AtlasMath.Activity(before, after, 0.016f), 0.9f);
        }

        /// <summary>A zero delta time is a paused frame, not an infinitely fast one.</summary>
        [Test]
        public void AZeroFrameIsNotInfiniteActivity()
        {
            AtlasViewer before = Viewer(Vector3.zero);
            AtlasViewer after = Viewer(new Vector3(0f, 0f, 100f));
            Assert.AreEqual(0f, AtlasMath.Activity(before, after, 0f), 0.0001f);
        }

        [Test]
        public void DistanceScaleInterpolatesBetweenTheAuthoredSizes()
        {
            Assert.AreEqual(0.8f, AtlasMath.DistanceScale(0f, 0.8f, 1.2f), 0.0001f);
            Assert.AreEqual(1.2f, AtlasMath.DistanceScale(1f, 0.8f, 1.2f), 0.0001f);
            Assert.AreEqual(1.0f, AtlasMath.DistanceScale(0.5f, 0.8f, 1.2f), 0.0001f);
        }


        // ---- the map plane (M1) ----------------------------------------------

        private static AtlasMapFrame Frame(float radius = 50f, float rotation = 0f) =>
            new AtlasMapFrame(Vector2.zero, radius, rotation);

        [Test]
        public void TheFrameCentreIsTheMiddleOfTheMap()
        {
            Vector2 at = AtlasMath.MapPoint(Frame(), Vector2.zero);
            Assert.AreEqual(0.5f, at.x, 0.0001f);
            Assert.AreEqual(0.5f, at.y, 0.0001f);
        }

        /// <summary>
        /// The frame's radius is a half-span: a radius of 50 shows 100 across, so a point
        /// 50 north of the centre is at the top edge rather than halfway to it.
        /// </summary>
        [Test]
        public void TheRadiusIsAHalfSpan()
        {
            Vector2 at = AtlasMath.MapPoint(Frame(50f), new Vector2(0f, 50f));
            Assert.AreEqual(0.5f, at.x, 0.0001f);
            Assert.AreEqual(1f, at.y, 0.0001f, "50 units on a radius of 50 is the top edge");
        }

        [Test]
        public void MapDirectionsAreNotMirrored()
        {
            AtlasMapFrame frame = Frame(50f);

            Assert.Greater(AtlasMath.MapPoint(frame, new Vector2(10f, 0f)).x, 0.5f, "east is right");
            Assert.Less(AtlasMath.MapPoint(frame, new Vector2(-10f, 0f)).x, 0.5f, "west is left");
            Assert.Greater(AtlasMath.MapPoint(frame, new Vector2(0f, 10f)).y, 0.5f, "north is up");
            Assert.Less(AtlasMath.MapPoint(frame, new Vector2(0f, -10f)).y, 0.5f, "south is down");
        }

        /// <summary>
        /// Rotating the frame by 90 degrees moves a point that was north to the west of
        /// the map. This is the sign that makes a viewer-up minimap turn the right way,
        /// and getting it backwards reads as inverted controls rather than as a bug.
        /// </summary>
        [Test]
        public void RotatingTheFrameTurnsTheMapCounterClockwise()
        {
            Vector2 at = AtlasMath.MapPoint(Frame(50f, 90f), new Vector2(0f, 25f));

            Assert.Less(at.x, 0.5f, "a point to the north swings to the left");
            Assert.AreEqual(0.5f, at.y, 0.0001f);
        }

        [Test]
        public void RotateMapIsAPlainCounterClockwiseTurn()
        {
            Vector2 turned = AtlasMath.RotateMap(new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0f, turned.x, 0.0001f);
            Assert.AreEqual(1f, turned.y, 0.0001f);
        }

        [Test]
        public void TheRadiusFractionIsOneAtTheEdge()
        {
            Assert.AreEqual(1f, AtlasMath.MapRadiusFraction(Frame(50f), new Vector2(50f, 0f)), 0.0001f);
            Assert.AreEqual(2f, AtlasMath.MapRadiusFraction(Frame(50f), new Vector2(0f, 100f)), 0.0001f);
            Assert.AreEqual(0f, AtlasMath.MapRadiusFraction(Frame(50f), Vector2.zero), 0.0001f);
        }

        /// <summary>
        /// A round map clamps to a circle. Clamping it to the rectangle instead is what
        /// makes markers bunch at the diagonals of a map that is visibly round - it looks
        /// like a spacing bug rather than the wrong shape.
        /// </summary>
        [Test]
        public void ARoundMapPinsToItsCircleNotItsCorners()
        {
            // Diagonally out: both axes past the edge by the same amount.
            Vector2 pinned = AtlasMath.ClampToCircle(new Vector2(1.5f, 1.5f), 0f, out float angle);

            Assert.AreEqual(0.5f, (pinned - new Vector2(0.5f, 0.5f)).magnitude, 0.0001f,
                "on the circle, not in a corner");
            Assert.AreEqual(45f, angle, 0.01f, "and the arrow still points at it");
        }

        [Test]
        public void APointInsideTheCircleIsLeftAlone()
        {
            var inside = new Vector2(0.55f, 0.52f);
            Assert.AreEqual(inside, AtlasMath.ClampToCircle(inside, 0f, out _));
        }

        /// <summary>Dead centre has no direction to pin along, and a marker on top of the
        /// viewer is not off the edge anyway.</summary>
        [Test]
        public void AMarkerOnTopOfTheViewerStaysAtTheCentre()
        {
            Vector2 at = AtlasMath.ClampToCircle(new Vector2(0.5f, 0.5f), 0.1f, out float angle);
            Assert.AreEqual(0.5f, at.x, 0.0001f);
            Assert.AreEqual(0.5f, at.y, 0.0001f);
            Assert.AreEqual(0f, angle, 0.0001f);
        }

        /// <summary>A zero radius would divide by zero and put every marker at the centre,
        /// which reads as a broken map rather than as a bad number.</summary>
        [Test]
        public void AZeroRadiusFrameDoesNotCollapseTheMap()
        {
            var frame = new AtlasMapFrame(Vector2.zero, 0f);
            Assert.Greater(frame.Radius, 0f);
            Assert.IsFalse(float.IsNaN(AtlasMath.MapPoint(frame, new Vector2(1f, 1f)).x));
        }

    }
}
