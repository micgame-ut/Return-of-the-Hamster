# PaletteApplier

## Purpose

Switches the game's colours to a `ColorPalette`. It sets the material colours listed in `PaletteTargets`, the rock outline,
far fade and Lance beam colours on `SpawnableTemplate`, and the UI text and button colours, and it tells the scripts that keep
their own colours (through an event) to follow too. It remembers every original colour, so the game's own palette can be
restored exactly, and it puts everything back when you stop playing. The Tab overlay's **Palette** row drives it.

## Where it lives

- **Attached to:** nothing in a scene. It makes its own GameObject once, as the game starts (`[RuntimeInitializeOnLoadMethod]`),
  and keeps it between scenes (`DontDestroyOnLoad`), like `TuningOverlay`.
- **Requires:** the palette assets in `Assets/Resources/Palettes` and `Assets/Resources/PaletteTargets.asset`. Without either, it switches
  itself off and does nothing. `SpawnableTemplate` is loaded from `Resources/SpawnableTemplate` (the template colours are
  skipped if it's missing).
- **Switch:** the constant `Enabled` at the top. Set it to `false` to leave palettes out of a build.

## Settings

There are no Inspector fields. The palettes are the assets in the folder, ordered with the original first and the rest by name.

## How it works

### `Awake`

Loads the palettes and the targets list (and switches itself off if either is missing), sorts the palettes, then reads the
original colours (`CaptureOriginals`, `CaptureTemplate`) and sets `Instance`.

### `CaptureOriginals`

For every entry with a material that has the property, stores the colour in `originalColors`, and the glow colour in
`originalEmissions` if the entry has an emission property. These are what Neon Arcade, and quitting, put back. Entries
with a missing material or property are left out, so later code skips them.

### `ColorFor(entry, palette)`

The colour an entry gets: the palette's colour for `role`, blended toward `role2` by `blend`, then shifted in hue by
`hueShift` and multiplied in brightness (done in HSV). If the original was brighter than 1 (a glow), the result is scaled
so its brightest channel matches the original's: the palette picks the hue, the game keeps the strength. Alpha is
kept from the original.

### `ApplyMaterials`

For each captured entry: the original colour for the original palette (or a `None` role), otherwise `ColorFor`, written
with `SetColor`. An entry with a glow gets its emission colour rebuilt as the new colour at the original glow strength.
It also writes the colour to the live skybox clone (see `LiveCopy`).

### `LiveCopy`

`SkyboxSpeedController` swaps the skybox for a clone of the sky material when a level starts, and the clone is what's
drawn. `LiveCopy(material)` returns the clone if its name is `<material name>(Clone)`, so the sky changes at once. A new level
makes a fresh clone from the already-changed asset, so that case works anyway.

### `ApplyTemplate`

Sets `SpawnableTemplate`'s `outlineTooSlow` ← Danger, `outlineCanBreak` ← Breakable, `farColor` ← 30% of the way from Space to
Rock, and `lanceBeamColor` ← Lance at its original strength. The original palette puts the captured values back.

### `ApplyUi`

Sweeps every `TMP_Text` (including inactive ones): only text that was white follows the palette's `text` colour. Every
`Button` whose "selected" look is a visible colour (not see-through, not grey) takes `uiAccent`. The first time it sees an
object it stores the original colour. It runs on every palette change, and again when a scene loads
(`OnSceneLoaded`), so new scenes' text and buttons get the chosen palette.

### `Select`, `SelectByName`, `Next`, `Previous`

`Select(i)` wraps `i` into the list (`(i % n + n) % n`), then applies the palette and remembers it in `appliedIndex`.
`SelectByName` finds a palette by `displayName` (an unknown or empty name gives the first), and does nothing if it's
already applied, because the Tab overlay calls it on every slider change.

### `Apply`

Runs `ApplyMaterials`, `ApplyTemplate` and `ApplyUi`, sets the static `Current`, and raises the static event `OnChanged`.

### `Current` and `OnChanged` (static)

Scripts that keep their own colours (`OrbitRing`, `BoostRing`, `LeaderboardPanel`, `OverdriveTally`, `NameEntry`) subscribe to
`OnChanged` in `OnEnable`, unsubscribe in `OnDisable`, and apply `Current` straight away in `OnEnable`. That way an object
created after a palette was chosen (a twin copy of the ring) still gets it. `Current` is `null` until a palette is chosen, and
a script treats `null` like the original palette.

### `OnApplicationQuit`

Writes every original colour back into its material, and the template's four colours back. Material and `Resources` assets keep
changes made in Play mode after you stop, so without this the project's real colours would be left changed.

## Talks to

- **ColorPalette, PaletteRole, PaletteTargets**: the data it reads.
- **SpawnableTemplate**: reads and sets four colours.
- **SkyboxSpeedController**: no direct link, but `LiveCopy` depends on the clone name it makes.
- **TuningOverlay**: calls `SelectByName` from `Apply`, and `Next`, `Previous` and `CurrentName` from the Palette row.
- **OrbitRing, BoostRing, LeaderboardPanel, OverdriveTally, NameEntry**: raise `OnChanged` to them.

## Gotchas

- **A crash or a forced stop skips the restore.** The assets would keep the palette's colours, and the next run would
  capture those as the originals. `git restore` on the `.mat` files and `SpawnableTemplate.asset` fixes it.
- **`LiveCopy` depends on the clone's name.** If `SkyboxSpeedController` stops using `Instantiate`'s default name, the sky stops
  following live changes (new levels still work).
- **Lance colours change when Lance next starts or stops,** not instantly, if you switch palettes while Lance is on.
- **The leaderboard highlight changes the next time the window opens,** because it's written into the text when it opens.
- **The UI dictionaries keep entries for destroyed objects** after a scene change. They're small, and harmless here.
- **Statics:** `Current` and `OnChanged` are `static`. With Domain Reload turned off in the Editor's Play Mode settings, they
  would survive between runs.
- **Not covered:** the black hole, frost, the planet atmosphere, the flame gradients, the pickup particle bursts and any colour
  written in code (`Color.red` and the like). The colour of the 3D title face is its own script (`TitleFaceFlash`).
