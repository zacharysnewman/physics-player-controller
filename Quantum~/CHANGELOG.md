# Changelog

## [0.1.0-preview.1] - Unreleased

### Changed
- **Defaults are now a recommended feel:** run 7 m/s (was 10), acceleration/deceleration 40 m/s² (was 10),
  reverse 80 (was 20), air control 0.4 (was 1), max step 0.35 m (was 0.5), jump height 1.25 m (was 5).
  `PPCConfig.ApplyUnityParity()` (also a right-click menu on the asset) restores the Unity numbers;
  `ApplyRecommendedFeel()` goes back.
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
