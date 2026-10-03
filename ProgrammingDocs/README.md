# Programming Documentation

This folder has one Markdown file per script in `Assets/Scripts`. Each file explains
the approach and logic behind its script: what it does, how it does it, and why.

Only hand-written scripts are documented. Auto-generated ones (like `PlayerInput.cs`, which Unity
generates from `Assets/Settings/PlayerInput.inputactions`) are left out.

It lives outside `Assets/` so Unity does not import these files.

## Scripts

| Script | Doc | Status |
| --- | --- | --- |
| AsteroidBehavior.cs | [AsteroidBehavior.md](AsteroidBehavior.md) | Done |
| CameraShake.cs | [CameraShake.md](CameraShake.md) | Done |
| DangerWarning.cs | [DangerWarning.md](DangerWarning.md) | Done |
| EndScreen.cs | [EndScreen.md](EndScreen.md) | Done |
| GameManager.cs | [GameManager.md](GameManager.md) | Done |
| LevelTimer.cs | [LevelTimer.md](LevelTimer.md) | Done |
| ObjectSpawner.cs | [ObjectSpawner.md](ObjectSpawner.md) | Done |
| OrbitRing.cs | [OrbitRing.md](OrbitRing.md) | Done |
| OrbitSettings.cs | [OrbitSettings.md](OrbitSettings.md) | Done |
| PatternGizmos.cs | [PatternGizmos.md](PatternGizmos.md) | Done |
| Editor/PatternGizmosEditor.cs | [PatternGizmosEditor.md](PatternGizmosEditor.md) | Done |
| PlayerCamera.cs | [PlayerCamera.md](PlayerCamera.md) | Done |
| PlayerDeath.cs | [PlayerDeath.md](PlayerDeath.md) | Done |
| PlayerHealth.cs | [PlayerHealth.md](PlayerHealth.md) | Done |
| PlayerInvincibility.cs | [PlayerInvincibility.md](PlayerInvincibility.md) | Done |
| PlayerMovement.cs | [PlayerMovement.md](PlayerMovement.md) | Done |
| PlayerShell.cs | [PlayerShell.md](PlayerShell.md) | Done |
| PowerUp.cs | [PowerUp.md](PowerUp.md) | Done |
| RunStats.cs | [RunStats.md](RunStats.md) | Done |
| SkyboxSpeedController.cs | [SkyboxSpeedController.md](SkyboxSpeedController.md) | Done |
| SpawnableTemplate.cs | [SpawnableTemplate.md](SpawnableTemplate.md) | Done |
| StarfieldStreamer.cs | [StarfieldStreamer.md](StarfieldStreamer.md) | Done |

## Template

Each doc follows this structure (see [_Template.md](_Template.md)):

1. **Purpose**: what the script is for, in a sentence or two
2. **Where it lives**: which GameObject it goes on and what it needs from other scripts
3. **Inspector settings**: the tunable fields and what they change
4. **How it works**: the logic, walked through in the order it runs
5. **Talks to**: other scripts it reads from or calls
6. **Gotchas**: things that are easy to break or easy to forget
