using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas.SampleM0.Editor
{
    /// <summary>
    /// Builds the M0 sample scene and saves it into the sample folder, so it lands
    /// beside the other Liminal Labs demos as a real asset you can reopen.
    ///
    /// A builder rather than a hand-authored scene, for two reasons. A generated
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
            // Resolved before the new scene replaces the open one. If the sample cannot be
            // located there is nowhere to save the result, and throwing away someone's open
            // scene to build something that then exists only in memory is not a trade worth
            // making.
            string folder = SampleFolder();
            if (folder == null)
            {
                EditorUtility.DisplayDialog("Atlas M0",
                    "Could not locate the sample folder from this script's own position." + "\n\n" +
                    "Link it with Window > Liminal Labs > Developer > Link Samples for " +
                    "Editing, or import it from the Package Manager.", "OK");
                return;
            }

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
            var orbitingMarker = orbiting.AddComponent<AtlasMarkerBehaviour>();

            var marker = new SerializedObject(orbitingMarker);
            marker.FindProperty("kind").enumValueIndex = (int)AtlasMarkerKind.Point;
            marker.FindProperty("label").stringValue = "Orbiting Signal";
            marker.FindProperty("iconId").intValue = AtlasM0Icons.Signal;
            marker.FindProperty("tint").colorValue = new Color(1f, 0.35f, 0.5f);
            marker.FindProperty("priority").floatValue = 0.75f;
            marker.ApplyModifiedPropertiesWithoutUndo();

            AtlasSpriteIcons icons = BuildIcons(folder);

            Canvas canvas = BuildCanvas();
            AddIndicatorLayer(canvas, icons);
            AddCompassBar(canvas, icons);

            var demo = registryObject.AddComponent<AtlasM0Demo>();
            var serialized = new SerializedObject(demo);
            serialized.FindProperty("registry").objectReferenceValue = registry;
            serialized.FindProperty("orbiting").objectReferenceValue = orbiting.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = registryObject;

            string scenePath = folder + "/Atlas_M0.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log(
                "[Atlas M0] Scene saved to " + scenePath + ".\n" +
                "Press play, hold right mouse to look, Tab for the readout.\n" +
                "Watch the orbiting marker pass behind you: which end of the bar it leaves, and " +
                "which screen edge its icon pins to.\n" +
                "For a single-view scene, delete 'Compass Bar' or 'Screen Indicators' - the " +
                "presenters register themselves, so removing one changes nothing else.\n" +
                "No icons are assigned, so markers draw as tinted blanks; assign an " +
                "AtlasSpriteIcons asset on the presenters to see sprites.");
        }

        /// <summary>
        /// Where this sample actually lives, which is not knowable in advance.
        ///
        /// A package developer reaches it through the junction that
        /// <c>Window > Liminal Labs > Developer > Link Samples for Editing</c> creates at
        /// <c>Assets/LiminalLabsSamples/com.liminallabs.atlas/Atlas M0</c>, and a scene
        /// saved through that junction lands in the package repository. That is how the
        /// other Liminal Labs demos come to ship a committed scene. A consumer imports the
        /// sample instead and gets <c>Assets/Samples/Liminal Labs Atlas/[version]/Atlas M0</c>.
        ///
        /// Asking the AssetDatabase where this script sits covers both, and cannot go stale
        /// the way a hardcoded path does the moment a display name or a version changes.
        /// </summary>
        private static string SampleFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets("AtlasM0SceneBuilder t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/Editor/AtlasM0SceneBuilder.cs")) continue;

                // .../Atlas M0/Editor/AtlasM0SceneBuilder.cs  ->  .../Atlas M0
                string editorFolder = Path.GetDirectoryName(path).Replace('\\', '/');
                return Path.GetDirectoryName(editorFolder).Replace('\\', '/');
            }

            return null;
        }

        private const string SharedSprites =
            "Packages/com.liminallabs.shareddemoassets/Creative Commons 0 Sprites/GenericUI/";

        /// <summary>
        /// Builds the icon set the two presenters share.
        ///
        /// One asset, both views: the compass and the on-screen indicators resolve the
        /// same id through the same provider, so an objective cannot end up a flag on one
        /// and a star on the other. That is the same argument as the shared solve, applied
        /// to art.
        ///
        /// Returns null when the shared demo assets package is absent. That is not a
        /// failure - the presenters draw tinted blanks without it, exactly as
        /// IAtlasIconProvider promises a missing icon costs, and the milestone is about
        /// where markers are rather than what they look like.
        /// </summary>
        private static AtlasSpriteIcons BuildIcons(string folder)
        {
            var sprites = new List<Sprite>();
            var missing = new List<string>();

            foreach (string name in AtlasM0Icons.SpriteNames)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SharedSprites + name + ".png");
                if (sprite == null) missing.Add(name);
                sprites.Add(sprite);
            }

            if (missing.Count == AtlasM0Icons.SpriteNames.Length)
            {
                Debug.LogWarning(
                    "[Atlas M0] com.liminallabs.shareddemoassets is not installed, so the " +
                    "demo has no icons. Markers draw as tinted blanks; everything else in " +
                    "the milestone works.");
                return null;
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning("[Atlas M0] Missing sprites: " + string.Join(", ", missing));
            }

            string path = folder + "/AtlasIcons.asset";
            var icons = AssetDatabase.LoadAssetAtPath<AtlasSpriteIcons>(path);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<AtlasSpriteIcons>();
                AssetDatabase.CreateAsset(icons, path);
            }

            // Written through SerializedObject rather than a public setter: the list is
            // private because its order is a contract with AtlasM0Icons, and widening the
            // API of a shipped type to make a sample easier to build is the wrong trade.
            var serialized = new SerializedObject(icons);
            SerializedProperty list = serialized.FindProperty("icons");
            list.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(icons);
            return icons;
        }

        private static Sprite Arrow() => AssetDatabase.LoadAssetAtPath<Sprite>(
            SharedSprites + AtlasM0Icons.ArrowSpriteName + ".png");

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
        private static void AddIndicatorLayer(Canvas canvas, AtlasSpriteIcons icons)
        {
            var layer = new GameObject("Screen Indicators", typeof(RectTransform));
            layer.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var screen = layer.AddComponent<ScreenPresenter>();

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("icons").objectReferenceValue = icons;
            serialized.FindProperty("arrowSprite").objectReferenceValue = Arrow();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A strip across the bottom. Its width is what a bearing maps across, so the
        /// presenter reads the rect rather than assuming a size — resize it and the
        /// mapping follows.
        /// </summary>
        private static void AddCompassBar(Canvas canvas, AtlasSpriteIcons icons)
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

            var bar = barObject.AddComponent<BarPresenter>();

            var serialized = new SerializedObject(bar);
            serialized.FindProperty("icons").objectReferenceValue = icons;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
