namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Phase 5: moving platforms and external forces.</summary>
  [Collection("Quantum")]
  public unsafe class Phase5PlatformTests {
    static readonly FPVector3 PlatformHalf = new FPVector3(3, FP._0_50, 3);

    /// <summary>Kinematic platform moved by script (Quantum doesn't move kinematic bodies by velocity);
    /// its velocity is also set so collisions respond correctly.</summary>
    static EntityRef KinematicPlatform(Frame f, FPVector3 topCenter, FPVector3 velocity, FP yawRate = default, FPVector3 halfExtents = default) {
      var e = Box(f, topCenter - FPVector3.Up * FP._0_50, halfExtents == default ? PlatformHalf : halfExtents);
      var body = f.Unsafe.GetPointer<PhysicsBody3D>(e);
      body->Velocity = velocity;
      body->AngularVelocity = new FPVector3(0, yawRate * FP.Deg2Rad, 0);
      HarnessMoverSystem.Movers[e] = (velocity, yawRate);
      return e;
    }

    static FPVector3 Offset(HeadlessSession s, EntityRef e, EntityRef platform) =>
      s.Position(e) - s.Frame.Get<Transform3D>(platform).Position;

    [Fact]
    public void Rides_A_Moving_Kinematic_Platform() {
      EntityRef e = default, platform = default;
      using var s = PPCTest.Session(f => {
        platform = KinematicPlatform(f, FPVector3.Zero, new FPVector3(3, 0, 0));
        e = SpawnCharacter(f, FPVector3.Zero);
      });
      s.Step(PPCTest.Ticks(0.5f));   // catch up with the platform (deceleration rate, like Unity)
      var settled = Offset(s, e, platform);
      s.Step(PPCTest.Ticks(2f));

      Assert.True(s.Frame.Get<Transform3D>(platform).Position.X > 5, "platform moved");
      Assert.InRange((Offset(s, e, platform) - settled).Magnitude.AsFloat, 0f, 0.03f);
      Assert.True(s.Character(e).Ground.IsGrounded);
    }

    [Fact]
    public void Rides_A_Script_Moved_Platform_Without_A_Body() {
      EntityRef e = default, platform = default;
      using var s = PPCTest.Session(f => {
        platform = ScriptedBox(f, new FPVector3(0, -FP._0_50, 0), PlatformHalf);
        HarnessMoverSystem.Movers[platform] = (new FPVector3(0, 0, 2), FP._0);
        e = SpawnCharacter(f, FPVector3.Zero);
      });
      s.Step(PPCTest.Ticks(0.5f));
      var settled = Offset(s, e, platform);
      s.Step(PPCTest.Ticks(2f));

      Assert.InRange((Offset(s, e, platform) - settled).Magnitude.AsFloat, 0f, 0.03f);
    }

    [Fact]
    public void Rides_A_Dynamic_Platform_Using_Its_Body_Velocity() {
      EntityRef e = default, platform = default;
      using var s = PPCTest.Session(f => {
        platform = f.Create();
        f.Set(platform, Transform3D.Create(new FPVector3(0, -FP._0_50, 0)));
        f.Set(platform, PhysicsCollider3D.Create(f, Shape3D.CreateBox(PlatformHalf)));
        var body = PhysicsBody3D.CreateDynamic(1000);
        body.GravityScale = FP._0;
        body.RotationFreeze = RotationFreezeFlags.FreezeAll;
        body.Velocity = new FPVector3(2, 0, 0);
        f.Set(platform, body);
        e = SpawnCharacter(f, FPVector3.Zero);
      });
      s.Step(PPCTest.Ticks(0.5f));
      var settled = Offset(s, e, platform);
      s.Step(PPCTest.Ticks(2f));

      Assert.True(s.Frame.Get<Transform3D>(platform).Position.X > 4, "platform moved");
      Assert.InRange((Offset(s, e, platform) - settled).Magnitude.AsFloat, 0f, 0.05f);
      Assert.InRange(s.Character(e).Platform.BaseVelocity.X.AsFloat, 1.9f, 2.1f);
    }

    [Fact]
    public void Walking_On_A_Moving_Platform_Is_Relative_To_It() {
      EntityRef e = default, platform = default;
      using var s = PPCTest.Session(f => {
        platform = KinematicPlatform(f, FPVector3.Zero, new FPVector3(0, 0, 3), halfExtents: new FPVector3(10, FP._0_50, 10));
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(1, 0));   // walk sideways (+X)
      s.Step(PPCTest.Ticks(0.8f));

      var v = s.Velocity(e);
      Assert.InRange(v.X.AsFloat, 4.9f, 5.1f);   // walk speed relative to the platform
      Assert.InRange(v.Z.AsFloat, 2.9f, 3.1f);   // plus the platform's own velocity
    }

    [Fact]
    public void Carried_Around_A_Rotating_Platform() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        KinematicPlatform(f, FPVector3.Zero, FPVector3.Zero, yawRate: 90, halfExtents: new FPVector3(5, FP._0_50, 5));
        e = SpawnCharacter(f, new FPVector3(0, 0, 2));
      });
      s.Step(3);
      var yawDelta = s.Character(e).Platform.YawDelta;
      s.Step(PPCTest.Ticks(1f) - 3);

      var p = s.Position(e);
      var radius = new FPVector3(p.X, 0, p.Z).Magnitude.AsFloat;
      Assert.InRange(radius, 1.85f, 2.15f);       // stays on its circle
      Assert.True(p.X > FP._1, $"carried round to {p}");   // 90° about +Y takes (0,0,2) to (2,0,0)
      Assert.InRange(yawDelta.AsFloat, 1.4f, 1.6f);   // 90°/s · 1/60 s
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-2)]
    public void Rides_An_Elevator_Staying_Grounded(int speed) {
      EntityRef e = default, lift = default;
      using var s = PPCTest.Session(f => {
        Floor(f, -30);
        lift = KinematicPlatform(f, FPVector3.Zero, new FPVector3(0, speed, 0));
        e = SpawnCharacter(f, FPVector3.Zero);
      });
      int ungrounded = 0;
      for (int i = 0; i < PPCTest.Ticks(2f); i++) {
        s.Step(1);
        if (!s.Character(e).Ground.IsGrounded) ungrounded++;
      }

      var liftTop = (s.Frame.Get<Transform3D>(lift).Position.Y + FP._0_50).AsFloat;
      Assert.InRange(s.Feet(e) - liftTop, -0.03f, 0.05f);
      Assert.True(ungrounded <= 1, $"lost ground {ungrounded} ticks");
    }

    [Fact]
    public void Jumping_Off_A_Moving_Platform_Keeps_Its_Momentum() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        KinematicPlatform(f, FPVector3.Zero, new FPVector3(4, 0, 0));
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 0, jump: t >= 40 && t < 42));
      s.Step(44);   // two ticks after takeoff

      Assert.False(s.Character(e).Ground.IsGrounded);
      // Keeps the platform's 4 m/s; without input it then decelerates at Movement.Deceleration (as in Unity).
      Assert.InRange(s.Velocity(e).X.AsFloat, 3.5f, 4.05f);
    }

    [Fact]
    public void Explosion_Pushes_Nearby_Characters_Only() {
      EntityRef near = default, far = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        near = SpawnCharacter(f, new FPVector3(2, 0, 0), player: PlayerRef.None);
        far = SpawnCharacter(f, new FPVector3(20, 0, 0), player: PlayerRef.None);
      });
      s.Step(2);
      HarnessKickSystem.Explosions.Add((5, new FPVector3(0, 1, 0), (FP)6, (FP)12, FP._0_50));
      s.Step(12);

      Assert.True(s.Position(near).X > FP._2 + FP._0_50, "near character pushed away");
      Assert.InRange(s.Position(far).X.AsFloat, 19.98f, 20.02f);
    }
  }
}
