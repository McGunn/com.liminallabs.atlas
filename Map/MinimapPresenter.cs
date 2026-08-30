using System.Collections.Generic;
using LiminalLabs.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// A map, drawn on a rect: the space's image beneath, markers on top, and the viewer
    /// at the centre.
    ///
    /// <b>One component for the minimap and the world map.</b> Which it is lives on the
    /// <see cref="MapProjection"/> that feeds it - a small radius that follows the viewer,
    /// or a large one that frames the space - and this draws either without knowing the
    /// difference. That is the design's claim about maps made real: not two systems, one
    /// projection with two framings.
    ///
    /// Reads <see cref="AtlasSolve.MapPoint"/> and the frame from its projection. Like
    /// every other presenter it never touches a camera or a tracked object's transform.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Minimap Presenter")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MinimapPresenter : MonoBehaviour, IAtlasPresenter
    {
        [Header("Shape")]
        [Tooltip("Round maps clamp to a circle, square maps to the rect. It has to match " +
                 "the art or markers bunch where the shape is not.")]
        [SerializeField] private bool round = true;

        [Tooltip("Clip children to this rect. Off if a parent Mask already does it.")]
        [SerializeField] private bool clipToRect = true;

        [Header("Markers")]
        [SerializeField] private Vector2 markerSize = new Vector2(24f, 24f);

        [Tooltip("How far inside the edge a pinned marker sits, as a fraction of the map.")]
        [SerializeField, Range(0f, 0.45f)] private float edgeMargin = 0.06f;

        [Tooltip("Pin markers beyond the edge to it. Off hides them instead.")]
        [SerializeField] private bool pinOutsideMarkers = true;

        [Header("Pool")]
        [Tooltip("Must be at least the registry's MaxMarkers. Allocated once, at Awake.")]
        [SerializeField, Min(1)] private int poolSize = 48;

        [Header("Icons")]
        [SerializeField] private AtlasSpriteIcons icons;

        [Header("Distance")]
        [Tooltip("Alpha against Fade, which is already 1 near and 0 at the cull distance.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField, Range(0.1f, 2f)] private float minScale = 0.85f;
        [SerializeField, Range(0.1f, 2f)] private float maxScale = 1f;

        [Header("Background")]
        [Tooltip("Draws the space's Image, panned and rotated with the frame. Baking is M3; " +
                 "until then assign a Texture on the space by hand.")]
        [SerializeField] private RawImage background;

        [Header("Viewer")]
        [Tooltip("Drawn at the centre. Rotates to the viewer's facing on a north-up map, " +
                 "and stays pointing up on a viewer-up one.")]
        [SerializeField] private RectTransform viewerArrow;

        [Header("Registry")]
        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Tooltip("Register with the registry automatically. Turn off to wire it in code.")]
        [SerializeField] private bool selfRegister = true;

        [Header("Framing")]
        [Tooltip("Half the visible span in map units. Ignored when Centre is SpaceBounds.")]
        [SerializeField, Min(1f)] private float radius = 60f;

        [SerializeField] private AtlasMapCentre centre = AtlasMapCentre.Viewer;
        [SerializeField] private AtlasMapRotation rotation = AtlasMapRotation.ViewerUp;

        private RectTransform area;
        private Entry[] pool;
        private MapProjection projection;

        /// <summary>Where icons come from. Assign in code to use a provider that is not
        /// a sprite array - the seam is the point.</summary>
        public IAtlasIconProvider IconProvider { get; set; }

        /// <summary>The projection feeding this. Exposed so pan and zoom at M2 have one
        /// object to move, and so a world map's buttons do not each keep their own idea of
        /// where the map is.</summary>
        public MapProjection Projection => projection ?? (projection = BuildProjection());

        public int Capacity => pool != null ? pool.Length : poolSize;

        public bool SelfRegister
        {
            get => selfRegister;
            set => selfRegister = value;
        }

        /// <summary>Half the visible span, in map units. Setting it re-frames the next
        /// tick; nothing is rebuilt.</summary>
        public float Radius
        {
            get => radius;
            set
            {
                radius = Mathf.Max(1f, value);
                Projection.Radius = radius;
            }
        }

        /// <summary>
        /// Multiplies the framed radius. 1 is the authored size, smaller is zoomed in.
        ///
        /// A multiplier rather than a second radius, so zoom works the same on a map framed
        /// to a space's bounds as on one with an authored radius - "show the whole map" and
        /// "zoomed in two steps" have to be able to be true at the same time.
        /// </summary>
        public float Zoom
        {
            get => Projection.Zoom;
            set => Projection.Zoom = value;
        }

        /// <summary>
        /// Zooms about the centre by a multiplicative step.
        ///
        /// Multiplicative because zoom is perceived that way: a fixed additive step is
        /// glacial when zoomed out and jumps a whole map when zoomed in, and the wheel
        /// feels broken at one end or the other.
        /// </summary>
        public void ZoomBy(float step) => Zoom *= Mathf.Max(0.0001f, step);

        /// <summary>Moves the frame, in map units. Only meaningful on a map that is not
        /// following the viewer, which would re-centre it next tick.</summary>
        public void PanBy(Vector2 delta) => Projection.Pan += delta;

        /// <summary>Back to the authored framing: no zoom, no pan.</summary>
        public void ResetFraming()
        {
            Projection.Zoom = 1f;
            Projection.Pan = Vector2.zero;
        }

        /// <summary>Map units per unit of rect. What a drag in pixels has to be multiplied
        /// by to pan the map under the cursor exactly.</summary>
        public float MapUnitsPerPixel
        {
            get
            {
                Rect bounds = area != null ? area.rect : new Rect(0f, 0f, 1f, 1f);
                float across = Mathf.Max(bounds.width, AtlasMath.Epsilon);
                return Projection.LastFrame.Span / across;
            }
        }

        private MapProjection BuildProjection() => new MapProjection
        {
            Centre = centre,
            Rotation = rotation,
            Radius = radius,
        };

        private void OnEnable()
        {
            if (!selfRegister) return;

            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);
            if (registry == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry. Assign one, or put an " +
                    "AtlasRegistryBehaviour on a parent or in the scene.", this);
                return;
            }

            registry.AddProjection(Projection, this);
        }

        private void OnDisable()
        {
            if (registry != null) registry.Registry.RemoveProjection(this);
        }

        private void Awake()
        {
            area = (RectTransform)transform;
            if (IconProvider == null) IconProvider = icons;

            if (clipToRect && GetComponent<RectMask2D>() == null && GetComponent<Mask>() == null)
                gameObject.AddComponent<RectMask2D>();

            BuildPool();
        }

        private void BuildPool()
        {
            pool = new Entry[Mathf.Max(1, poolSize)];

            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Marker " + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(area, false);

                var rect = (RectTransform)go.transform;

                // Anchored to the bottom-left corner, so anchoredPosition runs 0..size and
                // a map fraction multiplies straight into it. Anchoring to the centre and
                // then subtracting half the rect is the same arithmetic with one more step
                // to get the sign of wrong in.
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = markerSize;

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;

                go.SetActive(false);
                pool[i] = new Entry(rect, image);
            }
        }

        public void Present(in AtlasViewer viewer, IReadOnlyList<AtlasSolve> solves)
        {
            if (pool == null) return;

            Rect bounds = area.rect;
            AtlasMapFrame frame = Projection.LastFrame;
            int shown = 0;

            for (int i = 0; i < solves.Count && shown < pool.Length; i++)
            {
                AtlasSolve solve = solves[i];
                if (solve.Fade <= 0f) continue;

                Vector2 point = solve.MapPoint;
                bool outside = round
                    ? (point - Half).magnitude > 0.5f - edgeMargin
                    : Mathf.Abs(point.x - 0.5f) > 0.5f - edgeMargin ||
                      Mathf.Abs(point.y - 0.5f) > 0.5f - edgeMargin;

                if (outside)
                {
                    if (!pinOutsideMarkers) continue;

                    point = round
                        ? AtlasMath.ClampToCircle(point, edgeMargin, out _)
                        : ClampToBox(point, edgeMargin);
                }

                Entry entry = pool[shown++];

                entry.Rect.anchoredPosition = new Vector2(
                    point.x * bounds.width, point.y * bounds.height);

                float fade = Mathf.Clamp01(fadeCurve.Evaluate(Mathf.Clamp01(solve.Fade)));
                entry.Rect.localScale = Vector3.one * AtlasMath.DistanceScale(fade, minScale, maxScale);

                Sprite sprite = solve.Marker.IconOverride != null
                    ? solve.Marker.IconOverride
                    : IconProvider != null ? IconProvider.Resolve(solve.Marker.IconId) : null;

                Color tint = solve.Marker.Tint;

                if (sprite == null)
                {
                    sprite = LiminalPlaceholder.Missing;
                    if (sprite != null) tint = LiminalPlaceholder.Tint;
                }

                entry.Image.sprite = sprite;
                entry.Image.enabled = true;

                tint.a *= fade;
                entry.Image.color = tint;

                if (!entry.Object.activeSelf) entry.Object.SetActive(true);
            }

            for (int i = shown; i < pool.Length; i++)
            {
                if (pool[i].Object.activeSelf) pool[i].Object.SetActive(false);
            }

            PresentBackground(frame, bounds);
            PresentViewerArrow(viewer, frame);
        }

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

        /// <summary>Square clamp, keeping the direction. The round case is
        /// <see cref="AtlasMath.ClampToCircle"/>; this is its rectangular twin and lives
        /// here because only a square map wants it.</summary>
        private static Vector2 ClampToBox(Vector2 point, float margin)
        {
            Vector2 direction = point - Half;
            float limit = 0.5f - margin;

            float scaleX = limit / Mathf.Max(Mathf.Abs(direction.x), AtlasMath.Epsilon);
            float scaleY = limit / Mathf.Max(Mathf.Abs(direction.y), AtlasMath.Epsilon);

            return Half + direction * Mathf.Min(scaleX, scaleY);
        }

        /// <summary>
        /// Pans and rotates the space's image under the markers.
        /// </summary>
        private void PresentBackground(in AtlasMapFrame frame, Rect bounds)
        {
            if (background == null) return;

            AtlasSpace space = registry != null
                ? registry.Registry.Spaces.GetOrDefault(frame.Space)
                : null;

            Texture image = space != null ? space.Image : null;
            if (image == null)
            {
                if (background.enabled) background.enabled = false;
                return;
            }

            if (!background.enabled) background.enabled = true;
            background.texture = image;

            // The image covers the space's bounds, so the visible window is the frame
            // expressed as a fraction of those bounds. uvRect is in 0..1 of the texture,
            // which is exactly that fraction - no second copy of the framing maths.
            Vector2 min = space.ToMap(space.WorldBounds.min);
            Vector2 max = space.ToMap(space.WorldBounds.max);
            Vector2 size = max - min;

            if (Mathf.Abs(size.x) < AtlasMath.Epsilon || Mathf.Abs(size.y) < AtlasMath.Epsilon)
            {
                background.enabled = false;
                return;
            }

            Vector2 window = new Vector2(frame.Span / Mathf.Abs(size.x), frame.Span / Mathf.Abs(size.y));
            Vector2 centreUv = new Vector2((frame.Centre.x - min.x) / size.x,
                                           (frame.Centre.y - min.y) / size.y);

            background.uvRect = new Rect(centreUv - window * 0.5f, window);

            // Rotated by the same number the markers were, in the same direction. A
            // background that turns the other way is the most disorienting single defect a
            // minimap can have, and it is one sign away at every moment.
            background.rectTransform.localRotation = Quaternion.Euler(0f, 0f, frame.Rotation);
        }

        /// <summary>
        /// The viewer, at the centre.
        ///
        /// On a viewer-up map it never turns, because the map turned instead. On a
        /// north-up map it carries the whole of the viewer's facing. Both fall out of the
        /// frame's own rotation rather than from a second look at the camera.
        /// </summary>
        private void PresentViewerArrow(in AtlasViewer viewer, in AtlasMapFrame frame)
        {
            if (viewerArrow == null) return;

            float facing = AtlasMath.BearingOfDirection(viewer, Vector3.forward);
            viewerArrow.localRotation = Quaternion.Euler(0f, 0f, facing + frame.Rotation);
        }

        public int VisibleCount
        {
            get
            {
                if (pool == null) return 0;

                int count = 0;
                for (int i = 0; i < pool.Length; i++)
                    if (pool[i].Object.activeSelf) count++;
                return count;
            }
        }

        /// <summary>The anchored position of the nth visible marker, for tests.</summary>
        public Vector2 VisiblePosition(int index) =>
            pool != null && index >= 0 && index < pool.Length
                ? pool[index].Rect.anchoredPosition
                : Vector2.zero;

        private readonly struct Entry
        {
            public readonly RectTransform Rect;
            public readonly Image Image;
            public readonly GameObject Object;

            public Entry(RectTransform rect, Image image)
            {
                Rect = rect;
                Image = image;
                Object = rect.gameObject;
            }
        }
    }
}
