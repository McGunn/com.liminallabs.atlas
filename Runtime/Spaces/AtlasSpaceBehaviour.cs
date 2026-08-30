using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// A map plane, authored in a scene.
    ///
    /// The space model existed from M0 and had no way in until now: <see cref="AtlasRegistry"/>
    /// is a plain object built at runtime, so anything written into its spaces from an
    /// editor script is set on a registry that is thrown away before play. A world map
    /// framed to a space's bounds therefore framed a space whose bounds were always zero,
    /// and quietly fell back to a radius meant for a minimap.
    ///
    /// So spaces are authored here, as components, and applied to the registry when the
    /// scene wakes. One per space: drop it on an empty object, size the bounds to the
    /// region it covers, and a world map can frame it with no numbers typed twice.
    ///
    /// <b>The bounds are in world units, not map units.</b> They are what the projection
    /// frames, what a baked image will cover at M3, and what a reveal mask will be indexed
    /// against at M4 - so getting them to actually match the playable area is worth the
    /// minute it takes, and the gizmo is there to make that a minute.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Atlas Space")]
    [DefaultExecutionOrder(50)]   // before the registry ticks, after nothing in particular
    public sealed class AtlasSpaceBehaviour : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Empty means the Default space, which always exists.")]
        [SerializeField] private string spaceName;

        [Header("Extent")]
        [Tooltip("Centre of the region this plane covers, in world units. Relative to this object.")]
        [SerializeField] private Vector3 boundsCentre = Vector3.zero;

        [Tooltip("Size of that region, in world units.")]
        [SerializeField] private Vector3 boundsSize = new Vector3(200f, 20f, 200f);

        [Tooltip("Use this object's position as the bounds centre, so moving it moves the space.")]
        [SerializeField] private bool centreOnTransform = true;

        [Header("Image")]
        [Tooltip("Drawn under the markers, stretched across the bounds. Baking is M3; " +
                 "until then assign an authored top-down image.")]
        [SerializeField] private Texture image;

        [Header("Floor")]
        [Tooltip("Where this floor sits vertically, for deciding which floor a position is on.")]
        [SerializeField] private float floorHeight;

        [SerializeField, Min(0.01f)] private float floorThickness = 3f;

        [Header("Registry")]
        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Header("Gizmo")]
        [SerializeField] private bool drawGizmo = true;
        [SerializeField] private Color gizmoColor = new Color(0.3f, 0.8f, 1f, 0.5f);

        /// <summary>The id this authors. Computed rather than stored, so renaming the
        /// space in the inspector cannot leave a stale id behind.</summary>
        public AtlasSpaceId Id =>
            string.IsNullOrEmpty(spaceName) ? AtlasSpaceId.Default : new AtlasSpaceId(spaceName);

        /// <summary>The region this plane covers, in world units.</summary>
        public Bounds WorldBounds =>
            new Bounds(centreOnTransform ? transform.position + boundsCentre : boundsCentre,
                       boundsSize);

        private void OnEnable()
        {
            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);
            if (registry == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry, so its space is not registered. " +
                    "A world map framed to this space will have nothing to frame.", this);
                return;
            }

            Apply(registry.Registry);
        }

        /// <summary>
        /// Writes this into a registry's spaces.
        ///
        /// Updates the existing space rather than replacing it, because the Default space
        /// is created by the registry itself and markers may already refer to it - swapping
        /// the object out would leave them pointing at one nobody reads.
        /// </summary>
        public void Apply(AtlasRegistry target)
        {
            if (target == null) return;

            AtlasSpaceId id = Id;
            if (!target.Spaces.TryGet(id, out AtlasSpace space))
            {
                space = new AtlasSpace { Id = id };
                target.Spaces.Add(space);
            }

            space.Name = string.IsNullOrEmpty(spaceName) ? "Default" : spaceName;
            space.WorldBounds = WorldBounds;
            space.Image = image;
            space.FloorHeight = floorHeight;
            space.FloorThickness = floorThickness;
        }

        /// <summary>
        /// The bounds, drawn. A space whose bounds do not match the playable area is the
        /// most common way a world map comes out wrong, and it is invisible without this.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Bounds bounds = WorldBounds;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // The plane itself, at floor height, since that is what a map actually shows.
            var flat = new Vector3(bounds.size.x, 0f, bounds.size.z);
            var at = new Vector3(bounds.center.x, floorHeight, bounds.center.z);

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.5f);
            Gizmos.DrawWireCube(at, flat);
        }
    }
}
