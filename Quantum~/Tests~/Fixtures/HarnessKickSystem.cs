namespace Quantum {
  using System.Collections.Generic;
  using Photon.Deterministic;

  /// <summary>
  /// Applies scheduled velocity changes ("kicks": explosions, launch pads) to entities. Registered after
  /// the controller's systems, so the kick lands in the next physics step like an external force.
  /// Not part of the shipped package.
  /// </summary>
  public unsafe class HarnessKickSystem : SystemMainThread {
    public static readonly List<(int tick, EntityRef entity, FPVector3 deltaV)> Schedule = new List<(int, EntityRef, FPVector3)>();
    static int _startTick = -1;

    public override void OnInit(Frame f) {
      _startTick = -1;
    }

    public override void Update(Frame f) {
      if (_startTick < 0) {
        _startTick = f.Number;
      }
      var tick = f.Number - _startTick;
      foreach (var (t, entity, deltaV) in Schedule) {
        if (t == tick && f.Unsafe.TryGetPointer<PhysicsBody3D>(entity, out var body)) {
          body->Velocity += deltaV;
        }
      }
    }
  }
}
