using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas.SampleM1.Editor
{
    /// <summary>
    /// Builds the M1 sample scene and saves it into the sample folder.
    ///
    /// Four views of one registration: a compass bar, on-screen indicators, a minimap that
    /// follows and turns, and a world map that neither follows nor turns. The last two are
    /// the milestone — and they are the same <see cref="MapProjection"/> with different
    /// numbers, which is easier to believe when you can hold M and watch the same markers
    /// appear at a different framing rather than take it on trust.
    /// </summary>
    public static class AtlasM1SceneBuilder
    {
        private const string SharedSprites =
            "Packages/com.liminallabs.shareddemoassets/Creative Commons 0 Sprites/GenericUI/";

        [MenuItem("Window/Liminal Labs/Atlas/Build M1 Sample Scene", priority = 301)]
        public static void Build()
        {
            // Resolved before the new scene replaces the open one: if the sample cannot be
            // located there is nowhere to save the result, and discarding someone's open
            // scene to build something that then exists only in memory is a bad trade.
            string folder = SampleFolder();
            if (folder == null)
            {
                EditorUtility.DisplayDialog("Atlas M1",
                    "Could not locate the sample folder from this script's own position.\n\n" +
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
                camera.farClipPlane = 500f;
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 40f;

            var registryObject = new GameObject("Atlas Registry");
            var registry = registryObject.AddComponent<AtlasRegistryBehaviour>();
            registry.ViewerCamera = camera;

            // The space, authored as a component.
            //
            // Writing straight into registry.Registry.Spaces here would do nothing: the
            // registry is a plain object built when the component wakes, so anything an
            // editor script puts into it is thrown away before play. The world map then
            // framed a space whose bounds were always zero and fell back to a radius meant
            // for a minimap - which is exactly what it looked like.
            var spaceObject = new GameObject("Atlas Space");
            var space = spaceObject.AddComponent<AtlasSpaceBehaviour>();

            var spaceFields = new SerializedObject(space);
            spaceFields.FindProperty("boundsSize").vector3Value = new Vector3(400f, 20f, 400f);
            spaceFields.FindProperty("centreOnTransform").boolValue = false;
            spaceFields.FindProperty("boundsCentre").vector3Value = Vector3.zero;
            spaceFields.ApplyModifiedPropertiesWithoutUndo();

            GameObject orbiting = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            orbiting.name = "Orbiting Signal";
            orbiting.transform.position = new Vector3(0f, 1f, 45f);

            var orbitingMarker = orbiting.AddComponent<AtlasMarkerBehaviour>();
            var marker = new SerializedObject(orbitingMarker);
            marker.FindProperty("kind").enumValueIndex = (int)AtlasMarkerKind.Point;
            marker.FindProperty("label").stringValue = "Orbiting Signal";
            marker.FindProperty("iconId").intValue = AtlasM1Icons.Signal;
            marker.FindProperty("tint").colorValue = new Color(1f, 0.35f, 0.5f);
            marker.FindProperty("priority").floatValue = 0.75f;
            marker.ApplyModifiedPropertiesWithoutUndo();

            AtlasSpriteIcons icons = BuildIcons(folder);

            Canvas canvas = BuildCanvas();
            AddIndicatorLayer(canvas, icons);
            AddCompassBar(canvas, icons);
            MinimapPresenter minimap = AddMinimap(canvas, icons);
            GameObject worldMap = AddWorldMap(canvas, icons);

            var demo = registryObject.AddComponent<AtlasM1Demo>();
            var serialized = new SerializedObject(demo);
            serialized.FindProperty("registry").objectReferenceValue = registry;
            serialized.FindProperty("worldMap").objectReferenceValue = worldMap;
            serialized.FindProperty("worldMapPresenter").objectReferenceValue =
                worldMap.GetComponent<MinimapPresenter>();
            serialized.FindProperty("orbiting").objectReferenceValue = orbiting.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = registryObject;

            string scenePath = folder + "/Atlas_M1.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log(
                "[Atlas M1] Scene saved to " + scenePath + ".\n" +
                "WASD to move, hold right mouse to look, M for the world map.\n" +
                "The minimap turns under a fixed arrow; the world map does not turn at all. " +
                "Same markers, same solve, two framings of one projection.\n" +
                "On the world map: scroll to zoom, drag with the left mouse to pan, R to reset.\n" +
                "Watch a landmark pin to the minimap's circle while it is still sitting in " +
                "place on the world map: " + minimap.name + " is following you, the world map is not.");
        }

        /// <summary>
        /// Where this sample actually lives, which is not knowable in advance.
        ///
        /// A package developer reaches it through the junction that Link Samples for
        /// Editing creates, and a scene saved through that junction lands in the package
        /// repository. A consumer imports it and gets their own copy under Assets/Samples.
        /// Asking the AssetDatabase covers both and cannot go stale.
        /// </summary>
        private static string SampleFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets("AtlasM1SceneBuilder t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/Editor/AtlasM1SceneBuilder.cs")) continue;

                string editorFolder = Path.GetDirectoryName(path).Replace('\\', '/');
                return Path.GetDirectoryName(editorFolder).Replace('\\', '/');
            }

            return null;
        }

        /// <summary>
        /// One icon set, four views.
        ///
        /// The compass, the indicators, the minimap and the world map resolve the same id
        /// through the same provider, so an objective cannot be a flag in one place and a
        /// star in another. That is the shared-solve argument applied to art.
        /// </summary>
        private static AtlasSpriteIcons BuildIcons(string folder)
        {
            var sprites = new List<Sprite>();
            var missing = new List<string>();

            foreach (string name in AtlasM1Icons.SpriteNames)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SharedSprites + name + ".png");
                if (sprite == null) missing.Add(name);
                sprites.Add(sprite);
            }

            if (missing.Count == AtlasM1Icons.SpriteNames.Length)
            {
                Debug.LogWarning(
                    "[Atlas M1] com.liminallabs.shareddemoassets is not installed, so the demo " +
                    "has no icons. Markers draw as the missing-sprite placeholder; everything " +
                    "else in the milestone works.");
                return null;
            }

            string path = folder + "/AtlasIcons.asset";
            var icons = AssetDatabase.LoadAssetAtPath<AtlasSpriteIcons>(path);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<AtlasSpriteIcons>();
                AssetDatabase.CreateAsset(icons, path);
            }

            var serialized = new SerializedObject(icons);
            SerializedProperty list = serialized.FindProperty("icons");
            list.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(icons);
            return icons;
        }

        private static Sprite Shared(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(SharedSprites + name + ".png");

        /// <summary>
        /// The layer a baked space image is drawn on, under everything else.
        ///
        /// Stretched to the whole map rect, because the presenter expresses the visible
        /// window as a uv rect over the space's bounds - the image has to fill the rect for
        /// that arithmetic to land where the markers do. It draws nothing until the space
        /// has an image, which is a bake away.
        /// </summary>
        private static RawImage AddBackground(RectTransform parent)
        {
            var go = new GameObject("Map Image", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;    // the presenter switches it on when a space has one

            // First, so markers and the viewer arrow draw over it.
            go.transform.SetAsFirstSibling();

            return image;
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
            serialized.FindProperty("arrowSprite").objectReferenceValue =
                Shared(AtlasM1Icons.ArrowSpriteName);
            serialized.FindProperty("showDistanceLabels").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddCompassBar(Canvas canvas, AtlasSpriteIcons icons)
        {
            var barObject = new GameObject("Compass Bar", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)barObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(900f, 76f);

            Image backing = barObject.GetComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.35f);
            backing.raycastTarget = false;

            barObject.AddComponent<CanvasGroup>();

            var bar = barObject.AddComponent<BarPresenter>();
            var serialized = new SerializedObject(bar);
            serialized.FindProperty("icons").objectReferenceValue = icons;
            serialized.FindProperty("markerY").floatValue = 10f;
            serialized.FindProperty("labelOffsetY").floatValue = -24f;
            serialized.FindProperty("directionY").floatValue = 26f;
            serialized.FindProperty("includeDiagonals").boolValue = true;
            serialized.FindProperty("fadeWhenIdle").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The minimap: bottom-right, round, following and turning.
        ///
        /// Round with a real circular mask rather than a square one that pretends: the
        /// presenter pins markers to a circle in map fractions, and on a square rect with
        /// square clipping the pinning looks like a spacing bug instead of a shape.
        /// </summary>
        private static MinimapPresenter AddMinimap(Canvas canvas, AtlasSpriteIcons icons)
        {
            var mapObject = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask));
            mapObject.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)mapObject.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-32f, 32f);
            rect.sizeDelta = new Vector2(256f, 256f);

            Image frame = mapObject.GetComponent<Image>();
            frame.sprite = Shared("UI_DotBig");
            frame.color = new Color(0.05f, 0.07f, 0.1f, 0.85f);
            frame.raycastTarget = false;

            // Keeps the dark disc visible under the markers; the Mask alone would draw
            // nothing and the map would float with no edge to read it against.
            mapObject.GetComponent<Mask>().showMaskGraphic = true;

            RawImage minimapBackground = AddBackground(rect);

            var viewerArrow = new GameObject("Viewer", typeof(RectTransform), typeof(Image));
            viewerArrow.transform.SetParent(rect, false);
            var arrowRect = (RectTransform)viewerArrow.transform;
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(26f, 26f);

            Image arrowImage = viewerArrow.GetComponent<Image>();
            arrowImage.sprite = Shared(AtlasM1Icons.ViewerSpriteName);
            arrowImage.color = Color.white;
            arrowImage.raycastTarget = false;

            var minimap = mapObject.AddComponent<MinimapPresenter>();
            var serialized = new SerializedObject(minimap);
            serialized.FindProperty("icons").objectReferenceValue = icons;
            serialized.FindProperty("round").boolValue = true;
            serialized.FindProperty("clipToRect").boolValue = false;   // the Mask does it
            serialized.FindProperty("radius").floatValue = 70f;
            serialized.FindProperty("centre").enumValueIndex = (int)AtlasMapCentre.Viewer;
            serialized.FindProperty("rotation").enumValueIndex = (int)AtlasMapRotation.ViewerUp;
            serialized.FindProperty("viewerArrow").objectReferenceValue = arrowRect;
            serialized.FindProperty("background").objectReferenceValue = minimapBackground;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The arrow is drawn last so markers cannot cover the thing telling you where
            // you are.
            viewerArrow.transform.SetAsLastSibling();

            return minimap;
        }

        /// <summary>
        /// The world map: centred, square, north-up, framing the whole space.
        ///
        /// Every difference from the minimap is a number on the projection. That is the
        /// milestone's claim, and building it any other way here would quietly disprove it.
        /// </summary>
        private static GameObject AddWorldMap(Canvas canvas, AtlasSpriteIcons icons)
        {
            var mapObject = new GameObject("World Map", typeof(RectTransform), typeof(Image));
            mapObject.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)mapObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 760f);

            Image backing = mapObject.GetComponent<Image>();
            backing.color = new Color(0.04f, 0.05f, 0.07f, 0.94f);
            backing.raycastTarget = false;

            RawImage worldBackground = AddBackground(rect);

            var worldMap = mapObject.AddComponent<MinimapPresenter>();
            var serialized = new SerializedObject(worldMap);
            serialized.FindProperty("icons").objectReferenceValue = icons;
            serialized.FindProperty("round").boolValue = false;
            serialized.FindProperty("clipToRect").boolValue = true;
            serialized.FindProperty("centre").enumValueIndex = (int)AtlasMapCentre.SpaceBounds;
            serialized.FindProperty("rotation").enumValueIndex = (int)AtlasMapRotation.NorthUp;
            serialized.FindProperty("markerSize").vector2Value = new Vector2(30f, 30f);
            serialized.FindProperty("pinOutsideMarkers").boolValue = false;
            serialized.FindProperty("background").objectReferenceValue = worldBackground;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return mapObject;
        }
    }
}
