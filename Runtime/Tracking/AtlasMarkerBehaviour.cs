using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Entry point one: drop it on anything, write no code.
    ///
    /// Registers in OnEnable and unregisters in OnDisable, which Unity guarantees runs on
    /// disable, deactivate, destroy and scene unload - so a marker cannot outlive its
    /// object and leave the compass pointing at nothing.
    ///
    /// It finds its registry through a serialized reference or by searching upward for an
    /// <see cref="AtlasRegistryBehaviour"/>. <b>Never through a static.</b> The search is
    /// the compromise that keeps "drop it on anything, write no code" true without a
    /// singleton, and it happens once per enable rather than per frame.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Marker")]
    public class AtlasMarkerBehaviour : MonoBehaviour, IAtlasTrackable
    {
        [Header("Registry")]
        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Header("Marker")]
        [SerializeField] private AtlasMarkerKind kind = AtlasMarkerKind.Point;
        [SerializeField] private string label;
        [SerializeField] private int iconId;
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Who survives when there are more markers than room. Higher wins.")]
        [SerializeField] private float priority;

        [Tooltip("Zoom LOD threshold. Carried now, used by the world map in M2.")]
        [SerializeField] private float importance;

        [Tooltip("Beyond this, culled. Zero means no limit.")]
        [SerializeField, Min(0f)] private float maxDistance;

        [Header("Space")]
        [Tooltip("Which map plane this belongs to. Empty means Default.")]
        [SerializeField] private string spaceName;

        [Header("Tracking")]
        [Tooltip("Off means the atlas skips it without unregistering.")]
        [SerializeField] private bool tracked = true;

        [Tooltip("Where the marker sits. Leave empty for this object's own position.")]
        [SerializeField] private Transform anchor;

        private AtlasRegistry owner;

        public Vector3 Position => anchor != null ? anchor.position : transform.position;

        /// <summary>
        /// The marker, built once and kept.
        ///
        /// This used to construct a fresh struct on every read, which was correct and
        /// wasteful: the registry reads it, then every projection read it again, so a
        /// nine-field struct was assembled five or six times per marker per frame to
        /// describe data that had not changed. Now it is built when something changes and
        /// read the rest of the time.
        ///
        /// <see cref="OnValidate"/> rebuilds it when the inspector is touched, and
        /// <see cref="Rebuild"/> is there for code that changes a field at runtime. A
        /// subclass overriding <see cref="Marker"/> to compute something live still works -
        /// it never touches the cache.
        /// </summary>
        public virtual AtlasMarker Marker
        {
            get
            {
                if (!markerCached) Rebuild();
                return cachedMarker;
            }
        }

        private AtlasMarker cachedMarker;
        private bool markerCached;

        private AtlasSpaceId cachedSpace;
        private bool spaceCached;

        /// <summary>Rebuilds the cached marker and space id. Call after changing a field in code.</summary>
        public void Rebuild()
        {
            cachedMarker = Build();
            markerCached = true;

            cachedSpace = string.IsNullOrEmpty(spaceName) ? AtlasSpaceId.Default : new AtlasSpaceId(spaceName);
            spaceCached = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            markerCached = false;
            spaceCached = false;
        }
#endif

        private AtlasMarker Build() => new AtlasMarker
        {
            Kind = kind,
            Priority = priority,
            Importance = importance,
            Label = label,
            MaxDistance = maxDistance,
            IconId = iconId,
            Tint = tint,
        };

        /// <summary>
        /// The space this belongs to. Hashed from the name once and kept: the registry asks
        /// twice per marker per frame, and hashing a string twice per marker per frame is
        /// how a name field turns into a per-frame cost that scales with the crowd.
        /// </summary>
        public AtlasSpaceId Space
        {
            get
            {
                if (!spaceCached) Rebuild();
                return cachedSpace;
            }
        }

        public bool IsTracked
        {
            get => tracked;
            set => tracked = value;
        }

        protected virtual void OnEnable()
        {
            owner = Resolve();

            if (owner == null)
            {
                // Named, because the alternative is a marker that silently never appears
                // and a developer checking their icon ids.
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry. Assign one, or put an " +
                    "AtlasRegistryBehaviour on a parent or in the scene.", this);
                return;
            }

            owner.Register(this);
        }

        protected virtual void OnDisable()
        {
            owner?.Unregister(this);
            owner = null;
        }

        private AtlasRegistry Resolve()
        {
            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);
            return registry != null ? registry.Registry : null;
        }

        /// <summary>
        /// Draws the marker in the scene view.
        ///
        /// Worth the fifteen lines: a marker is an invisible component on an object that
        /// often has no renderer of its own, and without a gizmo the only way to find out
        /// whether one is where you think it is, is to press play.
        /// </summary>
        private void OnDrawGizmos()
        {
            Vector3 at = Position;

            Gizmos.color = tracked ? tint : new Color(tint.r, tint.g, tint.b, 0.25f);
            Gizmos.DrawWireSphere(at, 0.35f);

            // The anchor line, when the marker is not where its object is - otherwise the
            // gizmo sits somewhere unexplained.
            if (anchor != null && anchor != transform)
            {
                Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.4f);
                Gizmos.DrawLine(transform.position, at);
            }

            if (maxDistance <= 0f) return;

            // The cull radius, so "why does it vanish over there" is answerable without
            // reading the inspector.
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.12f);
            Gizmos.DrawWireSphere(at, maxDistance);
        }
    }
}
