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
        private readonly Dictionary<IAtlasTrackable, int> indexOf = new Dictionary<IAtlasTrackable, int>();
        private int holes;

        private readonly List<Delegated> delegated = new List<Delegated>();
        private readonly List<int> freeSlots = new List<int>();

        private readonly List<Output> outputs = new List<Output>();

        /// <summary>Refilled every tick and never resized after warm-up.</summary>
        private readonly List<AtlasCandidate> candidates = new List<AtlasCandidate>();
        private readonly List<Ranked> ranked = new List<Ranked>();

        public AtlasRegistry(AtlasSettings settings = null)
        {
            this.settings = settings ?? new AtlasSettings();
            Spaces = new AtlasSpaceRegistry();
        }

        /// <summary>The spaces this registry knows. Default already exists.</summary>
        public AtlasSpaceRegistry Spaces { get; }

        public AtlasSettings Settings => settings;

        /// <summary>Everything registered, tracked or not, in registration order. For
        /// diagnostics.</summary>
        public IReadOnlyList<IAtlasTrackable> Tracked
        {
            get
            {
                Compact();
                return tracked;
            }
        }

        /// <summary>The viewer of the last tick, for diagnostics and for presenters that
        /// need to know where the camera was without asking one.</summary>
        public AtlasViewer LastViewer { get; private set; }

        // ---- entry point 2: implement the interface -------------------------
        //
        // Registration is a dictionary insert and unregistration leaves a hole that the
        // next tick closes. The obvious List.Contains / List.Remove pair is a walk of the
        // list per call, which turns the README's ten thousand units into fifty million
        // compares to register and fifty million to unload - the frame hitch on a scene
        // change that nobody traces to a HUD.

        public void Register(IAtlasTrackable trackable)
        {
            if (trackable == null || indexOf.ContainsKey(trackable)) return;

            indexOf[trackable] = tracked.Count;
            tracked.Add(trackable);
        }

        public void Unregister(IAtlasTrackable trackable)
        {
            if (trackable == null || !indexOf.TryGetValue(trackable, out int index)) return;

            indexOf.Remove(trackable);
            tracked[index] = null;
            holes++;
        }

        /// <summary>Closes the holes unregistration left, keeping registration order - which
        /// the priority tie-break depends on, so markers of equal priority never trade
        /// places because a third one left.</summary>
        private void Compact()
        {
            if (holes == 0) return;

            int write = 0;
            for (int read = 0; read < tracked.Count; read++)
            {
                IAtlasTrackable entry = tracked[read];
                if (entry == null) continue;

                if (write != read)
                {
                    tracked[write] = entry;
                    indexOf[entry] = write;
                }
                write++;
            }

            tracked.RemoveRange(write, tracked.Count - write);
            holes = 0;
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

            Register(slot);
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

            Unregister(slot);
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
        /// <param name="maxMarkers">How many markers this view draws at once. Zero uses
        /// <see cref="AtlasSettings.MaxMarkers"/>. Per view because a world map wanting 64
        /// and a compass wanting 12 are both reasonable, and one shared number suits
        /// neither - it also made the pool-size check warn against a figure that was right
        /// for nothing in the scene.</param>
        public bool AddProjection(IAtlasProjection projection, IAtlasPresenter presenter,
                                  int maxMarkers = 0)
        {
            if (projection == null || presenter == null) return false;

            for (int i = 0; i < outputs.Count; i++)
                if (ReferenceEquals(outputs[i].Presenter, presenter)) return false;

            outputs.Add(new Output(projection, presenter, maxMarkers));
            return true;
        }

        /// <summary>
        /// Who decides what is blocked. Null means nothing ever is, which is how every
        /// system without occlusion behaves and therefore the right default.
        /// </summary>
        public IAtlasOcclusion Occlusion { get; set; }

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
        /// interfaces, and a hand-written selection. A HUD that allocates once per marker
        /// per frame is a HUD that shows up in someone's GC profile and gets deleted.
        /// </summary>
        public void Tick(in AtlasViewer viewer)
        {
            LastViewer = viewer;
            if (outputs.Count == 0) return;

            Gather(viewer);
            Solve(viewer);
        }

        /// <summary>
        /// Every marker worth considering, solved once, cheapest test first.
        ///
        /// The ordering of the three passes is the whole performance story.
        ///
        /// <b>Cull, then rank, then solve.</b> Distance culling is a squared compare and
        /// runs over everything. Ranking keeps only the top slice - the largest view's
        /// limit times the slack - as the survivors stream past, so a crowd of ten
        /// thousand costs ten thousand compares against the slice's floor and a handful
        /// of inserts, not a sort of ten thousand. Only that slice gets the square roots,
        /// the bearings and the viewport transforms - so the frame costs what is
        /// <i>drawn</i> rather than what is <i>tracked</i>.
        ///
        /// Priority can be ranked before solving because it lives on the marker and owes
        /// nothing to the viewer. That is the only reason this ordering is available, and
        /// it is worth not breaking.
        /// </summary>
        private void Gather(in AtlasViewer viewer)
        {
            candidates.Clear();
            ranked.Clear();
            Compact();

            // The slice worth solving. Slack above the largest view's limit because the
            // projections filter further - by space, by AtlasFilter, by fade - so taking
            // exactly the limit here would leave a view short of markers it would have
            // drawn. Four is generous for a HUD and still bounded.
            int wanted = LargestLimit() * Mathf.Max(1, settings.CandidateSlack);

            for (int i = 0; i < tracked.Count; i++)
            {
                IAtlasTrackable target = tracked[i];
                if (!target.IsTracked) continue;

                if (settings.CullOtherSpaces && target.Space != viewer.Space) continue;

                AtlasMarker marker = target.Marker;
                float maxDistance = marker.MaxDistance;
                if (maxDistance <= 0f) maxDistance = settings.DefaultMaxDistance;

                Vector3 position = target.Position;

                if (maxDistance > 0f)
                {
                    // Squared, so culling a thousand markers costs no square roots. The
                    // survivors get the real distance once, below.
                    Vector3 offset = position - viewer.Position;
                    if (offset.sqrMagnitude > maxDistance * maxDistance) continue;
                }

                Offer(new Ranked(target, marker, position), wanted);
            }

            if (Occlusion != null) Occlusion.Tick(viewer, tracked);

            float band = Mathf.Max(0f, settings.ElevationBand);

            for (int i = 0; i < ranked.Count; i++)
            {
                Ranked entry = ranked[i];
                Vector3 position = entry.Position;

                // Everything below happens once per marker per frame, for every view.
                float distance = Vector3.Distance(viewer.Position, position);
                float elevation = position.y - viewer.Position.y;

                AtlasElevation level = elevation > band ? AtlasElevation.Above
                    : elevation < -band ? AtlasElevation.Below
                    : AtlasElevation.Level;

                candidates.Add(new AtlasCandidate(
                    entry.Target,
                    entry.Marker,
                    position,
                    distance,
                    AtlasMath.Bearing(viewer, position),
                    AtlasMath.Fade(distance, entry.Marker.MaxDistance),
                    AtlasMath.Viewport(viewer, position),
                    elevation,
                    level,
                    Occlusion != null && Occlusion.IsOccluded(entry.Target, viewer),
                    entry.Target.Space == viewer.Space));
            }
        }

        /// <summary>
        /// Keeps the <paramref name="capacity"/> highest-priority survivors, in priority
        /// order and registration order within a priority - the same answer a stable
        /// descending sort truncated to the capacity would give, without sorting everything
        /// that will not be drawn.
        ///
        /// Hand-written insertion for the same two reasons the old sort was: it allocates
        /// nothing, and it is stable, so markers of equal priority keep their registration
        /// order instead of swapping places between frames and making the bar shimmer.
        /// </summary>
        private void Offer(in Ranked entry, int capacity)
        {
            // Full, and no better than the worst kept: the common case in a crowd, and one
            // compare. Equal loses to what is already there, which is registration order.
            if (ranked.Count >= capacity && entry.Marker.Priority <= ranked[ranked.Count - 1].Marker.Priority)
                return;

            int at = ranked.Count;
            while (at > 0 && ranked[at - 1].Marker.Priority < entry.Marker.Priority) at--;

            ranked.Insert(at, entry);
            if (ranked.Count > capacity) ranked.RemoveAt(ranked.Count - 1);
        }

        private void Solve(in AtlasViewer viewer)
        {
            for (int i = 0; i < outputs.Count; i++)
            {
                Output output = outputs[i];

                output.Solves.Clear();
                output.Projection.Solve(viewer, Spaces, candidates, output.Solves);

                // Already ranked: candidates were ordered before solving and projections
                // preserve order, so the truncation below keeps the highest priorities
                // without a second sort.
                int limit = output.MaxMarkers > 0 ? output.MaxMarkers : settings.MaxMarkers;
                if (limit > 0 && output.Solves.Count > limit)
                    output.Solves.RemoveRange(limit, output.Solves.Count - limit);

                output.Presenter.Present(viewer, output.Solves);
            }
        }

        private int LargestLimit()
        {
            int largest = settings.MaxMarkers;
            for (int i = 0; i < outputs.Count; i++)
                if (outputs[i].MaxMarkers > largest) largest = outputs[i].MaxMarkers;

            return Mathf.Max(1, largest);
        }

        /// <summary>A marker that survived culling, with the two things read off it, so
        /// neither is read again.</summary>
        private readonly struct Ranked
        {
            public readonly IAtlasTrackable Target;
            public readonly AtlasMarker Marker;
            public readonly Vector3 Position;

            public Ranked(IAtlasTrackable target, in AtlasMarker marker, Vector3 position)
            {
                Target = target;
                Marker = marker;
                Position = position;
            }
        }

        private sealed class Output
        {
            public readonly IAtlasProjection Projection;
            public readonly IAtlasPresenter Presenter;
            public readonly List<AtlasSolve> Solves = new List<AtlasSolve>();
            public readonly int MaxMarkers;

            public Output(IAtlasProjection projection, IAtlasPresenter presenter, int maxMarkers)
            {
                Projection = projection;
                Presenter = presenter;
                MaxMarkers = maxMarkers;
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
