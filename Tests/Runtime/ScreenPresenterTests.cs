using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// The on-screen half of §7.4. Needs a scene, so it runs in Unity's Test Runner.
    /// </summary>
    public class ScreenPresenterTests
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

        [Test]
        public void OneRegistrationReachesTheScreen()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new ScreenProjection(), screen);
            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });
            registry.Tick(Viewer());

            Assert.AreEqual(1, screen.VisibleCount);

            foreach (System.Reflection.AssemblyName reference in
                     typeof(ScreenPresenter).Assembly.GetReferencedAssemblies())
                Assert.AreNotEqual("LiminalLabs.Atlas.Compass", reference.Name,
                    "on-screen indicators must never learn that a compass exists");
        }

        /// <summary>
        /// The case this package exists to get right. Behind and to the left projects to
        /// the RIGHT of the screen, mirrored by the perspective divide, and an indicator
        /// that clamps the raw value pins to the wrong edge with its arrow backwards.
        /// </summary>
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

        [Test]
        public void AnOnScreenTargetSitsWhereItIs()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new ScreenProjection(), screen);
            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });   // dead ahead
            registry.Tick(Viewer());

            Assert.AreEqual(0f, screen.VisiblePosition(0).x, 2f, "centre of the screen");
        }

        [Test]
        public void PresentInstantiatesNothing()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
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
    }
}
