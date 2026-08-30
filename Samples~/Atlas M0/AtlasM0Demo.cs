using UnityEngine;

namespace LiminalLabs.Atlas.SampleM0
{
    /// <summary>
    /// The milestone, in one scene.
    ///
    /// Three markers, registered three different ways, each appearing on the compass bar
    /// at the correct bearing <b>and</b> as an on-screen indicator — from one
    /// registration and one solve.
    ///
    /// The case worth watching is the orbiting marker passing behind you. A projection
    /// matrix divides by w, and behind the viewer w is negative, so the raw point comes
    /// back mirrored through the centre: something behind and to your left projects to
    /// the <i>right</i> of the screen. Every ad-hoc indicator ships that bug, it looks
    /// almost right, and it survives playtests. It is only catchable when both views read
    /// the same answer — the bar marker leaves the correct end at the same instant the
    /// icon pins to the correct edge.
    ///
    /// Press <b>Tab</b> for the numbers behind the pixels: bearing, distance and whether
    /// the solve thinks each marker is behind you. Those are computed once, before either
    /// view draws anything.
    /// </summary>
    public sealed class AtlasM0Demo : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Leave empty to find one in the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Header("Orbit")]
        [Tooltip("Circles you, so the behind-the-viewer case happens without you arranging it.")]
        [SerializeField] private Transform orbiting;

        [SerializeField] private float orbitRadius = 18f;
        [SerializeField] private float orbitSpeed = 20f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 3f;

        [Header("Readout")]
        [SerializeField] private bool readoutVisible = true;

        private Vector3 driftingPosition = new Vector3(-25f, 1f, 12f);
        private AtlasHandle driftingHandle;
        private float yaw;
        private float pitch;

        /// <summary>
        /// Entry point 2: a plain class implementing the interface.
        ///
        /// No MonoBehaviour, no base class, no component on anything. A quest, a network
        /// peer or a row in a database can be trackable the same way.
        /// </summary>
        private sealed class Landmark : IAtlasTrackable
        {
            private readonly Vector3 position;
            private readonly AtlasMarker marker;

            public Landmark(Vector3 position, AtlasMarker marker)
            {
                this.position = position;
                this.marker = marker;
            }

            public Vector3 Position => position;
            public AtlasMarker Marker => marker;
            public AtlasSpaceId Space => AtlasSpaceId.Default;
            public bool IsTracked => true;
        }

        private void Start()
        {
            if (registry == null) registry = FindAnyObjectByType<AtlasRegistryBehaviour>();
            if (registry == null)
            {
                Debug.LogError("[Atlas M0] No AtlasRegistryBehaviour in the scene. " +
                               "Rebuild it from Window > Liminal Labs > Atlas.");
                enabled = false;
                return;
            }

            // Note what is *not* here: no AddProjection call. The compass bar and the
            // indicator layer register themselves when they are enabled, exactly as
            // marker components do — so a working scene is a registry, a presenter and
            // some markers, with no glue script at all. This one exists to move things
            // around and print numbers.
            registry.Registry.Register(new Landmark(new Vector3(0f, 1f, 40f), new AtlasMarker
            {
                Kind = AtlasMarkerKind.Objective,
                Label = "North Tower",
                Tint = Color.cyan,
                Priority = 1f,
            }));

            // Entry point 3: a position delegate. There is no GameObject here at all —
            // driftingPosition is a field, and that is the whole object. This is what
            // lets a strategy game track ten thousand units without ten thousand
            // components.
            driftingHandle = registry.Registry.Track(() => driftingPosition, new AtlasMarker
            {
                Kind = AtlasMarkerKind.Waypoint,
                Label = "Drifting Signal",
                Tint = new Color(1f, 0.8f, 0.3f),
                Priority = 0.5f,
            }, AtlasSpaceId.Default);
        }

        private void OnDestroy()
        {
            // The component-tracked marker unregisters itself in OnDisable. A delegate
            // has no component to do that for it, so releasing the handle is the caller's
            // job — and forgetting would leave the registry calling into a closure that
            // outlived its scene.
            if (registry != null) registry.Registry.Release(driftingHandle);
        }

        private void Update()
        {
            if (AtlasM0Input.ReadoutPressed) readoutVisible = !readoutVisible;

            Look();
            Orbit();

            driftingPosition += new Vector3(Mathf.Sin(Time.time * 0.4f), 0f, 0f) *
                                (4f * Time.deltaTime);
        }

        /// <summary>
        /// Right mouse to look, so the sample captures no cursor.
        ///
        /// Turning is the whole point — everything interesting about a bearing happens
        /// when the viewer rotates, and the behind-you case cannot be seen standing still.
        /// </summary>
        private void Look()
        {
            if (!AtlasM0Input.LookHeld) return;

            Vector2 look = AtlasM0Input.LookDelta * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);

            Camera camera = registry.ViewerCamera;
            if (camera != null) camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void Orbit()
        {
            if (orbiting == null) return;

            float angle = Time.time * orbitSpeed * Mathf.Deg2Rad;
            orbiting.position = new Vector3(
                Mathf.Sin(angle) * orbitRadius, 1f, Mathf.Cos(angle) * orbitRadius);
        }

        /// <summary>
        /// The solve, as numbers, beside the views drawing it.
        ///
        /// Worth having in the sample rather than only in the console: seeing a bearing of
        /// −173° next to a marker sitting at the left end of the bar and an icon pinned to
        /// the left edge is what makes it obvious those are one answer rendered twice,
        /// rather than two implementations that happen to agree today.
        /// </summary>
        private void OnGUI()
        {
            GUI.Label(new Rect(12f, 12f, 820f, 20f),
                "Hold right mouse to look.  Three markers, three entry points, two views.");
            GUI.Label(new Rect(12f, 30f, 820f, 20f),
                "Watch the orbiting marker pass behind you.  [Tab] readout.");

            if (!readoutVisible || registry == null) return;

            AtlasViewer viewer = registry.Registry.LastViewer;
            int count = registry.Registry.Tracked.Count;

            GUI.Box(new Rect(10f, 56f, 470f, 26f + count * 20f), GUIContent.none);
            GUI.Label(new Rect(20f, 60f, 450f, 20f), "  marker              bearing   distance   where");

            float y = 80f;
            for (int i = 0; i < count; i++)
            {
                IAtlasTrackable target = registry.Registry.Tracked[i];
                Vector3 position = target.Position;
                Vector3 viewport = AtlasMath.Viewport(viewer, position);

                string where = viewport.z < 0f ? "BEHIND"
                    : AtlasMath.IsOnScreen(viewport) ? "on screen"
                    : "off screen";

                GUI.Label(new Rect(20f, y, 450f, 20f), string.Format(
                    "  {0,-18} {1,7:0.0}° {2,8:0.0} m   {3}",
                    target.Marker.Label ?? target.Marker.Kind.ToString(),
                    AtlasMath.Bearing(viewer, position),
                    Vector3.Distance(viewer.Position, position),
                    where));
                y += 20f;
            }
        }
    }
}
