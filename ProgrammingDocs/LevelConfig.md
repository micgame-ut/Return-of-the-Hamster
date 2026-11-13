# LevelConfig

## Purpose

Holds the numbers that make one level different from another: how long it takes and what the finish planet
looks like and where it starts. Each level has its own asset, so changing a level's time or planet means
editing one file in the Project window, not opening the scene.

The level's shared setup (player, camera, HUD, end screen, spawner) lives in the `LevelCore` prefab. The
config is the small part that isn't shared. Waves are not in it yet; they are still on each scene's
`ObjectSpawner`.

It's only data. It has no methods. `GameManager` reads it once, in `Awake`.

## Where it lives

- **Attached to:** nothing. It's a **ScriptableObject**, saved as an asset file (see
  [SpawnableTemplate.md](SpawnableTemplate.md) for why).
- **The assets:** `Assets/Levels/Level1Config.asset`, `Level2Config.asset`, `Level3Config.asset` and
  `TemplateConfig.asset` (for the `LevelTemplate` scene).
- **Used by:** the `levelConfig` field on `GameManager`, which is set on the `LevelCore` instance in each
  scene.
- **Making a new one:** right-click in the Project window → Create → Return of the Hamster → Level Config.

## Inspector settings

"Default" is what a newly created asset starts with (the old Mars values).

### Timing

| Field | Default | What it changes |
| --- | --- | --- |
| `levelTime` | 180 | Seconds the level takes if the player's `Speed` stays at 0. Passed to `LevelTimer.SetLevelTime` |

### Finish Planet

| Field | Default | What it changes |
| --- | --- | --- |
| `planetMaterial` | none | The material on the planet that arrives at the end. Empty keeps the planet's current material |
| `planetPosition` | (0, -1, 990) | Where the planet starts, in the Planet Camera's local space. `LevelTimer` moves it toward the camera from here |
| `planetRotation` | (0, 300, 0) | The planet's rotation in degrees. Changes which side faces the player |
| `planetScale` | (100, 100, 100) | The planet's size on each axis |

### The four assets

| Asset | `levelTime` | Planet | Position | Rotation |
| --- | --- | --- | --- | --- |
| `Level1Config` | 120 | `Mars_Mat` | (333, -1, 990) | (0, 180, 0) |
| `Level2Config` | 180 | `Moon_Mat` | (0, -1, 990) | (0, 180, 0) |
| `Level3Config` | 240 | `Earth_Mat` | (0, -1, 990) | (0, 180, 0) |
| `TemplateConfig` | 180 | default Lit | (0, -1, 990) | (0, 180, 0) |

Level 1's planet starts off to the side (x = 333) because the menu moves it into place when you press Start.

## How it works

Nothing runs in this script. `GameManager.ApplyLevelConfig` (called from `Awake`) reads it:

1. If `levelConfig` is empty, it does nothing and the level keeps what the scene or prefab already has.
2. It passes `levelTime` to the `LevelTimer`.
3. It sets the planet's `sharedMaterial`, local position, local rotation and local scale.

`GameManager` runs first (`-100`), so `MenuTransition` and `LevelTimer` see the final planet values.

## Talks to

- **GameManager**: reads every field in `Awake`.
- **LevelTimer**: receives `levelTime` through `SetLevelTime`.

## Gotchas

- **The config wins over the scene.** If the config is assigned, a value you set on the planet or on
  `LevelTimer` in the scene is replaced when the level starts. Edit the config instead.
- **An empty config field is not an error.** Without a config nothing changes. Without a `planetMaterial`,
  the planet keeps its material but still gets the position, rotation and scale.
- **Changes made in Play mode are kept.** It's an asset, so edits to it are saved for real.
- **`sharedMaterial` swaps the reference.** It doesn't copy the material, so nothing needs cleaning up.
- **Position is local.** The planet is a child of the Planet Camera, so the numbers are relative to it.
- **Waves aren't here yet.** Each level's wave list is still on its own `ObjectSpawner`.
