using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// A compass strip: markers slide left and right along a bar as the viewer turns.
    ///
    /// Reads <see cref="AtlasSolve.Bearing"/> and nothing else. It does not know what a
    /// camera is, never touches a tracked object's <c>Transform</c>, and does not
    /// reference the screen-indicator assembly - which is what makes the two agree about
    /// what is behind you rather than merely usually agreeing.
    ///
    /// Markers outside the bar's field of view are <b>hidden, not clamped</b>. A clamped
    /// marker piles up at the end of the bar and reads as "there is something exactly
    /// there", which is a lie; a hidden one reads as "it is not in front of you", which
    /// is the truth.
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

        [Header("Pool")]
        [Tooltip("Must be at least the registry's MaxMarkers. Allocated once, at Awake.")]
        [SerializeField, Min(1)] private int poolSize = 32;

        [Header("Icons")]
        [SerializeField] private AtlasSpriteIcons icons;

        private RectTransform bar;
        private Entry[] pool;

        /// <summary>Where icons come from. Assign in code to use a provider that is not
        /// a sprite array - the seam is the point.</summary>
        public IAtlasIconProvider IconProvider { get; set; }

        /// <summary>Degrees the bar spans. Markers beyond half of this are hidden.</summary>
        public float BarFieldOfView
        {
            get => barFieldOfView;
            set => barFieldOfView = Mathf.Clamp(value, 1f, 360f);
        }

        /// <summary>How many markers this can draw at once. A frame with more is
        /// truncated by the registry before it gets here.</summary>
        public int Capacity => pool != null ? pool.Length : poolSize;


        [Header("Registry")]
        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Tooltip("Register with the registry automatically. Turn off to wire it in code.")]
        [SerializeField] private bool selfRegister = true;

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
        /// <c>registry.AddProjection(new BearingProjection(), this)</c> - and a presenter that
        /// looked correctly configured, drew nothing, and reported nothing when that line
        /// was missing. Marker components already register themselves in OnEnable; there
        /// was no reason for the drawing half to be different.
        ///
        /// Turn <c>selfRegister</c> off to keep the code-driven route, which is still what
        /// a game with several viewers or a custom projection wants.
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
        }

        private void Awake()
        {
            bar = (RectTransform)transform;
            if (IconProvider == null) IconProvider = icons;

            BuildPool();
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

                go.SetActive(false);
                pool[i] = new Entry(rect, image);
            }
        }

        public void Present(IReadOnlyList<AtlasSolve> solves)
        {
            if (pool == null) return;

            float half = barFieldOfView * 0.5f;
            float width = bar.rect.width;
            int shown = 0;

            for (int i = 0; i < solves.Count && shown < pool.Length; i++)
            {
                AtlasSolve solve = solves[i];

                // Hidden, not clamped. See the class comment - this is the difference
                // between a bar that tells the truth and one that looks plausible.
                if (Mathf.Abs(solve.Bearing) > half) continue;
                if (solve.Fade <= 0f) continue;

                Entry entry = pool[shown++];

                // Linear in bearing across the bar: -half maps to the left edge, +half to
                // the right, 0 to the centre. Test 22 asserts three points on that line.
                float t = solve.Bearing / half;
                entry.Rect.anchoredPosition = new Vector2(t * width * 0.5f, markerY);

                Sprite sprite = IconProvider != null ? IconProvider.Resolve(solve.Marker.IconId) : null;
                entry.Image.sprite = sprite;
                entry.Image.enabled = sprite != null;

                Color tint = solve.Marker.Tint;
                tint.a *= solve.Fade;
                entry.Image.color = tint;

                if (!entry.Object.activeSelf) entry.Object.SetActive(true);
            }

            // Everything the pool has left over. Hiding rather than destroying is what
            // keeps Present allocation-free.
            for (int i = shown; i < pool.Length; i++)
            {
                if (pool[i].Object.activeSelf) pool[i].Object.SetActive(false);
            }
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
