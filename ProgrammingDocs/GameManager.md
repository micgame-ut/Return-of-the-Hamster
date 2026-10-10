# GameManager

## Purpose

One central place for the scene's important objects: the player, the gameplay camera, the earth, the
earth camera, the LevelTimer, and the player's PlayerHealth, PlayerInvincibility, PlayerShell and PlayerTwins. Instead of every script having its own
Inspector fields for these (and each one needing to be dragged in by hand), scripts ask the GameManager
with `GameManager.Instance`.

**The rule for player components:** a script on the player finds the player's other components itself. A
script anywhere else gets them (and the player itself) from GameManager, once in `Awake` or `Start`. If
GameManager doesn't expose one yet, it's added here. (AGENTS.md section 5.)

It also holds the level's end state: whether the game is over or the level is won. It decides when the
level is won (the earth has arrived and the field is clear), freezes the game on either ending, and shows
the matching end screen. It doesn't stop other scripts itself. It announces the state (the `OnGameOver` and
`OnWin` events, plus the `IsGameOver` / `HasWon` properties), and each script reacts and stops itself.

## Where it lives

- **Attached to:** the `Game Manager` GameObject in `TestGame.unity`. `LevelTimer` sits on the same object.
  `PlayerHealth`, `PlayerInvincibility`, `PlayerShell` and `PlayerTwins` sit on the player object instead, since they belong to the player.
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
| `levelTimer` | LevelTimer | The level's timer. Can be left empty if LevelTimer is on the same GameObject (it is in `TestGame.unity`) |
| `playerHealth` | PlayerHealth | The player's lives. Can be left empty if PlayerHealth is on the `player` object |
| `playerInvincibility` | PlayerInvincibility | The player's invincibility power-up state. Can be left empty if PlayerInvincibility is on the `player` object |
| `playerShell` | PlayerShell | The player's shield shell and its hitbox. Can be left empty if PlayerShell is on the `player` object |
| `playerTwins` | PlayerTwins | The twin power-up on the player. Can be left empty if PlayerTwins is on the `player` object |
| `runStats` | RunStats | This run's stats and the Apollo number. Can be left empty if RunStats is on the same GameObject |
| `score` | Score | This level's score and its HUD counter. Can be left empty if Score is on the same GameObject |
| `endScreen` | EndScreen | The EndScreen component on the `EndScreen` canvas, which holds both the Game Over and Win screens. **Must be assigned**: it isn't on this GameObject, so there's no `GetComponent` fallback. It has no public property, since only GameManager uses it |
| `showOrbitRing` | bool (default on) | Accessibility setting: whether OrbitRing draws the orbit ring and its danger arcs. Read once when the level starts. Meant to be set from a menu setting later |

Each field has a matching read-only property (`Player`, `GameplayCamera`, `Earth`, `EarthCamera`,
`LevelTimer`, `PlayerHealth`, `PlayerInvincibility`, `RunStats`, `Score`, `ShowOrbitRing`). Other scripts can read the values but can't change them.

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
4. **Fill in the LevelTimer, RunStats and Score.** If any was left empty, it looks for one on the same
   GameObject with `GetComponent`.
5. **Fill in the PlayerHealth, PlayerInvincibility, PlayerShell and PlayerTwins.** If any was left empty and `player` is
   assigned, it looks for it on the player object with `player.GetComponent`. Calling `GetComponent` on
   another component searches that component's GameObject. The `&& player != null` check stops before
   `player.GetComponent` could throw.

`[DisallowMultipleComponent]` also stops two GameManagers from being added to the same GameObject in the
editor. The duplicate check in `Awake` covers two on *different* GameObjects.

### End state: `IsGameOver` and `HasWon`

Two `bool` properties, both `private set`, so other scripts can read them but only GameManager can assign
them. Both start as `false`, the default for a `bool`. They're instance properties, so a reloaded scene
starts with a new GameManager and both are `false` again.

GameManager never reaches into other scripts to stop them. There are two ways a script can react:

- **Subscribe to an event** when it needs to do something *once*, at the moment the level ends.
  `OnGameOver` is raised at the end of `GameOver()`, and `OnWin` at the end of `Win()`. Both are `Action`
  (no value passed) and both can fire at most once, because of the guards at the top of those methods.
  Subscribe in `OnEnable` and unsubscribe in `OnDisable`, like `PlayerHealth.OnLivesChanged`. PlayerDeath,
  PlayerInvincibility, ExhaustFlame and DangerWarning do this.
- **Read `IsGameOver` / `HasWon`** when the check is part of work that runs anyway (every frame, or on a
  hit). PlayerMovement, ObjectSpawner, LevelTimer, RunStats, PlayerHealth and PowerUp do this.

If something new needs to stop at game over, the reaction goes in that script and GameManager doesn't
change.

### `GameOver`

Called by PlayerHealth when the player runs out of lives. If the game is already over or the level is
already won, it exits immediately. Otherwise it sets `IsGameOver`, freezes the level, records the death
in RunStats (`RecordDeath()`, which saves the next Apollo number), and raises `OnGameOver` last, so
listeners see the final state. So it only ever runs once, and losing a life after winning can't turn a win into a loss.

**The freeze** is `Time.timeScale = 0f`. `Time.deltaTime` becomes 0, so everything that moves by it
stops: asteroids, the starfield, the skybox, the spawner's timers, physics, and particles. Unity still
calls `Update` and `LateUpdate`, so anything that uses **unscaled** time keeps running. That's
PlayerDeath (the death sequence), CameraShake, and the Player Explosion particles (set to Unscaled in
the prefab). Freezing is a level-wide operation, which is why it lives here and not in PlayerDeath.

### `DeathSequenceFinished`

Called by PlayerDeath once the ship has exploded and `endDelay` has passed. If the game isn't over, it
does nothing. Otherwise it shows the Game Over screen (`endScreen.ShowGameOver()`), if one is assigned.

### `Win`

Called by GameManager's own `Update` when the level is cleared (see below). It mirrors `GameOver`: it exits
if either state is already set, otherwise it sets `HasWon`, freezes the game (`Time.timeScale = 0`), records
the win in RunStats (`RecordWin()`, which saves the Apollo's totals for the next level), shows the win
screen (`endScreen.ShowWin()`), and raises `OnWin` last.

### `LevelCleared` and `Update`: deciding the win

`LevelCleared` is a private property that's true when:

- the earth has arrived (`LevelTimer.IsComplete`), so the spawner has stopped starting waves and revealing
  objects, **and**
- nothing spawned is still flying: `AsteroidBehavior.Active` and `PowerUp.Active` are both empty.

`&&` short-circuits, so the lists aren't checked until the earth has arrived. `Update` checks it every
frame, but only while neither ending has happened (`!HasWon && !IsGameOver`), and calls `Win()` the first
time it's true. So the level doesn't end while a wave is still on screen, and dying before the field
clears is still a game over.

A boss fight at the end of a level would add one more condition to `LevelCleared` (e.g. "and the boss is
defeated"); nothing else would change.

### `OnDestroy`

When the GameManager is destroyed (e.g. the scene unloads or reloads), it clears `Instance`, but only if
`Instance` is still this one. That way a removed duplicate can't clear the real one.

This matters because static values survive a scene reload. Without it, `Instance` would still point at the
old, destroyed GameManager until the new one's `Awake` replaced it.

The GameManager isn't kept between scenes (there's no `DontDestroyOnLoad`). Each scene that needs one has
its own.

## Talks to

GameManager reads `player` once in `Awake` to find the PlayerHealth, PlayerInvincibility, PlayerShell and PlayerTwins. Otherwise it doesn't read from or call
anything. Other scripts read from it or call it:

| Script | What it gets or calls | When |
| --- | --- | --- |
| **PlayerMovement** | `LevelTimer`; reads `IsGameOver` | `Start`; every frame and on the boost button |
| **PlayerHealth** | reads `IsGameOver` and `HasWon`, calls `GameOver()` | when it loses a life |
| **PlayerDeath** | subscribes to `OnGameOver`; gets `GameplayCamera`, calls `DeathSequenceFinished()` | at game over; at the end of the sequence |
| **EndScreen** | GameManager calls `ShowGameOver()` / `ShowWin()`; it reads `RunStats` and `LevelTimer` | when the death sequence finishes; on a win |
| **PlayerInvincibility** | subscribes to `OnGameOver` and `OnWin` | at game over or win |
| **ExhaustFlame** | subscribes to `OnGameOver` and `OnWin`; on a twin copy, gets `Player` and `PlayerInvincibility` | `Awake`; at game over or win |
| **TwinCopy** | `Player`, `PlayerShell` | `Awake` |
| **DangerWarning** | gets `PlayerHealth` and `PlayerTwins`; subscribes to `OnWin` | `Awake`; on a win |
| **AsteroidBehavior** | `GameplayCamera`, `Player`, `PlayerHealth`, `PlayerInvincibility`, `PlayerTwins`; `RunStats`. GameManager reads `AsteroidBehavior.Active` for `LevelCleared` | `Awake`; when an asteroid is smashed; every frame |
| **PowerUp** | `GameplayCamera`, `Player`, `PlayerHealth`, `PlayerInvincibility`, `PlayerTwins`; reads `HasWon`, gets `RunStats`. GameManager reads `PowerUp.Active` for `LevelCleared` | `Awake`; on pickup; every frame |
| **RunStats** | reads `HasWon` and `Player` (for `InOverdrive`); GameManager calls its `RecordDeath()` | every frame; at game over |
| **Score** | `LevelTimer`; reads `IsGameOver` and `HasWon` | `Awake`; when points are added |
| **ObjectSpawner** | `LevelTimer`, `GameplayCamera`; reads `IsGameOver` | `Start`, and when spawning |
| **LevelTimer** | `Earth`, `EarthCamera`; reads `IsGameOver`. GameManager reads its `IsComplete` for `LevelCleared` | `Start`; every frame |
| **OrbitRing** | `ShowOrbitRing`, `Player`, `PlayerShell` | `Start` |
| **StarfieldStreamer** | `Earth` | `Start` |

## Gotchas

- **Unfilled fields fail somewhere else.** GameManager doesn't check its fields. If one is left empty, the
  error shows up in whichever script uses it, not in GameManager.
- **LevelTimer doesn't check for `null`.** Every other script uses `GameManager.Instance != null ? ... : null`,
  so it keeps running in a scene without a GameManager. LevelTimer reads `GameManager.Instance` directly
  (for `Earth`, `EarthCamera`, `IsGameOver` and `Win()`), so it throws an error if there's no GameManager or
  `earth` / `earthCamera` is empty. It's fine today because LevelTimer is only in `TestGame.unity`.
- **Only safe from `Awake` onward.** `Instance` is set in `Awake`. Reading it from a field initializer or a
  constructor gets `null`.
- **Most lookups happen once.** Almost every script grabs its reference in `Awake` or `Start` and keeps it.
  Changing a GameManager field during Play mode won't reach those scripts.
- **The PlayerHealth, PlayerInvincibility, PlayerShell and PlayerTwins fallbacks need `player`.** If `player` is empty,
  they stay empty too. AsteroidBehavior and PowerUp also get the player itself from here, so with no
  `player` they ignore every contact: no hits, no smashes, no pickups.
- **Anything that must move during the game-over freeze has to use unscaled time.** Anything using
  `Time.deltaTime`, `WaitForSeconds`, or particle systems left on Scaled stays frozen.
- **A spawnable that isn't in an `Active` list can't hold up the win.** A new kind of object sent at the
  player (other than asteroids and power-ups) needs its own list, added to `LevelCleared`, or the level
  can be won while it's still flying.
- **Nothing resets the end state during play.** Once `IsGameOver` or `HasWon` is set, it stays set until
  the scene reloads.
- **Many scripts depend on `player` being assigned.** OrbitRing, RunStats, AsteroidBehavior and PowerUp
  read `Player`, and every player-component fallback goes through it.
