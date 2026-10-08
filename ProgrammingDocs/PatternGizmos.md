# PatternGizmos

## Purpose

An editor helper for designing wave patterns (asteroids and power-ups). When a pattern prefab is open in
the editor, it draws:

- the **player's orbit ring**, at the player's depth
- a copy of the orbit ring at **every depth a child sits at**, labeled in the order ObjectSpawner will
  reveal them, with a count of any power-ups at that depth
- a dotted line through the middle of the rings, to show how deep the pattern is

That way you can see where each asteroid is relative to the circle the player can reach, and in what
order they'll arrive.

It only draws in the editor, and **not in Play mode**: `OnDrawGizmos` returns straight away while
`Application.isPlaying` is true. With object pooling, every pattern copy stays in the scene for the whole
level, so drawing their rings and text labels on every Scene view redraw made the Editor crawl. In a build,
it does nothing at all.

It also passes on the orbit numbers (`OrbitRadius`, `OrbitCenter`) from `OrbitSettings` that the Pattern
Builder uses to place asteroids. The builder itself is PatternGizmosEditor, which replaces this component's
Inspector panel.

## Where it lives

- **Attached to:** the root of each pattern prefab in `Assets/Prefabs/Patterns/`.
- **Requires:** the pattern's asteroids to be **direct children** of the root, the same as ObjectSpawner
  expects.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `ringColor` | light blue | Color of the rings drawn at each asteroid depth |
| `playerRingColor` | yellow-orange | Color of the ring drawn at the player's depth |

Below these, the Inspector also shows the Pattern Builder (see PatternGizmosEditor).

### Values from the game

These aren't Inspector fields. They're read from the same places the game reads them, so the editor
preview always matches what happens in play:

| Property | Value (defaults) | Comes from |
| --- | --- | --- |
| `OrbitRadius` | 3 | `OrbitSettings.OrbitRadius`, the same value the player orbits at |
| `OrbitCenter` | (0, 1) | `OrbitSettings.OrbitCenterFromSpawner`: the orbit center, measured from the spawner |
| `PlayerZ` | -19 | `OrbitSettings.PlayerZFromSpawner`: the player's z (-4) minus the spawner's z (15) |
| `ObjectSpawner.SameZTolerance` | 0.01 | Read straight from ObjectSpawner |

`OrbitRadius` and `OrbitCenter` are `public static` so the Pattern Builder can use them too.

## How it works

### Gizmos and Handles

**Gizmos** are the lines and icons Unity draws in the Scene view to help you edit, like a collider's green
outline or a camera's view shape. They never show up in the game. A script can draw its own by adding an
`OnDrawGizmos` method, which Unity calls whenever it redraws the Scene view.

This script draws with **Handles** instead of the `Gizmos` class, because Handles can draw flat circles
(`DrawWireDisc`), text labels, and dotted lines.

### Why it's wrapped in `#if UNITY_EDITOR`

`Handles` lives in the `UnityEditor` namespace, which only exists inside the editor. A game build doesn't
include it, so any code that uses it would stop the build from compiling. `#if UNITY_EDITOR` ... `#endif`
tells the compiler to skip everything between them when building the game. In a build, the component is
left with just its two color fields and does nothing.

### Spawner space

In the game, ObjectSpawner spawns each pattern as a child of the Asteroid Spawner object (at z = 15). So
the positions in a pattern are relative to the spawner, not the world. The values above are in that
same space. That's why `PlayerZ` is -19: the player is at z = -4, which is 19 units in front of the
spawner. `OrbitSettings` works out that subtraction, so it stays right if either position changes.

`OnDrawGizmos` draws in the space of the pattern's **parent**:

- **In the game**, the parent is the spawner, so it draws in spawner space.
- **In Prefab Mode**, the pattern has no parent, so it draws in world space, which then stands in for
  spawner space.

`Handles.DrawingScope` sets this up. Everything drawn inside it uses the parent's space, and when the
scope ends (the `using` statement), Handles' settings go back to what they were, so other gizmos aren't
affected.

It also adds the pattern root's own position (`rootOffset`), so if you move the whole pattern, the rings
move with it.

### `OnDrawGizmos`, step by step

1. **Collect the children.** For every child, store a **tuple** `(z, powerUp)`: its z in spawner space
   (the root's z plus the child's z) and whether it has a `PowerUp` component. A tuple bundles two values
   without writing a class.
2. **Sort them, lowest z first**, with `items.Sort((a, b) => a.z.CompareTo(b.z))`. Tuples don't know how
   to compare themselves, so `Sort` is given a small function that compares by z (a higher-order
   function). The camera is at a lower z than the spawner, so lowest z means closest to the camera. That's
   the same order ObjectSpawner reveals them in.
3. **Group them into steps.** Walk through the sorted list. If a depth is within `SameZTolerance` of the one
   before, it's the same step, so add 1 to that step's count (and 1 to its power-up count if it's a
   power-up). Otherwise, start a new step. Children in the same step spawn together.
4. **Draw the player ring** at `PlayerZ`, labeled "Player".
5. **Draw a ring for each step**, labeled with its step number and how many children are in it, e.g.
   "Step 2 (6)". If the step has power-ups, they're added: "Step 3 (4, 1 power-up)" means 4 children in
   total, one of them a power-up.
6. **Draw the dotted spine** from the first step's center to the last, if there's more than one step.

Each ring is a flat circle (`DrawWireDisc`) facing along z, centered on `OrbitCenter` at that depth, with a
radius of `OrbitRadius`. The label sits at the top of the ring.

## Talks to

- **OrbitSettings**: reads the orbit radius and center, and the player's depth, all measured from the
  spawner.
- **ObjectSpawner**: reads `SameZTolerance`. The step order copies the spawner's logic, so the labels
  match the order it reveals asteroids in.
- **PatternGizmosEditor**: reads `OrbitRadius` and `OrbitCenter` to place asteroids and power-ups on the
  ring, and draws this component's Inspector.
- **PowerUp**: checks each child for the component, to count power-ups in the labels.

## Gotchas

- **No rings in Play mode.** To see a pattern's layout, open the prefab (or a scene with it) outside Play.

- **The step order must match ObjectSpawner's.** The spawner sorts by distance from the camera. Sorting by
  lowest z only gives the same order because the camera is at a lower z than every asteroid. If the camera
  or spawner moved so that wasn't true, the labels would be wrong.
- **Only direct children are counted.** A sub-group counts as one step at the group's own depth, whatever
  is inside it. That matches how ObjectSpawner treats it (see its gotchas).
