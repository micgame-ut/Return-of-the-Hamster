# PooledEffect

## Purpose

Sits on **one pooled effect copy** (a particle effect such as Asteroid Explosion). When its particles finish,
it switches the copy off and announces it, so EffectPool can reuse it, instead of the effect destroying
itself. It makes no copies itself; EffectPool does that.

## Where it lives

- **Attached to:** the root of each effect copy, **added at runtime** by EffectPool's `CreateEffect`. The
  root must have the ParticleSystem, because Unity sends the stop message to that object.
- **Requires:** a ParticleSystem on the same GameObject.

## Inspector settings

None. It's only ever added in code.

| Member | What it is |
| --- | --- |
| `Prefab` | Which effect prefab this is a copy of (public get, private set). EffectPool's free-list key |
| `Particles` | This copy's own ParticleSystem, cached so EffectPool can call `Play()` without a `GetComponent` |
| `OnFinished` | Event fired when the particles are done and the copy is free |

`Particles` has a private set so only PooledEffect decides which system it points to. Other scripts can read
it and call methods on it (it's a reference type), but can't swap it for another object's particles, which
would make the pool play one effect while waiting for another to finish.

## How it works

### `Init`

Called once by EffectPool right after it creates the copy. Records `Prefab`, caches `Particles`, and sets
the copy's **Stop Action** to **Callback**. The prefab's own Stop Action (Destroy) isn't changed, only this
copy's. `main` is a module handle, so setting a value on it changes the real system.

Without Callback, the copy would destroy itself when finished, never fire `OnFinished`, never come back, and
the pool would silently make a new copy for every effect: no errors, just no benefit.

### `OnParticleSystemStopped`

A **Unity message**, like `Update`: Unity calls it by name when this system stops, because Stop Action is
Callback. It switches the copy off, then fires `OnFinished`, so the copy is already hidden when it goes back
into the free list.

## Talks to

- **EffectPool**: creates it, calls `Init`, plays it through `Particles`, and listens to `OnFinished`.

## Gotchas

- **The ParticleSystem must be on the root.** Stop Action Callback only messages the object that has the
  system.
- **Child particle systems don't trigger the callback.** Only the root system's stop does.
