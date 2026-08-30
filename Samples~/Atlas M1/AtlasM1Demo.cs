using UnityEngine;

namespace LiminalLabs.Atlas.SampleM1
{
    /// <summary>
    /// M1, in one scene: the same markers on a compass, on screen, on a minimap and on a
    /// world map — from one registration and one solve.
    ///
    /// The claim being demonstrated is narrower and more useful than "there is a map".
    /// <b>The minimap and the world map are the same projection with different framings.</b>
    /// Hold M and the world map is not a second system waking up; it is a
    /// <see cref="MapProjection"/> with a bigger radius and no rotation. Nothing is
    /// registered twice, so nothing can drift.
    ///
    /// Worth watching while you turn: the minimap rotates under a fixed player arrow, the
    /// world map does not rotate at all, and a marker leaving the minimap's edge pins to
    /// its circle while the same marker is still sitting in place on the world map.
    /// </summary>
    public sealed class AtlasM1Demo : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private AtlasRegistryBehaviour registry;
        [SerializeField] private GameObject worldMap;
        [SerializeField] private MinimapPresenter worldMapPresenter;
        [SerializeField] private Transform orbiting;

        [Header("World map controls")]
        [Tooltip("Multiplied per wheel notch. Multiplicative because zoom is perceived " +
                 "that way: a fixed step is glacial zoomed out and jumps a whole map in.")]
        [SerializeField] private float zoomStep = 1.15f;

        [Header("Orbit")]
        [SerializeField] private float orbitRadius = 45f;
        [SerializeField] private float orbitSpeed = 25f;

        [Header("Move")]
        [SerializeField] private float moveSpeed = 14f;
        [SerializeField] private float lookSensitivity = 3f;

        private float yaw;
        private float pitch;
        private bool worldMapOpen;

        private void Start()
        {
            if (registry == null) registry = FindAnyObjectByType<AtlasRegistryBehaviour>();
            if (registry == null)
            {
                Debug.LogError("[Atlas M1] No AtlasRegistryBehaviour in the scene. " +
                               "Rebuild it from Window > Liminal Labs > Atlas.");
                enabled = false;
                return;
            }

            // A landmark ring, so there is always something at the minimap's edge to pin
            // and something on the world map that the minimap cannot reach. Heights vary,
            // so the elevation chevrons have something to say.
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                float height = (i % 3 - 1) * 12f;    // below, level, above
                var at = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 120f
                       + Vector3.up * height;

                registry.Registry.Register(new Landmark(at, new AtlasMarker
                {
                    Kind = AtlasMarkerKind.Discovery,
                    Label = "Landmark " + (i + 1),
                    IconId = AtlasM1Icons.Discovery,
                    Tint = new Color(0.6f, 0.85f, 1f),
                    Priority = 0.25f,
                }));
            }

            registry.Registry.Register(new Landmark(new Vector3(0f, 0f, 40f), new AtlasMarker
            {
                Kind = AtlasMarkerKind.Objective,
                Label = "North Tower",
                IconId = AtlasM1Icons.Objective,
                Tint = Color.cyan,
                Priority = 1f,
            }));

            if (worldMap != null) worldMap.SetActive(false);
        }

        private void Update()
        {
            if (AtlasM1Input.ToggleMapPressed) ToggleWorldMap();

            if (worldMapOpen) WorldMapControls();

            Look();
            Move();
            Orbit();
        }

        /// <summary>
        /// Scroll to zoom, drag to pan, R to reset.
        ///
        /// The pan is scaled by the map's own units-per-pixel, so the map moves exactly as
        /// far as the cursor did at any zoom. A fixed pan speed feels correct at one zoom
        /// level and wrong at every other, which reads as the map fighting you.
        /// </summary>
        private void WorldMapControls()
        {
            if (worldMapPresenter == null) return;

            float notches = AtlasM1Input.ScrollNotches;
            if (Mathf.Abs(notches) > 0.01f)
                worldMapPresenter.ZoomBy(Mathf.Pow(zoomStep, -notches));

            if (AtlasM1Input.DragHeld)
            {
                Vector2 drag = AtlasM1Input.DragDelta * worldMapPresenter.MapUnitsPerPixel;
                worldMapPresenter.PanBy(-drag);
            }

            if (AtlasM1Input.ResetPressed) worldMapPresenter.ResetFraming();
        }

        private void ToggleWorldMap()
        {
            worldMapOpen = !worldMapOpen;
            if (worldMap != null) worldMap.SetActive(worldMapOpen);
        }

        /// <summary>Right mouse to look, so the sample captures no cursor.</summary>
        private void Look()
        {
            if (!AtlasM1Input.LookHeld) return;

            Vector2 look = AtlasM1Input.LookDelta * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);

            Camera camera = registry.ViewerCamera;
            if (camera != null) camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>
        /// WASD moves the camera, which is what makes a minimap a minimap.
        ///
        /// A map that only ever rotates hides the half of the framing that follows the
        /// viewer, and following is the half that is easy to get wrong.
        /// </summary>
        private void Move()
        {
            Camera camera = registry.ViewerCamera;
            if (camera == null) return;

            Vector2 input = AtlasM1Input.Move;
            if (input.sqrMagnitude <= 0f) return;

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;

            camera.transform.position +=
                (forward * input.y + right * input.x) * (moveSpeed * Time.deltaTime);
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
            GUI.Label(new Rect(12f, 12f, 900f, 20f),
                "WASD to move, hold right mouse to look, [M] for the world map.");
            GUI.Label(new Rect(12f, 30f, 900f, 20f),
                worldMapOpen
                    ? "Same projection, bigger radius, no rotation.  Scroll to zoom, drag to pan, [R] to reset."
                    : "The minimap turns with you under a fixed arrow. Markers past its edge pin to the circle.");
        }

        /// <summary>A plain class implementing the interface: no MonoBehaviour, no base
        /// class, no component on anything.</summary>
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
    }
}
