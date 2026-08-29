using UnityEngine;

namespace LiminalLabs.Atlas.SampleM0
{
    /// <summary>
    /// The M0 sample, and the thing it demonstrates is not a compass - it is that one
    /// registration feeds two views.
    ///
    /// Three markers, registered three different ways, none of which requires changing
    /// anyone's class hierarchy:
    ///
    /// <list type="number">
    /// <item>a <see cref="AtlasMarkerBehaviour"/> component, zero code</item>
    /// <item>a plain class implementing <see cref="IAtlasTrackable"/></item>
    /// <item>a delegate, on something with <b>no GameObject at all</b></item>
    /// </list>
    ///
    /// Turn on the spot. All three slide along the bar at the same rate and pin to the
    /// same screen edges, because there is one solve behind both views. When the orbiting
    /// marker passes behind you, watch which end of the bar it leaves and which edge the
    /// icon pins to - that is the case this package exists to get right.
    /// </summary>
    public sealed class AtlasM0Demo : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private AtlasRegistryBehaviour registry;
        [SerializeField] private BarPresenter bar;
        [SerializeField] private ScreenPresenter screen;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 3f;

        [Header("Orbit")]
        [Tooltip("The marker that circles you, so the behind-the-viewer case happens on its own.")]
        [SerializeField] private Transform orbiting;

        [SerializeField] private float orbitRadius = 18f;
        [SerializeField] private float orbitSpeed = 20f;

        /// <summary>Entry point 2: a plain class. No MonoBehaviour, no base class.</summary>
        private sealed class Landmark : IAtlasTrackable
        {
            private readonly Vector3 position;
            private readonly AtlasMarker marker;

            public Landmark(Vector3 position, string label, int iconId, Color tint)
            {
                this.position = position;
                marker = new AtlasMarker
                {
                    Kind = AtlasMarkerKind.Objective,
                    Label = label,
                    IconId = iconId,
                    Tint = tint,
                    Priority = 1f,
                };
            }

            public Vector3 Position => position;
            public AtlasMarker Marker => marker;
            public AtlasSpaceId Space => AtlasSpaceId.Default;
            public bool IsTracked => true;
        }

        private Vector3 driftingPosition;
        private AtlasHandle driftingHandle;
        private float yaw;
        private float pitch;

        private void Start()
        {
            if (registry == null) registry = FindAnyObjectByType<AtlasRegistryBehaviour>();
            if (registry == null)
            {
                Debug.LogError("[Atlas M0] No AtlasRegistryBehaviour. Rebuild the sample scene.");
                enabled = false;
                return;
            }

            // Two projections over one registry. This pair is the milestone: they are
            // never given the same numbers twice, they are given one answer twice.
            registry.AddProjection(new BearingProjection(), bar);
            registry.AddProjection(new ScreenProjection(), screen);

            // Entry point 2.
            registry.Registry.Register(new Landmark(new Vector3(0f, 1f, 40f), "North Tower", 1, Color.cyan));

            // Entry point 3: tracked by a delegate. There is no GameObject here at all -
            // driftingPosition is a field, and that is the whole object. This is what
            // lets a strategy game track ten thousand units without ten thousand
            // components.
            driftingPosition = new Vector3(-25f, 1f, 12f);
            driftingHandle = registry.Registry.Track(
                () => driftingPosition,
                new AtlasMarker
                {
                    Kind = AtlasMarkerKind.Waypoint,
                    Label = "Drifting Signal",
                    IconId = 2,
                    Tint = new Color(1f, 0.8f, 0.3f),
                    Priority = 0.5f,
                },
                AtlasSpaceId.Default);
        }

        private void OnDestroy()
        {
            if (registry != null) registry.Registry.Release(driftingHandle);
        }

        private void Update()
        {
            Look();
            Orbit();

            // A position that is nothing but a number, moving.
            driftingPosition += new Vector3(Mathf.Sin(Time.time * 0.4f), 0f, 0f) * (4f * Time.deltaTime);
        }

        private void Look()
        {
            if (!Input.GetMouseButton(1) && Cursor.lockState != CursorLockMode.Locked) return;

            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -80f, 80f);

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

        private void OnGUI()
        {
            GUI.Label(new Rect(12f, 12f, 640f, 22f),
                "Hold right mouse to look. Three markers, three entry points, two views, one registry.");
            GUI.Label(new Rect(12f, 32f, 640f, 22f),
                "Watch the orbiting marker pass behind you: which end of the bar, which screen edge.");
        }
    }
}
