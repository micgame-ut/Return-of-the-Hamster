# PaletteRole

## Purpose

A list of the colour roles in a `ColorPalette`. A `PaletteTargets` entry names a role, so "this colour follows Danger" is
a dropdown choice, not a text string that a typo could break.

## Where it lives

- **It's an `enum`** (a named list of options) in its own file. It's not attached to anything.

## Values

`None`, `Space`, `NebDeep`, `NebMid`, `NebBright`, `NebCore`, `Danger`, `Breakable`, `Phased`, `Rock`, `Player`, `Ring2`,
`Health`, `Wild`, `Lance`, `Warp`, `Slim`, `UiAccent`, `Text`. They match the fields of `ColorPalette` one to one (see
[ColorPalette.md](ColorPalette.md)).

## How it works

`None` is the first value on purpose, so it's the number 0. An entry that hasn't been filled in does nothing, instead of
silently following `Space`. `ColorPalette.Get(None)` returns white, and `PaletteApplier` leaves an entry with role `None`
at its original colour.

## Talks to

- **ColorPalette**: `Get(role)` has one case per value.
- **PaletteTargets**: each entry has a `role` and a `role2`.

## Gotchas

- **Adding a role** means adding a field to `ColorPalette`, a case to `Get`, a value here, and updating every palette asset.
- **Don't reorder the values.** Unity saves an enum as a number, so reordering would change what saved entries mean.
