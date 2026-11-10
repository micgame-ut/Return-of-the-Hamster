# HamsterAnimationDriver

## Purpose

Drives the hamster's Animator from the player's speed, and plays the hit reaction when a life is lost.
The hamster idles when the player is slow, scurries faster as `Speed` rises, and winds down gently when
the player lets go. While Level 1's menu is showing, he floats with his arms and legs out instead, and
breaks into the run when the level starts.

## Where it lives

- **Attached to:** the GameObject that carries the hamster's Animator: the `Apollo_Rigged` model under `Apollo`
  in `hamter.prefab`, and the same object in `Twin Copy.prefab`
- **Requires:**
  - An **Animator** on the same GameObject, using the `HamsterAnimator` controller. The controller needs
    four parameters with exactly these names: `Speed01` (Float), `RunSpeed` (Float), `Hit` (Trigger) and
    `InMenu` (Bool)
  - A `PlayerHealth` somewhere above it in the hierarchy (optional: twin copies don't have one)
  - A `GameManager` in the scene (optional: without one, `InMenu` stays false)
- **The animations** come from `Apollo_Rigged.fbx`: **Scurry**, **Idle**, **Hit** and **Float**

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `minRate` | 0.8 | Playback rate of the Locomotion state when the player is stopped (`Speed01` = 0). 1 = the clip's normal speed |
| `maxRate` | 2 | Playback rate at the player's top speed (`Speed01` = 1). 2 = twice the clip's normal speed |
| `speedUpDampTime` | 0.05 | Seconds the Animator takes to catch up when the player speeds up. 0 = instant, bigger = softer |
| `slowDownDampTime` | 0.6 | Seconds the Animator takes to wind down when the player slows. 0 = instant, bigger = a longer, gentler wind-down |

## How it works

### The controller (`HamsterAnimator`)

The script only sets parameters. What they do is decided in the controller:

- **Locomotion** (the default state) is a 1D Blend Tree on `Speed01`: **Idle** at 0, **Scurry** at 0.15. Between
  the two the poses blend. Its **Multiplier** is the `RunSpeed` parameter, so `RunSpeed` scales the playback rate
  of the whole state.
- **Hit** is its own state. **Any State → Hit** fires on the `Hit` trigger (no exit time, so a hit interrupts
  whatever is playing), and Hit goes back to Locomotion when it ends.
- **Float** is the menu pose: arms and legs out, a slow 5-second loop of small limb drift and a gentle bob.
  **Any State → Float** fires when `InMenu` is true (no blend, and *Can Transition To Self* is off so it doesn't
  restart every frame). **Float → Locomotion** fires when `InMenu` is false, with a 0.4 s blend so the limbs
  ease from the float into the run. Locomotion stays the default state, so a level without a menu, or a restart,
  never shows Float.

### `Awake`

Finds the things the script needs, once: the Animator on this GameObject (`GetComponent`), the `PlayerHealth`
above it (`GetComponentInParent`) and the `GameManager` (`GameManager.Instance`). On a twin copy there's no
`PlayerHealth`, so that one stays `null`, and the script copes with that.

It also sets `InMenu` to true if there is a `GameManager` and the game hasn't started yet. `GameManager`'s
`Awake` runs first (`[DefaultExecutionOrder(-100)]`), so `IsFlying` (`HasStarted` or the tutorial) is already decided. On a restart or a level
without a menu it is already true, so `InMenu` is false from the first frame and Float never shows.

### `OnEnable` and `OnDisable`

`OnEnable` subscribes `HandleLifeLost` to `PlayerHealth`'s `OnLifeLost` event, but only if a `PlayerHealth` was
found, and `HandleGameStarted` to both of `GameManager`'s `OnGameStarted` and `OnTutorialStarted`, but only if a
`GameManager` was found.
`OnDisable` unsubscribes both again, so an event never ends up with the same method twice, or with a method
on a destroyed object. `Awake` runs before `OnEnable`, so both references are already filled in by then.

### `HandleGameStarted`

Sets `InMenu` to false. `OnGameStarted` fires from `GameManager.StartGame()`, and `OnTutorialStarted` from
`StartTutorial()`. `MenuTransition` calls one of them at the end of its transition (3 seconds after Start is pressed, with the scene's values). The hamster floats while he
glides into position, then blends into the run as control begins.

### `HandleLifeLost`

Sets the `Hit` **trigger** on the Animator. A trigger fires once and resets itself as soon as a transition uses
it, so nothing needs to clear it afterwards. `OnLifeLost` only fires when a life is actually lost, so smashed
asteroids and hits during invulnerability don't play it.

### `Update`

Every frame it reads `PlayerMovement.Speed01` (the player's speed, 0 to 1) and sets two Float parameters:

- **`Speed01`**: the player's speed as it is. The Blend Tree uses it to pick Idle or Scurry.
- **`RunSpeed`**: `Lerp(minRate, maxRate, Speed01)`, the playback rate.

| `Speed01` | `RunSpeed` (min 0.8, max 2) |
| --- | --- |
| 0 | 0.8 |
| 0.25 | 1.1 |
| 0.5 | 1.4 |
| 1 | 2.0 |

**Smoothing.** Both are set with the damped version of `SetFloat`, which moves the parameter toward the target
over a set time instead of jumping. The time depends on direction: the script compares the player's speed with the
Animator's *current* `Speed01` value (`GetFloat`). If the player is above it, he is speeding up and
`speedUpDampTime` is used (short, so he snaps into the run). Otherwise `slowDownDampTime` is used (long, so he
winds down gently). One comparison picks the time for both parameters, since `RunSpeed` always rises and falls
together with `Speed01`.

## Talks to

- **PlayerMovement**: reads the static `Speed01` every frame (no reference needed)
- **PlayerHealth**: subscribes to `OnLifeLost`
- **GameManager**: reads `HasStarted` in `Awake` and subscribes to `OnGameStarted`
- **Animator** and the **`HamsterAnimator`** controller: sets `Speed01` and `RunSpeed`, reads `Speed01`, sets
  the `Hit` trigger and sets the `InMenu` bool

## Gotchas

- **The parameter names must match the controller exactly** (`Speed01`, `RunSpeed`, `Hit`, `InMenu`, case-sensitive). The
  names are text, so a mismatch doesn't fail to compile or throw an error. Unity only prints
  "Parameter ... does not exist" in the Console, and the animation silently doesn't play.
- **The script must be on the same GameObject as the Animator.** `GetComponent` only looks at its own object,
  so otherwise `animator` is `null` and `Update` throws a `NullReferenceException`.
- **`RunSpeed` scales Idle too.** It is the multiplier on the whole Locomotion state, so at a standstill Idle plays
  at `minRate`. Set `minRate` to 1 for Idle at its normal speed.
- **The wind-down can't be longer than the game's own decay.** `Speed` already falls by `speedDecay` per second,
  and `slowDownDampTime` smooths on top of that. It mostly softens abrupt drops, like a hit.
- **Twin copies don't play Hit.** They have no `PlayerHealth`, so nothing is subscribed. They still idle and
  scurry with the player's speed.
- **Hit only plays when a life is lost.** Hits during invulnerability and smashes (fast enough to break the
  asteroid) never reach `LoseLife`.
- **Time is frozen on the last hit.** GameManager sets `Time.timeScale` to 0 on game over, and the Animator
  runs on scaled time, so the Hit animation freezes with everything else on the final life.
- **Renaming `Hit`, `Speed01`, `RunSpeed` or `InMenu` in the controller** means changing the matching string in
  the script as well.
- **The Float pose follows `HamsterMenuFloat`'s spin.** The Animator only poses the limbs; `HamsterMenuFloat`
  moves and rotates the whole hamster, and `MenuTransition` glides him into place. The two don't interfere.
- **Don't switch this script off on the menu hamster.** It's what turns `InMenu` back to false when the level
  starts. With it off, the hamster would stay in Float forever.
- **Float is baked into `Apollo_Rigged.fbx`.** Changing the pose or timing means editing the `Float` action in
  Blender and re-exporting the FBX (with the NLA-strip export, so the clip names stay plain).
