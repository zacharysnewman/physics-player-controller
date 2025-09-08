# Task-Based Implementation Plan (RigidBody Player Controller with Crouch Jump + Step Handling + Responsive Direction Switching + Configurable Parameters + Debug)

## Phase 1 – Core Foundations

### Task 1: Player Setup

- Add Rigidbody (interpolate, continuous collision, freeze X/Z rotation) [Editor]
- Configure Rigidbody properties (mass, drag, angularDrag, interpolation, collisionDetectionMode) on component [Editor]
- Add CapsuleCollider [Editor]
- Configure CapsuleCollider properties (height, radius, center) on component [Editor]
- Create child transforms: GroundCheck, CeilingCheck, FrontCheck (for ladder detection) [Editor]
- Debug / Gizmos Config:
  - Optionally visualize Rigidbody and CapsuleCollider bounds in editor [Code]

### Task 1a: Camera Setup

- Add Main Camera to scene or player prefab [Editor]
- Configure camera position and rotation (e.g., third-person follow) [Editor]
- Optionally integrate Cinemachine Virtual Camera for smooth tracking [Editor]
- Ensure Camera.main is accessible in code [Editor]

### Task 2: Input System Integration

- Create InputActions asset using new Input System [Editor]
- Define actions: Move (Vector2), Look (Vector2), Run (Button), Crouch (Button), Jump (Button), Interact (Button) [Editor]
- Add PlayerInput component to player prefab and link script [Editor]
- Write handler methods in script (OnMove, OnLook, OnRun, etc.) [Code]
- Implement camera look integration: rotate camera based on Look input (mouse delta) [Code]
- Configurable: mouseSensitivity, invertY
- Debug / Gizmos Config:
  - Optionally log input vector, button presses for testing [Code]

### Task 3: Ground / Ceiling Checks

- Implement CheckGrounded() using Physics.SphereCast [Code]
- Configurable: groundCheckRadius, groundCheckDistance, groundLayerMask
- Store isGrounded, groundNormal, groundSlopeAngle [Code]
- Implement CheckCeiling() using upward spherecast [Code]
- Configurable: ceilingCheckRadius, ceilingCheckDistance, ceilingLayerMask
- Debug / Gizmos Config:
  - Draw ground and ceiling check spheres [Code]
  - Optionally visualize groundNormal vector [Code]

## Phase 2 – Basic Locomotion

### Task 4: Walk & Run

- Implement HandleMovement():
  - Read input vector, convert to world space relative to camera, project onto ground plane [Code]
  - Compute target velocity (walk vs run) [Code]
  - Configurable: walkSpeed, runSpeed
- Debug / Gizmos Config:
  - Draw velocity vector [Code]
  - Optionally show projected movement direction [Code]

### Task 4a: Direction Responsiveness (Acceleration & Deceleration)

- Implement velocity smoothing:
  - Vector3.SmoothDamp or custom acceleration toward target velocity [Code]
  - Apply faster deceleration when input reverses direction [Code]
  - Configurable: acceleration, deceleration, maxVelocityChange
- Debug / Gizmos Config:
  - Display smoothed velocity vs raw input velocity [Code]

### Task 5: Jump (with Buffer + Coyote Time)

- Add jump buffer (timestamp) [Code]
- Configurable: jumpBufferTime
- Add coyote time (allow jump shortly after leaving ground) [Code]
- Configurable: coyoteTime
- On valid jump: reset vertical velocity, apply upward impulse [Code]
- Configurable: jumpForce
- Debug / Gizmos Config:
  - Draw line for jump apex height [Code]
  - Show timers for jump buffer and coyote time [Code]

### Task 6: Crouch + Crouch Jump

- Grounded Crouch: shrink capsule downward (or optionally upward), lower camera, reduce speed
- Configurable: crouchHeight, crouchSpeed, groundedCrouchUpwardShrink
- Note: Handle upward shrink properly to avoid jitter [Code]
- Midair Crouch: shrink capsule upward (lift legs), optional slight velocity boost
- Configurable: midAirCrouchUpwardShrink, midAirCrouchBoost [Code]
- Prevent uncrouch if ceiling blocked [Code]
- Debug / Gizmos Config:
  - Draw current capsule collider dimensions [Code]
  - Optionally show crouch state flags in editor [Code]

## Phase 3 – Environment-Driven Interactions

### Task 7a: Smooth Terrain Navigation

- Transform player's movement velocity to align with terrain normals for slope matching [Code]
- Use raycasts in movement direction to determine slope and adjust velocity accordingly [Code]
- Ensure movement on x/z axis follows terrain contours, not flat horizontal velocity [Code]
- Configurable: raycast distance, number of rays, slope alignment strength [Code]
- Raycast diagonally from player's center to ground to detect walls, steps, and slopes [Code]
- Account for player height (2m standing, 1m crouching) and configurable offsets [Code]
- Handle cases where raycast hits nothing (e.g., fall or edge detection) [Code]
- Auto dismount ladder when becoming grounded: dismount from ladder when transitioning from not grounded to grounded while on ladder, allowing normal movement on ground after climbing down to the ground
- Debug / Gizmos Config:
  - Visualize slope detection rays and adjusted velocity vectors [Code]

### Task 7b: Step Handling

- Detect obstacle in front [Code]
- Adjust vertical position if height ≤ maxStepHeight [Code]
- Configurable: maxStepHeight
- Update movement vector projection onto ground plane [Code]
- Debug / Gizmos Config:
  - Highlight stepped obstacles in scene [Code]

### Task 8: Ladder Climb

- Add ladder trigger volumes [Editor]
- On enter: set isClimbing [Code]
- Disable gravity, move player along ladder axis based on input [Code]
- Configurable: ladderClimbSpeed, ladderLayerMask [Code]
- Use ladderLayerMask to filter trigger volumes [Code]
- Allow horizontal movement (left/right) on ladder [Code]
- Make vertical movement relative to ladder direction (forward/backward input controls up/down based on direction toward/away from ladder) [Code]
- Exit if jump pressed or ladder ends [Code]
- Debug / Gizmos Config:
  - Draw ladder climb trigger bounds [Code]
  - Show climbing axis vector [Code]

### Task 9: Moving Platform Support

- Detect when player is grounded on any moving object [Code]
- Cache reference to the object the player is standing on [Code]
- Track the object's movement between frames [Code]
- Apply the object's velocity to player while grounded on it [Code]
- Handle object rotation [Code]
  - Rotate player to match object's orientation changes [Code]
- Configurable: velocityMultiplier
- Debug / Gizmos Config:
  - Visualize object velocity vector [Code]
  - Highlight moving objects the player is standing on [Code]

## Phase 4 – Movement Feel & Air Control

### Task 10: Sliding (Slope Only)

- Detect isSliding if groundSlopeAngle > slopeLimit [Code]
- Configurable: slopeLimit, slideForceMultiplier
- Apply slope movement force downward [Code]
- Block manual uphill control [Code]
- Debug / Gizmos Config:
  - Draw slope normal and slope angle in scene [Code]
  - Optionally highlight sliding zones [Code]

### Task 11: Air Control

- Reduce horizontal influence on velocity while in air [Code]
- Configurable: airControlMultiplier
- Prevent over-acceleration mid-air [Code]
- Debug / Gizmos Config:
  - Visualize air control vector [Code]

## Phase 5 – State & Animator Integration

### Task 12: State Machine

- Define enum: Idle, Walking, Running, Crouching, Sliding, Jumping, Falling, Climbing [Code]
- Transition rules based on checks + input [Code]
- Debug / Gizmos Config:
  - Display current state text in editor [Code]

### Task 13: Animator Sync

- Setup Animator parameters: [Editor]
  - Speed (float), IsGrounded (bool), IsCrouching (bool), IsSliding (bool), IsClimbing (bool), Jump (trigger)
- Update Animator in LateUpdate() with current state machine flags [Code]
- Use root motion only for climbing animations [Editor/Code]
- Debug / Gizmos Config:
  - Optionally show parameter values in editor runtime [Code]

## Phase 6 – Debugging & Polish

- Most debug functionality is now integrated per task, but optionally:
- Toggle all gizmos / debug displays via global PlayerDebugSettings [Editor/Code]
- Inspect runtime values for grounded, sliding, jump buffer, crouch, climbing [Code]
