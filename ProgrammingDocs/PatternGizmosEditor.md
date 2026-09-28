# PatternGizmosEditor

## Purpose

The **Pattern Builder**: a panel in the Inspector for laying out asteroid patterns quickly. Pick a shape
(Ring, Arc, Row, or Spiral), set how many asteroids and how deep, see a green preview in the Scene view,
and click **Add** to place real asteroids exactly on the player's orbit. It also has buttons to snap
existing asteroids onto the orbit and to clear a pattern.

It's an editor tool only. It isn't part of the game and isn't included in builds.

## Where it lives

- **File:** `Assets/Scripts/Editor/PatternGizmosEditor.cs`.
- **Shows up:** in the Inspector, under the PatternGizmos fields, whenever a pattern prefab's root is
  selected (usually in Prefab Mode).
- **Menu item:** Tools → Pattern Builder → Snap Selected to Circle.
- **Requires:**
  - A pattern root with PatternGizmos on it.
  - An asteroid prefab to place (defaults to `Assets/Prefabs/Asteroid Types/Asteroid.prefab`).
  - The `OrbitSettings` asset (through PatternGizmos) for the orbit's size and center.

### Custom inspectors and the `Editor` folder

Unity normally draws a component's Inspector panel by listing its fields. A **custom inspector** replaces
that panel with your own. `[CustomEditor(typeof(PatternGizmos))]` tells Unity to use this class whenever
a PatternGizmos component is shown.

Any folder named **`Editor`** is special: scripts inside it are only compiled for the Unity editor and are
left out of game builds. That's why this file can use `UnityEditor` freely, without the
`#if UNITY_EDITOR` wrapper PatternGizmos needs.

## Settings

These are in the builder panel, not saved on the component.

### Shared by every shape

| Setting | Default | What it changes |
| --- | --- | --- |
| Asteroid Prefab | `Asteroid.prefab` | Which asteroid gets placed |
| Shape tabs | Ring | Which shape to build |
| Z | 0 | Depth, in spawner space. Lower Z spawns first |
| Count | 8 | How many asteroids to place (at least 1) |
| Preview Size | 1 | Size of the green preview spheres. Only affects the preview |

There's no radius setting. Every shape that sits on a circle uses the orbit radius from `OrbitSettings`,
so asteroids always land exactly on the player's orbit.

### Per shape

| Shape | Settings | What it builds |
| --- | --- | --- |
| **Ring** | Start Angle (-90) | `Count` asteroids evenly spaced all the way around the orbit |
| **Arc** | From Angle (-135), To Angle (-45) | `Count` asteroids spread evenly along part of the orbit, including both ends |
| **Row** | Height Above Bottom (0), Spacing (1) | A straight horizontal line, centered left-to-right on the orbit. 0 is level with the bottom of the orbit, where the player starts |
| **Spiral** | Start Angle (-90), Angle Step (30), Z Step (2) | Each asteroid steps around the orbit by `Angle Step` degrees *and* back by `Z Step`, so each one is its own spawn step and they arrive one after another |

**Angles** are in degrees and work the same as PlayerMovement's `startAngle`: 0 is right (3 o'clock), 90 is
the top, -90 is the bottom (where the player starts), and angles increase counter-clockwise.

## How it works

### Remembering settings (static fields and EditorPrefs)

Unity creates a new Editor object every time you select something. Normal fields would reset every time
you clicked away. So all the builder settings are **static**, which means they belong to the class, not to
one Editor object. They last for the whole editor session and carry over when you switch between pattern
prefabs. They reset when Unity restarts or recompiles scripts.

The chosen asteroid prefab is remembered longer, with **EditorPrefs**, a small key/value store saved on
your computer (not in the project). In `OnEnable`, if no prefab is picked yet, it loads the saved path, or
the default one. Because it's saved on your computer, each person on the team has their own choice.

### The Inspector panel (`OnInspectorGUI`)

Unity calls `OnInspectorGUI` every time the Inspector redraws. Unity's editor UI is **immediate mode**:
each `EditorGUILayout` call both draws a field and returns its current value, so every setting follows the
pattern:

```csharp
value = EditorGUILayout.SomeField("Label", value);
```

The panel, top to bottom:

1. **`DrawDefaultInspector()`** draws PatternGizmos' own fields (the ring colors) as normal.
2. A short help box explaining angles and Z.
3. **Asteroid Prefab.** When it changes, the new path is saved to EditorPrefs.
4. **The shape tabs and settings.** Only the selected shape's settings are shown. If anything changes,
   `SceneView.RepaintAll()` redraws the preview straight away, instead of waiting for the mouse to move.
5. **Add [Shape].** Greyed out until a prefab is picked (`DisabledScope`).
6. **Snap All to Circle** and **Delete All Asteroids.**

`BeginChangeCheck` / `EndChangeCheck` wrap a group of fields and report whether any of them changed.
That's how it knows when to save the prefab or redraw the preview.

### The preview (`OnSceneGUI`)

Unity calls `OnSceneGUI` whenever the Scene view redraws while the pattern is selected. It only draws on
the **Repaint** event (Scene GUI also runs for mouse moves and layout, which don't need drawing).

For each asteroid about to be added, it draws a translucent green sphere and a number showing the order
they're generated in. It draws in spawner space (see PatternGizmos), the same way the rings are drawn.

### Where each asteroid goes (`ShapePosition` and `OnCircle`)

`ShapePosition(i)` gives the position of asteroid number `i` (counting from 0) for the selected shape.
**Both the preview and Add use it**, so what you see is exactly what gets placed.

`OnCircle(angle, z)` gives a point on the orbit, using the same cos/sin placement as PlayerMovement:
`center + (cos(angle), sin(angle)) × orbit radius`, after converting the angle from degrees to radians.

| Shape | Asteroid `i` goes at |
| --- | --- |
| Ring | Angle `startAngle + 360 × i / count`. Dividing by `count` (not `count − 1`) leaves a gap after the last one, so it doesn't land on top of the first |
| Arc | Angle `Lerp(from, to, i / (count − 1))`, which puts the first exactly at `from` and the last exactly at `to`. A single asteroid goes at `from` |
| Row | x = center + `(i − (count − 1) / 2) × spacing`. That runs from `-half` to `+half`, so the row is centered. y = bottom of the orbit + height |
| Spiral | Angle `startAngle + angleStep × i`, at depth `z + zStep × i` |

### Adding asteroids (`Build`)

For each position:

1. **`PrefabUtility.InstantiatePrefab`** creates the asteroid as a child of the pattern. Unlike plain
   `Instantiate`, this keeps the link to the asteroid prefab, so later changes to the prefab still reach
   these copies.
2. **Set its position.** Shape positions are in spawner space, but a child's position is relative to the
   pattern root, so the root's own offset is subtracted.

### Undo

Editor tools have to tell Unity what they're about to change, or Ctrl+Z won't know how to undo it:

- `Undo.RegisterCreatedObjectUndo`: "this object was just created", so undoing deletes it.
- `Undo.RecordObject`: takes a snapshot of an object before changing it.
- `Undo.DestroyObjectImmediate`: deletes an object in a way that can be undone.

Each action opens an undo **group** and collapses it at the end, so a whole Add (or Snap, or Delete)
undoes with one Ctrl+Z instead of one asteroid at a time.

### Snapping to the orbit (`Snap`)

Moves each asteroid straight out from (or in toward) the orbit center until it sits on the orbit, keeping
its angle and depth:

1. Work out its position in spawner space, then its direction from the orbit center.
2. `Atan2` turns that direction into an angle (see AsteroidBehavior).
3. `OnCircle` gives the point on the orbit at that angle and the same depth.

An asteroid exactly at the center has no direction, so it's skipped.

- **Snap All to Circle** (button) snaps every child of the pattern.
- **Snap Selected to Circle** (Tools menu) snaps only the selected asteroids. It's a menu item instead of a
  button because selecting individual asteroids deselects the pattern root, which hides this whole panel.
  It only snaps direct children of a pattern with PatternGizmos.

### Deleting (`DeleteAll`)

Asks for confirmation with a popup, then deletes every child of the pattern, in a way that can be undone.
It loops **backward** from the last child. Deleting child 0 first would shift every other child's number
down by one, and the loop would skip every second asteroid.

## Talks to

- **PatternGizmos**: the component this panel is for. Reads its `OrbitRadius` and `OrbitCenter` (which
  come from `OrbitSettings`).
- **The asteroid prefab**: places copies of it, linked to the prefab.

Nothing in the game talks to it. It only runs in the editor.

## Gotchas

- **Builder settings reset when scripts recompile.** Static fields are cleared whenever Unity reloads
  scripts, which happens every time you save a code change. Only the prefab choice survives.
- **The default prefab path is hard-coded.** If `Asteroid.prefab` is moved or renamed again, the builder
  starts with an empty prefab field until one is picked. Once picked, it's remembered.
- **Snap and Delete act on every direct child.** Anything else placed under a pattern root (not an asteroid)
  would be snapped or deleted too. Keep only asteroids directly under the root.
- **Row isn't on the orbit.** It's a straight line, so at best it touches the circle in the middle. That's
  intended, but Snap All would bend a row onto the orbit.
