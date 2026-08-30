# Changelog

## Unreleased

### Fixed

- **Markers drew nothing without an icon set.** Both presenters disabled the `Image`
  when a sprite failed to resolve, so an unconfigured compass bar and indicator layer
  rendered an empty frame while `VisibleCount` cheerfully reported markers visible.
  `IAtlasIconProvider` documents that a missing icon costs a blank marker rather than a
  broken frame; the code did the opposite. Markers now always draw, tinted, with the
  sprite when there is one. Two tests cover it — the previous nine all passed while both
  views rendered nothing, because they asserted on `activeSelf` rather than on what uGUI
  would draw.

### Added

- The M0 sample wires real icons from `com.liminallabs.shareddemoassets` (optional):
  one `AtlasSpriteIcons` asset shared by both views, so an objective cannot be a flag on
  the compass and a star on screen. Off-screen indicators get an arrow sprite, and the
  orbiting marker gets a label, tint and icon instead of defaults.
- Demo input reads through `AtlasM0Input`, which works on either input backend.
- An unassigned icon now draws core's `LiminalPlaceholder` — a red question mark — in the
  editor and development builds, in its own colour rather than the marker's. Release
  builds keep the tinted blank, so nothing red reaches a player.

### Changed

- **`Compass` and `Screen` now require `com.liminallabs.core` (0.4.0).** `Runtime/` still
  references nothing; the line moved to "the half that draws may know about the house's
  shared assets, the half that solves may not."
- The test assembly no longer references `UnityEditor.TestRunner`. It declares no platform
  restriction, so that reference would have broken a player test build — the exact class of
  failure the assembly was written to avoid.

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
