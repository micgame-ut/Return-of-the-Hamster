# ObjectSpawner

## Purpose

Sends asteroids and power-ups at the player. It plays through a list of **waves**, where each wave is a
pattern prefab (a group of asteroids and power-ups laid out in the pattern builder). It reveals each
pattern's children a few at a time so they arrive in the shape they were laid out in, then moves on to
the next wave. It stops once the earth has arrived or the game is over.

It was called `AsteroidSpawner` until power-ups were added. The GameObject in `Game.unity` is still named
`Asteroid Spawner`.

## Where it lives

- **Attached to:** the `Asteroid Spawner` GameObject in `Game.unity`. Its position matters: patterns are
  spawned as its children, so a pattern keeps its offset from the spawner. The position comes from the
  `OrbitSettings` asset (see `Awake`).
- **Requires:**
  - Pattern prefabs whose **direct children** are asteroids (with AsteroidBehavior) or power-ups (with
    PowerUp).
  - `GameManager.Instance` for the LevelTimer and `IsGameOver` (to know when to stop) and the gameplay
    camera (to sort asteroids by distance). It still runs without one, but it never stops and sorts from
    z = 0.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `waves` | empty | The list of waves. Each has a `pattern` (the prefab to spawn) and a `delayAfter` |
| `waves[].delayAfter` | 2 | Seconds to wait after this pattern has finished appearing, before the next wave starts |
| `order` | Sequential | Sequential plays the list top to bottom. Random shuffles it each time through |
| `loop` | true | Start the list over after the last wave. If off, it plays through once and stops |
| `startDelay` | 1 | Seconds before the first wave (the scene has it set to 2) |
| `prewarmPerPattern` | 1 | Copies of each pattern made at level start, so the first waves don't stutter |

## Pooling

Pattern copies are **reused** instead of being created and destroyed every wave (object pooling, see
[PatternCopy.md](PatternCopy.md)):

- Each copy's root gets a `PatternCopy` at runtime. Its children stay **under the copy** the whole time,
  and asteroids and power-ups call `PatternCopy.Despawn` instead of `Destroy`, which switches them off and
  puts them back at their authored spot.
- `freeCopies` holds the free copies of each pattern prefab (a `Dictionary` from prefab to a `Stack`).
  `revealOrder` holds each copy's children, **sorted once** when the copy is made.
- `GetCopy` pops a free copy, or makes one with `CreateCopy` when none is free (no limit). A copy is free
  again when PatternCopy's `OnReleased` fires, which `HandleReleased` pushes back onto its free list.
- After the first loop or two, no new copies are made at all.

## How it works

It's built from two coroutines (methods that can pause and continue later, see AsteroidBehavior):
`RunWaves` handles the list of waves, and `SpawnPattern` handles revealing the asteroids inside one
pattern.

### `StopSpawning`: when to stop

A private property that's true once the earth has arrived (LevelTimer's `IsComplete`) **or** the game is
over (GameManager's `IsGameOver`). `RunWaves` and `SpawnPattern` both check it, so one property covers
both reasons to stop. Each part checks for `null` first, so a missing LevelTimer or GameManager just
counts as "not stopped".

### `Awake`

Moves the spawner to `SpawnerPosition` from `OrbitSettings`. The pattern builder reads the same value, so
patterns built in the editor always line up with where they spawn in the game.

### `Start`

Gets the LevelTimer from GameManager, **prewarms** (for each wave's pattern, makes copies until its free list
holds `prewarmPerPattern`; a pattern listed in two waves only gets them once), then starts `RunWaves`, as
long as there's at least one wave.

### `CreateCopy`, `GetCopy`, `GetFreeList`, `HandleReleased`

- **`CreateCopy`** instantiates the pattern under the spawner, switches its children off, sorts them closest
  to the camera first (`Array.Sort` with a comparison function), adds a `PatternCopy` and calls `Init`,
  subscribes `HandleReleased` to its `OnReleased`, and stores the sorted children in `revealOrder`.
- **`GetCopy`** returns a free copy if the pattern has one, otherwise `CreateCopy`.
- **`GetFreeList`** returns a pattern's free list, making an empty one the first time (`TryGetValue`).
- **`HandleReleased`** pushes a copy that's free again back onto its pattern's list.

### `RunWaves`: going through the list

1. Wait `startDelay` seconds.
2. For each wave, in the order `GetWaveOrder` gives:
   - If `StopSpawning` is true, stop for good.
   - If the wave has no pattern assigned, skip it.
   - Run `SpawnPattern` and **wait for it to finish**. `yield return SpawnPattern(...)` runs another
     coroutine inside this one and continues only when that one is done.
   - Wait `delayAfter` seconds.
3. If `loop` is on and `StopSpawning` is still false, go back to step 2.

`delayAfter` starts once the pattern's **last asteroid has appeared**, not once it has flown past. So
with a short delay, the next pattern can start while the previous one is still coming at the player.

### `GetWaveOrder`: sequential or shuffled

Returns a copy of the wave list. For Random, it shuffles the copy by walking backward through it and
swapping each wave with a randomly picked one at or before it. That's the **Fisher-Yates shuffle**,
which gives every possible order an equal chance.

Shuffling the whole list (instead of picking a random wave each time) means every wave plays once per
pass, with no wave repeated or skipped until the pass is done.

### `SpawnPattern`: revealing one pattern

This is the heart of the script. The goal is for the asteroids to reach the player in the same shape
and spacing they were laid out in, including their depth. Asteroids further back in the pattern
should arrive later.

**1. Get a copy.** `GetCopy` gives a pattern copy whose children are all switched off and at their
authored spots, and `revealOrder` gives them sorted by how far each is from the camera along Z, closest
first. That's the order they'll appear in. Both were done once, in `CreateCopy`, so the player never sees
the whole pattern at once. `BeginReveal` tells the copy the spawner is using it.

**2. Pick the spawn plane.** `spawnZ` is the Z of the closest asteroid. **Every** asteroid appears at
this Z, not where it was laid out.

**3. Reveal them in order, waiting between depths.** Rather than placing far-back asteroids further
away, it places them all at the spawn plane, so every asteroid appears from the same distance, and
reveals the deeper ones **later**. The wait is how long an asteroid takes to fly the gap:

`wait = gap / ApproachSpeed` (time = distance ÷ speed)

For example, three asteroids laid out 5 units apart in Z, approaching at 10 units/sec:

| Asteroid | Laid out at | Gap to previous | Waits | Appears at |
| --- | --- | --- | --- | --- |
| A | 20 from the camera | none | none | spawn plane, straight away |
| B | 25 | 5 | 5 ÷ 10 = 0.5s | spawn plane, 0.5s later |
| C | 30 | 5 | 0.5s | spawn plane, 1s after A |

By the time B appears, A has flown 5 units, so they're 5 apart, just as they were laid out.

**Where `ApproachSpeed` comes from: `GetApproachSpeed`.** A small helper that asks the child for its
approach speed: an asteroid's `AsteroidBehavior.ApproachSpeed` if it has one, otherwise a power-up's
`PowerUp.ApproachSpeed`, otherwise 0 (no wait). Power-ups use the same formula as asteroids, so a
power-up laid out between two asteroids stays between them in flight.

Asteroids at the **same depth** (within `SameZTolerance`, 0.01 units) have no gap, so they appear
together. `SameZTolerance` is `public const`, so PatternGizmos can group asteroids the same way. That's how a ring or row laid out at one depth appears all at once.

For each asteroid, once its wait is over:
- If `StopSpawning` is true (the earth arrived or the game ended mid-pattern), stop revealing. The hidden
  children stay home in the copy.
- Move it to the spawn plane, keeping its x/y. It **stays under the copy**: asteroids and power-ups set
  their world position every frame and the copy never moves, so being parented changes nothing about how
  they fly, and `PatternCopy.Despawn` can find their copy later.
- `ChildRevealed` counts it as out (before it's switched on, so the count is right before it can do anything).
- **Switch it on.** That runs its `OnEnable`, which records its start point (and, for asteroids, begins
  the warning blink; power-ups skip it).

**4. Finish.** `EndReveal` tells the copy the spawner is done with it. If every revealed child is already
home, the copy is free straight away; otherwise the last child home frees it.

## Talks to

- **GameManager**: gets the LevelTimer (in `Start`) and the gameplay camera (each pattern), and reads
  `IsGameOver` through `StopSpawning`.
- **OrbitSettings**: reads `SpawnerPosition` in `Awake`.
- **LevelTimer**: checks `IsComplete`, through `StopSpawning`, to stop spawning once the earth has arrived.
- **AsteroidBehavior** and **PowerUp**: reads `ApproachSpeed` (through `GetApproachSpeed`) to time the
  gaps, and switches them on, which starts their `OnEnable`.
- **PatternCopy**: adds one to each copy, calls `Init`, `BeginReveal`, `ChildRevealed` and `EndReveal`, and
  listens to `OnReleased`.
- **PatternGizmos / PatternGizmosEditor**: indirectly. The pattern builder lays out the pattern prefabs
  this script spawns.

## Gotchas

- **Moving the spawner in the scene does nothing.** It moves itself to `OrbitSettings`' `spawnerPosition`
  when the game starts. Change the asset instead.
- **Every pattern empty means a freeze.** If `loop` is on and every wave's `pattern` is empty,
  `RunWaves` skips them all without ever waiting, then loops again, forever. Unity freezes. One real
  pattern in the list is enough to avoid it.
- **Spacing is only exact at a steady speed.** The wait is calculated from the approach speed when it
  starts. If the player speeds up or slows down during the wait, the gap between asteroids in flight
  ends up smaller or bigger than laid out.
- **Mixed asteroid speeds throw off the spacing.** The wait uses the next asteroid's `ApproachSpeed`, but
  the asteroid ahead of it is moving at its own speed. If they have different `speed` values, they drift
  closer or further apart.
- **Only direct children are spawned.** Asteroids inside a sub-group in the pattern are revealed together
  with the group, and there's no wait for them (the group has no AsteroidBehavior or PowerUp, so no speed).
- **A new kind of spawnable needs a line in `GetApproachSpeed`.** Anything else placed in a pattern is
  revealed with no wait, so it drifts off its laid-out spacing.
- **Everything appears at the closest asteroid's depth.** Moving a whole pattern closer to or further from
  the camera changes where its asteroids appear.
- **Stopping doesn't clear the screen.** At game over or when the earth arrives, asteroids already
  revealed keep flying until they pass the camera. Unrevealed ones just stay hidden in their copy.
- **Anything a pattern spawns must leave through `PatternCopy.Despawn`, never `Destroy`.** A destroyed child
  leaves a hole in its copy, and the next reuse of that copy throws errors.
- **Pattern copies persist for the whole level** under the spawner. That's why PatternGizmos skips drawing
  in Play.
- **Random can repeat a wave back to back.** The last wave of one shuffle and the first of the next can be
  the same.
- **Asteroids' `OnEnable` runs once when a copy is created,** before they're switched off. See
  AsteroidBehavior's gotchas.
