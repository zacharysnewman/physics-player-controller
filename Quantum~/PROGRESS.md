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
| 4 — Crouch | — | — | |
| 5 — Moving platforms & external forces | — | — | |
| 6 — Ladder climbing | — | — | |
| 7 — View layer | — | — | |
| 8 — Hardening & release | — | — | |

Harness SDK: Quantum **3.0.0** Stable 1548 (`quantum-sdk-libs`). Package target: **3.0.13**.

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
