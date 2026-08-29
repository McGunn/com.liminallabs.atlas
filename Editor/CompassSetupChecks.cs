using LiminalLabs.Core.Editor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// Checks for the Atlas Compass presenter. Its own package's business rather than the
    /// core's, which is the point of the split.
    /// </summary>
    public sealed class AtlasCompassCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas Compass";
        public int Order => 0;

        public void Run(LiminalSetupReport report)
        {
            BarPresenter[] presenters = AtlasEditorScene.FindAll<BarPresenter>();
            if (presenters.Length == 0) return;

            int limit = AtlasEditorScene.MarkerLimit();

            foreach (BarPresenter presenter in presenters)
            {
                BarPresenter captured = presenter;

                if (limit > 0 && presenter.Capacity < limit)
                {
                    report.Warn(
                        $"'{presenter.name}' pools {presenter.Capacity} markers but the registry sends up to {limit}",
                        "The extras are silently dropped. The pool is built at Awake, so raising " +
                        "Pool Size after play has started does nothing.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                if (presenter.IconProvider == null && !AtlasEditorScene.HasSerializedIcons(presenter))
                {
                    report.Warn($"'{presenter.name}' has no icon provider",
                        "Markers will position correctly and draw as nothing. Assign an " +
                        "AtlasSpriteIcons asset, or set IconProvider in code.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }

                if (presenter.BarFieldOfView > 300f)
                {
                    // Near 360 the bar shows what is behind you at the same scale as what
                    // is in front, and a marker at one edge is indistinguishable from one
                    // at the other.
                    report.Warn($"'{presenter.name}' spans {presenter.BarFieldOfView:0} degrees",
                        "Above about 300 the bar shows what is behind you, and the two ends " +
                        "become hard to tell apart.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }
            }
        }
    }
}
