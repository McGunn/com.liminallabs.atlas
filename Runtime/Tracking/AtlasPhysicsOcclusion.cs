using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Occlusion by raycast, on a budget.
    ///
    /// Drop it beside the registry and indicators dim when their target goes behind
    /// something. It is the obvious implementation of <see cref="IAtlasOcclusion"/> and
    /// deliberately not the only possible one — a stealth game asks its own visibility
    /// grid, a strategy game asks its fog, and neither should have to pay for physics.
    ///
    /// <b>A raycast per marker per frame is not affordable, so this does not do that.</b>
    /// It spends a fixed budget of casts each frame, walking the tracked list round-robin,
    /// and answers every other query from the last result it has. A marker's occlusion is
    /// therefore up to a few frames stale — which is invisible, because occlusion changes
    /// on the timescale of walking behind a wall, not of a frame.
    ///
    /// Results are held per target rather than per index, so a marker registering or
    /// unregistering mid-frame cannot make another marker inherit its answer. That is the
    /// bug the obvious array-of-bools version has, and it looks like a random indicator
    /// dimming for no reason.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Atlas Physics Occlusion")]
    [DefaultExecutionOrder(90)]   // before the registry ticks at 100
    public sealed class AtlasPhysicsOcclusion : MonoBehaviour, IAtlasOcclusion
    {
        [Header("What blocks sight")]
        [Tooltip("Layers that count as solid. Exclude the player, triggers, and anything " +
                 "the markers are attached to.")]
        [SerializeField] private LayerMask blockers = ~0;

        [Tooltip("Whether trigger colliders block. Almost always no - a trigger is a " +
                 "volume, not a wall.")]
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Ignore;

        [Header("Budget")]
        [Tooltip("Casts per frame, spread over the tracked markers round-robin. Occlusion " +
                 "changes when someone walks behind a wall, not between frames, so a small " +
                 "budget is indistinguishable from testing everything.")]
        [SerializeField, Min(1)] private int castsPerFrame = 8;

        [Tooltip("Metres pulled back from the marker, so a marker sitting on a surface is " +
                 "not occluded by the surface it sits on.")]
        [SerializeField, Min(0f)] private float targetInset = 0.25f;

        [Tooltip("Metres pushed out from the viewer, so the camera's own collider - or the " +
                 "wall it is pressed against - does not block everything at once.")]
        [SerializeField, Min(0f)] private float viewerInset = 0.3f;

        [Header("Registry")]
        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        private readonly Dictionary<IAtlasTrackable, bool> occluded =
            new Dictionary<IAtlasTrackable, bool>();

        private int cursor;

        /// <summary>How many markers currently have a cached answer. For diagnostics, and
        /// for anyone wondering whether the budget is keeping up.</summary>
        public int Tracked => occluded.Count;

        private void OnEnable()
        {
            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);
            if (registry == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry, so nothing will be tested " +
                    "for occlusion.", this);
                enabled = false;
                return;
            }

            registry.Registry.Occlusion = this;
        }

        private void OnDisable()
        {
            // Cleared as well as detached: leaving stale answers behind would mean
            // re-enabling this mid-game restores occlusion states from before whatever
            // happened while it was off.
            if (registry != null && ReferenceEquals(registry.Registry.Occlusion, this))
                registry.Registry.Occlusion = null;

            occluded.Clear();
        }

        /// <summary>
        /// Spends this frame's budget, then drops answers for markers that have gone.
        ///
        /// The round-robin cursor walks the tracked list rather than restarting, so every
        /// marker is refreshed on a fixed period rather than the first few being tested
        /// forever - which is what a naive "test the first N" does, and it looks like
        /// occlusion working perfectly near the top of the list and not at all below it.
        /// </summary>
        public void Tick(in AtlasViewer viewer, IReadOnlyList<IAtlasTrackable> targets)
        {
            if (targets.Count == 0)
            {
                occluded.Clear();
                return;
            }

            int budget = Mathf.Min(castsPerFrame, targets.Count);

            for (int i = 0; i < budget; i++)
            {
                if (cursor >= targets.Count) cursor = 0;

                IAtlasTrackable target = targets[cursor++];
                if (target == null || !target.IsTracked) continue;

                occluded[target] = Cast(viewer.Position, target.Position);
            }

            Prune(targets);
        }

        public bool IsOccluded(IAtlasTrackable target, in AtlasViewer viewer) =>
            target != null && occluded.TryGetValue(target, out bool value) && value;

        private bool Cast(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance <= viewerInset + targetInset) return false;

            Vector3 direction = offset / distance;
            Vector3 origin = from + direction * viewerInset;
            float length = distance - viewerInset - targetInset;

            return Physics.Raycast(origin, direction, length, blockers, triggers);
        }

        /// <summary>
        /// Forgets markers that are no longer tracked.
        ///
        /// Not every frame: it is a full walk of the dictionary, and a stale entry costs
        /// nothing but a little memory until then. Tied to the cursor wrapping, so it runs
        /// once per full pass of the tracked list however long that list is.
        /// </summary>
        private void Prune(IReadOnlyList<IAtlasTrackable> targets)
        {
            if (cursor < targets.Count || occluded.Count <= targets.Count) return;

            stale.Clear();
            foreach (KeyValuePair<IAtlasTrackable, bool> entry in occluded)
            {
                bool found = false;
                for (int i = 0; i < targets.Count && !found; i++)
                    found = ReferenceEquals(targets[i], entry.Key);

                if (!found) stale.Add(entry.Key);
            }

            for (int i = 0; i < stale.Count; i++) occluded.Remove(stale[i]);
        }

        private readonly List<IAtlasTrackable> stale = new List<IAtlasTrackable>();
    }
}
