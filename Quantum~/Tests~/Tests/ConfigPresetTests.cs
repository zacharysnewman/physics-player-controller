namespace PPC.Tests {
  using System;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>The recommended defaults, the Unity parity preset, and crouch ignoring run.</summary>
  [Collection("Quantum")]
  public unsafe class ConfigPresetTests {
    /// <summary>Session with the package defaults (no parity preset).</summary>
    static HeadlessSession Recommended(Action<Frame> setup, Func<int, Quantum.Input> input) {
      var (config, assets) = DefaultAssets();
      config.ApplyRecommendedFeel();
      HarnessKickSystem.Schedule.Clear();
      HarnessKickSystem.Explosions.Clear();
      HarnessMoverSystem.Movers.Clear();
      return new HeadlessSession(setup, (t, p) => input(t),
        c => { PPCSystems.AddTo(c); c.AddSystem<HarnessKickSystem>(); }, extraAssets: assets);
    }

    [Fact]
    public void Defaults_Are_The_Recommended_Feel() {
      var fresh = new PPCConfig();
      var applied = new PPCConfig();
      applied.ApplyUnityParity();
      applied.ApplyRecommendedFeel();

      Assert.Equal((FP)8, fresh.Movement.RunSpeed);
      Assert.Equal((FP)50, fresh.Movement.Acceleration);
      Assert.Equal(FP._2, fresh.Body.GravityScale);
      Assert.Equal(FP.FromString("1.25"), fresh.Jump.Height);
      Assert.Equal(fresh.Movement.RunSpeed, applied.Movement.RunSpeed);
      Assert.Equal(fresh.Movement.AirControl, applied.Movement.AirControl);
      Assert.Equal(fresh.Movement.MaxStepHeight, applied.Movement.MaxStepHeight);
      Assert.Equal(fresh.Jump.Height, applied.Jump.Height);
      Assert.Equal(fresh.Body.GravityScale, applied.Body.GravityScale);
      Assert.Equal(fresh.Movement.AirExternalDrag, applied.Movement.AirExternalDrag);
      Assert.Equal(fresh.Crouch.Speed, applied.Crouch.Speed);
    }

    [Fact]
    public void Unity_Parity_Restores_The_Original_Numbers_And_Nothing_Else() {
      var c = new PPCConfig();
      c.Body.Radius = FP._0_25;
      c.ApplyUnityParity();

      Assert.Equal((FP)10, c.Movement.RunSpeed);
      Assert.Equal((FP)10, c.Movement.Acceleration);
      Assert.Equal((FP)20, c.Movement.ReverseDeceleration);
      Assert.Equal(FP._1, c.Movement.AirControl);
      Assert.Equal(FP._0_50, c.Movement.MaxStepHeight);
      Assert.Equal((FP)5, c.Jump.Height);
      Assert.Equal(FP._1, c.Body.GravityScale);
      Assert.Equal(FP._0_50, c.Movement.AirExternalDrag);
      Assert.Equal((FP)2, c.Crouch.Speed);
      Assert.Equal(FP._0_25, c.Body.Radius);   // untouched
    }

    [Fact]
    public void Recommended_Jump_Reaches_About_1_25_m() {
      EntityRef e = default;
      using var s = Recommended(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                t => Move(0, 0, jump: t >= 5 && t < 8));

      Assert.InRange(s.MaxFeet(e, PPCTest.Ticks(1.5f)), 1.15f, 1.3f);
    }

    [Fact]
    public void Recommended_Reaches_Walk_Speed_Quickly_And_Runs_At_8() {
      EntityRef e = default;
      using var s = Recommended(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                t => Move(0, 1, run: t >= 30));
      s.Step(9);   // 0.15 s at 50 m/s² → full walk speed after 0.1 s
      var walk = s.HorizontalSpeed(e);
      s.Step(60);

      Assert.InRange(walk, 4.95f, 5.05f);
      Assert.InRange(s.HorizontalSpeed(e), 7.95f, 8.05f);
    }

    [Fact]
    public void Crouching_Ignores_Run() {
      EntityRef e = default;
      using var s = Recommended(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                t => Move(0, 1, run: true, crouch: true));
      s.Step(60);

      Assert.True(s.Character(e).Crouch.IsCrouching);
      Assert.InRange(s.HorizontalSpeed(e), 1.55f, 1.65f);
    }

    [Fact]
    public void Recommended_Jump_Is_Snappy_With_Source_Gravity() {
      // 1.25 m at 20 m/s²: up in ~0.35 s, total airtime ~0.7 s (vs ~1 s at 10 m/s²).
      EntityRef e = default;
      using var s = Recommended(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                t => Move(0, 0, jump: t >= 5 && t < 8));
      s.Step(6);
      int airborne = 0;
      for (int i = 0; i < 90; i++) { s.Step(1); if (!s.Character(e).Ground.IsGrounded) airborne++; }

      Assert.InRange(airborne / 60f, 0.6f, 0.8f);
    }

    [Fact]
    public void Recommended_Keeps_Air_Momentum_From_Pushes() {
      EntityRef e = default;
      using var s = Recommended(f => e = SpawnCharacter(f, new FPVector3(0, 30, 0)), t => Move(0, 0));
      s.Step(1);
      HarnessKickSystem.Schedule.Add((3, e, new FPVector3(6, 0, 0)));
      s.Step(6);
      var early = s.Velocity(e).X.AsFloat;
      s.Step(30);

      // No air drag: the push is kept, except what the (low) air control brakes with no input.
      Assert.True(s.Velocity(e).X.AsFloat > early * 0.5f, $"{s.Velocity(e).X.AsFloat} vs {early}");
    }
  }
}
