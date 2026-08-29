using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// The game, as a component: owns a registry, ticks it, and is what everything else in
    /// a scene finds its registry through.
    ///
    /// This exists so markers and presenters have something to find that is <b>not</b> a
    /// static. A scene-level provider keeps split-screen possible - two of these, each
    /// with its own camera, its own markers and its own HUD - which a singleton would
    /// quietly make impossible on the day someone tried it.
    ///
    /// Nothing about the registry requires this component. A game that ticks its own
    /// registry from its own update order does not need it, which is the point of the
    /// registry not ticking itself.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Atlas Registry")]
    [DefaultExecutionOrder(100)]
    public sealed class AtlasRegistryBehaviour : MonoBehaviour
    {
        [Header("Viewer")]
        [Tooltip("The camera markers are solved against. Falls back to the main camera.")]
        [SerializeField] private Camera viewerCamera;

        [Tooltip("Which space the viewer is in. Default unless a game models more.")]
        [SerializeField] private string spaceName;

        [Header("Limits")]
        [Tooltip("How many markers reach a presenter in one frame, and the size of every presenter's pool.")]
        [SerializeField, Min(1)] private int maxMarkers = 32;

        [Tooltip("Cull distance for markers that set none. Zero means no limit.")]
        [SerializeField, Min(0f)] private float defaultMaxDistance;

        private AtlasRegistry registry;

        /// <summary>The registry this component owns. Created on first access, so anything
        /// that wakes before this component still finds one.</summary>
        public AtlasRegistry Registry => registry ??= new AtlasRegistry(new AtlasSettings
        {
            MaxMarkers = maxMarkers,
            DefaultMaxDistance = defaultMaxDistance,
        });

        /// <summary>The space this viewer is looking at.</summary>
        public AtlasSpaceId Space =>
            string.IsNullOrEmpty(spaceName) ? AtlasSpaceId.Default : new AtlasSpaceId(spaceName);

        /// <summary>The camera in use, or the main one.</summary>
        public Camera ViewerCamera
        {
            get => viewerCamera != null ? viewerCamera : Camera.main;
            set => viewerCamera = value;
        }

        /// <summary>Registers a projection and its presenter. Shorthand, so a scene can be
        /// wired without reaching through to <see cref="Registry"/>.</summary>
        public void AddProjection(IAtlasProjection projection, IAtlasPresenter presenter) =>
            Registry.AddProjection(projection, presenter);

        /// <summary>
        /// Finds the registry a component in a scene should use.
        ///
        /// Parents first, then the scene. Parents first matters for split-screen: each
        /// player's markers and HUD hang under that player's rig, and the nearest registry
        /// is the right one - a scene-wide search would hand both players the same one.
        ///
        /// A static method, not a static instance. It searches; it does not hold. That
        /// distinction is the whole of §8.2, and it is why two registries can coexist.
        /// </summary>
        public static AtlasRegistryBehaviour ResolveFor(Component context)
        {
            if (context == null) return null;

            AtlasRegistryBehaviour found = context.GetComponentInParent<AtlasRegistryBehaviour>(true);
            if (found != null) return found;

            // FindAny rather than FindFirst: First is deprecated for depending on
            // instance-id ordering, and "the first one" was never the meaningful answer -
            // a scene with two registries wants the parent search to have found the right
            // one already.
            return FindAnyObjectByType<AtlasRegistryBehaviour>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Ticked in LateUpdate so the camera has finished moving.
        ///
        /// A compass solved before the camera's own LateUpdate lags it by a frame, which
        /// reads as the bar sliding slightly behind the world when you turn quickly -
        /// subtle, constant, and hard to attribute once shipped.
        /// </summary>
        private void LateUpdate()
        {
            Camera camera = ViewerCamera;
            if (camera == null) return;

            Registry.Tick(AtlasViewer.FromCamera(camera, Space));
        }
    }
}
