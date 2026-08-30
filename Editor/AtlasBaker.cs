using System.IO;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// M3: renders a space top-down into an image the maps draw under their markers.
    ///
    /// <b>Baking is an editor render of a space's bounds, and that is the whole idea.</b>
    /// Because a space is already a plane with a world transform and a known extent, the
    /// map image is not a new concept anyone has to author and keep in sync - it is a
    /// picture of a volume the system already knows. Which is why the design put baking
    /// behind the space model rather than beside it.
    ///
    /// The one property that has to hold exactly: <b>the image covers the bounds, corner
    /// to corner, with no padding and no letterboxing.</b> Everything downstream assumes
    /// it - <see cref="MinimapPresenter"/> turns a frame into a uv window by dividing by
    /// the bounds, and at M4 a reveal mask will be indexed the same way. An image that is
    /// a few percent off is a map whose terrain slides against its markers as you move,
    /// which reads as the markers being wrong.
    /// </summary>
    public static class AtlasBaker
    {
        private const string DefaultFolder = "Assets/Atlas Bakes";

        [MenuItem("Window/Liminal Labs/Atlas/Bake Spaces in Scene", priority = 320)]
        public static void BakeAll()
        {
            AtlasSpaceBehaviour[] spaces = AtlasEditorScene.FindAll<AtlasSpaceBehaviour>();
            if (spaces.Length == 0)
            {
                EditorUtility.DisplayDialog("Atlas Bake",
                    "No AtlasSpaceBehaviour in the scene.\n\n" +
                    "A space is what a bake is a picture of: add one, size its bounds to " +
                    "the area the map should cover, then bake.", "OK");
                return;
            }

            int baked = 0;
            foreach (AtlasSpaceBehaviour space in spaces)
                if (Bake(space) != null) baked++;

            EditorUtility.DisplayDialog("Atlas Bake",
                $"Baked {baked} of {spaces.Length} space(s) into {DefaultFolder}.\n\n" +
                "Assign a Raw Image to each map presenter's Background field if you have " +
                "not already - the image has nowhere to draw without one.", "OK");
        }

        /// <summary>
        /// Renders one space and assigns the result to it.
        ///
        /// Returns the saved texture, or null if the space could not be baked - which is
        /// reported rather than thrown, because baking several spaces should not stop at
        /// the first one with empty bounds.
        /// </summary>
        public static Texture2D Bake(AtlasSpaceBehaviour space)
        {
            if (space == null) return null;

            Bounds bounds = space.WorldBounds;
            if (bounds.size.x < 0.01f || bounds.size.z < 0.01f)
            {
                Debug.LogWarning(
                    $"[Atlas] '{space.name}' has no meaningful bounds, so there is nothing " +
                    "to bake. Size them to the area the map should cover.", space);
                return null;
            }

            // The texture takes the bounds' aspect rather than being squared off. The
            // presenter maps uv 0..1 onto the bounds, so a square image of a rectangular
            // space would stretch - and the stretch is invisible until you notice the
            // terrain drifting against the markers.
            float aspect = bounds.size.x / bounds.size.z;
            int width = aspect >= 1f ? space.BakeResolution : Mathf.RoundToInt(space.BakeResolution * aspect);
            int height = aspect >= 1f ? Mathf.RoundToInt(space.BakeResolution / aspect) : space.BakeResolution;

            width = Mathf.Max(64, width);
            height = Mathf.Max(64, height);

            Texture2D image = Render(space, bounds, width, height, aspect);
            if (image == null) return null;

            string path = Save(space, image);
            Object.DestroyImmediate(image);

            var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (saved == null) return null;

            Undo.RecordObject(space, "Bake Atlas Space");
            space.SetImage(saved);
            EditorUtility.SetDirty(space);

            Debug.Log($"[Atlas] Baked '{space.name}' to {path} ({width}x{height}, " +
                      $"covering {bounds.size.x:0}x{bounds.size.z:0} world units).", saved);

            return saved;
        }

        /// <summary>
        /// The render itself: an orthographic camera looking straight down, framed to the
        /// bounds exactly.
        ///
        /// Orthographic on purpose. A perspective camera high enough to see the whole
        /// space would still show the sides of everything tall, so buildings would lean
        /// outward from the centre and a marker on a roof would not sit over its building.
        /// </summary>
        private static Texture2D Render(AtlasSpaceBehaviour space, Bounds bounds,
                                        int width, int height, float aspect)
        {
            var cameraObject = new GameObject("Atlas Bake Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();

            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            try
            {
                // Looking down, with the camera's up along +Z so world north is image up -
                // which is the convention every other part of this package uses.
                cameraObject.transform.position =
                    new Vector3(bounds.center.x, bounds.max.y + space.BakeHeadroom, bounds.center.z);
                cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

                camera.orthographic = true;

                // orthographicSize is a half-height along the camera's local up, which is
                // world +Z here. Half the depth, not half the width - swapping them is a
                // map that is correct on one axis and wrong on the other, which looks like
                // a rotation bug.
                camera.orthographicSize = bounds.extents.z;
                camera.aspect = aspect;

                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = bounds.size.y + space.BakeHeadroom * 2f + 1f;
                camera.cullingMask = space.BakeLayers;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = space.BakeBackground;
                camera.allowMSAA = false;
                camera.enabled = false;
                camera.targetTexture = target;

                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();

                return image;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Atlas] Baking '{space.name}' failed: {e.Message}", space);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        /// <summary>
        /// Writes the PNG and imports it with settings a map wants.
        ///
        /// Mipmaps on, because a world map zoomed out is the texture minified hard, and
        /// without them it crawls. Clamped, because a map that wraps shows the far side of
        /// the world past its own edge.
        /// </summary>
        private static string Save(AtlasSpaceBehaviour space, Texture2D image)
        {
            if (!Directory.Exists(DefaultFolder))
            {
                Directory.CreateDirectory(DefaultFolder);
                AssetDatabase.Refresh();
            }

            string scene = space.gameObject.scene.name;
            string safe = string.IsNullOrEmpty(scene) ? "Scene" : scene;
            string path = $"{DefaultFolder}/{safe}_{space.name}.png";

            File.WriteAllBytes(path, image.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return path;
        }
    }
}
