# EffectPool

## Purpose

Keeps and reuses particle effects (explosions, pickup effects) instead of creating and destroying one every
time. It saves all the pooled copies, keeps a free list for each effect, and hands a copy out whenever one is
needed. Any script plays an effect with `EffectPool.Play(prefab, position)`, the same idea as
`CameraShake.Shake`.

## Where it lives

- **Attached to:** the `Game Manager` object in `TestGame.unity`. Effect copies are kept as its children.
- **Requires:** a SpawnableTemplate for prewarming (optional). Without a pool in the scene, `Play` still
  works by falling back to `Instantiate`.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `template` | none | The SpawnableTemplate whose `explosionPrefab` and `pickupEffectPrefab` are prewarmed (scene: `SpawnableTemplate.asset`) |
| `prewarmEffects` | 5 | Copies of each effect made at level start |

Both template fields currently point to the same Asteroid Explosion prefab, so 5 copies are made in total:
the free lists are keyed by prefab, so two references to one prefab share one list.

## How it works

`freeEffects` is a `Dictionary` from effect prefab to a `Stack` of free copies (like labelled bins, one per
prefab). `instance` is a private static reference to the pool, used by `Play`.

### `Awake` and `OnDestroy`

`Awake` registers this pool as `instance`, then prewarms both template effects. `OnDestroy` clears
`instance` when the scene unloads (only if it's still this pool), so the static field never keeps a
destroyed pool and its dead copies alive.

### `Prewarm`

Skips a missing prefab, then makes copies until that effect's free list holds `prewarmEffects`.

### `GetFreeList`

Returns an effect's free list, making an empty one the first time (`TryGetValue`).

### `CreateEffect`

Makes a copy under the pool's object and **switches it off straight away** (the prefab has Play On Awake,
so leaving it on would play an explosion at the pool's position at level start, and the copy would end up in
the free list twice). Then adds a PooledEffect, calls its `Init`, and subscribes `HandleFinished` to its
`OnFinished`.

### `HandleFinished`

A copy's particles are done, so it goes back onto its effect's free list.

### `Play` (static)

1. No prefab: nothing to play.
2. No pool in the scene: `Instantiate`, as before pooling. That copy keeps Stop Action Destroy and cleans
   itself up.
3. Otherwise: take a free copy, or make one if all are busy (no limit). **Move it first**, then switch it on
   and play it (`Play(true)` includes child systems). Moving first matters for World-space effects, whose
   particles would otherwise start at the copy's previous spot. The explosion uses Local space, so it would
   look fine either way.

`Play` is static but can call the private `GetFreeList` and `CreateEffect` on `instance`, because private
means private to the class.

## Talks to

- **PooledEffect**: adds one to each copy, calls `Init`, plays `Particles`, listens to `OnFinished`.
- **SpawnableTemplate**: reads `explosionPrefab` and `pickupEffectPrefab` to prewarm.
- **AsteroidBehavior** and **PowerUp**: call `Play` for the explosion and pickup effects.

## Gotchas

- **Only one pool per scene.** `[DisallowMultipleComponent]` stops two on one object, but a second pool on
  another object would replace `instance`.
- **Effects must have their ParticleSystem on the root,** for PooledEffect's stop callback.
- **The player's death explosion isn't pooled.** PlayerDeath uses `Instantiate`; it happens once per level.
- **Copies are never destroyed during the level.** The pool only grows (to the most effects ever playing at
  once) and is cleaned up with the scene.
