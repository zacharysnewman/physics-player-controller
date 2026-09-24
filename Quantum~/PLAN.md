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
    ├── Samples~/
    │   └── Playground/             ← test scene, prototypes, configs, sample input.qtn
    └── Tests~/                     ← headless .NET harness (CodeGen + compile + tests), never shipped
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
| `Rigidbody` (rotation frozen, no gravity) | `PhysicsBody3D` dynamic, `RotationFreeze = FreezeAll`, `GravityScale = 0` (verified in Phase 0) |
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

Every phase has two gates:

1. **Headless gate (automated, runs in the cloud):** `Quantum~/Tests~/build.sh` generates code from the
   `.qtn` files and compiles the simulation against the Quantum libraries, with no Unity involved
   (see `Tests~/README.md`), then runs the headless tests in a real Quantum session: the phase's
   behaviour tests and a determinism check.
2. **Unity checkpoint (manual):** imports cleanly, no Quantum determinism warnings, and the listed
   behaviours are verified in the Playground scene. Only needed for what the headless gate can't
   cover: view code, prefabs, feel.

The headless harness currently builds against SDK **3.0.0** (from the private `quantum-sdk-libs`
repo), while the package targets **3.0.13**. The Unity checkpoint is where 3.0.13 gets checked.

### Phase 0 — Spikes & validation ✅ *(headless part done; one Unity check left)*

Goal: prove the packaging and the risky APIs before writing real code.

- [x] Headless build harness: `.qtn` CodeGen + simulation compile via .NET (`Tests~/build.sh`)
- [x] Headless runner (`Tests~/Tests/HeadlessSession.cs`): a real `SessionRunner` in Local mode with the
      asset database built in code, scripted input, exact one-tick stepping, per-tick checksums.
      `HarnessTests` checks input delivery, determinism (two identical 180-tick physics runs match) and
      a control (a different seed diverges).
- [x] `Quantum~/package.json` (`com.zacharysnewman.ppc.quantum`, Unity 2021.3+, which is Quantum 3's minimum)
- [x] `.asmref`s → `Quantum.Simulation` (`Simulation/`) and `Quantum.Unity` (`View/`), using the SDK's fixed
      assembly GUIDs; `PPC.qtn` (`PPCState`, `PPCCharacter`); `PPCSystems.AddTo`; stub `PPCStateSystem`
- [x] `.meta` files for everything Unity imports (git packages are read-only), enforced by
      `Tests~/Tools/check-metas.sh` in `build.sh`. `.qtn` metas point at the SDK's `QuantumQtnAssetImporter`.
- [ ] **Verify in Unity:** git install with `?path=/Quantum~` works *(needs Unity; the only open item)*.
      Unity's docs (via search; docs.unity3d.com is blocked here) confirm `?path=` must be repo-relative,
      point at the folder holding `package.json`, and come before `#revision`, which our URL does.
      Whether the folder may end in `~` is undocumented. Unity copies the subfolder into
      `Library/PackageCache/<name>@<hash>`, so it should work, but it's unconfirmed.
- [x] **Verify:** Quantum CodeGen picks up `.qtn` files inside `Packages/`: the SDK's
      `QuantumCodeGenQtn.Run()` finds them with `AssetDatabase.FindAssets("t:QuantumQtnAsset")`, which
      includes packages. Confirm in the same Unity check.
- [x] **Verify:** where generated code lands: all `.qtn` files are generated together into
      `Assets/QuantumUser/Simulation/Generated` (plus `View/Generated`). So the package ships only `.qtn`
      and C#, and each project regenerates the code itself.
- [x] **Verify API names** (3.0.0, by reflection and `PhysicsApiTests`): `RotationFreeze`
      (`RotationFreezeFlags.FreezeAll`), `GravityScale`, `IsKinematic`, `AddLinearImpulse`, `Velocity` (field),
      writable `PhysicsCollider3D.Shape`, `ResetCenterOfMass` then `ResetInertia`. Capsules are
      `Shape3D.CreateCapsule(radius, extent)`.
- [x] **Decide:** package route (`Quantum~` as its own UPM package). Nothing found so far rules it out;
      the drop-in folder remains the fallback if the Unity check fails.

Findings from Photon's docs, release notes and the SDK source that shape later phases:
- **Precedent:** Photon's KCC addon joins Quantum's assemblies with `.asmref` files the same way, and
  since 3.0.5 the Quantum SDK itself can run as a local UPM package.
- **Prototype MonoBehaviours:** CodeGen emits a Unity component per DSL component (`QPrototypePPCCharacter`)
  into the *user's* `QuantumUser/View/Generated`, so its script GUID differs per project and prefabs
  shipped by the package would break. The KCC addon hit this and now ships its generated prototype
  scripts with fixed GUIDs (KCC 3.0.3 notes). Undocumented DSL controls, confirmed by running the
  generator here: `[CodeGen(NoUnityPrototypeWrapper)]` suppresses the component, and
  `[CodeGen(UnityWrapperFolder, "path")]` redirects it. In 3.1 these become `[CodeGen(NoMonoBehaviour)]`.
  → Decide in Phase 1 (see below).
- **Asset search paths:** Quantum's asset database only indexes `QuantumEditorSettings.AssetSearchPaths`,
  which defaults to `["Assets"]`. Quantum assets (configs) shipped inside the package would be invisible.
  → Ship config assets in `Samples~` (imported into `Assets/`), not in the package body.

Headless setup findings (details in `Tests~/README.md`):
- A session needs, in code: `SimulationConfig` (entity capacities, 32 physics layers + collision matrix,
  a default `PhysicsMaterial`), `SystemsConfig`, `Map`, `QuantumJsonSerializer` (needs Newtonsoft.Json),
  and `FPLut` tables (generated with `FPLut.GenerateTables`, so no binary LUT files are needed).
- Quantum 3 only polls input for players added with `QuantumGame.AddPlayer`.

Checkpoint: headless gate green ✅ (10/10 tests, Debug and Release). Unity: package imports into a
fresh Quantum 3.0.13 project via git URL, CodeGen picks up `PPC.qtn`, and a `PPCCharacter` entity ticks.

### Phase 1 — Skeleton ✅ *(entity spawns and stands still)*

- [x] DSL (`PPC.qtn`): one `PPCCharacter` component holding the config ref, `PPCInput`, previous input,
      state, and a section per layer (`Ground`, `Horizontal`, `Vertical`, `Jump`, `Crouch`, `Climb`,
      `Platform`). All systems share one filter (`PPCFilter`). Runtime sections are
      `[ExcludeFromPrototype]`, so the inspector only shows `Config`. Plus `PPCPlayerLink`, `PPCLadder`
      and events (`PPCJumped`, `PPCLanded`, `PPCCrouchChanged`, `PPCClimbStarted`, `PPCClimbEnded`).
- [x] Config: **one** `PPCConfig` asset with sections (`Body`, `Movement`, `Probes`, `Jump`, `Crouch`,
      `Climb`, `Platforms`) mirroring the Unity ScriptableObjects and their defaults. One asset ref per
      character instead of five.
- [x] Input: `PPCInputBridge` partial hook, implemented by the game in the same assembly (the harness
      fixture does this). Unlinked characters (bots) take `PPCCharacter.Input` as written.
- [x] Spawning: `PPCSetupSystem` (`ISignalOnComponentAdded<PPCCharacter>`) builds the dynamic body
      (rotation frozen, gravity 0, no sleeping) and capsule from the config, so prototypes only need
      `PPCCharacter`. `PPCSpawn.Character(...)` spawns from code with an optional player and view.
- [x] `PPCAggregateSystem` sums the layers (or the exclusive climb layer) and drives the body.
- [x] **Decided** the drive method: direct `body->Velocity = target` (equivalent to Unity's
      `AddForce(Δv/dt, Acceleration)`). Other bodies still push the character (tested).
- [x] **Decided** prototype MonoBehaviours: each project generates its own. Shipping them means also
      shipping the generated adapter class, because `NoUnityPrototypeWrapper` suppresses that too
      (found by running the generator), and keeping extracted generated code in sync. Nothing in the
      package references them: the sample spawns from code, and entity view prefabs don't need them.
- [ ] Default config asset in `Samples~/Playground` (because of `AssetSearchPaths`), in Phase 7
- [ ] Sample `input.qtn` + Unity input poller, in Phase 7 (view code)

Checkpoint: headless ✅ (`Phase1SkeletonTests`): configured from config, stands still on the floor
without tipping or drifting, input arrives through the bridge, a heavy box pushes it.

### Phase 2 — Probes & horizontal movement ✅

- [x] `PPCProbeSystem`: ground and ceiling use the Unity layout (centre ray plus a ring of 16), walls use
      4 axis rays; returns normal, slope angle and ground entity; `MaxSlopeAngle`; layer masks from the
      config; the character's own collider and triggers are ignored (`PPCProbe.Raycast`).
      **Fixed:** probe lengths follow the *current* capsule half-height, so they don't grow when crouched.
- [x] `PPCMovementLayerSystem`: camera-relative direction from `Input.LookYaw`; walk/run; acceleration,
      deceleration, faster reversal (dot < −0.1); `MaxVelocityChange` clamp; accelerates in the
      platform's frame (ready for Phase 5)
- [x] ~~Slope alignment~~ was ported here, then replaced in Phase 3 by ground following in the vertical
      layer (see Phase 3).
- [x] Steps: **fixed** to use the real capsule height (Unity hard-coded 1 m), plus a 1 cm minimum so
      flat ground never causes tiny lifts, and a walkable-slope check on the step top
- [x] Air control: `Movement.AirControl` (default 1 = Unity behaviour)
- [x] External horizontal absorption: deviation from last tick's contribution beyond
      `ExternalAbsorbThreshold`; linear friction on the ground, exponential drag in the air
- [x] `PPCStateSystem`: the Unity state machine (Climbing > Crouching > Jumping/Falling > Running/Walking/Idle)

Checkpoint: headless ✅ (`Phase2MovementTests`, 17): probes (grounded, airborne, too steep, ceiling,
wall), walk/run speeds and acceleration, camera yaw, deceleration, faster reversal, walls block, no
diagonal speed-up, low step climbed, tall step blocks, air control, ground/air kick absorption and decay.

### Phase 3 — Vertical layer, jump, ceilings ✅

- [x] `PPCVerticalLayerSystem`: accumulated Y, gravity × `Body.GravityScale`, platform-relative grounding,
      walk-off dismount, ceiling cancel, launch detection while grounded, airborne absorption of
      external vertical velocity
- [x] Jump (`PPCJumpSystem`): buffer + coyote on simulation time, `max(accumulated, jump + platformY)`
      so a launch isn't clamped. Events `PPCJumped` and `PPCLanded` (with impact speed).
- [x] **Fixed** the double jump from coyote time after a real jump
- [x] Ceiling hit cancels upward velocity
- [x] Unity workarounds: `skipExternalAbsorption` is **removed** (a jump changes `AccumulatedY`, not
      `LastTargetY`, so absorption is measured against what the body was driven to). Ignoring
      "grounded" during a jump is **kept, for a different reason**: the ground probe reaches 0.15 m below
      the feet and still sees the floor on the first ticks after takeoff.
- [x] **Changed from Unity: ground following.** While grounded, the vertical layer picks the vertical
      speed that keeps the horizontal motion along the ground surface, then closes any gap to the
      ground within a tick (snap). Unity's approach (vertical 0 plus a half-strength slope projection
      of the move direction) hovered up to 0.15 m above floors, and on slopes the solver's push-out
      tripped launch detection (reproduced here: a 20° ramp launched the character). The now-unused
      `SlopeAlignmentStrength` and `SlopeDetectionRayDistance` settings were removed.

Harness fix found here: scripted input received absolute frame numbers (Quantum doesn't start at 0),
so "tick N" inputs never fired. Scripts now get ticks relative to the first simulated tick.

Checkpoint: headless ✅ (`Phase3VerticalTests`, 12): falls and lands (event), jump apex 5 m, no bunny
hop while held, buffered press, too-early press forgotten, coyote jump, coyote expiry, no double jump,
ceiling stops the jump, launch pad, jump during a launch keeps the larger velocity, ramp up and down
stays grounded with the along-slope speed.

### Phase 4 — Crouch ✅

- [x] `PPCCrouchSystem`: hold-to-crouch like Unity (press crouches, release stands when there's room);
      capsule shape swap, then `ResetCenterOfMass` → `ResetInertia`
- [x] Feet stay planted when crouching on the ground; in mid-air the capsule shrinks towards the head.
      **Fixed** the landing bug: the capsule is always centred on the entity (the entity moves by the
      height difference), and standing up grows from the feet when grounded, or down first then up in
      the air.
- [x] Stand-up check: overlap test of a slightly slimmer standing capsule (more robust than Unity's
      ceiling rays)
- [x] Speed multiplier `Crouch.Speed / WalkSpeed`; `Crouch.MidAirBoost`; `PPCCrouchChanged` event
- Note: the Unity version lerped the capsule size over ~0.1 s. Here it changes in one tick (simulation
  state should be discrete); the view can smooth the camera.

Checkpoint: headless ✅ (`Phase4CrouchTests`, 9): feet planted, stands back up, slower crouch walk,
blocked standing under a bar, stays crouched under it and stands once clear, mid-air tuck keeps the
head, crouch jump gains ~1 m of clearance, mid-air boost, landing crouched then standing from the feet.

### Phase 5 — Moving platforms & external forces ✅

- [x] `PPCPlatformSystem`: ground entity → base velocity. Dynamic platforms use body velocity +
      angular × r (as in Unity). Kinematic and body-less platforms use the transform change since last
      tick, applied as an exact rigid motion. **Found:** Quantum 3 doesn't move kinematic bodies by
      their velocity (you move them and set velocity only for collision response), so trusting kinematic
      velocity would break common setups. Run platform movers before the controller to avoid a tick of lag.
- [x] Rotational carry: `Platform.YawDelta` (clamped by `MaxRotationSpeed`) for the view to turn the
      camera; the body's rotation stays frozen
- [x] Walk-off / jump-off keep the platform's momentum (vertical via `LastPlatformY`, horizontal via
      the platform-relative movement loop, as in Unity)
- [x] `PPCForces.AddVelocity` (launch pads) and `PPCForces.AddExplosion` (radial, linear falloff,
      upward bias)
- Note (Unity parity): a character that lands on a moving platform catches up at the
  acceleration/deceleration rate instead of snapping to its velocity.

Checkpoint: headless ✅ (`Phase5PlatformTests`, 8): kinematic, script-moved and dynamic platforms
carry without drift, walking is platform-relative, rotating disc carries around its circle and
reports yaw, elevator up and down stays grounded, jump-off keeps momentum, explosion pushes only
nearby characters.

### Phase 6 — Ladder climbing ✅

- [x] Ladder detection by component: a trigger collider on an entity with `PPCLadder` (overlap query each
      tick, `Climb.LayerMask`). A ladder you let go of isn't re-grabbed until you've left it (Unity got
      this from `OnTriggerEnter` semantics).
- [x] Exclusive climb layer: forward/back climbs, strafing moves sideways; the other layers hold,
      with the climb velocity as their baseline so letting go isn't treated as an external force
- [x] **Fixed:** snap to the ladder face (`SnapStrength`, along the ladder's facing axis); jump-off
      launch (`JumpOffVelocity`, up and away); look-down reversal needs `LookDownThreshold` (30°)
      instead of flipping at level. The unused `playerToLadder` doesn't exist here.
- [x] Let go on jump, on reaching the ground from above, or on leaving the volume (over the top).
      Events `PPCClimbStarted` and `PPCClimbEnded`.

Checkpoint: headless ✅ (`Phase6ClimbTests`, 8): grab and hold height, climb speed with face snap,
look-down reversal, a slight glance down doesn't reverse, strafe, jump-off launches away without
re-grabbing, climbing down to the floor lets go, climbing over the top lands on the ledge.

### Phase 7 — View layer ✅ *(needs the Unity checkpoint: not compiled here)*

- [x] `PPCCameraView` (`QuantumEntityViewComponent`): first-person camera for the local player's
      character. Yaw/pitch with sensitivity, invert and pitch limits; turns with rotating platforms
      (`Platform.YawDelta`); eye height follows crouch with view-side smoothing (the simulation's
      crouch is instant, like the Unity capsule lerp). Exposes `Local.Yaw/Pitch` for the input poller.
- [x] `PPCAnimatorView`: the Unity `PlayerAnimatorController` parameter names (`Speed`, `DirectionX/Y`,
      `IsGrounded`, `IsFalling`, `IsCrouching`, `IsRunning`, `IsSliding`, `IsClimbing`, `Jump` trigger
      from `PPCJumped`); parameters the animator doesn't have are skipped
- [x] `PPCDebugView`: state label and probe/velocity gizmos, optional (fixes "debug in production")
- [x] `PPCSystemGroup`: one SystemsConfig entry for all controller systems (tested to move the
      character exactly like the individual systems)
- [x] Playground sample (`Samples~/Playground`): the game's `input.qtn`, input bridge, spawn system
      (+ `RuntimeConfig` fields), Input System poller in its own asmdef (Quantum.Unity doesn't
      reference the Input System), setup README. `PPCConfig` has a create menu.
- Verification: the view and poller code **can't be compiled here** (no UnityEngine assemblies; a
  NuGet repackaging of Unity's binaries exists but is unlicensed, so it isn't used). Every
  Quantum/Unity API they call was checked against the SDK source (`QuantumEntityViewComponent`
  lifecycle, `QuantumEvent.Subscribe/UnsubscribeListener`, `PlayerIsLocal`, `ToUnityVector3`,
  `ToFPVector2`, menu paths). The sample's **simulation** code is compiled headlessly on every build.

Checkpoint: headless ✅ (sample simulation compiles, `PPCSystemGroup` equivalence test). Unity:
the view scripts compile; the Playground runs with camera, animator and crouch eye height.

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
| Headless harness uses SDK 3.0.0, package targets 3.0.13 | Unity checkpoint every phase; add `3.0.13/` to `quantum-sdk-libs` when available |
| Headless gate can't cover view code, prefabs or feel | Unity checkpoint at the end of every phase |

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
