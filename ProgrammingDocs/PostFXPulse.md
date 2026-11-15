# PostFXPulse

## Purpose

Spikes a few post-processing effects on game events (a hit, Warp, Jackpot) and eases them back to the profile's resting look, so the screen reacts to what the player does.

## Where it lives

- **Attached to:** the `Global Volume` object in each level scene (Level_1, Level_2, Level_3).
- **Requires:** a `Volume` whose profile has `Vignette`, `ChromaticAberration`, `LensDistortion` and `Bloom` overrides (`SampleSceneProfile` has them), and a `GameManager` in the scene.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| Volume | the object's own Volume | The Global Volume whose look is pulsed. Its profile is copied at start, so the saved profile is never changed. |
| Decay Speed | 4 | How fast a pulse fades, as a share of the full pulse per second. 4 = gone in about a quarter of a second. Also the speed the Warp and Jackpot effects ease in and out. |
| Hit Vignette | 0.55 | The vignette intensity right after a hit (0 to 1). |
| Hit Aberration | 0.6 | The chromatic aberration right after a hit (0 to 1). |
| Warp Aberration | 0.8 | The chromatic aberration while Warp is running (0 to 1). |
| Warp Lens Distortion | -0.4 | The lens distortion while Warp is running (-1 to 1). Negative pinches the picture in. |
| Jackpot Bloom Boost | 1.5 | How much extra bloom intensity Jackpot adds while it's active. |

## How it works

### Awake

Copies the Volume's profile with `Instantiate` and gives the copy to the Volume, so the saved profile asset is never changed. It then gets the four overrides from the copy and remembers the vignette, chromatic aberration and bloom intensities as the resting values.

### OnEnable / OnDisable

Subscribe to and unsubscribe from `PlayerHealth.OnLifeLost` and `PlayerJackpot.OnJackpotChanged` on `GameManager`.

### HandleLifeLost / HandleJackpotChanged

A life lost sets `hitPulse` to 1 (full). Jackpot starting or ending sets `jackpotOn`. They only record what happened; `Update` does all the writing to the Volume.

### Update

Each frame, with a step of `decaySpeed` × `Time.unscaledDeltaTime`:

- `hitPulse` moves toward 0 by the step.
- `warpShare` moves toward 1 while `PlayerWarp.IsWarping` is true, and toward 0 when it's not.
- `jackpotShare` moves toward 1 while Jackpot is on, and toward 0 when it's not.

Then it writes the final values:

- **Vignette:** a blend from the resting value to `hitVignette` by `hitPulse`.
- **Chromatic aberration:** the resting value plus the stronger of the hit push and the Warp push (so a hit during Warp doesn't stack).
- **Lens distortion:** `warpLensDistortion` × `warpShare` (rests at 0).
- **Bloom:** the resting value plus `jackpotBloomBoost` × `jackpotShare`.

### OnDestroy

Destroys the profile copy, because a profile made with `Instantiate` isn't removed when the scene unloads.

## Talks to

- **GameManager**: reads `Instance`, `PlayerHealth`, `PlayerJackpot` and `PlayerWarp`.
- **PlayerHealth**: subscribes to `OnLifeLost`.
- **PlayerJackpot**: subscribes to `OnJackpotChanged`.
- **PlayerWarp**: reads `IsWarping` each frame (it has no event).

## Gotchas

- The pulses use unscaled time, because Warp runs the game fast and the pulses must not speed up with it.
- The resting values are read once in `Awake`. Changing the profile asset while playing doesn't change the resting look until the next Play.
- If the profile is missing `Vignette`, `ChromaticAberration`, `LensDistortion` or `Bloom`, `Awake` throws a null reference error (there are no null checks).
- `OnEnable` needs `GameManager.Instance` and its player references to be set already. If hits or Jackpot never pulse, check the script order and move the subscribing into `Start`.
- Other scripts that change the same effects at runtime would fight this one, because it writes every frame.
