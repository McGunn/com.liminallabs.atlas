using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// M4: reveals a space as the viewer moves through it.
    ///
    /// Drop it beside an <see cref="AtlasSpaceBehaviour"/>, give it a sight radius, and the
    /// space's <see cref="AtlasReveal"/> fills in behind the player. That is the whole
    /// setup, because the mask is indexed against bounds the space already has.
    ///
    /// <b>Not every frame.</b> Revealing a disc touches thousands of cells, and a player
    /// walking cannot outrun a quarter-second interval at any sane sight radius — so it
    /// runs on a timer and on distance moved, and does nothing at all while the viewer
    /// stands still. A fog system that costs measurable frame time is one a studio turns
    /// off.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Atlas Discovery")]
    [DefaultExecutionOrder(60)]   // after the space registers, before the registry ticks
    public sealed class AtlasDiscovery : MonoBehaviour
    {
        [Header("Space")]
        [Tooltip("Leave empty to use the space on this object, then the viewer's space.")]
        [SerializeField] private AtlasSpaceBehaviour space;

        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Header("Mask")]
        [Tooltip("Cells across the longer side. 256 over a 400-unit space is about 1.5 " +
                 "units a cell, which is finer than fog is ever drawn.")]
        [SerializeField, Min(8)] private int resolution = 256;

        [Header("Sight")]
        [Tooltip("How far the viewer reveals, in world units.")]
        [SerializeField, Min(0.1f)] private float sightRadius = 40f;

        [Tooltip("Seconds between reveals. A player cannot outrun a quarter second at any " +
                 "sane sight radius, and this is the difference between free and costly.")]
        [SerializeField, Min(0.02f)] private float interval = 0.25f;

        [Tooltip("Skip the reveal entirely if the viewer has not moved this far since the " +
                 "last one. Standing still costs nothing at all.")]
        [SerializeField, Min(0f)] private float minimumMovement = 1f;

        [Header("Start")]
        [Tooltip("Reveal everything at startup. For a game that wants the mask for its " +
                 "shader without the exploration.")]
        [SerializeField] private bool startRevealed;

        private AtlasSpace target;
        private float nextRevealTime;
        private Vector3 lastRevealPosition;
        private bool hasRevealed;

        /// <summary>The mask being filled in, or null before the space resolves.</summary>
        public AtlasReveal Reveal => target?.Reveal;

        /// <summary>How much of the space has been seen, 0 to 1.</summary>
        public float RevealedFraction => target?.Reveal?.RevealedFraction() ?? 1f;

        /// <summary>How many cells the last reveal newly uncovered. What a game hooks to
        /// award exploration without diffing the mask itself.</summary>
        public int LastRevealedCells { get; private set; }

        private void OnEnable()
        {
            if (space == null) space = GetComponent<AtlasSpaceBehaviour>();
            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);

            if (registry == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry, so nothing will be revealed.", this);
                enabled = false;
                return;
            }

            AtlasSpaceId id = space != null ? space.Id : registry.Space;
            target = registry.Registry.Spaces.GetOrDefault(id);

            Bounds bounds = target.WorldBounds;
            if (bounds.size.x < 0.01f || bounds.size.z < 0.01f)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' cannot reveal '{target.Name}': it has no bounds, so " +
                    "there is nothing to index a mask against. Author the space with an " +
                    "AtlasSpaceBehaviour and size its bounds.", this);
                enabled = false;
                return;
            }

            // The mask takes the bounds' aspect, so a cell is square in world units and a
            // circular sight radius stays circular. A square mask over a rectangular space
            // reveals an ellipse, which looks like the fog leaking on one axis.
            float aspect = bounds.size.x / bounds.size.z;
            int width = aspect >= 1f ? resolution : Mathf.Max(8, Mathf.RoundToInt(resolution * aspect));
            int height = aspect >= 1f ? Mathf.Max(8, Mathf.RoundToInt(resolution / aspect)) : resolution;

            if (target.Reveal == null ||
                target.Reveal.Width != width || target.Reveal.Height != height)
            {
                target.Reveal = new AtlasReveal(width, height);
            }

            if (startRevealed) target.Reveal.RevealAll();

            nextRevealTime = 0f;
            hasRevealed = false;
        }

        private void LateUpdate()
        {
            if (target?.Reveal == null || registry == null) return;
            if (Time.time < nextRevealTime) return;

            Camera camera = registry.ViewerCamera;
            if (camera == null) return;

            Vector3 at = camera.transform.position;
            if (hasRevealed && (at - lastRevealPosition).sqrMagnitude < minimumMovement * minimumMovement)
                return;

            Bounds bounds = target.WorldBounds;

            // The radius is normalised against the longer side, so it stays one circle
            // rather than becoming two different radii on the two axes.
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            LastRevealedCells = target.Reveal.Reveal(target.Normalise(at), sightRadius / longest);

            nextRevealTime = Time.time + interval;
            lastRevealPosition = at;
            hasRevealed = true;
        }

        /// <summary>Reveals everything. For a debug command and for cheats.</summary>
        public void RevealAll() => target?.Reveal?.RevealAll();

        /// <summary>Hides everything again. For a new game on a reused scene.</summary>
        public void Clear() => target?.Reveal?.Clear();
    }
}
