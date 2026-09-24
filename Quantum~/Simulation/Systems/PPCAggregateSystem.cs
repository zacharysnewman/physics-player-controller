namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Sums the velocity layers and drives the body to that velocity. Quantum equivalent of the Unity
  /// package's VelocityAggregator (<c>AddForce((target - v) / dt, Acceleration)</c>): writing the
  /// velocity before the physics step lets the solver resolve contacts, and any deviation seen next
  /// tick is treated as an external force by the layers.
  /// </summary>
  public unsafe class PPCAggregateSystem : PPCSystemBase {
    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      FPVector3 target;

      if (c->Climb.IsClimbing) {
        // Exclusive layer: climbing replaces the other layers entirely.
        target = c->Climb.Velocity;
      } else {
        target = c->Horizontal.Contribution + new FPVector3(0, c->Vertical.TargetY, 0);
      }

      c->TargetVelocity = target;
      filter.Body->Velocity = target;
    }
  }
}
