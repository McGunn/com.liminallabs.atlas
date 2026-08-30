using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// The space inspector, with a bake button and the two numbers that decide whether a
    /// bake is worth looking at.
    ///
    /// The default inspector is nearly right; what it cannot say is how big the region
    /// actually is in world units and what a pixel of the baked image will be worth. A
    /// bake at 1024 across a 4km region is 4 metres a pixel, which is a map with no
    /// buildings on it - and that is much easier to see before waiting for the render.
    /// </summary>
    [CustomEditor(typeof(AtlasSpaceBehaviour))]
    public sealed class AtlasSpaceInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var space = (AtlasSpaceBehaviour)target;
            Bounds bounds = space.WorldBounds;

            EditorGUILayout.Space();

            if (bounds.size.x < 0.01f || bounds.size.z < 0.01f)
            {
                EditorGUILayout.HelpBox(
                    "These bounds have no area, so there is nothing to frame and nothing to " +
                    "bake. Size them to the region this map should cover.",
                    MessageType.Error);
                return;
            }

            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            float metresPerPixel = longest / Mathf.Max(1, space.BakeResolution);

            EditorGUILayout.LabelField("Covers",
                $"{bounds.size.x:0.#} x {bounds.size.z:0.#} world units");
            EditorGUILayout.LabelField("Baked detail",
                $"{metresPerPixel:0.###} units per pixel");

            if (metresPerPixel > 1f)
            {
                EditorGUILayout.HelpBox(
                    $"At {metresPerPixel:0.#} units per pixel a doorway is less than a pixel " +
                    "wide. Raise the resolution, or split this into several spaces - which " +
                    "is what spaces are for.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake Map Image", GUILayout.Height(26f)))
            {
                AtlasBaker.Bake(space);
            }

            if (space.Image != null)
            {
                EditorGUILayout.Space();
                Rect preview = GUILayoutUtility.GetAspectRect(bounds.size.x / bounds.size.z);
                EditorGUI.DrawPreviewTexture(preview, space.Image, null, ScaleMode.ScaleToFit);
            }
        }
    }
}
