# Transform Pivot Migration: Middle → Bottom (Feet)

## Overview

The player's `Transform` origin currently sits at the **middle** of the CapsuleCollider
(`m_Center: {x: 0, y: 0, z: 0}`, height 2 → pivot is at y=1 above feet). This forces
every system that cares about either the feet or the head to manually compute those positions.

The goal of this migration is to move the pivot to the **feet** (`m_Center: {x: 0, y: 1, z: 0}`
for a height-2 capsule), so that `transform.position` always equals the character's foot position.

---

## Current State vs Target State

| Property | Current (middle pivot) | Target (bottom/feet pivot) |
|---|---|---|
| `transform.position` | Middle of capsule | Feet / ground contact point |
| `capsule.center` | `(0, 0, 0)` | `(0, height/2, 0)` |
| Foot position formula | `transform.position + capsule.center - up * height/2` | `transform.position` |
| Head position formula | `transform.position + capsule.center + up * height/2` | `transform.position + up * height` |
| Ground check origin (local) | `capsule.center` = `(0, 0, 0)` | `capsule.center` = `(0, 1, 0)` |
| Crouch center shift needed? | Yes — complex | No — center is always `height/2` |

---

## Affected Systems

### 1. `PlayerCrouch.cs` — **Highest Impact**

This is the most complex system and the primary motivation for the migration.

#### Current Complexity

The crouch system must shift `capsule.center` differently depending on whether the player is
grounded or airborne, because with a middle pivot, shrinking the capsule height without
adjusting center causes the capsule to shrink symmetrically (half downward, half upward).

```csharp
// PlayerCrouch.cs:92 — Grounded: shift center DOWN to keep top fixed (head stays)
currentCenter = originalCenter + Vector3.down * (originalHeight - currentHeight) / 2f;

// PlayerCrouch.cs:98 — Midair: shift center UP to keep bottom fixed (feet tuck up)
currentCenter = originalCenter + Vector3.up * (originalHeight - currentHeight) / 2f;

// PlayerCrouch.cs:110 — Camera adjustment: complex formula balancing both factors
float heightAdjustment = (currentCenter.y - originalCenter.y) + (currentHeight - originalHeight) / 2f;
```

The grounded/midair distinction exists entirely because the pivot is in the middle. With bottom
pivot, `capsule.center = height / 2` at all times for grounded crouch — the capsule automatically
shrinks downward from the head, with the bottom staying pinned at `transform.position`.

#### Changes Required

**`PlayerCrouch.cs`**

- `ColliderBottomPosition` (line 31): Simplifies to `transform.position`. Remove the formula
  `transform.position + capsule.center - Vector3.up * (capsule.height / 2f)`.

- `originalCenter` (line 43): Will now store `(0, originalHeight/2, 0)` instead of `(0, 0, 0)`.
  This happens automatically since it reads from the collider, but the conceptual meaning changes.

- `StartCrouch()` grounded case (line 92): Replace with:
  ```csharp
  currentCenter = Vector3.up * (currentHeight / 2f);
  ```
  No more manual "shift down" calculation. Center is always half-height above feet.

- `StartCrouch()` midair case (line 98): **Decision required** (see Risks below).
  The original intent was to keep the player's head fixed while legs tuck up. With bottom pivot
  and center always at `height/2`, the capsule instead shrinks from the head downward (same as
  grounded). If leg-tuck behavior is still desired midair, it requires moving `rb.position`
  upward by the height delta, which is a new physics concern.

  Simple path (bottom shrinks from head, both cases same):
  ```csharp
  currentCenter = Vector3.up * (currentHeight / 2f);
  // Remove grounded/midair distinction entirely for center calculation
  ```

- Camera height adjustment formula (line 110): Simplifies to:
  ```csharp
  float heightAdjustment = currentHeight - originalHeight;
  ```
  (Camera drops by the full height delta when crouching grounded. For midair where head stays
  fixed, adjust to `0f` if preserving that behavior.)

- `StopCrouch()` (line 127): `currentCenter = originalCenter` stays valid since `originalCenter`
  will be `(0, originalHeight/2, 0)` — no change needed in logic.

**Risk:** Midair crouch behavior changes unless explicitly handled. The current system returns
camera offset to 0 in midair (head stays fixed). With simplified formula, camera also moves,
which may look wrong. Decide whether midair crouch is in-scope before migrating.

---

### 2. `GroundChecker.cs` — **Medium Impact**

#### Current Complexity

`groundCheck` and `ceilingCheck` transforms are placed at `capsule.center` (world-space middle)
every frame. The raycast distances are inflated by ~1 unit to compensate for the pivot offset:

```csharp
// GroundChecker.cs:46, 52, 67, 71
groundCheck.localPosition = capsule.center;   // (0,0,0) = middle of capsule
ceilingCheck.localPosition = capsule.center;  // (0,0,0) = middle of capsule
```

```
// GroundCheckerConfig.cs:9, 14 (comments explain the compensation)
groundCheckDistance = 1.15f;  // Was 0.15f — +1.0 to reach past middle to ground
ceilingCheckDistance = 1.1f;  // Was 0.1f  — +1.0 to reach past middle to ceiling
```

The `CheckWall()` method (line 200) also uses `groundCheck.position` as its ray origin, placing
wall checks at capsule center height — which happens to be correct for middle pivot but is
implicit coupling.

#### Changes Required

**Option A — Minimum change (keep conceptual model):**
Keep `groundCheck.localPosition = capsule.center`. With bottom pivot, `capsule.center` becomes
`(0, height/2, 0)`, so the check point is still at capsule center in world space. Distances
remain the same (1.05/1.1 to reach ground/ceiling from center). Wall checks also remain at
capsule center. **No code changes required** — behavior is identical.

**Option B — Full simplification (recommended long-term):**
Place `groundCheck` at feet and `ceilingCheck` at the capsule top:

```csharp
// Awake / Update
groundCheck.localPosition = Vector3.zero;               // At feet
ceilingCheck.localPosition = Vector3.up * capsule.height; // At top of capsule
```

Then distances return to their natural small-buffer values:
```
groundCheckDistance = 0.15f   // Small buffer below feet
ceilingCheckDistance = 0.1f   // Small buffer above head
```

**Risk with Option B:** `CheckWall()` uses `groundCheck.position` as ray origin for horizontal
wall detection (line 200). With `groundCheck` at feet, wall rays fire from ground level, which
will miss walls that don't extend to the ground. A dedicated `wallCheck` transform at capsule
center must be introduced, or the wall check must compute its origin independently.

**Recommended approach:** Do Option A first (zero risk, zero behavioral change), then Option B
as a separate cleanup PR once the pivot migration is validated.

---

### 3. `PlayerMovement.cs` — **Medium Impact (contains a live bug)**

#### Current Complexity / Bug

**`AdjustForTerrain` (line 279):**
```csharp
Vector3 center = transform.position + capsule.center;
```
With middle pivot, `capsule.center = (0,0,0)` so this is `transform.position` (the middle).
With bottom pivot, `capsule.center = (0, height/2, 0)` so this is still the capsule midpoint
in world space. **No behavioral change** — formula is the same.

**`HandleStep` — LIVE BUG (line 322):**
```csharp
float playerBottomY = transform.position.y - 1f;  // Hardcoded assumption: capsule half-height = 1
```
This is already broken during crouch (height=1.4, half=0.7, but code uses 1.0). With bottom
pivot this becomes:

```csharp
float playerBottomY = transform.position.y;  // Feet ARE the transform — no calculation needed
```

This fixes the existing bug automatically as a side effect of the migration.

**Step ray origin (line 313):**
```csharp
Vector3 rayOrigin = transform.position + moveDirection.normalized * 0.51f;
```
With middle pivot, this shoots from the middle of the character (y ≈ 1) horizontally. With
bottom pivot, this shoots from feet level. The downward raycast distance of `2f` (line 317)
would still capture steps since it fires from feet height and steps are below. **Low risk**,
but visually the debug gizmo `VisualizeStepRays` will show the ray origin at feet instead
of mid-body. Consider raising the origin to `transform.position + Vector3.up * 0.05f` to
avoid false positives from ground-level noise.

**`VisualizeTerrainRays` (line 369):**
```csharp
Vector3 center = transform.position + capsule.center;
```
Same as `AdjustForTerrain` — formula unchanged, result unchanged.

---

### 4. `CameraController.cs` — **Low Code Impact, Prefab Change Required**

No logic changes to the script itself. The `baseHeight` is read from the camera's initial
`localPosition.y` in the prefab.

**Current prefab:** Camera child `localPosition.y = 0.75`. With middle pivot, the camera is
0.75 above the capsule center = 0.75 + 1.0 = **1.75m above the ground** (eye height).

**After migration:** The player root is now at feet. Camera `localPosition.y` must be set to
`1.75` in the prefab to maintain the same eye height. The `AdjustHeight()` method and the
crouch height offset system are unaffected in logic.

**Risk:** If the camera `localPosition.y` is not updated in the prefab, the player will appear
to view the world from foot level.

---

### 5. `DebugVisualizer.cs` — **Low Impact**

Two debug check transforms are created and placed at `capsule.center` (Awake lines 57, 61):
```csharp
groundCheck.localPosition = capsule.center;  // line 57
ceilingCheck.localPosition = capsule.center;  // line 61
```
These are visual-only debug objects. They are unaffected in behavior if Option A is used for
`GroundChecker`. If Option B is used, update these to match the new positions.

Gizmo visualizations that use `transform.position + capsule.center` (bounds, crouch box):
remain correct since `capsule.center` accounts for the offset. Lines 94-96, 160.

Gizmos that use bare `transform.position` (velocity lines 121-122, acceleration lines 128-129,
jump/coyote spheres lines 139, 147, 152, crouch indicator sphere line 164) will now draw from
feet rather than the capsule center. These are visual-only — behavior is unaffected, but the
debug indicators will appear lower.

---

### 6. `PlayerController.cs` — **Minimal Impact**

`ColliderBottomPosition` (line 40) delegates to `playerCrouch.ColliderBottomPosition`. After
the `PlayerCrouch` change, this returns `transform.position`. The property may be simplified to:
```csharp
public Vector3 ColliderBottomPosition => transform.position;
```
or left delegating to `PlayerCrouch` — either works.

No other impact.

---

### 7. `TransformFollower.cs` — **Minimal Impact**

The `useDynamicColliderBottom` path (line 41) calls `playerController.ColliderBottomPosition`.
After the migration, this returns `transform.position` (feet), which is exactly what
`TransformFollower` uses to position a character mesh or similar.

The `yOffset` path (line 45) adds `Vector3.up * yOffset` to the position target. The prefab
currently sets `yOffset: 0` and `useDynamicColliderBottom: 1` for the `Character` child.
After migration, if the `Character` child visual uses `yOffset` instead of dynamic collider
bottom, the `yOffset` default in code (`-1f`) would place it at the wrong position.
**Check any non-prefab users of `TransformFollower` that assume middle pivot.**

---

### 8. `PlayerAnimatorController.cs` — **No Impact**

Uses `playerRigidbody.linearVelocity`, `playerController.CurrentState`, and `playerInput`
exclusively. Does not reference world positions or capsule geometry. No changes needed.

---

### 9. `PlayerClimb.cs` — **No Impact Expected**

Climb positioning uses ladder bounds and relative position calculations against the ladder
collider. Does not reference capsule center or foot position directly. Verify after migration
that ladder attachment points look correct, but no code changes anticipated.

---

## Config Assets That Need Updating

### `Runtime/DefaultConfigs/GroundCheckerConfig.asset`

| Field | Current Value | Post-Migration Value (Option A) | Post-Migration Value (Option B) |
|---|---|---|---|
| `groundCheckDistance` | `1.05` | `1.05` (unchanged) | `0.05` |
| `ceilingCheckDistance` | `1.1` | `1.1` (unchanged) | `0.1` |

The comment in `GroundCheckerConfig.cs` ("Increased from 0.15f to account for ray origin at
capsule center") remains accurate under Option A since the ray still originates at capsule
center. Under Option B, update comments and values.

### `Runtime/DefaultConfigs/PlayerMovementConfig.asset`

| Field | Current Value | Notes |
|---|---|---|
| `standingHeight` | `2` | Unchanged |
| `crouchingHeight` | `1` | Unchanged |

These are only used in `AdjustForTerrain` for a local variable `playerHeight` (line 277)
that is currently unused after assignment. No impact from migration, but the unused variable
is worth cleaning up.

### `Runtime/DefaultConfigs/PlayerCrouchConfig.asset`

No value changes needed. The `crouchHeight: 1.4` value is still the target height.

### `Runtime/DefaultConfigs/CameraControllerConfig.asset`

No changes needed.

---

## Prefab Changes (`Runtime/Prefabs/PPC.prefab`)

All values are text-based YAML and can be edited directly.

### 1. CapsuleCollider center

```yaml
# Current
m_Center: {x: 0, y: 0, z: 0}

# After migration
m_Center: {x: 0, y: 1, z: 0}
```
(Use `height / 2` = `2 / 2 = 1` for the standing collider.)

### 2. Camera child local position

```yaml
# Current (Camera child, fileID: 2535230407007130156)
m_LocalPosition: {x: 0, y: 0.75, z: 0}

# After migration — maintain 1.75m eye height above feet
m_LocalPosition: {x: 0, y: 1.75, z: 0}
```

### 3. Character visual child local position

```yaml
# Current (Character child, fileID: 8686996330862596232)
m_LocalPosition: {x: 0, y: -1, z: 0}

# After migration — visual mesh root at feet, no offset needed
m_LocalPosition: {x: 0, y: 0, z: 0}
```
The -1 offset was compensating for the middle pivot so the mesh feet align with the ground.
With bottom pivot, the root IS at the ground.

### 4. GroundCheck child local position

```yaml
# Current (GroundCheck child, fileID: 7540897855728754020)
m_LocalPosition: {x: 0, y: -0.55, z: 0}

# After migration (Option A — keep at capsule center)
m_LocalPosition: {x: 0, y: 1, z: 0}

# After migration (Option B — place at feet)
m_LocalPosition: {x: 0, y: 0, z: 0}
```
Note: `GroundChecker.cs` overwrites this every frame via `groundCheck.localPosition = capsule.center`,
so the prefab-serialized value only matters for the first frame before `Awake` runs. Still
worth updating for consistency and to avoid confusion.

### 5. CeilingCheck child local position

```yaml
# Current (CeilingCheck child, fileID: 4606087182710894023)
m_LocalPosition: {x: 0, y: 0.55, z: 0}

# After migration (Option A — keep at capsule center)
m_LocalPosition: {x: 0, y: 1, z: 0}

# After migration (Option B — place at capsule top)
m_LocalPosition: {x: 0, y: 2, z: 0}
```
Same caveat: overwritten at runtime by `GroundChecker`.

### 6. Root Transform position

```yaml
# Current world position (wherever placed in scene)
m_LocalPosition: {x: 20.542, y: 2.1602952, z: 31.124}
```
After migration, `transform.position.y` represents feet, not middle. If the prefab is placed
at a specific world position, its Y must be shifted down by `originalHeight / 2 = 1.0` to
keep the feet at the same ground contact point. **Verify scene placement after migration.**

---

## Migration Order (Recommended)

1. **Update `PPC.prefab`** — CapsuleCollider center, Camera localPosition, Character
   localPosition. This is the core change. Test immediately: character should appear at
   same visual position, capsule should be rooted at feet.

2. **Update `PlayerCrouch.cs`** — Simplify `ColliderBottomPosition`, simplify grounded
   crouch center formula. Decide on midair crouch behavior before touching that branch.

3. **Update `PlayerMovement.cs`** — Fix `HandleStep` bug (`transform.position.y - 1f` → `transform.position.y`).

4. **Update `GroundCheckerConfig.cs` default value** — Change `1.15f` comment to reflect
   new understanding. Defer Option B changes until wall-check refactor is planned.

5. **Test all states** — Standing, crouching grounded, crouching midair, jumping, landing,
   step-up, ceiling collision, moving platforms.

6. **Optional cleanup (separate PR)** — Option B ground/ceiling check simplification,
   wall-check refactor, `VisualizeTerrainRays` origin adjustment.

---

## Risk Summary

| Risk | Severity | Mitigation |
|---|---|---|
| Midair crouch behavior changes (camera moves instead of staying fixed) | Medium | Explicitly handle midair case in `PlayerCrouch.StartCrouch()` |
| Scene prefab Y position off by 1 unit after migration | High | Shift all scene instances of PPC down by 1.0 after changing collider center |
| Wall detection fires from feet level (Option B only) | Medium | Defer Option B; add dedicated wallCheck origin before removing groundCheck dependency |
| `TransformFollower` users with `yOffset = -1f` break | Low | Audit all `TransformFollower` instances; update `yOffset` or switch to `useDynamicColliderBottom` |
| Debug gizmos appear at feet instead of body center | Low | Visual only — no gameplay impact |
| Step detection ray origin at ground level may graze flat surfaces | Low | Raise step ray origin by a small epsilon after migration |
