# OrbitRing

## Purpose

Draws the player's orbit as a faint circle, plus a colored **danger arc** on it wherever an incoming
asteroid will cross the ring. Red means it will hit you, green means you're fast enough to smash it. The
arc fades in as the asteroid gets close.

This is there because depth is hard to judge in this game. The camera's field of view changes with speed
(see PlayerCamera), so it's hard to tell from the asteroid alone whether it's about to reach you or is
still far off. The ring and arcs are always drawn at the player's depth, so they show exactly which part
of the circle is about to be dangerous.

## Where it lives

- **Attached to:** the `Game Manager` GameObject in `Game.unity`. The lines it draws are created as child
  objects of it when the game starts.
- **Requires:** `GameManager.Instance` with `player` assigned. Without a player, it draws nothing.

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `lineMaterial` | none | none | Material for the lines. If empty, it makes one with the built-in `Sprites/Default` shader |
| `ringColor` | white, 15% opacity | grey, about 5% opacity | Color of the orbit circle |
| `ringWidth` | 0.05 | 0.05 | Thickness of the orbit circle |
| `ringSegments` | 96 | 96 | How many straight pieces make up the circle. More is smoother |
| `dangerColor` | red | red | Arc color for an asteroid that will hit the player |
| `breakableColor` | green | green | Arc color for an asteroid the player would smash right now (fast enough, or invincible) |
| `arcWidth` | 0.15 | 0.1 | Thickness of the danger arcs |
| `arcOffset` | 0.6 | 0.5 | How far outside the ring the arcs are drawn, so they don't cover the hamster |
| `warnTime` | 1.5 | 1.5 | Seconds before impact that an arc starts fading in |
| `playerRadius` | 0.5 | 0.1 | The player's size, added to the asteroid's size to work out how wide the danger zone is. While PlayerShell's hitbox is on and bigger, its size is used instead |

## How it works

### Drawing lines (`CreateLine` and `SetArc`)

Everything is drawn with **LineRenderers**. A LineRenderer takes a list of points and draws a line through
them. A circle is really a lot of short straight lines, so with enough points it looks round.

- **`CreateLine`** makes a new child GameObject with a LineRenderer, set up to use the line material, no
  shadows, and rounded ends.
- **`SetArc`** fills a LineRenderer with points along part of a circle, from one angle to another. Each
  point uses the same cos/sin placement as PlayerMovement: `center + (cos(a), sin(a)) × radius`. It
  splits the angle range into `segments` equal steps and places a point at each one.

  A full circle is set to `loop`, so the LineRenderer joins the last point back to the first by itself.
  That's why it needs one fewer point than an open arc.

### `Start`

1. Get the player from GameManager, and the PlayerShell on it (if there is one).
2. If no line material was assigned, make one from the `Sprites/Default` shader. It's also stored in
   `createdMaterial`, so `OnDestroy` can destroy it. A material made with `new` isn't cleaned up with the
   scene. Only this one is destroyed, never an assigned asset.
3. Create the orbit ring line.

### `LateUpdate`: every frame

It runs in `LateUpdate` so the player and asteroids have already moved this frame.

**1. Draw the orbit ring** at the player's orbit center, radius, and depth (z). The player gets the center
and radius from `OrbitSettings`. It's redrawn every frame so it follows the asset if it's tuned in Play mode.

**2. For each asteroid in `AsteroidBehavior.Active`**, decide whether it gets an arc:

- **Skip it if it's still warning.** Only moving asteroids count.
- **Work out its reach.** `reach = asteroid's HitRadius + playerReach`. The player's center is on the ring,
  so the player gets hit anywhere their center comes within `reach` of the asteroid's center.
  `playerReach` is worked out once per frame, before the loop: `Max(playerRadius, shell.HitRadius)`. While
  PlayerShell's hitbox is on (shield or invincible), the player is as big as the shell, so the arcs widen to
  match. It's `Max`, not a sum, because the shell surrounds the ship from the same center.
- **Skip it if it has already passed.** `dz` is how far in front of the player it is along z. Once it's
  more than `reach` behind the player, it's gone.
- **How visible is it?** `timeToImpact = dz / ApproachSpeed` (time = distance ÷ speed). Then
  `alpha = 1 − timeToImpact / warnTime`, capped between 0 and 1. With `warnTime = 1.5`:

  | Time to impact | Alpha (opacity) |
  | --- | --- |
  | 1.5s or more | 0 (skipped) |
  | 0.75s | 0.5 |
  | 0s (at the player's depth) | 1 (fully visible) |

  It uses time, not distance, so the warning always gives the same amount of time to react, no matter how
  fast the asteroid is coming.
- **Which part of the ring does it cover?** `TryGetArc` works this out (see below). If the asteroid doesn't
  reach the ring at all, it's skipped.
- **Draw the arc.** Colored green if `CanBreak` (which includes the player being invincible), otherwise
  red, faded by `alpha`. It's drawn at
  `radius + arcOffset`, a little outside the ring, but covers the angles worked out from the real ring.

**3. Hide any leftover arcs.**

### Reusing arcs (pooling)

Creating and destroying GameObjects is slow, and destroyed objects leave memory for Unity to clean up
later. So arcs are never destroyed. `arcs` is a list of every arc line made so far, and each frame they're
handed out in order: the first asteroid that needs one gets `arcs[0]`, the next gets `arcs[1]`, and so on.
A new line is only created when every existing one is in use. Any not used this frame are switched off.

An arc isn't tied to one asteroid. The same line can show a different asteroid next frame. This is called
**object pooling**.

### `TryGetArc`: which part of the ring is in danger

Given where the asteroid is (relative to the orbit center), the ring's radius, and the reach, this finds
the stretch of ring within `reach` of the asteroid. It returns that stretch as a middle angle and a half
width, so the arc runs from `midAngle − halfAngle` to `midAngle + halfAngle`.

**The middle.** `midAngle = Atan2(y, x)`, the angle pointing from the center to the asteroid. The danger
zone is centered on the part of the ring right "under" the asteroid.

**Does it touch the ring at all?** `d` is the asteroid's distance from the center. If it's more than `reach`
inside or outside the ring (`|d − radius| >= reach`), it can't touch the ring, so there's no arc.

**Does it cover the whole ring?** If the asteroid is big enough, or close enough to the center, every point
on the ring is within reach. Then `halfAngle` is π (180° each way, a full circle).

**Otherwise, how wide is the arc?** Picture a triangle with three corners:

- **O**, the orbit center
- **A**, the asteroid
- **P**, the point where the danger zone ends on the ring

All three side lengths are known:

- O to A is `d`
- O to P is `radius` (P is on the ring)
- A to P is `reach` (P is exactly at the edge of the asteroid's reach)

The **law of cosines** gives an angle in a triangle from its three sides. The angle at O, between the line
to the asteroid and the line to P, is:

`cos(angle) = (d² + radius² − reach²) / (2 × d × radius)`

That angle is `halfAngle`, how far the danger zone spreads each way from `midAngle`. `Acos` turns the
cosine back into an angle, just as `Atan2` turns an x/y back into an angle. The value is clamped between
-1 and 1 first, because rounding errors could push it just outside that range, where `Acos` has no answer.

## Talks to

- **GameManager**: gets the player in `Start`.
- **PlayerShell**: found on the player in `Start`; reads `HitRadius` once per frame.
- **PlayerMovement**: reads `OrbitCenter`, `CircleRadius`, and the player's position every frame.
- **AsteroidBehavior**: loops over the static `Active` list and reads `IsMoving`, `HitRadius`,
  `ApproachSpeed`, and `CanBreak`.

## Gotchas

- **Asteroids are treated as circles.** `HitRadius` uses whichever of the collider's width or height is
  bigger, so an oddly shaped collider gets a slightly bigger arc than the real hit area.
- **Arcs show where a Wave asteroid is now, not where it will be.** A Wave asteroid keeps swinging along
  the ring as it approaches, so its arc slides back and forth with it. Where the arc is when it's fully
  visible is where the asteroid will hit.
- **The fallback material uses `Shader.Find`.** A shader only used through `Shader.Find` can be left out
  of a build. It works in the editor either way. If lines are missing or pink in a build, assign
  `lineMaterial` in the Inspector.
