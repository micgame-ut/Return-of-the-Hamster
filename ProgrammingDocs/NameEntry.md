# NameEntry

## Purpose

The three-letter, arcade-style name page. It opens after a run whose score makes the leaderboard. With only a
trackball and one button, rolling the ball down changes the current letter forward and rolling up changes it back,
the button locks the letter and moves to the next slot, and rolling left steps back a slot to fix one. Locking the
third letter submits the name.

## Where it lives

- **Attached to:** the `Name Entry` page under the `EndScreen` canvas in `Level_1`. The page is switched off, and
  EndScreen switches it on. It also carries a `MenuNavigator` with no buttons.
- **Requires:**
  - Three texts for the letters (`letterSlots`), left to right.
  - The Move and Button actions.
  - The page's own `MenuNavigator` (empty). It makes this page the top page, so the trackball and button stop
    driving the end panel's buttons underneath while a name is being typed.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `letterSlots` | the 3 letter texts | The three texts that show the letters, left to right |
| `activeColor` | white | The colour of the letter being chosen |
| `inactiveColor` | grey | The colour of the letters not being chosen |
| `moveAction` | Player 1/Move | The Move action (the mouse delta, which the trackball sends) |
| `selectAction` | Player 1/Button | The Button action (Space). Locks the current letter |
| `stepDistance` | 120 | Pixels of trackball movement that change a letter by one, or step back a slot. Lower = more sensitive |
| `deadzone` | 2 | Movement smaller than this in one frame is ignored, so a drifting ball can't change anything |
| `inputDelay` | 0.5 | Seconds after the page opens before it reacts to input, so the press that skipped the score count doesn't lock a letter |

## How it works

### The working values

`letters` holds each slot's letter as a number (0 = A to 25 = Z). `slot` is the slot being chosen. `forward` and
`backward` are running totals of trackball movement. `openedAt` is the unscaled time the page opened.

### `OnEnable`

Runs every time the page is switched on, so each run starts fresh: all letters at 0 (`AAA`), the first slot
active, both totals at 0, `openedAt = Time.unscaledTime`. It enables the two actions (harmless if they're already
on) and calls `Draw`.

### `Draw`

Writes each slot's letter and colours it: `activeColor` for the slot being chosen, `inactiveColor` for the rest.
A letter number becomes a character with `(char)('A' + letters[i])`.

### `Update`

Does nothing until `inputDelay` has passed (measured in unscaled time, because the end screen runs with
`Time.timeScale` at 0). Then it reads the trackball:

- **Which axis counts:** whichever movement is larger this frame (`|x| > |y|`), so a diagonal roll can't change a
  letter and step back at once.
- **Sideways:** only rolling left counts. Movement to the left adds to `backward`; rolling right, or a drift
  under the deadzone, resets it to 0. At `stepDistance` it calls `StepBack` and resets.
- **Vertical:** rolling down is negative `y`, so `roll = -y` is positive toward the next letter. Movement under
  the deadzone is ignored. Rolling the other way throws away what was built up. At `stepDistance` it calls
  `ChangeLetter(+1)` or `ChangeLetter(-1)` and resets.
- **The button:** `WasPressedThisFrame` on the select action calls `LockLetter`.

Example (`stepDistance` 120): frames of `y` of -1, 50, 50, 50 give the -1 ignored (under the deadzone), then `forward`
of 50, 100, 150, and at 150 the letter moves forward and `forward` resets to 0.

### `ChangeLetter(direction)`

`letters[slot] = ((letters[slot] + direction) % 26 + 26) % 26`, so A going back wraps to Z and Z going forward wraps
to A (adding 26 first keeps the remainder from going negative). Then it redraws and raises `OnLetterChanged`.

### `StepBack`

Moves to the previous slot, unless already on the first. Redraws and raises `OnLetterChanged`. The letters
in later slots keep their values.

### `LockLetter`

Raises `OnLetterLocked`. On slots 1 and 2 it moves to the next slot and redraws. On the last slot it builds
the name from the three letters, switches the page **off first**, then raises `OnNameSubmitted` and
`OnSubmitted(name)`. Closing first means the page below is on top again before EndScreen opens the leaderboard.

## Events

- **`OnSubmitted(string)`** (instance): the finished name. EndScreen listens, and saves it.
- **`OnLetterChanged`**, **`OnLetterLocked`**, **`OnNameSubmitted`** (static): for sound. UISFX plays its
  `letterChange`, `letterLock` and `nameSubmitted` slots. The last lock raises both `OnLetterLocked` and
  `OnNameSubmitted`.

## Talks to

- **EndScreen**: switches this page on, and listens to `OnSubmitted`.
- **MenuNavigator**: the page's own empty one takes the top spot in the page stack.
- **UISFX**: subscribes to the three static events.
- **Input System**: reads the Move and Button actions.

## Gotchas

- **`letterSlots` must be assigned.** `OnEnable` reads its length, so an empty list throws when the page opens.
- **The page needs its empty `MenuNavigator`.** Without it, the buttons on the end panel below still react to the
  same trackball and button.
- **The input delay is real.** Pressing the button in the first half second does nothing, on purpose. Keep it at
  least as long as it takes to release the button that skipped the score count.
- **"AAA" can be submitted.** There is no check on the name.
- **A name is exactly the number of slots.** Three, with the scene's three texts. A fourth text would give a
  four-letter name, but the leaderboard columns are laid out for three.
