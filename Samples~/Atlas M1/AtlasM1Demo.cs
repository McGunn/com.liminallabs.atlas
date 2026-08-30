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

        private Vector3 waypointPosition;
        private AtlasHandle waypointHandle;
        private bool hasWaypoint;

        private Vector2 pressedAt;
        private bool dragMoved;
        private string mapMessage = "";

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

            // Press, drag, release. A click and a drag start identically, so which one it
            // was is only knowable at release - hence the movement threshold rather than
            // acting on press. Acting on press would place a waypoint every time someone
            // began panning, which is the single most annoying way to get this wrong.
            if (AtlasM1Input.DragPressed)
            {
                pressedAt = AtlasM1Input.PointerPosition;
                dragMoved = false;
            }

            if (AtlasM1Input.DragHeld)
            {
                Vector2 drag = AtlasM1Input.DragDelta * worldMapPresenter.MapUnitsPerPixel;
                worldMapPresenter.PanBy(-drag);

                if ((AtlasM1Input.PointerPosition - pressedAt).sqrMagnitude > ClickSlop * ClickSlop)
                    dragMoved = true;
            }

            if (AtlasM1Input.DragReleased && !dragMoved) ClickMap(AtlasM1Input.PointerPosition);

            if (AtlasM1Input.ClearPressed) ClearWaypoint();
            if (AtlasM1Input.ResetPressed) worldMapPresenter.ResetFraming();
        }

        /// <summary>Pixels of movement that still counts as a click rather than a drag.</summary>
        private const float ClickSlop = 6f;

        /// <summary>
        /// A click on the map: name what is under it, or drop a waypoint where it is.
        ///
        /// Marker first, empty space second - clicking an objective to ask about it is a
        /// more common intent than dropping a waypoint exactly on top of one, and the map
        /// hit-tests where markers were <i>drawn</i>, so clicking a pinned icon at the edge
        /// finds the marker it stands for rather than whatever is at that map position.
        /// </summary>
        private void ClickMap(Vector2 screenPosition)
        {
            var rect = (RectTransform)worldMapPresenter.transform;

            // Screen-space-overlay canvases pass a null camera; anything else needs its
            // own. Getting this wrong puts every click in the corner.
            Canvas canvas = worldMapPresenter.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, screenPosition, uiCamera, out Vector2 local))
            {
                return;
            }

            if (!rect.rect.Contains(local)) return;

            if (worldMapPresenter.TryGetMarkerAt(local, out AtlasSolve solve))
            {
                mapMessage = $"{solve.Marker.Label ?? solve.Marker.Kind.ToString()} — " +
                             $"{solve.Distance:0} m, {Describe(solve.Level)}";
                return;
            }

            if (worldMapPresenter.TryGetWorldPosition(local, out Vector3 world)) PlaceWaypoint(world);
        }

        private static string Describe(AtlasElevation level) =>
            level == AtlasElevation.Above ? "above you"
            : level == AtlasElevation.Below ? "below you"
            : "on your level";

        /// <summary>
        /// The delegate entry point, doing the thing it exists for.
        ///
        /// A player-placed waypoint has no GameObject and never needs one: it is a position
        /// and a marker. Tracking it through Track(() =&gt; position, ...) is the same
        /// mechanism a strategy game uses for ten thousand units, at a count of one.
        /// </summary>
        private void PlaceWaypoint(Vector3 world)
        {
            waypointPosition = world;
            mapMessage = $"Waypoint at {world.x:0}, {world.z:0}";

            if (hasWaypoint) return;

            waypointHandle = registry.Registry.Track(() => waypointPosition, new AtlasMarker
            {
                Kind = AtlasMarkerKind.Waypoint,
                Label = "Waypoint",
                IconId = AtlasM1Icons.Objective,
                Tint = new Color(0.4f, 1f, 0.6f),
                Priority = 2f,          // above the landmarks, so declutter moves them
            }, AtlasSpaceId.Default);

            hasWaypoint = true;
        }

        private void ClearWaypoint()
        {
            if (!hasWaypoint) return;

            registry.Registry.Release(waypointHandle);
            hasWaypoint = false;
            mapMessage = "Waypoint cleared";
        }

        /// <summary>
        /// Releases the waypoint handle.
        ///
        /// A delegate has no component to unregister it, so releasing is the caller's job -
        /// and forgetting leaves the registry calling into a closure that outlived its
        /// scene, which is the one way the delegate entry point can bite.
        /// </summary>
        private void OnDestroy()
        {
            if (hasWaypoint && registry != null) registry.Registry.Release(waypointHandle);
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
            if (worldMapOpen && mapMessage.Length > 0)
                GUI.Label(new Rect(12f, 48f, 900f, 20f), mapMessage);

            GUI.Label(new Rect(12f, 30f, 900f, 20f),
                worldMapOpen
                    ? "Scroll to zoom, drag to pan, click a marker or empty space, [C] clears, [R] resets."
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
