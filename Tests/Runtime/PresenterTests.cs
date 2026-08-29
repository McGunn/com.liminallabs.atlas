using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// §7.4 — the milestone gate.
    ///
    /// These need a scene, because a presenter is a MonoBehaviour drawing uGUI, so they
    /// run in Unity's Test Runner rather than in the standalone pass that covers §7.1
    /// to §7.3.
    ///
    /// <b>Test 20 is the milestone</b>, and it is the reason the views share a package
    /// rather than sitting in separate ones: it can only be written somewhere that sees
    /// both presenters. One registration, both views, one frame, with neither presenter
    /// assembly naming the other.
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

        /// <summary>
        /// A viewer at the origin looking down +Z, built by hand.
        ///
        /// <c>Matrix4x4.Perspective</c> is native-backed, and building the matrix here
        /// keeps these tests identical to the ones that run with no engine at all.
        /// </summary>
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

            var view = Matrix4x4.identity;
            view.m22 = -1f;   // Unity's camera looks down -Z

            return projection * view;
        }

        /// <summary>
        /// Spawns a presenter with self-registration off.
        ///
        /// The tests wire projections explicitly so each asserts one known pairing.
        /// Self-registration is what a scene gets; a test that relied on it would be
        /// asserting the resolver's scene search as well as the presenter.
        /// </summary>
        private T Spawn<T>(float width = 800f, float height = 200f) where T : Component
        {
            // Created inactive so OnEnable has not run yet, self-registration switched off,
            // then activated. No UnityEditor anywhere: a runtime test assembly that
            // referenced it would not survive a player test build.
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.SetActive(false);
            spawned.Add(go);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            T component = go.AddComponent<T>();

            if (component is BarPresenter bar) bar.SelfRegister = false;
            else if (component is ScreenPresenter screen) screen.SelfRegister = false;

            go.SetActive(true);
            return component;
        }

        // ---- 20: the milestone ----------------------------------------------

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
        }

        /// <summary>
        /// The half of test 20 that is about structure rather than behaviour.
        ///
        /// Asserted by reflecting over the built assemblies rather than by reading the
        /// asmdefs, because an asmdef reference added in a hurry is invisible in review
        /// and would not fail anything else. The moment one view can name the other,
        /// take-only-what-you-use is gone and they can quietly diverge on what "behind"
        /// means.
        /// </summary>
        [Test]
        public void NeitherViewAssemblyNamesTheOther()
        {
            foreach (AssemblyName reference in typeof(BarPresenter).Assembly.GetReferencedAssemblies())
                Assert.AreNotEqual("LiminalLabs.Atlas.Screen", reference.Name,
                    "the compass must never learn that on-screen indicators exist");

            foreach (AssemblyName reference in typeof(ScreenPresenter).Assembly.GetReferencedAssemblies())
                Assert.AreNotEqual("LiminalLabs.Atlas.Compass", reference.Name,
                    "on-screen indicators must never learn that a compass exists");
        }

        /// <summary>
        /// The behind-the-viewer case, end to end, through both views at once.
        ///
        /// This is the sentence the milestone is written in: the bar marker leaves the
        /// correct end while the screen indicator clamps to the correct edge. Test 8
        /// proves the maths; this proves the two views agree about it, which is the whole
        /// reason they share a solve.
        /// </summary>
        [Test]
        public void BehindTheViewerBothViewsAgree()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            bar.BarFieldOfView = 180f;

            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.AddProjection(new ScreenProjection(), screen);

            // Behind and to the left. The raw projection mirrors this to the right.
            registry.Register(new Fake { At = new Vector3(-40f, 0f, -20f) });
            registry.Tick(Viewer());

            Assert.AreEqual(0, bar.VisibleCount,
                "outside a 180 degree bar, so hidden rather than piled at an end");

            Assert.AreEqual(1, screen.VisibleCount);
            Assert.Less(screen.VisiblePosition(0).x, 0f,
                "pinned to the LEFT half - mirrored through the centre before clamping");
        }

        // ---- 21, 22: the compass ---------------------------------------------

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

        // ---- 23, and the screen half ------------------------------------------

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
        public void AnOnScreenTargetSitsWhereItIs()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new ScreenProjection(), screen);
            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });   // dead ahead
            registry.Tick(Viewer());

            Assert.AreEqual(0f, screen.VisiblePosition(0).x, 2f, "centre of the screen");
        }

        // ---- registration hygiene ---------------------------------------------

        /// <summary>
        /// A presenter registered twice would be handed two solve lists a frame, and the
        /// second would overwrite the first's pool state - which shows up as half the
        /// markers flickering, and is not a symptom anyone traces back to a duplicate.
        /// </summary>
        [Test]
        public void APresenterCannotBeRegisteredTwice()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            var registry = new AtlasRegistry();

            Assert.IsTrue(registry.AddProjection(new BearingProjection(), bar));
            Assert.IsFalse(registry.AddProjection(new BearingProjection(), bar));
            Assert.AreEqual(1, registry.ProjectionCount);
        }

        /// <summary>
        /// The scene story: drop a registry and a presenter in, and it works.
        ///
        /// Everything else here wires projections by hand so each test asserts one known
        /// pairing. This one asserts the thing a person actually does.
        /// </summary>
        [Test]
        public void APresenterWiresItselfToTheRegistryInTheScene()
        {
            var host = new GameObject("Atlas Registry");
            spawned.Add(host);
            AtlasRegistryBehaviour registry = host.AddComponent<AtlasRegistryBehaviour>();

            var barObject = new GameObject("Bar", typeof(RectTransform));
            spawned.Add(barObject);
            barObject.transform.SetParent(host.transform, false);
            ((RectTransform)barObject.transform).sizeDelta = new Vector2(800f, 100f);

            BarPresenter bar = barObject.AddComponent<BarPresenter>();

            Assert.AreEqual(1, registry.Registry.ProjectionCount,
                "dropping the component in is the whole of the wiring");

            bar.enabled = false;
            Assert.AreEqual(0, registry.Registry.ProjectionCount,
                "and disabling it takes the projection back out");
        }
    }
}
