using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// What is tracked, and one solve per tracked thing per projection per frame.
    ///
    /// <b>An instance the game constructs and owns. There is no singleton and there will
    /// not be one.</b> Split-screen needs two registries with different viewers; a menu
    /// scene needs none; a test needs one with no scene at all. A static instance makes
    /// the first impossible and the third painful, and it is the single hardest thing to
    /// remove once a codebase has grown around it.
    ///
    /// <b>Ticked by the game, not by itself.</b> Same reason: a registry that finds its
    /// own update loop cannot be stepped deterministically in a test, and cannot be run
    /// twice in one frame for two viewers.
    ///
    /// <code>
    /// registry = new AtlasRegistry();
    /// registry.AddProjection(new BearingProjection(), compassBar);
    /// registry.AddProjection(new ScreenProjection(),  screenIcons);
    /// // once a frame:
    /// registry.Tick(AtlasViewer.FromCamera(camera, AtlasSpaceId.Default));
    /// </code>
    /// </summary>
    public sealed class AtlasRegistry
    {
        private readonly AtlasSettings settings;

        private readonly List<IAtlasTrackable> tracked = new List<IAtlasTrackable>();
        private readonly List<Delegated> delegated = new List<Delegated>();
        private readonly List<int> freeSlots = new List<int>();

        private readonly List<Output> outputs = new List<Output>();

        /// <summary>Refilled every tick and never resized after warm-up.</summary>
        private readonly List<IAtlasTrackable> candidates = new List<IAtlasTrackable>();

        public AtlasRegistry(AtlasSettings settings = null)
        {
            this.settings = settings ?? new AtlasSettings();
            Spaces = new AtlasSpaceRegistry();
        }

        /// <summary>The spaces this registry knows. Default already exists.</summary>
        public AtlasSpaceRegistry Spaces { get; }

        public AtlasSettings Settings => settings;

        /// <summary>Everything registered, tracked or not. For diagnostics.</summary>
        public IReadOnlyList<IAtlasTrackable> Tracked => tracked;

        /// <summary>The viewer of the last tick, for diagnostics and for presenters that
        /// need to know where the camera was without asking one.</summary>
        public AtlasViewer LastViewer { get; private set; }

        // ---- entry point 2: implement the interface -------------------------

        public void Register(IAtlasTrackable trackable)
        {
            if (trackable == null || tracked.Contains(trackable)) return;
            tracked.Add(trackable);
        }

        public void Unregister(IAtlasTrackable trackable)
        {
            if (trackable == null) return;
            tracked.Remove(trackable);
        }

        // ---- entry point 3: track something with no GameObject --------------

        /// <summary>
        /// Tracks a position delegate.
        ///
        /// This entry point matters more than it looks. It is what lets a strategy game
        /// track ten thousand units without ten thousand components, and what will let a
        /// content instance be trackable without ever becoming a GameObject.
        /// </summary>
        public AtlasHandle Track(Func<Vector3> position, in AtlasMarker marker, AtlasSpaceId space)
        {
            if (position == null) return AtlasHandle.None;

            int index;
            if (freeSlots.Count > 0)
            {
                index = freeSlots[freeSlots.Count - 1];
                freeSlots.RemoveAt(freeSlots.Count - 1);
            }
            else
            {
                index = delegated.Count;
                delegated.Add(new Delegated());
            }

            Delegated slot = delegated[index];
            slot.Reset(position, marker, space);

            tracked.Add(slot);
            return new AtlasHandle(index, slot.Generation);
        }

        /// <summary>
        /// Stops a tracked delegate being called.
        ///
        /// Bumps the slot's generation, so a stale handle released twice - or released
        /// after the slot was reused - does nothing instead of silently releasing
        /// somebody else's marker.
        /// </summary>
        public bool Release(AtlasHandle handle)
        {
            if (!handle.IsValid) return false;
            if (handle.Index < 0 || handle.Index >= delegated.Count) return false;

            Delegated slot = delegated[handle.Index];
            if (slot.Generation != handle.Generation || !slot.InUse) return false;

            tracked.Remove(slot);
            slot.Retire();
            freeSlots.Add(handle.Index);
            return true;
        }

        /// <summary>Updates the marker of a delegate-tracked object without
        /// re-registering it.</summary>
        public bool SetMarker(AtlasHandle handle, in AtlasMarker marker)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= delegated.Count) return false;

            Delegated slot = delegated[handle.Index];
            if (slot.Generation != handle.Generation || !slot.InUse) return false;

            slot.SetMarker(marker);
            return true;
        }

        // ---- projections ----------------------------------------------------

        /// <summary>
        /// Registers a projection and the presenter that draws it.
        ///
        /// Refuses a presenter that is already registered. Two entries for one presenter
        /// means it is handed two solve lists per frame and the second overwrites the
        /// first's pool state, which shows up as half the markers flickering - a symptom
        /// nobody traces back to a duplicate registration.
        /// </summary>
        public bool AddProjection(IAtlasProjection projection, IAtlasPresenter presenter)
        {
            if (projection == null || presenter == null) return false;

            for (int i = 0; i < outputs.Count; i++)
                if (ReferenceEquals(outputs[i].Presenter, presenter)) return false;

            outputs.Add(new Output(projection, presenter));
            return true;
        }

        public bool RemoveProjection(IAtlasPresenter presenter)
        {
            for (int i = 0; i < outputs.Count; i++)
            {
                if (!ReferenceEquals(outputs[i].Presenter, presenter)) continue;
                outputs.RemoveAt(i);
                return true;
            }
            return false;
        }

        public int ProjectionCount => outputs.Count;

        // ---- the frame ------------------------------------------------------

        /// <summary>
        /// Filter, solve, order, truncate, present. Once per projection.
        ///
        /// Allocates nothing after warm-up, and the whole shape of this method is that
        /// requirement: reused lists, indexed loops rather than <c>foreach</c> over
        /// interfaces, and a hand-written sort. A HUD that allocates once per marker per
        /// frame is a HUD that shows up in someone's GC profile and gets deleted.
        /// </summary>
        public void Tick(in AtlasViewer viewer)
        {
            LastViewer = viewer;
            if (outputs.Count == 0) return;

            candidates.Clear();
            for (int i = 0; i < tracked.Count; i++)
            {
                IAtlasTrackable target = tracked[i];
                if (!target.IsTracked) continue;

                if (settings.CullOtherSpaces && target.Space != viewer.Space) continue;

                float maxDistance = target.Marker.MaxDistance;
                if (maxDistance <= 0f) maxDistance = settings.DefaultMaxDistance;

                if (maxDistance > 0f)
                {
                    // Squared, so culling a thousand markers costs no square roots. The
                    // projections take the real distance for the ones that survive.
                    Vector3 offset = target.Position - viewer.Position;
                    if (offset.sqrMagnitude > maxDistance * maxDistance) continue;
                }

                candidates.Add(target);
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                Output output = outputs[i];

                output.Solves.Clear();
                output.Projection.Solve(viewer, Spaces, candidates, output.Solves);

                SortByPriority(output.Solves);

                if (output.Solves.Count > settings.MaxMarkers)
                    output.Solves.RemoveRange(settings.MaxMarkers, output.Solves.Count - settings.MaxMarkers);

                output.Presenter.Present(viewer, output.Solves);
            }
        }

        /// <summary>
        /// Insertion sort, descending by priority.
        ///
        /// Hand-written for two reasons. It allocates nothing - <c>List.Sort</c> with a
        /// comparison can allocate a comparer wrapper on some runtimes, which is the one
        /// thing test 15 forbids. And it is stable, so markers of equal priority keep
        /// their registration order instead of swapping places between frames and making
        /// the bar shimmer. At the tens of markers a HUD shows, it is also simply faster.
        /// </summary>
        private static void SortByPriority(List<AtlasSolve> solves)
        {
            for (int i = 1; i < solves.Count; i++)
            {
                AtlasSolve current = solves[i];
                int j = i - 1;

                while (j >= 0 && solves[j].Marker.Priority < current.Marker.Priority)
                {
                    solves[j + 1] = solves[j];
                    j--;
                }

                solves[j + 1] = current;
            }
        }

        private sealed class Output
        {
            public readonly IAtlasProjection Projection;
            public readonly IAtlasPresenter Presenter;
            public readonly List<AtlasSolve> Solves = new List<AtlasSolve>();

            public Output(IAtlasProjection projection, IAtlasPresenter presenter)
            {
                Projection = projection;
                Presenter = presenter;
            }
        }

        /// <summary>
        /// A tracked position delegate, wearing the same interface as everything else.
        ///
        /// A class held in a pooled slot rather than a struct, so the registry's one list
        /// holds every kind of trackable and the tick loop has no idea which is which -
        /// test 16's "all three entry points produce equivalent solves" is true by
        /// construction rather than by care.
        /// </summary>
        private sealed class Delegated : IAtlasTrackable
        {
            private Func<Vector3> position;
            private AtlasMarker marker;
            private AtlasSpaceId space;

            public int Generation { get; private set; }
            public bool InUse { get; private set; }

            public void Reset(Func<Vector3> positionSource, in AtlasMarker markerValue, AtlasSpaceId spaceId)
            {
                position = positionSource;
                marker = markerValue;
                space = spaceId;
                InUse = true;

                // Generation zero is never issued, so default(AtlasHandle) stays invalid
                // however many times a slot is recycled.
                Generation++;
                if (Generation == 0) Generation = 1;
            }

            public void SetMarker(in AtlasMarker value) => marker = value;

            public void Retire()
            {
                InUse = false;

                // Dropping the delegate is what actually stops it being called, and it
                // also stops the registry keeping whatever the closure captured alive.
                position = null;
            }

            public Vector3 Position => position != null ? position() : Vector3.zero;
            public AtlasMarker Marker => marker;
            public AtlasSpaceId Space => space;
            public bool IsTracked => InUse;
        }
    }
}
