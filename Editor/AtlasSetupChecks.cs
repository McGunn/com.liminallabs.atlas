using System.Collections.Generic;
using LiminalLabs.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// Registry wiring, per the house rule that anything failing silently at runtime has
    /// to surface here.
    ///
    /// Atlas earns that more than most: a registry with no projections, a presenter
    /// nobody registered, a viewer with no camera - every one produces the same symptom,
    /// which is nothing on screen and no error anywhere.
    ///
    /// Deliberately about the *core*. Whether a bar pools enough markers is the compass
    /// package's business, and it ships its own check for it.
    /// </summary>
    public sealed class AtlasRegistryCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas";
        public int Order => 0;

        public void Run(LiminalSetupReport report)
        {
            AtlasRegistryBehaviour[] registries = AtlasEditorScene.FindAll<AtlasRegistryBehaviour>();
            var markers = new List<AtlasMarkerBehaviour>(AtlasEditorScene.FindAll<AtlasMarkerBehaviour>());

            var presenters = new List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in AtlasEditorScene.FindAll<MonoBehaviour>())
                if (behaviour is IAtlasPresenter) presenters.Add(behaviour);

            if (registries.Length == 0 && markers.Count == 0 && presenters.Count == 0) return;

            if (registries.Length == 0)
            {
                report.Fail("No AtlasRegistryBehaviour in the scene",
                    markers.Count > 0
                        ? $"{markers.Count} marker(s) will log a warning and never appear."
                        : "Presenters will never be given anything to draw.");
            }
            else if (registries.Length > 1)
            {
                // Legitimate for split-screen, a mistake everywhere else - a marker takes
                // the nearest registry in its parents, then any in the scene.
                report.Warn($"{registries.Length} AtlasRegistryBehaviour components in the scene",
                    "Correct for split-screen. Otherwise markers may register with the wrong one.");
            }

            foreach (AtlasRegistryBehaviour registry in registries)
            {
                if (registry.ViewerCamera != null) continue;

                AtlasRegistryBehaviour captured = registry;
                report.Fail($"'{registry.name}' has no viewer camera",
                    "No camera is assigned and there is no MainCamera, so it will never tick.",
                    () => AtlasEditorScene.Select(captured), "Select");
            }

            if (presenters.Count == 0 && registries.Length > 0 && markers.Count > 0)
            {
                report.Warn("Markers are tracked but nothing is drawing them",
                    "The core package draws nothing, by design. Install Liminal Labs Atlas Compass " +
                    "or Atlas On-Screen, or write your own IAtlasPresenter.");
            }
            else if (presenters.Count > 0 && registries.Length > 0)
            {
                // Presenters are wired in code, so one sitting in a scene with nothing
                // calling AddProjection is the likeliest way to see nothing at all.
                report.Warn($"{presenters.Count} presenter(s) present - check something calls AddProjection",
                    "registry.AddProjection(new BearingProjection(), bar). A presenter nobody " +
                    "registered draws nothing and reports nothing.");
            }

            if (registries.Length == 1 && presenters.Count > 0 && markers.Count > 0)
                report.Pass($"Atlas wired: {markers.Count} marker component(s), {presenters.Count} presenter(s)");
        }
    }

    /// <summary>
    /// Scene helpers the atlas editor checks share - including the ones in the presenter
    /// packages, which is why this is public.
    /// </summary>
    public static class AtlasEditorScene
    {
        /// <summary>
        /// Every component of a type, inactive included.
        ///
        /// Version-gated: the sort-mode overloads were deprecated in 6000.3 and the
        /// replacements do not exist before it, and this package supports 6000.0 up.
        /// </summary>
        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_3_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
        }

        public static void Select(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        /// <summary>
        /// The registry's marker limit, or zero if there is none in the scene.
        ///
        /// A presenter pool smaller than this silently drops the markers past it - and it
        /// drops the lowest priority ones, which are exactly the ones nobody notices
        /// missing until they matter.
        /// </summary>
        public static int MarkerLimit()
        {
            AtlasRegistryBehaviour[] registries = FindAll<AtlasRegistryBehaviour>();
            return registries.Length > 0 ? registries[0].Registry.Settings.MaxMarkers : 0;
        }

        /// <summary>
        /// Reads a presenter's serialized icon field.
        ///
        /// IconProvider is only populated at Awake, so checking the property outside play
        /// mode would report every presenter in the project as broken.
        /// </summary>
        public static bool HasSerializedIcons(MonoBehaviour presenter)
        {
            var serialized = new SerializedObject(presenter);
            SerializedProperty icons = serialized.FindProperty("icons");
            return icons != null && icons.objectReferenceValue != null;
        }
    }

    /// <summary>
    /// Says where the samples went.
    ///
    /// Scene builders live in <c>Samples~</c>, which Unity does not compile until the
    /// sample is imported - so a sample's menu item genuinely does not exist before that,
    /// and "the menu is missing" is the correct observation rather than a bug.
    /// </summary>
    public sealed class AtlasSampleCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas";
        public int Order => 90;

        public void Run(LiminalSetupReport report)
        {
            report.Pass("Samples live in Samples~ and have to be imported",
                "Unity only compiles a package's Samples~ folder after its sample is imported, so " +
                "a sample's menu item does not exist before then. " +
                "Package Manager > the package > Samples > Import.");
        }
    }
}
