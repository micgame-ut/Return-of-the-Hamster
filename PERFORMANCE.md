# Performance

Things to optimize, ranked by expected payoff. Found by reading the code and settings, not by profiling, so
confirm with the Unity Profiler and Frame Debugger before and after each change. Each item is a change to
existing code or settings, so follow [AGENTS.md](AGENTS.md) (explain first, one snippet at a time, approval).

**Keep the full look on the top quality level.** A change that only makes things faster without changing how
the game looks goes in for every quality level. A change that lowers visual fidelity (fewer ray steps, lower-
resolution sky, softer or shorter shadows) only goes on the lower quality level (*PC Low*), and *PC High* keeps
the current look. Items marked *(visual)* below are the ones this applies to.

**Every visual setting is tunable.** Each one gets a High and a Low value in the Inspector, with a tooltip that
says what the high and low ends look like and what they cost. Settings that live in Unity's own assets (the URP
asset) are tuned there instead.

Check an item off when it's done and tested in Play mode.

## High impact

- [x] **1. Sky shader cost.** `Starry Sky Volume` raymarches the whole screen every frame. With the material's
  current values (Ray Steps 16, Cloud Octaves 1), Nebula Volume does 16 × (1 + 1) + 2 = about 34 noise reads
  per pixel, and Tube Streams adds 6 more. Cloud Octaves is already at its cheapest (Cloud Detail is 0, so
  extra octaves would cost time but add nothing), which leaves Ray Steps as the main setting. See [StarrySkyVolume.md](ProgrammingDocs/StarrySkyVolume.md) and
  [NebulaVolume.md](ProgrammingDocs/NebulaVolume.md).
  - [x] *(visual)* Lower Ray Steps (8–12), and compare the look. Done: `GraphicsQuality` sets 8 on PC Low.
    PC High was later raised to 24 (scene value) for a smoother sky, since baked noise made each step cheaper.
  - [x] Replace procedural noise with a pre-baked noise texture (3D texture for the volume). Done:
    `CloudNoiseBaker` (Editor menu) bakes `CloudNoise3D` (64³, R8); `NebulaVolumeNoise` reads it with one sample
    per noise read, on both levels. It looked just as good, so it isn't *(visual)* and the math path was removed.
    Also added: `SkyboxSpeedController.randomStartRange`, so each load starts at a different sky.
  - [x] *(visual)* Render the sky at a lower resolution, since it's soft anyway. Done: `PC_RP_LowAsset` Render
    Scale 0.75 with FSR 1.0 upscaling (whole 3D image, not just the sky; UI unaffected). PC High stays at 1.
- [x] **2. Uncapped frame rate.** Done: VSync Count set to Every V Blank on both quality levels. `vSyncCount: 0` in QualitySettings and no `Application.targetFrameRate`, so
  the GPU renders as many frames as it can, mostly spent on the sky. Heats up and throttles the mini PC.
- [x] **3. URP settings heavier than needed.** [PC_RPAsset.asset](Assets/Settings/PC_RPAsset.asset) has 4
  shadow cascades, 2048 shadow maps, soft shadows, and a 50-unit shadow distance. The camera is fixed and play
  happens within about 20 units.
  - [x] *(visual)* Try 1–2 cascades and a shorter shadow distance. Done: PC Low has its own
    [PC_RP_LowAsset.asset](Assets/Settings/PC_RP_LowAsset.asset) with 1 cascade, 30 distance, 1024, soft
    shadows off. PC High keeps `PC_RPAsset`.
  - [x] Check whether the Earth camera in `TestGame` renders a full pass of its own (skybox, shadows) or is an
    Overlay camera. Done: Earth Camera is the Base camera and Main Camera is an Overlay in its stack, so the sky
    is drawn once. Earth Camera's Render Shadows turned off and its Culling Mask set to Earth only (both levels,
    no visual change), which removed its shadow pass.

## Medium impact

- [ ] **4. Object pooling for spawned objects.** [ObjectSpawner.cs](Assets/Scripts/ObjectSpawner.cs)
  `Instantiate`s a whole pattern each wave, and asteroids, power-ups, explosions, and pickup effects are all
  `Destroy`ed when done. That causes garbage collection and frame spikes. `AsteroidBehavior.OnEnable` already
  resets its state, so it's close to pool-ready.
- [ ] **5. Starfield particle copy.** [StarfieldStreamer.cs](Assets/Scripts/StarfieldStreamer.cs) `LateUpdate`
  copies up to 3000 particles out and back (`GetParticles` / `SetParticles`) almost every frame to kill stars
  past `despawnDistance` or inside the Earth. Options: set each star's lifetime so it dies at the despawn point,
  and use the particle Collision or Trigger module for the Earth.
- [ ] **6. Property blocks break the SRP Batcher.** [AsteroidBehavior.cs](Assets/Scripts/AsteroidBehavior.cs)
  `SetGlow` and `UpdateOutline` use `MaterialPropertyBlock`s, which take each destructible or warning asteroid
  out of the SRP Batcher, so each one is a separate, slower draw call. Option: swap between shared materials
  instead. Check in the Frame Debugger whether it matters at real asteroid counts.
- [ ] **7. Danger glow rebuilds the canvas.** [DangerWarning.cs](Assets/Scripts/DangerWarning.cs) `Update`
  changes `sizeDelta` and `color` every frame, which rebuilds the whole canvas it's on. Options: give the
  overlays their own nested Canvas, or animate `localScale` instead of `sizeDelta`.

## Low impact

- [ ] **8. Cache the blink wait.** `AsteroidBehavior.WarnThenMove` creates a `new WaitForSeconds` on every
  blink (every 0.1 s per warning asteroid). Create it once in `Awake`.
- [ ] **9. `OrbitRing` line updates.** Calls `SetPosition` 96+ times a frame for the ring. Use one
  `SetPositions` call with a cached array, and only redraw the ring when its radius or center changes.
- [ ] **10. Events instead of polling for game over / win.** `PlayerDeath`, `PlayerInvincibility`,
  `ExhaustFlame`, and `DangerWarning` check `GameManager` in `Update` every frame. An `OnGameOver` / `OnWin`
  event on `GameManager` (like `PlayerHealth.OnLivesChanged`) would replace that. Mostly a design cleanup.
- [ ] **11. Repeated lookups on hit.** `OnTriggerEnter` in `AsteroidBehavior` and `PowerUp` calls
  `GetComponentInParent` and `GetComponent<PlayerHealth>()` on every hit, though `GameManager` already holds
  the player and `PlayerHealth`.
- [ ] **12. `Debug.Log` in builds.** `PlayerHealth` and `GameManager` log on every life change and on game
  over. Logs still run in a build and are slower than they look.
- [ ] **13. `ExhaustFlame` per-frame sets.** `Update` sets `startLifetime` and `flameRenderer.enabled` every
  frame even when nothing changed.

## Already fine

`PlayerMovement`, `CameraShake`, `LevelTimer`, `PlayerCamera`, `PlayerShell`, `RunStats`,
`SkyboxSpeedController`, and the `AsteroidBehavior.Active` list.
