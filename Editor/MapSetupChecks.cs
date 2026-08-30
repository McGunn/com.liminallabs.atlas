using LiminalLabs.Core.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// Checks for the map presenter.
    ///
    /// A map has more ways to be wired almost correctly than either of the other views,
    /// and every one of them produces a rectangle with nothing in it. The list below is
    /// the set that cost real time rather than the set that is easy to detect.
    /// </summary>
    public sealed class AtlasMapCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas Map";
        public int Order => 20;

        public void Run(LiminalSetupReport report)
        {
            MinimapPresenter[] maps = AtlasEditorScene.FindAll<MinimapPresenter>();
            if (maps.Length == 0) return;

            int limit = AtlasEditorScene.MarkerLimit();

            foreach (MinimapPresenter map in maps)
            {
                MinimapPresenter captured = map;

                if (limit > 0 && map.Capacity < limit)
                {
                    report.Warn(
                        $"'{map.name}' pools {map.Capacity} markers but the registry sends up to {limit}",
                        "The extras are silently dropped, lowest priority first. The pool is " +
                        "built at Awake, so raising Pool Size during play does nothing.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                if (map.IconProvider == null && !AtlasEditorScene.HasSerializedIcons(map))
                {
                    report.Warn($"'{map.name}' has no icon provider",
                        "Markers position correctly and draw as the missing-sprite placeholder. " +
                        "Assign an AtlasSpriteIcons asset, or set IconProvider in code.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                // A map with no mask draws its markers over the rest of the HUD, which
                // looks like markers in the wrong place rather than like missing clipping.
                if (map.GetComponent<RectMask2D>() == null && map.GetComponent<Mask>() == null &&
                    map.GetComponentInParent<RectMask2D>() == null)
                {
                    report.Warn($"'{map.name}' has nothing clipping it",
                        "Pinned markers sit at the edge, but a marker that is drawn outside " +
                        "the rect will overlap the rest of the HUD. The presenter adds a " +
                        "RectMask2D at Awake when Clip To Rect is on.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                var rect = (RectTransform)map.transform;
                float width = rect.rect.width;
                float height = rect.rect.height;

                // A round map clamps to a circle, and a circle inside a non-square rect is
                // an ellipse the maths does not know about: markers pin to a radius in
                // fractions, which is only a circle on screen when the rect is square.
                if (width > 0f && height > 0f && Mathf.Abs(width - height) / Mathf.Max(width, height) > 0.02f)
                {
                    report.Warn($"'{map.name}' is {width:0}x{height:0}, not square",
                        "Round maps pin markers to a circle in map fractions, which only " +
                        "looks like a circle on a square rect. Either square it up or turn " +
                        "Round off.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                // The defect this check exists for. A world map framed to a space with no
                // authored bounds falls back to its own Radius, which is a minimap's
                // number - so it comes out smaller than the minimap beside it and looks
                // like a broken map rather than an unset field. Nothing writes the bounds
                // at runtime, and an editor script cannot: the registry is built fresh
                // when the component wakes.
                if (map.Projection.Centre == AtlasMapCentre.SpaceBounds && !HasAuthoredSpace())
                {
                    report.Fail($"'{map.name}' frames a space that nothing authors",
                        "Centre is Space Bounds, but there is no AtlasSpaceBehaviour in the " +
                        "scene, so the space's bounds are zero and the map falls back to " +
                        "Radius. Add one and size its bounds to the playable area.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                if (map.Projection.Radius <= 1f)
                {
                    report.Fail($"'{map.name}' has a radius of {map.Projection.Radius:0.##}",
                        "Everything will be pinned to the edge. The radius is a half-span in " +
                        "world units, so 60 shows 120 across.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }
            }

            // A legend whose icon ids do not line up with its kinds is a legend that
            // confidently labels the wrong things, which is worse than no legend.
            foreach (AtlasLegend legend in AtlasEditorScene.FindAll<AtlasLegend>())
            {
                AtlasLegend captured = legend;
                if (legend.IconProvider == null && !AtlasEditorScene.HasSerializedIcons(legend))
                {
                    report.Warn($"'{legend.name}' has no icon provider",
                        "Every row will show the missing-sprite placeholder, so the legend " +
                        "will label kinds correctly and illustrate none of them.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }
            }

            if (maps.Length > 0) report.Pass($"{maps.Length} map presenter(s) wired");
        }

        private static bool HasAuthoredSpace() =>
            AtlasEditorScene.FindAll<AtlasSpaceBehaviour>().Length > 0;
    }
}
