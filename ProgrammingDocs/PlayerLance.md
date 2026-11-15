# PlayerLance

## Purpose

The shots behind the Lance power-up. Collecting the pickup gives the player one strong shot that doesn't run out until it is fired.
While a shot is held, every click fires a pink railgun beam straight ahead from the player and from each twin copy, in place of the
dodge. Every asteroid in the beam's lane is removed, and the shot pays one flat total as a single popup. When the last shot is used,
the ring goes back to blue and the dash goes on its normal cooldown. It runs on the player because the shots outlast the pickup,
like PlayerJackpot and PlayerSlim.

## Where it lives

- **Attached to:** the player's root (`hamter`, in `LevelCore.prefab`).
- **Found by:** `GameManager` (the `playerLance` field, or `GetComponent` on the player in `Awake`), exposed as `GameManager.PlayerLance`.
  PowerUp, BoostRing and FreeBoostPips read it from there.
- **Requires:** `PlayerMovement` and (optionally) `PlayerTwins` on the same object, and the level's `Score` from `GameManager`.

## Inspector settings

| Field | Default | What it does |
| --- | --- | --- |
| `template` | the SpawnableTemplate asset | Holds every number (shots, cooldown, lane width, range, points, beam look). Assign the same one the asteroids use |
| `beamPrefab` | `Lance Beam` | A LineRenderer for the glow, with a child LineRenderer for the bright core. One beam is made for each body that fires, then reused |

The numbers are the `lance*` fields on the SpawnableTemplate (see [SpawnableTemplate.md](SpawnableTemplate.md)).

## How it works

- **`IsActive`** is true while at least one shot is held. `ShotsLeft` is the count. `ShotCooldown` is `lanceCooldown`, the seconds before the
  next shot when more are left. `OnShotsChanged` is raised whenever the count changes, so the small rings can update.
- **`Activate()`** (Lance pickup): calls `PlayerMovement.ReadyBoost()` so the click is ready at once, then holds `Max(shots, lanceShots)`,
  so a second Lance never gives fewer shots than you already have.
- **`AddShots(count)`** (a Boost Recharge picked up while Lance is active): readies the click and adds `count` shots, up to `lanceMaxShots` (3).
  It gives no free boosts and doesn't reset the dash.
- **`TryFire()`**: called by `PlayerMovement` on a click. Returns false with no shot. Otherwise fires from the player and from every twin
  copy (`FireFrom`), shakes the camera once, pays `removed × lancePoints` as one popup ("N HIT"), and uses one shot.
- **`FireFrom(origin)`**: draws a beam, then collects every revealed asteroid that is ahead of `origin` (no more than 0.5 behind, no
  further than `lanceRange`), isn't phased, and is within `lanceWidth / 2 + HitRadius` of `origin` sideways. Each one gets `Blast()`, and only the
  ones really removed are counted.
- **Beams:** `ShowBeam` takes a finished beam from the pool or makes one. `Update` thins each one over `lanceBeamTime` and fades it, then switches it off.
  The glow is `lanceBeamColor`, the core a lighter version of it. Colours are set with a property block, so no material copies are made.
- **Level end:** on game over or win, the shots are set to 0.

## Talks to

- **PlayerMovement**: asks `TryFire` on each click while `IsActive`; `ReadyBoost` on `Activate` and `AddShots`.
- **PowerUp**: `Activate`, `AddShots`, and `IsActive` / `ShotsLeft` for the Random roll's wasted check.
- **AsteroidBehavior**: reads `Active`, `HitRadius`, `IsPhased`; calls `Blast()`.
- **BoostRing / FreeBoostPips**: read `IsActive`, `ShotsLeft` and `OnShotsChanged` to turn the rings pink and show the extra shots.
- **Score**, **CameraShake**, **PlayerTwins**, **GameManager**.

## Gotchas

- **Lane width is sideways distance, not angle.** The default 1.4 is about a 43° band on the orbit (radius 3). A rock must be within 0.7 plus its own radius of the player's position.
- **The center ring is the first shot, the two small rings are the extras.** With 3 shots, both small rings show.
- **Only revealed rocks are hit.** Rocks of a pattern that haven't been revealed yet aren't on screen, so the beam can't reach them.
- **Twin copies fire too,** so a pair of twins at different spots can score for two lanes with one click.
- **`Pattern_Lance`** is the test pattern: Lance, then a Recharge, three rocks in one lane, then single rocks for shots 2 and 3, then a last rock to dodge once the ring is blue again.
