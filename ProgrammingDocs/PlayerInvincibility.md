# PlayerInvincibility

## Purpose

Makes the player invincible for a set time after picking up an Invincible power-up. While it's active,
every asteroid the player touches is busted (it explodes and counts as destroyed) instead of hitting them.
It keeps the timer and decides when the rainbow should show, including a flicker in the last
`warningTime` seconds so the player knows it's about to end. PlayerShell does the actual drawing.

## Where it lives

- **Attached to:** the player object (the one with `PlayerMovement`), next to `PlayerHealth` and `PlayerShell`.
- **Requires:**
  - `GameManager.Instance`, to switch off on game over or a win. Without one, it still runs, but only ends
    when the timer runs out.
  - `PlayerShell` on the same object, to show the rainbow. Without it, invincibility still works, just with
    nothing on screen.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `warningTime` | 1 | Seconds before the end when the rainbow starts flickering |
| `warningBlinkInterval` | 0.1 | Seconds between each flicker on/off |
| `lockMaxSpeed` | true | While invincible, PlayerMovement holds the player at max speed |

The duration isn't set here. It's `invincibleDuration` in the SpawnableTemplate (default 5), passed in by
the power-up.

## How it works

The whole feature, start to finish:

1. The player touches an Invincible power-up. PowerUp's `ApplyEffect` calls `MakeInvincible`, which finds
   this component and calls `Activate(template.invincibleDuration)`. The power-up then destroys itself,
   which is why the timer has to live here on the player instead.
2. Each frame, `Update` counts the timer down. While `IsInvincible` is true, AsteroidBehavior explodes any
   asteroid that touches the player, with no knockback, speed loss or lost life.
3. In the last `warningTime` seconds, the rainbow flickers.
4. It ends when the timer runs out, or straight away on game over or a win.

### `IsInvincible` and `RainbowShowing`

- **`IsInvincible`**: `timer > 0f`. It's worked out each time it's read, so it can never get out of sync with
  the timer. AsteroidBehavior reads it (in `CanBreak`), and PlayerShell reads it for the hitbox.
- **`RainbowShowing`**: whether the rainbow should be visible right now, including the flicker (`overlayOn`).
  PlayerShell reads it for the look.
- **`LocksMaxSpeed`**: `lockMaxSpeed && IsInvincible`. PlayerMovement reads it at the end of every speed
  update and, while it's true, sets `Speed` to max (unless the level has ended). When invincibility runs out,
  the normal overdrive drain takes over straight away.

They differ during the flicker: you're still invincible on the "off" moments, so the hitbox stays on while
only the look flickers.

### Events

Both are `Action<bool>`, raised with `?.Invoke`:

- **`OnInvincibilityChanged`**: exactly once when invincibility starts (`true`) and once when it ends
  (`false`), however many pickups or frames happen in between.
- **`OnRainbowChanged`**: every time the rainbow flips: at the start, on each flicker flip, and at the end.

PlayerShell listens to both and re-checks everything when either fires.

### `SetOverlay`

Changes whether the rainbow should show, only when the state actually flips.

1. **Guard:** if it's already in the requested state, it returns straight away. `overlayOn` tracks the
   current state.
2. Records the new state and raises `OnRainbowChanged`.

`Update` calls it every frame during the flicker, but the guard means the event only fires when the flicker
flips. With the defaults, that's about 10 times a second instead of 60.

### `Activate`

Sets the timer to the full duration (a second pickup resets it) and turns the rainbow on. It's `public`
so PowerUp can call it.

1. A duration of 0 or less is ignored. Without this, the rainbow would turn on, but the timer would already
   be finished, so nothing would ever turn it off.
2. It remembers whether it was already invincible, sets the timer, and turns the rainbow on.
3. It raises `OnInvincibilityChanged(true)` only if it **wasn't** already invincible, so a re-pickup doesn't
   announce a second start.

| Event | `timer` after |
| --- | --- |
| 1st pickup | 5 |
| 3 seconds later | 2 |
| 2nd pickup | 5 (reset, not 7) |

A pickup during the flicker turns the rainbow back on straight away, and the flicker stops because the
timer is no longer inside `warningTime`.

### `Update`

Checks for game over or a win first (and switches everything off), then counts the timer down, handles the
timer running out, and handles the warning flicker.

1. **Game over or won?** If it's still invincible, it sets `timer` to 0, turns the rainbow off and raises
   `OnInvincibilityChanged(false)`, once. This has to come first: `GameOver()` and `Win()` set
   `Time.timeScale` to 0, so `Time.deltaTime` is 0 and the countdown would never finish, leaving the
   rainbow stuck on.
2. **Not active?** Returns. The timer may be slightly below 0 after it ran out (say −0.01), which counts as
   not active.
3. **Count down.** If that takes the timer to 0 or below, it turns the rainbow off, raises
   `OnInvincibilityChanged(false)`, and returns.
4. **Flicker.** The rainbow is on if there's more than `warningTime` left. Otherwise it uses the same
   formula as PlayerMovement's hit blink: on when `Mathf.FloorToInt(timer / warningBlinkInterval)` is even.

With `warningTime` 1 and `warningBlinkInterval` 0.25:

| `timer` | Rounded down (÷ 0.25) | Rainbow |
| --- | --- | --- |
| 0.99 to 0.75 | 3 | off |
| 0.74 to 0.50 | 2 | on |
| 0.49 to 0.25 | 1 | off |
| 0.24 to 0.01 | 0 | on |
| 0 or less | — | off (ended) |

### History: why it doesn't draw anything itself

It used to add the rainbow material to the ship's own renderers, by swapping their material arrays. That
clashed with the shield, which needed the same renderers. Now one script, PlayerShell, owns all the overlay
visuals, and this script only announces state. The rainbow shader is documented in
[PlayerShell.md](PlayerShell.md).

## Talks to

- **PowerUp**: calls `Activate(duration)` from `MakeInvincible` (Invincible type).
- **SpawnableTemplate**: not directly; PowerUp reads `invincibleDuration` from it and passes it in.
- **PlayerShell**: listens to both events, and reads `IsInvincible` (hitbox) and `RainbowShowing` (look).
- **AsteroidBehavior**: reads `IsInvincible` in its `CanBreak`. While it's true, any asteroid explodes on
  contact, and destructible outlines turn green.
- **OrbitRing**: not directly; it reads `CanBreak`, so every danger arc turns green while invincible.
- **PlayerMovement**: reads `LocksMaxSpeed` every frame (it finds this component in `Awake`) to hold the
  player at max speed.
- **GameManager**: reads `IsGameOver` and `HasWon` every frame. GameManager also keeps a reference to this
  component (auto-filled from the player), which is how asteroids find it.

## Gotchas

- **GameManager needs `player` assigned** (or `playerInvincibility` filled in). Asteroids get this
  component through GameManager, so without it, invincibility shows the rainbow but doesn't bust asteroids
  or turn them green.
- **Without PlayerShell there's no visual,** but busting still works.
- **Removed Inspector fields.** `meshTransform` and `rainbowMaterial` used to be set here. The rainbow
  material is now assigned on PlayerShell.
