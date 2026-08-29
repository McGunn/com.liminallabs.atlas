# Liminal Labs Atlas

Knowing where things are. Register an object once; every installed view draws it, from one
solve.

**This package is the core and draws nothing.** It owns the registry, the marker
vocabulary, the space identity and the solve — the part every view shares. The views are
separate packages:

| | |
| --- | --- |
| **Liminal Labs Atlas Compass** | a bearing bar |
| **Liminal Labs Atlas On-Screen** | floating indicators, edge clamping |
| **Liminal Labs Atlas Maps** | minimap and world map — M1, not yet built |

Take one, take all of them, or take the core and draw your own. None of the view packages
knows the others exist, which is not a convention but a structural fact: they cannot name
each other, because they do not depend on each other.

**M0.** Registry, markers, spaces, the solve, and the first two views. Not the map — that
is M1 and M2, and building it before the registry is proven is how a map gets rewritten.

## The claim M0 has to survive

> One tracked object, registered **once**, appears simultaneously on the compass bar at
> the correct bearing *and* as an on-screen indicator — and when it moves behind the
> viewer, the bar marker leaves the correct end while the screen indicator clamps to the
> correct screen edge with its arrow pointing back at it.

The behind-the-viewer case is in there deliberately. A projection matrix divides by w, and
behind the viewer w is negative — so the projected point comes back **mirrored through the
centre**. Something behind and to your left projects to the *right* of the screen. Every
ad-hoc indicator ships this bug, it looks almost right, and it survives playtests.

It is only catchable when the solve is shared, which is the argument for one package
rather than four.

## Three ways in, none of which touches your class hierarchy

```csharp
// 1. Drop a component on anything.
[AddComponentMenu("Liminal Labs/Atlas/Marker")] AtlasMarkerBehaviour

// 2. Implement an interface on a type you already own.
public class MyQuest : IAtlasTrackable { … }

// 3. Track something with no GameObject at all.
AtlasHandle handle = registry.Track(() => unit.Position, marker, spaceId);
```

The third matters more than it looks. It is what lets a strategy game track ten thousand
units without ten thousand components, and what will let a content instance be trackable
without ever becoming one.

## Wiring

```csharp
var registry = new AtlasRegistry();
registry.AddProjection(new BearingProjection(), compassBar);
registry.AddProjection(new ScreenProjection(),  screenIcons);

// once a frame, from wherever you own the update order:
registry.Tick(AtlasViewer.FromCamera(camera, AtlasSpaceId.Default));
```

`AtlasRegistryBehaviour` does all of that as a component if you would rather not.

## Two rules the code enforces on itself

**The solve is a pure function.** `AtlasMath` never references `Camera` — a grep in the
verify loop says so. The camera is flattened into `AtlasViewer` once per frame, which is
what lets every bearing and viewport case be tested with no scene, no camera and no
rendered frame. Thirty-four of them are, outside Unity entirely.

**Presenters never query the world.** They are handed a solve list and draw it. No
`Camera`, no `Transform` belonging to anything they are drawing, no bearing arithmetic of
their own. That is why the bar and the icons agree about what is behind you rather than
merely usually agreeing — and why a studio with its own HUD art can throw both away and
keep the registry.

## Layout

| | |
| --- | --- |
| `Runtime/` | `LiminalLabs.Atlas` — registry, markers, spaces, solve, seams. **References nothing.** |
| `Console/` | optional; needs `com.liminallabs.core` |
| `Editor/` | Setup and Validation checks; optional, needs core |
| `Tests/` | the maths, registry and space suites |
| `Samples~/AtlasCore/` | three entry points, no presenter, the solve printed on screen |

A projection lives with the presenter that consumes it, in the view package — they are the
two halves of one output, and nothing but the compass needs world-to-bearing.

## Why the views are separate packages

The original design argued for one package with several assemblies: separate packages
would each need the shared vocabulary as a dependency, and would only ever version
together.

Half of that is right and stays right — **the vocabulary must never fork.** `AtlasMarker`,
`AtlasSolve`, `AtlasSpaceId` and the registry are one thing, in this package, and every
view depends on it.

The other half does not survive contact with shipping them. Each view wants its own demo,
and Package Manager gives a package one sample list. Each wants its own README as its
landing page, its own version history, and its own changelog. A project that wants a
compass should not install a map system. And "an unreferenced assembly costs nothing at
runtime" is true and beside the point when the question is what someone installs.

The property the one-package argument was protecting — that the views cannot reference
each other — comes out **stronger**, not weaker. It used to be a reflection assertion in a
test. Now it is structural: they have no dependency through which to name each other.

## Spaces, in M0

A map is not a texture — it is a plane with a world transform, and modelling it that way
is what separates a map system from a minimap script. Interiors, basements, towers and
regions are then the same type with different numbers.

M0 ships almost none of that, on purpose: a `Default` space that exists without being
registered, markers that carry a space, and a registry that excludes markers the viewer is
not in the same space as. What it *does* ship is the identity — because
`AtlasSpaceId` ends up in save data, and changing how spaces are identified after that is
a migration rather than a refactor.

## From the console

With `com.liminallabs.core` present: `atlas` for the registry, `atlas.markers` for every
marker's bearing, distance and whether it is behind you, `atlas.spaces`, `atlas.probe` for
an arbitrary position, and `atlas.selection` to track the console's selected object
through the delegate entry point without modifying it.

An early slice of the Atlas Board (M5), kept because checking a bearing sign by eye is how
one survives.

## The sample

**Import it first.** Package Manager → Liminal Labs Atlas → Samples → **Atlas Core** →
Import. Unity does not compile `Samples~` until the sample is imported, so the menu item
does not exist before you do — that is UPM behaviour rather than a fault, but it catches
everyone once.

Then **Window → Liminal Labs → Atlas → Build Core Sample Scene**.

This sample has **no presenter**, on purpose. It registers three markers through all three
entry points and prints the raw solve — bearing, distance, behind-or-not — in plain IMGUI.
A package that draws nothing should be demonstrable without installing one that does, and
what it demonstrates is the claim underneath everything: those numbers are computed once,
from plain values, before anything draws.

Install Atlas Compass or Atlas On-Screen and the same registrations draw themselves.
Nothing in this sample changes.

## Not built

The map projection, minimap and world map — **Liminal Labs Atlas Maps**, M1–M2. Pan, zoom, importance LOD, legend and
filters (M2) — `Importance` exists on the marker and is unused. Baking (M3). Discovery and
fog (M4). Save, content and TMP bridges, and the Atlas Board (M5). Direction labels,
distance text and fade curves are M1 polish; `Fade` is computed and applied as alpha.

No Addressables, in any milestone.

## Open questions

`docs/atlas-open-questions.md`. Twelve, none blocking. **Q5 is the one to read** — the
space id representation is the decision that becomes saved data.
