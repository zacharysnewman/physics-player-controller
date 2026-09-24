namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// External forces for characters (and any other physics body). The controller absorbs the resulting
  /// velocity change on the next tick and lets it decay (see the movement and vertical layers).
  /// </summary>
  public static unsafe class PPCForces {
    /// <summary>Instant velocity change, e.g. a launch pad.</summary>
    public static void AddVelocity(Frame f, EntityRef entity, FPVector3 deltaV) {
      if (f.Unsafe.TryGetPointer<PhysicsBody3D>(entity, out var body) && !body->IsKinematic) {
        body->Velocity += deltaV;
      }
    }

    /// <summary>
    /// Pushes every dynamic body within <paramref name="radius"/> away from <paramref name="center"/>,
    /// with speed falling off linearly to zero at the edge. <paramref name="upwardBias"/> adds a
    /// fraction of the speed straight up (explosions usually lift).
    /// </summary>
    public static void AddExplosion(Frame f, FPVector3 center, FP radius, FP speed, FP upwardBias = default, int layerMask = -1) {
      var hits = f.Physics3D.OverlapShape(center, FPQuaternion.Identity, Shape3D.CreateSphere(radius), layerMask,
                                          QueryOptions.HitDynamics);
      for (int i = 0; i < hits.Count; i++) {
        var entity = hits[i].Entity;
        if (!f.Unsafe.TryGetPointer<Transform3D>(entity, out var t)) continue;
        var offset = t->Position - center;
        var distance = offset.Magnitude;
        if (distance > radius) continue;
        var direction = distance > FP.EN3 ? offset / distance : FPVector3.Up;
        var falloff = FP._1 - distance / radius;
        AddVelocity(f, entity, (direction + FPVector3.Up * upwardBias) * (speed * falloff));
      }
    }
  }
}
