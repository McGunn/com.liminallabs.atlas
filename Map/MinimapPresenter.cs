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

        [Header("Fog")]
        [Tooltip("Draws the space's reveal mask over the map. Needs a Raw Image above the " +
                 "background and below the markers.")]
        [SerializeField] private RawImage fog;

        [Tooltip("Colour of the unseen. Alpha is what actually hides the map.")]
        [SerializeField] private Color fogColor = new Color(0.03f, 0.04f, 0.06f, 0.93f);

        [Tooltip("Seconds between texture rebuilds. The mask itself only changes on " +
                 "AtlasDiscovery's own timer, so this is a second ceiling, not a poll.")]
        [SerializeField, Min(0.05f)] private float fogRefreshInterval = 0.25f;

        [Tooltip("Cells of blur on the fog edge. 0 is a hard boundary the cells are " +
                 "visible in; 2 or 3 reads as fog rather than as a grid.")]
        [SerializeField, Range(0, 6)] private int fogSoftness = 2;

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

        [Header("Crowding")]
        [Tooltip("Hide low-importance markers as the map zooms out. Off by default, " +
                 "because a project that authored no importance would watch its markers " +
                 "vanish and have no idea why.")]
        [SerializeField] private bool autoImportanceLod;

        [Tooltip("Map units of span per point of importance. A marker with importance 1 " +
                 "survives until the frame spans this much.")]
        [SerializeField, Min(1f)] private float importancePerSpan = 400f;

        private RectTransform area;
        private Entry[] pool;
        private MapProjection projection;

        private readonly List<AtlasSolve> drawn = new List<AtlasSolve>();
        private Texture2D fogTexture;
        private Color32[] fogPixels;
        private float[] fogCoverage;
        private float[] fogScratch;
        private int fogVersion = -1;
        private float nextFogRefresh;

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

        /// <summary>
        /// Which markers this map draws. Unfiltered by default.
        ///
        /// A legend's checkboxes write here, and so does zoom LOD - see
        /// <see cref="AutoImportanceLod"/>, which is the version most maps want.
        /// </summary>
        public AtlasFilter Filter
        {
            get => Projection.Filter;
            set => Projection.Filter = value;
        }

        /// <summary>
        /// Raises the importance floor as the map zooms out.
        ///
        /// Off by default, because a game that authored no importance would watch its
        /// markers vanish as it zoomed out and have no idea why. On, it is the crowding
        /// fix the design named: the floor scales with how much world is on screen, so a
        /// continent shows its cities and a street shows its shops without anyone writing
        /// a rule per marker.
        /// </summary>
        public bool AutoImportanceLod
        {
            get => autoImportanceLod;
            set => autoImportanceLod = value;
        }

        /// <summary>Map units per importance point, when <see cref="AutoImportanceLod"/>
        /// is on. A marker with importance 1 survives until the frame spans this much.</summary>
        public float ImportancePerSpan
        {
            get => importancePerSpan;
            set => importancePerSpan = Mathf.Max(1f, value);
        }

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

            // Kept so the map can be asked what is under a point after the fact. The solve
            // list the registry hands over is reused between frames, so holding a reference
            // to it would be reading next frame's answers; these are copied.
            drawn.Clear();

            // Set for the *next* solve, not this one - the list in hand was filtered with
            // the previous frame's span. One frame of lag on a zoom threshold is invisible;
            // re-solving to avoid it would not be.
            if (autoImportanceLod)
            {
                AtlasFilter filter = Projection.Filter;
                filter.MinimumImportance = Mathf.Max(0f, frame.Span / importancePerSpan - 1f);
                Projection.Filter = filter;
            }

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

                drawn.Add(solve);

                if (!entry.Object.activeSelf) entry.Object.SetActive(true);
            }

            for (int i = shown; i < pool.Length; i++)
            {
                if (pool[i].Object.activeSelf) pool[i].Object.SetActive(false);
            }

            PresentBackground(frame, bounds);
            PresentFog(frame);
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
            // expressed as a fraction of those bounds - which is exactly what a uvRect is.
            // Shared with the fog, because they are two images over one extent.
            background.uvRect = BoundsWindow(space, frame);

            // Rotated by the same number the markers were, in the same direction. A
            // background that turns the other way is the most disorienting single defect a
            // minimap can have, and it is one sign away at every moment.
            background.rectTransform.localRotation = Quaternion.Euler(0f, 0f, frame.Rotation);
        }

        /// <summary>
        /// Draws the reveal mask over the map.
        ///
        /// A texture built from the bits rather than a shader, so it works on every render
        /// pipeline with no material to ship, no keyword to enable and nothing to break
        /// when a project upgrades URP. Bilinear filtering softens the cell edges for
        /// free - a game that wants a painterly fog samples <see cref="AtlasReveal"/>
        /// itself and draws whatever it likes, which is why the data stays exact.
        ///
        /// Rebuilt only when the mask's version changes, and at most on an interval. The
        /// mask is filled in on a timer and not at all while the viewer stands still, so
        /// in practice this uploads a texture a few times a second while walking and never
        /// while stopped.
        /// </summary>
        private void PresentFog(in AtlasMapFrame frame)
        {
            if (fog == null) return;

            AtlasSpace space = registry != null
                ? registry.Registry.Spaces.GetOrDefault(frame.Space)
                : null;

            AtlasReveal reveal = space?.Reveal;
            if (reveal == null)
            {
                if (fog.enabled) fog.enabled = false;
                return;
            }

            if (!fog.enabled) fog.enabled = true;

            if (reveal.Version != fogVersion && Time.unscaledTime >= nextFogRefresh)
            {
                RebuildFogTexture(reveal);
                fogVersion = reveal.Version;
                nextFogRefresh = Time.unscaledTime + fogRefreshInterval;
            }

            if (fogTexture == null) return;

            fog.texture = fogTexture;
            fog.color = Color.white;

            // The same uv window and the same rotation as the background, because the fog
            // is indexed against the same bounds. Computing it separately here is how fog
            // comes to slide against the terrain it is meant to hide.
            fog.uvRect = BoundsWindow(space, frame);
            fog.rectTransform.localRotation = Quaternion.Euler(0f, 0f, frame.Rotation);
        }

        private void RebuildFogTexture(AtlasReveal reveal)
        {
            int width = reveal.Width;
            int height = reveal.Height;

            if (fogTexture == null || fogTexture.width != width || fogTexture.height != height)
            {
                if (fogTexture != null) Destroy(fogTexture);

                fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                fogPixels = new Color32[width * height];
                fogCoverage = new float[width * height];
                fogScratch = new float[width * height];
            }

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    fogCoverage[row + x] = reveal.IsRevealed(x, y) ? 0f : 1f;
            }

            if (fogSoftness > 0) Blur(width, height, fogSoftness);

            var tint = (Color32)fogColor;
            float alpha = fogColor.a * 255f;

            for (int i = 0; i < fogPixels.Length; i++)
            {
                fogPixels[i] = new Color32(
                    tint.r, tint.g, tint.b, (byte)Mathf.Clamp(fogCoverage[i] * alpha, 0f, 255f));
            }

            fogTexture.SetPixels32(fogPixels);
            fogTexture.Apply(false);
        }

        /// <summary>
        /// A separable box blur over the coverage, which is what turns a grid of bits into
        /// something that reads as fog.
        ///
        /// Separable because a radius-3 box is 49 samples done naively and 14 done in two
        /// passes, and this runs over 65,000 cells a few times a second while walking.
        /// Outside the mask counts as revealed, matching
        /// <see cref="AtlasReveal.IsRevealed"/> - so the fog fades out at the map's edge
        /// rather than being clipped hard against it.
        ///
        /// Done here rather than in a shader on purpose: the package ships no material, no
        /// keyword and nothing that breaks when a project changes render pipeline, and a
        /// blur this cheap does not need the GPU.
        /// </summary>
        private void Blur(int width, int height, int radius)
        {
            float span = radius * 2 + 1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int at = x + k;
                        sum += at < 0 || at >= width ? 0f : fogCoverage[row + at];
                    }
                    fogScratch[row + x] = sum / span;
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int at = y + k;
                        sum += at < 0 || at >= height ? 0f : fogScratch[at * width + x];
                    }
                    fogCoverage[y * width + x] = sum / span;
                }
            }
        }

        private void OnDestroy()
        {
            // Created with HideAndDontSave, so nothing else will collect it.
            if (fogTexture != null) Destroy(fogTexture);
        }

        /// <summary>
        /// The visible window as a uv rect over a space's bounds.
        ///
        /// Shared by the background and the fog on purpose: they are two images indexed
        /// against one extent, and two copies of this arithmetic is two chances for them to
        /// disagree by a fraction that reads as the fog lagging the terrain.
        /// </summary>
        private static Rect BoundsWindow(AtlasSpace space, in AtlasMapFrame frame)
        {
            Vector2 min = space.ToMap(space.WorldBounds.min);
            Vector2 max = space.ToMap(space.WorldBounds.max);
            Vector2 size = max - min;

            if (Mathf.Abs(size.x) < AtlasMath.Epsilon || Mathf.Abs(size.y) < AtlasMath.Epsilon)
                return new Rect(0f, 0f, 1f, 1f);

            var window = new Vector2(frame.Span / Mathf.Abs(size.x), frame.Span / Mathf.Abs(size.y));
            var centre = new Vector2((frame.Centre.x - min.x) / size.x,
                                     (frame.Centre.y - min.y) / size.y);

            return new Rect(centre - window * 0.5f, window);
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

        // ---- interaction ------------------------------------------------------

        /// <summary>
        /// The marker nearest a point in this map's local space, within a radius.
        ///
        /// Hit-tested against where markers were <b>drawn</b> rather than where their
        /// targets are, which is the only version that is correct: a marker pinned to the
        /// edge is not at its target's map position, and a player clicking the pinned icon
        /// means the pinned icon.
        ///
        /// Nearest rather than first-inside, so overlapping markers resolve to the one the
        /// cursor is actually closest to instead of to whichever happened to be drawn
        /// first.
        /// </summary>
        public bool TryGetMarkerAt(Vector2 localPoint, out AtlasSolve solve, float radius = 24f)
        {
            solve = default;
            if (pool == null) return false;

            Rect bounds = area.rect;

            // anchoredPosition runs from the rect's corner; a local point from
            // RectTransformUtility runs from its pivot. Converting here rather than asking
            // callers to is the difference between this being usable and being a trap.
            Vector2 fromCorner = new Vector2(localPoint.x - bounds.xMin, localPoint.y - bounds.yMin);

            float best = radius * radius;
            bool found = false;

            for (int i = 0; i < drawn.Count && i < pool.Length; i++)
            {
                float distance = (pool[i].Rect.anchoredPosition - fromCorner).sqrMagnitude;
                if (distance > best) continue;

                best = distance;
                solve = drawn[i];
                found = true;
            }

            return found;
        }

        /// <summary>
        /// The world position a point on this map corresponds to, on the space's plane.
        ///
        /// What a player-placed waypoint needs: click the map, get somewhere to walk to.
        /// The height is the space's floor, because a map has no opinion about altitude and
        /// guessing one from the terrain would be a raycast this component has no business
        /// doing.
        /// </summary>
        public bool TryGetWorldPosition(Vector2 localPoint, out Vector3 world)
        {
            world = default;
            if (area == null || registry == null) return false;

            Rect bounds = area.rect;
            if (bounds.width < AtlasMath.Epsilon || bounds.height < AtlasMath.Epsilon) return false;

            AtlasMapFrame frame = Projection.LastFrame;

            // Screen fraction, then undo the frame: rotate the other way and scale back out.
            var fraction = new Vector2((localPoint.x - bounds.xMin) / bounds.width - 0.5f,
                                       (localPoint.y - bounds.yMin) / bounds.height - 0.5f);

            Vector2 onPlane = frame.Centre + AtlasMath.RotateMap(fraction * frame.Span, -frame.Rotation);

            AtlasSpace space = registry.Registry.Spaces.GetOrDefault(frame.Space);

            // The default plane is world XZ, so the inverse is a swap. A space with its own
            // WorldToMap would need that matrix inverted, which is why this reports failure
            // rather than guessing.
            if (space == null) return false;

            world = new Vector3(onPlane.x, space.FloorHeight, onPlane.y);
            return true;
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
