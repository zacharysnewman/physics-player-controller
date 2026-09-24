namespace Quantum {
  using System.Collections.Generic;
  using Photon.Deterministic;

  /// <summary>
  /// Moves entities by script (translation + yaw rate), like an animated platform with no physics body.
  /// Registered before the controller systems. Not part of the shipped package.
  /// </summary>
  public unsafe class HarnessMoverSystem : SystemMainThread {
    public static readonly Dictionary<EntityRef, (FPVector3 velocity, FP yawDegreesPerSecond)> Movers =
      new Dictionary<EntityRef, (FPVector3, FP)>();

    public override void Update(Frame f) {
      foreach (var pair in Movers) {
        if (!f.Unsafe.TryGetPointer<Transform3D>(pair.Key, out var t)) continue;
        t->Position += pair.Value.velocity * f.DeltaTime;
        t->Rotation = FPQuaternion.Euler(0, pair.Value.yawDegreesPerSecond * f.DeltaTime, 0) * t->Rotation;
      }
    }
  }
}
