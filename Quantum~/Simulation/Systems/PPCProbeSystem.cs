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
    static readonly FP RisingMargin = FP._0_10;

    // Unit ring directions, computed once. Built on first use (not in a static constructor) so the
    // fixed-point trig tables are loaded by then.
    static FPVector3[] _ring;
    static FPVector3[] Ring {
      get {
        if (_ring == null) {
          var ring = new FPVector3[RingRays];
          for (int i = 0; i < RingRays; i++) {
            var angle = i * (FP.PiTimes2 / RingRays);
            ring[i] = new FPVector3(FPMath.Cos(angle), 0, FPMath.Sin(angle));
          }
          _ring = ring;
        }
        return _ring;
      }
    }
    static readonly FP WallMinAngle = 45;

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var probes = config.Probes;
      var center = PPCProbe.CapsuleCenter(filter.Transform, filter.Collider);
      var halfHeight = config.HalfHeight(c->Crouch.IsCrouching);
      var ringRadius = config.Body.Radius * config.Advanced.ProbeRingRadius;

      c->Ground.WasGrounded = c->Ground.IsGrounded;

      // Ground
      var groundDistance = halfHeight + probes.GroundProbeMargin;
      if (RingCast(f, filter.Entity, center, FPVector3.Down, groundDistance, ringRadius, probes.GroundLayerMask, out var ground, out var groundHitDistance)) {
        c->Ground.Normal = ground.Normal;
        c->Ground.Gap = groundHitDistance - halfHeight;
        c->Ground.SlopeAngle = PPCProbe.SlopeAngle(ground.Normal);
        c->Ground.Entity = ground.Entity;
        var walkable = c->Ground.SlopeAngle <= probes.MaxSlopeAngle;
        // Rising faster than the ground below (a jump or launch in progress): the probe reaches below
        // the feet and still sees the floor for a few ticks, so keep the previous answer until the
        // character stops rising. Walking up a slope also rises, but it was grounded already.
        // "The ground below" is last tick's GroundVelocity, tracked even while airborne, so riding a
        // lift isn't mistaken for a jump.
        var groundVy = c->Platform.Entity == ground.Entity ? c->Platform.GroundVelocity.Y : FP._0;
        var rising = c->Vertical.AccumulatedY > groundVy + RisingMargin;
        c->Ground.IsGrounded = walkable && (!rising || c->Ground.WasGrounded);
      } else {
        c->Ground.IsGrounded = false;
        c->Ground.Normal = FPVector3.Up;
        c->Ground.SlopeAngle = FP._0;
        c->Ground.Gap = FP._0;
        c->Ground.Entity = EntityRef.None;
      }

      // Ceiling
      var ceilingDistance = halfHeight + probes.CeilingProbeMargin;
      c->Ground.IsCeilingBlocked = RingCast(f, filter.Entity, center, FPVector3.Up, ceilingDistance, ringRadius,
                                            probes.CeilingLayerMask, out _, out _, detailed: false);

      // Walls (fixed world axes, like the Unity version)
      c->Ground.IsTouchingWall = false;
      c->Ground.WallNormal = FPVector3.Zero;
      for (int i = 0; i < 4; i++) {
        var dir = i switch { 0 => FPVector3.Forward, 1 => FPVector3.Back, 2 => FPVector3.Left, _ => FPVector3.Right };
        if (PPCProbe.Raycast(f, filter.Entity, center + dir * ringRadius, dir, config.Advanced.WallCheckDistance,
                             probes.GroundLayerMask, out var wall, out _) &&
            PPCProbe.SlopeAngle(wall.Normal) > WallMinAngle) {
          c->Ground.IsTouchingWall = true;
          c->Ground.WallNormal = wall.Normal;
          break;
        }
      }
    }

    /// <summary>Centre ray plus a ring of rays; returns the closest hit.</summary>
    /// <param name="detailed">false when only "hit or not" matters: skips normals and hit points, stops at
    /// the first hit, and leaves <paramref name="closest"/>/<paramref name="best"/> unset.</param>
    static bool RingCast(Frame f, EntityRef self, FPVector3 center, FPVector3 dir, FP distance, FP radius, int mask,
                         out Hit3D closest, out FP best, bool detailed = true) {
      closest = default;
      best = FP.MaxValue;
      bool found = false;
      var ring = Ring;

      for (int i = -1; i < RingRays; i++) {
        var origin = center;
        if (i >= 0) {
          origin += new FPVector3(ring[i].X * radius, 0, ring[i].Z * radius);
        }
        if (!PPCProbe.Raycast(f, self, origin, dir, distance, mask, out var hit, out var d, detailed)) {
          continue;
        }
        if (!detailed) {
          return true;   // only "hit or not" was asked: no need to cast the remaining rays
        }
        if (d < best) {
          best = d;
          closest = hit;
          found = true;
        }
      }
      return found;
    }
  }
}
