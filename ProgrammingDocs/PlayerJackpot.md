# PlayerJackpot

## Purpose

The timer behind the Jackpot power-up. While it's running, every asteroid counts as a smashable Bonus rock (see
[AsteroidBehavior.md](AsteroidBehavior.md), `Kind`), and it says when the asteroids should look gold. It works like
PlayerInvincibility: the pickup disappears the moment it's collected, so the timer has to live on the player.

## Where it lives

- **Attached to:** the player's root (`hamter`, in `LevelCore.prefab`), so the levels and the `PatternTest` scene have it.
- **Found by:** `GameManager` (the `playerJackpot` field, or `GetComponent` on the player in `Awake`), which exposes it as
  `GameManager.PlayerJackpot`. Asteroids and PowerUp read it from there.
- **Requires:** nothing else. The duration, flicker time, color and smash speed come from the SpawnableTemplate, passed in
  by PowerUp (or read by AsteroidBehavior).

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `blinkInterval` | 0.1 | Seconds between each flicker of the gold at the end of Jackpot |

## How it works

### `IsActive` and `ColorShowing`

- `IsActive` is true while the timer is above 0. It drives the rules: asteroids ask it for their effective kind and smash speed.
- `ColorShowing` is `IsActive && showing`. It drives only the gold look (the material swap): it is true all through Jackpot, then flickers off and on
  during the last `warning` seconds. Rocks stay smashable through the flicker.

### `Activate(duration, warningTime)`

Called by PowerUp. A duration of 0 or less does nothing. It sets `timer` to the full duration (a second pickup **restarts** it, it
doesn't add), stores `warningTime`, starts with the gold showing, and raises `OnJackpotChanged(true)` only if Jackpot wasn't
already running. The warning time is passed in so this script needs no link to the template.

### `Update`

Does nothing while the timer is 0. Otherwise it counts down by `Time.deltaTime`; at 0 it stops showing and raises
`OnJackpotChanged(false)`. While more than `warning` seconds remain, `showing` is true; in the last `warning` seconds it is
`FloorToInt(timer / blinkInterval) % 2 == 0`, so it flips every `blinkInterval` (the same formula as PlayerInvincibility's
rainbow). Example (warning 1, blink 0.1): at 0.95 s left `FloorToInt(9.5)` is 9, odd, off; at 0.85 s it is 8, even, on.

### `OnEnable` / `OnDisable` and `HandleLevelEnded`

Subscribes to GameManager's `OnGameOver` and `OnWin`, and unsubscribes in `OnDisable` (events outlive the object). When the level
ends, a running Jackpot is cut off at once (timer 0, gold off, `OnJackpotChanged(false)`): game over and win freeze time, so
`Update` would never finish it, and the gold would stay on the frozen rocks.

## Talks to

- **PowerUp**: calls `Activate`.
- **AsteroidBehavior**: reads `IsActive` and `ColorShowing`.
- **GameManager**: exposes it, and raises the game-over and win events it listens to.

## Gotchas

- **`OnJackpotChanged` has no listener yet.** A sound or HUD indicator can subscribe later without changing this script.
- **No HUD timer.** The gold rocks, and their flicker, are the only cue.
- **The player needs the component.** Without a `PlayerJackpot` on the player, a Jackpot pickup does nothing.
