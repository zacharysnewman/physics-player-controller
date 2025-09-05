# Bug Tracker

## Known Issues

### 1. Ceiling Check Debug Sphere Position
- **Description**: Debug sphere for ceiling check appears 5m above the player instead of at the correct ceiling check position
- **Impact**: Misleading debug visualization for ceiling detection
- **Status**: Open
- **Priority**: Low
- **Root Cause**: Position mismatch between actual ceiling check and debug visualization
  - GroundChecker.cs:70 uses `capsule.center + Vector3.up * (capsule.height / 2f - capsule.radius * 0.1f)`
  - DebugVisualizer.cs:54 uses `Vector3.up * (capsule.height / 2f - capsule.radius)` (missing capsule.center and wrong radius multiplier)

### 2. Grounded Check Ray Origin
- **Description**: Grounded check rays originate from the bottom of the player instead of the middle, causing rays to sometimes start inside the floor and miss ground detection (returning false when player should be grounded)
- **Impact**: Inconsistent grounded state, potential movement issues on certain surfaces
- **Status**: Open
- **Priority**: Medium
- **Root Cause**: Ground check rays originate from capsule bottom instead of capsule center
  - GroundChecker.cs:45 sets groundCheck to `capsule.center + Vector3.down * (capsule.height / 2f - capsule.radius * 0.1f)`
  - Should raycast from capsule center without downward offset to avoid starting inside floor geometry
  - Note: Ray distance will need to be increased by capsule height/2 to maintain same detection range