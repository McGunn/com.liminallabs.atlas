using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>§7.2. Still no scene - a registry is a plain object by design.</summary>
    public class RegistryTests
    {
        /// <summary>A trackable with no GameObject behind it, which is entry point 2.</summary>
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

        /// <summary>Captures what it was handed, so a test can assert on the solve list
        /// the registry actually produced.</summary>
        private sealed class Capture : IAtlasPresenter
        {
            public readonly List<AtlasSolve> Last = new List<AtlasSolve>();
            public int Presented;

            public void Present(IReadOnlyList<AtlasSolve> solves)
            {
                Presented++;
                Last.Clear();
                for (int i = 0; i < solves.Count; i++) Last.Add(solves[i]);
            }
        }

        private static AtlasViewer Viewer(AtlasSpaceId space = default) =>
            new AtlasViewer(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right,
                60f, 16f / 9f, Matrix4x4.identity, space);

        private static (AtlasRegistry, Capture) Rig(AtlasSettings settings = null)
        {
            var registry = new AtlasRegistry(settings);
            var capture = new Capture();
            registry.AddProjection(new BearingProjection(), capture);
            return (registry, capture);
        }

        // 10
        [Test]
        public void RegisteredAppearsAndUnregisteredDoesNot()
        {
            (AtlasRegistry registry, Capture capture) = Rig();
            var fake = new Fake { At = new Vector3(0f, 0f, 5f) };

            registry.Register(fake);
            registry.Tick(Viewer());
            Assert.AreEqual(1, capture.Last.Count);

            registry.Unregister(fake);
            registry.Tick(Viewer());
            Assert.AreEqual(0, capture.Last.Count);
        }

        // 11
        [Test]
        public void IsTrackedFalseExcludesWithoutUnregistering()
        {
            (AtlasRegistry registry, Capture capture) = Rig();
            var fake = new Fake { At = new Vector3(0f, 0f, 5f) };
            registry.Register(fake);

            fake.On = false;
            registry.Tick(Viewer());
            Assert.AreEqual(0, capture.Last.Count);

            fake.On = true;
            registry.Tick(Viewer());
            Assert.AreEqual(1, capture.Last.Count, "still registered the whole time");
        }

        // 12
        [Test]
        public void TwoRegistriesShareNothing()
        {
            (AtlasRegistry a, Capture captureA) = Rig();
            (AtlasRegistry b, Capture captureB) = Rig();

            a.Register(new Fake { At = new Vector3(0f, 0f, 5f) });

            a.Tick(Viewer());
            b.Tick(Viewer());

            Assert.AreEqual(1, captureA.Last.Count);
            Assert.AreEqual(0, captureB.Last.Count, "split-screen needs two, so there is no shared state");
        }

        // 13
        [Test]
        public void AMarkerInAnotherSpaceIsExcluded()
        {
            (AtlasRegistry registry, Capture capture) = Rig();
            AtlasSpaceId interior = new AtlasSpaceId("Tower Interior");

            registry.Register(new Fake { At = new Vector3(0f, 0f, 5f), In = interior });

            registry.Tick(Viewer(AtlasSpaceId.Default));
            Assert.AreEqual(0, capture.Last.Count, "the viewer is outside; the marker is inside");

            registry.Tick(Viewer(interior));
            Assert.AreEqual(1, capture.Last.Count);
            Assert.IsTrue(capture.Last[0].SameSpace);
        }

        // 14
        [Test]
        public void BeyondMaxDistanceIsCulled()
        {
            (AtlasRegistry registry, Capture capture) = Rig();

            var near = new Fake { At = new Vector3(0f, 0f, 5f) };
            near.Mark.MaxDistance = 10f;

            var far = new Fake { At = new Vector3(0f, 0f, 500f) };
            far.Mark.MaxDistance = 10f;

            registry.Register(near);
            registry.Register(far);
            registry.Tick(Viewer());

            Assert.AreEqual(1, capture.Last.Count);
            Assert.AreSame(near, capture.Last[0].Target);
        }

        // 14b
        [Test]
        public void HighestPrioritiesSurviveTruncation()
        {
            (AtlasRegistry registry, Capture capture) = Rig(new AtlasSettings { MaxMarkers = 3 });

            for (int i = 0; i < 10; i++)
            {
                var fake = new Fake { At = new Vector3(0f, 0f, 5f) };
                fake.Mark.Priority = i;
                registry.Register(fake);
            }

            registry.Tick(Viewer());

            Assert.AreEqual(3, capture.Last.Count);
            Assert.AreEqual(9f, capture.Last[0].Marker.Priority);
            Assert.AreEqual(8f, capture.Last[1].Marker.Priority);
            Assert.AreEqual(7f, capture.Last[2].Marker.Priority, "and in order, not merely present");
        }

        // 15
        [Test]
        public void TickAllocatesNothingAfterWarmUp()
        {
            var registry = new AtlasRegistry();
            registry.AddProjection(new BearingProjection(), new Capture());
            registry.AddProjection(new ScreenProjection(), new Capture());

            for (int i = 0; i < 24; i++)
                registry.Register(new Fake { At = new Vector3(i, 0f, 5f) });

            // Warm-up: the reused lists grow to their working size on the first ticks,
            // and that growth is the allocation this test is not measuring.
            for (int i = 0; i < 8; i++) registry.Tick(Viewer());

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) registry.Tick(Viewer());
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before,
                "a HUD that allocates per marker per frame shows up in someone's GC profile " +
                "and gets deleted");
        }

        // 16
        [Test]
        public void InterfaceAndDelegateEntryPointsAgree()
        {
            var position = new Vector3(3f, 0f, 7f);
            AtlasMarker marker = AtlasMarker.Point("same");

            (AtlasRegistry viaInterface, Capture captureInterface) = Rig();
            viaInterface.Register(new Fake { At = position, Mark = marker });
            viaInterface.Tick(Viewer());

            (AtlasRegistry viaDelegate, Capture captureDelegate) = Rig();
            viaDelegate.Track(() => position, marker, AtlasSpaceId.Default);
            viaDelegate.Tick(Viewer());

            Assert.AreEqual(1, captureInterface.Last.Count);
            Assert.AreEqual(1, captureDelegate.Last.Count);
            Assert.AreEqual(captureInterface.Last[0].Bearing, captureDelegate.Last[0].Bearing, 0.0001f);
            Assert.AreEqual(captureInterface.Last[0].Distance, captureDelegate.Last[0].Distance, 0.0001f);
        }

        // 17
        [Test]
        public void ReleaseStopsThePositionDelegateBeingCalled()
        {
            (AtlasRegistry registry, Capture _) = Rig();

            int calls = 0;
            AtlasHandle handle = registry.Track(
                () => { calls++; return Vector3.forward * 5f; },
                AtlasMarker.Point(), AtlasSpaceId.Default);

            registry.Tick(Viewer());
            Assert.Greater(calls, 0);

            int afterFirstTick = calls;
            Assert.IsTrue(registry.Release(handle));

            registry.Tick(Viewer());
            Assert.AreEqual(afterFirstTick, calls, "released means the closure is never called again");
        }

        // 17b - the reason handles carry a generation
        [Test]
        public void AStaleHandleCannotReleaseSomebodyElsesMarker()
        {
            (AtlasRegistry registry, Capture capture) = Rig();

            AtlasHandle first = registry.Track(() => Vector3.forward, AtlasMarker.Point("first"), AtlasSpaceId.Default);
            Assert.IsTrue(registry.Release(first));

            // Reuses the slot the first one just freed.
            registry.Track(() => Vector3.forward, AtlasMarker.Point("second"), AtlasSpaceId.Default);

            Assert.IsFalse(registry.Release(first), "the stale handle must not reach the new marker");

            registry.Tick(Viewer());
            Assert.AreEqual(1, capture.Last.Count, "the second marker is still tracked");
        }

        [Test]
        public void ReleasingTwiceIsHarmless()
        {
            (AtlasRegistry registry, Capture _) = Rig();

            AtlasHandle handle = registry.Track(() => Vector3.zero, AtlasMarker.Point(), AtlasSpaceId.Default);
            Assert.IsTrue(registry.Release(handle));
            Assert.IsFalse(registry.Release(handle));
            Assert.IsFalse(registry.Release(AtlasHandle.None));
        }

        [Test]
        public void RegisteringTwiceDoesNotDoubleTheMarker()
        {
            (AtlasRegistry registry, Capture capture) = Rig();
            var fake = new Fake { At = Vector3.forward };

            registry.Register(fake);
            registry.Register(fake);
            registry.Tick(Viewer());

            Assert.AreEqual(1, capture.Last.Count);
        }

        [Test]
        public void TwoProjectionsEachGetTheirOwnSolveList()
        {
            var registry = new AtlasRegistry();
            var bar = new Capture();
            var screen = new Capture();

            registry.AddProjection(new BearingProjection(), bar);
            registry.AddProjection(new ScreenProjection(), screen);
            registry.Register(new Fake { At = new Vector3(0f, 0f, 5f) });

            registry.Tick(Viewer());

            Assert.AreEqual(1, bar.Presented);
            Assert.AreEqual(1, screen.Presented);
            Assert.AreEqual(1, bar.Last.Count);
            Assert.AreEqual(1, screen.Last.Count);
            Assert.AreNotEqual(Vector3.zero, screen.Last[0].ViewportPoint,
                "the screen projection filled the viewport point the bearing one leaves alone");
        }
    }
}
