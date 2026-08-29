using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas.SampleM0.Editor
{
    /// <summary>
    /// Builds the M0 scene.
    ///
    /// A builder rather than a committed .unity file, which is the house pattern for
    /// samples and earns its keep twice: a generated scene cannot carry a stale GUID
    /// reference to an asset that moved, and the build code doubles as readable
    /// documentation of exactly what wiring the package needs. Nothing here is magic -
    /// it is the same five components anyone would add by hand.
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

            // Entry point 1: a component. This one is a real object in the world, and it
            // is the one that orbits.
            GameObject orbiting = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            orbiting.name = "Orbiting Marker";
            orbiting.transform.position = new Vector3(0f, 1f, 18f);
            orbiting.AddComponent<AtlasMarkerBehaviour>();

            GameObject canvasObject = BuildCanvas(out BarPresenter bar, out ScreenPresenter screen);

            var demo = registryObject.AddComponent<AtlasM0Demo>();
            SerializedObject serialized = new SerializedObject(demo);
            serialized.FindProperty("registry").objectReferenceValue = registry;
            serialized.FindProperty("bar").objectReferenceValue = bar;
            serialized.FindProperty("screen").objectReferenceValue = screen;
            serialized.FindProperty("orbiting").objectReferenceValue = orbiting.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = registryObject;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[Atlas M0] Scene built. Press play and hold right mouse to look.\n" +
                "Three markers, three entry points, two views, one registry. Watch the " +
                "orbiting marker pass behind you - which end of the bar it leaves, and " +
                "which screen edge its icon pins to.\n" +
                "No icons are assigned, so markers draw as tinted blanks; assign an " +
                "AtlasSpriteIcons asset on the presenters to see sprites.");
        }

        private static GameObject BuildCanvas(out BarPresenter bar, out ScreenPresenter screen)
        {
            var canvasObject = new GameObject("Atlas Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // The screen layer fills the canvas: its indicators are positioned in
            // viewport fractions of this rect.
            var screenObject = new GameObject("Screen Indicators", typeof(RectTransform));
            screenObject.transform.SetParent(canvasObject.transform, false);
            Fill((RectTransform)screenObject.transform);
            screen = screenObject.AddComponent<ScreenPresenter>();

            // The bar is a strip across the bottom. Its width is what a bearing maps
            // across, so the presenter reads it rather than assuming a size.
            var barObject = new GameObject("Compass Bar", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(canvasObject.transform, false);

            var barRect = (RectTransform)barObject.transform;
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 48f);
            barRect.sizeDelta = new Vector2(900f, 56f);

            var barImage = barObject.GetComponent<Image>();
            barImage.color = new Color(0f, 0f, 0f, 0.35f);
            barImage.raycastTarget = false;

            bar = barObject.AddComponent<BarPresenter>();

            return canvasObject;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
