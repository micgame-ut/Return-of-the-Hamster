# QuestionMarkBillboard

## Purpose

Keeps the "?" inside the Mystery star facing the camera while the star around it spins. `PowerUp` spins the whole prefab root, so the "?" would
spin with it. This script sets the "?" rotation again every frame, so the spin never shows.

## Where it lives

- **Attached to:** the `QuestionMark` child of `Mystery PowerUp.prefab`. It is a child of the spinning root, at its centre, so it moves and
  scales with the pickup but its rotation is overwritten.
- **Requires:** a `GameManager` in the scene (for the gameplay camera). Without one it does nothing, and the "?" just spins with the star.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `rotationOffset` | (0, 0, 0); **(0, 180, 0)** on the prefab | Extra rotation in degrees, applied after facing the camera. It fixes a model that imports facing the wrong way. The "?" mesh imports with its front toward −Z, so the prefab needs the 180° turn about Y |

## How it works

### `Start`

Finds the gameplay camera's transform from `GameManager.Instance.GameplayCamera`, the same way `PowerUp` does, and keeps it. If there is no
`GameManager` or camera, `cam` stays null.

### `LateUpdate`

`LateUpdate` runs after every `Update`, and `PowerUp.Update` is where the root is spun. So setting `transform.rotation` here overwrites the
rotation the "?" inherited that frame. It sets it to `Quaternion.LookRotation(-cam.forward, cam.up)`, which points the object's +Z back at the camera
and keeps it upright (the camera's `up` stops it tilting), then multiplies by `Euler(rotationOffset)`. It does nothing while `cam` is null.

## Talks to

- **GameManager**: reads `GameplayCamera` once.
- **PowerUp**: not called, but it spins the parent every frame; this script undoes that for the "?".

## Gotchas

- **It faces the camera's direction, not the player's position.** The camera looks down the lane at the player, so the two are the same in this game.
- **The offset belongs to the mesh.** If the "?" is ever re-exported with a different orientation, change `rotationOffset`, not the script.
- **It has no effect on the "?"'s position.** The "?" mesh is offset slightly below its pivot, so it swings a little around the centre as the camera moves.
- **The camera is looked up once in `Start`.** A pickup that outlives a camera change would keep the old one; pickups are short-lived, so this doesn't happen.
