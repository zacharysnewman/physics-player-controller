# Quantum Port — Progress

Tracks the Quantum 3 port against [PLAN.md](PLAN.md). Each phase has two gates: the **headless
gate** (`Tests~/build.sh`: CodeGen, compile, tests; runs in the cloud) and the **Unity checkpoint**
(manual).

## Status

| Phase | Headless gate | Unity checkpoint | Notes |
|---|---|---|---|
| 0 — Spikes & validation | ✅ 10/10 tests (Debug + Release) | ⏳ git install + CodeGen in `Packages/` | Package route chosen |
| 1 — Skeleton | ✅ | ⏳ | Single `PPCConfig`; code spawning |
| 2 — Probes & horizontal movement | ✅ | ⏳ | Probe-length and step-height bugs fixed |
| 3 — Vertical layer, jump, ceilings | ✅ | ⏳ | Double jump fixed; ground following + snap |
| 4 — Crouch | ✅ | ⏳ | Landing-centre bug fixed |
| 5 — Moving platforms & external forces | ✅ | ⏳ | Transform-delta for kinematic platforms |
| 6 — Ladder climbing | ✅ | ⏳ | Snap, jump-off, look-down threshold fixed |
| 7 — View layer | ✅ sample sim compiles | ⏳ view scripts need Unity | Not compilable here |
| 8 — Hardening & release | ✅ Debug == Release | ⏳ then tag `quantum-v0.1.0` | 16 chars ≈ 3–4.5 ms/tick |
| Post-review cleanup | ✅ 80 tests; golden trace | ⏳ | Refactor, optional config, recommended feel |
| Post-review round 2 | ✅ 95 tests | ⏳ camera step smoothing | Shared grounded, stacking, Source defaults, stairs |

Harness SDK: Quantum **3.0.0** Stable 1548 (`quantum-sdk-libs`). Package target: **3.0.13**.

## Post-review cleanup ✅

After the review of the merged port (behaviour locked by `GoldenTraceTests`, which compares every
player's position and state per tick of the four-player scenario):
- **Refactor** (golden deviation 0): dead state removed, climbing's layer hold in one place, clearer
  names, shared helpers, the harness reuses the sample's input/bridge, cheaper probes.
- **Config** (golden deviation ≤ 0.00012 m): optional config (`PPCConfig.Default`), automatic
  frictionless material, probe margins, `Jump.Height`, no-effect settings removed, `Advanced` section.
- **Feel defaults**: recommended values plus `ApplyUnityParity()`; crouch ignores run. The behaviour
  tests run under the Unity parity preset; `ConfigPresetTests` cover the new defaults.
- Performance: no measurable change (machine noise is larger than the probe savings).
- Open for discussion: one shared "grounded" (review item 12); shape casts instead of ray rings (13).

## Post-review round 2 ✅

- **Item 12, one shared grounded:** `Ground.IsGrounded` is off from the takeoff tick. Golden re-recorded:
  23 takeoff ticks now read Jumping, 7 post-launch ticks Falling.
- **Characters aren't platforms** by default (`Movement.CarriedByCharacters`). `CharacterStackingTests`.
- **Source/Halo 3 defaults** (see README → Tuning). Sources: Valve's source-sdk-2013 (`gamemovement.cpp`,
  `movevars_shared.cpp`, `hl2_player.cpp`); Halo 3 speeds from community measurements (HaloRuns, Bungie
  forums), which Bungie never published.
- **Item 13, shape casts (experiment, not adopted).** A sphere-cast ground probe was compared with the
  ray ring on stairs and ledges (`StepSmoothnessTests` scenarios, recommended feel):

  | Down 0.2/0.3 m stairs, walking | Airborne ticks | Largest one-tick drop |
  |---|---|---|
  | Rays (before) | 19 | 0.145 m |
  | Sphere cast | 5 | 0.108 m |
  | Rays + step-down snap (adopted) | **0** | 0.200 m (a snap; the camera smooths it) |

  The sphere cast rounds edges, but it still left the ground on every scenario (2–18 ticks), made going
  *up* stairs briefly airborne (3–8 ticks), and costs more. Snapping down a step while grounded (Source's
  `StayOnGround`) fixed the real problem with the existing rays. Also tried and dropped: rising onto
  steps at a set speed instead of the one-tick lift. At 3 m/s the character stalled against the step face
  (down to 0.3 m/s) and spent up to 70 ticks airborne; at 6 m/s it got stuck on 0.4 m steps.
- **Stairs now:** up and down 0.2 m and 0.4 m steps at full walk and run speed with zero airborne ticks;
  the one-tick lift/snap is smoothed by `PPCCameraView` (Source's `SmoothViewOnStairs`; needs checking in
  Unity). Golden re-recorded: player 3 now snaps from the 0.5 m platform to the floor instead of falling.

## Phase 8 — Hardening ✅

- Four-player full-feature scenario is deterministic (600 ticks), and identical on the Debug and
  Release libraries (`Tools/cross-config-check.sh`).
- 16 characters: 3.3–4.6 ms/tick Release depending on the shared machine's load (repeat runs of
  the same code vary that much), 5.4 ms Debug (whole session).
- Simulation code audited for non-deterministic constructs: none.

## Unity checkpoint — what to verify

The headless gate covers the simulation. In Unity (Quantum 3.0.x), please check:

1. Install via git URL (`?path=/Quantum~#<branch>`), run Qtn CodeGen: no errors; `PPCCharacter` etc. generated.
2. The view scripts (`PPCCameraView`, `PPCAnimatorView`, `PPCDebugView`) and the Playground's
   `PlaygroundInputPoller` compile. They couldn't be compiled here.
3. Playground per `Samples~/Playground/README.md`: walk/run/jump/crouch/climb feel, camera, crouch
   eye height, animator parameters, moving platform.

## Phase 7 — View layer ✅ (Unity compile pending)

- `PPCCameraView`, `PPCAnimatorView`, `PPCDebugView`, `PPCSystemGroup`; Playground sample.
- build.sh compiles the sample's simulation code against the package. View code checked against SDK
  source only.

## Phase 6 — Ladder climbing ✅

- `PPCClimbSystem` (exclusive layer); `PPCLadder` component. Tests: `Phase6ClimbTests` (8).
- Fixed from EVALUATION: face snapping, jump-off launch, look-down hair trigger.

## Phase 5 — Moving platforms & external forces ✅

- `PPCPlatformSystem`, `PPCForces`. Tests: `Phase5PlatformTests` (8). Harness: `HarnessMoverSystem`,
  scheduled explosions.
- Finding: Quantum kinematic bodies aren't moved by their velocity, so kinematic platforms use the transform change.

## Phase 4 — Crouch ✅

- `PPCCrouchSystem`. Tests: `Phase4CrouchTests` (9). Fixed: mid-air crouch capsule centre after landing.

## Phase 3 — Vertical layer, jump, ceilings ✅

- `PPCJumpSystem`, `PPCVerticalLayerSystem`; probe now reports `Ground.Gap`.
- Tests: `Phase3VerticalTests` (12). Harness: events exposed (`s.Events`), relative input ticks (bug fix).
- Fixed: double jump via coyote time; hovering above floors; false launches on slopes (ground following).

## Phase 2 — Probes & horizontal movement ✅

- `PPCProbe`, `PPCProbeSystem`, `PPCMovementLayerSystem`, full `PPCStateSystem`.
- Tests: `Phase2MovementTests` (17). Harness: `HarnessKickSystem` for scheduled external impulses.
- Fixed from EVALUATION/BUGS: probe lengths follow the crouched height; step height uses the real capsule.

## Phase 1 — Skeleton ✅

- DSL, `PPCConfig`, `PPCInputBridge`, `PPCSetupSystem`, `PPCSpawn`, `PPCInputSystem`, `PPCAggregateSystem`.
- Tests: `Phase1SkeletonTests` (4) plus updated `PackageTests`.
- Decisions: direct velocity drive; one config asset; per-project prototype scripts with code spawning.
  See PLAN.md.

## Phase 0 — Spikes & validation

### Done

- **Headless build**: `Tests~/build.sh` runs Quantum's `.qtn` CodeGen through a .NET wrapper and
  compiles the simulation (C# 9, like Unity) against the SDK's non-Unity libraries. On up-guys'
  `.qtn`, the generator's output is byte-identical to what Unity produced.
- **Headless runner**: `Tests~/Tests/HeadlessSession.cs` runs a real `SessionRunner` in Local mode
  with the asset database built in code, scripted input, exact one-tick steps and per-tick checksums.
- **Package skeleton**: `package.json` (`com.zacharysnewman.ppc.quantum`, Unity 2021.3+), `.asmref`s
  into `Quantum.Simulation` / `Quantum.Unity` via the SDK's fixed GUIDs, `PPC.qtn` (`PPCState`,
  `PPCCharacter`), `PPCSystems.AddTo`, stub `PPCStateSystem`.
- **`.meta` files** for everything Unity imports, enforced by `Tests~/Tools/check-metas.sh` in the
  build (catches missing, orphaned and duplicate-GUID metas).
- **Tests (10)**: exact stepping; input delivery; 180-tick physics determinism, plus a control that
  a different seed diverges; package systems register and run; physics API behaviour (frozen
  zero-gravity capsule holds still, velocity writes and impulses move it, runtime capsule resize).
- **Tooling**: private `quantum-sdk-libs` repo for SDK files; SessionStart hook installs .NET and
  clones it in cloud sessions.

### Verified

| Question | Answer | How |
|---|---|---|
| Physics API names on 3.0.x | `RotationFreeze`, `GravityScale`, `IsKinematic`, `AddLinearImpulse`, `Velocity`, writable `Shape`, `ResetCenterOfMass` → `ResetInertia` | Reflection + `PhysicsApiTests` |
| Does CodeGen see `.qtn` in `Packages/`? | Should: `AssetDatabase.FindAssets("t:QuantumQtnAsset")` | SDK source (confirm in Unity) |
| Where does generated code go? | One combined output in `Assets/QuantumUser/.../Generated` per project | SDK source |
| Is `?path=` URL valid? | Syntax is. A path ending in `~` is undocumented | Unity docs via search (site blocked) |
| How do addons join Quantum's assemblies? | `.asmref`, like ours | KCC addon notes |
| Can package prefabs reference prototype components? | Not with per-project generated scripts (GUIDs differ). Use `[CodeGen(NoUnityPrototypeWrapper)]` + shipped scripts (`UnityWrapperFolder` also exists) | KCC 3.0.3 notes, SDK 3.1 notes, generator experiments |
| Can the package ship Quantum config assets? | Not usefully: Quantum only indexes `AssetSearchPaths` (default `Assets`) | SDK source, release notes 3.0.5 |

### Open

- [ ] **Unity check (you):** install via
  `https://github.com/zacharysnewman/physics-player-controller.git?path=/Quantum~#<branch>` in a
  Quantum 3.0.x project, run CodeGen, confirm `PPCCharacter` is generated and no import errors.

### Sources

- Quantum project structure: https://doc.photonengine.com/quantum/v3/manual/quantum-project
- DSL (attributes, pragmas): https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/dsl
- KCC addon notes (asmref, shipped prototypes): https://doc.photonengine.com/quantum/v3/addons/kcc/download
- SDK release notes (UPM support, AssetSearchPaths): https://doc.photonengine.com/quantum/v3/getting-started/release-notes
- 3.1 preview notes (`NoMonoBehaviour`, `UnityWrapperFolder` fix): https://doc.photonengine.com/quantum/v3/getting-started/preview-3-1/sdk-download
- Unity git dependencies (`?path=`): https://docs.unity3d.com/Manual/upm-git.html
