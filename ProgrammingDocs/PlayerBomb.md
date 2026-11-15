# PlayerBomb

## Purpose

The sweep behind the Bomb power-up. When the pickup is collected, it removes every asteroid that is revealed on screen, nearest
first over a short time, each with its normal explosion, and pays one flat total as a single popup. The sweep takes time and the
pickup is switched off the moment it's collected, so it has to run on the player, like PlayerJackpot and PlayerSlim.

## Where it lives

- **Attached to:** the player's root (`hamter`, in `LevelCore.prefab`), so the levels and the `PatternTest` scene have it.
- **Found by:** `GameManager` (the `playerBomb` field, or `GetComponent` on the player in `Awake`), exposed as `GameManager.PlayerBomb`.
  PowerUp reads it from there.
- **Requires:** the level's `Score` (from `GameManager.Score`, found in `Awake`). The numbers come from the SpawnableTemplate, passed in by PowerUp.

## Inspector settings

None. Every value comes from `Detonate`.

## How it works

### `Detonate(pointsPerRock, sweepTime, shakeDuration, shakeMagnitude)`

Called by PowerUp. If no asteroid is revealed (`AsteroidBehavior.Active` is empty) it does nothing: no shake, no empty popup. Otherwise it
shakes the camera once (skipped if the duration is 0, like the pickup shake, so a zero setting can't cut a running shake short), copies the
live `Active` list (`new List<AsteroidBehavior>(Active)`) and starts `Sweep` on that copy. A fixed copy means rocks revealed later aren't
included, and rocks that leave while the sweep runs can't break it. Each detonation has its own copy and coroutine, so two overlapping
bombs just run side by side.

### `Sweep` (a coroutine)

1. Measures each target's distance from the player in depth (z), and the nearest and farthest of them. Rocks in the same ring have the
   same distance, so they go off together.
2. Works out when each goes off, once, at the start: `due = sweepTime × (distance − nearest) ÷ (farthest − nearest)`, so **the nearest rock goes
   off at once and the farthest after `sweepTime`** (0 for everything if they're all at the same distance). Then a safety rule: `due` is
   capped at `ArriveShare` (0.5) of the rock's own travel time to the player, `distance ÷ ApproachSpeed`, so **no rock goes off later than half
   the time it needs to reach the player**, and a fast player can't hit one first, whatever the layout or speed (a rock with no approach speed
   isn't capped). Fixed in advance, so the rocks flying toward the player during the sweep don't reshuffle the order.
3. Loops: every frame it scans the targets still waiting, and any whose time has come gets `Blast()` (counting the `true`s) and is marked done.
   It scans all of them, not just the next in line, because the safety cap can make a nearer, faster rock due later than a farther, slower
   one. Then it waits a frame (`yield return null`) and adds `Time.deltaTime`, so a frozen game pauses the sweep.
4. At the end, if anything was removed and `pointsPerRock` is above 0, it adds `removed × pointsPerRock` through `Score.AddBonusPoints` with
   the label "N DESTROYED" and `ScoreSource.Asteroids`. Only rocks that were really removed are paid. `Score` ignores gains after the level ends.

Measured with the first version (sweep 0.35 s, scaled by the farthest distance), 54 rocks in three rings, speed 60: 54 → 36 → 18 → 0 over about
0.2 to 0.37 s after the pickup; +2700 (54 × 50) each time. At top speed (100, 50 units per second) those rocks reach you in 0.24 to 0.32 s,
and that version fired them at 0.26 to 0.35 s, so they hit first. Current version (sweep 0.2 s, nearest at 0, the half-arrival cap), speed 100,
four loops: the three rings went off at about 0, 0.1 and 0.2 s, and no lives were lost.

## Talks to

- **PowerUp**: calls `Detonate`.
- **AsteroidBehavior**: calls `Blast()` on each target; reads the static `Active` list.
- **Score**: `AddBonusPoints` for the one summary popup.
- **CameraShake**: `Shake` once.
- **GameManager**: exposes it, and provides `Score`.

## Gotchas

- **Only revealed rocks are removed.** A pattern reveals its later steps in order (each after its depth gap ÷ approach speed), so rocks far
  behind the pickup in the same pattern aren't on screen yet when it's collected. `Pattern_Bomb` puts its rings only 10 to 14 units behind
  the pickup for this reason.
- **The half-arrival cap is estimated.** It uses depth distance ÷ the rock's `ApproachSpeed` as its travel time. That's exact for straight
  Linear rocks; rocks with their own movement (Homing, Spiral, Stop-N-Go, Wave) may reach you a little sooner or later, but they still go off
  within a fraction of that time.
- **A slow `bombSweepTime` can't make rocks hit you.** The cap trims it per rock, so raising `bombSweepTime` just makes the far rocks go off
  at their half-arrival time instead.
- **Frost patches stay.** A Frost comet is removed but the patches it already dropped run out on their own timer.
- **Pickups and power-ups aren't touched,** only asteroids.
- **Wave bonuses.** Clearing a wave this way still counts as clearing it (the spawner waits for `Active` to be empty), so its normal
  clear bonus is awarded.
- **No bomb sound yet,** and no `OnSmashed`, so the normal smash sound doesn't play 54 times.
