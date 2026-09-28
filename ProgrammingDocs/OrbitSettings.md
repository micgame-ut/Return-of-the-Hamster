# OrbitSettings

## Purpose

The one place the player's orbit and the Game scene's layout are set: the orbit's size and center, where
the player sits in depth, and where the Asteroid Spawner sits.

Both the game and the pattern builder need these numbers. Before, each kept its own copy, and they had to
be kept in sync by hand. Now every script reads them from this one asset, so the builder's preview always
matches what happens in play.

## Where it lives

- **Attached to:** nothing. It's a ScriptableObject (see AsteroidTemplate), saved as an asset.
- **The asset:** `Assets/Resources/OrbitSettings.asset`. It must keep that exact name and folder (see
  "Finding the asset" below).
- **Making a new one:** right-click in the Project window → Create → Return of the Hamster → Orbit
  Settings. There should only ever be the one in `Resources`, though.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `orbitRadius` | 3 | Size of the player's orbit, in world units. Can't go below 0.01 |
| `orbitCenter` | (0, 1) | The x/y the orbit circles around, in world space |
| `playerZ` | -4 | The player's depth. PlayerMovement moves itself here when the game starts |
| `spawnerPosition` | (0, 0, 15) | Where the Asteroid Spawner sits. AsteroidSpawner moves itself here when the game starts |

All of these can be changed in Play mode, and the change is kept when you stop (it's an asset, see
AsteroidTemplate's gotchas). `orbitRadius` and `orbitCenter` take effect immediately. `playerZ` and
`spawnerPosition` are only applied when the game starts.

## How it works

### Who reads what

| Value | Used by |
| --- | --- |
| `OrbitRadius` | PlayerMovement (the orbit), AsteroidBehavior (Wave movement), PatternGizmos and the builder (rings and placement). OrbitRing gets it through PlayerMovement |
| `OrbitCenter` | The same scripts as `OrbitRadius` |
| `PlayerZ` | PlayerMovement (moves itself there), PatternGizmos (the "Player" ring) |
| `SpawnerPosition` | AsteroidSpawner (moves itself there), PatternGizmos (to work out spawner space) |

`SameZTolerance` isn't here. It's a plain number, not something you'd tune, so it lives in AsteroidSpawner
as `public const`, and PatternGizmos reads it from there.

### Spawner space

Patterns are built relative to the spawner (see PatternGizmos). Two helpers give the values as measured
from the spawner:

- `OrbitCenterFromSpawner = orbitCenter − spawner x/y`. The spawner is at x/y (0, 0), so this is currently
  the same as `orbitCenter`.
- `PlayerZFromSpawner = playerZ − spawner z`. With the defaults, -4 − 15 = **-19**: the player is 19
  units in front of the spawner.

Doing this subtraction in code is what keeps the builder right when either position changes. It used to
be worked out by hand and typed in as -19.

### Finding the asset (`Instance`)

Scripts get the settings with `OrbitSettings.Instance`. There's no Inspector field to fill in anywhere, so
there's nothing to forget to assign or to point at the wrong asset.

**`Resources.Load`** finds an asset by name, as long as it's inside a folder called `Resources`. Unity
treats those folders specially: everything in them is included in game builds and can be loaded by name
from code, in the editor or in the game. That matters here because the pattern builder runs in Prefab
Mode, where the Game scene isn't loaded, so there's no scene object it could read these values from.

The first time `Instance` is used, it loads the asset and saves it in a static field. After that, it just
returns the saved one.

If the asset can't be found, it logs an error and makes a temporary copy with the default values from the
script, so the game still runs instead of crashing.

### `OnValidate`

Unity calls `OnValidate` whenever a value is changed in the Inspector. Here it stops `orbitRadius` from
going below 0.01. PlayerMovement divides by the radius, and dividing by 0 would break the orbit.

## Talks to

OrbitSettings doesn't read from or call anything. These scripts read from it:

- **PlayerMovement**: radius, center, and player z.
- **AsteroidBehavior**: radius and center, for Wave movement.
- **AsteroidSpawner**: spawner position.
- **PatternGizmos / PatternGizmosEditor**: all of it, measured from the spawner.

## Gotchas

- **Moving the player or spawner in the scene does nothing.** They move themselves to this asset's values
  when the game starts. Change the asset instead.
- **The camera isn't controlled by this.** `orbitCenter` should match `Main Camera`'s x/y, or the orbit
  won't sit in the middle of the screen. Nothing enforces that.
- **The asset must stay at `Resources/OrbitSettings.asset`.** Renaming or moving it means `Instance` can't
  find it. The game falls back to the script's defaults and logs an error.
- **Script defaults vs the asset.** As with AsteroidTemplate, the values in the script are only used for a
  new asset, or the fallback. The asset's values are what count.
