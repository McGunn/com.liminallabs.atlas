using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LiminalLabs.Atlas.SampleCore.Editor
{
    /// <summary>
    /// Builds the Core Sample scene.
    ///
    /// A builder rather than a committed .unity file: a generated scene cannot carry a
    /// stale GUID to an asset that moved, and the build code doubles as readable
    /// documentation of exactly what wiring the package needs.
    /// </summary>
    public static class AtlasCoreSceneBuilder
    {
        [MenuItem("Window/Liminal Labs/Atlas/Build Core Sample Scene", priority = 300)]
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

            // Entry point 1: a component on a real object, and the one that orbits.
            GameObject orbiting = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            orbiting.name = "Orbiting Marker";
            orbiting.transform.position = new Vector3(0f, 1f, 18f);
            orbiting.AddComponent<AtlasMarkerBehaviour>();

            // No canvas and no presenter: this sample prints the solve instead of
            // drawing it, which is the honest demonstration of a package that draws nothing.

            var demo = registryObject.AddComponent<AtlasCoreDemo>();
            var serialized = new SerializedObject(demo);
            serialized.FindProperty("registry").objectReferenceValue = registry;
            serialized.FindProperty("orbiting").objectReferenceValue = orbiting.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = registryObject;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[Atlas] Core Sample scene built. Press play and hold right mouse to look.\n" + "Three entry points, one registry, no presenter - the solve is printed on screen.");
        }
    }
}
