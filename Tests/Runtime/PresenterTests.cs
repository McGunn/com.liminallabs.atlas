using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using LiminalLabs.Core;
using UnityEngine;
using UnityEngine.UI;

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

            foreach (Object o in temporary)
                if (o != null) Object.DestroyImmediate(o);
            temporary.Clear();
        }

        private readonly List<Object> temporary = new List<Object>();

        /// <summary>An icon provider that answers every id with one sprite.</summary>
        private sealed class StubIcons : IAtlasIconProvider
        {
            public Sprite Sprite;
            public Sprite Resolve(int iconId) => Sprite;
        }

        /// <summary>A real Sprite with no asset behind it, so the normal draw path can be
        /// exercised without shipping a texture in the test assembly.</summary>
        private Sprite MakeSprite()
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            temporary.Add(texture);
            temporary.Add(sprite);
            return sprite;
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

        /// <summary>
        /// Images that would actually reach the renderer: on an active object, enabled,
        /// and not fully transparent.
        ///
        /// Read through the hierarchy rather than through a presenter API, because what
        /// matters is what uGUI draws, not what the presenter reports about itself. That
        /// distinction is the whole point of the two tests below.
        /// </summary>
        private static int DrawnImages(Component presenter)
        {
            int drawn = 0;
            foreach (Image image in presenter.GetComponentsInChildren<Image>(false))
                if (image.enabled && image.color.a > 0f) drawn++;
            return drawn;
        }

        // ---- the blank-marker contract --------------------------------------

        /// <summary>
        /// A marker with no icon still draws.
        ///
        /// This is the regression that hid behind <c>VisibleCount</c>. The pooled object
        /// was active, so the count reported one visible marker, while the Image on it was
        /// disabled and the frame was empty. Every presenter test passed and both views
        /// rendered nothing - which is indistinguishable from a registry that is not
        /// ticking, a marker that never registered, or a camera facing the wrong way.
        ///
        /// <see cref="IAtlasIconProvider"/> promises a missing icon costs a blank marker
        /// rather than a broken frame, and a scene with no icon list assigned is how most
        /// people first see this package.
        /// </summary>
        [Test]
        public void AMarkerWithNoIconStillDraws()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.AddProjection(new ScreenProjection(), screen);

            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });
            registry.Tick(Viewer());

            Assert.AreEqual(1, DrawnImages(bar),
                "the compass marker has to be rendered, not merely active");
            Assert.AreEqual(1, DrawnImages(screen),
                "and so does the on-screen indicator");
        }

        /// <summary>
        /// The marker's tint reaches the thing that draws it.
        ///
        /// Without an icon the tint is the only way one marker is told from another, so a
        /// blank marker that ignored it would be a blank marker in name only.
        /// </summary>
        [Test]
        public void TheMarkerTintReachesTheImage()
        {
            BarPresenter bar = Spawn<BarPresenter>();

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            // With an icon assigned: the tint belongs to the marker. Without one the
            // placeholder takes over and deliberately ignores it, which the next test
            // covers - so this one has to supply an icon to be about tinting at all.
            bar.IconProvider = new StubIcons { Sprite = MakeSprite() };

            var fake = new Fake { At = new Vector3(0f, 0f, 20f) };
            fake.Mark.Tint = Color.magenta;
            registry.Register(fake);
            registry.Tick(Viewer());

            foreach (Image image in bar.GetComponentsInChildren<Image>(false))
            {
                if (!image.enabled) continue;
                Assert.AreEqual(Color.magenta.r, image.color.r, 0.001f);
                Assert.AreEqual(Color.magenta.g, image.color.g, 0.001f);
                Assert.AreEqual(Color.magenta.b, image.color.b, 0.001f);
                return;
            }

            Assert.Fail("no drawn image to check the tint on");
        }

        /// <summary>
        /// A marker with no icon gets core's placeholder, in its own colour.
        ///
        /// The colour is the point. A placeholder tinted cyan because the marker is cyan
        /// reads as a deliberate icon, which is the one thing a placeholder must never do
        /// - so the marker's tint is dropped and only its fade is kept.
        ///
        /// Written to hold in a release build too, where <see cref="LiminalPlaceholder"/>
        /// returns null on purpose and the blank quad is the correct answer.
        /// </summary>
        [Test]
        public void AMarkerWithNoIconGetsThePlaceholder()
        {
            BarPresenter bar = Spawn<BarPresenter>();

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            var fake = new Fake { At = new Vector3(0f, 0f, 20f) };
            fake.Mark.Tint = Color.cyan;
            registry.Register(fake);
            registry.Tick(Viewer());

            Sprite placeholder = LiminalPlaceholder.Missing;

            foreach (Image image in bar.GetComponentsInChildren<Image>(false))
            {
                if (!image.enabled) continue;

                if (placeholder != null)
                {
                    Assert.AreSame(placeholder, image.sprite, "an unassigned icon announces itself");
                    Assert.AreEqual(LiminalPlaceholder.Tint.r, image.color.r, 0.001f,
                        "and does so in its own colour, not the marker's");
                }
                else
                {
                    Assert.IsNull(image.sprite, "release builds fall back to the blank quad");
                }
                return;
            }

            Assert.Fail("nothing was drawn");
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
                "far outside a 180 degree bar, so released rather than piled at an end");

            Assert.AreEqual(1, screen.VisibleCount);
            Assert.Less(screen.VisiblePosition(0).x, 960f,
                "pinned to the LEFT half - mirrored through the centre before clamping");
        }

        // ---- 21, 22: the compass ---------------------------------------------

        [Test]
        public void TheBarReleasesMarkersOnlyOnceTheyAreFullyPastTheEnd()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            bar.BarFieldOfView = 90f;

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });   // dead ahead
            registry.Register(new Fake { At = new Vector3(20f, 0f, 0f) });   // 90 degrees right
            registry.Tick(Viewer());

            Assert.AreEqual(1, bar.VisibleCount,
                "90 degrees on a 90 degree bar is a full bar-width past the end, so the " +
                "second marker is gone rather than piled up at the edge pretending to be there");
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

            // Anchored to the area's bottom-left, so the centre of a 1920x1080 area is
            // (960, 540). This previously asserted (0, ...) and passed, which is the
            // corner - the test was written against the arithmetic instead of against
            // where the icon has to appear, so it locked the defect in rather than
            // catching it.
            Assert.AreEqual(960f, screen.VisiblePosition(0).x, 2f, "horizontal centre");
            Assert.AreEqual(540f, screen.VisiblePosition(0).y, 2f, "vertical centre");
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

        // ---- slide-off, cardinal letters, overrides -------------------------

        /// <summary>
        /// A marker just past the bar's field of view is still drawn, and slides.
        ///
        /// The previous behaviour hid it the instant it crossed half the FOV, which pops
        /// - a marker vanishing a pixel before the edge reads as a bug rather than as a
        /// boundary. It keeps its slot until it is fully past the edge and is clipped by
        /// the mask on the way out.
        /// </summary>
        [Test]
        public void AMarkerJustPastTheEdgeStillDrawsAndSlides()
        {
            BarPresenter bar = Spawn<BarPresenter>(800f, 200f);
            bar.BarFieldOfView = 90f;   // half is 45 degrees across 800 units

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            // 46 degrees right. Past the 45-degree half FOV, so the old code hid it while
            // it was still entirely on screen. It slides out over its own width instead:
            // 45 degrees puts its centre on the bar's edge, and it is fully clipped half a
            // marker later.
            var fake = new Fake { At = new Vector3(Mathf.Sin(46f * Mathf.Deg2Rad), 0f,
                                                   Mathf.Cos(46f * Mathf.Deg2Rad)) * 20f };
            registry.Register(fake);
            registry.Tick(Viewer());

            Assert.AreEqual(1, bar.VisibleCount, "still drawn past the field of view");
            Assert.Greater(bar.VisiblePosition(0).x, 400f,
                "and sitting over the bar's edge, where the mask clips it");
        }

        /// <summary>Fully past the edge, the slot is released - otherwise a pool of 32
        /// would be spent on markers behind you.</summary>
        [Test]
        public void AMarkerFullyPastTheEdgeReleasesItsSlot()
        {
            BarPresenter bar = Spawn<BarPresenter>(800f, 200f);
            bar.BarFieldOfView = 90f;

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            registry.Register(new Fake { At = new Vector3(-20f, 0f, -1f) });   // ~behind left
            registry.Tick(Viewer());

            Assert.AreEqual(0, bar.VisibleCount);
        }

        /// <summary>
        /// Facing north, the N sits dead centre.
        ///
        /// The letters are the half of a compass that has no marker behind it, so nothing
        /// else in the suite would catch them being mirrored - and mirrored is exactly how
        /// a compass gets built wrong, because it looks plausible until you turn around.
        /// </summary>
        [Test]
        public void TheNorthLetterSitsAtTheCentreWhenFacingNorth()
        {
            BarPresenter bar = Spawn<BarPresenter>(800f, 200f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.Tick(Viewer());

            RectTransform north = FindDirection(bar, "N");
            Assert.IsNotNull(north, "the cardinal letters are built with the pool");
            Assert.AreEqual(0f, north.anchoredPosition.x, 1f);
        }

        /// <summary>Turning right slides the letters left, by the same mapping the
        /// markers use. If these two ever disagreed the bar would read as drifting.</summary>
        [Test]
        public void TheLettersUseTheSameMappingAsTheMarkers()
        {
            BarPresenter bar = Spawn<BarPresenter>(800f, 200f);
            bar.BarFieldOfView = 180f;

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);
            registry.Tick(Viewer());

            RectTransform east = FindDirection(bar, "E");
            Assert.IsNotNull(east);
            Assert.AreEqual(bar.XForBearing(90f), east.anchoredPosition.x, 1f);
        }

        /// <summary>
        /// A marker's own sprite wins over the provider.
        ///
        /// The id and its array are right for a fixed icon set and wrong for the handful
        /// of genuinely one-off markers, which would otherwise each need a permanent slot
        /// in an array whose order is a contract with save data.
        /// </summary>
        [Test]
        public void AMarkerIconOverrideBeatsTheProvider()
        {
            BarPresenter bar = Spawn<BarPresenter>();
            Sprite fromProvider = MakeSprite();
            Sprite own = MakeSprite();
            bar.IconProvider = new StubIcons { Sprite = fromProvider };

            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), bar);

            var fake = new Fake { At = new Vector3(0f, 0f, 20f) };
            fake.Mark.IconOverride = own;
            registry.Register(fake);
            registry.Tick(Viewer());

            foreach (Image image in bar.GetComponentsInChildren<Image>(false))
            {
                if (!image.enabled) continue;
                Assert.AreSame(own, image.sprite);
                return;
            }

            Assert.Fail("nothing was drawn");
        }

        private static RectTransform FindDirection(Component presenter, string label)
        {
            foreach (RectTransform rect in presenter.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Direction " + label) return rect;
            return null;
        }


        /// <summary>
        /// An on-screen target lands inside the area it is drawn in.
        ///
        /// This is the assertion that was missing, and its absence let every indicator sit
        /// half a screen down and to the left for as long as the component existed. The
        /// position was built from <c>rect.xMin</c>, which is measured from the pivot,
        /// while the pool anchors to the corner - so the two tests that did exist agreed
        /// with the arithmetic and disagreed with the screen.
        ///
        /// A bound rather than an exact point on purpose: an exact expected value is what
        /// a test written from the implementation looks like, and it is how this was missed.
        /// </summary>
        [Test]
        public void EveryOnScreenIndicatorLandsInsideTheArea()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new ScreenProjection(), screen);

            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });      // ahead
            registry.Register(new Fake { At = new Vector3(4f, 0f, 20f) });      // right of centre
            registry.Register(new Fake { At = new Vector3(-4f, 2f, 20f) });     // left and up
            registry.Tick(Viewer());

            Assert.AreEqual(3, screen.VisibleCount);

            for (int i = 0; i < 3; i++)
            {
                Vector2 at = screen.VisiblePosition(i);
                Assert.GreaterOrEqual(at.x, 0f, "indicator " + i + " is left of the area");
                Assert.LessOrEqual(at.x, 1920f, "indicator " + i + " is right of the area");
                Assert.GreaterOrEqual(at.y, 0f, "indicator " + i + " is below the area");
                Assert.LessOrEqual(at.y, 1080f, "indicator " + i + " is above the area");
            }
        }

        /// <summary>
        /// Right of centre draws right of centre, and up draws up.
        ///
        /// The sign check the position maths never had. Getting it mirrored is the classic
        /// indicator bug and looks entirely plausible until you compare two markers.
        /// </summary>
        [Test]
        public void ScreenIndicatorsKeepTheirDirection()
        {
            ScreenPresenter screen = Spawn<ScreenPresenter>(1920f, 1080f);

            var registry = new AtlasRegistry();
            registry.AddProjection(new ScreenProjection(), screen);

            registry.Register(new Fake { At = new Vector3(-4f, 0f, 20f) });     // left
            registry.Register(new Fake { At = new Vector3(4f, 3f, 20f) });      // right and up
            registry.Tick(Viewer());

            Vector2 a = screen.VisiblePosition(0);
            Vector2 b = screen.VisiblePosition(1);

            Assert.Less(a.x, 960f, "a target to the left draws left of centre");
            Assert.Greater(b.x, 960f, "a target to the right draws right of centre");
            Assert.Greater(b.y, 540f, "a target above draws above centre");
        }

    }
}
