# Changelog

## [0.1.0] - M0, the falsifiable core

One tracked object, registered once, on the compass bar and as an on-screen indicator
from the same solve - including the case where it is behind you.

### Added

- `AtlasRegistry` - an instance the game constructs and ticks. No singleton, so
  split-screen can have two and a test can have one with no scene.
- `AtlasMath`, `AtlasSolve`, `AtlasViewer` - the solve as a pure function. `AtlasMath`
  references no `Camera`; the camera is captured into a struct once per frame.
- `AtlasSpaceId`, `AtlasSpace`, `AtlasSpaceRegistry` - a `Default` space that exists
  without registration, and markers that carry a space. Multi-space behaviour is M1; the
  identity ships now because it becomes save data.
- `IAtlasTrackable`, `AtlasMarker`, `AtlasMarkerKind`, `AtlasHandle` and all three entry
  points: a component, an interface, and a position delegate for things with no
  GameObject.
- The presenter and projection seams. The views themselves ship as separate packages:
  `com.liminallabs.atlas.compass` and `com.liminallabs.atlas.onscreen`, with Maps at M1.
  Neither can reference the other, structurally rather than by convention.
- `IAtlasIconProvider` and a sprite-array implementation, so icon ids never become asset
  references.
- Optional `LiminalLabs.Atlas.Console` addon, gated on com.liminallabs.core.
- `Samples~/AtlasCore` - three entry points, no presenter, the solve printed on screen,
  so a package that draws nothing is still demonstrable on its own. Import the sample
  before looking for its menu item; Unity does not compile `Samples~` until then.
- Setup checks in the Setup and Validation window, per the house rule that anything
  failing silently at runtime must surface there. Atlas has several ways to be wired
  almost correctly and draw nothing.

### Verified

- 34 assertions covering the maths, registry and space suites executed against the
  compiled code outside Unity - possible only because the solve is pure.
- Every assembly compiles clean, zero warnings, in both player and editor configurations.
- The presenter suite (§7.4) is written but needs Unity's Test Runner.

### Not in this milestone

Map projection, minimap and world map; pan, zoom and importance LOD; baking; discovery;
save, content and TMP bridges; the Atlas Board. No Addressables, in any milestone.
