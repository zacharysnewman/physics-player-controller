# Physics Player Controller — Full Evaluation

> Sections §2–§15 reflect the state of the **main** branch at the time of the original review. §16 documents the velocity layer system introduced in that update. §17 documents all fixes applied in the subsequent review pass.

---

## Table of Contents

1. [Overall Architecture](#1-overall-architecture)
2. [PlayerController (Orchestrator)](#2-playercontroller-orchestrator)
3. [PlayerInput](#3-playerinput)
4. [PlayerMovement](#4-playermovement)
5. [PlayerJump](#5-playerjump)
6. [PlayerCrouch](#6-playercrouch)
7. [PlayerClimb](#7-playerclimb)
8. [GroundChecker](#8-groundchecker)
9. [CameraController](#9-cameracontroller)
10. [PlayerAnimatorController](#10-playeranimatorcontroller)
11. [Configuration System](#11-configuration-system)
12. [Cross-Cutting Issues](#12-cross-cutting-issues)
13. [Missing Features by System](#13-missing-features-by-system)
14. [Bloat](#14-bloat)
15. [Summary Table](#15-summary-table)
16. [Velocity Layer System](#16-velocity-layer-system-main-branch)
17. [Applied Fixes — Review Pass 2](#17-applied-fixes--review-pass-2)

---

## 1. Overall Architecture

### What Works Well

- **Component-per-concern** split is clean and readable. Each system is independently comprehensible.
- `RequireComponent` attributes enforce the dependency graph at edit time, preventing broken prefabs.
- `FixedUpdate` for physics forces, `Update` for input polling — the intent is correct.
- ScriptableObject configs per system allow per-game-object overrides in the inspector.
- Comprehensive `DebugVisualizer` with per-system toggles is a strong developer experience feature.

### Structural Problems

- **Ground checks run in `Update`, movement applies in `FixedUpdate`.** `UpdateGrounded()` is called from `PlayerController.Update()`, but `HandleMovement()` runs in `FixedUpdate()`. On frames where the physics step fires multiple times (or not at all), the grounded state used by movement may be one frame stale. This is especially visible at the transition between grounded and airborne on slow-framerate machines. Ground checks should be polled in `FixedUpdate` or cached per-physics-step.

- **All components sit on the same GameObject.** This is a deliberate and defensible design for performance (no child lookups), but it means `CameraController`, which conceptually belongs on a camera child, is on the root instead, requiring a manual inspector reference to the camera. This pattern is uncommon and not immediately obvious to users of the package.

---

## 2. PlayerController (Orchestrator)

### Issues

#### `RequireComponent(typeof(DebugVisualizer))` is a Production Dependency
`DebugVisualizer` is a hard dependency of `PlayerController`. Removing it from the prefab removes `PlayerController`. This means debug visualization code ships in every build regardless of whether it is used.

**Proposed Fix:** Remove `RequireComponent(typeof(DebugVisualizer))`. Make `DebugVisualizer` optional — all the `if (debugVisualizer != null)` null checks already exist elsewhere in the codebase, so the pattern is familiar. Each system that calls into `DebugVisualizer` already stores a private reference; these simply go null if the component is absent.

**Side Effects:** The debug reference in `PlayerController.debugVisualizer` and the getter `GetDebugVisualizer()` become optional. `ValidateRequiredComponents()` would need to stop treating it as an error.

---

#### `showStateDebug = true` Default — OnGUI in Production
`PlayerController.showStateDebug` defaults to `true`, causing an `OnGUI` label to render every frame. Same issue in `PlayerAnimatorController.showParameterDebug = true`.

**Proposed Fix:** Default both to `false`. These are developer conveniences, not end-user features.

---

#### Redundant Component Getter Methods
`GetPlayerInput()`, `GetPlayerMovement()`, etc. are getters for components on the same GameObject. Callers can use `GetComponent<T>()` directly. These methods add API surface with no benefit.

**Consideration:** They do provide a stable public API if the architecture ever moves components to different GameObjects. Keep or remove based on whether external scripts depend on them.

---

## 3. PlayerInput

### Issues

#### `inputSensitivity` Applied to `MoveInput`
`Scripts/PlayerInput.cs:50–51`:
```csharp
MoveInput = playerInputComponent.actions["Move"].ReadValue<Vector2>() * inputSensitivity;
LookInput = playerInputComponent.actions["Look"].ReadValue<Vector2>() * inputSensitivity;
```
Sensitivity is a look/mouse concept. Multiplying it into `MoveInput` means full WASD input yields a magnitude less than 1 when sensitivity ≠ 1. All downstream speed calculations assume `MoveInput.magnitude == 1` at full input, so any sensitivity value other than 1.0 silently scales the player's top speed.

**Proposed Fix:** Remove the `* inputSensitivity` from the `MoveInput` line. Apply sensitivity only to `LookInput`. Or rename `inputSensitivity` to `lookSensitivity` to clarify intent and keep it on look only. The camera has its own `MouseSensitivity` in config — these two sensitivity values also stack, which is a separate confusion.

**Side Effects:** If any existing project has relied on `inputSensitivity` affecting movement speed as a workaround, removing it changes behavior. Since this is a package, it's a breaking change worth noting in the changelog.

---

#### `GetComponent` Called on Debug Property Getters
```csharp
public string CurrentControlScheme => GetComponent<UnityEngine.InputSystem.PlayerInput>()?.currentControlScheme ?? "None";
public int PlayerIndex => GetComponent<UnityEngine.InputSystem.PlayerInput>()?.playerIndex ?? -1;
```
`playerInputComponent` is already cached in `Awake()`. These properties call `GetComponent` every time they are accessed. Since they are likely polled each frame by a debug display, this is wasteful.

**Proposed Fix:** Use `playerInputComponent?.currentControlScheme` and `playerInputComponent?.playerIndex` instead.

---

#### Mouse Lock Logic Belongs in `CameraController`
`PlayerInput` directly calls `cameraController.ToggleMouseLock()` in response to `Menu` and `Use` inputs. This couples the input layer to a specific behavior (mouse locking) that belongs to the camera system. `PlayerInput` should fire events (`OnMenuAction`, `OnUseAction` already exist), and `CameraController` should subscribe to them.

**Proposed Fix:** Remove the direct `cameraController` reference and the lock calls from `PlayerInput.Update()`. Move the subscription logic to `CameraController.Awake()` or `Start()`:
```csharp
playerInput.OnMenuAction += () => { if (IsMouseLocked) ToggleMouseLock(); };
playerInput.OnUseAction  += () => { if (!IsMouseLocked) ToggleMouseLock(); };
```
**Side Effects:** `cameraController` field is removed from `PlayerInput`, simplifying the component. The reference and null-check in `PlayerInput.Start()` are also eliminated.

---

## 4. PlayerMovement

### Bugs

#### Step Height Hardcodes Capsule Half-Height
`Scripts/PlayerMovement.cs:331`:
```csharp
float playerBottomY = transform.position.y - 1f; // Assuming capsule height 2, center at 0
```
This hardcodes a half-height of 1. During a crouch (capsule height = 1, center adjusted downward), `playerBottomY` is calculated incorrectly — the step may fire even when there is no step (negative stepHeight), or fail to step when there is one.

**Proposed Fix:**
```csharp
float playerBottomY = transform.position.y + capsule.center.y - capsule.height / 2f;
```
This uses the actual live capsule dimensions, so it remains correct during crouch.

**Side Effects:** Step behavior changes during crouch. A crouching player can now properly auto-step up to `MaxStepHeight`. This is likely the desired behavior. Verify that the step raycast origin (`transform.position + moveDirection.normalized * 0.51f`) is still appropriate for the crouching capsule radius.

---

#### `GetComponent<Rigidbody>()` Called Every Physics Step in `TrackPlatformMovement()`
Inside the non-kinematic platform branch of `TrackPlatformMovement()`, which now runs every `FixedUpdate` via `GetVelocityContribution()`:
```csharp
Rigidbody platformRb = currentPlatform.GetComponent<Rigidbody>();
```
This `GetComponent` call fires every physics step while on a platform. The platform Rigidbody should be cached when `currentPlatform` is assigned in `UpdateGrounded()` and cleared when it is set to null.

**Proposed Fix:** Add a `private Rigidbody currentPlatformRb` field. Assign it alongside `currentPlatform = groundObject` in `UpdateGrounded()`, and clear it with `currentPlatformRb = null` when leaving the platform.

---

#### No Air Control Reduction
Full acceleration and deceleration rates apply identically in the air and on the ground. The player can reverse direction instantly in mid-air. Most physics controllers reduce `accelRate` while airborne by a configurable multiplier.

**Proposed Fix:** Add `[SerializeField] float airControlMultiplier = 0.3f` to `PlayerMovementConfig`. Apply it in `HandleMovement()`:
```csharp
if (!isGrounded) accelRate *= config.AirControlMultiplier;
```
**Side Effects:** Jumps feel more committed. Players can no longer steer precisely mid-jump, which may be desired or not depending on game genre. Make the multiplier configurable and default it to something close to 1 if you want minimal change.

---

## 5. PlayerJump

### Bugs

#### Double-Jump Possible via Coyote Time After a Real Jump
`Scripts/PlayerJump.cs:68–70`:
```csharp
if (!groundChecker.IsGrounded && wasGrounded)
{
    coyoteTimer = config.CoyoteTime;
}
```
`coyoteTimer` starts whenever the player becomes non-grounded — including immediately after a real jump. On the first frame after jumping, `wasGrounded` is still `true` (it's set at the end of `Update`), so `coyoteTimer` is set to `CoyoteTime`. If the jump buffer also has remaining time (e.g., the player pressed jump twice quickly), `canJump = (coyoteTimer > 0) && (jumpBufferTimer > 0)` evaluates to `true`, and a second jump fires.

**Proposed Fix:** Guard coyote time activation on `!isJumping`:
```csharp
if (!groundChecker.IsGrounded && wasGrounded && !isJumping)
{
    coyoteTimer = config.CoyoteTime;
}
```
**Side Effects:** None — a player who intentionally jumped cannot use coyote time to double jump. This is the correct behavior for coyote time.

---

#### Redundant Nested `if (debugLogging)` Checks
`Scripts/PlayerJump.cs:71–74` and `95–98`:
```csharp
if (debugLogging)
{
    if (debugLogging) Debug.Log(...); // inner check is always true here
}
```
The inner check is always `true` when the outer check passes.

**Proposed Fix:** Remove the outer `if (debugLogging)` wrapper, keeping only the inner check. Or remove the inner check. Either form is correct.

---

### Missing Features

- **Variable jump height (cut on release):** The most common feel improvement for jumps. When the player releases jump before the apex, apply a downward force multiplier or hard cap the upward velocity. Requires `WasPressedThisFrame` vs `IsPressed` distinction already available in the Input System.
- **Directional launch on ladder exit:** `ExitClimb()` re-enables gravity but applies no impulse. The player drops straight down from where they were on the ladder.

---

## 6. PlayerCrouch

### Bugs

#### Mid-Air Crouch Leaves Wrong Capsule Center on Landing
`Scripts/PlayerCrouch.cs:86–107`: Grounded crouch shrinks center *down* (`originalCenter + Vector3.down * ...`). Mid-air crouch shrinks center *up* (`originalCenter + Vector3.up * ...`). But when the player crouches mid-air and then lands while still crouching, the capsule has its center shifted upward (mid-air crouch position). The correction from up-center to down-center never happens.

**Effect:** A crouching player who landed after a crouch-jump has a capsule that floats above the ground. Collisions, ground checks, and visuals will be subtly wrong until the player releases crouch and re-crouches.

**Proposed Fix:** In `Update()`, detect the transition from airborne-crouching to grounded-crouching and reapply the grounded center:
```csharp
if (isCrouching && groundChecker != null)
{
    Vector3 correctCenter = groundChecker.IsGrounded
        ? originalCenter + Vector3.down * (originalHeight - config.CrouchHeight) / 2f
        : originalCenter + Vector3.up  * (originalHeight - config.CrouchHeight) / 2f;
    currentCenter = correctCenter;
}
```
**Side Effects:** The capsule center snaps (or lerps, since interpolation is applied in `Update`) between the two positions on landing. The camera height offset may also need recomputing on that transition.

---

#### Camera Height Formula Potentially Double-Counts
`Scripts/PlayerCrouch.cs:112`:
```csharp
float heightAdjustment = (currentCenter.y - originalCenter.y) + (currentHeight - originalHeight) / 2f;
```
For a grounded crouch: `currentCenter.y - originalCenter.y` is already `-(originalHeight - crouchHeight) / 2f`. Then `(currentHeight - originalHeight) / 2f` is also `-(originalHeight - crouchHeight) / 2f`. These are equal, so the total offset is twice the intended value. The camera drops twice as far as the capsule shrinks.

**Example:** Original height 2, crouch height 1. Center shifts down by 0.5. `heightAdjustment = -0.5 + -0.5 = -1.0`. But the top of the capsule only dropped by 0.5. Camera drops 1.0.

**Proposed Fix:** The camera should track the capsule's top surface, which moves by `(currentHeight - originalHeight) / 2f + (currentCenter.y - originalCenter.y)`. Wait — this is exactly the formula. But a grounded crouch already adjusts center to keep the bottom fixed, so the top drops by exactly `(originalHeight - crouchHeight)`. Let's verify:
- Original: center = (0, 1, 0), height = 2. Top = 2.
- Crouched ground: center = (0, 0.5, 0), height = 1. Top = 1.
- Top moved: -1. Formula: `(0.5 - 1.0) + (1 - 2) / 2 = -0.5 + -0.5 = -1.0`. ✓

Actually the formula is *correct for tracking the top of the capsule*. However, this means the camera drops by the full height difference (1 unit), not half. Decide whether the camera should track the top of the capsule or follow the capsule center. For a first-person controller, the camera near the top is correct.

**Revised Assessment:** The formula produces the right result *if* the intent is to match the camera to the top of the capsule. The comment says "maintain offset from top of collider" — so this is actually correct. Remove this from the bugs list; clarify with a comment explaining why both terms are summed.

---

### Design Notes

- `wasCrouchPressed` on line 65 (`if (crouchInput && !wasCrouchPressed)`) combined with `else if (!crouchInput && isCrouching)` creates "hold to crouch." The `!wasCrouchPressed` guard is not strictly needed since `!isCrouching` already prevents re-entry on sustained hold, but it does prevent repeated re-attempts each frame if someone toggles rapidly. It's harmless but slightly confusing. A comment explaining the intent would help.
- `CrouchHeight` is in `PlayerCrouchConfig` but not `PlayerMovementConfig`, even though `PlayerMovementConfig` has `StandingHeight` and `CrouchingHeight` (used for terrain navigation). These should be the same value, but they're configured in two different places and can drift.

---

## 7. PlayerClimb

### Bugs

#### `playerToLadder` Is Calculated but Never Used
`Scripts/PlayerClimb.cs:180–182`:
```csharp
Vector3 playerToLadder = debugLadderBounds.center - transform.position;
playerToLadder.y = 0f;
playerToLadder.Normalize();
```
This variable is computed in `HandleClimbMovement()` every physics frame but is only referenced in `VisualizeLadder()` (the debug gizmo method), which recomputes it independently. It plays no role in movement calculations.

**Proposed Fix:** Remove the three lines from `HandleClimbMovement()`. They are dead code — the variable is unused in that method's scope.

---

#### Camera Pitch Inversion Zone Is Binary with a Hair-Trigger
`Scripts/PlayerClimb.cs:194`:
```csharp
bool isLookingUp = cameraForward.y > 0.0f;
```
Any camera pitch above exactly 0° (looking at all upward) switches to "looking up" mode. Since a player facing a ladder will naturally have their camera pitched upward (to see the top), this means the inversion switches erratically near horizontal. `isLookingDown`, `isLookingUp`, and the `else` (level) branch all behave differently, but the "level" band has zero width.

**Proposed Fix:** Use a dead zone:
```csharp
bool isLookingDown = cameraForward.y < -0.3f;
bool isLookingUp   = cameraForward.y >  0.3f;
// else: treat as level, use verticalInput directly
```
**Side Effects:** In the dead zone (camera near horizontal), input is used directly (same as "looking up" — so effectively no change there). The inversion transition becomes less abrupt. The threshold value of 0.3 is approximately 17° from horizontal; tune to taste.

---

#### No Player Snapping to Ladder Face
The player's horizontal position on the ladder is unconstrained. They can drift sideways and exit the trigger collider, causing an abrupt `OnTriggerExit` and `ExitClimb()`. This is especially problematic if the ladder collider is narrow.

**Proposed Fix:** In `HandleClimbMovement()`, apply a gentle spring force toward the ladder center on the horizontal plane:
```csharp
Vector3 toCenter = debugLadderBounds.center - transform.position;
toCenter.y = 0f;
float snapStrength = 5f; // configurable
rb.AddForce(toCenter * snapStrength, ForceMode.Acceleration);
```
Or use `playerToLadder` (the already-computed direction that is currently dead code) as the basis for this correction.

---

#### Jump-Off Ladder Has No Launch Impulse
`Scripts/PlayerClimb.cs:143–157`: `ExitClimb()` re-enables gravity and sets `isClimbing = false`. No velocity is applied. The player drops straight down from the ladder.

**Proposed Fix:** In `ExitClimb()`, optionally apply a small impulse in the direction the player is facing:
```csharp
if (jumpExit) // differentiate between jump-exit and auto-dismount
{
    rb.AddForce(transform.forward * exitImpulseForward + Vector3.up * exitImpulseUp, ForceMode.Impulse);
}
```
This could be driven by configurable values in `PlayerClimbConfig`.

---

#### `IsOverlappingLadder()` Uses Hardcoded Box Size
`Scripts/PlayerClimb.cs:246`:
```csharp
Collider[] colliders = Physics.OverlapBox(transform.position, Vector3.one * 0.5f, Quaternion.identity, config.LadderLayerMask);
```
The 0.5-unit half-extents are hardcoded. For a small ladder or a large player capsule, this may produce false negatives (player is still on the ladder but overlap check says no) or false positives (adjacent ladder triggers the check erroneously).

**Proposed Fix:** Use the capsule's actual radius as the overlap half-extent, or expose a configurable `LadderOverlapRadius` in `PlayerClimbConfig`.

---

## 8. GroundChecker

### Bugs

#### Ground Raycasts Originate from Capsule Center, Not Bottom
`Scripts/GroundChecker.cs:67`:
```csharp
groundCheck.localPosition = capsule.center;
```
For a standard capsule (height = 2, center = (0, 1, 0)), the center is at world-Y = 1 (assuming player at Y = 0). Raycasts go downward with distance 1.15. They detect ground at Y = 1 − 1.15 = −0.15 — slightly below the capsule's actual bottom (Y = 0). This works by accident for the default configuration, but:

1. Any capsule size other than height = 2 requires re-tuning `GroundCheckDistance`.
2. During crouch (center shifts downward), the check origin shifts down too — potentially firing ground detection while the player is mid-air after a jump, because the origin is now closer to the ground.
3. Not self-documenting — future maintainers will not understand why `GroundCheckDistance = 1.15`.

**Proposed Fix:** Set the check origin to the bottom sphere of the capsule:
```csharp
groundCheck.localPosition = capsule.center - Vector3.up * (capsule.height / 2f - capsule.radius);
```
With this origin, a short `GroundCheckDistance` (e.g., 0.1–0.2) reliably detects ground just below the capsule's actual surface, and the value is geometrically meaningful.

**Side Effects:** The default `GroundCheckDistance = 1.15` would need to be changed to ~0.1–0.15. Update the `DefaultConfigs` asset accordingly. Any project using this package would need to update their config asset.

---

#### Same Issue for Ceiling Raycasts
`Scripts/GroundChecker.cs:71`:
```csharp
ceilingCheck.localPosition = capsule.center;
```
For a capsule with height = 2, center = (0, 1, 0), the top of the capsule is at Y = 2. The ceiling check origin is at Y = 1, so `CeilingCheckDistance = 1.1` reaches Y = 2.1 — just above the capsule top. This is marginally correct for the default size. Same problem as ground: breaks with non-default capsule sizes and during crouch.

**Proposed Fix:** Set ceiling check origin to the top sphere of the capsule:
```csharp
ceilingCheck.localPosition = capsule.center + Vector3.up * (capsule.height / 2f - capsule.radius);
```

---

#### Wall Check Distance Is Hardcoded
`Scripts/GroundChecker.cs:192`:
```csharp
float wallCheckDistance = 0.16f; // Fixed wall check distance
```
Not in config. Not configurable. The 0.16 value has no geometric rationale documented.

**Proposed Fix:** Add `WallCheckDistance` to `GroundCheckerConfig`. A sensible default would be `capsule.radius * 0.1f` — just enough to detect contact without reaching through thin walls.

---

#### Wall Check Uses Global Axis Directions
`Scripts/GroundChecker.cs:191`:
```csharp
Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
```
These are world-space axes. A wall at 45° to the world axes (diagonal wall) will not be detected. The check misses any surface that doesn't face cardinally.

**Proposed Fix:** Use 8 directions at 45° intervals, or use player-relative axes (`transform.forward`, `transform.right`, etc.). The latter is more useful for "wall to my left/right" queries.

**Side Effects:** `IsTouchingWall` is currently detected but never used by any system. This is a medium-priority fix — but the wall detection results are only ever consumed if another system (e.g., wall-jump) is added.

---

#### Debug Logging Spams 34+ Lines Per Frame
When `debugLogging = true`, `CheckForPlayerCollision()` is called 17 times per `CheckGround()` and 17 times per `CheckCeiling()`. Each call logs a line if `debugLogging` is true. On any frame where both pass, this is 34 log entries, saturating the Unity console and impairing performance.

**Proposed Fix:** Move the per-hit log into `CheckForPlayerCollision` only when the player tag is actually matched (the warning is already gated on `hitObject.CompareTag("Player")`). Remove the general-purpose log from the non-player branch, or reduce it to a single summary log after all 17 raycasts.

---

#### Slope Normal Uses Closest-Distance Hit, Not Most-Central Hit
`Scripts/GroundChecker.cs:155–168`: `GroundNormal` is taken from the hit with the smallest raycast distance (i.e., the ground point closest to the ray origins). On a slope transition (e.g., the front edge of a step), the closest hit may be on the vertical face of the step rather than the flat top. This can briefly set `GroundSlopeAngle` to 90°, triggering the slope-too-steep check and momentarily setting `IsGrounded = false`.

**Proposed Fix:** Weight the normal by the center ray, or use the center ray's normal when it hits, only falling back to surrounding rays when the center ray misses.

---

## 9. CameraController

### Bugs

#### Rotation Applied Twice Per Frame
`Scripts/CameraController.cs:59–66` (Update) and `Scripts/CameraController.cs:103–104` (HandleLook):

In `Update()`:
```csharp
HandleLook(playerInput.LookInput);                                              // line 59
mainCamera.transform.localRotation = Quaternion.Euler(cameraPitch, ...);       // line 65
```
Inside `HandleLook()`, the method ends with:
```csharp
mainCamera.transform.localRotation = Quaternion.Euler(cameraPitch, ...);       // line 104
```
The rotation assignment runs twice each frame — once inside `HandleLook()` and once immediately after the call in `Update()`. Since both assignments use the same values, the result is visually identical, but it is redundant work and a maintenance hazard (the two call sites could diverge).

**Proposed Fix:** Remove the duplicate assignment from `Update()` (line 65). Let `HandleLook()` be the sole writer of the camera rotation.

---

#### `InitializeAngles()` May Read Gimbal-Wrapped Euler Angles
`Scripts/CameraController.cs:48–49`:
```csharp
cameraYaw   = mainCamera.transform.eulerAngles.y;
cameraPitch = mainCamera.transform.eulerAngles.x;
```
Unity stores rotation as quaternions internally and converts to Euler on demand. For a pitch between −90° and 0° (e.g., camera looking slightly downward), Unity returns `eulerAngles.x` in the range 270°–360° rather than −90°–0°. The subsequent clamp `Mathf.Clamp(cameraPitch, minAngle, maxAngle)` with `minAngle = −80` would immediately clamp 315° down to 80° — snapping the camera to look nearly straight up on game start.

**Proposed Fix:**
```csharp
cameraPitch = mainCamera.transform.localEulerAngles.x;
if (cameraPitch > 180f) cameraPitch -= 360f; // normalize to -180..180
```
**Side Effects:** If the camera prefab is configured with a non-zero starting pitch, it will now initialize correctly instead of snapping.

---

#### Camera Yaw Is View-Only — Player Body Does Not Rotate
By design (per PHYSICS-ROTATION-PLAN.md), Y rotation is frozen on the Rigidbody. All yaw happens only on the camera via `cameraYaw`. This means `transform.forward` on the player GameObject does not match the camera's facing direction. Any system using `transform.forward` for physics (projectile spawn, raycasts from player, etc.) will aim in the wrong direction.

**Assessment:** This is a documented and intentional trade-off (PHYSICS-ROTATION-PLAN.md Option 1). It is not a bug per se, but it is the most important architectural consequence any integrator needs to understand. The package needs a prominent note in its docs and on `PlayerController` itself: *"Use `CameraController.Forward` for facing direction, not `transform.forward`."*

**Proposed Fix:** Add a `public Vector3 FacingDirection` property to `CameraController` (or `PlayerController`) that returns the camera's horizontal forward. This gives integrators a stable API regardless of which rotation option is in use.

---

### Design Notes

- `PlatformYawOffset` is a `public` field (mutable by any external code without control). Should be a property with only `PlayerMovement` able to write it, or at minimum documented as internal-use.
- No input smoothing on look — raw mouse delta produces jittery motion at high DPI. A lerp or `SmoothDamp` on the input value before applying to yaw/pitch would improve feel.

---

## 10. PlayerAnimatorController

### Issues

#### `showParameterDebug = true` Default
Same issue as `PlayerController.showStateDebug`. Renders an `OnGUI` label in production builds.

**Proposed Fix:** Default to `false`.

---

#### Speed Parameter Drops to Zero Before Player Stops Moving
`Scripts/PlayerAnimatorController.cs:71`:
```csharp
animator.SetFloat("Speed", horizontalVelocity.magnitude * playerInput.MoveInput.magnitude);
```
When the player releases input, `MoveInput.magnitude` drops to 0 instantly, so `Speed` goes to 0 even though the player is still decelerating and has physical velocity. Animations keyed to `Speed` will cut to idle while the player visually slides to a stop.

**Proposed Fix:** Drive `Speed` from actual velocity magnitude alone:
```csharp
animator.SetFloat("Speed", horizontalVelocity.magnitude);
```
If the animator needs to know "is the player intentionally moving," use a separate bool `IsMovingIntentionally` driven by `MoveInput.magnitude > 0.1f`, rather than conflating it with speed.

---

#### `IsGrounded` Derived from State Machine, Not Ground Check
`Scripts/PlayerAnimatorController.cs:80`:
```csharp
bool currentIsGrounded = playerController.CurrentState != PlayerController.PlayerState.Jumping
                      && playerController.CurrentState != PlayerController.PlayerState.Falling;
```
This means `Crouching` and `Climbing` both yield `IsGrounded = true`. A crouching player jumping (before the state machine transitions from Crouching to Jumping) will have `IsGrounded = true` for one frame of the jump.

**Proposed Fix:** Use `GroundChecker.IsGrounded` directly:
```csharp
bool currentIsGrounded = playerController.GetGroundChecker().IsGrounded;
```
The debounce logic already handles the landing feel separately.

---

## 11. Configuration System

### Duplicated Configuration — Crouch Height

`PlayerCrouchConfig.CrouchHeight` (used for capsule sizing) and `PlayerMovementConfig.CrouchingHeight` (used for terrain ray distance calculations in `AdjustForTerrain`) are separate values that both represent the crouching capsule height. They can drift out of sync, causing terrain navigation to use the wrong height during a crouch.

**Proposed Fix:** Remove `CrouchingHeight` from `PlayerMovementConfig`. In `AdjustForTerrain()`, read the crouch height from `PlayerCrouch` directly, or pass it as a parameter.

---

## 12. Cross-Cutting Issues

### Wall Detection Is Fully Implemented but Never Used

`GroundChecker` detects walls, sets `IsTouchingWall` and `WallNormal`, but no system reads these properties. Wall-jump, wall-slide, and wall-run are absent from both the code and the planning documents.

**Assessment:** Either implement at least one wall-interaction mechanic (wall-jump is the most commonly expected in a physics controller), or remove the wall detection code to reduce runtime overhead. The 4-direction raycast in `CheckWall()` runs every frame even though its results are discarded.

---

### `PlayerState.Sliding` Exists but Is Never Set

The state machine enum contains `Sliding`. `PlayerAnimatorController` sets `IsSliding` on the Animator. The `UpdateStateMachine()` comment says "Skipping Phase 4." This means `IsSliding` is always `false`, and the animator parameter is dead. Any Animator state machine built against this package that includes a Sliding state will never be entered.

**Proposed Fix:** Either implement the sliding mechanic (trigger on fast crouch while moving above a speed threshold; apply forward impulse; block steering input) or remove `Sliding` from the enum and the animator parameter until it is implemented. Shipping a named-but-broken state machine value is confusing to integrators.

---

### Two Separate `[SerializeField] Camera mainCamera` References

`PlayerMovement` and `PlayerClimb` both have `[SerializeField] private Camera mainCamera` that must be assigned in the inspector independently. `CameraController` also has one. Three inspector slots for the same object.

**Proposed Fix:** Expose a `Camera MainCamera` property on `PlayerController` or `CameraController`. `PlayerMovement` and `PlayerClimb` read it from there in `Awake()`:
```csharp
mainCamera = GetComponent<CameraController>().MainCamera;
```
Single source of truth, one inspector slot.

---

### Assembly Definition Has No Root Namespace Set

`zacharysnewman.ppc.asmdef` has `"rootNamespace": ""`. All scripts manually declare `namespace ZacharysNewman.PPC`, which is consistent, but IDE tooling that auto-generates new scripts from the assembly definition will not apply the namespace. This leads to namespace-less scripts being created in the package by accident.

**Proposed Fix:** Set `"rootNamespace": "ZacharysNewman.PPC"` in the `.asmdef` file.

---

## 13. Missing Features by System

| System | Missing Feature | Priority |
|---|---|---|
| Movement | Air control multiplier | High — affects basic feel |
| Movement | Slide / momentum on slope > max angle | Medium — state exists, not wired |
| Jump | Variable jump height (cut on release) | High — standard platformer feel |
| Jump | Directional impulse on ladder exit | Medium |
| Crouch | Configurable hold vs. toggle mode | Low |
| Crouch | Crouch-walk blending (smooth enter/exit) tied to landing detection | Medium |
| Climb | Player snap to ladder face | Medium — prevents accidental exit |
| Climb | Non-vertical ladders | Low |
| Climb | Top-of-ladder auto-dismount | Low |
| Ground | Slope-slide physics when angle > MaxSlopeAngle | Medium |
| Ground | PhysicsMaterial friction integration | Low |
| Camera | Look input smoothing / mouse damping | Medium — affects feel |
| Camera | FOV change on sprint | Low |
| Camera | `FacingDirection` property for integrators | High — needed by any weapon/projectile system |
| General | Wall-jump (wall detection already exists) | Medium |
| General | Swimming / water volume detection | Low |

---

## 14. Bloat

### `DebugVisualizer` as a Hard `RequireComponent`
As noted in §2, the debug system ships with every build and cannot be removed. In most production contexts this is pure overhead. Convert to optional.

### `groundCheck` and `ceilingCheck` as Child `Transform` Objects
`GroundChecker` creates child GameObjects (`GroundCheck`, `CeilingCheck`) just to hold a `Vector3` position. A `Vector3` field updated each frame would serve the same purpose with no GameObject allocation, no scene hierarchy pollution, and no `transform.position` overhead.

```csharp
// Instead of:
groundCheck.localPosition = capsule.center;
Vector3 checkPosition = groundCheck.position;

// Use:
Vector3 groundCheckPosition = transform.position + capsule.center;
```

### Debug-Only `public` Fields on `PlayerMovement`
`public Vector3 DebugMovementForce` is a public mutable field used for visualization. It should be either a private field read by `DebugVisualizer` via a getter property, or an internal detail not exposed via the public API.

---

## 15. Summary Table

Ordered from most to least severe.

| Severity | Issue | File | Line(s) | Type | Status |
|---|---|---|---|---|---|
| **High** | `lastTargetY` stale after ladder dismount — player launches vertically | VerticalVelocityLayer.cs / PlayerClimb.cs | — | Bug | ✓ Fixed |
| **High** | Horizontal absorption fires on dismount — player launches laterally | PlayerMovement.cs / PlayerClimb.cs | 199, 268–270 | Bug | ✓ Fixed |
| **High** | Double-jump possible via coyote time after a normal jump | PlayerJump.cs | 68–70 | Bug | ✓ Fixed |
| **High** | Ground/ceiling ray origins at capsule center, not bottom/top edge | GroundChecker.cs | 67, 71 | Bug | ✓ Fixed |
| **High** | Step height uses hardcoded `- 1f` half-height, breaks during crouch | PlayerMovement.cs | 322 | Bug | ✓ Fixed |
| **High** | `inputSensitivity` applied to `MoveInput`, silently scales top speed | PlayerInput.cs | 50 | Bug | ✓ Fixed |
| **High** | Camera euler init wraps downward pitch to 270°+, snaps on start | CameraController.cs | 48–49 | Bug | ✓ Fixed |
| **High** | No `FacingDirection` property — integrators have no correct facing vector | CameraController.cs | — | Missing | ✓ Fixed |
| **High** | No air control reduction — full acceleration applies identically in the air | PlayerMovement.cs | — | Missing | Open |
| **High** | No variable jump height (cut on early release) | PlayerJump.cs | — | Missing | Open |
| **High** | `DebugVisualizer` is a hard `RequireComponent` — cannot be stripped from production | PlayerController.cs | 14 | Bloat | ✓ Fixed |
| **Medium** | `BaseVelocity.y` fed to `VerticalVelocityLayer` is one frame stale | VelocityAggregator.cs | 40–43 | Bug | Open |
| **Medium** | Speed animator param drops to 0 on input release before player stops moving | PlayerAnimatorController.cs | 71 | Bug | ✓ Fixed |
| **Medium** | Mid-air crouch center not corrected when player lands while still crouching | PlayerCrouch.cs | 86–107 | Bug | ✓ Fixed |
| **Medium** | Rotation applied twice per frame in `Update` + inside `HandleLook` | CameraController.cs | 65, 104 | Bug | ✓ Fixed |
| **Medium** | `IsGrounded` in animator derived from state machine, not `GroundChecker` directly | PlayerAnimatorController.cs | 80 | Bug | ✓ Fixed |
| **Medium** | Ladder camera-pitch inversion has zero dead zone — switches mode at 0.0001° | PlayerClimb.cs | 194 | Bug | ✓ Fixed |
| **Medium** | `playerToLadder` computed every physics step in `GetVelocityContribution()` but never used | PlayerClimb.cs | 156–158 | Bloat | ✓ Fixed |
| **Medium** | Ground debug logging fires 34+ `Debug.Log` entries per frame when enabled | GroundChecker.cs | 307–319 | Performance | Open |
| **Medium** | Two separate `mainCamera` inspector slots to assign same object (Movement + Climb) | PlayerMovement.cs / PlayerClimb.cs | 14, 20 | UX/Usability | Open |
| **Medium** | `CrouchHeight` duplicated in both `PlayerCrouchConfig` and `PlayerMovementConfig` — can drift | PlayerCrouchConfig / PlayerMovementConfig | — | Config | Open |
| **Medium** | `IsTouchingWall` detected every frame but consumed by no system | GroundChecker.cs | 188–222 | Bloat | Open |
| **Medium** | `PlayerState.Sliding` and `IsSliding` animator bool are permanently false (unimplemented) | PlayerController.cs / PlayerAnimatorController.cs | — | Bloat | Open |
| **Medium** | `showStateDebug` and `showParameterDebug` default to `true` — OnGUI renders in production | PlayerController.cs / PlayerAnimatorController.cs | — | Bloat | ✓ Fixed |
| **Medium** | No player snapping to ladder face — drifting off trigger causes abrupt exit | PlayerClimb.cs | — | Missing | Open |
| **Low** | Nested `if (debugLogging)` is always-true redundancy | PlayerJump.cs | 72–74, 95–98 | Bug | ✓ Fixed |
| **Low** | `GetComponent<PlayerInput>()` called on debug property getters every access | PlayerInput.cs | 27–28 | Performance | ✓ Fixed |
| **Low** | `GetComponent<Rigidbody>()` called every physics step in `TrackPlatformMovement()` | PlayerMovement.cs | — | Performance | ✓ Fixed |
| **Low** | `groundCheck`/`ceilingCheck` are child GameObjects — a `Vector3` field suffices | GroundChecker.cs | 44–53 | Bloat | Open |
| **Low** | `DebugMovementForce` is a public mutable field, not a property | PlayerMovement.cs | — | API | ~ Partial |
| **Low** | `PlatformYawOffset` is a public mutable field, not a property | CameraController.cs | 25 | API | Open |
| **Low** | Redundant component getter methods on `PlayerController` (`GetPlayerInput()`, etc.) | PlayerController.cs | — | API | Open |
| **Low** | Wall check distance hardcoded at `0.16f`, not in config | GroundChecker.cs | 192 | Config | Open |
| **Low** | Wall check uses global axis directions — diagonal walls not detected | GroundChecker.cs | 191 | Config | Open |
| **Low** | Mouse lock toggle logic in `PlayerInput` instead of `CameraController` | PlayerInput.cs | 58–74 | Architecture | Open |
| **Low** | `AddForce(delta/dt, ForceMode.Acceleration)` is equivalent to direct velocity set — unclear intent | VelocityAggregator.cs | 52 | Design | Open |
| **Low** | `rb.useGravity = false` in `Awake()` without warning or opt-out | VerticalVelocityLayer.cs | 28 | Design | Open |
| **Low** | Layer list in `VelocityAggregator` cached in `Start()` — not dynamic | VelocityAggregator.cs | 23 | Design | Open |
| **Low** | Launch pad detection can be suppressed by `SetGrounded(true)` before player lifts off | VerticalVelocityLayer.cs | 74–90 | Design | Open |
| **Low** | `.asmdef` has no `rootNamespace` set — IDE auto-generates namespace-less files | .asmdef | — | Tooling | ✓ Fixed |

---

## 16. Velocity Layer System (main branch)

This section evaluates the `IVelocityLayer` / `VelocityAggregator` / `VerticalVelocityLayer` system introduced alongside a refactor of `PlayerMovement` and `PlayerClimb`.

### What Was Fixed

The following issues from §2–§11 were resolved in this update:

| Fixed Issue | How |
|---|---|
| `ApplyConfiguration()` dead code — master config does nothing | Removed entirely, along with `PlayerControllerConfig` |
| Duplicate `platformRotationAccum = Quaternion.identity` | Removed |
| `rb.useGravity` fought between `PlayerMovement` and `PlayerClimb` | `VerticalVelocityLayer` takes sole ownership of gravity |
| `GetComponent<Rigidbody>()` called in `StartCrouch()` | Replaced by `verticalLayer.AddVerticalImpulse()` |
| Jump apex formula ignored Rigidbody mass | Now uses `force / rb.mass` to compute velocity correctly |
| Platform tracking used `Time.deltaTime` (wrong for physics) | Moved into `GetVelocityContribution()`, uses `Time.fixedDeltaTime` |
| `TargetVelocity` calculated twice with identical expressions | Consolidated |
| `rb.useGravity = !isGrounded` inside `HandleMovement()` | Removed; `VerticalVelocityLayer.Awake()` sets `rb.useGravity = false` permanently |
| `playerClimb` reference in `PlayerMovement` (old guard clause) | Removed; exclusivity handled by aggregator |

---

### Architecture of the New System

`IVelocityLayer` is a simple interface with three members:
- `Vector3 GetVelocityContribution(float dt)` — return the world-space velocity this layer targets
- `bool IsActive` — whether to include this layer
- `bool IsExclusive` — if any active layer is exclusive, all non-exclusive layers are skipped

`VelocityAggregator` collects all `IVelocityLayer` components, sums their contributions (respecting exclusivity), then applies the result:
```csharp
rb.AddForce((targetVelocity - rb.linearVelocity) / dt, ForceMode.Acceleration);
```
This is mathematically equivalent to `rb.linearVelocity = targetVelocity` — a direct velocity set, not force-based physics. The smoothing lives inside each layer's contribution logic.

Current layers:
- `PlayerMovement` — horizontal locomotion, platform frame, external force absorption (`IsExclusive = false`)
- `VerticalVelocityLayer` — gravity integration, jump, launch detection (`IsExclusive = false`)
- `PlayerClimb` — full 3D climb velocity (`IsExclusive = true`)

---

### New Issues

#### Bug — Stale `lastTargetY` After Ladder Dismount (High)

During climbing, `PlayerClimb.IsExclusive = true`. The aggregator skips all non-exclusive layers, so `VerticalVelocityLayer.GetVelocityContribution()` is never called. `lastTargetY` (the Y value the layer drove `rb.linearVelocity.y` toward last step) is therefore frozen at its pre-climb value for the entire duration of the climb.

`ExitClimb()` zeros `accumulatedY` via `verticalLayer.AddVerticalImpulse(-verticalLayer.AccumulatedY)`, but does **not** reset `lastTargetY`.

On the first post-dismount physics step:
```csharp
float externalDelta = rb.linearVelocity.y - lastTargetY;
// rb.linearVelocity.y  = vertical component of last climb velocity (e.g. 3.0 if climbing up)
// lastTargetY          = whatever it was before the climb started (e.g. -2.0 from falling)
// externalDelta        = 3.0 - (-2.0) = 5.0 → absorbed into accumulatedY
```
The player launches upward by the sum of their climb-exit velocity and their pre-climb fall velocity.

**Fix:** Add a `ResetAbsorptionBaseline()` method to `VerticalVelocityLayer` that sets `lastTargetY = rb.linearVelocity.y` and sets `skipExternalAbsorption = true`. Call it from `ExitClimb()` after zeroing `accumulatedY`. This gives the layer a fresh reference point without treating the velocity change as external.

**Side effect:** One step of absorption is skipped on dismount, which is the correct behaviour — same as after a jump.

---

#### Bug — Horizontal External Absorption Fires Spuriously on Ladder Dismount (High)

`ResetHorizontalVelocity()` is called in `EnterClimb()`, setting `lastHorizontalContribution = Vector3.zero`. During climbing, `PlayerMovement.GetVelocityContribution()` is never called (exclusive skip), so `lastHorizontalContribution` stays at zero for the entire climb.

On the first post-dismount physics step:
```csharp
Vector3 actualHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
// rb.linearVelocity.xz = last climb horizontal velocity (e.g. (1.5, 0, 0))
// lastHorizontalContribution = (0, 0, 0)
Vector3 externalDelta = actualHorizontal - lastHorizontalContribution; // = (1.5, 0, 0)
externalHorizontalVelocity += externalDelta; // player launches sideways
```
Any lateral movement on the ladder is absorbed as a phantom external impulse on dismount.

**Fix:** When `ExitClimb()` runs, also reset the horizontal absorption baseline in `PlayerMovement`. This could be a second method `ResetAbsorptionBaseline()` on `PlayerMovement`, or `ResetHorizontalVelocity()` could be extended to seed `lastHorizontalContribution` from the current `rb.linearVelocity.xz` rather than zeroing it:
```csharp
lastHorizontalContribution = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
```
**Side effect:** Residual climb lateral velocity is preserved as external velocity when dismounting, which will then decay naturally via `AirExternalDrag`. This is likely the desired feel (brief carry-through), but can be suppressed by also zeroing `externalHorizontalVelocity` in the reset.

---

#### Bug — `BaseVelocity.y` Fed to `VerticalVelocityLayer` Is One Frame Stale (Medium)

In `VelocityAggregator.Apply()`:
```csharp
// 1. Feed platform Y to vertical layer
verticalLayer.SetPlatformY(playerMovement.BaseVelocity.y); // ← BaseVelocity from LAST step

// 2. Then call each layer
foreach (var layer in layers)
    targetVelocity += layer.GetVelocityContribution(dt);
    // PlayerMovement.GetVelocityContribution() calls TrackPlatformMovement()
    // which updates BaseVelocity for the CURRENT step — too late
```
`SetPlatformY` always receives a one-step-old platform Y velocity. For smooth platforms this lag is negligible. For platforms that start, stop, or change direction abruptly (step changes), `VerticalVelocityLayer` uses the wrong base for one step, which can cause a brief pop or missed ground-stick.

**Fix:** Move `TrackPlatformMovement()` out of `GetVelocityContribution()` and into a separate `PrepareFrame(float dt)` method. Call `playerMovement.PrepareFrame(dt)` before `verticalLayer.SetPlatformY(playerMovement.BaseVelocity.y)` in `Apply()`. This ensures `BaseVelocity` is always current before being forwarded to other layers.

**Side effect:** `GetVelocityContribution()` would be a pure contribution query rather than mixing platform tracking. Cleaner separation of concerns.

---

#### Design — `VelocityAggregator.Apply()` Is Equivalent to Direct Velocity Assignment (Low)

```csharp
rb.AddForce((targetVelocity - rb.linearVelocity) / dt, ForceMode.Acceleration);
```
`ForceMode.Acceleration` applies `force * dt` to velocity during integration, so velocity change = `(delta / dt) * dt = delta`. The rigidbody velocity changes by exactly `delta` every step — identical to `rb.linearVelocity = targetVelocity`. Using `AddForce` implies physics-based application (mass-independent, integrated by the solver) but this is actually a direct velocity override. The distinction matters if Unity's solver reorders force application or if other forces are applied in the same step.

Using `ForceMode.VelocityChange` is semantically cleaner (it explicitly means "change velocity by this amount, ignoring mass") and communicates the intent better. Or using `rb.linearVelocity = targetVelocity` directly is even clearer, though it bypasses the solver pipeline.

**Consideration:** If `delta` is large (e.g., on the first frame, or after a teleport), this applies an unbounded instantaneous velocity change. The smoothing inside each layer mitigates this during normal play, but there is no cap at the aggregator level. A `Vector3.ClampMagnitude(delta, maxDeltaPerStep)` guard would add safety.

---

#### Design — `VerticalVelocityLayer.rb.useGravity = false` in Awake Without Warning (Low)

`VerticalVelocityLayer.Awake()` unconditionally sets `rb.useGravity = false`. If this component is ever added to a Rigidbody that should use Unity's built-in gravity (e.g., a prop or enemy using this layer separately), gravity is silently disabled. A `Debug.LogWarning` or an opt-in flag (`[SerializeField] bool disableUnityGravity = true`) would make the side effect visible.

---

#### Design — `VelocityAggregator.layers` Cached in `Start()`, Not Dynamic (Low)

`layers = GetComponents<IVelocityLayer>()` runs once in `Start()`. If any `IVelocityLayer` component is added or removed at runtime, the aggregator's layer list is stale. This is not a concern for the current prefab-based workflow, but worth documenting as a limitation for users who dynamically add systems.

---

#### Design — Launch Pad Detection Has a Timing Race While Grounded (Low)

In `VerticalVelocityLayer.GetVelocityContribution()`, when `isGrounded`:
```csharp
float externalDelta = rb.linearVelocity.y - lastTargetY;
if (externalDelta > 0.1f)
{
    accumulatedY = rb.linearVelocity.y;
    isGrounded = false;
    // ...
}
```
This sets the internal `isGrounded = false` when a launch is detected. The suppression check in `SetGrounded()`:
```csharp
if (grounded && accumulatedY > platformY + 0.1f) return;
```
prevents the next `Update()` call from resetting `isGrounded = true` — but only while `accumulatedY > platformY + 0.1f`. For a weak launch pad that imparts less than 0.1 m/s of upward velocity, the suppression threshold may not hold, and `SetGrounded(true)` cancels the launch on the next Update before the player physically leaves the ground. This is a tuning edge case but worth documenting alongside the `gravityScale` and `0.1f` thresholds.

All issues from this section are included in the master §15 summary table.

---

## 17. Applied Fixes — Review Pass 2

Two commits applied in this pass: one targeting the issues found in the original §2–§11 review, and one addressing the new issues surfaced in §16. All fixes are on branch `claude/review-evaluation-issues-JdcO9`.

### What Was Fixed

| Issue | Fix Applied | Rationale |
|---|---|---|
| `RequireComponent(typeof(DebugVisualizer))` production dependency | Removed attribute; `ValidateRequiredComponents()` no longer errors on null `debugVisualizer` | `DebugVisualizer` was already guarded by null checks everywhere it is used. Making it optional is a one-line change with zero runtime risk and removes an otherwise mandatory debug dependency from production builds. |
| `showStateDebug` / `showParameterDebug` default `true` | Defaulted both to `false` | OnGUI renders every frame in builds and in Play Mode when not needed. The fields are still exposed in the inspector; this only changes the out-of-box default. |
| `inputSensitivity` applied to `MoveInput` | Removed `* inputSensitivity` from `MoveInput` assignment | Sensitivity is a look concept. Applying it to WASD silently reduces maximum speed by a fixed ratio, which contradicts every downstream assumption that `MoveInput.magnitude == 1` at full input. Look sensitivity is already a separate field in `CameraControllerConfig`; this field's scope is now correctly limited to look. |
| `GetComponent` called on `CurrentControlScheme` / `PlayerIndex` debug properties | Changed to use already-cached `playerInputComponent` | `GetComponent` is an O(n) scan of the component list. Debug properties are polled every frame by the state debug display. Using the cached reference is a trivially safe fix. |
| Double-jump via coyote time after a real jump | Added `&& !isJumping` guard to coyote time activation | Coyote time is intended for walk-off edges, not as a second-jump opportunity. The flag `isJumping` is already maintained correctly; this is a single-condition addition that exactly encodes the intent. |
| Nested redundant `if (debugLogging)` in `PlayerJump` | Removed outer wrapper, kept inner guard | The inner check is always true when the outer passes — dead code. No behaviour change; reduces visual noise. |
| Camera euler init gimbal wrap (downward pitch snaps on start) | Normalize `eulerAngles.x` to −180..180 with `rawPitch > 180f ? rawPitch - 360f : rawPitch` | Unity's `eulerAngles.x` returns 270–360 for downward pitches. The subsequent `Mathf.Clamp(cameraPitch, minAngle, maxAngle)` would immediately snap 315° to `maxAngle` (≈80°), forcing the camera nearly straight up. The fix is a two-line normalization — it is the canonical solution and has no side effects beyond reading the correct angle. |
| Rotation applied twice per frame in `CameraController` | Removed the duplicate `localRotation` assignment from `Update()`; `HandleLook` remains the sole writer | Both assignments used identical values so the output was the same, but two write sites for the same transform property is a maintenance hazard — they could silently diverge. Removing the `Update()` copy is the minimal fix. |
| No `FacingDirection` property on `CameraController` | Added `public Vector3 FacingDirection` computed from `cameraYaw + PlatformYawOffset` | With Y-rotation frozen on the Rigidbody (per PHYSICS-ROTATION-PLAN.md), `transform.forward` does not reflect where the player faces. Any integrator system (projectiles, raycasts, AI targeting) would silently aim in the wrong direction. A property backed by the authoritative camera yaw field gives a stable, correct API without coupling callers to camera internals. |
| Step height hardcodes capsule half-height (`- 1f`) | Changed to `transform.position.y + capsule.center.y - capsule.height / 2f` | The hardcoded `1f` is the half-height of a default capsule. During a crouch (capsule height ≈ 1, center adjusted) `playerBottomY` is wrong, causing steps to fire when they shouldn't or not fire when they should. The formula is a direct geometric expression of the capsule's bottom surface — it self-corrects for any capsule size at all times. |
| `GetComponent<Rigidbody>()` every physics step in `TrackPlatformMovement()` | Added `private Rigidbody currentPlatformRb`; assigned alongside `currentPlatform` in `UpdateGrounded()`, cleared on exit | `GetComponent` is called every `FixedUpdate` while the player is on a non-kinematic platform. The Rigidbody reference is stable for the duration of the contact and can trivially be cached. This removes repeated allocations and component scans from the hot physics path. |
| Stale `lastTargetY` after ladder dismount — vertical launch | Added `VerticalVelocityLayer.ResetAbsorptionBaseline()`; called in `ExitClimb()` after zeroing `accumulatedY` | During climbing the vertical layer is skipped (exclusive), so `lastTargetY` freezes at the pre-climb value. On the first post-dismount step, the delta between `rb.linearVelocity.y` and the stale `lastTargetY` is absorbed as an external impulse, launching the player. `ResetAbsorptionBaseline()` sets `lastTargetY = accumulatedY` (0 after the zero-out) and sets `skipExternalAbsorption = true` (matching the same guard already used after `ApplyJumpImpulse`), giving the layer a clean reference point with no behaviour change on subsequent steps. |
| Horizontal absorption fires on ladder dismount — lateral launch | Added `PlayerMovement.SeedHorizontalBaseline()`; called in `ExitClimb()` | `EnterClimb()` calls `ResetHorizontalVelocity()`, setting `lastHorizontalContribution = 0`. The horizontal layer is then skipped for the entire climb. On dismount, `actualHorizontal - 0` is a non-zero delta absorbed as an external impulse. `SeedHorizontalBaseline()` sets `lastHorizontalContribution = rb.linearVelocity.xz` so the delta is zero on the first post-dismount step. `currentHorizontalVelocity` is zeroed (clean slate for movement control) while `externalHorizontalVelocity` is also zeroed (any residual climb lateral velocity decays via air drag rather than being re-injected as an impulse). |
| Camera pitch dead zone on ladder (zero-width, erratic near horizontal) | Changed threshold from `0.0f` to `±0.3f` | At exactly horizontal (cameraForward.y ≈ 0), the inversion mode switches with any tiny mouse movement, causing up/down input to flip randomly. A ±0.3 dead zone (≈17° band around horizontal) gives a stable neutral zone. The exact value is tunable; 0.3 is a reasonable default that matches how far the camera naturally pitches when looking at a mid-height ladder. |
| `playerToLadder` computed every physics step but unused in `GetVelocityContribution()` | Removed the three dead-code lines | The variable is computed via a vector subtraction and normalize (non-trivial operations) every `FixedUpdate` while climbing, but is never read within that method. `VisualizeLadder()` computes it independently. |
| Ground and ceiling ray origins at capsule center instead of bottom/top | Changed `groundCheck.localPosition` and `ceilingCheck.localPosition` to `capsule.center ∓ Vector3.up * (capsule.height / 2f - capsule.radius)` | Originating rays from the center requires `GroundCheckDistance ≈ height/2` to reach the ground — a value that is capsule-size-dependent, breaks during crouch (center shifts), and is not self-documenting. Originating from the bottom of the capsule sphere means the distance only needs to span the contact gap (~0.15 units), is geometrically correct for any capsule size, and self-adjusts during crouch as the capsule shrinks. Default config distances updated from 1.05/1.1 to 0.15/0.1. **Breaking change for existing config assets.** |
| Speed animator parameter drops to 0 before player stops moving | Changed `horizontalRelative.magnitude * playerInput.MoveInput.magnitude` to `horizontalRelative.magnitude` | `MoveInput` drops to zero instantly on key release; multiplying it into `Speed` causes an immediate idle transition while the player is visually still decelerating. Velocity magnitude naturally falls to zero as movement decelerates — it is the physically correct signal. Input intent is already available separately via `IsCrouching`, `IsRunning`, etc. |
| `IsGrounded` in animator derived from state machine states | Changed to `groundChecker.IsGrounded` via `playerController.GetGroundChecker()` | The state machine excludes `Jumping` and `Falling` but treats `Crouching` and `Climbing` as grounded. A crouching player initiating a jump is in `Crouching` state for one frame before the state machine transitions, so `IsGrounded = true` fires incorrectly during the jump. `GroundChecker.IsGrounded` is the authoritative physics-derived value and is already used by all other systems. |
| Mid-air crouch center not corrected on landing | In `PlayerCrouch.Update()`, detect `!wasGrounded && isGrounded && isCrouching` and reapply the grounded center (`originalCenter + Vector3.down * delta / 2f`) | The intentional design (mid-air shifts capsule up to "lift legs"; grounded shifts capsule down) is preserved — the fix only triggers on the airborne→grounded transition. Without it, the upward-shifted capsule persists after landing, floating the collision volume above the floor until the player releases and re-crouches. The camera height is also recalculated on the transition. |
| `.asmdef` has no `rootNamespace` | Set `"rootNamespace": "ZacharysNewman.PPC"` | Without this, any script created in the package via IDE tooling ("New Script" inside the assembly's folder) is generated without a namespace declaration, silently polluting the global namespace. One-field change, no runtime effect. |
| `DebugMovementForce` public mutable field | Added `[HideInInspector]` attribute | Full fix (make private with a getter property) would require changing `DebugVisualizer` callers. The attribute prevents it appearing in the inspector and being accidentally edited, which was the immediate UX concern. The API surface issue remains — tracked as Open in §15. |

### What Remains Open

The following items from §15 were not addressed in this pass, either because they are architectural (require broader design decisions), missing features (scope beyond bug fixing), or low-impact enough to defer:

- **Air control reduction** — requires new config field and design decision on default multiplier
- **Variable jump height** — requires input-phase tracking (pressed vs. held)
- **`BaseVelocity.y` one frame stale** — requires refactoring `TrackPlatformMovement` out of `GetVelocityContribution`
- **Ground debug logging spam** — requires restructuring the per-raycast log into a post-loop summary
- **Two `mainCamera` inspector slots** — requires `CameraController.MainCamera` property and `Awake()` auto-assign in both consumers
- **`CrouchHeight` config duplication** — requires removing `CrouchingHeight` from `PlayerMovementConfig` and reading from `PlayerCrouch`
- **`IsTouchingWall` unused / wall-jump** — missing feature
- **`PlayerState.Sliding` unimplemented** — missing feature
- **Player snapping to ladder face** — missing feature
- **`groundCheck`/`ceilingCheck` as child GameObjects** — bloat reduction; safe to do but no correctness impact now that positions are computed correctly
- **`PlatformYawOffset` public mutable field** — API cleanup
- **Redundant component getters** — API surface decision
- **Wall check config/direction issues** — dependent on whether wall-jump is ever implemented
- **Mouse lock in `PlayerInput`** — architecture cleanup
- **`VelocityAggregator` design notes** — low-impact design observations
