namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;

  /// <summary>
  /// Pins the Quantum physics API the port relies on (PLAN.md, Phase 0 "Verify API names"), and
  /// checks it behaves as the plan assumes. Fails to compile or run if a future SDK changes it.
  /// </summary>
  [Collection("Quantum")]
  public unsafe class PhysicsApiTests {
    static readonly FP CapsuleRadius = FP._0_50;
    static readonly FP StandingExtent = FP._0_50;   // Quantum capsules: radius + extent (not height)
    static readonly FP CrouchingExtent = FP._0_10;

    static EntityRef SpawnCharacterBody(Frame f, FPVector3 position) {
      var e = f.Create();
      f.Set(e, Transform3D.Create(position));
      f.Set(e, PhysicsCollider3D.Create(f, Shape3D.CreateCapsule(CapsuleRadius, StandingExtent)));

      var body = PhysicsBody3D.CreateDynamic(FP._1);
      body.RotationFreeze = RotationFreezeFlags.FreezeAll;
      body.GravityScale = FP._0;
      f.Set(e, body);
      return e;
    }

    [Fact]
    public void Frozen_Zero_Gravity_Body_Holds_Still() {
      EntityRef e = default;
      using var s = new HeadlessSession(f => e = SpawnCharacterBody(f, new FPVector3(0, 5, 0)));

      s.Step(60);

      var t = s.Frame.Get<Transform3D>(e);
      var body = s.Frame.Get<PhysicsBody3D>(e);
      Assert.Equal(new FPVector3(0, 5, 0), t.Position);
      Assert.Equal(FPQuaternion.Identity, t.Rotation);
      Assert.False(body.IsKinematic);
    }

    [Fact]
    public void Velocity_Write_And_Impulse_Move_The_Body() {
      EntityRef e = default;
      using var s = new HeadlessSession(
        f => e = SpawnCharacterBody(f, FPVector3.Zero),
        configureSystems: c => c.AddSystem<DriveBodySystem>());

      s.Step(60);

      // DriveBodySystem writes Velocity.X = 1 every tick and applies one +Z impulse.
      var t = s.Frame.Get<Transform3D>(e);
      Assert.InRange(t.Position.X.AsFloat, 0.9f, 1.1f);
      Assert.True(t.Position.Z > FP._0);
      Assert.Equal(FPQuaternion.Identity, t.Rotation);
    }

    [Fact]
    public void Capsule_Can_Be_Resized_At_Runtime() {
      EntityRef e = default;
      using var s = new HeadlessSession(
        f => e = SpawnCharacterBody(f, new FPVector3(0, 5, 0)),
        configureSystems: c => c.AddSystem<CrouchShapeSystem>());

      s.Step(5);

      var collider = s.Frame.Get<PhysicsCollider3D>(e);
      Assert.Equal(Shape3DType.Capsule, collider.Shape.Type);
      Assert.Equal(CrouchingExtent, collider.Shape.Capsule.Extent);
      Assert.Equal(CapsuleRadius, collider.Shape.Capsule.Radius);
    }

    public class DriveBodySystem : SystemMainThreadFilter<DriveBodySystem.Filter> {
      public struct Filter {
        public EntityRef Entity;
        public PhysicsBody3D* Body;
      }

      public override void Update(Frame f, ref Filter filter) {
        filter.Body->Velocity.X = FP._1;
        if (filter.Body->Velocity.Z == FP._0) { // only true before the first impulse lands
          filter.Body->AddLinearImpulse(FPVector3.Forward);
        }
      }
    }

    public class CrouchShapeSystem : SystemMainThreadFilter<CrouchShapeSystem.Filter> {
      public struct Filter {
        public EntityRef Entity;
        public PhysicsCollider3D* Collider;
        public PhysicsBody3D* Body;
      }

      public override void Update(Frame f, ref Filter filter) {
        if (filter.Collider->Shape.Capsule.Extent == CrouchingExtent) {
          return;
        }
        filter.Collider->Shape = Shape3D.CreateCapsule(CapsuleRadius, CrouchingExtent);
        // Order matters (Quantum docs): centre of mass first, then inertia.
        filter.Body->ResetCenterOfMass(f, filter.Entity);
        filter.Body->ResetInertia(f, filter.Entity);
      }
    }
  }
}
