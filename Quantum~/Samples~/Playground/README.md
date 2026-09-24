# Playground sample

A minimal first-person setup for the Quantum Physics Player Controller. It contains the pieces that
belong to *your game*, not the package:

| File | What it does |
|---|---|
| `Simulation/PlaygroundInput.qtn` | The game's `input` (Quantum allows one per game) |
| `Simulation/PlaygroundInputBridge.cs` | Maps that input onto the controller's `PPCInput` |
| `Simulation/PlaygroundSpawnSystem.cs` | Spawns a character per joining player; adds three `RuntimeConfig` fields |
| `View/PlaygroundInputPoller.cs` | Keyboard/mouse + gamepad → Quantum input (Unity Input System), including camera yaw/pitch |

If your game already defines `input`, copy the fields into it and adapt the bridge instead of
importing `PlaygroundInput.qtn`.

## Setup

1. Import this sample (Package Manager → Physics Player Controller (Quantum 3) → Samples), then run
   Quantum's code generation (**Tools → Quantum → CodeGen → Run Qtn CodeGen**).
2. **Config:** *Create → Quantum → Physics Player Controller → Character Config* (or
   *Create → Quantum → Asset...* and pick `PPCConfig`). Keep it under `Assets/`, because Quantum only
   indexes `AssetSearchPaths`, which is `Assets` by default. For best results create a `PhysicsMaterial`
   (*Create → Quantum → Asset...*) with 0 static/dynamic friction and combine function *Min*, and
   assign it to **Body → Material**.
3. **Character view:** make a prefab with a `QuantumEntityView`, a visual (e.g. a 2 m capsule mesh
   centred on the root, because the entity position is the capsule centre) and the package's
   `PPCCameraView`. Optionally add `PPCAnimatorView` and `PPCDebugView`. Quantum turns it into an
   `EntityView` asset.
4. **Systems:** in your `SystemsConfig` asset, after Quantum's core systems (including
   `PhysicsSystem3D`), add `PPCSystemGroup` and `PlaygroundSpawnSystem`.
5. **Scene:** a Quantum map with some static colliders (floor, ramps, steps), a `PlaygroundInputPoller`
   on any GameObject, and a camera. For a ladder, add an entity prototype with a trigger box collider
   and the `PPC Ladder` component, facing its local +Z.
6. **RuntimeConfig** (e.g. on `QuantumRunnerLocalDebug`): set `Playground Character Config`,
   `Playground Character View` and `Playground Spawn Point` (feet position).
7. Play. WASD move, mouse look, Shift run, Space jump, Ctrl/C crouch; gamepad equivalents.

Moving platforms: move kinematic platforms by transform from a system that runs *before*
`PPCSystemGroup` (Quantum doesn't move kinematic bodies by their velocity).
