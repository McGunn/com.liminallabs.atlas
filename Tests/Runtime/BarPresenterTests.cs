using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// The compass half of §7.4. Needs a scene, so it runs in Unity's Test Runner.
    ///
    /// Test 21 is the one with an opinion in it: a marker outside the bar's field of view
    /// is hidden, not clamped. A clamped marker piles up at the end of the strip and reads
    /// as "there is something exactly there", which is a lie.
    /// </summary>
    public class BarPresenterTests
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

        /// <summary>
        /// The half of the old test 20 this package can still assert on its own.
        ///
        /// The other half - that Compass and On-Screen do not reference each other - used
        /// to need a reflection check over both assemblies. Separate packages make it
        /// structural instead: this package cannot name the other one, because it does not
        /// depend on it.
        /// </summary>
        [Test]
        public void OneRegistrationReachesTheBar()
        {
            BarPresenter bar = Spawn<BarPresenter>();

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });
            registry.Tick(Viewer());

            Assert.AreEqual(1, bar.VisibleCount);

            foreach (System.Reflection.AssemblyName reference in
                     typeof(BarPresenter).Assembly.GetReferencedAssemblies())
                Assert.AreNotEqual("LiminalLabs.Atlas.OnScreen", reference.Name,
                    "the compass must never learn that on-screen indicators exist");
        }

        [Test]
        public void PresentInstantiatesNothing()
        {
            BarPresenter bar = Spawn<BarPresenter>();

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            for (int i = 0; i < 8; i++)
                registry.Register(new Fake { At = new Vector3(i - 4f, 0f, 20f) });

            registry.Tick(Viewer());
            int afterFirstFrame = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;

            for (int i = 0; i < 10; i++) registry.Tick(Viewer());

            Assert.AreEqual(afterFirstFrame,
                Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,
                "the pool is built at Awake; Present only moves and hides what is already there");
        }
    }
}
