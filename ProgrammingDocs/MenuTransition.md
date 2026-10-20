# MenuTransition

## Purpose

Level 1 loads with the menu showing. When the player presses Start, the menu goes away and the parts of
Level 1 ease into their gameplay positions: the far planet and the player (the hamster) glide into place, the
near planet slides off screen and the menu UI flies back past the camera. Then the level starts, and the star
stream and the player's exhaust flame ease in afterwards. There's no scene change: the menu is part of Level 1.

`MenuTransition` only does the easing and the hand-over. It doesn't start any gameplay itself. It calls
`GameManager.StartGame()`, and every other script reacts to that on its own (see "What starts the level").

## Where it lives

- **Attached to:** `MenuManager` in `Level_1`, next to `MainMenu`.
- **Requires:**
  - the player, with `PlayerMovement` and `HamsterMenuFloat`;
  - the far planet (Level 1's `Planet`, which is also GameManager's `Earth`) and a `Far Planet Target` marker;
  - the near planet (`Earth`, with `PlanetMovement`) and a `Near Planet Target` marker;
  - the menu title and buttons;
  - a `GameManager` with **Start With Menu** ticked.

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `duration` | 1 | 2 | Seconds for the planets and UI to reach their targets |
| `hamsterDuration` | 2.5 | 2.5 | Seconds for the hamster's glide. Longer than `duration`, so it arrives last |
| `startDelay` | 0.5 | 0.5 | Seconds the hamster sits at its spot before the level starts |
| `hamster` | — | `hamter` | The player. The glide's target comes from its `StartPosition` / `StartRotation` |
| `farPlanet` / `farPlanetTarget` | — | `Planet` / `Far Planet Target` | The far planet and where it ends up (its gameplay spot) |
| `nearPlanet` / `nearPlanetTarget` | — | `Earth` / `Near Planet Target` | The near planet and the off-screen spot it slides to |
| `uiElements` | — | the title and the four buttons | Everything that flies back past the camera |
| `uiEndBehindCamera` | 2 | 2 | How far behind the camera the UI finishes, in world units |

The targets are empty marker GameObjects, so they can be lined up by eye in the Scene view, with no numbers
in code.

## How it works

### `Awake`: storing references, and skipping the menu on a restart

Gets and stores the references it uses later: the main camera, the hamster's `HamsterMenuFloat` and the
near planet's `PlanetMovement`. Calling `GetComponent` through a `Transform` field searches that object's
GameObject.

Normally that's all it does. But if the game has **already started**, the menu is skipped. That happens on a
restart (EndScreen sets `GameManager.SkipMenuOnce`), or on a level that doesn't use the menu. In that case
`Awake` does instantly what the transition would have done:

- switches off `HamsterMenuFloat`, before it has moved the player even once;
- snaps the far planet to its target (position, rotation and scale);
- hides the menu (`HideMenu`).

This relies on GameManager's `[DefaultExecutionOrder(-100)]`: its `Awake` runs first, so `HasStarted` is
already decided when this `Awake` checks it.

The far planet **must** be snapped here. Without the glide, nothing else would move it to its gameplay spot.
`LevelTimer` records where the Earth starts on the first frame of play, so it would record the menu spot and
the Earth would approach diagonally from there all level.

### `HideMenu`

Switches off the near planet and every UI element (`SetActive(false)`), instantly. It doesn't move anything;
the movement happens in `Animate`. It's called in two places:

- **at the end of `Animate`**, when they're already out of view, so Unity stops drawing them;
- **in `Awake` on a restart**, to make the menu vanish straight away.

It's its own method so both places share the same code.

### `Play`

Called by `MainMenu.OnStartPressed` when the Start button is pressed. It starts the **transition**, not the
level. The level starts at the end of `Animate`.

1. Switches off `HamsterMenuFloat` and the near planet's `PlanetMovement`. Both set their object's position
   or rotation every frame, so leaving them on would fight the transition.
2. Starts the `Animate` coroutine.

`MainMenu` also hides and locks the cursor on the same click, and reads `Duration` (a read-only property for
`duration`) to blink the Start text for that long.

### `Animate`: the transition itself

A coroutine, so it runs across many frames.

1. **Records where everything starts:** the hamster's position and rotation, the far planet's position,
   rotation and scale, and the near planet's position. These are `Vector3` and `Quaternion` **value types**, so
   each one is a copy that doesn't change as the objects move. The hamster's target is read once from
   `PlayerMovement.StartPosition` and `StartRotation`.
2. **Works out each UI element's end point:** the same x and y, but z is `uiEndBehindCamera` behind the
   camera. With the camera at z −11, that's z −13.
3. **The loop, once a frame,** until the longer of `duration` and `hamsterDuration` is up:
   - `t = SmoothStep(0, 1, elapsed / duration)` for the planets and UI;
   - `hamsterT = SmoothStep(0, 1, elapsed / hamsterDuration)` for the hamster;
   - each object is placed that far between its recorded start and its target. `Lerp` is used for positions
     and scale, and `Slerp` for rotations.
4. `HideMenu()`.
5. Waits `startDelay`.
6. `GameManager.StartGame()`.

**The easing.** `SmoothStep` starts slow, is fastest in the middle and ends slow. It clamps to 0–1, so on the
last frame everything lands exactly on its target, with no separate snap. With `duration` 2:

| Time | Eased `t` | Near planet (x from −69 to −200) |
| --- | --- | --- |
| 0 s | 0 | −69 |
| 0.5 s | 0.156 | −89 |
| 1 s | 0.5 | −134.5 |
| 1.5 s | 0.844 | −180 |
| 2 s | 1 | −200 |

**Why blend from the recorded start.** Each frame blends from the fixed start to the target. Blending from
wherever the object is *right now* would never quite arrive, and would ignore the easing curve.

**Why the UI spreads out.** The UI only moves straight back in z. How far from the centre something appears
on screen is its sideways distance divided by its distance from the camera. So as each element comes closer it
grows and slides outward, then disappears once it passes the camera. Anything exactly at the centre of the
screen grows without sliding outward.

**Two durations.** Once `t` reaches 1, the planets and UI sit at their targets while the hamster finishes.
`Mathf.Max(..., 0.01f)` keeps a duration of 0 from dividing by zero; 0 means "arrive immediately".

**The timeline** with the scene's values:

| Time | What happens |
| --- | --- |
| 0 s | Start pressed. Float and spin off; the transition begins |
| 2 s | The planets and UI arrive |
| 2.5 s | The hamster arrives; the menu is switched off |
| 3 s | `StartGame()`: the level starts |

### Why the hamster doesn't jump at the hand-over

The glide's target is `PlayerMovement`'s own start position and rotation, worked out from OrbitSettings and
`startAngle`. So on the first frame of play, `PlayerMovement` puts the hamster exactly where it already is. If
`startAngle` changes, the glide follows it automatically.

### What starts the level

`StartGame()` sets `GameManager.HasStarted` and starts `GameManager.TimeSinceStart`. Everything else reacts by
itself:

- **Shown at once:** the HUD (GameManager) and the orbit ring (OrbitRing).
- **Begins:** player control and the cursor lock (PlayerMovement), the Earth's approach (LevelTimer), the waves
  after the spawner's own delay (ObjectSpawner), the stats (RunStats) and the sky drift
  (SkyboxSpeedController).
- **Eases in from `TimeSinceStart`:** the exhaust flame (ExhaustFlame, `growTime`), the star stream
  (StarfieldStreamer, `rampTime`) and the core glow (SkyboxSpeedController, `glowGrowTime`).

## Talks to

- **MainMenu**: calls `Play` when Start is pressed, and reads `Duration` for the Start text's blink.
- **GameManager**: reads `HasStarted` in `Awake`, and calls `StartGame()` at the end of `Animate`.
- **PlayerMovement**: reads `StartPosition` and `StartRotation` for the glide's target.
- **HamsterMenuFloat**: switches it off when the transition starts, or in `Awake` on a restart.
- **PlanetMovement** (on the near planet): switches it off when the transition starts.

## Gotchas

- **Every slot must be filled.** An empty `hamster` or `nearPlanet` throws a `NullReferenceException` in
  `Awake` as soon as the scene loads. An empty `farPlanet` or target throws one when Start is pressed (or in
  `Awake` on a restart).
- **The far planet target should share the far planet's parent.** Scale is blended with `localScale`, so the
  planet only ends up the right size if both parents have the same scale. Duplicating `Planet` and stripping
  the copy's Mesh Renderer and Mesh Filter makes a correct target.
- **Don't put anything in `uiElements` that must stay visible.** `HideMenu` switches every element off. If
  the end-screen canvas went in here, the win and game-over screens would never show.
- **Keep the menu buttons out of GameManager's `hud`**, or they'd be hidden with the HUD during the menu.
- **The menu canvas must be Screen Space - Camera** (Render Camera = `Main Camera`). On a Screen Space -
  Overlay canvas the buttons have no depth, so moving them in z does nothing.
- **Start With Menu goes on Level 1's GameManager only.** Other levels leave it off and start straight away.
