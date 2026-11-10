# LeaderboardPanel

## Purpose

The leaderboard window: the top 10 scores as one block of text, with a row highlighted when a score was just
added. It replaced the old stats overlay. It only displays what LeaderboardStore holds.

## Where it lives

- **Attached to:** `Leaderboard Overlay` under the `EndScreen` canvas in `Level_1`. It's switched off, and
  EndScreen opens it. The overlay has a `MenuNavigator` with one button, Close.
- **Requires:** the text to write into (`rowsText`), left-aligned with wrapping off so the columns line up.
- **Layout (set up in the Unity Editor):** the window is 950 × 895 with a Vertical Layout Group (padding 20,
  spacing 44, children centred, **Child Force Expand off**), so the title, rows and Close button keep their own
  sizes and the stack sits in the middle of the window. The title and rows have a Layout Element with
  Flexible Width 1, so they still fill the window's width (the `<pos>` columns are a percentage of it). Close has a
  Layout Element with a fixed 205 × 100, like the end-screen buttons.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `rowsText` | Leaderboard Rows | The text the rows are written into, one line per place |
| `highlightColor` | gold | The colour of the row just added. The other rows keep the text's own colour |
| `scoreDigits` | 6 | How many digits a score is padded to, so the scores line up. 6 gives 000123, as on the score total (range 1 to 9) |

## How it works

### `Show(highlightRank)` and `Close`

`Show` stores the place to highlight (0 = first, -1 = none) and switches the object on. Storing it first means
`OnEnable` already knows which row to colour. `Close` clears the highlight and switches the object off.

### `OnEnable` and `Refresh`

The window refreshes every time it's switched on, so it always shows the current saved list. `Refresh` builds the
text, one line for each of `MaxEntries` places:

- A place with an entry shows its name and its score, padded with `ToString("D" + scoreDigits)`.
- A place nobody has reached shows `---` and zeros.
- Each line is `1.<pos=18%>NAME<pos=45%>000123`. `<pos=18%>` is a TextMesh Pro tag that moves the next text to 18% of
  the box's width, so every name and score starts at the same spot and forms a column.
- The highlighted line is wrapped in `<color=#RRGGBB>...</color>`, using `highlightColor`.

## Talks to

- **LeaderboardStore**: reads `Entries` and `MaxEntries`.
- **EndScreen**: calls `Show` and `Close`.

## Gotchas

- **Keep `rowsText` left-aligned with wrapping off,** or the `<pos>` columns stop lining up.
- **Long scores push the columns.** `scoreDigits` pads, it doesn't cut. A score with more digits than
  `scoreDigits` is shown in full, and its line is longer than the others.
- **It doesn't refresh while open.** The text is written in `OnEnable`, so a score added while the window is
  already showing wouldn't appear until it's reopened. EndScreen only ever opens it after the entry is saved.
