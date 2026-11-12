# MenuNavigator

## Purpose

Lets a page of UI buttons be used with only a trackball and one button, with no mouse pointer: rolling the ball moves
the selection, and the button (Space) presses the selected item. There is one `MenuNavigator` per page (main menu,
credits, Game Over, Win, leaderboard window, and the name page, which has no buttons and only takes the top spot). Only the page on top reacts, so a page opened over another doesn't also
move the page under it.

## Where it lives

- **Attached to:** the root of each page (the object that is switched on and off to show the page).
- **Requires:**
  - The page's `Button`s, in the `buttons` list. Each needs a clearly different **Selected Color** (or Sprite
    Swap) in its Button component, or the selection can't be seen.
  - A scene `EventSystem` (the selection is stored in it).
  - The `Move` and `Button` actions of `PlayerInput.inputactions` (`InputActionReference`s).

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `buttons` | none | The page's buttons, in the order the selection moves through them |
| `layout` | Vertical | Vertical: rolling the ball down goes to the next button. Horizontal: rolling it right does. The other direction is ignored |
| `moveAction` | none | The Move action (mouse delta, which the trackball sends) |
| `selectAction` | none | The Button action (Space). Presses the selected button |
| `stepDistance` | 60 | Pixels of trackball movement per button. Lower = more sensitive |
| `deadzone` | 2 | Movement smaller than this in one frame is ignored, so a drifting ball can't move the selection |
| `wrapAround` | true | Moving past the last button goes to the first, and the other way round |
| `marker` | none | A triangle (UI Image) shown beside the selected button. Empty = no marker. Only the main menu uses it; the end screens have filled-in button areas |
| `markerOffset` | (−50, 0) | Where the marker sits, in canvas units, from the middle of the selected button's left edge. A negative x puts it further left |

## How it works

### The page stack (`openPages`, `IsTopPage`)

`openPages` is a `static` list shared by every `MenuNavigator`. A page adds itself in `OnEnable` and removes itself
in `OnDisable`. `IsTopPage` is true only for the last one added, which is the page most recently shown. Example:
the main menu is showing (`[Menu]`), Credits opens (`[Menu, Credits]`, only Credits reacts), Credits closes
(`[Menu]`, the menu reacts again).

### `OnEnable`

Adds the page to `openPages`, resets `accumulated`, enables the Move and Button actions (enabling an action that is
already enabled does nothing), and **hides and locks the cursor** (`Locked`, `visible = false`). Then, if there are
buttons, it selects the first one that can be used.

### `OnDisable`

Removes the page. If another page is now on top, it selects that page's current button again. The selection lives in
the `EventSystem`, so it would otherwise still point at a button on the closed page.

### `Select`, `PlaceMarker` and `FindUsable`

`Select(index)` stores the index, tells the `EventSystem` to select that button (which shows its Selected look, if
it has one), then runs `Canvas.ForceUpdateCanvases()` (a page that was just switched on hasn't been laid out yet)
and `PlaceMarker`.

`PlaceMarker` moves the `marker` triangle to the left-middle edge of the selected button. `GetWorldCorners` gives
the button's four corners in world space (0 = bottom-left, 1 = top-left), so the average of corners 0 and 1 is the
middle of the left edge. `marker.position` is set to it, then `markerOffset` is added to `anchoredPosition` (canvas
units). Example: a button at x = 12 with width 200 has its left edge at −88, so with the default offset the marker
sits at −138. It does nothing if there's no `marker`. `LateUpdate` calls it every frame for the top page, so the
marker follows the button if the layout or window changes. `LateUpdate` also switches the `marker` off whenever
its page isn't the top page (for example while Credits is open over the main menu) and back on when it is.

`FindUsable` is the search for the next button.
`FindUsable(from, direction)` walks `direction` (+1 or −1) from `from`, wrapping if `wrapAround` is on, and
returns the first button that exists, is `interactable` and is switched on. It tries at most `buttons.Length` times,
and returns `from` if nothing fits, so the selection stays put. The wrap uses `(index % length + length) % length`
because `-1 % 4` is `-1` in C#, not 3.

### `Update`

Runs only for the top page (and only if it has buttons):

1. **Roll.** The Move action's value is read. Only the axis that matches `layout` counts, and the sign is chosen so
   a positive `roll` always means "toward the next button": rolling down (negative y) for Vertical, right
   (positive x) for Horizontal.
2. **Accumulate.** A `roll` under `deadzone` is ignored. Otherwise it's added to `accumulated`, after zeroing it if
   the ball changed direction. When `|accumulated|` reaches `stepDistance`, the selection moves one usable button
   and `accumulated` goes back to 0.
3. **Press.** When the Button action is pressed this frame, a Submit event is sent to the selected button, the same
   as pressing Enter. The `Button` ignores it if it isn't interactable.

Worked example (Vertical, `stepDistance` 60, `deadzone` 2): frames of `move.y` −1, −20, −25, −30. The −1 is ignored;
`accumulated` goes 20, 45, 75, and at 75 the selection moves down one button and `accumulated` resets. Rolling back
up starts again from 0.

## Talks to

- **EventSystem** (Unity): stores the selected button and receives the Submit event.
- **Button** (Unity UI): reads `interactable`; presses via Submit.
- **EndScreen**: its panels each have a `MenuNavigator`. `EndScreen` no longer shows the cursor.
- **MainMenu**: its Start, Options, Credits and Exit buttons are navigated. `MainMenu.OnStartPressed` also locks the
  cursor, which is harmless.
- **UISFX**: subscribes to the static events `OnHighlighted` (raised in `Select`, so on every fresh highlight: a
  move, a page opening, or the page below becoming top again), `OnMoved` (the selection landed on a different button, not raised when a
  page opens or when nothing else is usable), `OnPressed` (Space on a button that can be pressed) and `OnBlocked`
  (Space on one that can't). Only the top page's `Update` raises them.
- **PlayerMovement**: no direct link. It ignores input before the start, after game over and after a win, so the
  same Space press can't dodge.

## Gotchas

- **Each `Button` needs a visible Selected Color.** Without it the selection works but can't be seen.
- **The marker must not be inside a Layout Group.** Make it a direct child of the page root, or the group will move
  it. Its anchors and pivot should be centred, and Raycast Target off.
- **`PlaceMarker` allocates a 4-item array each call,** every frame while a page with a marker is on top. Fine for a
  menu; keep one array in a field if it's ever used somewhere busy.
- **An empty `buttons` list does nothing** (no errors). A null entry is skipped.
- **The Unity UI pointer still exists at the centre of the screen** while the cursor is locked. If a button there
  looks hovered, clear the Point and Click bindings on the EventSystem's
  Input System UI Input Module.
- **Pages must be switched on and off to open and close.** The page stack is built in `OnEnable` and `OnDisable`.
  A page that is only hidden some other way (such as a CanvasGroup) stays on the stack and keeps reacting.
- **The first page is highlighted when it opens,** but if a page stays active across a scene reload, the stack is
  rebuilt by the reload's `OnEnable`/`OnDisable` calls as normal.
- **The actions are shared.** The script enables Move and Button but never disables them, because
  `PlayerMovement` uses them too.
