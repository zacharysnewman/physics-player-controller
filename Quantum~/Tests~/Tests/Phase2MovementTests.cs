namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Phase 2: probes and horizontal movement (no gravity yet; characters start on the floor).</summary>
  [Collection("Quantum")]
  public unsafe class Phase2MovementTests {
    // ---------- Probes ----------

    [Fact]
    public void Standing_On_Floor_Is_Grounded_With_Up_Normal() {
      EntityRef e = default, floor = default;
      using var s = PPCTest.Session(f => { floor = Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); });
      s.Step(2);

      var g = s.Character(e).Ground;
      Assert.True(g.IsGrounded);
      Assert.Equal(floor, g.Entity);
      Assert.True(g.SlopeAngle < FP._1);
    }

    [Fact]
    public void Floating_High_Above_Floor_Is_Not_Grounded() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, new FPVector3(0, 3, 0)); });
      s.Step(2);

      Assert.False(s.Character(e).Ground.IsGrounded);
    }

    [Fact]
    public void Too_Steep_Ground_Is_Not_Walkable() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        var ramp = Box(f, new FPVector3(0, -1, 0), new FPVector3(5, FP._0_50, 5));
        f.Unsafe.GetPointer<Transform3D>(ramp)->Rotation = FPQuaternion.Euler(0, 0, 60);
        e = SpawnCharacter(f, new FPVector3(0, FP._0_50, 0));
      });
      s.Step(2);

      var g = s.Character(e).Ground;
      Assert.False(g.IsGrounded);
      Assert.InRange(g.SlopeAngle.AsFloat, 55f, 65f);
    }

    [Fact]
    public void Low_Ceiling_Is_Detected() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Box(f, new FPVector3(0, FP.FromString("2.55"), 0), new FPVector3(2, FP._0_50, 2)); // underside at 2.05 m
        e = SpawnCharacter(f, FPVector3.Zero);
      });
      s.Step(2);

      Assert.True(s.Character(e).Ground.IsCeilingBlocked);
    }

    [Fact]
    public void Adjacent_Wall_Is_Detected() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Box(f, new FPVector3(FP.FromString("1.1"), 1, 0), new FPVector3(FP._0_50, 2, 2)); // face at x = 0.6
        e = SpawnCharacter(f, FPVector3.Zero);
      });
      s.Step(2);

      var g = s.Character(e).Ground;
      Assert.True(g.IsTouchingWall);
      Assert.True(g.WallNormal.X < FP._0);
    }

    // ---------- Walking ----------

    [Fact]
    public void Walk_Accelerates_To_Walk_Speed_Forward() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); }, input: t => Move(0, 1));

      s.Step(PPCTest.Ticks(0.25f));
      var early = s.HorizontalSpeed(e);
      s.Step(PPCTest.Ticks(1f));

      Assert.InRange(early, 2f, 3f);                 // 10 m/s² for 0.25 s
      Assert.InRange(s.HorizontalSpeed(e), 4.9f, 5.1f);
      Assert.True(s.Position(e).Z > 3, "moves along +Z at yaw 0");
      Assert.Equal(PPCState.Walking, s.Character(e).State);
    }

    [Fact]
    public void Run_Reaches_Run_Speed() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); }, input: t => Move(0, 1, run: true));
      s.Step(PPCTest.Ticks(1.5f));

      Assert.InRange(s.HorizontalSpeed(e), 9.9f, 10.1f);
      Assert.Equal(PPCState.Running, s.Character(e).State);
    }

    [Fact]
    public void Movement_Is_Relative_To_Camera_Yaw() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); }, input: t => Move(0, 1, yaw: 90));
      s.Step(PPCTest.Ticks(1f));

      var p = s.Position(e);
      Assert.True(p.X > 2, $"expected +X at yaw 90, got {p}");
      Assert.InRange(p.Z.AsFloat, -0.05f, 0.05f);
    }

    [Fact]
    public void Releasing_Input_Decelerates_To_Stop() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => t < PPCTest.Ticks(1f) ? Move(0, 1) : Move(0, 0));
      s.Step(PPCTest.Ticks(1f) + PPCTest.Ticks(0.6f));

      Assert.True(s.HorizontalSpeed(e) < 0.05f);
      Assert.Equal(PPCState.Idle, s.Character(e).State);
    }

    [Fact]
    public void Reversing_Brakes_Faster_Than_Releasing() {
      float SpeedAfterBraking(Quantum.Input brake) {
        EntityRef e = default;
        using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                      input: t => t < PPCTest.Ticks(1f) ? Move(0, 1) : brake);
        s.Step(PPCTest.Ticks(1f) + PPCTest.Ticks(0.1f));
        return s.Velocity(e).Z.AsFloat;
      }

      var released = SpeedAfterBraking(Move(0, 0));   // 10 m/s² → ~4 m/s left
      var reversed = SpeedAfterBraking(Move(0, -1));   // 20 m/s² → ~3 m/s left

      Assert.True(reversed < released - 0.5f, $"reverse {reversed} vs release {released}");
    }

    [Fact]
    public void Wall_Blocks_Movement() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Box(f, new FPVector3(0, 1, 3), new FPVector3(2, 2, FP._0_50)); // face at z = 2.5
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 1));
      s.Step(PPCTest.Ticks(2f));

      Assert.InRange(s.Position(e).Z.AsFloat, 1.8f, 2.05f); // capsule radius 0.5 stops it at ~2.0
    }

    [Fact]
    public void Diagonal_Input_Is_Not_Faster() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); }, input: t => Move(1, 1));
      s.Step(PPCTest.Ticks(1.5f));

      Assert.InRange(s.HorizontalSpeed(e), 4.9f, 5.1f);
    }

    // ---------- Steps and air ----------

    [Fact]
    public void Walks_Up_A_Low_Step() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Box(f, new FPVector3(0, FP.FromString("0.15"), 12), new FPVector3(3, FP.FromString("0.15"), 10)); // 0.3 m step from z = 2
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 1));
      s.Step(PPCTest.Ticks(2f));

      var feet = s.Position(e).Y - 1;
      Assert.True(s.Position(e).Z > 3, "passed the step edge");
      Assert.InRange(feet.AsFloat, 0.28f, 0.35f);
    }

    [Fact]
    public void Tall_Step_Blocks() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Box(f, new FPVector3(0, FP.FromString("0.4"), 12), new FPVector3(3, FP.FromString("0.4"), 10)); // 0.8 m
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 1));
      s.Step(PPCTest.Ticks(2f));

      Assert.True(s.Position(e).Z < FP.FromString("1.6"));
    }

    [Fact]
    public void Air_Control_Scales_Airborne_Acceleration() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => e = SpawnCharacter(f, new FPVector3(0, 10, 0)),
                                    input: t => Move(0, 1), tweak: c => c.Movement.AirControl = FP._0_50);
      s.Step(PPCTest.Ticks(0.25f));

      Assert.InRange(s.HorizontalSpeed(e), 1f, 1.5f); // half of the ~2.5 m/s it'd have on the ground
    }

    // ---------- External forces ----------

    [Fact]
    public void Ground_Kick_Is_Absorbed_Then_Decays_By_Friction() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); });
      s.Step(1);
      HarnessKickSystem.Schedule.Add((5, e, new FPVector3(6, 0, 0)));

      s.Step(8);
      var afterKick = s.Velocity(e).X.AsFloat;
      s.Step(PPCTest.Ticks(0.5f));

      Assert.InRange(afterKick, 4f, 6.1f);              // absorbed, not cancelled
      Assert.True(s.Velocity(e).X.AsFloat < 0.1f, "friction (15 m/s²) stops it within ~0.4 s");
      Assert.True(s.Position(e).X > FP._0_50);
    }

    [Fact]
    public void Air_Kick_Decays_Slowly() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => e = SpawnCharacter(f, new FPVector3(0, 10, 0)));
      s.Step(1);
      HarnessKickSystem.Schedule.Add((5, e, new FPVector3(6, 0, 0)));

      s.Step(8 + HeadlessSession.UpdateFps);

      // exp(-0.5 · 1 s) ≈ 0.61 of 6 m/s
      Assert.InRange(s.Velocity(e).X.AsFloat, 3.3f, 3.9f);
    }
  }
}
