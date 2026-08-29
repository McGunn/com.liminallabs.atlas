using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas.SampleM0.Editor
{
    /// <summary>
    /// Builds the M0 sample scene.
    ///
    /// A builder rather than a committed <c>.unity</c> file, for two reasons. A generated
    /// scene cannot carry a stale GUID to an asset that moved, which is how sample scenes
    /// usually rot. And the build code is readable documentation of exactly what wiring
    /// the package needs — which, since presenters register themselves, is a registry, a
    /// presenter and some markers, and nothing else.
    /// </summary>
    public static class AtlasM0SceneBuilder
    {
        [MenuItem("Window/Liminal Labs/Atlas/Build M0 Sample Scene", priority = 300)]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 1.7f, 0f);
                camera.transform.rotation = Quaternion.identity;
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 12f;

            var registryObject = new GameObject("Atlas Registry");
            var registry = registryObject.AddComponent<AtlasRegistryBehaviour>();
            registry.ViewerCamera = camera;

            // Entry point 1: a component on a real object. Zero code, and it is the one
            // that orbits, so the behind-the-viewer case happens on its own.
            GameObject orbiting = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            orbiting.name = "Orbiting Marker";
            orbiting.transform.position = new Vector3(0f, 1f, 18f);
            orbiting.AddComponent<AtlasMarkerBehaviour>();

            Canvas canvas = BuildCanvas();
            AddIndicatorLayer(canvas);
            AddCompassBar(canvas);

            var demo = registryObject.AddComponent<AtlasM0Demo>();
            var serialized = new SerializedObject(demo);
            serialized.FindProperty("registry").objectReferenceValue = registry;
            serialized.FindProperty("orbiting").objectReferenceValue = orbiting.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = registryObject;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[Atlas M0] Scene built. Press play, hold right mouse to look, Tab for the readout.\n" +
                "Watch the orbiting marker pass behind you: which end of the bar it leaves, and " +
                "which screen edge its icon pins to.\n" +
                "For a single-view scene, delete 'Compass Bar' or 'Screen Indicators' - the " +
                "presenters register themselves, so removing one changes nothing else.\n" +
                "No icons are assigned, so markers draw as tinted blanks; assign an " +
                "AtlasSpriteIcons asset on the presenters to see sprites.");
        }

        private static Canvas BuildCanvas()
        {
            var canvasObject = new GameObject("Atlas Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            return canvas;
        }

        /// <summary>
        /// Fills the canvas. Indicators are placed as viewport fractions of this rect, so
        /// it has to be the whole screen or the edges will not be where the screen edges
        /// are.
        ///
        /// Added before the bar so it sits underneath it in the hierarchy, and therefore
        /// draws behind it — an indicator sliding under the compass strip reads better
        /// than one drawn over the top of it.
        /// </summary>
        private static void AddIndicatorLayer(Canvas canvas)
        {
            var layer = new GameObject("Screen Indicators", typeof(RectTransform));
            layer.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            layer.AddComponent<ScreenPresenter>();
        }

        /// <summary>
        /// A strip across the bottom. Its width is what a bearing maps across, so the
        /// presenter reads the rect rather than assuming a size — resize it and the
        /// mapping follows.
        /// </summary>
        private static void AddCompassBar(Canvas canvas)
        {
            var barObject = new GameObject("Compass Bar", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)barObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 48f);
            rect.sizeDelta = new Vector2(900f, 56f);

            Image backing = barObject.GetComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.35f);
            backing.raycastTarget = false;

            barObject.AddComponent<BarPresenter>();
        }
    }
}
