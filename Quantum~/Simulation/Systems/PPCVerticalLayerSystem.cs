namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Vertical velocity layer, ported from the Unity package's VerticalVelocityLayer: gravity integration,
  /// platform-relative grounding, walk-off dismount, ceiling hits, and absorption of external vertical
  /// forces (launch pads, explosions). Adds slope following and ground snapping (see below).
  /// <para>
  /// The Unity version's <c>skipExternalAbsorption</c> flag is gone: it worked around Update and
  /// FixedUpdate running at different rates. Here the jump changes <c>AccumulatedY</c> but not
  /// <c>LastTargetY</c>, so the deviation check is always against what the body was actually driven to.
  /// </para>
  /// </summary>
  public unsafe class PPCVerticalLayerSystem : PPCSystemBase {
    static readonly FP LaunchThreshold = FP._0_10;
    static readonly FP AbsorbThreshold = FP.FromString("0.01");
    static readonly FP JumpInFlightMargin = FP._0_10;

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var v = &c->Vertical;
      var dt = f.DeltaTime;
      var gravity = f.PhysicsSceneSettings->Gravity.Y * config.Body.GravityScale;
      var bodyY = filter.Body->Velocity.Y;

      // Platform vertical velocity (PPCPlatformSystem).
      v->LastPlatformY = v->PlatformY;
      v->PlatformY = c->Platform.BaseVelocity.Y;

      // Grounding. While a jump is in flight the ground probe (which reaches below the feet) can still
      // report ground for a few ticks; ignore it until the character is no longer moving up.
      var wasGrounded = v->IsGrounded;
      var probeGrounded = c->Ground.IsGrounded;
      if (!(probeGrounded && v->AccumulatedY > v->PlatformY + JumpInFlightMargin)) {
        v->IsGrounded = probeGrounded;
      }
      if (wasGrounded && !v->IsGrounded && !c->Jump.JumpedThisTick) {
        v->AccumulatedY = v->LastPlatformY;   // walk-off: keep the platform's vertical motion
      }

      // Ceiling: cancel upward velocity.
      if (c->Ground.IsCeilingBlocked && v->AccumulatedY > 0) {
        v->AccumulatedY = FP._0;
      }

      if (v->IsGrounded) {
        // A significant upward deviation while grounded is a launch (launch pad, explosion).
        if (bodyY - v->LastTargetY > LaunchThreshold) {
          v->AccumulatedY = bodyY + gravity * dt;
          v->IsGrounded = false;
        } else {
          // Follow the ground: pick the vertical speed that keeps the (platform-relative) horizontal
          // motion along the surface, so the solver never has to push the character out of a slope.
          // Then close any gap to the ground within one tick (ground snap). The Unity version set 0
          // here: it could hover up to the probe margin (0.15 m) above the floor, and on slopes the
          // solver's push-out looked like a launch.
          var n = c->Ground.Normal;
          var horizontal = c->Horizontal.LastContribution - new FPVector3(c->Platform.BaseVelocity.X, 0, c->Platform.BaseVelocity.Z);
          var alongSlope = n.Y > FP._0_10 ? -(n.X * horizontal.X + n.Z * horizontal.Z) / n.Y : FP._0;
          v->AccumulatedY = v->PlatformY + alongSlope - FPMath.Max(FP._0, c->Ground.Gap) / dt;
        }
      } else {
        // Airborne: absorb external vertical forces, then integrate gravity.
        var external = bodyY - v->LastTargetY;
        if (FPMath.Abs(external) > AbsorbThreshold && !c->Jump.JumpedThisTick) {
          v->AccumulatedY += external;
        }
        v->AccumulatedY += gravity * dt;
      }

      if (!wasGrounded && v->IsGrounded) {
        f.Events.PPCLanded(filter.Entity, FPMath.Max(FP._0, -v->LastTargetY));
      }

      v->LastTargetY = v->AccumulatedY;
    }
  }
}
