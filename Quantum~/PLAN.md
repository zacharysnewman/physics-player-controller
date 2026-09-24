# Quantum 3 Port — Phased Plan

A deterministic port of the Physics Player Controller (PPC) to **Photon Quantum 3**, shipped as a
separately importable package that lives beside the existing Unity package in this repo.

- **Target SDK:** Quantum 3.0 stable (3.0.13, Build 2170, Aug 2026). Not the 3.1 preview.
- **Physics model:** faithful dynamic-body port. The character is a `PhysicsBody3D` driven by the same
  velocity-layer aggregation as the Unity version, so it keeps the "reactive" behaviour (launch pads,
  explosions, being pushed). This is an *alternative* to Photon's kinematic KCC addon, not a wrapper around it.
- **Scope:** full feature parity — walk/run, slopes, steps, jump (buffer + coyote), crouch (incl. mid-air
  boost), ceiling handling, external-force absorption, moving platforms, ladder climbing, camera, animator,
  debug visualisation.

---

## Packaging

```
physics-player-controller/          ← existing package: com.zacharysnewman.ppc (unchanged)
├── package.json
├── Runtime/ ...
└── Quantum~/                       ← new package: com.zacharysnewman.ppc.quantum
    ├── package.json
    ├── README.md
    ├── CHANGELOG.md
    ├── Simulation/                 ← deterministic code, joins Quantum.Simulation
    │   ├── PPC.Simulation.asmref
    │   ├── Dsl/                    ← *.qtn (components, events, signals)
    │   ├── Assets/                 ← AssetObject configs
    │   ├── Systems/
    │   └── Core/                   ← pure static helpers (layer math, probes)
    ├── View/                       ← Unity-side, joins Quantum.Unity
    │   ├── PPC.View.asmref
    │   ├── Input/
    │   ├── Camera/
    │   ├── Animation/
    │   └── Debug/
    ├── Editor/                     ← optional, joins Quantum.Unity.Editor
    └── Samples~/
        └── Playground/             ← test scene, prototypes, configs, sample input.qtn
```

- The trailing `~` makes Unity ignore the folder when the **root** package is imported, so non-Quantum users
  are unaffected.
- Quantum users install it via git URL with `?path=/Quantum~`, alongside (or instead of) the base package.
  The Quantum port has **no code dependency** on the base package.
- Code joins Quantum's assemblies through `.asmref` files (required: "all deterministic simulation code
  MUST be in QuantumUser/Simulation or included in the Quantum.Simulation assembly").
- **Fallback (if Phase 0 shows the package route doesn't work):** ship the same tree as a drop-in folder
  (copy `Simulation/` + `View/` under `Assets/`), exactly how Photon's own addons are distributed. The
  folder layout is identical, so nothing else in this plan changes.

### The `input` problem

Quantum has **one global `input` struct per game**. A reusable package must not define it, or it will
collide with the game's own input. Instead:

- The package defines a plain DSL struct `PPCInput` (move vector, look yaw/pitch, buttons).
- A tiny partial hook / static method maps the game's `Input` into `PPCInput` each tick.
- `Samples~/Playground` ships a ready-made `input.qtn` + mapping for users who have no input yet.

---

## Architecture mapping

| Unity PPC | Quantum port |
|---|---|
| `Rigidbody` (rotation frozen, no gravity) | `PhysicsBody3D` dynamic, rotation frozen, gravity scale 0 (verify flag names in Phase 0) |
| `CapsuleCollider` | `PhysicsCollider3D` with `Shape3D` capsule (Quantum uses radius + **extent**, not height) |
| `IVelocityLayer` + `GetComponents` | Fixed-order systems; each writes its contribution into a per-entity `PPCVelocityAccumulator` component |
| `IsExclusive` (climb) | Flag on the accumulator: when set, non-exclusive contributions are dropped |
| `VelocityAggregator.Apply` → `AddForce(delta/dt, Acceleration)` | `PPCAggregateSystem`: `body->AddLinearImpulse((target - body->Velocity) * mass)` or direct velocity write (decide in Phase 1 — see risks) |
| ScriptableObject configs | `AssetObject` configs with `FP` fields, referenced by `AssetRef<T>` from a `PPCCharacter` component |
| `Update` / `FixedUpdate` split | Single tick. Removes the ordering hacks (see below) |
| Unity `PlayerInput` / Input System | View-side `PPCInputPoller` (`CallbackPollInput`) using the Input System; buttons polled as *current state* |
| `Camera.main` for camera-relative movement | Camera **yaw sent in input**; simulation never reads the camera |
| `Physics.Raycast` / `SphereCast` / `OverlapBox` | `f.Physics3D.Raycast`, `ShapeCastAll`, `OverlapShape` with `QueryOptions` + layer masks |
| `OnTriggerEnter/Exit` (ladder) | `ISignalOnTriggerEnter3D/Exit3D` with `CallbackFlags` on the character collider, or an overlap query each tick |
| Moving platform transform deltas | Platform entity's `Transform3D` delta or `PhysicsBody3D` velocity; tracked in a `PPCPlatformContact` component |
| `PlayerController` state machine | `PPCStateSystem` writing a `PPCState` enum into `PPCCharacter` |
| `CameraController`, `PlayerAnimatorController`, `DebugVisualizer` | View-side `QuantumEntityViewComponent`s; animation driven by component state + Quantum events |
| `Debug.Log` / Gizmos | `Log.Debug` and `Draw.*` in simulation (stripped in release builds) |

### Hacks that should disappear

These exist only because `Update` and `FixedUpdate` run at different rates in Unity:

- `VerticalVelocityLayer.skipExternalAbsorption` (jump vs. same-step velocity lag)
- Grounded-suppression while `accumulatedY > platformY + 0.1` (second `Update` before the first `FixedUpdate`)
- `jumpBufferTimer` / `coyoteTimer` decremented by `Time.deltaTime`

In Quantum, input → logic → physics runs in a fixed order every tick. Each hack gets re-evaluated in its
phase: removed if the tick order makes it unnecessary, kept (with a comment) if the physics step still
introduces a one-tick lag.

### Known bugs — fix, don't port

From `EVALUATION.md` / `BUGS.md`. Each is fixed in the phase that ports the feature:

- Step height hardcodes capsule half-height (Phase 2)
- No air-control reduction (Phase 2, as a config value)
- Double-jump via coyote time after a real jump (Phase 3)
- Mid-air crouch leaves the wrong capsule centre on landing (Phase 4)
- Ladder: unused `playerToLadder`, no snap to ladder face, no jump-off impulse (Phase 6)
- Ceiling-check / ground-ray origin issues (Phase 2)

---

## System order (per tick)

Registered in a `SystemsConfig` (users add a `PPC` `SystemGroup` to their own config):

1. `PPCInputSystem` — map game input → `PPCInput`, update buttons
2. `PPCProbeSystem` — ground, ceiling, wall, platform probes → `PPCGroundState`
3. `PPCPlatformSystem` — platform base velocity
4. `PPCCrouchSystem` — capsule resize, speed multiplier
5. `PPCClimbSystem` — enter/exit ladder, exclusive layer
6. `PPCMovementLayerSystem` — horizontal contribution
7. `PPCVerticalLayerSystem` — gravity, jump, external absorption
8. `PPCAggregateSystem` — sum layers → drive body
9. *(Quantum core physics step)*
10. `PPCStateSystem` — state enum + events (Jumped, Landed, StartedClimb…)

Exact placement relative to the core physics systems is decided in Phase 0/1 (probes may need to run
*after* physics to see resolved positions).

---

## Phases

Every phase ends with a **checkpoint in Unity** (I can't compile or run Quantum in the cloud
container — the SDK is an account-gated download). A checkpoint is: compiles cleanly, no Quantum
determinism warnings, the listed behaviours are verified in the Playground scene.

### Phase 0 — Spikes & validation *(small; unblocks everything)*

Goal: prove the packaging and the risky APIs before writing real code.

- [ ] Create `Quantum~/package.json` (`com.zacharysnewman.ppc.quantum`, Unity 2021.3+ or whatever 3.0.13 requires)
- [ ] Minimal `.asmref` → `Quantum.Simulation`, one `.qtn` with a dummy component, one empty system
- [ ] **Verify:** git install with `?path=/Quantum~` works
- [ ] **Verify:** Quantum CodeGen picks up `.qtn` files inside `Packages/` (not just `Assets/`)
- [ ] **Verify:** generated code lands somewhere sensible (it may write into `Assets/QuantumUser/.../Generated`)
- [ ] **Verify API names** on 3.0.13: `PhysicsBody3D` rotation-freeze flags, gravity scale, `IsKinematic`,
      `AddLinearImpulse`, `Velocity`; runtime `collider->Shape` swap + `ResetCenterOfMass` → `ResetInertia`
- [ ] **Decide:** package route vs. drop-in fallback. Record the decision in this file.

Checkpoint: empty package imports into a fresh Quantum 3.0.13 project and the dummy system ticks.

### Phase 1 — Skeleton *(entity spawns and stands still)*

- [ ] DSL: `PPCCharacter` (config refs, state), `PPCInput` struct, `PPCVelocityAccumulator`, `PPCGroundState`
- [ ] Config assets: `PPCMovementConfig`, `PPCJumpConfig`, `PPCCrouchConfig`, `PPCClimbConfig`,
      `PPCProbeConfig` (was `GroundCheckerConfig`), all `FP`, same defaults as the Unity version
- [ ] Spawning: entity prototype with `Transform3D` + `PhysicsCollider3D` (capsule) + `PhysicsBody3D` + `PPCCharacter`;
      `ISignalOnPlayerAdded` spawn example in the sample
- [ ] Sample `input.qtn` + `PPCInputPoller` (Unity Input System → `Input`), including camera yaw
- [ ] `PPCAggregateSystem` driving a constant zero target velocity; body with gravity disabled
- [ ] **Decide** the drive method: impulse `(target − v)·mass` vs. direct `body->Velocity = target`. Test both
      against other dynamic bodies (does the character still push/get pushed?)

Checkpoint: character spawns, stands on the floor, doesn't tip, doesn't drift; a thrown box can nudge it.

### Phase 2 — Probes & horizontal movement

- [ ] `PPCProbeSystem`: ground (multi-ray or shape cast), ground normal, slope angle, ceiling, wall;
      layer masks from config. Fix the ray-origin bugs.
- [ ] `PPCMovementLayerSystem`: camera-relative direction from input yaw; walk/run; acceleration,
      deceleration, reverse deceleration, `maxAcceleration` clamp
- [ ] Slope alignment (project onto ground plane), max slope angle
- [ ] Step handling using real capsule dimensions (fix the half-height bug)
- [ ] Air control factor (new config value; defaults to current behaviour)
- [ ] External horizontal absorption: air drag + ground friction on the delta between last target and actual velocity

Checkpoint: walk/run on flat, slopes, stairs; blocked by walls; slides off too-steep slopes; a side impulse
decays per config.

### Phase 3 — Vertical layer, jump, ceilings

- [ ] `PPCVerticalLayerSystem`: accumulated Y, gravity scale, grounded clamp to platform Y
- [ ] External vertical absorption (launch pad while grounded → launch; airborne deltas absorbed)
- [ ] Jump: buffer + coyote measured in **ticks** (or `FP` seconds × `f.DeltaTime`), `max(accumulated, jump + platformY)`
- [ ] Fix double-jump via coyote after a real jump
- [ ] Ceiling hit cancels upward velocity
- [ ] Re-evaluate the `skipExternalAbsorption` and grounded-suppression hacks; delete if unnecessary
- [ ] Events: `PPCJumped`, `PPCLanded`

Checkpoint: jump height matches Unity version (same config); buffered and coyote jumps work; no double jump;
launch pad launches; head-bonk stops the jump.

### Phase 4 — Crouch

- [ ] `PPCCrouchSystem`: toggle/hold from input; swap capsule shape (`collider->Shape`) then
      `ResetCenterOfMass` → `ResetInertia`
- [ ] Keep feet planted when shrinking on ground; shrink toward the head in mid-air (fix landing-centre bug)
- [ ] Stand-up blocked by ceiling (overlap check)
- [ ] Speed multiplier into the movement layer; mid-air crouch boost

Checkpoint: crouch under a low bar, can't stand up beneath it, crouch-jump reaches a higher ledge,
capsule is correct after landing.

### Phase 5 — Moving platforms & external forces

- [ ] `PPCPlatformSystem`: detect ground entity; base velocity from dynamic body velocity (+ angular × r) or
      from kinematic `Transform3D` delta
- [ ] Rotational carry (yaw follows platform), capped by `maxRotationSpeed`
- [ ] Walk-off dismount seeds vertical velocity from platform
- [ ] Sample: elevator, rotating disc, conveyor, launch pad, explosion impulse helper

Checkpoint: ride all platform types without jitter or sliding; jumping off carries momentum; explosion
pushes character and decays correctly.

### Phase 6 — Ladder climbing

- [ ] Ladder detection (trigger signals or overlap query, config layer mask)
- [ ] Exclusive climb layer: vertical from move input, horizontal suppressed
- [ ] Snap to ladder face; exit at top/bottom; jump-off with configurable launch impulse
- [ ] Events: `PPCClimbStarted`, `PPCClimbEnded`

Checkpoint: climb up/down, dismount at top onto a ledge, jump off sideways.

### Phase 7 — View layer

- [ ] `PPCCameraView`: first/third person, pitch limits, invert Y, sensitivity (view-only; yaw fed back into input)
- [ ] Camera height follows crouch (fix double-count issue from `EVALUATION.md`)
- [ ] `PPCAnimatorView`: reads `PPCState`, velocities, grounded; subscribes to events
- [ ] `PPCDebugView`: on-screen state label + optional `Draw.*` probes (off by default — fixes the
      "debug in production" issue)
- [ ] Interpolation / misprediction smoothing check with Quantum's entity view settings

Checkpoint: camera and animations are smooth locally and with simulated lag (Quantum's input delay /
lag simulation tools).

### Phase 8 — Hardening & release

- [ ] Determinism check: run two clients (or the multi-client / replay tools) and compare checksums over a
      scripted path through the Playground
- [ ] Optional headless run via Quantum's exported dotnet simulation project + replay runner
- [ ] Performance: 16+ characters, profile with the Quantum graph profiler
- [ ] `README.md` (install, input hook, SystemsConfig setup, config reference), `CHANGELOG.md`, samples
- [ ] Root `README`/`CHANGELOG` mention the Quantum package
- [ ] Tag `quantum-v0.1.0`

---

## Risks & open questions

| Risk | Mitigation |
|---|---|
| CodeGen may not scan `.qtn` inside `Packages/` | Phase 0 spike; fall back to drop-in folder |
| Quantum docs discourage `PhysicsBody` on characters (solver nudges it) | Rotation frozen, low-friction physics material, drive method chosen in Phase 1, scenario checklist every phase |
| Reading `body->Velocity` for external-force absorption may lag a tick relative to Unity | Measure in Phase 1; adjust absorption to compare against last tick's *post-physics* velocity |
| Probe timing relative to the physics step | Try before-physics first; move to after-physics if grounding flickers |
| Global `input` struct collision | `PPCInput` + mapping hook; sample ships an `input.qtn` |
| API drift between 3.0.x patch releases | Pin to 3.0.13 in README; note the minimum version |
| No compile/test in cloud | Unity checkpoint at the end of every phase |

---

## References

- Quantum project structure & assemblies: https://doc.photonengine.com/quantum/v3/manual/quantum-project
- Physics bodies & colliders: https://doc.photonengine.com/quantum/v3/manual/physics/collider-body
- Physics queries: https://doc.photonengine.com/quantum/v3/manual/physics/queries
- Physics callbacks: https://doc.photonengine.com/quantum/v3/manual/physics/callbacks
- Input: https://doc.photonengine.com/quantum/v3/manual/input
- Systems & SystemsConfig: https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/systems
- Built-in KCC (for comparison): https://doc.photonengine.com/quantum/v3/manual/physics/kcc
- KCC addon (for comparison): https://doc.photonengine.com/quantum/v3/addons/kcc/overview
- Release notes: https://doc.photonengine.com/quantum/v3/getting-started/release-notes
