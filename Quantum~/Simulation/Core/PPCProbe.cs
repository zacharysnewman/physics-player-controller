namespace Quantum {
  using Photon.Deterministic;
  using Quantum.Physics3D;

  /// <summary>Physics query helpers that ignore the character's own collider and triggers.</summary>
  public static unsafe class PPCProbe {
    const QueryOptions SolidHits = QueryOptions.HitStatics | QueryOptions.HitKinematics | QueryOptions.HitDynamics |
                                   QueryOptions.ComputeDetailedInfo;

    /// <summary>Nearest solid hit along the ray that isn't <paramref name="self"/>.</summary>
    public static bool Raycast(Frame f, EntityRef self, FPVector3 origin, FPVector3 direction, FP distance, int layerMask,
                               out Hit3D hit, out FP hitDistance) {
      var hits = f.Physics3D.RaycastAll(origin, direction, distance, layerMask, SolidHits);
      hit = default;
      hitDistance = FP.MaxValue;
      bool found = false;
      for (int i = 0; i < hits.Count; i++) {
        var h = hits[i];
        if (h.Entity == self) {
          continue;
        }
        var d = FPVector3.Dot(h.Point - origin, direction);
        if (d < hitDistance) {
          hitDistance = d;
          hit = h;
          found = true;
        }
      }
      return found;
    }

    /// <summary>Where the capsule centre is (entity position plus the collider's local offset).</summary>
    public static FPVector3 CapsuleCenter(Transform3D* transform, PhysicsCollider3D* collider) =>
      transform->Position + collider->Shape.LocalTransform.Position;

    /// <summary>Angle between <paramref name="normal"/> and up, in degrees.</summary>
    public static FP SlopeAngle(FPVector3 normal) => FPVector3.Angle(FPVector3.Up, normal);
  }
}
