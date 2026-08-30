# Changelog

## [0.4.1] — the fog you can see

### Added

- **`MinimapPresenter` draws the reveal mask.** M4 shipped exact discovery data that
  nothing rendered, which is half a feature: the mask was correct and invisible.

  A texture built from the bits rather than a shader, so it works on every render pipeline
  with no material to ship, no keyword to enable and nothing to break when a project
  upgrades URP. Bilinear filtering softens the cell edges for free. A game that wants a
  painterly fog samples `AtlasReveal` itself — the data stays exact, which was always the
  point of storing bits.

  Rebuilt only when `AtlasReveal.Version` changes, and at most on an interval. The mask is
  filled in on a timer and not at all while the viewer stands still, so in practice this
  uploads a texture a few times a second while walking and never while stopped.

- The M1 sample wires discovery and fog into both maps. Walk and watch the world clear.
- Console: `atlas.reveal`, `atlas.fog`, `atlas.maps`, `atlas.zoom`. `atlas.maps` answers
  the question that cost the most time on this package — a map showing two markers is
  either zoomed wrong, centred wrong, or framing a space with no bounds, and all three
  look identical from the outside.

### Changed

- The background and the fog share one `BoundsWindow` function. Two copies of that
  arithmetic is two chances for them to disagree by a fraction, which reads as the fog
  lagging the terrain it is meant to hide.
- `AtlasReveal` carries a `Version` counter, bumped on every change and deliberately not
  serialised — it is a change signal, not state.


## [0.4.0] — M2, M3, M4 and M5

Baking first, because pan and zoom over an empty rectangle is less of a map than a
picture of the world with nothing moveable on it.

### Added — M3, baking

- **`AtlasBaker`** renders a space top-down into the image the maps draw under their
  markers. Baking is an *editor render of a space's bounds*, which is why the design put
  it behind the space model: a map image is a picture of a volume the system already
  knows, not a new thing to author and keep aligned.

  The property that has to hold exactly is that the image covers the bounds corner to
  corner — the presenter turns a frame into a uv window by dividing by them, and the reveal
  mask is indexed the same way. So the texture takes the bounds aspect rather than being
  squared off, and the camera is orthographic: a perspective one would lean tall things
  outward and a marker on a roof would not sit over its building.

- A space inspector with a bake button and the number that decides whether a bake is worth
  waiting for — **units per pixel**. At 4 units a pixel a doorway is invisible, which is
  much better learned before the render than after.

### Added — M2, crowding

- **`AtlasFilter`**: kinds as a bitmask plus an importance floor, applied *in the
  projection*. Filtering after the registry has truncated to `MaxMarkers` would show
  whatever survived a priority cut against every other kind, rather than the nearest
  markers of the kind asked for.
- **`MinimapPresenter.AutoImportanceLod`** — the importance floor scales with the frame
  span, so a continent shows its cities and a street shows its shops. Off by default,
  because a project that authored no importance would watch its markers vanish and have no
  idea why.
- Unticking a legend last checkbox shows everything again rather than nothing. An empty
  selection meaning nothing leaves the user staring at a blank map wondering what broke.

### Added — M4, discovery

- **`AtlasReveal`**: a bit per cell over the space bounds. 256x256 is 8 KB, a size a save
  can carry per space without anyone negotiating. Pure — the whole of discovery is tested
  with no scene.
- **`AtlasDiscovery`** fills it in behind the viewer, on a timer *and* on distance moved,
  doing nothing at all while the viewer stands still. A fog system that costs measurable
  frame time is one a studio turns off.
- `MapProjection.HideUndiscovered`, off by default — a compass should keep pointing at a
  quest marker it was told about even where the player has not walked.

### Added — M5, bridges and the board

- **`LiminalLabs.Atlas.Save`**, gated on `LIMINAL_SAVE` so Atlas never depends on the save
  package. It persists **the reveal mask and nothing else**: markers are registered by
  whatever owns them, and a marker restored by the atlas would be a second copy of state
  fighting the first. A saved mask whose dimensions do not match is *refused*, because
  stretching one from an older map reveals the wrong places.
- **The Atlas Board** (`Window > Liminal Labs > Atlas > Atlas Board`) — the registry live
  while the game runs: spaces, bounds, images, discovery percentage with reveal buttons,
  view counts, and every marker bearing and distance solved against the live viewer.

  Solved in the window rather than read out of a presenter pool, deliberately: the pool is
  the thing under suspicion whenever anyone opens this. Running the same pure functions on
  the same viewer is what separates a maths bug from a drawing bug, which is the question
  every bug in this package has turned on.


## [0.3.1] — spaces you can actually author

### Fixed

- **A world map framed to a space's bounds framed nothing.** `AtlasRegistry` is a plain
  object built when its component wakes, so anything an editor script writes into its
  spaces is thrown away before play. The bounds were therefore always zero, "frame the
  whole space" silently fell back to `Radius` — a minimap's number — and the world map
  came out *smaller* than the minimap beside it.

  The real gap was that **M1 shipped a space model with no authoring path at all.**

### Added

- **`AtlasSpaceBehaviour`** — a space, authored in a scene: bounds, image, floor height,
  and a gizmo, applied to the registry when the scene wakes. The bounds are in world
  units and are what the projection frames, what a baked image will cover at M3, and what
  a reveal mask will be indexed against at M4 — so the gizmo is there to make getting them
  right a one-minute job.
- **Zoom and pan** on `MapProjection` and `MinimapPresenter`: `Zoom`, `ZoomBy`, `PanBy`,
  `ResetFraming`, `MapUnitsPerPixel`. Zoom is a *multiplier* on the framed radius, which
  is the only way "frame the whole space" and "zoomed in two steps" can both be true —
  an absolute second radius would overwrite whatever the bounds computed, and the map
  would forget how big the world is the moment anyone touched the wheel.
- The M1 sample wires scroll to zoom, drag to pan and R to reset. The pan is scaled by
  the map's own units-per-pixel, so it tracks the cursor exactly at any zoom.
- A Setup and Validation failure for the exact defect above: Space Bounds centring with
  no `AtlasSpaceBehaviour` in the scene, and a runtime warning if it happens anyway.

## [0.3.0] — M1, the map

A map is not a texture. It is a plane with a world transform, which is what the space
model has carried since M0 and what this milestone finally spends.

### Added

- **`MapProjection`** — the third projection. World positions onto a space's plane,
  framed for one view.

  **A minimap and a world map are two instances of it**, differing only in `Centre`,
  `Radius` and `Rotation`. That is the design's central claim about maps, and it is what
  stops one objective being registered twice, drawn by two code paths, and drifting apart
  on which one knows it was completed. `AtlasMapCentre.SpaceBounds` frames a whole space
  with no authored numbers, because the space already knows how big it is.

- **`MinimapPresenter`** — one component for both. Round or square, pinning outside
  markers to a circle or a rect, panning and rotating the space's image beneath them, and
  a viewer arrow that turns on a north-up map and stays put on a viewer-up one — all of it
  falling out of the frame's own rotation rather than a second look at the camera.

- **`AtlasMapFrame`** and the maths under it: `MapPoint`, `MapRadiusFraction`,
  `RotateMap`, `ClampToCircle`. Pure, so the framing, the rotation sign and the circular
  pinning are all tested with no scene — **ten new assertions, run outside the engine**.

- **The Atlas M1 sample**: compass, indicators, minimap and world map on one screen, from
  one set of registrations. Hold M and watch the same markers reframe.

- Map checks in Setup and Validation, including the two that cost real time: a round map
  on a non-square rect (a circle in fractions is only a circle when the rect is square),
  and a map with nothing clipping it.

### Changed

- **`IAtlasProjection.Solve` now receives the space registry.** A bearing and a screen
  point need none of it; a map point is a position *on a plane*, and the plane is the
  space's. Passed rather than reached for, so a projection stays a function of what it is
  handed.


## [0.2.0] — the compass grows up

### Added — the compass, from a year of shipped iteration

Harvested from a working compass and indicator pair, reimplemented against this
package's architecture rather than ported. The features are the author's; the plumbing
is not, because the original had the presenter computing its own bearings, which is the
thing that lets two views quietly disagree about what is behind you.

- **Markers slide off the ends and are clipped, instead of vanishing.** They kept their
  slot until fully past the edge. The old behaviour hid a marker the instant it crossed
  half the bar's field of view — while it was still entirely on screen — which pops, and
  a marker that disappears a pixel before the edge reads as a bug. The README argued
  hiding was right because a *clamped* marker lies about where its target is; that was
  right about clamping and wrong about the remedy.
- **Cardinal letters** — N, E, S, W, optionally with the diagonals — sliding through the
  markers on the same mapping, asserted by a test rather than by eye. They need
  `AtlasMath.BearingOfDirection`, because a direction has no position and faking one by
  picking a point far to the north is wrong near the world origin.
- **Distance labels** on both views, and a designer-editable `fadeCurve` and a near/far
  `scale` range, so depth reads without anything becoming illegible.
- **Idle fade**: the bar dims while the viewer is still and returns when it moves.
  Driven by `AtlasMath.Activity`, which is arithmetic over two frozen viewers — so a
  compass on a cutscene camera, a drone or a replay fades on the same rule as one on a
  player, and it is testable with no scene.
- **`arrowRotationOffset`** on the screen presenter, so any arrow art works. Without it
  the component silently requires art that points right, and art that points up is wrong
  by ninety degrees in a way that looks like a maths bug.
- **`AtlasMarker.IconOverride`** — a sprite for one marker, bypassing the id and its
  provider. The array's order is a contract with save data, and a one-off icon should not
  need a permanent slot in it.
- **`Discovery`, `FastTravel` and `Event` marker kinds.**
- `hideWhenBehind` and `hideWhenOffScreen` on the screen presenter, both off.

### Fixed

- **Every on-screen indicator was half a screen down and to the left.** The position was
  built from `area.rect.xMin`/`yMin`, which are measured from the pivot, while the pooled
  indicators anchor to the area's bottom-left corner. On a full-screen layer with the
  default centre pivot those are `-width/2` and `-height/2`, so a target dead ahead drew
  in the corner. Wrong for every marker in every frame since the component was written.

  Two tests covered this position and **both encoded the error** — one asserted that a
  target dead ahead sits at `x == 0`, which is the corner, and called it "centre of the
  screen". They were written from the arithmetic rather than from where the icon has to
  appear, so they locked the defect in instead of catching it. Both are corrected, and
  two new tests assert bounds and direction rather than an exact expected value.


- **A missing TMP Essential Resources import crashed the presenters.**
  `TMP_Settings.defaultFontAsset` dereferences a null instance rather than returning
  null, so it threw from `Awake` — taking the whole presenter with it, and every marker
  with that. A fresh project has not run that import, so this was the default experience.
  Labels now switch themselves off and say which menu item to click; a Setup and
  Validation row says the same before anything is played.

### Changed

- **`IAtlasPresenter.Present` now takes the frame's viewer.** Cardinal letters are
  directions with no position, so no solve can carry them. The viewer is the same frozen
  struct the solve used; presenters may run the pure functions in `AtlasMath` over it and
  may still do nothing else.
- Labels fall back to a TMP font asset built from core's vendored Inter when the project
  has no default. TMP draws nothing at all without one, which reads as broken labels
  rather than as a setting nobody filled in.


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
