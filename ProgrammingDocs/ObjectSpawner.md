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
| `waves[].clearPoints` | 300 | Score points when every object in this wave is gone. Half if the earth cut the wave short |
| `waves[].perfectBonus` | 200 | Added on top of `clearPoints` if no life was lost while this wave was on screen |
| `waves[].fromProgress` | 0 (slider 0–1) | The wave becomes available once level progress reaches this. 0 = from the start. This is how waves **unlock** |
| `waves[].untilProgress` | 1 (slider 0–1) | The wave stops being available after this much progress. 1 = it never goes away. This is how old waves are **removed** |
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

Gets the LevelTimer and Score from GameManager, **prewarms** (for each wave's pattern, makes copies until its free list
holds `prewarmPerPattern`; a pattern listed in two waves only gets them once), then starts `RunWaves`, as
long as there's at least one wave. This happens on load, even while Level 1's menu shows, so the prewarm is
done before play begins. `RunWaves` itself waits for the start.

**Test This Pattern (editor only).** Before the prewarm, inside `#if UNITY_EDITOR`: if the pattern builder
asked for a test (`PatternTest.Active`) and its prefab still exists (`PatternTest.Pattern != null`), the wave
list is replaced **in memory** with one wave holding that pattern, with `delayAfter` set to the test's loop
delay, no score points, `order` Sequential and `loop` on. The scene file isn't changed, and the list goes back
to normal when Play stops. See [PatternGizmosEditor.md](PatternGizmosEditor.md). A build never contains this.

### `CreateCopy`, `GetCopy`, `GetFreeList`, `HandleReleased`

- **`CreateCopy`** instantiates the pattern under the spawner, switches its children off, sorts them closest
  to the camera first (`Array.Sort` with a comparison function), adds a `PatternCopy` and calls `Init`,
  subscribes `HandleReleased` to its `OnReleased`, and stores the sorted children in `revealOrder`.
- **`GetCopy`** returns a free copy if the pattern has one, otherwise `CreateCopy`.
- **`GetFreeList`** returns a pattern's free list, making an empty one the first time (`TryGetValue`).
- **`HandleReleased`** scores the wave (see "Wave score" below), then pushes a copy that's free again back
  onto its pattern's list.

### `RunWaves`: going through the list

1. **Wait for the game to start:** `yield return new WaitUntil(() => GameManager.Instance == null ||
   GameManager.Instance.HasStarted)`. `WaitUntil` takes a small function (a lambda) and calls it once a frame,
   continuing the first frame it returns true. That's a **higher-order function**, a method that takes another
   function as input. On Level 1 this holds the waves back during the menu. Everywhere else it's already true,
   and with no GameManager (the pattern-building scene) it never waits.
2. Wait `startDelay` seconds, counted from the start of play, not from the load.
3. For each wave, in the order `GetWaveOrder` gives:
   - If `StopSpawning` is true, stop for good.
   - If the wave isn't **available** (`IsAvailable`: it has no pattern, or the level's progress is outside its
     `fromProgress`–`untilProgress` range), skip it. Progress is read fresh at each wave's turn.
   - Run `SpawnPattern` and **wait for it to finish**. `yield return SpawnPattern(...)` runs another
     coroutine inside this one and continues only when that one is done.
   - In a test only (`#if UNITY_EDITOR`, `PatternTest.Active`): also wait until `AsteroidBehavior.Active` and
     `PowerUp.Active` are both empty, so the whole run has flown past.
   - Wait `delayAfter` seconds.
4. If that pass spawned nothing:
   - If no wave in the whole list has a pattern, that's a setup mistake: log a warning and stop, so an empty
     list can't spin forever.
   - Otherwise some waves exist but none is available at this progress: if `loop` is on, wait
     `NoWaveRetryDelay` (0.5 s) and go round again. This wait is what stops a pass over unavailable waves (which
     takes no time at all) from spinning and freezing Unity.
5. If `loop` is on and `StopSpawning` is still false, go back to step 3.

### Wave phases (`fromProgress` and `untilProgress`)

Each wave can be limited to a stretch of **level progress**, so more waves unlock as the player gets further and
old ones drop out. Progress is `LevelTimer.Progress`: 0 at the start, 1 when the Earth arrives, which is when
spawning stops. A scene with no LevelTimer counts as 0. A wave is available when
`fromProgress ≤ progress ≤ untilProgress` (inclusive at both ends). With the defaults, 0 and 1, every wave is
always available, so existing lists behave as before.

Progress isn't plain seconds: `LevelTimer` advances it by `Time.deltaTime ÷ (levelTime ÷ (1 + Speed × speedTimeFactor))`
each frame, so a faster player gets to later phases sooner. Progress 0.5 means halfway to the Earth.

Example (Sequential, loop on): Ring 0–0.4, Twist 0.2–0.7, Zigzag 0.5–1.

| Progress | What plays |
| --- | --- |
| 0 – 0.2 | Ring, Ring, Ring… (the others are skipped each pass) |
| 0.2 – 0.4 | Ring, Twist, Ring, Twist… |
| 0.4 – 0.5 | Twist only |
| 0.5 – 0.7 | Twist, Zigzag |
| 0.7 – 1 | Zigzag only. At 1 the Earth arrives and spawning stops |

In Random order the available waves shuffle each pass, as before. With `loop` off, each wave still gets one turn, so
one that isn't available at its turn is skipped.

A **gap** in the ranges (say Ring 0–0.3 and Zigzag 0.5–1) leaves an empty sky from 0.3 to 0.5, where the spawner
waits and rechecks every 0.5 s. That's allowed, but easy to do by accident.

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

### Wave score

`SpawnPattern` takes the whole `Wave` (not just its pattern) so it knows the wave's points.

- **At the start** it notes `Score.LivesLostCount` in `livesLostAtStart`. Score only ever counts that up,
  so if it's still the same number when the wave clears, no life was lost during the wave.
- **If `StopSpawning` stops the reveal**, it sets `cutShort`.
- **Just before `EndReveal`**, it stores a `RunningWave` (a small struct: the `Wave`, `livesLostAtStart`,
  `cutShort`) in `runningWaves`, keyed by the copy. It has to be stored first, because `EndReveal` can
  release the copy straight away, which calls `HandleReleased` from inside `EndReveal`. It's a struct
  because it's written once and read once, so storing it makes no garbage.

When the copy is released, `HandleReleased` looks the entry up (`TryGetValue`), removes it, and calls
`AwardWave`, which checks cut short first and perfect second:

| Fully revealed? | Life lost during it? | Calls | Popup |
| --- | --- | --- | --- |
| Yes | No | `AddWaveClear(500, true)` | `PERFECT WAVE +500` |
| Yes | Yes | `AddWaveClear(300, false)` | `WAVE CLEAR +300` |
| No (cut short) | doesn't matter | `AddWaveClear(150, false)` | `WAVE CLEAR +150` |

Waves overlap (the next starts after `delayAfter`, while the last may still be flying), but each one
compares against its own starting count, so a life lost with two waves on screen spoils both.

## Talks to

- **GameManager**: gets the LevelTimer and Score (in `Start`) and the gameplay camera (each pattern), waits for
  `HasStarted` at the top of `RunWaves`, and reads `IsGameOver` through `StopSpawning`.
- **OrbitSettings**: reads `SpawnerPosition` in `Awake`.
- **LevelTimer**: checks `IsComplete`, through `StopSpawning`, to stop spawning once the earth has arrived.
- **AsteroidBehavior** and **PowerUp**: reads `ApproachSpeed` (through `GetApproachSpeed`) to time the
  gaps, and switches them on, which starts their `OnEnable`.
- **PatternCopy**: adds one to each copy, calls `Init`, `BeginReveal`, `ChildRevealed` and `EndReveal`, and
  listens to `OnReleased`.
- **Score**: reads `LivesLostCount` when a wave starts and clears, and calls `AddWaveClear` when a wave's copy
  is released.
- **PatternGizmos / PatternGizmosEditor**: indirectly. The pattern builder lays out the pattern prefabs
  this script spawns. Its **Add to Spawner** button adds a pattern to `waves` (through a `SerializedObject`),
  and its **Test This Pattern** button sets the test that `Start` reads.
- **PatternTest** (editor only): `Start` reads `Active`, `Pattern` and `LoopDelay`, and `RunWaves` reads `Active`.

## Gotchas

- **A wave of only power-ups still scores.** It clears once the power-ups are gone. Set its `clearPoints`
  and `perfectBonus` to 0 if it shouldn't.
- **Moving the spawner in the scene does nothing.** It moves itself to `OrbitSettings`' `spawnerPosition`
  when the game starts. Change the asset instead.
- **No wave with a pattern means no spawning, plus a warning.** If every wave's `pattern` is empty (or points
  at a prefab that was deleted, which shows as Missing), `RunWaves` skips them all. It used to loop again
  without ever waiting, which froze Unity. Now a pass that spawns nothing logs "ObjectSpawner has no wave with
  a pattern assigned, so it is not spawning." in the Console and stops. Fix the wave list and press Play again.
- **Gaps between phases mean an empty sky.** If no wave is available for a stretch of progress, the spawner waits
  (0.5 s at a time) and nothing spawns until the next wave unlocks.
- **Progress follows the player's speed,** so a phase "0.3 to 0.6" is a fixed part of the journey, not a fixed
  number of seconds. A fast player passes it sooner and sees fewer of its waves.
- **A new wave added to the list starts at 0–1.** The two numbers are per wave, so each new wave needs its own
  range. If Unity ever showed `Until Progress` as 0 on an old wave, it would only play at the very start.
- **The Test This Pattern override ignores phases** (its single wave is 0–1).
- **Spacing is only exact at a steady speed.** The wait is calculated from the approach speed when it
  starts. If the player speeds up or slows down during the wait, the gap between asteroids in flight
  ends up smaller or bigger than laid out.
- **Different templates throw off the spacing.** The wait uses the next asteroid's `ApproachSpeed`, but
  the asteroid ahead of it is moving at its own. Asteroids and power-ups on the same template share the
  same `baseApproachSpeed` and `speedMultiplier`, so they match. If two prefabs point at different
  templates, or one has none (its `ApproachSpeed` is 0), they drift closer or further apart.
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
