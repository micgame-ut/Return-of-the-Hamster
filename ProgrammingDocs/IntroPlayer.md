# IntroPlayer

## Purpose

Plays the studio logo intro (an mp4 plus a .wav) in its own scene, right after Unity's splash logo. While it plays, it loads `Level_1` (which holds the menu) in the background, then switches to it once both the video and the sound have finished. The intro can't be skipped.

## Where it lives

- **Attached to:** an `Intro` GameObject in the `Intro` scene, which is index 0 in Build Settings
- **Requires:** a `VideoPlayer` (with the mp4 assigned, Play On Awake off) and an `AudioSource` (with the .wav assigned, Play On Awake off), both assigned in the Inspector. `Level_1` must be in Build Settings.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| Video Player | none | The `VideoPlayer` that plays the logo mp4 |
| Audio Source | none | The `AudioSource` that plays the logo .wav |
| Next Scene Name | `Level_1` | The scene to load once both the video and the sound have finished. It must be in Build Settings. |

## How it works

### Start

Subscribes `OnPrepared` to the video's `prepareCompleted` event, then calls `Prepare()` so the video loads its first frames without playing. It also starts `LoadSceneAsync` for the next scene and sets `allowSceneActivation = false`, so Unity loads it in the background but holds it at 90% instead of switching.

### OnPrepared

Called by Unity when the video is ready. It starts the video and the .wav in the same call so they stay in sync, and sets `started` to true. Waiting for `Prepare` avoids the .wav running ahead while the video decodes.

### Update

Each frame it returns early while `started` is false (nothing is playing yet) or `finished` is true (the scene was already released). While either the video or the .wav is still playing it also returns, so the intro lasts as long as the longer of the two. When both have stopped, it sets `finished` and sets `allowSceneActivation = true`, and Unity completes the switch to the next scene.

## Talks to

- **SceneManager (Unity):** starts the background load and releases it
- **Level_1 scene:** the scene it loads. Nothing in it needs to know about the intro, because it loads as it would from a normal start.

## Gotchas

- `Intro` has to be index 0 in Build Settings, or the intro never runs first. `EndScreen` loads the menu by name (`"Level_1"`) and the next level by the current index plus one, so the shift in indices doesn't affect it.
- `started` exists because `isPlaying` is false while the video is still preparing. Without it, `Update` would think the intro was already over on the first frame.
- If the Video Player or Audio Source field is empty, `Start` throws a `NullReferenceException`.
- While `allowSceneActivation` is false, other async scene loads queued behind it won't complete. Don't start any in this scene.
- If the platform pauses audio when the window loses focus, `audioSource.isPlaying` can read false and end the intro early.
