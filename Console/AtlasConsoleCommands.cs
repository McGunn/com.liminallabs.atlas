using System;
using System.Collections.Generic;
using LiminalLabs.Core.Console;
using UnityEngine;

namespace LiminalLabs.Atlas.Console
{
    /// <summary>
    /// Where the console finds a registry.
    ///
    /// There is no singleton, on purpose, so the console cannot go looking for one -
    /// split-screen has two and a menu scene has none. A game points this at whichever
    /// registry it wants to inspect:
    ///
    /// <code>AtlasConsole.Registry = myRegistry;</code>
    ///
    /// <see cref="AtlasRegistryBehaviour"/> does it automatically when there is one in
    /// the scene, so the usual case needs no code at all.
    /// </summary>
    public static class AtlasConsole
    {
        /// <summary>The registry the atlas commands report on.</summary>
        public static AtlasRegistry Registry { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registry = null;

        internal static AtlasRegistry Require()
        {
            if (Registry != null) return Registry;

            // Falling back to a scene search, and only here. This assembly is a developer
            // tool that already references the runtime; the runtime does not reference it
            // and must not, or the no-singleton rule would be a singleton with extra steps.
            // Finding a registry to *report on* is a different thing from gameplay finding
            // one to *use*.
            AtlasRegistryBehaviour found =
                UnityEngine.Object.FindAnyObjectByType<AtlasRegistryBehaviour>(FindObjectsInactive.Include);

            if (found != null)
            {
                Registry = found.Registry;
                return Registry;
            }

            throw new ConsoleException(
                "No atlas registry found. Atlas has no singleton by design, so either put an " +
                "AtlasRegistryBehaviour in the scene or set AtlasConsole.Registry = yourRegistry.");
        }
    }

    /// <summary>
    /// Atlas from the console.
    ///
    /// <b>An early slice of the Atlas Board, which the design puts at M5.</b> It is here
    /// rather than later because the whole M0 acceptance case is a claim about what the
    /// solve produced - "the bar marker leaves the correct end while the screen indicator
    /// clamps to the correct edge" - and checking that by eye in a running scene is
    /// exactly the kind of verification that misses a sign error. `atlas.markers` prints
    /// the numbers behind what is on screen.
    ///
    /// Its own assembly, define-constrained on the console being present, so the package
    /// gains no dependency and this folder simply does not compile without it.
    /// </summary>
    internal static class AtlasConsoleCommands
    {
        private const string Category = "Atlas";

        [ConsoleCommand("atlas", "The registry: what is tracked, and what is drawing it.",
            Category = Category)]
        public static void Info(ConsoleContext context)
        {
            AtlasRegistry registry = AtlasConsole.Require();
            AtlasViewer viewer = registry.LastViewer;

            int tracked = 0;
            for (int i = 0; i < registry.Tracked.Count; i++)
                if (registry.Tracked[i].IsTracked) tracked++;

            context.Heading("ATLAS");
            context.Table(new List<KeyValuePair<string, string>>
            {
                Row("registered", registry.Tracked.Count.ToString()),
                Row("tracked", tracked + ConsoleMarkup.Dim($" of {registry.Tracked.Count}")),
                Row("projections", registry.ProjectionCount.ToString()),
                Row("spaces", registry.Spaces.Count.ToString()),
                Row("max markers", registry.Settings.MaxMarkers.ToString()),
                Row("viewer space", viewer.Space.ToString()),
                Row("viewer at", ConsoleValues.Format(viewer.Position)),
                Row("viewer facing", ConsoleValues.Format(viewer.Forward)),
            }, 16);

            if (registry.ProjectionCount == 0)
                context.Warn("No projections. Nothing will be drawn however much is tracked.");
        }

        [ConsoleCommand("atlas.markers", "Every tracked marker, solved against the last viewer.",
            Category = Category,
            Aliases = new[] { "markers" },
            Description = "Bearing is signed - negative is left. This is the fastest way to tell " +
                          "a marker that is behind you from one that is missing: behind reads " +
                          "near 180, missing does not appear at all.")]
        public static void Markers(
            ConsoleContext context,
            [ConsoleParam("Only markers whose label contains this.")] string filter = null)
        {
            AtlasRegistry registry = AtlasConsole.Require();
            AtlasViewer viewer = registry.LastViewer;

            var rows = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < registry.Tracked.Count; i++)
            {
                IAtlasTrackable target = registry.Tracked[i];
                AtlasMarker marker = target.Marker;

                string label = string.IsNullOrEmpty(marker.Label) ? marker.Kind.ToString() : marker.Label;
                if (!string.IsNullOrEmpty(filter) &&
                    label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                Vector3 position = target.Position;
                float bearing = AtlasMath.Bearing(viewer, position);
                float distance = Vector3.Distance(viewer.Position, position);
                Vector3 viewport = AtlasMath.Viewport(viewer, position);

                var detail = new System.Text.StringBuilder();
                detail.Append(ConsoleMarkup.Value($"{bearing,7:0.#}°"));
                detail.Append(ConsoleMarkup.Dim($"  {distance,7:0.#}m"));

                if (!target.IsTracked) detail.Append(ConsoleMarkup.Dim("  untracked"));
                else if (viewport.z < 0f) detail.Append(ConsoleMarkup.Warn("  behind"));
                else if (AtlasMath.IsOnScreen(viewport)) detail.Append(ConsoleMarkup.Good("  on screen"));
                else detail.Append(ConsoleMarkup.Dim("  off screen"));

                if (target.Space != viewer.Space)
                    detail.Append(ConsoleMarkup.Warn($"  in {target.Space}"));

                float fade = AtlasMath.Fade(distance, marker.MaxDistance);
                if (fade < 1f) detail.Append(ConsoleMarkup.Dim($"  fade {fade:0.00}"));
                if (marker.Priority != 0f) detail.Append(ConsoleMarkup.Dim($"  pri {marker.Priority:0.#}"));

                rows.Add(new KeyValuePair<string, string>(label, detail.ToString()));
            }

            if (rows.Count == 0)
            {
                context.Info("Nothing tracked.");
                return;
            }

            context.Heading($"{rows.Count} marker(s)");
            context.Table(rows, 22);
        }

        [ConsoleCommand("atlas.spaces", "The map planes this registry knows.", Category = Category)]
        public static void Spaces(ConsoleContext context)
        {
            AtlasRegistry registry = AtlasConsole.Require();
            AtlasViewer viewer = registry.LastViewer;

            var rows = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < registry.Spaces.All.Count; i++)
            {
                AtlasSpace space = registry.Spaces.All[i];

                int markers = 0;
                for (int j = 0; j < registry.Tracked.Count; j++)
                    if (registry.Tracked[j].Space == space.Id) markers++;

                var detail = new System.Text.StringBuilder();
                detail.Append(ConsoleMarkup.Dim(space.Id.ToString()));
                detail.Append(ConsoleMarkup.Dim($"  {markers} marker(s)"));
                if (space.Id == viewer.Space) detail.Append(ConsoleMarkup.Accent("  ◄ viewer"));
                if (space.Image == null) detail.Append(ConsoleMarkup.Dim("  no image"));

                rows.Add(new KeyValuePair<string, string>(space.Name, detail.ToString()));
            }

            context.Heading($"{rows.Count} space(s)");
            context.Table(rows, 22);
        }

        [ConsoleCommand("atlas.probe", "What the atlas would say about a world position.",
            Category = Category,
            Description = "Solves an arbitrary point against the last viewer without tracking it. " +
                          "For checking a bearing against something you can see.",
            Examples = new[] { "atlas.probe 0 0 20", "atlas.probe ." })]
        public static void Probe(
            ConsoleContext context,
            [ConsoleParam("World position, or . for the console's selection.")] Vector3 position)
        {
            AtlasRegistry registry = AtlasConsole.Require();
            AtlasViewer viewer = registry.LastViewer;

            Vector3 viewport = AtlasMath.Viewport(viewer, position);
            Vector2 edge = AtlasMath.ClampToEdge(viewport, 0.05f, out float angle);

            context.Table(new List<KeyValuePair<string, string>>
            {
                Row("bearing", $"{AtlasMath.Bearing(viewer, position):0.#}°" +
                    ConsoleMarkup.Dim(AtlasMath.Bearing(viewer, position) < 0f ? "  left" : "  right")),
                Row("distance", $"{Vector3.Distance(viewer.Position, position):0.##} m"),
                Row("viewport", $"{viewport.x:0.###}, {viewport.y:0.###}"),
                Row("depth", viewport.z >= 0f
                    ? $"{viewport.z:0.##} m in front"
                    : ConsoleMarkup.Warn($"{-viewport.z:0.##} m behind")),
                Row("on screen", AtlasMath.IsOnScreen(viewport) ? "yes" : "no"),
                Row("clamped to", $"{edge.x:0.###}, {edge.y:0.###}" +
                    ConsoleMarkup.Dim($"  arrow {angle:0.#}°")),
            }, 14);
        }

        [ConsoleCommand("atlas.selection", "Tracks the console's selected object as a marker.",
            Category = Category,
            RequiresSelection = true,
            Description = "Uses the delegate entry point, so the object needs no component and " +
                          "nothing about it is modified. Run it again to stop.")]
        public static string TrackSelection(ConsoleContext context)
        {
            AtlasRegistry registry = AtlasConsole.Require();
            GameObject selected = context.RequireSelection();

            if (consoleTracked.TryGetValue(selected, out AtlasHandle existing))
            {
                registry.Release(existing);
                consoleTracked.Remove(selected);
                return $"Stopped tracking {selected.name}.";
            }

            Transform transform = selected.transform;
            AtlasHandle handle = registry.Track(
                () => transform != null ? transform.position : Vector3.zero,
                AtlasMarker.Point(selected.name),
                registry.LastViewer.Space);

            consoleTracked[selected] = handle;
            return $"Tracking {ConsoleMarkup.Accent(selected.name)}. Run it again to stop.";
        }

        private static readonly Dictionary<GameObject, AtlasHandle> consoleTracked =
            new Dictionary<GameObject, AtlasHandle>();

        private static KeyValuePair<string, string> Row(string key, string value) =>
            new KeyValuePair<string, string>(key, value);
    }
}
