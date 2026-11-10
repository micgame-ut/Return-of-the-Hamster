# Programming Documentation

This folder has one Markdown file per script in `Assets/Scripts`, and one per Shader Graph or Sub Graph
in `Assets/Shaders`. Each file explains
the approach and logic behind its script: what it does, how it does it, and why.

Only hand-written scripts are documented. Auto-generated ones (like `PlayerInput.cs`, which Unity
generates from `Assets/Settings/PlayerInput.inputactions`) are left out.

It lives outside `Assets/` so Unity does not import these files.

## Scripts

| Script | Doc | Status |
| --- | --- | --- |
| AsteroidBehavior.cs | [AsteroidBehavior.md](AsteroidBehavior.md) | Done |
| BoostRing.cs | [BoostRing.md](BoostRing.md) | Done |
| CameraShake.cs | [CameraShake.md](CameraShake.md) | Done |
| CloseCallZone.cs | [CloseCallZone.md](CloseCallZone.md) | Done |
| Editor/CloudNoiseBaker.cs | [CloudNoiseBaker.md](CloudNoiseBaker.md) | Done |
| DangerWarning.cs | [DangerWarning.md](DangerWarning.md) | Done |
| EffectPool.cs | [EffectPool.md](EffectPool.md) | Done |
| EndScreen.cs | [EndScreen.md](EndScreen.md) | Done |
| ExhaustFlame.cs | [ExhaustFlame.md](ExhaustFlame.md) | Done |
| GameManager.cs | [GameManager.md](GameManager.md) | Done |
| Audio/GameSFX.cs | [GameSFX.md](GameSFX.md) | Done |
| Audio/UISFX.cs | [UISFX.md](UISFX.md) | Done |
| GraphicsQuality.cs | [GraphicsQuality.md](GraphicsQuality.md) | Done |
| HamsterAnimationDriver.cs | [HamsterAnimationDriver.md](HamsterAnimationDriver.md) | Done |
| HamsterMenuFloat.cs | [HamsterMenuFloat.md](HamsterMenuFloat.md) | Done |
| LevelTimer.cs | [LevelTimer.md](LevelTimer.md) | Done |
| MagnetMirror.cs | [MagnetMirror.md](MagnetMirror.md) | Done |
| MenuNavigator.cs | [MenuNavigator.md](MenuNavigator.md) | Done |
| MenuTransition.cs | [MenuTransition.md](MenuTransition.md) | Done |
| MultiplierDisplay.cs | [MultiplierDisplay.md](MultiplierDisplay.md) | Done |
| ObjectSpawner.cs | [ObjectSpawner.md](ObjectSpawner.md) | Done |
| OrbitRing.cs | [OrbitRing.md](OrbitRing.md) | Done |
| OrbitSettings.cs | [OrbitSettings.md](OrbitSettings.md) | Done |
| OverdriveTally.cs | [OverdriveTally.md](OverdriveTally.md) | Done |
| PatternCopy.cs | [PatternCopy.md](PatternCopy.md) | Done |
| Editor/PatternFairness.cs | [PatternFairness.md](PatternFairness.md) | Done |
| PatternGizmos.cs | [PatternGizmos.md](PatternGizmos.md) | Done |
| Editor/PatternGizmosEditor.cs | [PatternGizmosEditor.md](PatternGizmosEditor.md) | Done |
| PatternTest.cs | [PatternTest.md](PatternTest.md) | Done |
| PlayerCamera.cs | [PlayerCamera.md](PlayerCamera.md) | Done |
| PlayerDeath.cs | [PlayerDeath.md](PlayerDeath.md) | Done |
| PlayerHealth.cs | [PlayerHealth.md](PlayerHealth.md) | Done |
| PlayerInvincibility.cs | [PlayerInvincibility.md](PlayerInvincibility.md) | Done |
| PlayerMovement.cs | [PlayerMovement.md](PlayerMovement.md) | Done |
| PlayerShell.cs | [PlayerShell.md](PlayerShell.md) | Done |
| PlayerTwins.cs | [PlayerTwins.md](PlayerTwins.md) | Done |
| PooledEffect.cs | [PooledEffect.md](PooledEffect.md) | Done |
| PowerUp.cs | [PowerUp.md](PowerUp.md) | Done |
| RunStats.cs | [RunStats.md](RunStats.md) | Done |
| Score.cs | [Score.md](Score.md) | Done |
| ScorePopups.cs | [ScorePopups.md](ScorePopups.md) | Done |
| SkyboxSpeedController.cs | [SkyboxSpeedController.md](SkyboxSpeedController.md) | Done |
| SpawnableTemplate.cs | [SpawnableTemplate.md](SpawnableTemplate.md) | Done |
| StarfieldStreamer.cs | [StarfieldStreamer.md](StarfieldStreamer.md) | Done |
| TwinCopy.cs | [TwinCopy.md](TwinCopy.md) | Done |

## Shaders

| Graph | Doc | Status |
| --- | --- | --- |
| Asteroid Outline.shadergraph | [AsteroidOutline.md](AsteroidOutline.md) | Done |
| Asteroid Phase.shadergraph | [AsteroidPhase.md](AsteroidPhase.md) | Done |
| Black Hole.shadergraph | [BlackHole.md](BlackHole.md) | Done |
| Frost Gas.shadergraph | [FrostGas.md](FrostGas.md) | Done |
| Frost Rock.shadergraph | [FrostRock.md](FrostRock.md) | Done |
| Frost Trail.shadergraph | [FrostTrail.md](FrostTrail.md) | Done |
| Heatwave.shadersubgraph | [Heatwave.md](Heatwave.md) | Done |
| Nebula Clouds.shadersubgraph | [NebulaClouds.md](NebulaClouds.md) | Done |
| Nebula Volume.shadersubgraph | [NebulaVolume.md](NebulaVolume.md) | Done |
| Radial Streams.shadersubgraph | [RadialStreams.md](RadialStreams.md) | Done |
| Rainbow Overlay.shadergraph | [RainbowOverlay.md](RainbowOverlay.md) | Done |
| Ring Glow.shadergraph | [RingGlow.md](RingGlow.md) | Done |
| Starry Sky.shadergraph | [StarrySky.md](StarrySky.md) | Done |
| Starry Sky Volume.shadergraph | [StarrySkyVolume.md](StarrySkyVolume.md) | Done |
| Triplanar Noise.shadersubgraph | [TriplanarNoise.md](TriplanarNoise.md) | Done |
| Tube Streams.shadersubgraph | [TubeStreams.md](TubeStreams.md) | Done |

## Template

Each doc follows this structure (see [_Template.md](_Template.md)):

1. **Purpose**: what the script is for, in a sentence or two
2. **Where it lives**: which GameObject it goes on and what it needs from other scripts
3. **Inspector settings**: the tunable fields and what they change
4. **How it works**: the logic, walked through in the order it runs
5. **Talks to**: other scripts it reads from or calls
6. **Gotchas**: things that are easy to break or easy to forget
