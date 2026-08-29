using System.Collections.Generic;
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

        [Header("Pool")]
        [Tooltip("Must be at least the registry's MaxMarkers. Allocated once, at Awake.")]
        [SerializeField, Min(1)] private int poolSize = 32;

        [Header("Icons")]
        [SerializeField] private AtlasSpriteIcons icons;

        private RectTransform area;
        private Entry[] pool;

        public IAtlasIconProvider IconProvider { get; set; }

        public float EdgeMargin
        {
            get => edgeMargin;
            set => edgeMargin = Mathf.Clamp(value, 0f, 0.45f);
        }

        public int Capacity => pool != null ? pool.Length : poolSize;

        private void Awake()
        {
            area = (RectTransform)transform;
            if (IconProvider == null) IconProvider = icons;

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
                pool[i] = new Entry(rect, image, arrowRect, arrowImage);
            }
        }

        public void Present(IReadOnlyList<AtlasSolve> solves)
        {
            if (pool == null) return;

            Rect bounds = area.rect;
            int shown = 0;

            for (int i = 0; i < solves.Count && shown < pool.Length; i++)
            {
                AtlasSolve solve = solves[i];
                if (solve.Fade <= 0f) continue;

                Entry entry = pool[shown++];

                Vector2 viewport;
                float angle = 0f;
                bool clamped = !solve.OnScreen;

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

                Sprite sprite = IconProvider != null ? IconProvider.Resolve(solve.Marker.IconId) : null;
                entry.Image.sprite = sprite;
                entry.Image.enabled = sprite != null;

                Color tint = solve.Marker.Tint;
                tint.a *= solve.Fade;
                entry.Image.color = tint;

                // The arrow only means anything when the icon is not where the thing is.
                bool showArrow = clamped && arrowSprite != null;
                if (entry.ArrowObject.activeSelf != showArrow) entry.ArrowObject.SetActive(showArrow);

                if (showArrow)
                {
                    entry.ArrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
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

            public Entry(RectTransform rect, Image image, RectTransform arrowRect, Image arrowImage)
            {
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
