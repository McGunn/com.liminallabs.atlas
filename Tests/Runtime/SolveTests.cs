using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// The registry's own contract: what it solves, how much of it, and for whom.
    ///
    /// Separate from the presenter tests because none of this needs a scene. The registry
    /// is plain objects over plain structs, so every assertion here runs in the build loop
    /// rather than waiting for someone to open Unity — which is the whole argument for
    /// keeping the solve free of engine types.
    /// </summary>
    public class SolveTests
    {
        private sealed class Fake : IAtlasTrackable
        {
            public Vector3 At;
            public AtlasMarker Mark = AtlasMarker.Point("fake");
            public AtlasSpaceId In = AtlasSpaceId.Default;
            public bool On = true;

            public Vector3 Position => At;
            public AtlasMarker Marker => Mark;
            public AtlasSpaceId Space => In;
            public bool IsTracked => On;
        }

        /// <summary>Captures the candidates a projection is handed, so the shared solve can
        /// be asserted directly rather than inferred from what two views happen to draw.</summary>
        private sealed class CaptureProjection : IAtlasProjection
        {
            public readonly List<AtlasCandidate> Last = new List<AtlasCandidate>();

            public void Solve(in AtlasViewer viewer, AtlasSpaceRegistry spaces,
                              IReadOnlyList<AtlasCandidate> candidates, List<AtlasSolve> into)
            {
                Last.Clear();
                for (int i = 0; i < candidates.Count; i++)
                {
                    Last.Add(candidates[i]);
                    into.Add(new AtlasSolve(candidates[i], default, true));
                }
            }
        }

        private sealed class CountingPresenter : IAtlasPresenter
        {
            public int LastCount;

            public void Present(in AtlasViewer viewer, IReadOnlyList<AtlasSolve> solves) =>
                LastCount = solves.Count;
        }

        private sealed class AlwaysOccluded : IAtlasOcclusion
        {
            public bool IsOccluded(IAtlasTrackable target, in AtlasViewer viewer) => true;
            public void Tick(in AtlasViewer viewer, IReadOnlyList<IAtlasTrackable> targets) { }
        }

        /// <summary>A viewer at the origin looking down +Z, built by hand so no engine is
        /// needed. Matrix4x4.Perspective is native-backed, which is why this is not it.</summary>
        private static AtlasViewer Viewer()
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
            view.m22 = -1f;

            return new AtlasViewer(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right,
                fov, aspect, projection * view, AtlasSpaceId.Default);
        }

        // ---- the shared solve --------------------------------------------------

        /// <summary>
        /// A projection is handed candidates that are already solved.
        ///
        /// This is the property that makes "one solve, several views" true about cost and
        /// not only about correctness. It used to be false: every projection read the
        /// target's position and marker again and recomputed distance, bearing and fade, so
        /// with three views the same square root ran three times per marker per frame. The
        /// views agreed because they ran identical arithmetic, not because they shared it.
        /// </summary>
        [Test]
        public void CandidatesArriveAlreadySolved()
        {
            var registry = new AtlasRegistry();
            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            registry.Register(new Fake { At = new Vector3(6f, 0f, 20f) });
            registry.Tick(Viewer());

            Assert.AreEqual(1, capture.Last.Count);
            AtlasCandidate candidate = capture.Last[0];

            Assert.AreEqual(Mathf.Sqrt(6f * 6f + 20f * 20f), candidate.Distance, 0.01f);
            Assert.Greater(candidate.Bearing, 0f, "to the right of the viewer");
            Assert.Greater(candidate.ViewportPoint.z, 0f, "and in front of it");
            Assert.AreEqual("fake", candidate.Marker.Label);
        }

        /// <summary>
        /// Cost follows what is drawn, not what is tracked.
        ///
        /// The registry ranks by priority before solving and only solves the top slice,
        /// because priority lives on the marker and owes nothing to the viewer. Without
        /// this, a strategy game tracking ten thousand units pays for ten thousand solves
        /// to draw thirty-two markers.
        /// </summary>
        [Test]
        public void OnlyTheTopSliceIsSolved()
        {
            var registry = new AtlasRegistry(new AtlasSettings { MaxMarkers = 4, CandidateSlack = 2 });

            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            for (int i = 0; i < 200; i++)
            {
                var fake = new Fake { At = new Vector3(0f, 0f, 20f + i * 0.01f) };
                fake.Mark.Priority = i;              // 199 is the most important
                registry.Register(fake);
            }

            registry.Tick(Viewer());

            Assert.AreEqual(8, capture.Last.Count, "four markers times a slack of two, not 200");
            Assert.AreEqual(199f, capture.Last[0].Marker.Priority, "and the important ones");
        }

        /// <summary>The slack exists so a heavily filtered view is not left short. Setting
        /// it to 1 is the "pay for exactly what you draw" end of that trade.</summary>
        [Test]
        public void SlackOfOneSolvesExactlyTheLimit()
        {
            var registry = new AtlasRegistry(new AtlasSettings { MaxMarkers = 3, CandidateSlack = 1 });

            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            for (int i = 0; i < 50; i++)
                registry.Register(new Fake { At = new Vector3(0f, 0f, 20f + i * 0.01f) });

            registry.Tick(Viewer());

            Assert.AreEqual(3, capture.Last.Count);
        }

        // ---- per-view limits ---------------------------------------------------

        /// <summary>A world map wanting 64 and a compass wanting 12 are both reasonable,
        /// and one shared number suits neither.</summary>
        [Test]
        public void EachViewGetsItsOwnLimit()
        {
            var registry = new AtlasRegistry(new AtlasSettings { MaxMarkers = 32, CandidateSlack = 4 });

            var few = new CountingPresenter();
            var many = new CountingPresenter();
            registry.AddProjection(new CaptureProjection(), few, maxMarkers: 2);
            registry.AddProjection(new CaptureProjection(), many, maxMarkers: 10);

            for (int i = 0; i < 40; i++)
                registry.Register(new Fake { At = new Vector3(0f, 0f, 20f + i * 0.01f) });

            registry.Tick(Viewer());

            Assert.AreEqual(2, few.LastCount, "the small view got two");
            Assert.AreEqual(10, many.LastCount, "and the large one got ten");
        }

        /// <summary>Zero means "use the shared setting", so existing wiring is unchanged by
        /// the option existing.</summary>
        [Test]
        public void ZeroMeansTheSharedLimit()
        {
            var registry = new AtlasRegistry(new AtlasSettings { MaxMarkers = 5, CandidateSlack = 4 });

            var presenter = new CountingPresenter();
            registry.AddProjection(new CaptureProjection(), presenter);

            for (int i = 0; i < 40; i++)
                registry.Register(new Fake { At = new Vector3(0f, 0f, 20f + i * 0.01f) });

            registry.Tick(Viewer());

            Assert.AreEqual(5, presenter.LastCount);
        }

        // ---- occlusion ---------------------------------------------------------

        /// <summary>
        /// The default matters more than the feature: a project with no occlusion provider
        /// must behave exactly as every system without occlusion does, which is
        /// "everything is visible".
        /// </summary>
        [Test]
        public void NothingIsOccludedUntilSomethingSaysSo()
        {
            var registry = new AtlasRegistry();
            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            registry.Register(new Fake { At = new Vector3(0f, 0f, 20f) });
            registry.Tick(Viewer());

            Assert.IsFalse(capture.Last[0].Occluded, "no provider means nothing is blocked");

            registry.Occlusion = new AlwaysOccluded();
            registry.Tick(Viewer());

            Assert.IsTrue(capture.Last[0].Occluded);
        }

        // ---- elevation ---------------------------------------------------------

        [Test]
        public void ElevationIsBandedNotCompared()
        {
            var registry = new AtlasRegistry(new AtlasSettings { ElevationBand = 2.5f });
            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            var above = new Fake { At = new Vector3(0f, 10f, 20f) };
            var below = new Fake { At = new Vector3(0f, -10f, 20f) };
            var kerb = new Fake { At = new Vector3(0f, 1f, 20f) };

            above.Mark.Priority = 3f;
            below.Mark.Priority = 2f;
            kerb.Mark.Priority = 1f;

            registry.Register(above);
            registry.Register(below);
            registry.Register(kerb);
            registry.Tick(Viewer());

            Assert.AreEqual(AtlasElevation.Above, capture.Last[0].Level);
            Assert.AreEqual(AtlasElevation.Below, capture.Last[1].Level);
            Assert.AreEqual(AtlasElevation.Level, capture.Last[2].Level,
                "a step up is not another floor");

            Assert.AreEqual(10f, capture.Last[0].Elevation, 0.01f);
            Assert.AreEqual(-10f, capture.Last[1].Elevation, 0.01f);
        }

        // ---- ordering ----------------------------------------------------------

        /// <summary>
        /// Candidates arrive priority-ordered, which is what lets the registry truncate to
        /// a view's limit without sorting a second time. A projection that reorders would
        /// silently break that, so the guarantee is asserted rather than assumed.
        /// </summary>
        [Test]
        public void CandidatesArriveOrderedByPriority()
        {
            var registry = new AtlasRegistry();
            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            for (int i = 0; i < 6; i++)
            {
                var fake = new Fake { At = new Vector3(0f, 0f, 20f) };
                fake.Mark.Priority = i;
                registry.Register(fake);
            }

            registry.Tick(Viewer());

            for (int i = 1; i < capture.Last.Count; i++)
            {
                Assert.GreaterOrEqual(capture.Last[i - 1].Marker.Priority,
                                      capture.Last[i].Marker.Priority);
            }
        }

        /// <summary>Equal priorities keep registration order, so a bar does not shimmer as
        /// markers swap places between frames.</summary>
        [Test]
        public void EqualPrioritiesKeepTheirOrder()
        {
            var registry = new AtlasRegistry();
            var capture = new CaptureProjection();
            registry.AddProjection(capture, new CountingPresenter());

            for (int i = 0; i < 5; i++)
            {
                var fake = new Fake { At = new Vector3(0f, 0f, 20f) };
                fake.Mark.Label = "marker " + i;
                registry.Register(fake);
            }

            registry.Tick(Viewer());

            for (int i = 0; i < 5; i++)
                Assert.AreEqual("marker " + i, capture.Last[i].Marker.Label);
        }
    }
}
