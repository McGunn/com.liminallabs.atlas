using System;
using System.Collections.Generic;
using LiminalLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// A legend: one row per marker kind, each a toggle that filters the maps.
    ///
    /// Two jobs that are usually two components and should not be. A legend that only
    /// labels the icons is a key nobody reads twice; a filter with no icons is a list of
    /// words. Together they answer both questions a player has about a crowded map — what
    /// is that, and show me only those — with one row each.
    ///
    /// Built from the icon provider rather than authored, so a project that adds a marker
    /// kind or changes an icon gets a legend that already agrees with its map. A legend
    /// maintained by hand is a legend that is wrong within a month.
    ///
    /// <b>Unticking every box shows everything again</b>, which is <see cref="AtlasFilter"/>'s
    /// rule and the right one: an empty selection meaning "nothing" leaves someone staring
    /// at a blank map wondering what they broke.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Atlas Legend")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class AtlasLegend : MonoBehaviour
    {
        [Header("Filters")]
        [Tooltip("The maps this legend filters. Empty means every map in the scene.")]
        [SerializeField] private List<MinimapPresenter> maps = new List<MinimapPresenter>();

        [Header("Kinds")]
        [Tooltip("Which kinds get a row. Empty means every kind the tracked markers use, " +
                 "which is usually what you want - a legend listing kinds nothing uses is " +
                 "a legend that is mostly noise.")]
        [SerializeField] private List<AtlasMarkerKind> kinds = new List<AtlasMarkerKind>();

        [Tooltip("Rebuild when the set of kinds in use changes. Off for a fixed legend.")]
        [SerializeField] private bool followTrackedKinds = true;

        [Tooltip("Seconds between checks of which kinds are in use. A quarter second is " +
                 "invisible; every frame is a walk of every tracked marker.")]
        [SerializeField, Min(0.05f)] private float followInterval = 0.25f;

        [Header("Icons")]
        [SerializeField] private AtlasSpriteIcons icons;

        [Tooltip("Icon id per kind, in the order kinds are listed. Short lists fall back " +
                 "to id 0.")]
        [SerializeField] private List<int> iconIds = new List<int>();

        [Header("Layout")]
        [SerializeField] private float rowHeight = 28f;
        [SerializeField] private Vector2 iconSize = new Vector2(20f, 20f);
        [SerializeField] private float labelSize = 14f;
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.3f);

        [Header("Registry")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        private readonly List<Row> rows = new List<Row>();
        private RectTransform area;
        private TMP_FontAsset font;
        private bool fontResolved;
        private int lastKindSignature = -1;
        private float nextFollowCheck;

        /// <summary>Where icons come from. Assign in code to share a provider that is not
        /// a sprite array.</summary>
        public IAtlasIconProvider IconProvider { get; set; }

        /// <summary>Raised when a row is toggled, with the filter that resulted. For a game
        /// that wants to persist which filters a player left on.</summary>
        public event Action<AtlasFilter> FilterChanged;

        /// <summary>The filter these rows currently describe.</summary>
        public AtlasFilter Filter { get; private set; } = AtlasFilter.All;

        private void Awake()
        {
            area = (RectTransform)transform;
            if (IconProvider == null) IconProvider = icons;
            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);
        }

        private void OnEnable() => Rebuild();

        private void Update()
        {
            if (!followTrackedKinds || registry == null) return;
            if (Time.unscaledTime < nextFollowCheck) return;

            nextFollowCheck = Time.unscaledTime + followInterval;

            int signature = KindSignature();
            if (signature != lastKindSignature) Rebuild();
        }

        /// <summary>
        /// A cheap fingerprint of which kinds are being tracked.
        ///
        /// A bitmask rather than a list comparison: kinds are few, and an int compare is
        /// what keeps "rebuild when the world changes" from being more expensive than the
        /// rebuild. It is still a walk of every tracked marker, which is why it runs on
        /// <see cref="followInterval"/> rather than every frame.
        /// </summary>
        private int KindSignature()
        {
            if (registry == null) return 0;

            int mask = 0;
            IReadOnlyList<IAtlasTrackable> tracked = registry.Registry.Tracked;
            for (int i = 0; i < tracked.Count; i++)
            {
                IAtlasTrackable target = tracked[i];
                if (target != null) mask |= 1 << (int)target.Marker.Kind;
            }

            return mask;
        }

        /// <summary>Rebuilds the rows. Cheap enough to call whenever the world changes,
        /// and called for you when it does.</summary>
        public void Rebuild()
        {
            if (area == null) area = (RectTransform)transform;

            foreach (Row row in rows)
                if (row.Object != null) Destroy(row.Object);
            rows.Clear();

            List<AtlasMarkerKind> listed = ResolveKinds();
            lastKindSignature = KindSignature();

            for (int i = 0; i < listed.Count; i++)
                rows.Add(BuildRow(listed[i], i));

            ApplyFilter();
        }

        private List<AtlasMarkerKind> ResolveKinds()
        {
            if (kinds.Count > 0) return kinds;

            var found = new List<AtlasMarkerKind>();
            if (registry == null) return found;

            int mask = KindSignature();
            foreach (AtlasMarkerKind kind in (AtlasMarkerKind[])Enum.GetValues(typeof(AtlasMarkerKind)))
                if ((mask & (1 << (int)kind)) != 0) found.Add(kind);

            return found;
        }

        private Row BuildRow(AtlasMarkerKind kind, int index)
        {
            var go = new GameObject(kind.ToString(), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(area, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * rowHeight);
            rect.sizeDelta = new Vector2(0f, rowHeight);

            // Invisible but raycastable, so the whole row is the hit target. A row whose
            // only clickable part is a 20-pixel icon is a row players think is broken.
            var background = go.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(rect, false);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(6f, 0f);
            iconRect.sizeDelta = iconSize;

            var iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;

            int iconId = index < iconIds.Count ? iconIds[index] : 0;
            Sprite sprite = IconProvider != null ? IconProvider.Resolve(iconId) : null;
            if (sprite == null) sprite = LiminalPlaceholder.Missing;
            iconImage.sprite = sprite;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rect, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(iconSize.x + 14f, 0f);
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = ResolveFont();
            label.fontSize = labelSize;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = Readable(kind);

            var row = new Row(go, kind, iconImage, label);

            Button button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Toggle(row));

            return row;
        }

        /// <summary>
        /// "FastTravel" is a type name, not a label. Split on the capitals so a legend
        /// reads like a legend rather than like source.
        /// </summary>
        private static string Readable(AtlasMarkerKind kind)
        {
            string name = kind.ToString();
            var text = new System.Text.StringBuilder(name.Length + 4);

            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) text.Append(' ');
                text.Append(name[i]);
            }

            return text.ToString();
        }

        private void Toggle(Row row)
        {
            row.On = !row.On;

            AtlasFilter filter = AtlasFilter.All;
            foreach (Row other in rows)
                if (other.On) filter = filter.Including(other.Kind);

            // Every row on is the same as no filter at all, and saying so keeps the maps
            // out of the per-marker test in the common case.
            bool everyRowOn = true;
            foreach (Row other in rows) everyRowOn &= other.On;
            if (everyRowOn) filter = AtlasFilter.All;

            Filter = filter;
            ApplyFilter();
            FilterChanged?.Invoke(Filter);
        }

        private void ApplyFilter()
        {
            foreach (Row row in rows)
            {
                Color tint = row.On ? labelColor : offColor;
                row.Icon.color = tint;
                row.Label.color = tint;
            }

            foreach (MinimapPresenter map in Maps())
                if (map != null) map.Filter = Filter;
        }

        private IEnumerable<MinimapPresenter> Maps()
        {
            if (maps.Count > 0) return maps;
            return FindObjectsByType<MinimapPresenter>(FindObjectsInactive.Include);
        }

        private TMP_FontAsset ResolveFont()
        {
            if (fontResolved) return font;
            fontResolved = true;

            // The same guard the presenters use: TMP_Settings.defaultFontAsset throws
            // rather than returning null when TMP Essential Resources are missing.
            try
            {
                if (TMP_Settings.instance != null) font = TMP_Settings.defaultFontAsset;
            }
            catch
            {
                font = null;
            }

            if (font != null) return font;

            Font fallback = LiminalFonts.Get(LiminalFontRole.Sans);
            if (fallback != null) font = TMP_FontAsset.CreateFontAsset(fallback);

            return font;
        }

        private sealed class Row
        {
            public readonly GameObject Object;
            public readonly AtlasMarkerKind Kind;
            public readonly Image Icon;
            public readonly TextMeshProUGUI Label;
            public bool On = true;

            public Row(GameObject go, AtlasMarkerKind kind, Image icon, TextMeshProUGUI label)
            {
                Object = go;
                Kind = kind;
                Icon = icon;
                Label = label;
            }
        }
    }
}
