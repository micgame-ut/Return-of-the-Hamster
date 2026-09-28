# GameManager

## Purpose

One central place for the scene's important objects: the player, the gameplay camera, the earth, the
earth camera, and the LevelTimer. Instead of every script having its own Inspector fields for these (and
each one needing to be dragged in by hand), scripts ask the GameManager with `GameManager.Instance`.

It holds references only. It doesn't run any game logic itself.

## Where it lives

- **Attached to:** the `Game Manager` GameObject in `Game.unity`. `LevelTimer` sits on the same object.
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

Each field has a matching read-only property (`Player`, `GameplayCamera`, `Earth`, `EarthCamera`,
`LevelTimer`). Other scripts can read the references but can't swap them out.

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
3. **Fill in the LevelTimer.** If `levelTimer` was left empty, it looks for a LevelTimer on the same
   GameObject with `GetComponent`.

`[DisallowMultipleComponent]` also stops two GameManagers from being added to the same GameObject in the
editor. The duplicate check in `Awake` covers two on *different* GameObjects.

### `OnDestroy`

When the GameManager is destroyed (e.g. the scene unloads or reloads), it clears `Instance`, but only if
`Instance` is still this one. That way a removed duplicate can't clear the real one.

This matters because static values survive a scene reload. Without it, `Instance` would still point at the
old, destroyed GameManager until the new one's `Awake` replaced it.

The GameManager isn't kept between scenes (there's no `DontDestroyOnLoad`). Each scene that needs one has
its own.

## Talks to

GameManager doesn't read from or call anything. Other scripts read from it:

| Script | What it gets | When |
| --- | --- | --- |
| **PlayerMovement** | `LevelTimer` | `Start` |
| **AsteroidBehavior** | `GameplayCamera` | `Awake` |
| **AsteroidSpawner** | `LevelTimer`, `GameplayCamera` | `Start`, and when spawning |
| **LevelTimer** | `Earth`, `EarthCamera` | `Start` |
| **OrbitRing** | `Player` | `Start` |
| **StarfieldStreamer** | `Earth` | `Start` |

## Gotchas

- **Unfilled fields fail somewhere else.** GameManager doesn't check its fields. If one is left empty, the
  error shows up in whichever script uses it, not in GameManager.
- **LevelTimer doesn't check for `null`.** Every other script uses `GameManager.Instance != null ? ... : null`,
  so it keeps running in a scene without a GameManager. LevelTimer reads `GameManager.Instance.Earth`
  directly, so it throws an error if there's no GameManager or `earth` / `earthCamera` is empty. It's fine
  today because LevelTimer is only in `Game.unity`.
- **Only safe from `Awake` onward.** `Instance` is set in `Awake`. Reading it from a field initializer or a
  constructor gets `null`.
- **Most lookups happen once.** Almost every script grabs its reference in `Awake` or `Start` and keeps it.
  Changing a GameManager field during Play mode won't reach those scripts.
- **The player is only used by OrbitRing.** Other scripts that need the player (like AsteroidBehavior)
  find it through the collision instead.
