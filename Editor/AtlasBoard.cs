using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Atlas.Editor
{
    /// <summary>
    /// M5: the registry, live, while the game runs.
    ///
    /// Every bug in this package so far has been one where the numbers were right and the
    /// pixels were wrong, or the reverse — an indicator half a screen out, a compass that
    /// drew nothing, a world map framed to bounds nobody had set. The console's
    /// <c>atlas.markers</c> answers that one line at a time; this answers it continuously,
    /// beside the scene view, while you move.
    ///
    /// Read-only on purpose, apart from the reveal buttons. A board that lets you edit
    /// solved values is a board that lets you convince yourself a bug is fixed.
    /// </summary>
    public sealed class AtlasBoard : EditorWindow
    {
        private Vector2 scroll;
        private bool showMarkers = true;
        private bool showSpaces = true;
        private bool showPresenters = true;

        [MenuItem("Window/Liminal Labs/Atlas/Atlas Board", priority = 310)]
        public static void Open() =>
            GetWindow<AtlasBoard>("Atlas Board").minSize = new Vector2(420f, 300f);

        private void OnEnable() => EditorApplication.update += Repaint;
        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            AtlasRegistryBehaviour[] registries = AtlasEditorScene.FindAll<AtlasRegistryBehaviour>();

            if (registries.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No AtlasRegistryBehaviour in the scene. The board reads a live registry, " +
                    "so there is nothing to show until one exists.", MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Not playing. Bearings, distances and discovery are solved per frame, so " +
                    "the interesting half of this is empty until you press play.",
                    MessageType.Info);
            }

            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;

                foreach (AtlasRegistryBehaviour behaviour in registries)
                {
                    DrawRegistry(behaviour);
                    EditorGUILayout.Space();
                }
            }
        }

        private void DrawRegistry(AtlasRegistryBehaviour behaviour)
        {
            AtlasRegistry registry = behaviour.Registry;

            EditorGUILayout.LabelField(behaviour.name, EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                Camera camera = behaviour.ViewerCamera;
                EditorGUILayout.LabelField("Viewer",
                    camera != null ? camera.name : "none — nothing will solve");
                EditorGUILayout.LabelField("Tracked", registry.Tracked.Count.ToString());

                showSpaces = EditorGUILayout.Foldout(showSpaces, "Spaces", true);
                if (showSpaces) DrawSpaces(registry);

                showPresenters = EditorGUILayout.Foldout(showPresenters, "Views", true);
                if (showPresenters) DrawPresenters();

                showMarkers = EditorGUILayout.Foldout(showMarkers, "Markers", true);
                if (showMarkers) DrawMarkers(behaviour, registry);
            }
        }

        private static void DrawSpaces(AtlasRegistry registry)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (AtlasSpace space in registry.Spaces.All)
                {
                    Bounds bounds = space.WorldBounds;
                    bool sized = bounds.size.x > 0.01f && bounds.size.z > 0.01f;

                    EditorGUILayout.LabelField(space.Name,
                        sized ? $"{bounds.size.x:0} x {bounds.size.z:0} units"
                              : "no bounds — a map framed to this has nothing to frame");

                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.LabelField("Image",
                            space.Image != null ? space.Image.name : "none — bake or assign one");

                        if (space.Reveal == null)
                        {
                            EditorGUILayout.LabelField("Discovery", "no mask");
                            continue;
                        }

                        EditorGUILayout.LabelField("Discovery",
                            $"{space.Reveal.RevealedFraction() * 100f:0.#}% of " +
                            $"{space.Reveal.Width}x{space.Reveal.Height}");

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(EditorGUI.indentLevel * 15f);
                            if (GUILayout.Button("Reveal All", GUILayout.Width(90f)))
                                space.Reveal.RevealAll();
                            if (GUILayout.Button("Clear", GUILayout.Width(70f)))
                                space.Reveal.Clear();
                        }
                    }
                }
            }
        }

        private static void DrawPresenters()
        {
            using (new EditorGUI.IndentLevelScope())
            {
                Row("Compass bars", AtlasEditorScene.FindAll<BarPresenter>().Length);
                Row("Screen layers", AtlasEditorScene.FindAll<ScreenPresenter>().Length);
                Row("Maps", AtlasEditorScene.FindAll<MinimapPresenter>().Length);
            }

            void Row(string label, int count) =>
                EditorGUILayout.LabelField(label, count == 0 ? "none" : count.ToString());
        }

        /// <summary>
        /// Every tracked marker, solved against the live viewer.
        ///
        /// Solved here rather than read from a presenter's pool, because a presenter's pool
        /// is the thing under suspicion whenever anyone opens this window. Running the same
        /// pure functions on the same viewer is what separates "the maths is wrong" from
        /// "the drawing is wrong", which is the question every bug in this package has
        /// turned on.
        /// </summary>
        private static void DrawMarkers(AtlasRegistryBehaviour behaviour, AtlasRegistry registry)
        {
            Camera camera = behaviour.ViewerCamera;
            if (camera == null || registry.Tracked.Count == 0) return;

            AtlasViewer viewer = AtlasViewer.FromCamera(camera, behaviour.Space);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    "  label                 bearing    distance   where", EditorStyles.miniLabel);

                IReadOnlyList<IAtlasTrackable> tracked = registry.Tracked;
                for (int i = 0; i < tracked.Count; i++)
                {
                    IAtlasTrackable target = tracked[i];
                    if (target == null) continue;

                    Vector3 at = target.Position;
                    Vector3 viewport = AtlasMath.Viewport(viewer, at);

                    string where = viewport.z < 0f ? "BEHIND"
                        : AtlasMath.IsOnScreen(viewport) ? "on screen"
                        : "off screen";

                    AtlasSpace space = registry.Spaces.GetOrDefault(target.Space);
                    if (space.Reveal != null && !space.IsRevealed(at)) where += ", fogged";

                    EditorGUILayout.LabelField(string.Format(
                        "  {0,-20} {1,7:0.0}° {2,9:0.0} m   {3}",
                        target.Marker.Label ?? target.Marker.Kind.ToString(),
                        AtlasMath.Bearing(viewer, at),
                        Vector3.Distance(viewer.Position, at),
                        where), EditorStyles.miniLabel);
                }
            }
        }
    }
}
