# PlayerHealth

## Purpose

Tracks how many lives the player has. Health power-ups give one back, up to the max. Each counted asteroid hit takes one away, and when none are left
it tells GameManager the game is over. It only handles lives; what "game over" means is up to
GameManager and the scripts that check it.

## Where it lives

- **Attached to:** the player object (the one with `PlayerMovement`). Components that belong to the
  player go on the player; GameManager holds level-wide references and level operations.
- **Requires:**
  - `GameManager.Instance`, to check whether the game has already ended and to call `GameOver()`.
    Without one, `LoseLife` does nothing.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `maxLives` | 3 | How many lives the player starts with, which is the number of counted hits that ends the game |

## How it works

### `maxLives`, `MaxLives` and `CurrentLives`

- **`maxLives`** is the private serialized field that's editable in the Inspector. It's where the number
  is actually stored.
- **`MaxLives`** is a read-only property that hands back `maxLives`, so other scripts can read it but
  not change it.
- **`CurrentLives`** is the number of lives the player has right now in the game. Anyone can read it,
  but only `PlayerHealth` can assign it (`private set`), so `LoseLife` and `GainLife` are the only ways it
  changes.

### `OnLivesChanged`

An **event** (`Action<int>`) that other scripts subscribe to with `+=` (and leave with `-=`). `LoseLife`
and `GainLife` raise it with the new `CurrentLives`, using `?.Invoke` so nothing happens when no one is
listening. This lets PlayerShell and DangerWarning react only when lives change, instead of checking every
frame.

- `LoseLife` raises it **before** calling `GameOver()`, so the drop to 0 is announced too.
- `GainLife` raises it even at max lives, when nothing changed. Listeners handle a repeat safely.
- `Awake` doesn't raise it. Listeners read `CurrentLives` themselves in `Start`, after every `Awake` has run.

### `OnLifeLost`

A second event (`Action`, no value) raised by `LoseLife` only, right after `OnLivesChanged`. It says exactly
"a life was just lost", so listeners don't have to compare the new count with the old one to tell a loss
from a `GainLife`. Score listens to end the combo and to count lives lost for perfect waves. Because it
comes after `LoseLife`'s guard, it never fires after game over or a win. On the last life it fires before
`GameOver()`.

These are instance members, not static, so a reloaded scene gets a new `PlayerHealth` that starts over
at `maxLives` without any manual reset.

### `Awake`

Runs as soon as the component is loaded, before any `Start`. It sets `CurrentLives` to `maxLives`.
Until then `CurrentLives` is `0`, the default for an `int`.

### `LoseLife`

Called by `AsteroidBehavior` when an asteroid hits the player and the hit counts (`TakeHit` returned
`true`, meaning the player wasn't invulnerable). Hits during invulnerability, and destructible asteroids
broken at speed, never call it.

1. Assigns `GameManager.Instance` to a local variable, `gm`.
2. If `gm` is null, the game is over, or the player has won, it exits the method immediately. The null
   check comes first so `||` stops before reading anything from a null reference.
3. Otherwise it takes one life away from `CurrentLives`, raises `OnLivesChanged` with the new count, then
   raises `OnLifeLost`.
4. If `CurrentLives` is 0 or less, it calls `gm.GameOver()`. `<= 0` rather than `== 0` means game over
   still fires if lives ever went below 0.

| Hit | `CurrentLives` after | Calls `GameOver()`? |
| --- | --- | --- |
| 1st | 2 | no |
| 2nd | 1 | no |
| 3rd | 0 | yes |
| 4th | 0 (the method exits early) | no |

### `GainLife`

Called by a Health power-up when it's picked up. It has the same guard as `LoseLife` (no GameManager, game
over, or won means it does nothing), then sets `CurrentLives` to `Mathf.Min(CurrentLives + 1, maxLives)`.
`Mathf.Min` returns the smaller of the two, so lives never go past the max, and raises `OnLivesChanged` with the result.

| Lives before | After |
| --- | --- |
| 1 | 2 |
| 2 | 3 |
| 3 | 3 (already at max; the pickup is still used up) |

## Talks to

- **GameManager**: reads `IsGameOver` and `HasWon`, and calls `GameOver()` when lives run out.
  GameManager also keeps a `PlayerHealth` reference, filled in from the player object if unassigned.
- **AsteroidBehavior**: calls `LoseLife()` on a counted hit, using GameManager's `PlayerHealth`.
- **PowerUp**: calls `GainLife()` from its Health effect, found the same way.
- **PlayerShell**: subscribes to `OnLivesChanged`, and reads `CurrentLives` and `MaxLives` to show the shield
  at full health.
- **DangerWarning**: subscribes to `OnLivesChanged` (through GameManager's reference) to show the warning at 1 life.
- **Score**: subscribes to `OnLifeLost` (through GameManager's reference) to end the combo and count lives lost.

## Gotchas

- It must be on the **same object as `PlayerMovement`** (or assigned on GameManager). GameManager only
  looks there, and if it isn't found, hits still knock the player back but never cost a life, with no error.
- Setting `maxLives` to 0 starts the player with no lives, but game over only happens on the first hit
  (which takes lives to -1).
- After the level is won, hits still knock the player back but don't cost lives.
