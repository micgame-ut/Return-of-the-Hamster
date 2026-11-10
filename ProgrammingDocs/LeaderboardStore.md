# LeaderboardStore

## Purpose

The high-score list. It keeps the best 10 scores with a three-letter name each, best first, and saves them on the
machine running the game, so every player at that machine shares one board. It decides whether a score makes
the list (`Qualifies`) and adds it in the right place (`Submit`). Showing the list is LeaderboardPanel's job, and
asking for the name is NameEntry's.

## Where it lives

- **Attached to:** nothing. It's a `static class`, so there is no object in the scene.
- **Requires:** nothing. It saves with `PlayerPrefs`, like RunStats.

## Inspector settings

None. `MaxEntries` is a constant in the script (10).

## How it works

### The data

- **`Entry`** (`[Serializable]`): a `name` and a `score`.
- **`Data`** (private, `[Serializable]`): a wrapper holding `List<Entry> entries`. `JsonUtility` can only turn an
  object with fields into JSON, not a bare list, so the list sits inside this class.
- **`data`**: the loaded list, kept in a `private static` field.
- **`Entries`**: a read-only view of the list (`IReadOnlyList<Entry>`). Other scripts can read and count it but
  can't add to it, so `Submit` is the only way in.

### `Load` (private)

Returns `data` if it's already loaded. Otherwise it reads the JSON string saved under the key `Leaderboard` and
turns it into a `Data` with `JsonUtility.FromJson`. An empty save gives an empty list. A damaged save (the JSON
throws `ArgumentException`) is caught and also gives an empty list, so a bad save can't break the end screen.

### `Save` (private)

Writes the list as JSON into `PlayerPrefs` under the same key and saves.

### `Qualifies(score)`

True if the score is above 0 **and** either the list has fewer than `MaxEntries` entries or the score is higher
than the last (lowest) entry. The `||` stops at the first true side, so on an empty list `list[list.Count - 1]`
is never read.

### `Submit(name, score)`

If the score doesn't qualify, returns -1. Otherwise it finds the place and inserts the entry:

1. `rank` starts at 0 and moves down past every entry whose score is the **same or higher**, so a tie keeps the
   earlier entry on top.
2. The entry is inserted at `rank`.
3. If the list is now longer than `MaxEntries`, the last entry is removed. That's always the old 10th.
4. Saves, and returns `rank` (0 = first place).

Example: with `500, 300, 300`, submitting 400 gives rank 1, 300 gives rank 3 (below both 300s), and 600 gives
rank 0.

### `Clear`

Empties the in-memory list and deletes the save. For testing; nothing in the game calls it.

## Talks to

- **EndScreen**: calls `Qualifies` and `Submit`.
- **LeaderboardPanel**: reads `Entries` and `MaxEntries`.
- **PlayerPrefs** (Unity): the saved JSON string, key `Leaderboard`.

## Gotchas

- **The save is per machine and per Windows user.** `PlayerPrefs` lives in the registry on Windows, so a new
  computer starts with an empty board. To reset the board, call `LeaderboardStore.Clear()` (for example from a
  temporary button or `execute_code`), or delete the `Leaderboard` key.
- **Static data survives until the game quits.** The list is loaded once and kept, so if the save is deleted from
  outside while the game runs, the list in memory doesn't change. `Clear` resets both.
- **A tie goes below the earlier score.** Two players with the same score are ranked by who got there first.
- **Names are not checked.** `Submit` stores whatever string it is given; NameEntry only ever gives three letters.
