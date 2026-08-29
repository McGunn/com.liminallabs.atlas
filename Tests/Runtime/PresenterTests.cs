using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// §7.4 - the milestone gate. These need a scene, because a presenter is a
    /// MonoBehaviour drawing uGUI, so they run in Unity's Test Runner rather than in the
    /// standalone pass that covers §7.1 to §7.3.
    ///
    /// Test 20 is the milestone: one registration, both views, in one frame, with
    /// neither presenter assembly referencing the other.
    /// </summary>
    public class PresenterTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private sealed class Fake : IAtlasTrackable
        {
            public Vector3 At;
            public AtlasMarker Mark = AtlasMarker.Point("fake");

            public Vector3 Position => At;
            public AtlasMarker Marker => Mark;
            public AtlasSpaceId Space => AtlasSpaceId.Default;
            public bool IsTracked => true;
        }

        private static AtlasViewer Viewer() =>
            new AtlasViewer(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right,
                60f, 16f / 9f, ViewProjection(), AtlasSpaceId.Default);

        private static Matrix4x4 ViewProjection()
        {
            const float near = 0.3f, far = 1000f, fov = 60f, aspect = 16f / 9f;
            float f = 1f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            var projection = new Matrix4x4();
            projection.m00 = f / aspect;
            projection.m11 = f;
            projection.m22 = -(far + near) / (far - near);
            projection.m23 = -(2f * far * near) / (far - near);
            projection.m32 = -1f;

            // At the origin looking down +Z, with Unity's camera facing -Z.
            var view = Matrix4x4.identity;
            view.m22 = -1f;

            return projection * view;
        }

        private T Spawn<T>(float width = 800f, float height = 200f) where T : Component
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            spawned.Add(go);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            return go.AddComponent<T>();
        }

        // 20 - the milestone
        [Test]
        public void OneRegistrationDrivesBothPresentersInOneFrame()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.AddProjection(new ScreenProjection(), screen);

            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });
            registry.Tick(Viewer());

            Assert.AreEqual(1, bar.VisibleCount, "registered once, on the compass");
            Assert.AreEqual(1, screen.VisibleCount, "and on screen, from that same registration");

            // Neither assembly may name the other. Asserted here as well as in the verify
            // loop, because an asmdef reference added in a hurry is invisible in review.
            foreach (System.Reflection.AssemblyName reference in
                     typeof(BarPresenter).Assembly.GetReferencedAssemblies())
                Assert.AreNotEqual("LiminalLabs.Atlas.Screen", reference.Name);

            foreach (System.Reflection.AssemblyName reference in
                     typeof(ScreenPresenter).Assembly.GetReferencedAssemblies())
                Assert.AreNotEqual("LiminalLabs.Atlas.Compass", reference.Name);
        }

        // 21
        [Test]
        public void TheBarHidesRatherThanClampsOutsideItsFieldOfView()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            bar.BarFieldOfView = 90f;

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });   // dead ahead
            registry.Register(new Fake { At = new Vector3(20f, 0f, 0f) });   // 90 degrees right
            registry.Tick(Viewer());

            Assert.AreEqual(1, bar.VisibleCount,
                "a clamped marker piles up at the end of the bar and reads as a target that is there");
        }

        // 22
        [Test]
        public void BarPositionIsLinearInBearing()
        {
            BarPresenter bar = Spawn<BarPresenter>(800f, 100f);
            bar.BarFieldOfView = 180f;

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            var ahead = new Fake { At = new Vector3(0f, 0f, 20f) };
            var right45 = new Fake { At = new Vector3(20f, 0f, 20f) };
            var right90 = new Fake { At = new Vector3(20f, 0f, 0f) };

            // Priority fixes the order they arrive in, so the assertions can name slots.
            ahead.Mark.Priority = 3f;
            right45.Mark.Priority = 2f;
            right90.Mark.Priority = 1f;

            registry.Register(ahead);
            registry.Register(right45);
            registry.Register(right90);
            registry.Tick(Viewer());

            float centre = bar.VisiblePosition(0).x;
            float at45 = bar.VisiblePosition(1).x;
            float at90 = bar.VisiblePosition(2).x;

            Assert.AreEqual(0f, centre, 0.5f);
            Assert.AreEqual(100f, at45, 1f, "45 of a 90 degree half-FOV, across half of 800");
            Assert.AreEqual(200f, at90, 1f);
            Assert.AreEqual(at45 - centre, at90 - at45, 1f, "equal bearing steps, equal pixel steps");
        }

        // 23
        [Test]
        public void PresentInstantiatesNothing()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.AddProjection(new ScreenProjection(), screen);

            for (int i = 0; i < 8; i++)
                registry.Register(new Fake { At = new Vector3(i - 4f, 0f, 20f) });

            registry.Tick(Viewer());
            int afterFirstFrame = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;

            for (int i = 0; i < 10; i++) registry.Tick(Viewer());

            Assert.AreEqual(afterFirstFrame,
                Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,
                "the pool is built at Awake; Present only moves and hides what is already there");
        }

        [Test]
        public void AnOffScreenTargetPinsToTheCorrectEdge()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new ScreenProjection(), screen);

            registry.Register(new Fake { At = new Vector3(-40f, 0f, -20f) });   // behind and left
            registry.Tick(Viewer());

            Assert.AreEqual(1, screen.VisibleCount);
            Assert.Less(screen.VisiblePosition(0).x, 0f,
                "pinned to the left half rather than mirrored to the right");
        }
    }
}
