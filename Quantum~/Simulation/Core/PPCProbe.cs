namespace Quantum {
  using Photon.Deterministic;
  using Quantum.Physics3D;

  /// <summary>Physics query helpers that ignore the character's own collider.</summary>
  public static unsafe class PPCProbe {
    const QueryOptions Solids = QueryOptions.HitStatics | QueryOptions.HitKinematics | QueryOptions.HitDynamics;

    /// <summary>
    /// Nearest solid (non-trigger) hit along the ray that isn't <paramref name="self"/>. Pass
    /// <paramref name="detailed"/> = false when only "hit or not" matters: normals and hit points are then
    /// not computed, so <paramref name="hit"/>'s normal and <paramref name="hitDistance"/> aren't meaningful.
    /// </summary>
    public static bool Raycast(Frame f, EntityRef self, FPVector3 origin, FPVector3 direction, FP distance, int layerMask,
                               out Hit3D hit, out FP hitDistance, bool detailed = true) {
      var hits = f.Physics3D.RaycastAll(origin, direction, distance, layerMask,
                                        detailed ? Solids | QueryOptions.ComputeDetailedInfo : Solids);
      hit = default;
      hitDistance = FP.MaxValue;
      bool found = false;
      for (int i = 0; i < hits.Count; i++) {
        var h = hits[i];
        if (h.Entity == self) {
          continue;
        }
        var d = FPVector3.Dot(h.Point - origin, direction);
        if (!found || d < hitDistance) {
          hitDistance = d;
          hit = h;
          found = true;
        }
      }
      return found;
    }

    /// <summary>Does <paramref name="shape"/> at <paramref name="center"/> overlap any solid other than <paramref name="self"/>?</summary>
    public static bool OverlapsOther(Frame f, EntityRef self, FPVector3 center, Shape3D shape, int layerMask) {
      var hits = f.Physics3D.OverlapShape(center, FPQuaternion.Identity, shape, layerMask, Solids);
      for (int i = 0; i < hits.Count; i++) {
        if (hits[i].Entity != self) return true;
      }
      return false;
    }

    /// <summary>First overlapping entity (triggers included) that has component <typeparamref name="T"/>.</summary>
    public static EntityRef FindOverlapping<T>(Frame f, EntityRef self, FPVector3 center, Shape3D shape, int layerMask)
      where T : unmanaged, IComponent {
      var hits = f.Physics3D.OverlapShape(center, FPQuaternion.Identity, shape, layerMask, QueryOptions.HitAll);
      for (int i = 0; i < hits.Count; i++) {
        var e = hits[i].Entity;
        if (e != self && e.IsValid && f.Has<T>(e)) return e;
      }
      return EntityRef.None;
    }

    /// <summary>Where the capsule centre is (entity position plus the collider's local offset).</summary>
    public static FPVector3 CapsuleCenter(Transform3D* transform, PhysicsCollider3D* collider) =>
      transform->Position + collider->Shape.LocalTransform.Position;

    /// <summary>Angle between <paramref name="normal"/> and up, in degrees.</summary>
    public static FP SlopeAngle(FPVector3 normal) => FPVector3.Angle(FPVector3.Up, normal);
  }
}
