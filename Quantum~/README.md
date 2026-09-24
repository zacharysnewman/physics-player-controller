# Physics Player Controller — Quantum 3

A deterministic port of the [Physics Player Controller](https://github.com/zacharysnewman/physics-player-controller)
for **Photon Quantum 3**: a dynamic-body character controller driven by velocity layers, as an
alternative to Quantum's kinematic KCC addon. Because the character is a real physics body, it reacts
to the world: other bodies push it, and explosions and launch pads throw it.

> **Status: 0.1.0 preview.** The simulation is complete and covered by 95 headless tests. The Unity
> side still needs its first check in the editor. See [PROGRESS.md](PROGRESS.md) and [PLAN.md](PLAN.md).

## Features

- Walk / run with acceleration, deceleration and faster reversal; camera-relative; air control
- Slopes (follows the ground surface, max walkable angle), stairs up and down at full speed without
  leaving the ground, ground snapping; the camera smooths steps
- Jump with press buffer and coyote time; ceilings stop jumps
- Hold-to-crouch: feet planted on the ground, legs tucked in the air, stands when there's room
- Moving and rotating platforms (dynamic, kinematic or script-moved); stand on other characters
- External forces: pushes, launch pads, explosions (absorbed, then decay with friction/drag)
- Ladders: climb, strafe, look-down reversal, face snap, jump-off
- States and events for animation; first-person camera, animator and debug views

## Requirements

- Unity 2021.3 LTS or newer
- Photon Quantum 3.0.x SDK in the project (targets 3.0.13)
- The Playground sample's input poller uses the Unity Input System

## Install

Package Manager → **Add package from git URL**:

```
https://github.com/zacharysnewman/physics-player-controller.git?path=/Quantum~
```

This package is independent of the base `com.zacharysnewman.ppc` package; you don't need both.
Then run **Tools → Quantum → CodeGen → Run Qtn CodeGen**.

## Quick start

1. **Input.** Your game defines Quantum's single `input`. Map it onto the controller by implementing
   the partial hook (it compiles into the same Quantum.Simulation assembly):
   ```csharp
   namespace Quantum {
     public static unsafe partial class PPCInputBridge {
       static partial void ReadPlayerInput(Frame f, PlayerRef player, ref PPCInput input) {
         var i = f.GetPlayerInput(player);
         input.Move = i->Move;            // x = right, y = forward
         input.LookYaw = i->LookYaw;      // camera yaw (degrees)
         input.LookPitch = i->LookPitch;  // camera pitch (degrees, up = positive)
         input.Jump = i->Jump.IsDown;
         input.Run = i->Run.IsDown;
         input.Crouch = i->Crouch.IsDown;
       }
     }
   }
   ```
2. **Systems.** Add `PPCSystemGroup` to your `SystemsConfig` after Quantum's core systems (including
   `PhysicsSystem3D`). From code: `PPCSystems.AddTo(systemsConfig)`.
3. **Config (optional).** Without one, characters use `PPCConfig.Default`. To tune:
   *Create → Quantum → Physics Player Controller → Character Config* (keep it under `Assets/`).
   Characters get a frictionless physics material automatically unless you set one.
4. **Spawn.** `PPCSpawn.Character(f, config, feetPosition, player: player, view: viewAsset)`, or an
   entity prototype with just a `PPCCharacter` (plus `PPCPlayerLink`). `PPCSetupSystem` adds the
   physics body and capsule.
5. **View.** Put `PPCCameraView` (and optionally `PPCAnimatorView`, `PPCDebugView`) on the character's
   entity view prefab; send `PPCCameraView.Local.Yaw/Pitch` in your input.

The **Playground** sample (Package Manager → Samples) does all of this, with a step-by-step setup.

## Tuning

Everything lives in one `PPCConfig` asset, and it's optional (characters without one use the
defaults). The defaults are a fast arena-shooter feel taken from the Source engine (Half-Life 2,
Counter-Strike) and checked against Halo 3:

| | Default | Source (converted) |
|---|---|---|
| Walk / run | 5 / 8 m/s | 190 / 320 units/s = 4.8 / 8.1 m/s |
| Acceleration / stop | 50 / 12 m/s² | `sv_accelerate` 10 × speed; `sv_friction` 4, `sv_stopspeed` 100 |
| Air control | 0.2 | `sv_airaccelerate` with a 30 units/s wish cap: little direct control |
| Gravity | × 2 = 20 m/s² | `sv_gravity` 800 units/s² = 20.3 m/s² |
| Jump | 1.25 m | ≈ 45 units in CS/TF2 |
| Step | 0.45 m | `sv_stepsize` 18 units |
| Pushes in the air | no drag | no air friction |

To get the Unity package's original numbers (run 10 m/s, 10 m/s², 5 m jumps, gravity × 1, full air
control, 0.5 m steps), right-click the asset → **Apply Unity Parity**, or call
`config.ApplyUnityParity()`. **Apply Recommended Feel** goes back. Rarely needed knobs are under
**Advanced**.

Steps work like Source: the simulation lifts the character onto a step, or snaps it down one, in a single
tick (so it keeps full speed and never leaves the ground), and `PPCCameraView` smooths the camera over it.
Characters can stand on each other; set `Movement.CarriedByCharacters` to be carried along too.

## How it works

Each tick, after Quantum's physics step:

| System | Job |
|---|---|
| `PPCInputSystem` | Reads input through `PPCInputBridge` |
| `PPCProbeSystem` | Ground (17 rays), ceiling (17 rays), walls (4 rays) |
| `PPCPlatformSystem` | Velocity of the ground under the character |
| `PPCCrouchSystem` | Capsule size |
| `PPCJumpSystem` | Buffer, coyote time, jump |
| `PPCClimbSystem` | Exclusive ladder layer |
| `PPCMovementLayerSystem` | Horizontal velocity layer (+ external horizontal forces) |
| `PPCVerticalLayerSystem` | Vertical layer: gravity, ground following, launches |
| `PPCAggregateSystem` | Drives `body.Velocity` to the sum of the layers |
| `PPCStateSystem` | `PPCState` for animation and gameplay |

Anything that moved the body away from last tick's target (a collision, an explosion) is absorbed
as external velocity and decays, which is what makes the controller reactive.

Moving platforms: move kinematic platforms by transform in a system that runs *before*
`PPCSystemGroup`. Quantum doesn't move kinematic bodies by their velocity.

## Differences from the Unity package

See [CHANGELOG.md](CHANGELOG.md). In short: fixed double jump, floor hovering, slope launches,
crouch landing offset, step height and several ladder issues; slopes are followed by the vertical
layer; crouch is instant in the simulation (the camera smooths it).

## Layout

| Folder | Assembly | Contents |
|---|---|---|
| `Simulation/` | `Quantum.Simulation` (via `.asmref`) | DSL (`.qtn`), config, systems, helpers |
| `View/` | `Quantum.Unity` (via `.asmref`) | Camera, animator and debug views |
| `Samples~/Playground` | (imported into your project) | Input, bridge, spawner, poller, setup guide |
| `Tests~/` | — | Headless .NET build and tests (not imported by Unity) |

## Development

`Tests~/build.sh` generates, compiles and tests the simulation without Unity: 95 tests,
determinism and Debug-vs-Release checks. See [Tests~/README.md](Tests~/README.md).
