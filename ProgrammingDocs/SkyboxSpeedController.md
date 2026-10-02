# SkyboxSpeedController

## Purpose

Makes the skybox's nebula clouds drift faster as the player speeds up. Every frame it moves the clouds a
little further, by an amount based on the player's `Speed`, and sends the total to the skybox shader.

## Where it lives

- **Attached to:** `Main Camera` in `Game.unity`. It doesn't use the camera. It could sit on any GameObject.
- **Requires:** a skybox material set in the scene's Lighting settings (currently `StarrySkyMat`, which uses
  the `Starry Sky` shader graph). The shader needs a float property with the reference name `_ScrollOffset`.

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `baseTimeSpeed` | 0.3 | 0.3 | How fast the clouds drift when the player's `Speed` is 0 |
| `speedInfluence` | 0.05 | 0.01 | How much each point of player `Speed` adds to the drift speed |

With the scene's values, the drift speed goes from 0.3 (stopped) to 0.4 (max speed 10).

## How it works

### `Awake`: making a copy of the skybox material

`RenderSettings.skybox` is the skybox material the scene uses. It's the actual `StarrySkyMat` asset in the
project.

Changing a material asset while the game runs changes the **asset file itself**, and those changes stay
after you stop Play mode (the same as with ScriptableObjects, see SpawnableTemplate). If the script changed
the asset directly, the material would be left with whatever value it had when you stopped playing.

To avoid that, `Awake` makes a copy of the material with `Instantiate`, and tells the scene to use the copy
as its skybox. The script only ever changes the copy, which is thrown away when the game stops.

If the scene has no skybox, there's nothing to copy, and `Update` does nothing.

### `Update`: scrolling the clouds

Every frame:

**1. How fast to drift right now.**

`timeSpeed = baseTimeSpeed + PlayerMovement.Speed × speedInfluence`

| Player `Speed` | Drift speed (scene values) |
| --- | --- |
| 0 | 0.3 |
| 5 | 0.35 |
| 10 (max) | 0.4 |

**2. Add this frame's drift to the running total.**

`scrollOffset += timeSpeed × Time.deltaTime`

This is the same running total idea as `orbitAngle` in PlayerMovement, `distance` in AsteroidBehavior, and
`Progress` in LevelTimer. A speed change only changes how fast the total grows from now on, so the clouds
never jump.

**3. Send it to the shader.** `SetFloat` sets `_ScrollOffset` on the copied material.

`SetFloat` uses `ScrollOffsetId` instead of the name `"_ScrollOffset"`. `Shader.PropertyToID` turns the name
into a number once, when the game starts, which is faster than Unity looking up the name every frame.
AsteroidBehavior does the same for its emission and outline colors.

**Pausing.** When `Time.timeScale` is 0, `Time.deltaTime` is 0, so nothing gets added and the clouds
freeze where they are. It needs no special case.

### What the shader does with it

Inside the `Starry Sky` shader graph, `_ScrollOffset` is used as the offset of the noise that makes the
clouds. As the offset grows, the clouds slide across the sky.

### Why it's a running total

The shader used to work out the offset itself as `Time × Time Speed` (Unity's clock multiplied by a speed
this script set). Because that's calculated fresh every frame, changing the speed moved the clouds to
where they *would* be if they'd been going at the new speed the whole time. 100 seconds in:

| | Time Speed | Cloud offset |
| --- | --- | --- |
| Stopped | 0.3 | 100 × 0.3 = 30 |
| After speeding up to max | 0.4 | 100 × 0.4 = 40 |

So the clouds rushed an extra 10 units while the player accelerated, scrolled backward while they slowed
down, and got worse the longer the level ran. Pausing would also have snapped the sky back to how it looked
at the start. Building the offset up here, a little each frame, fixes both.

## Talks to

- **PlayerMovement**: reads the static `Speed` every frame.
- **The `Starry Sky` shader**: sets its `_ScrollOffset` property on a copy of the skybox material.

## Gotchas

- **Tweak the sky in the material, not in Play mode.** While the game runs, the scene uses the copy. Edits to
  `StarrySkyMat` in Play mode change the asset, but not what's on screen.
