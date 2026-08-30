using System.Collections.Generic;
using LiminalLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Floating icons over the world, pinned to the screen edge when their target is not
    /// on screen.
    ///
    /// Reads <see cref="AtlasSolve.ViewportPoint"/> and <see cref="AtlasSolve.OnScreen"/>.
    /// It does not project anything itself, does not reference the compass assembly, and
    /// does not know a camera exists.
    ///
    /// The edge case that matters is behind the viewer, and it is handled entirely by
    /// <see cref="AtlasMath.ClampToEdge"/> - which is the point of putting it there. A
    /// presenter that did its own clamping would get the mirrored-projection case wrong
    /// in its own way, and then the compass and the icons would disagree about which side
    /// something is on.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Screen Presenter")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScreenPresenter : MonoBehaviour, IAtlasPresenter
    {
        [Header("Layout")]
        [Tooltip("How far inside the screen edge a clamped icon sits, as a fraction of the viewport.")]
        [SerializeField, Range(0f, 0.45f)] private float edgeMargin = 0.05f;

        [SerializeField] private Vector2 iconSize = new Vector2(48f, 48f);

        [Header("Off-screen arrow")]
        [Tooltip("Optional. Rotated to point back at the target when it is off screen.")]
        [SerializeField] private Sprite arrowSprite;

        [SerializeField] private Vector2 arrowSize = new Vector2(24f, 24f);

        [Tooltip("How far from the icon the arrow sits, in the direction of the target.")]
        [SerializeField] private float arrowOffset = 34f;

        [Tooltip("Degrees to add so your art points the right way. 0 if the arrow art " +
                 "points right, 90 if it points up.")]
        [SerializeField, Range(-180f, 180f)] private float arrowRotationOffset;

        [Header("Distance")]
        [Tooltip("Alpha against Fade, which is already 1 near and 0 at the cull distance.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Scale at the far edge of visibility.")]
        [SerializeField, Range(0.1f, 2f)] private float minScale = 0.7f;

        [Tooltip("Scale up close.")]
        [SerializeField, Range(0.1f, 2f)] private float maxScale = 1.1f;

        [Header("Labels")]
        [SerializeField] private bool showDistanceLabels;

        [Tooltip("{0} is the distance in metres, already rounded.")]
        [SerializeField] private string distanceFormat = "{0}m";

        [SerializeField] private float labelSize = 14f;
        [SerializeField] private float labelOffsetY = -30f;

        [Header("Culling")]
        [Tooltip("Hide indicators whose target is behind the viewer. The compass still " +
                 "shows them; this is for HUDs that want the screen kept clear.")]
        [SerializeField] private bool hideWhenBehind;

        [Tooltip("Hide indicators whose target is off screen, arrows included.")]
        [SerializeField] private bool hideWhenOffScreen;

        [Header("Pool")]
        [Tooltip("Must be at least the registry's MaxMarkers. Allocated once, at Awake.")]
        [SerializeField, Min(1)] private int poolSize = 32;

        [Header("Icons")]
        [SerializeField] private AtlasSpriteIcons icons;

        private RectTransform area;
        private Entry[] pool;
        private TMP_FontAsset font;

        public IAtlasIconProvider IconProvider { get; set; }

        public float EdgeMargin
        {
            get => edgeMargin;
            set => edgeMargin = Mathf.Clamp(value, 0f, 0.45f);
        }

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
        /// <c>registry.AddProjection(new ScreenProjection(), this)</c> - and a presenter that
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
                // Named, because the alternative is a screen indicator layer that silently never
                // appears and a developer checking their icon ids.
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry. Assign one, or put an " +
                    "AtlasRegistryBehaviour on a parent or in the scene.", this);
                return;
            }

            registry.AddProjection(new ScreenProjection(), this);
        }

        private void OnDisable()
        {
            // Unregisters on disable, destroy and scene unload alike, so a registry that
            // outlives a HUD is never left presenting into a destroyed pool.
            if (registry != null) registry.Registry.RemoveProjection(this);
        }

        private void Awake()
        {
            area = (RectTransform)transform;
            if (IconProvider == null) IconProvider = icons;

            // Resolved before the pool is built: if TMP cannot draw, the labels are
            // never created rather than created broken.
            if (!TryResolveFont(out font)) showDistanceLabels = false;

            BuildPool();
        }

        private void BuildPool()
        {
            pool = new Entry[Mathf.Max(1, poolSize)];

            for (int i = 0; i < pool.Length; i++)
            {
                var root = new GameObject("Indicator " + i, typeof(RectTransform), typeof(Image));
                root.transform.SetParent(area, false);

                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = iconSize;

                var image = root.GetComponent<Image>();
                image.raycastTarget = false;

                // The arrow is a child so it can rotate independently of the icon - an
                // icon that spun with its arrow would be unreadable.
                var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
                arrowObject.transform.SetParent(rect, false);

                var arrowRect = (RectTransform)arrowObject.transform;
                arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
                arrowRect.pivot = new Vector2(0.5f, 0.5f);
                arrowRect.sizeDelta = arrowSize;

                var arrowImage = arrowObject.GetComponent<Image>();
                arrowImage.raycastTarget = false;
                arrowImage.sprite = arrowSprite;

                root.SetActive(false);
                TextMeshProUGUI label = showDistanceLabels ? BuildLabel(rect) : null;

                pool[i] = new Entry(rect, image, arrowRect, arrowImage, label);
            }
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


        private TextMeshProUGUI BuildLabel(RectTransform parent)
        {
            var go = new GameObject("Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(90f, 20f);
            rect.anchoredPosition = new Vector2(0f, labelOffsetY);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = labelSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            return text;
        }

        public void Present(in AtlasViewer viewer, IReadOnlyList<AtlasSolve> solves)
        {
            if (pool == null) return;

            Rect bounds = area.rect;
            int shown = 0;

            for (int i = 0; i < solves.Count && shown < pool.Length; i++)
            {
                AtlasSolve solve = solves[i];
                if (solve.Fade <= 0f) continue;

                bool clamped = !solve.OnScreen;

                // Both off by default. An indicator pinned to the edge for something
                // behind you is the feature, not a nuisance - but a HUD that already says
                // so on a compass may want the screen kept clear, and that is a decision
                // for whoever is designing the HUD rather than for this component.
                if (hideWhenOffScreen && clamped) continue;
                if (hideWhenBehind && solve.Behind) continue;

                Entry entry = pool[shown++];

                float fade = Mathf.Clamp01(fadeCurve.Evaluate(Mathf.Clamp01(solve.Fade)));
                entry.Rect.localScale = Vector3.one * AtlasMath.DistanceScale(fade, minScale, maxScale);

                Vector2 viewport;
                float angle = 0f;

                if (clamped)
                {
                    viewport = AtlasMath.ClampToEdge(solve.ViewportPoint, edgeMargin, out angle);
                }
                else
                {
                    viewport = new Vector2(solve.ViewportPoint.x, solve.ViewportPoint.y);
                }

                entry.Rect.anchoredPosition = new Vector2(
                    bounds.xMin + viewport.x * bounds.width,
                    bounds.yMin + viewport.y * bounds.height);

                // Drawn whether or not a sprite resolved. An Image with no sprite renders
                // a plain quad, which tinted is a readable blank marker - and a blank
                // marker is what IAtlasIconProvider promises a missing icon costs.
                //
                // Disabling the Image instead made an unconfigured presenter draw nothing
                // at all, which is indistinguishable from a registry that is not ticking,
                // a marker that never registered, or a camera facing the wrong way. The
                // system's whole promise is that a registered marker in view is visible;
                // an unassigned icon list is a styling gap, not grounds to break it.
                Sprite sprite = IconProvider != null
                    ? IconProvider.Resolve(solve.Marker.IconId)
                    : null;

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

                // The arrow only means anything when the icon is not where the thing is.
                bool showArrow = clamped && arrowSprite != null;
                if (entry.ArrowObject.activeSelf != showArrow) entry.ArrowObject.SetActive(showArrow);

                if (showArrow)
                {
                    // The offset is what lets any arrow art work. Without it the component
                    // silently requires art that points right, and art that points up -
                    // which is the more common way to draw an arrow - is wrong by 90
                    // degrees in a way that looks like a maths bug rather than a setting.
                    entry.ArrowRect.localRotation =
                        Quaternion.Euler(0f, 0f, angle + arrowRotationOffset);
                    entry.ArrowRect.anchoredPosition = new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * arrowOffset,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * arrowOffset);
                    entry.ArrowImage.color = tint;
                }

                if (!entry.Object.activeSelf) entry.Object.SetActive(true);
            }

            for (int i = shown; i < pool.Length; i++)
            {
                if (pool[i].Object.activeSelf) pool[i].Object.SetActive(false);
            }
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

        /// <summary>The anchored position of the nth visible indicator, for tests.</summary>
        public Vector2 VisiblePosition(int index) =>
            pool != null && index >= 0 && index < pool.Length
                ? pool[index].Rect.anchoredPosition
                : Vector2.zero;

        private readonly struct Entry
        {
            public readonly RectTransform Rect;
            public readonly Image Image;
            public readonly RectTransform ArrowRect;
            public readonly Image ArrowImage;
            public readonly GameObject Object;
            public readonly GameObject ArrowObject;

            public readonly TextMeshProUGUI Label;

            public Entry(RectTransform rect, Image image, RectTransform arrowRect,
                         Image arrowImage, TextMeshProUGUI label)
            {
                Label = label;
                Rect = rect;
                Image = image;
                ArrowRect = arrowRect;
                ArrowImage = arrowImage;
                Object = rect.gameObject;
                ArrowObject = arrowRect.gameObject;
            }
        }
    }
}
