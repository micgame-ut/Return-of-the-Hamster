# GameManager

## Purpose

One central place for the scene's important objects: the player, the gameplay camera, the earth, the
earth camera, the LevelTimer, and the player's PlayerHealth. Instead of every script having its own
Inspector fields for these (and each one needing to be dragged in by hand), scripts ask the GameManager
with `GameManager.Instance`.

It also holds the level's end state: whether the game is over or the level is won. It doesn't stop other
scripts itself. It announces the state, and each script checks it and stops itself.

## Where it lives

- **Attached to:** the `Game Manager` GameObject in `Game.unity`. `LevelTimer` sits on the same object.
  `PlayerHealth` sits on the player object instead, since it belongs to the player.
- **Requires:** its Inspector fields filled in (see below). There must be exactly one in the scene.
- **Not in:** `PatternEnvironment.unity`. Scripts that use it check for `null`, so they still run there
  without it (see Gotchas).

## Inspector settings

| Field | Type | What it points to |
| --- | --- | --- |
| `player` | PlayerMovement | The player ship |
| `gameplayCamera` | Camera | The main camera. Used to despawn asteroids once they're behind it, and to sort a pattern's asteroids by distance |
| `earth` | Renderer | The earth model that approaches as the level goes on |
| `earthCamera` | Camera | The camera that renders the earth |
| `levelTimer` | LevelTimer | The level's timer. Can be left empty if LevelTimer is on the same GameObject (it is in `Game.unity`) |
| `playerHealth` | PlayerHealth | The player's lives. Can be left empty if PlayerHealth is on the `player` object |

Each field has a matching read-only property (`Player`, `GameplayCamera`, `Earth`, `EarthCamera`,
`LevelTimer`, `PlayerHealth`). Other scripts can read the references but can't swap them out.

## How it works

### The singleton (`Instance`)

`Instance` is a **static** property, so it belongs to the `GameManager` class itself, not to any one
GameManager object. Any script can write `GameManager.Instance.GameplayCamera` without needing a reference
to the GameManager first. This setup (one shared object, reachable from anywhere through a static
property) is called a **singleton**.

Like `Speed` in PlayerMovement, it's `private set`, so only GameManager can assign it.

### Running first (`[DefaultExecutionOrder(-100)]`)

Unity doesn't guarantee what order different scripts' `Awake` and `Start` run in. That's a problem if
PlayerMovement's `Start` asks for `GameManager.Instance` before the GameManager's `Awake` has set it.

`[DefaultExecutionOrder(-100)]` fixes that. Scripts run in order from lowest to highest number, and most
scripts are 0, so GameManager's `Awake` always runs before theirs. By the time any other script's `Awake`
or `Start` runs, `Instance` is ready.

### `Awake`

1. **Check for a duplicate.** If `Instance` is already set to a different GameManager, this one logs a
   warning and removes itself. `Destroy(this)` removes only the GameManager component, not the whole
   GameObject. The `return` stops it from going on to overwrite `Instance`.
2. **Register.** `Instance = this`.
3. **Unfreeze time.** `Time.timeScale = 1f`. Time scale is global and, like static values, survives a
   scene reload. Without this, restarting after a game over would start the new scene frozen. GameManager
   runs first (`-100`), so time is normal before any other script runs.
4. **Fill in the LevelTimer.** If `levelTimer` was left empty, it looks for a LevelTimer on the same
   GameObject with `GetComponent`.
5. **Fill in the PlayerHealth.** If `playerHealth` was left empty and `player` is assigned, it looks for a
   PlayerHealth on the player object with `player.GetComponent`. Calling `GetComponent` on another
   component searches that component's GameObject. The `&& player != null` check stops before
   `player.GetComponent` could throw.

`[DisallowMultipleComponent]` also stops two GameManagers from being added to the same GameObject in the
editor. The duplicate check in `Awake` covers two on *different* GameObjects.

### End state: `IsGameOver` and `HasWon`

Two `bool` properties, both `private set`, so other scripts can read them but only GameManager can assign
them. Both start as `false`, the default for a `bool`. They're instance properties, so a reloaded scene
starts with a new GameManager and both are `false` again.

Other scripts **pull** this state: each one checks it and stops itself, instead of GameManager reaching
into them. If something new needs to stop at game over, the check goes in that script and GameManager
doesn't change.

### `GameOver`

Called by PlayerHealth when the player runs out of lives. If the game is already over or the level is
already won, it exits immediately. Otherwise it sets `IsGameOver`, freezes the level, and logs
"Game over". So it only ever runs once, and losing a life after winning can't turn a win into a loss.

**The freeze** is `Time.timeScale = 0f`. `Time.deltaTime` becomes 0, so everything that moves by it
stops: asteroids, the starfield, the skybox, the spawner's timers, physics, and particles. Unity still
calls `Update` and `LateUpdate`, so anything that uses **unscaled** time keeps running. That's
PlayerDeath (the death sequence), CameraShake, and the Player Explosion particles (set to Unscaled in
the prefab). Freezing is a level-wide operation, which is why it lives here and not in PlayerDeath.

### `DeathSequenceFinished`

Called by PlayerDeath once the ship has exploded and `endDelay` has passed. If the game isn't over, it
does nothing. Otherwise it logs "Death sequence finished". It's the hook for the game-over screen, which
isn't built yet.

### `Win`

Called by LevelTimer on the frame the earth arrives. It mirrors `GameOver`: it exits if either state is
already set, otherwise it sets `HasWon` and logs "Level complete". It's a placeholder for now, for things
like a victory screen.

### `OnDestroy`

When the GameManager is destroyed (e.g. the scene unloads or reloads), it clears `Instance`, but only if
`Instance` is still this one. That way a removed duplicate can't clear the real one.

This matters because static values survive a scene reload. Without it, `Instance` would still point at the
old, destroyed GameManager until the new one's `Awake` replaced it.

The GameManager isn't kept between scenes (there's no `DontDestroyOnLoad`). Each scene that needs one has
its own.

## Talks to

GameManager reads `player` once in `Awake` to find the PlayerHealth. Otherwise it doesn't read from or call
anything. Other scripts read from it or call it:

| Script | What it gets or calls | When |
| --- | --- | --- |
| **PlayerMovement** | `LevelTimer`; reads `IsGameOver` | `Start`; every frame and on the boost button |
| **PlayerHealth** | reads `IsGameOver` and `HasWon`, calls `GameOver()` | when it loses a life |
| **PlayerDeath** | reads `IsGameOver` and `GameplayCamera`, calls `DeathSequenceFinished()` | every frame until game over; at the end of the sequence |
| **AsteroidBehavior** | `GameplayCamera` | `Awake` |
| **AsteroidSpawner** | `LevelTimer`, `GameplayCamera`; reads `IsGameOver` | `Start`, and when spawning |
| **LevelTimer** | `Earth`, `EarthCamera`; reads `IsGameOver`, calls `Win()` | `Start`; every frame |
| **OrbitRing** | `Player` | `Start` |
| **StarfieldStreamer** | `Earth` | `Start` |

## Gotchas

- **Unfilled fields fail somewhere else.** GameManager doesn't check its fields. If one is left empty, the
  error shows up in whichever script uses it, not in GameManager.
- **LevelTimer doesn't check for `null`.** Every other script uses `GameManager.Instance != null ? ... : null`,
  so it keeps running in a scene without a GameManager. LevelTimer reads `GameManager.Instance` directly
  (for `Earth`, `EarthCamera`, `IsGameOver` and `Win()`), so it throws an error if there's no GameManager or
  `earth` / `earthCamera` is empty. It's fine today because LevelTimer is only in `Game.unity`.
- **Only safe from `Awake` onward.** `Instance` is set in `Awake`. Reading it from a field initializer or a
  constructor gets `null`.
- **Most lookups happen once.** Almost every script grabs its reference in `Awake` or `Start` and keeps it.
  Changing a GameManager field during Play mode won't reach those scripts.
- **The PlayerHealth fallback needs `player`.** If `player` is empty, `playerHealth` stays empty too.
  AsteroidBehavior doesn't depend on this, since it finds PlayerHealth through the player it hit.
- **Anything that must move during the game-over freeze has to use unscaled time.** Anything using
  `Time.deltaTime`, `WaitForSeconds`, or particle systems left on Scaled stays frozen.
- **Nothing resets the end state during play.** Once `IsGameOver` or `HasWon` is set, it stays set until
  the scene reloads.
- **The player is only used by OrbitRing and GameManager's own `Awake`.** Other scripts that need the
  player (like AsteroidBehavior) find it through the collision instead.
