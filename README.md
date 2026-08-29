# Liminal Atlas

Knowing where things are. Register an object once; it appears on the compass **and** as
an on-screen indicator, from one solve.

**M0.** The registry, the marker vocabulary, the space identity, two projections and two
presenters. Not the map — that is M1 and M2, and building it before the registry is
proven is how a map gets rewritten.

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
| `Runtime/` | `LiminalLabs.Atlas` — registry, markers, spaces, solve, projections. **References nothing.** |
| `Compass/` | `LiminalLabs.Atlas.Compass` — the bar |
| `Screen/` | `LiminalLabs.Atlas.Screen` — floating icons, edge clamping |
| `Console/` | `LiminalLabs.Atlas.Console` — optional; needs `com.liminallabs.core` |
| `Tests/` | §7 acceptance suite |
| `Samples~/AtlasM0/` | three markers, three entry points, two views |

`Compass` and `Screen` each reference core and **not each other**. The moment one
references the other, take-only-what-you-use is gone; test 20 asserts it, by reflecting
over the assemblies rather than trusting the asmdef.

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

**Import it first.** Package Manager → Liminal Atlas → Samples → **Atlas M0** → Import.

That step is not optional and it is not obvious: the scene builder lives in `Samples~`,
which Unity does not compile until the sample is imported — so **the menu item does not
exist until you import**, and looking for it beforehand finds nothing. That is UPM
behaviour rather than a fault in the package, but it catches everyone once.

Then: **Window → Liminal Labs → Atlas → Build M0 Sample Scene.**

Hold right mouse and turn on the spot. Three markers, one per entry point. Watch the
orbiting one pass behind you — which end of the bar it leaves, and which screen edge its
icon pins to.

## Not built

The map projection, minimap and world map (M1–M2). Pan, zoom, importance LOD, legend and
filters (M2) — `Importance` exists on the marker and is unused. Baking (M3). Discovery and
fog (M4). Save, content and TMP bridges, and the Atlas Board (M5). Direction labels,
distance text and fade curves are M1 polish; `Fade` is computed and applied as alpha.

No Addressables, in any milestone.

## Open questions

`docs/atlas-open-questions.md`. Eleven, none blocking. **Q5 is the one to read** — the
space id representation is the decision that becomes saved data.
