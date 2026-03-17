# Physics Player Controller — Full Evaluation

> Evaluated against source at v1.0.1. All line references are to files under `Runtime/`.

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

#### Dead Code — `ApplyConfiguration()` Does Nothing
`PlayerController.ApplyConfiguration()` (`Scripts/PlayerController.cs:78–114`) is a completely empty stub. Every `if` block inside it contains only a comment reading "placeholder for future implementation." The `PlayerControllerConfig` ScriptableObject is read by `PlayerController` but its sub-configs are never forwarded to the child components. Each child component reads its own `[SerializeField] config` directly.

**Effect:** Runtime config swapping via `PlayerControllerConfig` does not work. Setting a different `PlayerControllerConfig` at runtime has zero effect. The master config hierarchy is dead infrastructure.

**Proposed Fix:** Either implement the forwarding — each component gets a `SetConfig(XxxConfig)` public method, called from `ApplyConfiguration()` — or remove `PlayerControllerConfig` entirely and document that each component manages its own config. The former is the correct path since runtime swapping is a stated design goal.

**Side Effects of Fix:** Each component would need a public `SetConfig` method. Calling it mid-game requires that all mutable state derived from the old config (speed, heights, etc.) be re-initialized. Consider whether a config swap mid-game is actually a safe operation (e.g., if walk speed changes but the player is already at the old walk speed, the transition may feel abrupt).

---

#### `RequireComponent(typeof(DebugVisualizer))` is a Production Dependency
`DebugVisualizer` is a hard dependency of `PlayerController`. Removing it from the prefab removes `PlayerController`. This means debug visualization code ships in every build regardless of whether it is used.

**Proposed Fix:** Remove `RequireComponent(typeof(DebugVisualizer))`. Make `DebugVisualizer` optional — all the `if (debugVisualizer != null)` null checks already exist elsewhere in the codebase, so the pattern is familiar. Each system that calls into `DebugVisualizer` already stores a private reference; these simply go null if the component is absent.

**Side Effects:** The debug reference in `PlayerController.debugVisualizer` and the getter `GetDebugVisualizer()` become optional. `ValidateRequiredComponents()` would need to stop treating it as an error.

---

#### `showStateDebug = true` Default — OnGUI in Production
`PlayerController.showStateDebug` defaults to `true`, causing an `OnGUI` label to render every frame. Same issue in `PlayerAnimatorController.showParameterDebug = true`.

**Proposed Fix:** Default both to `false`. These are developer conveniences, not end-user features.

---

#### `freezeYRotation` Not Connected to Config
`PlayerController` has a `[SerializeField] bool freezeYRotation` field and `CameraControllerConfig` also has a `FreezeYRotation` property. They are independent — changing one does not change the other.

**Proposed Fix:** In `ApplyConfiguration()`, set `freezeYRotation = config.CameraConfig.FreezeYRotation` when the config is applied. (This is moot until `ApplyConfiguration()` is implemented, but worth noting for when it is.)

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

#### Duplicate `platformRotationAccum = Quaternion.identity` Assignment
`Scripts/PlayerMovement.cs:158–160`:
```csharp
platformDeltaRotation = Quaternion.identity;
platformRotationAccum = Quaternion.identity;
if (cameraController != null) cameraController.PlatformYawOffset = 0f;
platformRotationAccum = Quaternion.identity;  // ← duplicate, wrong indentation
```
The last line is a copy-paste artifact sitting outside the `if (currentPlatform != groundObject)` block's proper scope but still inside the outer `if (grounded && groundObject != null)` block. It harmlessly overwrites the same value but is misleading.

**Proposed Fix:** Remove the duplicate line.

---

#### No Air Control Reduction
Full acceleration and deceleration rates apply identically in the air and on the ground. The player can reverse direction instantly in mid-air. Most physics controllers reduce `accelRate` while airborne by a configurable multiplier.

**Proposed Fix:** Add `[SerializeField] float airControlMultiplier = 0.3f` to `PlayerMovementConfig`. Apply it in `HandleMovement()`:
```csharp
if (!isGrounded) accelRate *= config.AirControlMultiplier;
```
**Side Effects:** Jumps feel more committed. Players can no longer steer precisely mid-jump, which may be desired or not depending on game genre. Make the multiplier configurable and default it to something close to 1 if you want minimal change.

---

#### Acceleration Formula Is Circular and Confusing
```csharp
Vector3 smoothedVelocityChange = Vector3.MoveTowards(Vector3.zero, desiredVelocityChange, accelRate * Time.fixedDeltaTime);
Vector3 totalAcceleration = smoothedVelocityChange / Time.fixedDeltaTime;
rb.AddForce(totalAcceleration, ForceMode.Acceleration);
```
Multiplying by `Time.fixedDeltaTime` then immediately dividing by it cancels out. The result is identical to:
```csharp
Vector3 smoothedVelocityChange = Vector3.ClampMagnitude(desiredVelocityChange, accelRate);
rb.AddForce(smoothedVelocityChange, ForceMode.VelocityChange);
```
`MaxVelocityChange` then also clamps `desiredVelocityChange` before this step, creating two separate cap mechanisms on the same quantity. The intent of each cap is not documented.

**Proposed Fix:** Consolidate into a single `MaxAcceleration` field. Remove `MaxVelocityChange` or clearly document that it caps the instantaneous velocity error (useful for teleport recovery) while `Acceleration` caps the rate of change.

---

#### Camera Reference Silently Disables Movement
`HandleMovement()` returns early if `mainCamera == null`, but `Start()` calls `enabled = false` if it's null. So movement is both disabled *and* the early return is unreachable. The guard in `HandleMovement()` is dead code after `Start()` runs.

**Proposed Fix:** Remove the `mainCamera == null` check from `HandleMovement()` since `Start()` already disables the component if it's missing.

---

### Design Notes

- `TargetVelocity` is set to `playerTargetVelocity + platformTargetVelocity` (line 226) but then recalculated identically as `totalTargetVelocity` on line 229. The variable `TargetVelocity` is a public property for debug/external use, but it should be set *after* the airborne Y-velocity preservation step so it reflects the actual target, not an intermediate value.

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

#### Jump Apex Formula Assumes Rigidbody Mass = 1
`Scripts/PlayerJump.cs:127`:
```csharp
jumpApexHeight = transform.position.y + (force * force) / (2 * Physics.gravity.magnitude);
```
`ForceMode.Impulse` applies `force / mass` as a velocity change. The kinematic formula for apex height is `v² / (2g)` where `v` is the initial velocity. If Rigidbody mass ≠ 1, the actual initial velocity is `force / mass`, not `force`, so the displayed apex is wrong.

**Proposed Fix:**
```csharp
float initialVelocity = force / rb.mass;
jumpApexHeight = transform.position.y + (initialVelocity * initialVelocity) / (2 * Physics.gravity.magnitude);
```
**Side Effects:** The debug apex line moves to the correct position. If anyone has been tuning jump force by watching the apex line, the line now shows the truth.

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

#### `GetComponent<Rigidbody>()` Called at Runtime in `StartCrouch()`
`Scripts/PlayerCrouch.cs:101`:
```csharp
Rigidbody rb = GetComponent<Rigidbody>();
```
This is called every time `StartCrouch()` fires, which happens on every crouch press. `GetComponent` is not free at runtime, especially if called mid-jump.

**Proposed Fix:** Cache it in `Awake()`:
```csharp
private Rigidbody rb;
// in Awake:
rb = GetComponent<Rigidbody>();
```

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

#### Gravity Management Split Between Two Components
`PlayerClimb.EnterClimb()` sets `rb.useGravity = false`. `PlayerMovement.HandleMovement()` sets `rb.useGravity = !isGrounded` every physics frame. The climb guard (`if (playerClimb.IsClimbing) return`) in `HandleMovement` prevents the conflict — but only as long as that guard exists and is not bypassed. Gravity ownership is implicit and fragile.

**Proposed Fix:** Centralize gravity control. A single `GravityManager` component (or a method on `PlayerController`) that all systems call, with priority rules: climbing disables gravity > airborne enables gravity > grounded disables gravity. Alternatively, document the dependency clearly so the guard is not accidentally removed.

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

### Critical Issue — Master Config Does Nothing

`PlayerControllerConfig` is designed as a master container to allow runtime config swapping. `PlayerController.ApplyConfiguration()` is called in `Start()` but contains only comments. None of the sub-configs in `PlayerControllerConfig` are ever forwarded to the components that need them. Each component reads its own inspector-assigned config.

**Effect:**
- Setting `PlayerControllerConfig` on `PlayerController` has no effect on any child component.
- Runtime config swapping (e.g., switching between "normal" and "underwater" movement profiles) does not work.
- There are two separate config assignment UIs for the same logical settings: once on `PlayerController` and once on each individual component.

**Proposed Fix — Option A (Implement Forwarding):**
Add a `SetConfig(XxxConfig)` public method to each component. In `ApplyConfiguration()`:
```csharp
playerMovement.SetConfig(config.MovementConfig);
playerJump.SetConfig(config.JumpConfig);
cameraController.SetConfig(config.CameraConfig);
// etc.
```
Each `SetConfig` replaces the internal config reference and reinitializes any derived state.

**Proposed Fix — Option B (Remove Master Config):**
Delete `PlayerControllerConfig.cs` and the `config` field on `PlayerController`. Document that each component manages its own config. Simpler, but loses the runtime swapping capability.

**Side Effects of Option A:** A config swap mid-game must handle all live state cleanly. For example, if the jump force changes mid-jump, the in-flight trajectory does not change (the force was already applied). A config swap is effectively "take effect on next action," which should be documented.

---

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
| Config | Master config forwarding (ApplyConfiguration) | Critical — current state is broken |
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

### `TargetVelocity` Calculated Twice
`PlayerMovement.HandleMovement()` calculates `TargetVelocity = playerTargetVelocity + platformTargetVelocity` (line 226) and then immediately recalculates `totalTargetVelocity = playerTargetVelocity + platformTargetVelocity` (line 229) as a separate local variable. One of the two is redundant.

### Debug-Only `public` Fields on `PlayerMovement`
`public Vector3 DebugMovementForce` is a public mutable field used for visualization. It should be either a private field read by `DebugVisualizer` via a getter property, or an internal detail not exposed via the public API.

### `PlayerControllerConfig` With No Implementation
As described in §11, this entire class and its sub-references do nothing at runtime. It is five fields and five properties plus a method — all inert. Either implement it or delete it.

---

## 15. Summary Table

Ordered from most to least severe.

| Severity | Issue | File | Line(s) | Type |
|---|---|---|---|---|
| **Critical** | `ApplyConfiguration()` is empty — master config does nothing | PlayerController.cs | 78–114 | Bug / Missing |
| **High** | Double-jump possible via coyote time after a normal jump | PlayerJump.cs | 68–70 | Bug |
| **High** | Ground/ceiling ray origins at capsule center, not bottom/top edge | GroundChecker.cs | 67, 71 | Bug |
| **High** | Step height uses hardcoded `- 1f` half-height, breaks during crouch | PlayerMovement.cs | 331 | Bug |
| **High** | `inputSensitivity` applied to `MoveInput`, silently scales top speed | PlayerInput.cs | 50 | Bug |
| **High** | Camera euler init wraps downward pitch to 270°+, snaps on start | CameraController.cs | 48–49 | Bug |
| **High** | No `FacingDirection` property — integrators have no correct facing vector | CameraController.cs | — | Missing |
| **High** | No air control reduction — full acceleration applies identically in the air | PlayerMovement.cs | — | Missing |
| **High** | No variable jump height (cut on early release) | PlayerJump.cs | — | Missing |
| **High** | `DebugVisualizer` is a hard `RequireComponent` — cannot be stripped from production | PlayerController.cs | 14 | Bloat |
| **Medium** | Speed animator param drops to 0 on input release before player stops moving | PlayerAnimatorController.cs | 71 | Bug |
| **Medium** | Mid-air crouch center not corrected when player lands while still crouching | PlayerCrouch.cs | 86–107 | Bug |
| **Medium** | Rotation applied twice per frame in `Update` + inside `HandleLook` | CameraController.cs | 65, 104 | Bug |
| **Medium** | `IsGrounded` in animator derived from state machine, not `GroundChecker` directly | PlayerAnimatorController.cs | 80 | Bug |
| **Medium** | Ladder camera-pitch inversion has zero dead zone — switches mode at 0.0001° | PlayerClimb.cs | 194 | Bug |
| **Medium** | `playerToLadder` computed every frame in `HandleClimbMovement` but never used | PlayerClimb.cs | 180–182 | Bug |
| **Medium** | Ground debug logging fires 34+ `Debug.Log` entries per frame when enabled | GroundChecker.cs | 307–319 | Performance |
| **Medium** | Two separate `mainCamera` inspector slots to assign same object (Movement + Climb) | PlayerMovement.cs / PlayerClimb.cs | 14, 20 | UX/Usability |
| **Medium** | `CrouchHeight` duplicated in both `PlayerCrouchConfig` and `PlayerMovementConfig` — can drift | PlayerCrouchConfig / PlayerMovementConfig | — | Config |
| **Medium** | `IsTouchingWall` detected every frame but consumed by no system | GroundChecker.cs | 188–222 | Bloat |
| **Medium** | `PlayerState.Sliding` and `IsSliding` animator bool are permanently false (unimplemented) | PlayerController.cs / PlayerAnimatorController.cs | — | Bloat |
| **Medium** | `showStateDebug` and `showParameterDebug` default to `true` — OnGUI renders in production | PlayerController.cs / PlayerAnimatorController.cs | — | Bloat |
| **Medium** | Gravity ownership split between `PlayerMovement` and `PlayerClimb` — implicit fragile dependency | PlayerMovement.cs / PlayerClimb.cs | — | Architecture |
| **Medium** | No player snapping to ladder face — drifting off trigger causes abrupt exit | PlayerClimb.cs | — | Missing |
| **Low** | Duplicate `platformRotationAccum = Quaternion.identity` — copy-paste artifact | PlayerMovement.cs | 160 | Bug |
| **Low** | Nested `if (debugLogging)` is always-true redundancy | PlayerJump.cs | 72–74, 95–98 | Bug |
| **Low** | Jump apex formula ignores Rigidbody mass, displays wrong debug height | PlayerJump.cs | 127 | Bug |
| **Low** | `GetComponent<Rigidbody>()` called in `StartCrouch()` on every crouch press | PlayerCrouch.cs | 101 | Performance |
| **Low** | `GetComponent<PlayerInput>()` called on debug property getters every access | PlayerInput.cs | 27–28 | Performance |
| **Low** | `groundCheck`/`ceilingCheck` are child GameObjects — a `Vector3` field suffices | GroundChecker.cs | 44–53 | Bloat |
| **Low** | `TargetVelocity` calculated twice with identical expressions | PlayerMovement.cs | 226, 229 | Bloat |
| **Low** | `DebugMovementForce` is a public mutable field, not a property | PlayerMovement.cs | 357 | API |
| **Low** | `PlatformYawOffset` is a public mutable field, not a property | CameraController.cs | 25 | API |
| **Low** | Redundant component getter methods on `PlayerController` (`GetPlayerInput()`, etc.) | PlayerController.cs | 236–243 | API |
| **Low** | Wall check distance hardcoded at `0.16f`, not in config | GroundChecker.cs | 192 | Config |
| **Low** | Wall check uses global axis directions — diagonal walls not detected | GroundChecker.cs | 191 | Config |
| **Low** | Mouse lock toggle logic in `PlayerInput` instead of `CameraController` | PlayerInput.cs | 58–74 | Architecture |
| **Low** | `freezeYRotation` on `PlayerController` not synced with `CameraControllerConfig.FreezeYRotation` | PlayerController.cs | 42 | Config |
| **Low** | `.asmdef` has no `rootNamespace` set — IDE auto-generates namespace-less files | .asmdef | — | Tooling |
