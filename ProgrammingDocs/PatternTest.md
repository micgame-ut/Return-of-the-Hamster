# PatternTest

## Purpose

Carries the pattern builder's **Test This Pattern** request into Play mode. When you press the button, the
builder stores which pattern to test and how long to pause between runs. `ObjectSpawner` and `GameManager`
read that request when the level starts: the spawner plays only that pattern on a loop, and the menu is
skipped. When Play stops, the request is deleted, so the next Play is the normal level.

It is a small **static class**: there is no object to create, and nothing to attach to a GameObject. It exists
only in the Unity editor. The whole file is inside `#if UNITY_EDITOR`, so a game build doesn't contain it.

## Where it lives

- **File:** `Assets/Scripts/PatternTest.cs`
- **Attached to:** nothing. Any script can call it by name, like `Mathf`.
- **Requires:** nothing in the scene. It stores its values in `EditorPrefs`, a small key/value store saved on
  your computer (not in the project).

## Inspector settings

None. The values it holds come from the pattern builder's panel (**Test Loop Delay**, default 3 seconds) and
from whichever pattern is open in Prefab Mode.

The two `EditorPrefs` keys it uses:

| Key | Holds |
| --- | --- |
| `PatternBuilder.TestPattern` | The asset path of the pattern prefab to test, for example `Assets/Prefabs/Patterns/Ring-Gap.prefab` |
| `PatternBuilder.TestLoopDelay` | The pause between runs, in seconds |

## How it works

### Why `EditorPrefs`

Pressing Play reloads scripts, which resets ordinary `static` fields. A request kept in a field would be gone
before the level started. `EditorPrefs` is saved by Unity itself, so the request is still there when
`ObjectSpawner` and `GameManager` read it in Play. The cost is that it also survives other things, like an
editor crash during a test, so it has to be cleared deliberately (see `Clear`).

### The methods

- **`Request(patternAssetPath, loopDelay)`** stores a test request: the pattern's path and the loop delay go
  into `EditorPrefs`. It doesn't open any scene. The builder's `TestPattern` does that, after calling this.
- **`Active`** (property) is `true` once a request has been stored, meaning the saved path isn't empty. It
  doesn't check that the prefab still exists.
- **`Pattern`** (property) returns the pattern prefab at the saved path, or `null` if nothing is saved or the
  prefab was moved or deleted. Because `Active` can be `true` while `Pattern` is `null`, callers check both.
- **`LoopDelay`** (property) is the saved pause, in seconds, between test runs (3 if none was saved).
  `ObjectSpawner` uses it as the test wave's `delayAfter`, after the last asteroid and power-up are gone.
- **`Clear()`** deletes both saved values, so `Active` is `false` again. It doesn't touch the pattern builder.

### The life of a test

1. **You press Test This Pattern.** The builder saves the open pattern, calls `Request`, opens `PatternTest` and
   starts Play.
2. **`GameManager.Awake`** sees `Active` and sets `SkipMenuOnce`, so a scene with a menu would start at once.
   `PatternTest` has no menu, so this does nothing there, but it lets the test be pointed at a level like
   `Level_1` later.
3. **`ObjectSpawner.Start`** sees `Active` and a non-null `Pattern`, and replaces its wave list in memory with
   one wave holding that pattern, on a loop. In `RunWaves`, after each run it waits until no asteroids or
   power-ups are left, then `LoopDelay` seconds, then spawns the pattern again.
4. **You press Stop.** The builder's play-mode handler calls `Clear()`, so the next Play is normal, and reopens
   the pattern in Prefab Mode. It also takes you back to the scene you pressed Test from (it reopens that scene
   if it isn't the open one), not `PatternTest`. Neither of these is done by `PatternTest` itself.

A **Restart** on the game-over screen reloads the level during a test. The request is still stored, so the test
carries on.

## Talks to

- **PatternGizmosEditor**: calls `Request` (from `TestPattern`) and `Clear` (when Play ends, and when the editor
  loads and isn't entering Play).
- **ObjectSpawner**: reads `Active`, `Pattern` and `LoopDelay` in `Start`, and `Active` in `RunWaves`.
- **GameManager**: reads `Active` in `Awake`, to skip the menu.

## Gotchas

- **Every use is inside `#if UNITY_EDITOR`.** The class doesn't exist in a build, so any script that calls it
  without the same wrapper would stop the build from compiling.
- **`Active` doesn't mean the pattern exists.** Check `Pattern != null` too, as `ObjectSpawner` does. If the
  prefab was moved after pressing Test, the spawner falls back to the normal waves.
- **A leftover request turns the next Play into a test.** An editor crash during a test skips the usual
  `Clear`. The builder also clears at editor start-up (when it isn't entering Play) to avoid that.
- **Nothing here opens a scene or enters Play.** That is the builder's `TestPattern`. This class only holds the
  request.
