# Liminal Labs Atlas

Knowing where things are. Register an object **once**; it appears on the compass bar at
the correct bearing *and* as an on-screen indicator, from one solve.

One package, several assemblies — a compass, on-screen indicators, and from M1 a minimap
and a world map. Reference the assemblies you want; nothing from the others runs.

---

## The claim this has to survive

> One tracked object, registered once, appears simultaneously on the compass bar at the
> correct bearing **and** as an on-screen indicator — and when it moves behind the viewer,
> the bar marker leaves the correct end while the screen indicator clamps to the correct
> screen edge with its arrow pointing back at it.

The behind-the-viewer case is in there on purpose. A projection matrix divides by w, and
behind the viewer **w is negative** — so the projected point comes back mirrored through
the centre. Something behind and to your left projects to the *right* of the screen.

Every ad-hoc indicator ships that bug. It looks almost right. It survives playtests. And
it is only catchable when both views read the same answer, which is the argument for one
system rather than four.

---

## Getting started

Drop an **Atlas Registry** in the scene, a **Bar Presenter** on a UI strip, a **Screen
Presenter** on a full-screen rect, and an **Atlas Marker** on anything worth pointing at.
That is the whole setup — presenters register themselves when they are enabled, the same
way markers do.

In code, if you would rather own it:

```csharp
var registry = new AtlasRegistry();
registry.AddProjection(new BearingProjection(), compassBar);
registry.AddProjection(new ScreenProjection(),  screenIcons);

// once a frame, from wherever you own the update order:
registry.Tick(AtlasViewer.FromCamera(camera, AtlasSpaceId.Default));
```

## Three ways in, none of which touches your class hierarchy

```csharp
// 1. Drop a component on anything. Zero code.
AtlasMarkerBehaviour

// 2. Implement an interface on a type you already own.
public class MyQuest : IAtlasTrackable { … }

// 3. Track something with no GameObject at all.
AtlasHandle handle = registry.Track(() => unit.Position, marker, spaceId);
```

The third matters more than it looks. It is what lets a strategy game track ten thousand
units without ten thousand components, and what will let a content instance be trackable
without ever becoming one.

---

## Two rules the code enforces on itself

**The solve is a pure function.** `AtlasMath` never references `Camera` — a grep in the
verify loop says so, and `AtlasViewer.FromCamera` lives outside `Solve/` precisely so that
grep means something. The camera is flattened into a plain struct once per frame, which is
what lets every bearing and viewport case be tested with no scene, no camera and no
rendered frame. **Thirty-four of them are, outside Unity entirely.**

**Presenters never query the world.** They are handed a solve list and draw it. No
`Camera`, no `Transform` belonging to anything they draw, no bearing arithmetic of their
own. That is why the bar and the icons agree about what is behind you rather than merely
usually agreeing — and why a studio with its own HUD art can throw both away and keep the
registry.

---

## Layout

| | |
| --- | --- |
| `Runtime/` | `LiminalLabs.Atlas` — registry, markers, spaces, solve, seams. **References nothing.** |
| `Compass/` | `LiminalLabs.Atlas.Compass` — `BearingProjection`, `BarPresenter`, cardinal letters. Needs core. |
| `Screen/` | `LiminalLabs.Atlas.Screen` — `ScreenProjection` + `ScreenPresenter`. Needs core. |
| `Console/` | optional — needs `com.liminallabs.core` |
| `Editor/` | Setup and Validation checks; optional, needs core |
| `Tests/` | the §7 acceptance suite |
| `Samples~/Atlas M0/` | the milestone, in one scene |

A projection lives with the presenter that consumes it — they are the two halves of one
output, and nothing but the compass needs world-to-bearing.

**`Compass` and `Screen` reference `LiminalLabs.Atlas` and not each other.** A test
asserts it by reflecting over the built assemblies rather than by reading the asmdefs,
because a reference added in a hurry is invisible in review and would break nothing else.
The moment one view can name the other, take-only-what-you-use is gone and the two can
quietly diverge on what "behind" means.

Both views also reference `com.liminallabs.core`, for the shared missing-sprite
placeholder and nothing else. **`Runtime/` does not**, and that is the line worth
holding: the solve stays a pure function over structs that anything can call, while the
half that draws is allowed to know about the house's shared assets.

### Why one package rather than four

Because a compass, an indicator, a minimap and a world map are **four outputs of one
system**, not four systems. Split them and you register the same objective three times, in
three formats, with three sets of icons that drift apart.

The house splits packages by *domain* — doink is not "audio's surface view", it is a
different system that uses audio. Four views of one answer are not a domain boundary.

Practically: UPM does not resolve git-URL dependencies transitively, so four packages
would mean four URLs installed in the right order with versions matched by hand; every
milestone from here to M5 changes the core, so they would version together regardless; and
`samples` is an array, so one package ships as many demos as it likes.

What separate packages would have bought — that the views cannot reference each other — is
delivered by separate *assemblies*, which is what these are.

---

## Spaces

A map is not a texture. It is a **plane with a world transform**, and modelling it that
way is what separates a map system from a minimap script: interiors, basements, towers and
regions become the same type with different numbers.

M0 ships almost none of that on purpose — a `Default` space that exists without being
registered, markers that carry a space, and a registry that excludes markers the viewer is
not in the same space as. What it *does* ship is the **identity**, because `AtlasSpaceId`
ends up in save data, and changing how spaces are identified afterwards is a migration
rather than a refactor.

---

## Setup and Validation

Atlas has an unusual number of ways to be wired almost correctly and draw nothing, and
every one of them looks identical from the outside. The checks name them: no registry;
more than one (right for split-screen, a mistake otherwise); a registry with no camera; a
presenter with self-registration off and nothing wiring it; a pool smaller than the
registry's marker limit, which silently drops the lowest-priority markers; a presenter
with no icon provider; a bar so wide its two ends are indistinguishable.

Needs `com.liminallabs.core` — the checks simply do not compile without it.

## From the console

With core present: `atlas` for the registry, `atlas.markers` for every marker's bearing,
distance and whether it is behind you, `atlas.spaces`, `atlas.probe` for an arbitrary
world position, and `atlas.selection` to track the console's selected object through the
delegate entry point without modifying it.

An early slice of the Atlas Board (M5), kept because checking a bearing sign by eye is how
one survives.

---

## The sample

**Import it first** — Package Manager → Liminal Labs Atlas → Samples → **Atlas M0** →
Import. Unity does not compile `Samples~` until you do, so the menu item does not exist
before then. UPM behaviour rather than a fault, but it catches everyone once.

Then **Window → Liminal Labs → Atlas → Build M0 Sample Scene**.

Hold right mouse and turn. Three markers, one per entry point, on the bar and on screen.
Press **Tab** for the raw solve beside the views drawing it — a bearing of −173° next to a
marker at the left end of the bar and an icon pinned to the left edge is what makes it
obvious those are one answer rendered twice, not two implementations that agree today.

For a single-view scene, delete `Compass Bar` or `Screen Indicators`. The presenters
register themselves, so removing one changes nothing else.

Icons come from `com.liminallabs.shareddemoassets`, which is optional. Without it every
marker draws core's red question mark, which is the point — a missing icon announces
itself rather than looking like minimal styling. Release builds fall back to a tinted
blank instead, so nothing red ever reaches a player. A missing icon costs a marker you
can see, never a blank frame.

---

## Not built

The map projection, minimap and world map (M1–M2). Pan, zoom, importance LOD, legend and
filters (M2) — `Importance` exists on the marker and is unused. Baking (M3). Discovery and
fog (M4). Save, content and TMP bridges, and the Atlas Board (M5). Direction labels,
distance text and fade curves are M1 polish; `Fade` is computed and applied as alpha.

No Addressables, in any milestone.

## Open questions

`docs/atlas-open-questions.md` — thirteen, none blocking. **Q5 is the one to read**: the
space id representation is the decision that becomes saved data.
