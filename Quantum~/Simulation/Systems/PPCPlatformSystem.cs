namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Moving-platform support, ported from the Unity package's PlayerMovement.TrackPlatformMovement.
  /// Works out the velocity of the point the character stands on (<see cref="PPCPlatformState.BaseVelocity"/>);
  /// the movement and vertical layers then work relative to it. Dynamic platforms use their body's
  /// linear and angular velocity (as in Unity). Kinematic and body-less platforms use the change in
  /// their transform since the last tick, applied as an exact rigid motion. Quantum doesn't move
  /// kinematic bodies by their velocity, and games often move platforms by transform without setting
  /// it. Run your platform-moving systems before the controller's so there's no one-tick lag.
  /// </summary>
  public unsafe class PPCPlatformSystem : PPCSystemBase {
    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var p = &c->Platform;
      var ground = c->Ground.Entity;
      var dt = f.DeltaTime;

      if (!c->Ground.IsGrounded || !ground.IsValid || !f.Unsafe.TryGetPointer<Transform3D>(ground, out var platform)) {
        p->Entity = EntityRef.None;
        p->BaseVelocity = FPVector3.Zero;
        p->YawDelta = FP._0;
        return;
      }

      var characterPosition = filter.Transform->Position;

      if (p->Entity != ground) {
        // New platform: start tracking; no motion known yet unless it has a body.
        p->Entity = ground;
        p->PreviousPosition = platform->Position;
        p->PreviousRotation = platform->Rotation;
      }

      FPVector3 velocity;
      FP yawDelta;
      if (f.Unsafe.TryGetPointer<PhysicsBody3D>(ground, out var body) && !body->IsKinematic) {
        var r = characterPosition - platform->Position;
        velocity = body->Velocity + FPVector3.Cross(body->AngularVelocity, r);
        yawDelta = body->AngularVelocity.Y * dt * FP.Rad2Deg;
      } else {
        var deltaRotation = platform->Rotation * FPQuaternion.Inverse(p->PreviousRotation);
        var carried = platform->Position + deltaRotation * (characterPosition - p->PreviousPosition);
        velocity = (carried - characterPosition) / dt;
        yawDelta = YawOf(deltaRotation);
      }

      p->PreviousPosition = platform->Position;
      p->PreviousRotation = platform->Rotation;
      p->BaseVelocity = velocity;

      var maxYaw = config.Advanced.MaxPlatformYawSpeed * dt;
      p->YawDelta = FPMath.Clamp(yawDelta, -maxYaw, maxYaw);
    }

    /// <summary>Signed yaw (degrees) of a rotation, measured on the horizontal plane.</summary>
    static FP YawOf(FPQuaternion rotation) {
      var forward = rotation * FPVector3.Forward;
      return FPMath.Atan2(forward.X, forward.Z) * FP.Rad2Deg;
    }
  }
}
