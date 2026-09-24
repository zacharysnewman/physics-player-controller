# Changelog

## [0.1.0-preview.0] - Unreleased

First port of the Physics Player Controller to Photon Quantum 3 (targets 3.0.x).

### Added
- `PPCCharacter` component with per-layer state, one `PPCConfig` asset (Unity defaults), `PPCPlayerLink`,
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
