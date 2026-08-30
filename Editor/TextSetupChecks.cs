using LiminalLabs.Core.Editor;
using TMPro;
using UnityEditor;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// Whether this project can draw TextMeshPro text at all.
    ///
    /// TextMeshPro ships inside com.unity.ugui, so the assemblies always resolve and the
    /// code always compiles - but its settings asset and its SDF shaders arrive
    /// separately, through a one-time menu item that a fresh project has not run. Until
    /// then <c>TMP_Settings.defaultFontAsset</c> dereferences a null instance and throws,
    /// and a font built from a raw TTF gets a material whose shader cannot be found.
    ///
    /// The presenters survive this now: labels switch themselves off and say why. This
    /// check exists so the answer arrives before the label does, since "my distance
    /// labels are missing" is a much worse first symptom than a row here saying which
    /// menu item to click.
    /// </summary>
    public sealed class AtlasTextCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas Text";
        public int Order => 40;

        public void Run(LiminalSetupReport report)
        {
            // Only worth reporting if something in this scene would actually want text.
            bool wantsText = AtlasEditorScene.FindAll<BarPresenter>().Length > 0 ||
                             AtlasEditorScene.FindAll<ScreenPresenter>().Length > 0;
            if (!wantsText) return;

            TMP_Settings settings;
            try
            {
                settings = TMP_Settings.instance;
            }
            catch
            {
                // The same throw the presenters guard against, from the same cause.
                settings = null;
            }

            if (settings == null)
            {
                report.Fail("TMP Essential Resources are not imported",
                    "Distance labels and the compass letters switch themselves off without " +
                    "them, because TextMeshPro has neither a settings asset nor its SDF " +
                    "shaders until they are imported. Everything else in Atlas works. This " +
                    "is a one-time, per-project import.",
                    ImportEssentials, "Open Importer");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                report.Warn("No default TMP font asset",
                    "Atlas falls back to converting core's vendored Inter, which works but " +
                    "will not match the rest of your UI. Set a default under " +
                    "Project Settings > TextMeshPro.",
                    () => SettingsService.OpenProjectSettings("Project/TextMesh Pro/Settings"),
                    "Open Settings");
                return;
            }

            report.Pass("TextMeshPro is ready", "Labels and cardinal letters will draw.");
        }

        /// <summary>
        /// Opens TMP's own importer window rather than importing anything directly.
        ///
        /// The import writes a folder into Assets and is the user's decision, not a fix
        /// this check should make on their behalf while they are looking at a list.
        /// </summary>
        private static void ImportEssentials()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources"))
            {
                UnityEngine.Debug.LogWarning(
                    "[Atlas] Could not open the TMP importer from here. It is at " +
                    "Window > TextMeshPro > Import TMP Essential Resources.");
            }
        }
    }
}
