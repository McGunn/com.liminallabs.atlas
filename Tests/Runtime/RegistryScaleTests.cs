using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// The registry at crowd sizes. Registration and unregistration are dictionary work,
    /// the tracked list keeps registration order across holes, and the slice that reaches
    /// a presenter is exactly what a stable sort and a truncation would have produced.
    /// </summary>
    public class RegistryScaleTests
    {
        private sealed class Fake : IAtlasTrackable
        {
            public Vector3 At;
            public AtlasMarker Mark = AtlasMarker.Point("fake");
            public bool On = true;

            public Vector3 Position => At;
            public AtlasMarker Marker => Mark;
            public AtlasSpaceId Space => AtlasSpaceId.Default;
            public bool IsTracked => On;
        }

        private sealed class Capture : IAtlasPresenter
        {
            public readonly List<AtlasSolve> Last = new List<AtlasSolve>();

            public void Present(in AtlasViewer viewer, IReadOnlyList<AtlasSolve> solves)
            {
                Last.Clear();
                for (int i = 0; i < solves.Count; i++) Last.Add(solves[i]);
            }
        }

        private static AtlasViewer Viewer() =>
            new AtlasViewer(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right,
                60f, 16f / 9f, Matrix4x4.identity, AtlasSpaceId.Default);

        [Test]
        public void TheSliceThatReachesAPresenter_IsAStableSortTruncated()
        {
            var settings = new AtlasSettings { MaxMarkers = 8, CandidateSlack = 2 };
            var registry = new AtlasRegistry(settings);
            var capture = new Capture();
            registry.AddProjection(new BearingProjection(), capture);

            var random = new System.Random(1234);
            var fakes = new List<Fake>();
            for (int i = 0; i < 500; i++)
            {
                var fake = new Fake { At = new Vector3(i * 0.01f, 0f, 5f) };
                fake.Mark.Priority = random.Next(0, 6);   // few distinct values, so ties are everywhere
                fakes.Add(fake);
                registry.Register(fake);
            }

            registry.Tick(Viewer());

            // What a stable descending sort then a cut to the view's limit would give.
            List<Fake> expected = StableByPriority(fakes);

            Assert.AreEqual(8, capture.Last.Count);
            for (int i = 0; i < 8; i++)
                Assert.AreSame(expected[i], capture.Last[i].Target, $"position {i}");
        }

        private static List<Fake> StableByPriority(List<Fake> fakes)
        {
            var indexed = new List<(Fake fake, int order)>();
            for (int i = 0; i < fakes.Count; i++) indexed.Add((fakes[i], i));
            indexed.Sort((a, b) =>
            {
                int byPriority = b.fake.Mark.Priority.CompareTo(a.fake.Mark.Priority);
                return byPriority != 0 ? byPriority : a.order.CompareTo(b.order);
            });
            var result = new List<Fake>();
            foreach ((Fake fake, int _) in indexed) result.Add(fake);
            return result;
        }

        [Test]
        public void UnregisteringLeavesNoHole_AndKeepsTheOrderOfTheRest()
        {
            var registry = new AtlasRegistry();
            var fakes = new Fake[5];
            for (int i = 0; i < fakes.Length; i++)
            {
                fakes[i] = new Fake { At = new Vector3(0f, 0f, 5f + i) };
                registry.Register(fakes[i]);
            }

            registry.Unregister(fakes[2]);
            registry.Unregister(fakes[0]);

            IReadOnlyList<IAtlasTrackable> tracked = registry.Tracked;
            Assert.AreEqual(3, tracked.Count);
            Assert.AreSame(fakes[1], tracked[0]);
            Assert.AreSame(fakes[3], tracked[1]);
            Assert.AreSame(fakes[4], tracked[2]);

            registry.Register(fakes[2]);
            Assert.AreSame(fakes[2], registry.Tracked[3], "re-registered at the end, like a newcomer");

            registry.Unregister(fakes[2]);
            registry.Unregister(fakes[2]);   // twice is nothing
            Assert.AreEqual(3, registry.Tracked.Count);
        }

        [Test]
        public void ACrowdRegistersAndUnregisters_AndTheRegistryIsEmptyAfter()
        {
            var registry = new AtlasRegistry();
            var capture = new Capture();
            registry.AddProjection(new BearingProjection(), capture);

            var fakes = new List<Fake>();
            for (int i = 0; i < 10000; i++)
            {
                var fake = new Fake { At = new Vector3(i % 100, 0f, 5f + i / 100) };
                fakes.Add(fake);
                registry.Register(fake);
            }
            Assert.AreEqual(10000, registry.Tracked.Count);

            registry.Tick(Viewer());
            Assert.AreEqual(new AtlasSettings().MaxMarkers, capture.Last.Count, "the view's limit, not the crowd");

            for (int i = fakes.Count - 1; i >= 0; i--) registry.Unregister(fakes[i]);
            Assert.AreEqual(0, registry.Tracked.Count);

            registry.Tick(Viewer());
            Assert.AreEqual(0, capture.Last.Count);
        }

        [Test]
        public void DelegateTracking_UsesTheSameIndex()
        {
            var registry = new AtlasRegistry();
            Vector3 at = new Vector3(0f, 0f, 5f);
            AtlasHandle a = registry.Track(() => at, AtlasMarker.Point("a"), AtlasSpaceId.Default);
            AtlasHandle b = registry.Track(() => at, AtlasMarker.Point("b"), AtlasSpaceId.Default);

            Assert.AreEqual(2, registry.Tracked.Count);
            Assert.IsTrue(registry.Release(a));
            Assert.AreEqual(1, registry.Tracked.Count);
            Assert.IsFalse(registry.Release(a), "a stale handle does nothing");
            Assert.IsTrue(registry.Release(b));
            Assert.AreEqual(0, registry.Tracked.Count);
        }
    }
}
