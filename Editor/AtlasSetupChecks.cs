using System.Collections.Generic;
using LiminalLabs.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// Scene wiring checks, per the house rule: anything that would fail silently at
    /// runtime has to surface here.
    ///
    /// Atlas has an unusually large number of ways to be wired almost correctly and draw
    /// nothing at all - a registry with no projections, a presenter nobody registered, a
    /// pool smaller than the marker limit - and every one of them looks identical from
    /// the outside. That is the whole argument for these being checks rather than
    /// comments in a README.
    ///
    /// Editor-only, in the Editor assembly. The runtime assembly still references
    /// nothing.
    /// </summary>
    public sealed class AtlasSceneCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas";
        public int Order => 0;

        public void Run(LiminalSetupReport report)
        {
            AtlasRegistryBehaviour[] registries = FindAll<AtlasRegistryBehaviour>();
            var markers = new List<AtlasMarkerBehaviour>(FindAll<AtlasMarkerBehaviour>());

            var presenters = new List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in FindAll<MonoBehaviour>())
            {
                if (behaviour is IAtlasPresenter) presenters.Add(behaviour);
            }

            if (registries.Length == 0 && markers.Count == 0 && presenters.Count == 0)
            {
                // Nothing to say about a scene that does not use atlas at all.
                return;
            }

            if (registries.Length == 0)
            {
                report.Fail("No AtlasRegistryBehaviour in the scene",
                    markers.Count > 0
                        ? $"{markers.Count} marker(s) will log a warning and never appear."
                        : "Presenters will never be given anything to draw.");
            }
            else if (registries.Length > 1)
            {
                // Legitimate for split-screen, and a mistake everywhere else - markers
                // resolve to whichever one is nearest in the hierarchy, which is easy to
                // get wrong by accident.
                report.Warn($"{registries.Length} AtlasRegistryBehaviour components in the scene",
                    "Correct for split-screen. Otherwise markers may register with the wrong one - " +
                    "each marker takes the nearest in its parents, then any in the scene.");
            }

            foreach (AtlasRegistryBehaviour registry in registries)
            {
                if (registry.ViewerCamera == null)
                {
                    AtlasRegistryBehaviour captured = registry;
                    report.Fail($"'{registry.name}' has no viewer camera",
                        "No camera is assigned and there is no MainCamera, so it will never tick.",
                        () => Select(captured), "Select");
                }
            }

            // A presenter is only drawn by a projection someone added in code. There is no
            // component for that in M0, so a presenter sitting in a scene with nothing
            // calling AddProjection is the single most likely way to see nothing at all.
            if (presenters.Count > 0 && registries.Length > 0)
            {
                report.Warn($"{presenters.Count} presenter(s) present - check something calls AddProjection",
                    "Presenters are wired in code: registry.AddProjection(new BearingProjection(), bar). " +
                    "A presenter nobody registered draws nothing and reports nothing.");
            }

            CheckPools(report, registries, presenters);
            CheckIcons(report, presenters);

            if (registries.Length == 1 && presenters.Count > 0 && markers.Count > 0)
            {
                report.Pass($"Atlas wired: {markers.Count} marker component(s), {presenters.Count} presenter(s)");
            }
        }

        /// <summary>
        /// A pool smaller than the registry's limit silently drops the markers past it -
        /// and it drops the lowest priority ones, which are exactly the ones nobody
        /// notices missing until they matter.
        /// </summary>
        private static void CheckPools(LiminalSetupReport report,
                                       AtlasRegistryBehaviour[] registries,
                                       List<MonoBehaviour> presenters)
        {
            if (registries.Length == 0) return;

            int limit = registries[0].Registry.Settings.MaxMarkers;

            foreach (MonoBehaviour presenter in presenters)
            {
                int capacity = presenter switch
                {
                    BarPresenter bar => bar.Capacity,
                    ScreenPresenter screen => screen.Capacity,
                    _ => int.MaxValue,
                };

                if (capacity >= limit) continue;

                MonoBehaviour captured = presenter;
                report.Warn($"'{presenter.name}' pools {capacity} markers but the registry sends up to {limit}",
                    "The extras are silently dropped. The pool is built at Awake, so raising Pool Size " +
                    "after play has started does nothing.",
                    () => Select(captured), "Select");
            }
        }

        private static void CheckIcons(LiminalSetupReport report, List<MonoBehaviour> presenters)
        {
            foreach (MonoBehaviour presenter in presenters)
            {
                bool hasIcons = presenter switch
                {
                    BarPresenter bar => bar.IconProvider != null || HasSerializedIcons(bar),
                    ScreenPresenter screen => screen.IconProvider != null || HasSerializedIcons(screen),
                    _ => true,
                };

                if (hasIcons) continue;

                MonoBehaviour captured = presenter;
                report.Warn($"'{presenter.name}' has no icon provider",
                    "Markers will position correctly but draw as nothing. Assign an AtlasSpriteIcons " +
                    "asset, or set IconProvider in code.",
                    () => Select(captured), "Select");
            }
        }

        /// <summary>
        /// Reads the serialized field, because IconProvider is only populated at Awake -
        /// checking the property alone would report every presenter as broken while the
        /// editor is not in play mode.
        /// </summary>
        private static bool HasSerializedIcons(MonoBehaviour presenter)
        {
            var serialized = new SerializedObject(presenter);
            SerializedProperty icons = serialized.FindProperty("icons");
            return icons != null && icons.objectReferenceValue != null;
        }

        /// <summary>
        /// Every component of a type, inactive included.
        ///
        /// Version-gated because the sort-mode overloads were deprecated in 6000.3 and
        /// the replacements do not exist before it - and this package declares support
        /// from 6000.0. One local helper beats the same #if at three call sites.
        /// </summary>
        private static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_3_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
        }

        private static void Select(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }

    /// <summary>
    /// Tells you where the sample went.
    ///
    /// Its scene builder lives in <c>Samples~</c>, which Unity does not compile until the
    /// sample is imported - so the menu item genuinely does not exist until then, and
    /// "the menu is missing" is the correct observation rather than a bug. Saying so here
    /// costs one row and saves the search.
    /// </summary>
    public sealed class AtlasSampleCheck : ILiminalSetupCheck
    {
        public string Category => "Atlas";
        public int Order => 10;

        public void Run(LiminalSetupReport report)
        {
            // The builder's menu item exists only once the sample has been imported, so
            // its type being loadable is the honest test for "is the sample here".
            bool imported = System.AppDomain.CurrentDomain.GetAssemblies().Length > 0 &&
                            FindSampleBuilder() != null;

            if (imported)
            {
                report.Pass("Atlas M0 sample imported",
                    "Window > Liminal Labs > Atlas > Build M0 Sample Scene.");
                return;
            }

            report.Pass("Atlas M0 sample not imported",
                "Its scene builder lives in Samples~, which Unity only compiles after import - " +
                "so the Build M0 Sample Scene menu item will not appear until then. " +
                "Package Manager > Liminal Atlas > Samples > Atlas M0 > Import.");
        }

        private static System.Type FindSampleBuilder()
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "LiminalLabs.Atlas.SampleM0.Editor") continue;
                return assembly.GetType("LiminalLabs.Atlas.SampleM0.Editor.AtlasM0SceneBuilder");
            }
            return null;
        }
    }
}
