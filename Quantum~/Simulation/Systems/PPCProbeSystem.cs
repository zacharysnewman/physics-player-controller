namespace Quantum {
  using Photon.Deterministic;
  using Quantum.Physics3D;

  /// <summary>
  /// Ground, ceiling and wall probes, ported from the Unity package's GroundChecker: a ring of 16 rays
  /// plus a centre ray, downwards for ground and upwards for the ceiling, and four horizontal wall rays.
  /// Probe lengths follow the current capsule height (the Unity version used fixed lengths from the
  /// capsule centre, which grew relative to a crouched capsule).
  /// </summary>
  public unsafe class PPCProbeSystem : PPCSystemBase {
    const int RingRays = 16;
    static readonly FP WallMinAngle = 45;

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var probes = config.Probes;
      var center = PPCProbe.CapsuleCenter(filter.Transform, filter.Collider);
      var halfHeight = config.HalfHeight(c->Crouch.IsCrouching);
      var standingHalf = config.HalfHeight(false);
      var ringRadius = config.Body.Radius * probes.RadiusMultiplier;

      c->Ground.WasGrounded = c->Ground.IsGrounded;

      // Ground
      var groundDistance = halfHeight + (probes.GroundCheckDistance - standingHalf);
      if (RingCast(f, filter.Entity, center, FPVector3.Down, groundDistance, ringRadius, probes.GroundLayerMask, out var ground, out var groundHitDistance)) {
        c->Ground.Normal = ground.Normal;
        c->Ground.Gap = groundHitDistance - halfHeight;
        c->Ground.SlopeAngle = PPCProbe.SlopeAngle(ground.Normal);
        c->Ground.Entity = ground.Entity;
        c->Ground.IsGrounded = c->Ground.SlopeAngle <= probes.MaxSlopeAngle;
      } else {
        c->Ground.IsGrounded = false;
        c->Ground.Normal = FPVector3.Up;
        c->Ground.SlopeAngle = FP._0;
        c->Ground.Gap = FP._0;
        c->Ground.Entity = EntityRef.None;
      }

      // Ceiling
      var ceilingDistance = halfHeight + (probes.CeilingCheckDistance - standingHalf);
      c->Ground.IsCeilingBlocked = RingCast(f, filter.Entity, center, FPVector3.Up, ceilingDistance, ringRadius,
                                            probes.CeilingLayerMask, out _, out _);

      // Walls (fixed world axes, like the Unity version)
      c->Ground.IsTouchingWall = false;
      c->Ground.WallNormal = FPVector3.Zero;
      for (int i = 0; i < 4; i++) {
        var dir = i switch { 0 => FPVector3.Forward, 1 => FPVector3.Back, 2 => FPVector3.Left, _ => FPVector3.Right };
        if (PPCProbe.Raycast(f, filter.Entity, center + dir * ringRadius, dir, probes.WallCheckDistance,
                             probes.GroundLayerMask, out var wall, out _) &&
            PPCProbe.SlopeAngle(wall.Normal) > WallMinAngle) {
          c->Ground.IsTouchingWall = true;
          c->Ground.WallNormal = wall.Normal;
          break;
        }
      }
    }

    /// <summary>Centre ray plus a ring of rays; returns the closest hit.</summary>
    static bool RingCast(Frame f, EntityRef self, FPVector3 center, FPVector3 dir, FP distance, FP radius, int mask,
                         out Hit3D closest, out FP best) {
      closest = default;
      best = FP.MaxValue;
      bool found = false;

      for (int i = -1; i < RingRays; i++) {
        var origin = center;
        if (i >= 0) {
          var angle = i * (FP.PiTimes2 / RingRays);
          origin += new FPVector3(FPMath.Cos(angle) * radius, 0, FPMath.Sin(angle) * radius);
        }
        if (PPCProbe.Raycast(f, self, origin, dir, distance, mask, out var hit, out var d) && d < best) {
          best = d;
          closest = hit;
          found = true;
        }
      }
      return found;
    }
  }
}
