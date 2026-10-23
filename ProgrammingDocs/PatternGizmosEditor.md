# PatternGizmosEditor

## Purpose

The **Pattern Builder**: a panel in the Inspector for laying out wave patterns quickly. Choose whether to
place asteroids or power-ups, pick a shape (Ring, Arc, Row, Spiral, or Random), set how many and how deep, see a
preview in the Scene view (green for asteroids, yellow for power-ups), and click **Add** to place them
exactly on the player's orbit. Ring and Arc can leave a **gap** (a lane to fly through). It also has buttons to
snap existing children onto the orbit, rotate or mirror them, and clear a pattern, and a **Waves** section that speeds up the loop of making a wave: save the pattern as a new prefab
(and empty the builder), test just that pattern in the `PatternTest` scene, and add it to the spawner's waves.

It's an editor tool only. It isn't part of the game and isn't included in builds.

## Where it lives

- **File:** `Assets/Scripts/Editor/PatternGizmosEditor.cs`.
- **Shows up:** in the Inspector, under the PatternGizmos fields, whenever a pattern prefab's root is
  selected (usually in Prefab Mode).
- **Menu items:** Tools → Pattern Builder → Snap Selected to Circle, Rotate Selected, Mirror Selected, Save
  Pattern As..., Test This Pattern and Add to Spawner. Save, Test and Add to Spawner also have buttons in the
  **Waves** section of the panel, and Rotate and Mirror have **Rotate All** and **Mirror All** buttons.
- **Requires:**
  - A pattern root with PatternGizmos on it.
  - An asteroid prefab to place (defaults to `Assets/Prefabs/Asteroid Types/Asteroid.prefab`) and a
    power-up prefab (defaults to `Assets/Prefabs/Health PowerUp.prefab`).
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
| Power-Up Prefab | `Health PowerUp.prefab` | Which power-up gets placed |
| Asteroid / Power-Up tabs | Asteroid | Which of the two prefabs **Add** places. Also sets the preview color |
| Shape tabs | Ring | Which shape to build |
| Z | 0 | Depth, in spawner space. Lower Z spawns first |
| Count | 8 | How many objects to place (at least 1) |
| Preview Size | 1 | Size of the preview spheres. Only affects the preview |

There's no radius setting. Every shape that sits on a circle uses the orbit radius from `OrbitSettings`,
so everything always lands exactly on the player's orbit.

### Per shape

| Shape | Settings | What it builds |
| --- | --- | --- |
| **Ring** | Start Angle (-90), Gap Width (0), Gap Center (-90) | `Count` asteroids evenly spaced all the way around the orbit. With a gap, they fill the rest of the circle instead (see "The gap" below) |
| **Arc** | From Angle (-135), To Angle (-45), Gap Width (0), Gap Center (-90) | `Count` asteroids spread evenly along part of the orbit, including both ends. With a gap, a wedge is cut out of the arc |
| **Row** | Height Above Bottom (0), Spacing (1) | A straight horizontal line, centered left-to-right on the orbit. 0 is level with the bottom of the orbit, where the player starts |
| **Spiral** | Start Angle (-90), Angle Step (30), Z Step (2) | Each asteroid steps around the orbit by `Angle Step` degrees *and* back by `Z Step`, so each one is its own spawn step and they arrive one after another |
| **Random** | From Angle (-180), To Angle (180), Z Range (0), Min Gap (20), Seed (1), Reroll button | Up to `Count` objects at random angles between From and To, at least `Min Gap` degrees apart, each at depth `Z` plus a random amount up to `Z Range`. The same seed always gives the same layout. See "Random layout" below |

**Angles** are in degrees and work the same as PlayerMovement's `startAngle`: 0 is right (3 o'clock), 90 is
the top, -90 is the bottom (where the player starts), and angles increase counter-clockwise.

## How it works

### Remembering settings (static fields and EditorPrefs)

Unity creates a new Editor object every time you select something. Normal fields would reset every time
you clicked away. So all the builder settings are **static**, which means they belong to the class, not to
one Editor object. They last for the whole editor session and carry over when you switch between pattern
prefabs. They reset when Unity restarts or recompiles scripts.

The chosen asteroid and power-up prefabs are remembered longer, with **EditorPrefs**, a small key/value
store saved on your computer (not in the project). Each slot has its own key. In `OnEnable`, if a slot is
empty, it loads that slot's saved path, or its default. Because it's saved on your computer, each person
on the team has their own choice.

`SelectedPrefab` is a property that returns whichever slot the Asteroid / Power-Up tabs point at. The Add
button, its greyed-out check, and `Build` all use it.

### The Inspector panel (`OnInspectorGUI`)

Unity calls `OnInspectorGUI` every time the Inspector redraws. Unity's editor UI is **immediate mode**:
each `EditorGUILayout` call both draws a field and returns its current value, so every setting follows the
pattern:

```csharp
value = EditorGUILayout.SomeField("Label", value);
```

The panel, top to bottom:

1. **`DrawDefaultInspector()`** draws PatternGizmos' own fields (the ring colors) as normal.
2. A short help box explaining angles, Z, and the preview colors.
3. **Asteroid Prefab** and **Power-Up Prefab.** When either changes, its new path is saved to EditorPrefs.
4. **The Asteroid / Power-Up tabs, then the shape tabs and settings.** Only the selected shape's settings
   are shown. If anything changes, `SceneView.RepaintAll()` redraws the preview straight away, instead of
   waiting for the mouse to move. A change also sets `randomDirty`, so the Random layout is rebuilt. If the
   Random shape placed fewer than `Count`, a warning ("Placed 6 of 8: the minimum gap doesn't fit in the angle
   range.") shows under the fields.
5. **Add [Asteroid/Power-Up] [Shape]**, e.g. "Add Power-Up Arc". Greyed out while the selected slot is
   empty (`DisabledScope`). The same text is used as the Undo name.
6. **Waves:** the **Clear After Save** checkbox, **Save Pattern As...**, the **Test Loop Delay** field,
   **Test This Pattern** and **Add to Spawner** (see "The Waves tools" below).
7. **Snap All to Circle**, then **Rotate By** with **Rotate All** and **Mirror All**, then **Delete All
   Children.**

`BeginChangeCheck` / `EndChangeCheck` wrap a group of fields and report whether any of them changed.
That's how it knows when to save the prefab or redraw the preview.

### The preview (`OnSceneGUI`)

Unity calls `OnSceneGUI` whenever the Scene view redraws while the pattern is selected. It only draws on
the **Repaint** event (Scene GUI also runs for mouse moves and layout, which don't need drawing).

For each object about to be added, it draws a translucent sphere and a number showing the order they're
generated in: **green** when placing asteroids, **yellow** when placing power-ups. It draws in spawner
space (see PatternGizmos), the same way the rings are drawn.

When a Ring or Arc has a gap, it also draws the gap as a translucent **red wedge** (`Handles.DrawSolidArc`) at
the shape's depth, with the orbit's radius. `Handles.color` is set to red for the wedge and back to the preview
color afterwards, because the spheres use whatever color is current. On an Arc whose wedge only partly overlaps
the arc, the whole wedge is drawn, including the part outside the arc, which removes nothing.

The number labels use one `GUIStyle`, created the first time it's needed (GUI styles can't be built before
the editor's GUI is ready). Its color is set every time the style is read, so it follows the tabs.

### Where each asteroid goes (`ShapePosition` and `OnCircle`)

`ShapePosition(i)` gives the position of asteroid number `i` (counting from 0) for the selected shape.
**Both the preview and Add use it**, so what you see is exactly what gets placed.

`OnCircle(angle, z)` gives a point on the orbit, using the same cos/sin placement as PlayerMovement:
`center + (cos(angle), sin(angle)) × orbit radius`, after converting the angle from degrees to radians.

| Shape | Asteroid `i` goes at |
| --- | --- |
| Ring | Angle `startAngle + 360 × i / count`. Dividing by `count` (not `count − 1`) leaves a gap after the last one, so it doesn't land on top of the first. With `Gap Width` above 0, see `RingAngleWithGap` below |
| Arc | Angle `Lerp(from, to, i / (count − 1))`, which puts the first exactly at `from` and the last exactly at `to`. A single asteroid goes at `from`. With `Gap Width` above 0, see `ArcAngleWithGap` below |
| Row | x = center + `(i − (count − 1) / 2) × spacing`. That runs from `-half` to `+half`, so the row is centered. y = bottom of the orbit + height |
| Spiral | Angle `startAngle + angleStep × i`, at depth `z + zStep × i` |
| Random | The `i`th stored angle from the random layout, at depth `z + (its stored 0–1 fraction) × Z Range` |

Also, `PlacedCount` is how many objects the selected shape places. It's `count` for every shape except Random,
which can place fewer if the gap doesn't fit. The preview and `Build` loop over `PlacedCount`.

### The gap (`RingAngleWithGap` and `ArcAngleWithGap`)

`Gap Width` (degrees, 0 = off, limited to 0–359 in the panel) and `Gap Center` (degrees, default -90, the bottom
where the player starts) cut an empty wedge out of a Ring or Arc, so there's always a lane to fly through. With
`Gap Width` 0 the old formulas run, so existing Rings and Arcs don't change.

**Ring.** The `Count` asteroids fill the circle outside the gap, with the first and last at the gap's two edges:
`start = gapCenter + gapWidth / 2`, `step = (360 − gapWidth) / (count − 1)`, and asteroid `i` is at
`start + step × i`. With Gap Center -90, Gap Width 60 and Count 8: start = -60°, step = 42.86°, so the angles
are -60°, -17°, 26°, 69°, 111°, 154°, 197° and 240°. The last asteroid (240°, the same as -120°) and the first
(-60°) are 60° apart, centred on -90°. A Count of 1 goes at `start`. `Start Angle` is ignored while there's a
gap (the panel says so).

| Gap Width | Space between neighbours (Count 8) |
| --- | --- |
| 0 (off) | 360 ÷ 8 = 45° |
| 60° | (360 − 60) ÷ 7 = 42.9° |
| 120° | (360 − 120) ÷ 7 = 34.3° |

**Arc.** The wedge `gapCenter ± gapWidth / 2` is cut out of the arc, and the asteroids are spread evenly along
what's left, with none inside the wedge:
1. `length` is the arc's size and `dir` is +1 or -1 depending on which way it runs (From to To).
2. The wedge's two ends become distances along the arc from `arcFrom`, cut off at 0 and `length`
   (`Mathf.Clamp`), with `Min` and `Max` because `dir` -1 swaps them. `removed` is how much of the arc the
   wedge takes out. A wedge outside the arc clamps to a width of 0, so it removes nothing (the panel warns).
3. Each asteroid is placed at `(length − removed) × i / (count − 1)` along what's left, and any that is past
   the wedge's start jumps forward by `removed`.

Example: arc -135° to -45° (length 90), Gap Center -90, Gap Width 30, Count 5. The wedge is 30–60° along the
arc. The positions along what's left are 0, 15, 30, 45 and 60°, and the 45° one jumps by 30°, so the angles are
-135°, -120°, -105°, -60° and -45°.

The Arc's angles and `Gap Center` are compared on one number line, with no wrap-around. For an arc that crosses
±180°, use angles like 170 → 190 with a Gap Center of 180.

### Random layout (`BuildRandomLayout`)

The Random shape picks its angles once, from a seed, then keeps them:

1. **A seeded generator.** `new System.Random(randomSeed)` makes a private random-number generator. The same
   seed always gives the same sequence. It's written `System.Random` in full because `using System;` and
   `using UnityEngine;` together make a bare `Random` ambiguous. `UnityEngine.Random` isn't used on purpose:
   it's one global generator for the whole editor, so reseeding it would disturb other tools.
2. **For each of `count` objects, up to 50 tries.** Pick an angle between From and To
   (`Mathf.Lerp(from, to, NextDouble())`). If it is at least `Min Gap` degrees from every angle already placed
   (`FarEnough`), keep it and pick the object's depth fraction (another `NextDouble()`). Otherwise try again.
   This is **rejection sampling**: pick at random and throw away choices that don't fit.
3. **The gap is measured the short way round.** `Mathf.DeltaAngle` gives the shortest angle between two
   angles, so -170° and 170° are 20° apart, not 340°.
4. **The gap applies to every object, whatever its depth.** Two objects at slightly different depths still pass
   the player almost together, so they would look stacked.
5. **The arrays are cut to the objects that fit** with `Array.Resize(ref ...)`. If the gap is too big for the
   range, fewer than `count` are placed and a warning shows (below).

What's stored is each object's **angle and 0–1 depth fraction**, not a final position. The position is worked
out in `ShapePosition` with `OnCircle`, so changing the orbit radius, `Z` or `Z Range` moves the preview with
no re-roll. `randomDirty` says the stored lists are out of date. Any change in the panel sets it, so the
layout is rebuilt on the next read; rebuilding is harmless because the same seed and settings give the same
layout. The spheres don't jitter between redraws for the same reason.

**Reroll** sets the seed to `Environment.TickCount` and sets `GUI.changed = true` (a button click doesn't count
as a changed control, so the change check wouldn't otherwise see it).

**Add** places exactly what the preview shows. The random choice is made **in the editor, once**, and baked into
the pattern prefab. It isn't re-rolled each time the pattern spawns in a level.

### Adding objects (`Build`)

For each position:

1. **`PrefabUtility.InstantiatePrefab`** creates a copy of `SelectedPrefab` as a child of the pattern.
   Unlike plain `Instantiate`, this keeps the link to the prefab, so later changes to the prefab still
   reach these copies.
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

### Rotating and mirroring (`MoveChildren`)

Both change where existing children sit, around the orbit center, keeping each one's depth.

`MoveChildren(pattern, children, undoName, move)` does the shared work: for each child it works out its
position in spawner space, takes its **offset from the orbit center**, calls `move` to get the new offset,
and writes the new position back (minus the pattern root's own offset). The whole thing is one undo step.
`move` is a `Func<Vector2, Vector2>`, a function passed in (a higher-order function, like the comparison in
`Sort`). Rotate and Mirror only differ in the small function they pass:

| Action | What `move` does to a child's offset | Result |
| --- | --- | --- |
| **Rotate** (`RotateChildren`) | Turns it by `Rotate By` degrees with `Quaternion.Euler(0, 0, rotateBy) * offset` | Counter-clockwise for a positive number. A child off the circle, like a Row, keeps its distance from the center |
| **Mirror** (`MirrorChildren`) | Negates x, keeps y | Flips left-right across the vertical line through the orbit center. The new x is `2 × center.x − x`; an asteroid at angle θ ends up at 180° − θ, and one on the center line stays put |

Mirroring twice puts everything back. Neither changes a child's own rotation, which doesn't matter because
asteroids spin on their own.

- **Rotate All** and **Mirror All** (buttons) act on every direct child (`ChildrenOf`). **Rotate By** (default
  30) is the panel field.
- **Rotate Selected** and **Mirror Selected** (Tools menu) act on the selected direct children of one pattern
  (`SelectedPatternChildren`, which uses `Array.FindAll` with a function that keeps only children of a pattern
  with PatternGizmos, and returns the pattern through an `out` parameter). They are menu items for the same
  reason as Snap Selected, and they use the same `Rotate By` value. If the selection spans two patterns, only
  the first one found is changed.

### Deleting (`DeleteAll` and `ClearChildren`)

`DeleteAll` asks for confirmation with a popup, then calls `ClearChildren`, which deletes every child of the
pattern in a way that can be undone (one Ctrl+Z, named "Clear Pattern"). It loops **backward** from the last
child. Deleting child 0 first would shift every other child's number down by one, and the loop would skip
every second asteroid. Save Pattern As uses `ClearChildren` too.

### The Waves tools

All three work on the pattern open in Prefab Mode. `OpenPatternStage` returns that session (a `PrefabStage`),
or tells you to open a pattern first. They are `static`, so the Tools menu and the panel buttons call the same
methods.

**Save Pattern As...** (`SavePatternAs`)
1. Opens Unity's "save as" window, in `Assets/Prefabs/Patterns/`, starting at `Pattern_New`. Cancelling stops.
2. `PrefabUtility.SaveAsPrefabAsset` writes a new prefab from the open pattern. Nested prefabs (the
   asteroids) stay linked.
3. If **Clear After Save** is on (the default), `ClearChildren` empties the open pattern, ready for the next
   one. It's not saved to disk, and Ctrl+Z brings the children back.

This replaces the old manual loop of duplicating `Pattern_Builder` and clearing the original.

**Test This Pattern** (`TestPattern`)
1. Saves the open pattern, so the test uses what is on screen. If there are unsaved scene changes, Unity asks
   about them first; cancelling stops everything.
2. Leaves Prefab Mode, stores the request with `PatternTest.Request(path, testLoopDelay)` (see below), and
   remembers the pattern's path under `ReopenKey`.
3. Opens `Assets/Scenes/PatternTest.unity` and starts Play.
4. In Play, ObjectSpawner spawns only this pattern, in a loop (see its doc), and GameManager skips the menu if
   the scene has one (`PatternTest` doesn't). You have the full game: the whole range of speed, boosts and hits.
5. When Play stops, the request is cleared and the pattern reopens in Prefab Mode.

`Test Loop Delay` (default 3 s) is the pause after a run has fully cleared before the next one starts.

**Add to Spawner** (`AddToSpawner`)
1. Saves the open pattern, then finds an `ObjectSpawner` in any loaded scene (`FindSpawner`). Prefab Mode
   hides the scene it came from but leaves it loaded, so the level's spawner is found while you edit a prefab.
2. Skips (and says so) if the pattern is already in the spawner's `waves` list.
3. Otherwise adds a new wave with this pattern through a `SerializedObject` (Unity's editor-safe way to edit a
   component's saved fields, private ones included), and marks the scene changed so it gets saved. A new wave
   copies the previous wave's delay and points.

**Clean-up (`[InitializeOnLoad]`).** The class has a static constructor, which Unity runs when the editor
loads and whenever scripts recompile. It subscribes `OnPlayModeChanged` to `playModeStateChanged`:
- On `EnteredEditMode` it clears the test request and reopens the pattern. This is how a test ends.
- The constructor also clears a leftover request if the editor isn't entering Play. That covers an editor
  crash during a test, which would otherwise make the next normal Play a test. It skips the clear when
  `isPlayingOrWillChangePlaymode` is true, because entering Play also runs the constructor and would erase the
  request it just made.

## Talks to

- **PatternGizmos**: the component this panel is for. Reads its `OrbitRadius` and `OrbitCenter` (which
  come from `OrbitSettings`).
- **The asteroid and power-up prefabs**: places copies of the selected one, linked to the prefab.
- **PatternTest**: `TestPattern` stores the request with `Request`, and `OnPlayModeChanged` clears it.
- **ObjectSpawner**: Add to Spawner edits its `waves` list. In a test, it reads the request (see its doc).
- **GameManager**: in a test, it reads the request to skip the menu.
- **The `PatternTest` scene**: Test This Pattern opens `Assets/Scenes/PatternTest.unity`.

The game's scripts only read the test request, and only inside `#if UNITY_EDITOR`. A build contains none of it.

## Gotchas

- **Builder settings reset when scripts recompile.** Static fields are cleared whenever Unity reloads
  scripts, which happens every time you save a code change. Only the two prefab choices survive (the
  Asteroid / Power-Up tab goes back to Asteroid).
- **The default prefab paths are hard-coded.** If `Asteroid.prefab` or `Health PowerUp.prefab` is moved or
  renamed, the builder starts with that slot empty until one is picked. Once picked, it's remembered.
- **Snap and Delete act on every direct child,** asteroids and power-ups alike. Anything else placed under
  a pattern root would be snapped or deleted too. Keep only spawnable objects directly under the root.
- **Either slot accepts any prefab.** Nothing checks that the Power-Up slot holds a power-up. A wrong
  prefab is placed (and spawned) as-is.
- **Random is chosen once, when you press Add.** It isn't random in the game: every spawn of that pattern
  looks the same. Press Reroll and Add again for a different layout.
- **A gap too big for the range places fewer objects.** 20 objects with a 20° gap need 400° of circle; the
  warning shows how many fit. Lower the gap, widen the angle range, or lower `Count`.
- **A random power-up can land anywhere in the range,** including somewhere hard to reach. Check the preview
  before adding power-ups.
- **The test scene is set up so a test doesn't end by itself.** `PatternTest` was made from the old `TestGame`
  scene: no menu (`startWithMenu` off), no EndScreen, the LevelTimer's `levelTime` set to 9999 (so the earth
  never arrives and the spawner never stops) and the player's `maxLives` overridden to 99 (so game over is
  very hard to reach). If you change those, a test can end like a real level: the spawner stops at
  `levelTime`, and game over is normal. Stop and press Test again; Restart keeps the test going.
- **Opening the test scene leaves Prefab Mode,** and Stop brings you back to the pattern. Scene changes you
  haven't saved get a prompt first.
- **The test scene path is hard-coded** (`TestScenePath`). If `PatternTest` is moved or renamed, the button says
  it can't find it. Pointing it at another scene is a one-line change.
- **A wave with a missing pattern used to freeze Unity.** In `PatternTest` the spawner's only wave pointed at a
  deleted prefab, and `RunWaves` spun forever. It now logs a warning and stops (see ObjectSpawner.md).
- **Save Pattern As copies, it doesn't link.** The new prefab is independent of the one you built it from.
- **The gap width is measured between the two asteroids' centers.** The lane you can actually fly through is
  narrower, because the edge asteroids have their own size. On the radius-3 orbit 1° is about 0.05 units of
  arc, so a 60° gap is about 3.1 units between centers, minus an asteroid's width.
- **A Mirrored Wave asteroid isn't an exact mirror image.** Its position is flipped, but it always swings
  counter-clockwise first (its sine starts positive), so its motion isn't.
- **Rotate and Mirror move children, not the pattern's settings.** Gap Center and the shape fields aren't
  rotated, so use them for building and Rotate or Mirror for changing a finished pattern.
- **Row isn't on the orbit.** It's a straight line, so at best it touches the circle in the middle. That's
  intended, but Snap All would bend a row onto the orbit.
