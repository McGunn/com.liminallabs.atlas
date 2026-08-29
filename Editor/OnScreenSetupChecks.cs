using LiminalLabs.Core.Editor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// Checks for the Atlas On-Screen presenter. Its own package's business rather than the
    /// core's, which is the point of the split.
    /// </summary>
    public sealed class AtlasOnScreenCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas On-Screen";
        public int Order => 0;

        public void Run(LiminalSetupReport report)
        {
            ScreenPresenter[] presenters = AtlasEditorScene.FindAll<ScreenPresenter>();
            if (presenters.Length == 0) return;

            int limit = AtlasEditorScene.MarkerLimit();

            foreach (ScreenPresenter presenter in presenters)
            {
                ScreenPresenter captured = presenter;

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

                if (presenter.EdgeMargin <= 0f)
                {
                    report.Warn($"'{presenter.name}' has no edge margin",
                        "Clamped indicators sit exactly on the screen edge and are drawn half off it.",
                        () => AtlasEditorScene.Select(captured), "Select");
                }
            }
        }
    }
}
