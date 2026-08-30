# Changelog

## [0.1.0] — M0, the falsifiable core

One tracked object, registered once, on the compass bar and as an on-screen indicator
from the same solve — including the case where it is behind you.

### The system

- `AtlasRegistry` — an instance the game constructs and ticks. No singleton, so
  split-screen can have two and a test can have one with no scene at all.
- `AtlasMath`, `AtlasSolve`, `AtlasViewer` — the solve as a pure function. `AtlasMath`
  references no `Camera`; the camera is flattened into a struct once per frame, and
  `FromCamera` lives outside `Solve/` so a grep can prove it.
- `AtlasSpaceId`, `AtlasSpace`, `AtlasSpaceRegistry` — a `Default` space that exists
  without registration, and markers that carry a space. Multi-space behaviour is M1; the
  identity ships now because it becomes save data.
- `IAtlasTrackable`, `AtlasMarker`, `AtlasMarkerKind`, `AtlasHandle`, and all three entry
  points: a component, an interface, and a position delegate for things with no
  GameObject.

### The views

- `LiminalLabs.Atlas.Compass` — `BearingProjection` and `BarPresenter`.
- `LiminalLabs.Atlas.Screen` — `ScreenProjection` and `ScreenPresenter`.
- Separate assemblies, each referencing the core and not the other, both drawing from
  fixed pools built at `Awake`. A test asserts the no-cross-reference rule by reflecting
  over the built assemblies rather than by reading the asmdefs.
- **Presenters register themselves when enabled**, and unregister when disabled — so a
  working scene is a registry, a presenter and some markers, with no glue script. The
  alternative was one line per presenter per scene, and a presenter that looked correctly
  configured, drew nothing, and reported nothing when that line was missing.

### Around it

- `IAtlasIconProvider` and a sprite-array implementation, so icon ids never become asset
  references and the package never learns what Addressables is.
- Optional console addon and Setup and Validation checks, both gated on
  `com.liminallabs.core` and neither reachable from the runtime assembly.
- Marker gizmos: position, anchor line and cull radius, so a marker on an object with no
  renderer is findable without pressing play.
- `Samples~/Atlas M0` — the milestone in one scene, with Tab showing the raw solve beside
  the views drawing it. Import the sample before looking for its menu item; Unity does not
  compile `Samples~` until then.

### Verified

- **34 assertions** covering the maths, registry and space suites, executed against the
  compiled code **outside Unity entirely** — possible only because the solve is pure.
- Every assembly compiles clean, zero warnings, in player and editor configurations. The
  test assembly is compiled without `UnityEditor` on purpose, so a stray reference to it
  fails here rather than in someone's player test build.
- The presenter suite (§7.4, including test 20) is written and compiles; running it needs
  Unity's Test Runner.

### Not in this milestone

Map projection, minimap and world map; pan, zoom and importance LOD; baking; discovery;
save, content and TMP bridges; the Atlas Board. No Addressables, in any milestone.

### A structural decision, made and then reversed

The views were briefly split into `com.liminallabs.atlas.compass` and
`.atlas.onscreen`. That was wrong and is undone: a compass, an indicator, a minimap and a
world map are four outputs of one system rather than four systems, UPM does not resolve
git-URL dependencies transitively, and every milestone through M5 changes the core anyway.
Recorded in full as Q12 in `docs/atlas-open-questions.md`, including the argument for the
split that turned out to be factually wrong.
