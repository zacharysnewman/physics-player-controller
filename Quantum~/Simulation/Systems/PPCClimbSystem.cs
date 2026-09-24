namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Ladder climbing, ported from the Unity package's PlayerClimb, as an exclusive velocity layer.
  /// A ladder is any trigger collider whose entity has a <see cref="PPCLadder"/> component; the ladder
  /// faces its local +Z (or -Z: the character climbs on whichever side it's on).
  /// <list type="bullet">
  /// <item>Grab: touching a ladder starts climbing (a ladder just let go of isn't re-grabbed until the
  /// character has left it).</item>
  /// <item>Move: forward/back climbs up/down, reversed when looking down past
  /// <c>Climb.LookDownThreshold</c>; strafing moves sideways along the ladder.</item>
  /// <item>Let go: jump (launches with <c>Climb.JumpOffVelocity</c>), reaching the ground from above,
  /// or leaving the ladder's volume (e.g. over the top).</item>
  /// </list>
  /// Fixes from the Unity evaluation: the character is pulled to the ladder face (<c>SnapStrength</c>),
  /// jumping off launches, and the look-down reversal has a threshold instead of flipping at level.
  /// </summary>
  public unsafe class PPCClimbSystem : PPCSystemBase {
    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var climb = &c->Climb;
      var ladder = FindLadder(f, ref filter, config);

      if (climb->Released.IsValid && climb->Released != ladder) {
        climb->Released = EntityRef.None;   // left the ladder we let go of
      }

      if (!climb->IsClimbing) {
        if (ladder.IsValid && ladder != climb->Released) {
          Grab(f, ref filter, ladder);
        } else {
          return;
        }
      }

      // Let go?
      var jumpPressed = c->Input.Jump && !c->PreviousInput.Jump;
      if (jumpPressed) {
        Release(f, ref filter, config, jumpOff: true);
        return;
      }
      if (!ladder.IsValid || (c->Ground.IsGrounded && !c->Ground.WasGrounded)) {
        Release(f, ref filter, config, jumpOff: false);
        return;
      }
      climb->Ladder = ladder;
      climb->Velocity = ClimbVelocity(f, ref filter, config);
    }

    static EntityRef FindLadder(Frame f, ref PPCFilter filter, PPCConfig config) {
      var hits = f.Physics3D.OverlapShape(filter.Transform->Position, FPQuaternion.Identity, filter.Collider->Shape,
                                          config.Climb.LayerMask, QueryOptions.HitAll);
      for (int i = 0; i < hits.Count; i++) {
        var e = hits[i].Entity;
        if (e != filter.Entity && e.IsValid && f.Has<PPCLadder>(e)) {
          return e;
        }
      }
      return EntityRef.None;
    }

    static void Grab(Frame f, ref PPCFilter filter, EntityRef ladder) {
      var c = filter.Character;
      c->Climb.IsClimbing = true;
      c->Climb.Ladder = ladder;
      c->Horizontal.Current = FPVector3.Zero;   // Unity: ResetHorizontalVelocity
      c->Horizontal.External = FPVector3.Zero;
      c->Jump.IsJumping = false;
      f.Events.PPCClimbStarted(filter.Entity, ladder);
    }

    static void Release(Frame f, ref PPCFilter filter, PPCConfig config, bool jumpOff) {
      var c = filter.Character;
      var ladder = c->Climb.Ladder;
      c->Climb.IsClimbing = false;
      c->Climb.Released = ladder;
      c->Climb.Velocity = FPVector3.Zero;
      c->Vertical.AccumulatedY = FP._0;          // Unity: cancel vertical velocity on exit

      if (jumpOff) {
        var away = AwayFromLadder(f, ref filter, ladder);
        var launch = config.Climb.JumpOffVelocity;
        c->Horizontal.Current = away * launch.Z;
        c->Vertical.AccumulatedY = launch.Y;
        c->Vertical.IsGrounded = false;
        c->Jump.JumpedThisTick = true;
      }
      f.Events.PPCClimbEnded(filter.Entity);
    }

    static FPVector3 ClimbVelocity(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var input = c->Input;
      var speed = config.Climb.Speed;

      var vertical = input.Move.Y;
      if (input.LookPitch < -config.Climb.LookDownThreshold) {
        vertical = -vertical;   // looking down the ladder: forward goes down
      }

      var right = FPQuaternion.Euler(0, input.LookYaw, 0) * FPVector3.Right;
      var velocity = FPVector3.Up * (vertical * speed) + right * (input.Move.X * speed);

      // Pull onto the ladder face: keep a fixed distance in front of the ladder along its facing axis.
      if (config.Climb.SnapStrength > 0 && f.Unsafe.TryGetPointer<Transform3D>(c->Climb.Ladder, out var t) &&
          f.Unsafe.TryGetPointer<PhysicsCollider3D>(c->Climb.Ladder, out var col) && col->Shape.Type == Shape3DType.Box) {
        var facing = t->Rotation * FPVector3.Forward;
        var depth = FPVector3.Dot(filter.Transform->Position - t->Position, facing);
        var side = depth >= 0 ? FP._1 : -FP._1;
        // Slightly inside touching distance, so the character never hovers at the trigger's edge.
        var target = side * (col->Shape.Box.Extents.Z + config.Body.Radius - FP._0_10);
        velocity += facing * ((target - depth) * config.Climb.SnapStrength);
      }
      return velocity;
    }

    static FPVector3 AwayFromLadder(Frame f, ref PPCFilter filter, EntityRef ladder) {
      if (!f.Unsafe.TryGetPointer<Transform3D>(ladder, out var t)) return FPVector3.Zero;
      var facing = t->Rotation * FPVector3.Forward;
      var depth = FPVector3.Dot(filter.Transform->Position - t->Position, facing);
      return depth >= 0 ? facing : -facing;
    }
  }
}
