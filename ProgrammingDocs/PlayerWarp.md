# PlayerWarp

## Purpose

The Warp power-up on the player. When a Warp is picked up the ship pops out (shrinks away), the whole game runs faster for a set number of
seconds of game time while the player is gone and untouchable, then the ship pops back in. It is not a skip: the level, the spawner and every
rock already on screen run fast together, so every wave still spawns and flies past and its clear bonus is still paid.

## Where it lives

- **Attached to:** the player object (`hamter` in `LevelCore.prefab`), next to PlayerMovement, PlayerSlim and PlayerTwins.
- **Requires:** nothing to be assigned. `Awake` finds `PlayerSlim` and `PlayerTwins` on the same object with `GetComponent` (both are optional:
  without them it just doesn't multiply Slim's size or scale twin copies). `GameManager` finds this script for the others (`GameManager.PlayerWarp`).

## Inspector settings

None on this script. The values come from the SpawnableTemplate asset, through `PowerUp.StartWarp`:

| Template field | Default | What it changes |
| --- | --- | --- |
| `warpSeconds` | 10 | Seconds of game time skipped during the hold |
| `warpOutTime` | 0.5 | Seconds to pop out (normal speed) |
| `warpHoldTime` | 1 | Real seconds the player stays gone. The game runs `warpSeconds ÷ warpHoldTime` times faster (10×) |
| `warpInTime` | 0.5 | Seconds to pop back in (normal speed) |
| `warpGraceTime` | 1.5 | Seconds of protection after the pop-in (normal speed). 0 = none |
| `phasedMaterial` | Asteroid Phase | The look the ship wears during the grace (the Phase asteroids' hologram). Empty = protection with no look |
| `warpChance` | 20 | The Mystery roll's weight for Warp |

## How it works

There are two properties other scripts read. `IsWarping` is true in `Out`, `Hold` and `In`: input is locked and pickups are ignored. `IsProtected` is
true in those three and also in `Grace`, the phase after `In`: rocks can't hurt the ship, but the player has control and can pick things up. Both are
false when the phase is `None`.

### `Activate(seconds, outSeconds, holdRealTime, inSeconds, graceSeconds, look)`

Called by PowerUp. It does nothing if a Warp is already running (`IsWarping`) or `seconds` is 0 or less. Otherwise it stores the values, works out the
hold's time scale as `seconds ÷ max(holdRealTime, MinHoldTime)` (`MinHoldTime` is 0.1, so a hold time of 0 can't ask for an endless scale),
takes the phase look off if a grace was still running (a second Warp picked up during the grace), starts the `Out` phase and raises
`OnWarpChanged(true)` (the ship is popping out).

### `Update`: the phases

Each frame the phase timer `phaseTime` grows by `Time.deltaTime` (game time, so during the hold it is the scaled time). When the timer reaches the
phase's length, `StartPhase` moves to the next one: `Out` → `Hold` → `In` → `Grace` → `None` (`In` goes straight to `None` when the grace time is 0). The hold's length is `holdSeconds` of **game time**, so a slow
frame can't make the skip shorter than `warpSeconds` (it can overshoot by up to one frame, about 1.7% at 60 fps).

`StartPhase` is also where the time scale is set: `holdTimeScale` for the `Hold` phase, 1 for every other phase including the end. It also turns the
phase look on while the phase is `Grace` and off otherwise. When the next phase is `In` it raises `OnWarpChanged(false)` (the ship is coming back).
`OnWarpChanged(bool)` is `true` for the pop out and `false` for the pop in; it is not raised for the other phases.

### `AddBody`, `SetLook`: the phase look

During `Grace` the whole ship wears the phase material (the Phase asteroids' hologram, passed in from `template.phasedMaterial`). `SetLook(true)` goes
through the player and each twin copy with `AddBody`, which collects every `MeshRenderer` and `SkinnedMeshRenderer` under it (so the hamster and the
shell; the flame, trails and particles are left alone) and saves each one's `sharedMaterials`. The boost ring and the two free-boost rings keep their
own glow: `AddBody` finds the body's `BoostRing`, and skips every renderer that uses the same material as it (all three rings share `Ring Glow`). Each renderer then gets an array of the same length with
every slot set to the phase material. `SetLook(false)` puts the saved arrays back (skipping any renderer that was destroyed meanwhile) and clears the
lists. It uses `sharedMaterials`, so no `Material` is created and nothing needs destroying. It is also called when the level ends mid-grace and when a
new Warp starts during one.

Worked example (`warpSeconds` 10, hold 1, out 0.5, in 0.5, `levelTime` 60, Speed 0):

| Real time | Phase | `Time.timeScale` | What happens |
| --- | --- | --- | --- |
| 0 to 0.5 s | Out | 1 | The ship pops out |
| 0.5 to 1.5 s | Hold | 10 | 10 s of game time pass: `Progress` goes 0.300 → 0.467 and +10 time points. A wave with `delayAfter` 2 waits 0.2 s |
| 1.5 to 2 s | In | 1 | The ship pops back in |

At Speed 10 with `speedTimeFactor` 0.1 the level runs twice as fast, so the same 10 s of game time covers a third of the level and pays about
20 time points. `Speed` is frozen during a Warp (PlayerMovement is locked), so it stays whatever it was.

### `Pop`, `LateUpdate`: the pop in and out

`Pop` is a public property worked out fresh from the phase (`ExhaustFlame` reads it): 1 = full size, 0 = gone. It eases with `Mathf.SmoothStep` over `outTime` (1 → 0) and `inTime` (0 → 1),
is 0 during the hold, and is 1 when there is no Warp. A pop time of 0 jumps straight to the end.

`LateUpdate` sets the size of the player **and each twin copy** to `slim.Scale × max(Pop, 0.001)`. It runs after every `Update`, so it wins over
PlayerSlim's own scaling while still including Slim's size. `MinScale` (0.001) stops a collider being scaled to exactly 0. The `scaled` flag
makes one more `LateUpdate` run after the Warp ends, so the ship goes back to full size (times Slim's scale) and then the method stops doing anything.

### `OnEnable`, `OnDisable`, `HandleLevelEnded`

Subscribes to `GameManager.OnGameOver` and `OnWin`. If the level ends during a Warp, `HandleLevelEnded` sets the phase to `None` **without touching
`Time.timeScale`**, because GameManager has just set it to 0 for the freeze. The ship's size is still put back by the next `LateUpdate`.

### What the other scripts do while `IsWarping`

- **PlayerMovement**: `InputLocked` is true, so no movement, no boosts or Lance shots, and `Speed`, the boost cooldown and the hit-invulnerability timer pause.
- **AsteroidBehavior**: ignores contact with the player and twin copies, and never counts a close call.
- **PowerUp**: ignores contact, so nothing can be picked up.
- **LevelTimer, Score, ObjectSpawner, asteroids and power-ups**: unchanged. They run on `Time.deltaTime` or `WaitForSeconds`, so they simply run faster.
- **ScorePopups, CameraShake, music, end screens**: use unscaled time, so they run at normal speed.

## Talks to

- **PowerUp**: calls `Activate`; reads `IsWarping` to ignore pickups. Also `IsWasted` for the Mystery roll (no PlayerWarp = wasted).
- **PlayerMovement**: reads `IsWarping` in `InputLocked`.
- **AsteroidBehavior**: reads `IsProtected` (contacts and close calls), including the grace after the Warp.
- **GameManager**: holds the reference (`PlayerWarp` property); this script reads `OnWin` and `OnGameOver` from it.
- **GameSFX**: subscribes to `OnWarpChanged` for the `warpOut` and `warpIn` sounds, and reads `IsWarping` to keep the low-life loop quiet during a warp.
- **PlayerSlim**: reads `Scale` so Slim's size is kept.
- **PlayerTwins**: reads `Copies` to scale each copy.
- **ExhaustFlame**: reads `Pop` to shrink the flame with the ship (the flame's lines are world space, so scaling the ship alone leaves a dot).

## Gotchas

- **Other timers run fast too.** Anything on `Time.deltaTime` runs 10× during the hold: score power-up timers, the combo timer, Invincible, Slim,
  Jackpot. A 10-second score multiplier is used up by one Warp. This was a decision (the skipped time counts for everything), not an oversight.
- **Only one Warp at a time.** A second pickup can't happen while one runs (contacts are ignored), and `Activate` refuses anyway. During the grace a
  pickup is allowed again, and it starts a new Warp (the phase look comes off first).
- **A twinned player loses nothing in the grace.** `AsteroidBehavior` ignores the touch before it reaches `LoseBody`, so no body is lost.
- **Invincible or a new Twin during the grace.** `PlayerShell` rewrites the shell's material when Invincible starts or ends, and a Twin copy made
  during the grace doesn't get the look. If the shell changes mid-grace, the restore puts back the shell material saved at the start of the grace, so it
  can be wrong until the shell next changes.
- **Near the end of the level** a Warp can carry `Progress` past 1: the earth arrives, the remaining rocks fly past, and the level is won as usual.
- **`TuningOverlay` and `EndScreen` also set `Time.timeScale`.** Opening the tuning overlay during a Warp would put the scale back to 1 (a dev tool, left alone).
- **Physics runs more steps per frame at 10×.** Unity caps one step at the Maximum Allowed Timestep (0.333 s by default), so very low frame rates would
  make the hold take longer in real time. Game time still adds up to `warpSeconds`.
- **Things that follow game time may flash during the hold** (for example danger warnings and the orbit ring's arcs). Check in Play mode.
- **No `Time.timeScale` restore in `OnDisable`.** Every way out of a level already resets it (`GameManager.Awake`, `EndScreen`), and a restore could
  overwrite the 0 set on a win.
