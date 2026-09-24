namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;

  /// <summary>Phase 1: the character spawns from a config, stands still and can be pushed.</summary>
  [Collection("Quantum")]
  public unsafe class Phase1SkeletonTests {
    [Fact]
    public void Adding_PPCCharacter_Configures_Body_And_Capsule_From_Config() {
      var (config, assets) = PPCTestWorld.DefaultAssets(c => { c.Body.Mass = 3; c.Body.Layer = 5; });
      EntityRef e = default;
      using var s = new HeadlessSession(f => e = PPCTestWorld.SpawnCharacter(f, FPVector3.Zero),
                                        configureSystems: PPCSystems.AddTo, extraAssets: assets);
      s.Step(1);

      var body = s.Frame.Get<PhysicsBody3D>(e);
      var collider = s.Frame.Get<PhysicsCollider3D>(e);
      Assert.False(body.IsKinematic);
      Assert.Equal(RotationFreezeFlags.FreezeAll, body.RotationFreeze);
      Assert.Equal(FP._0, body.GravityScale);
      Assert.InRange(body.Mass.AsFloat, 2.999f, 3.001f); // stored as inverse mass
      Assert.Equal(5, collider.Layer);
      Assert.Equal(Shape3DType.Capsule, collider.Shape.Type);
      Assert.Equal(config.Body.Radius, collider.Shape.Capsule.Radius);
      Assert.Equal(config.HalfHeight(false), collider.Shape.Capsule.Extent + collider.Shape.Capsule.Radius);
      Assert.Equal(config.Body.Material, collider.Material);
    }

    [Fact]
    public void Character_Stands_Still_On_The_Floor() {
      var (_, assets) = PPCTestWorld.DefaultAssets();
      EntityRef e = default;
      using var s = new HeadlessSession(f => {
        PPCTestWorld.Floor(f, FP._0);
        e = PPCTestWorld.SpawnCharacter(f, FPVector3.Zero);
      }, configureSystems: PPCSystems.AddTo, extraAssets: assets);

      var start = s.Frame.Get<Transform3D>(e).Position;
      s.Step(120);

      var t = s.Frame.Get<Transform3D>(e);
      Assert.True(FPVector3.Distance(start, t.Position) < FP.FromString("0.01"), $"drifted to {t.Position}");
      Assert.Equal(FPQuaternion.Identity, t.Rotation);
    }

    [Fact]
    public void Player_Input_Reaches_The_Character_Through_The_Bridge() {
      var (_, assets) = PPCTestWorld.DefaultAssets();
      EntityRef e = default;
      using var s = new HeadlessSession(f => e = PPCTestWorld.SpawnCharacter(f, FPVector3.Zero, (PlayerRef)0),
        input: (tick, p) => PPCTestWorld.Move(FP._1, FP._1, run: true, yaw: 90, pitch: -10),
        configureSystems: PPCSystems.AddTo, extraAssets: assets);

      s.Step(3);

      var input = s.Frame.Get<PPCCharacter>(e).Input;
      Assert.True(input.Run);
      Assert.Equal((FP)90, input.LookYaw);
      Assert.Equal((FP)(-10), input.LookPitch);
      // (1, 1) is clamped to unit length
      Assert.InRange(input.Move.Magnitude.AsFloat, 0.99f, 1.01f);
    }

    [Fact]
    public void Heavy_Box_Hitting_The_Character_Moves_It() {
      var (_, assets) = PPCTestWorld.DefaultAssets();
      EntityRef e = default;
      using var s = new HeadlessSession(f => {
        e = PPCTestWorld.SpawnCharacter(f, FPVector3.Zero);
        var box = f.Create();
        f.Set(box, Transform3D.Create(new FPVector3(-3, 1, 0)));
        f.Set(box, PhysicsCollider3D.Create(f, Shape3D.CreateBox(FPVector3.One * FP._0_50)));
        var body = PhysicsBody3D.CreateDynamic(20);
        body.GravityScale = FP._0;
        body.Velocity = new FPVector3(10, 0, 0);
        f.Set(box, body);
      }, configureSystems: PPCSystems.AddTo, extraAssets: assets);

      s.Step(30);

      Assert.True(s.Frame.Get<Transform3D>(e).Position.X > FP._0, "character was not pushed");
    }
  }
}
