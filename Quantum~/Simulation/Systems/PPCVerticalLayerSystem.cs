namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Vertical velocity layer, ported from the Unity package's VerticalVelocityLayer: gravity integration,
  /// platform-relative grounding, walk-off dismount, ceiling hits, and absorption of external vertical
  /// forces (launch pads, explosions). Adds slope following and ground snapping (see below).
  /// <para>
  /// The Unity version's <c>skipExternalAbsorption</c> flag is gone: it worked around Update and
  /// FixedUpdate running at different rates. Here the jump changes <c>AccumulatedY</c> but not
  /// <c>TargetY</c>, so the deviation check is always against what the body was actually driven to.
  /// </para>
  /// </summary>
  public unsafe class PPCVerticalLayerSystem : PPCSystemBase {
    static readonly FP LaunchThreshold = FP._0_10;
    static readonly FP AbsorbThreshold = FP.FromString("0.01");

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var v = &c->Vertical;
      var dt = f.DeltaTime;
      var gravity = f.PhysicsSceneSettings->Gravity.Y * config.Body.GravityScale;
      var bodyY = filter.Body->Velocity.Y;

      if (c->Climb.IsClimbing) {
        return;   // the exclusive climb layer drives (and holds this layer; see PPCClimbSystem)
      }

      // Platform vertical velocity (PPCPlatformSystem).
      v->LastPlatformY = v->PlatformY;
      v->PlatformY = c->Platform.BaseVelocity.Y;

      // Grounding comes from PPCProbeSystem (shared by every system; see PPCGroundInfo).
      var wasGrounded = c->Ground.WasGrounded;
      if (wasGrounded && !c->Ground.IsGrounded && !c->Jump.JumpedThisTick) {
        v->AccumulatedY = v->LastPlatformY;   // walk-off: keep the platform's vertical motion
      }

      // Ceiling: cancel upward velocity.
      if (c->Ground.IsCeilingBlocked && v->AccumulatedY > 0) {
        v->AccumulatedY = FP._0;
      }

      if (c->Ground.IsGrounded) {
        // A significant upward deviation while grounded is a launch (launch pad, explosion), unless
        // the ground itself explains it (a lift pushing the character up at its own speed).
        var explained = FPMath.Max(v->TargetY, c->Platform.GroundVelocity.Y);
        if (bodyY - explained > LaunchThreshold) {
          v->AccumulatedY = bodyY + gravity * dt;
          c->Ground.IsGrounded = false;
        } else {
          // Follow the ground: pick the vertical speed that keeps the (platform-relative) horizontal
          // motion along the surface, so the solver never has to push the character out of a slope.
          // Then close any gap to the ground within one tick (ground snap). The Unity version set 0
          // here: it could hover up to the probe margin (0.15 m) above the floor, and on slopes the
          // solver's push-out looked like a launch.
          var n = c->Ground.Normal;
          var horizontal = c->Horizontal.Contribution - c->Platform.BaseVelocity.Flat();
          var alongSlope = n.Y > FP._0_10 ? -(n.X * horizontal.X + n.Z * horizontal.Z) / n.Y : FP._0;
          v->AccumulatedY = v->PlatformY + alongSlope - FPMath.Max(FP._0, c->Ground.Gap) / dt;
        }
      } else {
        // Airborne: absorb external vertical forces, then integrate gravity.
        var external = bodyY - v->TargetY;
        if (FPMath.Abs(external) > AbsorbThreshold && !c->Jump.JumpedThisTick) {
          v->AccumulatedY += external;
        }
        v->AccumulatedY += gravity * dt;
      }

      if (!wasGrounded && c->Ground.IsGrounded) {
        f.Events.PPCLanded(filter.Entity, FPMath.Max(FP._0, -v->TargetY));
      }

      v->TargetY = v->AccumulatedY;
    }
  }
}
