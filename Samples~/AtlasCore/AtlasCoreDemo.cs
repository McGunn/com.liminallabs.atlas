using UnityEngine;

namespace LiminalLabs.Atlas.SampleCore
{
    /// <summary>
    /// The registry, with nothing drawing it.
    ///
    /// This sample exists because the core package draws nothing by design, and a package
    /// that cannot be demonstrated without installing another one is a package nobody
    /// trusts. So: three markers registered three different ways, and the raw solve
    /// printed on screen in plain IMGUI.
    ///
    /// What it shows is the claim underneath the whole system - that a bearing, a
    /// distance and a viewport point are computed once, from plain numbers, with no
    /// presenter involved. Install Atlas Compass or Atlas On-Screen and the same
    /// registration draws itself; nothing here changes.
    /// </summary>
    public sealed class AtlasCoreDemo : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Header("Orbit")]
        [Tooltip("Circles you, so the behind-the-viewer case happens on its own.")]
        [SerializeField] private Transform orbiting;

        [SerializeField] private float orbitRadius = 18f;
        [SerializeField] private float orbitSpeed = 20f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 3f;

        private float yaw;
        private float pitch;

        /// <summary>
        /// Right-mouse to look, so the demo works without capturing the cursor.
        ///
        /// Turning is the entire point: everything interesting about a bearing happens
        /// when the viewer rotates, and the behind-you case cannot be seen standing still.
        /// </summary>
        private void Look()
        {
            if (!Input.GetMouseButton(1)) return;

            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -80f, 80f);

            Camera camera = registry != null ? registry.ViewerCamera : null;
            if (camera != null) camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }


        /// <summary>Entry point 2: a plain class. No MonoBehaviour, no base class, no
        /// component on anything.</summary>
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

        private Vector3 driftingPosition = new Vector3(-25f, 1f, 12f);
        private AtlasHandle driftingHandle;

        private void Start()
        {
            if (registry == null) registry = FindAnyObjectByType<AtlasRegistryBehaviour>();
            if (registry == null)
            {
                Debug.LogError("[Atlas Core] No AtlasRegistryBehaviour. Rebuild the sample scene.");
                enabled = false;
                return;
            }

            registry.Registry.Register(new Landmark(new Vector3(0f, 1f, 40f), new AtlasMarker
            {
                Kind = AtlasMarkerKind.Objective,
                Label = "North Tower",
                Tint = Color.cyan,
                Priority = 1f,
            }));

            // Entry point 3. There is no GameObject here at all - driftingPosition is a
            // field, and that is the whole object.
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
            if (registry != null) registry.Registry.Release(driftingHandle);
        }

        private void Update()
        {
            Look();

            if (orbiting != null)
            {
                float angle = Time.time * orbitSpeed * Mathf.Deg2Rad;
                orbiting.position = new Vector3(
                    Mathf.Sin(angle) * orbitRadius, 1f, Mathf.Cos(angle) * orbitRadius);
            }

            driftingPosition += new Vector3(Mathf.Sin(Time.time * 0.4f), 0f, 0f) * (4f * Time.deltaTime);
        }

        /// <summary>
        /// The solve, as numbers.
        ///
        /// Deliberately not a presenter. A presenter would be the other packages' job,
        /// and printing the raw values is what makes it obvious that the bearing sign and
        /// the behind-you flag are decided here rather than in whatever draws them.
        /// </summary>
        private void OnGUI()
        {
            if (registry == null) return;

            AtlasViewer viewer = registry.Registry.LastViewer;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };

            GUI.Box(new Rect(10f, 10f, 640f, 34f + registry.Registry.Tracked.Count * 20f), GUIContent.none);
            GUI.Label(new Rect(20f, 16f, 620f, 20f),
                "Hold right mouse to look.  Three entry points, one registry, no presenter.", style);

            float y = 40f;
            for (int i = 0; i < registry.Registry.Tracked.Count; i++)
            {
                IAtlasTrackable target = registry.Registry.Tracked[i];
                Vector3 position = target.Position;

                float bearing = AtlasMath.Bearing(viewer, position);
                Vector3 viewport = AtlasMath.Viewport(viewer, position);

                string where = viewport.z < 0f ? "BEHIND"
                    : AtlasMath.IsOnScreen(viewport) ? "on screen" : "off screen";

                GUI.Label(new Rect(20f, y, 620f, 20f), string.Format(
                    "{0,-18} {1,7:0.0} deg {2,7:0.0} m   {3}",
                    target.Marker.Label ?? target.Marker.Kind.ToString(),
                    bearing, Vector3.Distance(viewer.Position, position), where), style);
                y += 20f;
            }
        }
    }
}
