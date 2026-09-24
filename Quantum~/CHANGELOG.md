# Changelog

## [0.1.0-preview.1] - Unreleased

### Changed
- **Defaults are now a recommended feel, derived from Source engine movement** (Source SDK 2013's
  defaults converted from inches to metres, for a 2 m character) and checked against Halo 3: walk 5 m/s,
  run 8 (was 10; HL2 sprint 8.1, Halo 3 ≈ 7.7), acceleration 50 m/s² (was 10; `sv_accelerate` 10 ×
  speed), deceleration 12 (was 10; `sv_friction` 4 + `sv_stopspeed`), reverse 60 (was 20), air control
  0.2 (was 1), max step 0.45 m (was 0.5; `sv_stepsize` 18), jump height 1.25 m (was 5), gravity × 2 =
  20 m/s² (was × 1; `sv_gravity` 800), no air drag on pushes (was 0.5; Source has none), crouch 1.6 m/s
  (was 2). `PPCConfig.ApplyUnityParity()` (also a right-click menu on the asset) restores the Unity
  numbers; `ApplyRecommendedFeel()` goes back. Both presets also set gravity scale, external drag and
  friction, and crouch speed.
- **One "grounded" for every system** (`Ground.IsGrounded`): it turns off on the tick the character jumps
  or is launched and stays off until it stops rising. Before, the vertical layer kept its own flag, so for
  a few ticks after a jump the state read Walking and the movement layer used ground acceleration.
  Rising is measured against the ground's own velocity, which is now tracked while airborne too
  (`Platform.GroundVelocity`), so riding a lift isn't mistaken for a jump.
- **Stairs:** while grounded, the ground probe reaches down `MaxStepHeight` (Source's `StayOnGround`), so
  walking down stairs and off ledges up to a step high stays on the ground. Before, any drop deeper than
  the 0.15 m probe margin was a short fall (19 airborne ticks down a flight of 0.2 m stairs).
- **Characters aren't moving platforms** by default: you can stand on someone's head, but it doesn't carry
  you when they move. `Movement.CarriedByCharacters` turns carrying back on.
- Crouching ignores run: crouched speed is `Crouch.Speed` (the Unity version allowed crouch-running at
  run speed × crouch/walk, 4 m/s by default).
- `Jump.Force` (N·s, divided by mass) is replaced by `Jump.Height` (m), so mass and gravity no longer
  change jump height.
- `Probes.GroundCheckDistance`/`CeilingCheckDistance` (measured from the capsule centre) are replaced by
  `GroundProbeMargin` (0.15 m) / `CeilingProbeMargin` (0.1 m), which don't change when `StandingHeight` does.
- Rarely needed settings moved under `Advanced`: `StepProbeDistance`, `ExternalAbsorbThreshold`,
  `ProbeRingRadius` (was `RadiusMultiplier`), `WallCheckDistance`, `MaxPlatformYawSpeed` (was `MaxRotationSpeed`).
- Renamed state: `Horizontal.LastContribution` → `Contribution`, `Vertical.LastTargetY` → `TargetY`.

### Added
- The config is optional: characters without one use `PPCConfig.Default`.
- Characters without `Body.Material` get a shared frictionless material automatically (default friction
  slowed sliding along walls from 3.5 to 1.6 m/s).
- Helpers: `FPVector3.Flat()`, `PPCInput.MoveDirection`/`CameraRight`, `PPCCharacter.JumpPressed`/`CrouchPressed`,
  `PPCProbe.OverlapsOther`/`FindOverlapping<T>`, `PPCCapsule.Shape(..., inset)`.
- Golden trace test locking the four-player scenario's behaviour for refactors.
- `PPCCameraView` smooths the camera over steps (Source's `SmoothViewOnStairs`): the simulation lifts the
  character onto a step, or snaps it down one, in a single tick. `SmoothSteps`, `StepSmoothSpeed`,
  `MaxStepLag`.
- Tests for stairs and ledges, character stacking and grounded consistency.

### Removed
- `Movement.MaxVelocityChange` (no effect at any practical acceleration), `Platforms.VelocityMultiplier`
  (anything but 1 made characters slide on platforms), unused `JumpedThisAirtime`, `Horizontal.Target`,
  `Horizontal.SpeedMultiplier`.

### Performance
- Probe ring directions computed once; ceiling rays skip detailed hit info and stop at the first hit.

## [0.1.0-preview.0]

First port of the Physics Player Controller to Photon Quantum 3 (targets 3.0.x).

### Added
- `PPCCharacter` component with per-layer state, one `PPCConfig` asset (Unity defaults at the time), `PPCPlayerLink`,
  `PPCLadder`, and events `PPCJumped`, `PPCLanded`, `PPCCrouchChanged`, `PPCClimbStarted`, `PPCClimbEnded`.
- Systems (in order, or as one `PPCSystemGroup`): setup, input, probes, platforms, crouch, jump, climb,
  horizontal layer, vertical layer, aggregate, state.
- `PPCInputBridge` partial hook for game input; `PPCSpawn.Character`; `PPCForces.AddVelocity/AddExplosion`.
- View components: `PPCCameraView`, `PPCAnimatorView` (Unity animator parameter names), `PPCDebugView`.
- Playground sample: input definition and poller, input bridge, spawn system, setup guide.
- Headless .NET harness (`Tests~`): Quantum CodeGen, compile, 72 tests including determinism and a
  Debug-vs-Release checksum comparison.

### Fixed (compared with the Unity package)
- Double jump via coyote time after a real jump.
- Hovering up to 0.15 m above floors, and false launches on slopes (ground following + snap).
- Mid-air crouch leaving the capsule off-centre after landing.
- Step height assuming a 2 m capsule; probe lengths growing when crouched.
- Ladders: snap to the ladder face, jump-off launch, look-down reversal threshold.

### Changed (compared with the Unity package)
- Slopes are followed by the vertical layer; `SlopeAlignmentStrength` and `SlopeDetectionRayDistance` are gone.
- Crouch resizes the capsule instantly (the view smooths the camera).
- Kinematic platforms are tracked by transform (Quantum doesn't move kinematic bodies by velocity).
