namespace Quantum {
  using Photon.Deterministic;

  // Harness smoke test: exercises the generated component and the core ECS/physics API surface
  // so a broken SDK path or CodeGen step fails loudly. Not part of the shipped package.
  public unsafe class HarnessSmokeSystem : SystemMainThreadFilter<HarnessSmokeSystem.Filter> {
    public struct Filter {
      public EntityRef Entity;
      public Transform3D* Transform;
      public PPCHarnessSmoke* Smoke;
    }

    public override void Update(Frame f, ref Filter filter) {
      filter.Smoke->Velocity += FPVector3.Forward * filter.Smoke->Speed * f.DeltaTime;
      filter.Transform->Position += filter.Smoke->Velocity * f.DeltaTime;

      if (f.Unsafe.TryGetPointer<PhysicsBody3D>(filter.Entity, out var body)) {
        body->AddLinearImpulse(FPVector3.Zero);
      }
    }
  }
}
