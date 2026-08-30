using System.Collections.Generic;
using LiminalLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// A compass strip: markers slide left and right along a bar as the viewer turns,
    /// with cardinal letters sliding through them.
    ///
    /// Reads <see cref="AtlasSolve.Bearing"/> and the frame's viewer, and nothing else.
    /// It does not know what a camera is, never touches a tracked object's
    /// <c>Transform</c>, and does not reference the screen-indicator assembly - which is
    /// what makes the two agree about what is behind you rather than merely usually
    /// agreeing.
    ///
    /// <b>Markers slide off the ends and are clipped, not hidden.</b> An earlier version
    /// hid them the instant they passed the bar's field of view, on the argument that a
    /// marker clamped to the end lies about where the thing is. That argument was right
    /// about clamping and wrong about the remedy: hiding pops, and a marker that vanishes
    /// a pixel before the edge reads as a bug. Sliding out under a mask neither lies nor
    /// pops, so a slot is only released once its marker is fully past the edge.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Bar Presenter")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BarPresenter : MonoBehaviour, IAtlasPresenter
    {
        [Header("Bar")]
        [Tooltip("How many degrees the full width of the bar covers. 180 shows everything in front.")]
        [SerializeField, Range(20f, 360f)] private float barFieldOfView = 180f;

        [Tooltip("Size of each marker icon, in bar-local units.")]
        [SerializeField] private Vector2 markerSize = new Vector2(32f, 32f);

        [Tooltip("Vertical offset of markers within the bar.")]
        [SerializeField] private float markerY;

        [Tooltip("Clip anything that slides past the ends. Off only if a parent already masks.")]
        [SerializeField] private bool clipToBar = true;

        [Header("Pool")]
        [Tooltip("Must be at least the registry's MaxMarkers. Allocated once, at Awake.")]
        [SerializeField, Min(1)] private int poolSize = 32;

        [Header("Icons")]
        [SerializeField] private AtlasSpriteIcons icons;

        [Header("Distance")]
        [Tooltip("Alpha against Fade, which is already 1 near and 0 at the cull distance.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Scale at the far edge of visibility.")]
        [SerializeField, Range(0.1f, 2f)] private float minScale = 0.8f;

        [Tooltip("Scale up close.")]
        [SerializeField, Range(0.1f, 2f)] private float maxScale = 1.2f;

        [Header("Labels")]
        [SerializeField] private bool showDistanceLabels = true;

        [Tooltip("{0} is the distance in metres, already rounded.")]
        [SerializeField] private string distanceFormat = "{0}m";

        [SerializeField] private float labelSize = 14f;
        [SerializeField] private float labelOffsetY = -22f;

        [Header("Cardinal Letters")]
        [SerializeField] private bool showDirections = true;

        [Tooltip("Adds NE, SE, SW and NW to the four cardinals.")]
        [SerializeField] private bool includeDiagonals;

        [SerializeField] private Color directionColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private float directionSize = 18f;
        [SerializeField] private float directionY = 4f;

        [Header("Idle Fade")]
        [Tooltip("Dim the whole bar while the viewer is still. Needs a CanvasGroup.")]
        [SerializeField] private bool fadeWhenIdle;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.35f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 4f;

        [Header("Registry")]
        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Tooltip("Register with the registry automatically. Turn off to wire it in code.")]
        [SerializeField] private bool selfRegister = true;

        private RectTransform bar;
        private Entry[] pool;
        private Direction[] directions;
        private TMP_FontAsset font;

        private AtlasViewer lastViewer;
        private bool hasLastViewer;

        /// <summary>Where icons come from. Assign in code to use a provider that is not
        /// a sprite array - the seam is the point.</summary>
        public IAtlasIconProvider IconProvider { get; set; }

        /// <summary>Degrees the bar spans. Markers beyond half of this slide off the
        /// ends and are clipped.</summary>
        public float BarFieldOfView
        {
            get => barFieldOfView;
            set => barFieldOfView = Mathf.Clamp(value, 1f, 360f);
        }

        /// <summary>How many markers this can draw at once. A frame with more is
        /// truncated by the registry before it gets here.</summary>
        public int Capacity => pool != null ? pool.Length : poolSize;

        /// <summary>
        /// Whether this registers itself when enabled.
        ///
        /// Settable so a game with several viewers can wire projections explicitly, and so
        /// a test can assert one known pairing rather than whatever the scene search
        /// found. Set it before the component is enabled - create the object inactive,
        /// configure, then activate.
        /// </summary>
        public bool SelfRegister
        {
            get => selfRegister;
            set => selfRegister = value;
        }

        /// <summary>
        /// Registers itself, so dropping the component in a scene is enough.
        ///
        /// The alternative was a line of code per presenter per scene -
        /// <c>registry.AddProjection(new BearingProjection(), this)</c> - and a presenter
        /// that looked correctly configured, drew nothing, and reported nothing when that
        /// line was missing. Marker components already register themselves in OnEnable;
        /// there was no reason for the drawing half to be different.
        /// </summary>
        private void OnEnable()
        {
            if (!selfRegister) return;

            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);
            if (registry == null)
            {
                // Named, because the alternative is a compass bar that silently never
                // appears and a developer checking their icon ids.
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry. Assign one, or put an " +
                    "AtlasRegistryBehaviour on a parent or in the scene.", this);
                return;
            }

            registry.AddProjection(new BearingProjection(), this);
        }

        private void OnDisable()
        {
            // Unregisters on disable, destroy and scene unload alike, so a registry that
            // outlives a HUD is never left presenting into a destroyed pool.
            if (registry != null) registry.Registry.RemoveProjection(this);
            hasLastViewer = false;
        }

        private void Awake()
        {
            bar = (RectTransform)transform;
            if (IconProvider == null) IconProvider = icons;

            if (clipToBar && GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            if (fadeWhenIdle && canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            // Resolved before the pool is built: if TMP cannot draw, the labels are
            // never created rather than created broken.
            if (!TryResolveFont(out font)) showDistanceLabels = false;

            BuildPool();
            BuildDirections();
        }

        /// <summary>
        /// A font for the labels, or false when this project cannot render TMP text at all.
        ///
        /// <c>TMP_Settings.defaultFontAsset</c> dereferences its instance without checking
        /// it, so when TMP Essential Resources have not been imported it does not return
        /// null - it throws. From <c>Awake</c>, that killed the whole presenter and took
        /// every marker with it, which is a spectacular price for a missing distance label.
        ///
        /// Converting core's vendored typeface is no escape either: the material wants
        /// TextMeshPro's SDF shader, which arrives with the same import. So when the
        /// settings are absent the labels are not built at all, rather than built and left
        /// invisible - a label nobody can see is harder to diagnose than one that was never
        /// there and said why.
        /// </summary>
        private bool TryResolveFont(out TMP_FontAsset resolved)
        {
            resolved = null;

            TMP_Settings settings;
            try
            {
                settings = TMP_Settings.instance;
            }
            catch
            {
                settings = null;
            }

            if (settings == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' is turning distance labels off: this project has no " +
                    "TMP Settings, so TextMeshPro cannot draw. Import them once from " +
                    "Window > TextMeshPro > Import TMP Essential Resources. Markers, " +
                    "bearings and everything else are unaffected.", this);
                return false;
            }

            resolved = TMP_Settings.defaultFontAsset;
            if (resolved != null) return true;

            // No project default, but TMP itself works - so core's vendored face is a
            // genuine answer here rather than a guess.
            Font fallback = LiminalFonts.Get(LiminalFontRole.Sans);
            if (fallback != null) resolved = TMP_FontAsset.CreateFontAsset(fallback);

            if (resolved == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' has no TMP font asset and core's fallback could not " +
                    "be loaded, so labels are off. Assign a default under " +
                    "Project Settings > TextMeshPro.", this);
            }

            return resolved != null;
        }


        /// <summary>
        /// Every marker object made once, up front.
        ///
        /// A HUD that instantiates when something new comes into view spikes on exactly
        /// the frame the player needed it - a fight starting, a waypoint appearing. The
        /// pool is a fixed array and <see cref="Present"/> only ever moves and hides what
        /// is already there.
        /// </summary>
        private void BuildPool()
        {
            pool = new Entry[Mathf.Max(1, poolSize)];

            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Marker " + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(bar, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = markerSize;

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;

                TextMeshProUGUI label = showDistanceLabels ? BuildLabel(rect) : null;

                go.SetActive(false);
                pool[i] = new Entry(rect, image, label);
            }
        }

        private TextMeshProUGUI BuildLabel(RectTransform parent)
        {
            var go = new GameObject("Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80f, 20f);
            rect.anchoredPosition = new Vector2(0f, labelOffsetY);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = labelSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            return text;
        }

        /// <summary>
        /// The cardinal letters, built once.
        ///
        /// North is +Z and east is +X, which is Unity's world convention rather than an
        /// opinion of this package. A game whose world is laid out to a different compass
        /// rotates its own root; it does not want the letters renamed.
        /// </summary>
        private void BuildDirections()
        {
            if (!showDirections)
            {
                directions = new Direction[0];
                return;
            }

            var cardinals = new List<Direction>(8)
            {
                new Direction("N", Vector3.forward),
                new Direction("E", Vector3.right),
                new Direction("S", Vector3.back),
                new Direction("W", Vector3.left),
            };

            if (includeDiagonals)
            {
                cardinals.Add(new Direction("NE", new Vector3(1f, 0f, 1f)));
                cardinals.Add(new Direction("SE", new Vector3(1f, 0f, -1f)));
                cardinals.Add(new Direction("SW", new Vector3(-1f, 0f, -1f)));
                cardinals.Add(new Direction("NW", new Vector3(-1f, 0f, 1f)));
            }

            directions = cardinals.ToArray();

            for (int i = 0; i < directions.Length; i++)
            {
                var go = new GameObject("Direction " + directions[i].Label,
                    typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(bar, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(48f, 24f);

                var text = go.GetComponent<TextMeshProUGUI>();
                text.font = font;
                text.text = directions[i].Label;
                text.fontSize = directionSize;
                text.color = directionColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;

                // Behind the markers: a cardinal letter is background information, and a
                // waypoint disappearing behind an N would be the wrong way round.
                go.transform.SetAsFirstSibling();

                directions[i].Rect = rect;
                directions[i].Text = text;
            }
        }

        public void Present(in AtlasViewer viewer, IReadOnlyList<AtlasSolve> solves)
        {
            if (pool == null) return;

            float half = barFieldOfView * 0.5f;
            float width = bar.rect.width;
            float edge = width * 0.5f + markerSize.x * 0.5f;
            int shown = 0;

            for (int i = 0; i < solves.Count && shown < pool.Length; i++)
            {
                AtlasSolve solve = solves[i];
                if (solve.Fade <= 0f) continue;

                float x = XForBearing(solve.Bearing, half, width);

                // Released only once fully past the edge. Anything still touching the bar
                // keeps its slot and is clipped by the mask, so it slides rather than pops.
                if (Mathf.Abs(x) > edge) continue;

                Entry entry = pool[shown++];
                entry.Rect.anchoredPosition = new Vector2(x, markerY);

                float fade = Mathf.Clamp01(fadeCurve.Evaluate(Mathf.Clamp01(solve.Fade)));
                entry.Rect.localScale = Vector3.one * AtlasMath.DistanceScale(fade, minScale, maxScale);

                Sprite sprite = solve.Marker.IconOverride != null
                    ? solve.Marker.IconOverride
                    : IconProvider != null ? IconProvider.Resolve(solve.Marker.IconId) : null;

                Color tint = solve.Marker.Tint;

                if (sprite == null)
                {
                    // Core's shared placeholder, in the editor and development builds.
                    // Drawn in its own colour rather than the marker's: a placeholder
                    // tinted cyan because the marker is cyan reads as a deliberate icon,
                    // which is the one thing it must never do. Null in a release build,
                    // where the blank quad below is the right answer instead.
                    sprite = LiminalPlaceholder.Missing;
                    if (sprite != null) tint = LiminalPlaceholder.Tint;
                }

                entry.Image.sprite = sprite;
                entry.Image.enabled = true;

                tint.a *= fade;
                entry.Image.color = tint;

                if (entry.Label != null)
                {
                    entry.Label.text = string.Format(distanceFormat, Mathf.RoundToInt(solve.Distance));
                    entry.Label.color = tint;
                }

                if (!entry.Object.activeSelf) entry.Object.SetActive(true);
            }

            // Everything the pool has left over. Hiding rather than destroying is what
            // keeps Present allocation-free.
            for (int i = shown; i < pool.Length; i++)
            {
                if (pool[i].Object.activeSelf) pool[i].Object.SetActive(false);
            }

            PresentDirections(viewer, half, width, edge);
            PresentIdleFade(viewer);
        }

        /// <summary>
        /// Linear in bearing across the bar: -half maps to the left edge, +half to the
        /// right, 0 to the centre.
        ///
        /// Public because it is the bar's whole coordinate system, and anything drawing
        /// alongside these markers - a custom overlay, a tutorial arrow - has to agree
        /// with it rather than re-deriving it and drifting.
        /// </summary>
        public float XForBearing(float bearing) =>
            XForBearing(bearing, barFieldOfView * 0.5f, bar != null ? bar.rect.width : 0f);

        private static float XForBearing(float bearing, float half, float width) =>
            half <= 0f ? 0f : bearing / half * width * 0.5f;

        private void PresentDirections(in AtlasViewer viewer, float half, float width, float edge)
        {
            if (directions == null) return;

            for (int i = 0; i < directions.Length; i++)
            {
                Direction direction = directions[i];
                if (direction.Rect == null) continue;

                float bearing = AtlasMath.BearingOfDirection(viewer, direction.World);
                float x = XForBearing(bearing, half, width);

                bool visible = Mathf.Abs(x) <= edge;
                if (direction.Rect.gameObject.activeSelf != visible)
                    direction.Rect.gameObject.SetActive(visible);

                if (visible) direction.Rect.anchoredPosition = new Vector2(x, directionY);
            }
        }

        /// <summary>
        /// Dims the bar while the viewer is still.
        ///
        /// The activity measure is computed from two frozen viewers by
        /// <see cref="AtlasMath.Activity"/> rather than read off a rigidbody or an input
        /// axis, so a compass on a cutscene camera, a drone or a replay fades on the same
        /// rule as one on a player.
        /// </summary>
        private void PresentIdleFade(in AtlasViewer viewer)
        {
            if (!fadeWhenIdle || canvasGroup == null)
            {
                lastViewer = viewer;
                hasLastViewer = true;
                return;
            }

            float target = 1f;
            if (hasLastViewer)
            {
                float activity = AtlasMath.Activity(lastViewer, viewer, Time.deltaTime);
                target = Mathf.Lerp(idleAlpha, 1f, activity);
            }

            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);

            lastViewer = viewer;
            hasLastViewer = true;
        }

        /// <summary>How many markers are visible right now. For tests and diagnostics.</summary>
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

        /// <summary>The anchored position of the nth visible marker. For tests that
        /// assert the bearing-to-x mapping without reading private state.</summary>
        public Vector2 VisiblePosition(int index) =>
            pool != null && index >= 0 && index < pool.Length
                ? pool[index].Rect.anchoredPosition
                : Vector2.zero;

        private readonly struct Entry
        {
            public readonly RectTransform Rect;
            public readonly Image Image;
            public readonly TextMeshProUGUI Label;
            public readonly GameObject Object;

            public Entry(RectTransform rect, Image image, TextMeshProUGUI label)
            {
                Rect = rect;
                Image = image;
                Label = label;
                Object = rect.gameObject;
            }
        }

        private struct Direction
        {
            public readonly string Label;
            public readonly Vector3 World;
            public RectTransform Rect;
            public TextMeshProUGUI Text;

            public Direction(string label, Vector3 world)
            {
                Label = label;
                World = world;
                Rect = null;
                Text = null;
            }
        }
    }
}
