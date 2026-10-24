# PatternGizmosEditor

## Purpose

The **Pattern Builder**: a panel in the Inspector for laying out wave patterns quickly. Choose whether to
place asteroids or power-ups, pick a shape (Ring, Arc, Row, Spiral, Zigzag, Twist, or Random), set how many and how deep, see a
preview in the Scene view (green for asteroids, yellow for power-ups), and click **Add** to place them
exactly on the player's orbit. Ring and Arc can leave a **gap** (a lane to fly through). It also has buttons to
snap existing children onto the orbit, rotate or mirror them, and clear a pattern, and a **Waves** section that speeds up the loop of making a wave: save the pattern as a new prefab
(and empty the builder), test just that pattern in the `PatternTest` scene, and add it to the spawner's waves.

It's an editor tool only. It isn't part of the game and isn't included in builds.

## Where it lives

- **File:** `Assets/Scripts/Editor/PatternGizmosEditor.cs`.
- **Shows up:** in the Inspector, under the PatternGizmos fields, whenever a pattern prefab's root is
  selected (usually in Prefab Mode).
- **Menu items:** Tools → Pattern Builder → Snap Selected to Circle, Rotate Selected, Mirror Selected, Shift
  Selected Z, Reverse Selected Z, Distribute Selected by Depth, Distribute Selected by Angle, Save Pattern As...,
  Test This Pattern and Add to Spawner. Save, Test and Add to Spawner also have buttons in the **Waves**
  section of the panel, and Rotate, Mirror, Shift Z and Reverse Z have **Rotate All**, **Mirror All**,
  **Shift Z All** and **Reverse Z All** buttons. (Distribute is menu-only.)
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
| **Spiral** | Start Angle (-90), Angle Step (30), Z Step (2) | Each asteroid steps around the orbit by `Angle Step` degrees *and* back by `Z Step`, so each one is its own spawn step and they arrive one after another. **Angle Step 0 = a Lane** (the same angle, going deeper) and **Z Step 0 = a Burst** (the same depth, spaced by Angle Step); a note in the panel says so |
| **Zigzag** | Angle A (-135), Angle B (-45), Z Step (2) | Asteroids alternate between angle A (even numbers) and angle B (odd), each `Z Step` deeper, so the player swings left and right |
| **Twist** | Rings (5), Z Step (3), Twist Per Ring (15), Start Angle (-90), Gap Width (0), Gap Center (-90) | `Count` asteroids per ring, `Rings` rings stacked in depth, each ring turned `Twist Per Ring` more than the one before. It uses the Ring's start angle and gap, and a gap turns with its ring, so the safe lane spirals down the tunnel |
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
2. A short help box explaining angles, Z, and the preview colors, then the **Presets** section (see "Presets"
   below).
3. **Asteroid Prefab** and **Power-Up Prefab.** When either changes, its new path is saved to EditorPrefs.
4. **The Asteroid / Power-Up tabs, then the shape tabs and settings.** Only the selected shape's settings
   are shown. If anything changes, `SceneView.RepaintAll()` redraws the preview straight away, instead of
   waiting for the mouse to move. A change also sets `randomDirty`, so the Random layout is rebuilt. If the
   Random shape placed fewer than `Count`, a warning ("Placed 6 of 8: the minimum gap doesn't fit in the angle
   range.") shows under the fields. For every shape except Random, a **Jitter** block follows the shape's fields.
5. **Add [Asteroid/Power-Up] [Shape]**, e.g. "Add Power-Up Arc". Greyed out while the selected slot is
   empty (`DisabledScope`). The same text is used as the Undo name.
6. **Prefab Mix**, **Timing** and **Fairness** (see "The Prefab Mix" above, and "The Timing section" and "The Fairness section" below), then **Waves:** the **Clear After Save** checkbox, **Save Pattern As...**, the **Test Loop Delay** field,
   **Test This Pattern** and **Add to Spawner** (see "The Waves tools" below).
7. **Snap All to Circle**, then **Rotate By** with **Rotate All** and **Mirror All**, then **Shift Z By** with
   **Shift Z All** and **Reverse Z All**, then **Delete All
   Children.**

**Tooltips:** every field, button and tab in the panel has hover text. Because the panel is drawn in code, a
plain `[Tooltip]` attribute isn't available. Each control takes a `GUIContent` (a label plus a tooltip) instead,
for example `EditorGUILayout.FloatField(new GUIContent("Gap Width", "Width of an empty wedge…"), gapWidth)`, and
the two tab bars use `GUIContent[]` arrays (`ShapeTabs`, `PlacingTabs`) so each tab has its own. The Prefab Mix
weight boxes have no label of their own, so their explanation is on the prefab row's label. AGENTS.md requires a
tooltip on every new control.

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
**Both the preview and Add use it**, so what you see is exactly what gets placed. It is a small wrapper: it asks
`BasePosition(i)` (the shape's own position, the table below) and then, for every shape except Random, applies
the jitter (see "Jitter" below).

`OnCircle(angle, z)` gives a point on the orbit, using the same cos/sin placement as PlayerMovement:
`center + (cos(angle), sin(angle)) × orbit radius`, after converting the angle from degrees to radians.

| Shape | Asteroid `i` goes at |
| --- | --- |
| Ring | Angle `startAngle + 360 × i / count`. Dividing by `count` (not `count − 1`) leaves a gap after the last one, so it doesn't land on top of the first. With `Gap Width` above 0, see `RingAngleWithGap` below |
| Arc | Angle `Lerp(from, to, i / (count − 1))`, which puts the first exactly at `from` and the last exactly at `to`. A single asteroid goes at `from`. With `Gap Width` above 0, see `ArcAngleWithGap` below |
| Row | x = center + `(i − (count − 1) / 2) × spacing`. That runs from `-half` to `+half`, so the row is centered. y = bottom of the orbit + height |
| Spiral | Angle `startAngle + angleStep × i`, at depth `z + zStep × i` |
| Zigzag | Angle `zigzagAngleA` if `i % 2 == 0`, otherwise `zigzagAngleB`, at depth `z + zStep × i`. Example: A -135°, B -45°, Z step 2, Count 5 gives -135° at depth 0, -45° at 2, -135° at 4, -45° at 6, -135° at 8 |
| Twist | Object `i` is in ring `i / Count` (whole-number division), at place `i % Count` in it. Its angle is the Ring angle for that place (the gap version if a gap is set) plus `twistAngle × ring`, at depth `z + twistZStep × ring`. `PlacedCount` is `Count × Rings`. Example: Count 8, 3 rings, twist 15°: ring 0 at -90°, -45°, 0°…; ring 1 at -75°, -30°, 15°… (3 deeper); ring 2 at -60°, -15°, 30°… (6 deeper) |
| Random | The `i`th stored angle from the random layout, at depth `z + (its stored 0–1 fraction) × Z Range` |

Also, `PlacedCount` is how many objects the selected shape places. It's `count` for every shape except Random,
which can place fewer if the gap doesn't fit. The preview and `Build` loop over `PlacedCount`.

### Jitter (`Jitter`)

A small random nudge on every object, so a pattern doesn't look machine-perfect. It works on every shape except
Random (already random), is chosen when you press **Add**, and is baked in like the Random layout.

| Setting | Default | What it changes |
| --- | --- | --- |
| Jitter Angle | 0 (degrees) | Each object is moved round the orbit by a random amount between -Jitter Angle and +Jitter Angle. 0 = off |
| Jitter Z | 0 | Each object's depth is moved by a random amount between -Jitter Z and +Jitter Z. 0 = off |
| Seed / Reroll | the Random tab's seed | Shown once a jitter is on. The same `randomSeed` the Random tab uses |

For object `i`, `Jitter` creates `new System.Random(randomSeed)` and throws away the two numbers each earlier object
used, then takes two numbers between -1 and +1 (`NextDouble() × 2 − 1`): the first times `jitterAngle` is how
far to turn the position round the orbit center (`Quaternion.Euler(0, 0, turn) * offset`, like Rotate, so it
works on a Row too), and the second times `jitterZ` is added to the depth. So the same seed always gives the
same layout, and object 5 gets the same wobble whether Count is 6 or 60. Example: a Ring object at -90°,
depth 0, with Jitter Angle 5 and Jitter Z 1 and numbers +0.6 and -0.5 moves to -87° at depth -0.5.

Objects spawn together only if their depths match within `SameZTolerance` (0.01). A Ring at one depth is one
step, but with Jitter Z above 0.01 every object gets its own depth and becomes its own step, arriving one
after another. The panel says so, and the Scene labels show it. Jitter Angle alone doesn't do that. Jitter can
also push objects together or close a gap, so use the Fairness section afterwards.

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

1. **`PrefabUtility.InstantiatePrefab`** creates a copy of the prefab `PickPrefab(i)` gives as a child of the
   pattern. Unlike plain `Instantiate`, this keeps the link to the prefab, so later changes to the prefab still
   reach these copies.
2. **Set its position.** Shape positions are in spawner space, but a child's position is relative to the
   pattern root, so the root's own offset is subtracted.

### The Prefab Mix (`DrawMix`, `PickPrefab`)

Each slot (Asteroid and Power-Up) can pick between up to **4 prefabs** at random, each with a **weight**, so one
Ring can mix Linear, Destructible and Phase asteroids. Entry 1 is the slot's existing prefab field; entries 2–4
are extra prefab slots, each with a weight box beside it. Entry 1's weight starts at 1 and the others at 0
(unused), so nothing changes until you fill one in. The section follows the Asteroid / Power-Up tab, and it
sits below the Add button.

`PickPrefab(i)` makes the choice for object `i`:
1. Make the list of the four options (the selected prefab and the three extras). An empty slot adds nothing.
2. Add up the weights of the filled slots (`total`). If that's 0, use the selected prefab.
3. Roll a number from 0 to `total`, from a seeded stream (`new System.Random(randomSeed + 4242)`, skipping the
   numbers earlier objects used, so the pick doesn't depend on Count and is independent of the jitter's numbers).
4. Walk down the entries: if the roll is below an entry's weight, that's the pick; otherwise subtract the weight
   and move on.

Example: weights 3, 1, 1 (total 5). A roll of 3.4 isn't below 3, so subtract 3 → 0.4, below 1, so entry 2. A roll
of 4.5 → 1.5 → 0.5, below 1, so entry 3. A prefab with weight 0 can never win. The same seed gives the same
picks, and **Reroll** (with Jitter on, or from the Random tab) reshuffles which object gets which type.

The extra prefabs (as asset paths) and all the weights are saved in `EditorPrefs` (`SaveMix`) and read back once
per editor session (`LoadMix`, in `OnEnable`), like the Asteroid and Power-Up prefab choices. The preview spheres
stay one colour, so which type an object is shows only after **Add**.

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

### Depth edits and Distribute

`ChangeDepth(children, undoName, newZ)` is the depth-only sibling of `MoveChildren`: it takes a function that
turns each child's old local Z into a new one, and writes back only the Z (angle and distance from the center
stay). One undo step.

- **Shift Z** (`ShiftChildrenZ`): `z => z + shiftZBy`. Depths 0, 3, 10 with a shift of 5 become 5, 8, 15. The
  nearest step is also where everything appears, so shifting everything deeper moves the spawn depth and changes
  the Timing table's "Appears → reaches player" time.
- **Reverse Z** (`ReverseChildrenZ`): finds the smallest and largest depth among the children given, then
  `z => min + max − z`. Depths 0, 3, 10 become 10, 7, 0. The range doesn't change, so the spawn depth doesn't
  either.
- **Shift Z All / Reverse Z All** (buttons) act on every child. **Shift Selected Z / Reverse Selected Z** (Tools
  menu) act on the selected children (`SelectedPatternChildren`). **Shift Z By** (default 5) is the panel field.

**Distribute** (Tools menu only, for the selected children):
- **Distribute Selected by Depth** (`DistributeDepth`): sorts the children by depth, keeps the nearest and the
  furthest where they are, and respaces the ones between evenly with `Mathf.Lerp(first, last, i / (n − 1))`.
  Depths 0, 1, 10, 12 become 0, 4, 8, 12. It needs at least 3 children.
- **Distribute Selected by Angle** (`DistributeAngle`): records each child's angle and distance from the orbit
  center as a named tuple `(t, angle, distance)`, sorts by angle, and gives child `k` the angle
  `lowest angle + 360 × k / n`. Each keeps its depth and its distance, so a child off the circle stays at its own
  distance. 4 objects with the lowest at -90° become -90°, 0°, 90°, 180°. It needs at least 2 children.

### Presets (`DrawPresets`)

A preset is a snapshot of the builder's **placement settings**, saved as a small readable `.json` file in
`Assets/Scripts/Editor/PatternPresets/` and loaded back by name. The files are in the project, so they travel with
git and a preset someone else commits appears in your dropdown.

**What's in a preset** (the `Preset` class, one field per setting): the Asteroid / Power-Up tab, the shape, Z,
Count, every shape's own settings (Ring, Arc, Row, Spiral, Zigzag, Twist, Random), the Gap, Jitter and the seed,
and the Prefab Mix: the two main prefabs and the extras as **asset paths**, plus all the weights. **Not in it:**
Preview Size, Rotate By, Shift Z By, the naming checkbox, Timing, Fairness and the Waves/test settings.

**The section** (at the top of the panel, under the help box):
- **Preset** dropdown (`PresetNames`): the `.json` files in the folder, without the extension.
- **Load**: reads the file and puts every value into the builder (`ApplyPreset`), reloading the prefabs from their
  paths and saving the prefab choices and the mix again so they survive a restart. It copies the preset's name into
  the name box, so saving again updates it. It doesn't touch objects already in the pattern, and it can't be undone
  (it only changes the builder's settings).
- **Delete Preset**: asks first, then deletes the file (`AssetDatabase.DeleteAsset`, so the `.meta` goes too).
- **Name** box and **Save Preset**: greyed out while the name is empty. If a preset with that name exists, it asks
  before overwriting.

**How it works.** `CapturePreset` copies each static setting into a `Preset`. `SavePreset` turns it into text
with `JsonUtility.ToJson(…, true)` (indented) and writes `<name>.json` (creating the folder the first time), then
`AssetDatabase.Refresh()` so Unity notices it. `LoadPreset` reads the text, `JsonUtility.FromJson<Preset>`
turns it back, and `ApplyPreset` copies it in. `PresetPath` replaces characters Windows doesn't allow in a file
name (`/`, `:` and so on) with `_`. A prefab path that no longer exists, or a field missing from an older file,
loads as an empty slot (weight 1 for the first entry, 0 for the rest) and counts of at least 1 are enforced.

Example: a preset called `Gap Ring 12` is a file like
`{ "shape": 0, "count": 12, "z": 0, "gapWidth": 60, "gapCenter": -90, "jitterAngle": 0, "asteroidPrefab": "Assets/Prefabs/Asteroid Types/Linear.prefab", … }`.
Loading it sets Ring, Count 12 and a 60° gap at the bottom, and the preview updates.

**`GUIUtility.ExitGUI()`** is called after Load and Delete. The panel is drawn in two passes that must draw the
same controls, and loading changes what is drawn below (the shape's fields). `ExitGUI` stops the current pass
cleanly and Unity redraws at once with the new values. It's the standard fix for a button that changes what the
panel shows.

### Naming the children (`NameChildren`)

Names every child of the pattern `Step N - Prefab K` and puts them in step order in the Hierarchy, so the list
reads like the Scene labels (`Step 3 (4, 1 power-up)`). It runs after each **Add** when **Name Children After Add**
is ticked (the default), and the **Name Children** button does it on demand, for patterns built earlier.

1. `Undo.RegisterFullObjectHierarchyUndo` records the pattern and all its children (names and order) first, so
   one Ctrl+Z restores them. When it runs from **Add**, it is inside Add's undo group, so Add and the renaming
   undo together.
2. For each step from `GetSteps()` and each child in it: the name is `Step {step number} - {prefab name} {k}`,
   where `k` counts the objects within the step from 1 (it is just the order the objects already have, not
   sorted by angle). The prefab name comes from `PrefabUtility.GetCorrespondingObjectFromSource` (the prefab
   asset the object was made from), or the object's current name if it isn't a prefab instance. That avoids
   names like `Linear (3)` and means a Prefab Mix reads properly.
3. `SetSiblingIndex(sibling++)` puts each child at the next place in the Hierarchy, so step 1's objects come
   first, then step 2's, and so on.

Example: a Ring of 4 Linear asteroids at depth 0, then a Linear and a Health power-up at depth 3, becomes
`Step 1 - Linear 1` to `Step 1 - Linear 4`, then `Step 2 - Linear 1` and `Step 2 - Health PowerUp 2`.

Running it again gives the same names, because each name is built from the prefab's name, not from the old name.
Nothing in the game looks pattern children up by name, so renaming is safe. The names are only for reading.

### Deleting (`DeleteAll` and `ClearChildren`)

`DeleteAll` asks for confirmation with a popup, then calls `ClearChildren`, which deletes every child of the
pattern in a way that can be undone (one Ctrl+Z, named "Clear Pattern"). It loops **backward** from the last
child. Deleting child 0 first would shift every other child's number down by one, and the loop would skip
every second asteroid. Save Pattern As uses `ClearChildren` too.

### The Timing section (`DrawTiming`)

Shows how the pattern **already in the prefab** is paced, at three player `Speed`s: slow (0), the **Speed**
slider's value (default 50) and fast (the player's top speed). The same step times appear on the Scene view
labels (see PatternGizmos). The slider runs from 0 to the player's `maxSpeed`, read from
`Assets/Prefabs/hamter.prefab` (`PlayerMaxSpeed`, through a `SerializedObject`, so it stays in sync; 10 if the
prefab can't be read), and moving it redraws the Scene view.

| Row | Meaning |
| --- | --- |
| Approach speed (units/s) | `baseApproachSpeed + Speed × speedMultiplier`, from the SpawnableTemplate |
| Appears → reaches player | `warningTime` plus the distance from the spawn plane to the player, divided by the approach speed |
| Between steps, shortest / longest | The smallest and largest wait between two steps in a row (`StepWait`). The shortest is your tightest reaction window. "-" for a one-step pattern |
| Total length | From the first step to the last step arriving, `(last Z − first Z) ÷ approach speed` |

Example (template: base 20, multiplier 0.3, warning 0; spawner at z 40 so the player is 44 units from the
spawn plane): a pattern with steps at depths 0, 10 and 25 shows 20 / 35 / 50 units/s, appears → reaches player
2.20 / 1.26 / 0.88 s, shortest 0.50 / 0.29 / 0.20 s, longest 0.75 / 0.43 / 0.30 s, and total 1.25 / 0.71 / 0.50 s.

How it's drawn: `TimingRow(label, speeds, value)` draws one row of the table. Its `value` argument is a small
function that turns a speed into text, and the row calls it once per column. With no children it says "No
children yet."; with no template found, it says so.

### The Fairness section (`DrawFairness`)

Warns about problems in the children already in the prefab. It is warnings only, and it assumes the patterns
stay within the player's limits for now. The checks themselves are in PatternFairness (see
[PatternFairness.md](PatternFairness.md)); this section holds the settings, calls them every time the Inspector
draws, and shows the result.

| Setting | Default | Meaning |
| --- | --- | --- |
| Player Radius | 0.3 | The player's hitbox radius. 0.3 is the shield's collider, the biggest, so it's the worst case. The player prefab has colliders of 0.3, about 0.26 (0.5 on a 0.51-scale object) and 0.2 |
| Lane Margin (degrees) | 5 | Room a gap needs, on top of the bare minimum, to count as a lane |
| Player Move Speed (deg/s) | 90 | How fast the player is assumed to move around the orbit. It sets the "can they get there in time" checks, so tune it by feel |

The player `Speed` for the time-based checks is the Timing section's **Speed** slider.

**What it reports** (each as a yellow box, and with red rings on the objects involved in the Scene view):
- *Step N: two asteroids overlap.*
- *Step N: no gap wide enough for the player.*
- *Step N → N+1: the nearest gap is X° away, but only Y° can be covered in Z s.*
- *Step N: a power-up is too far from the orbit to collect / is blocked by an asteroid / is X° from the nearest gap before it, but only Y° can be covered in Z s.*

"No problems found." in a blue box means all the checks passed. An empty pattern says "No children yet."

Not checked: **Wave asteroids** (they swing sideways, so they aren't treated as obstacles), step 1 for
reach (the player is assumed to be able to be in any of its gaps), and anything beyond the Z steps themselves.
Destructible and Phase asteroids count as obstacles (the worst case).

The latest result is kept in `fairnessWarnings` (a static array), which `OnSceneGUI` reads to draw a red wire
ring (`WarningColor`) on each warning's objects, at the same spawner-space position the checks used. The rings
update whenever the Inspector redraws.

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
   remembers the pattern's path under `ReopenKey` and the scene you were on (`GetActiveScene().path`, taken after
   leaving Prefab Mode) under `ReturnSceneKey`. An unsaved new scene has no path, so it isn't remembered.
3. Opens `Assets/Scenes/PatternTest.unity` and starts Play.
4. In Play, ObjectSpawner spawns only this pattern, in a loop (see its doc), and GameManager skips the menu if
   the scene has one (`PatternTest` doesn't). You have the full game: the whole range of speed, boosts and hits.
5. When Play stops, the request is cleared, you go back to the scene you started from (if it isn't already
   open; Unity asks first if the open scene has unsaved changes), and the pattern reopens in Prefab Mode.

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
- On `EnteredEditMode` it clears the test request, returns to the scene you started from, and reopens the
  pattern. This is how a test ends. The scene and pattern are reopened on the editor's next update
  (`delayCall`), scene first.
- The constructor also queues (with `delayCall`, so it runs on the editor's next update) a clear of any leftover
  request if the editor isn't entering Play. That covers an editor crash during a test, which would otherwise
  make the next normal Play a test. It skips the clear when `isPlayingOrWillChangePlaymode` is true, because
  entering Play also runs the constructor and would erase the request it just made. The check is *not* made
  inside the constructor itself: Unity refuses `isPlayingOrWillChangePlaymode` while it is constructing a
  ScriptableObject (an `Editor` is one), and the constructor can run at exactly that moment, for example when
  scripts recompile with a pattern selected. That threw a `TypeInitializationException` and filled the Console
  with errors until the check was moved.

## Talks to

- **PatternGizmos**: the component this panel is for. Reads its `OrbitRadius` and `OrbitCenter` (which
  come from `OrbitSettings`), and for the Timing section calls `GetSteps`, `ApproachSpeedAt`, `SecondsBetween`
  and `SecondsToPlayer`, and sets `PreviewSpeed`.
- **PatternFairness**: the Fairness section builds a `PatternFairness.Settings` and calls `Check`, then shows the
  returned `Warning`s.
- **The player prefab** (`Assets/Prefabs/hamter.prefab`): the Timing slider reads its `PlayerMovement`'s
  `maxSpeed`.
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
- **The Fairness checks run on every Inspector redraw.** They are cheap enough for that, but a very large
  pattern (hundreds of objects) may make the panel feel slow.
- **The fairness numbers are design targets.** The player hitbox, the move speed and the margin are all
  assumptions, and a pattern the checks call fair can still be hard, or unfair in a way they don't look for.
  The collider sizes they use are the ones on the prefabs in Prefab Mode, so an asteroid scaled in the pattern
  counts at its real size.
- **The Timing numbers assume a steady `Speed` and the template in `Assets/Resources/`.** Real spacing
  changes if the player speeds up or slows down in flight, so treat them as the design target. They describe
  the children already in the prefab, not the Add preview.
- **A preset stores prefab asset paths.** Move or rename a prefab and that slot loads empty until you save the
  preset again. The same goes for the default prefab paths.
- **Presets don't store Preview Size, the edit settings, Timing, Fairness or the Waves settings.** Those stay as
  they are when you load one.
- **The red gap wedge is only drawn for Ring and Arc,** not Twist, even though a gapped Twist leaves the gap.
- **In Twist, `Count` is the number per ring,** not the total. The total is `Count × Rings`.
- **A Mirrored Wave asteroid isn't an exact mirror image.** Its position is flipped, but it always swings
  counter-clockwise first (its sine starts positive), so its motion isn't.
- **Rotate and Mirror move children, not the pattern's settings.** Gap Center and the shape fields aren't
  rotated, so use them for building and Rotate or Mirror for changing a finished pattern.
- **Row isn't on the orbit.** It's a straight line, so at best it touches the circle in the middle. That's
  intended, but Snap All would bend a row onto the orbit.
