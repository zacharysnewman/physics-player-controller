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
      return new HeadlessSession(setup, (t, p) => input(t), PPCSystems.AddTo, extraAssets: assets);
    }

    [Fact]
    public void Defaults_Are_The_Recommended_Feel() {
      var fresh = new PPCConfig();
      var applied = new PPCConfig();
      applied.ApplyUnityParity();
      applied.ApplyRecommendedFeel();

      Assert.Equal((FP)7, fresh.Movement.RunSpeed);
      Assert.Equal((FP)40, fresh.Movement.Acceleration);
      Assert.Equal(FP.FromString("1.25"), fresh.Jump.Height);
      Assert.Equal(fresh.Movement.RunSpeed, applied.Movement.RunSpeed);
      Assert.Equal(fresh.Movement.AirControl, applied.Movement.AirControl);
      Assert.Equal(fresh.Movement.MaxStepHeight, applied.Movement.MaxStepHeight);
      Assert.Equal(fresh.Jump.Height, applied.Jump.Height);
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
    public void Recommended_Reaches_Walk_Speed_Quickly_And_Runs_At_7() {
      EntityRef e = default;
      using var s = Recommended(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                t => Move(0, 1, run: t >= 30));
      s.Step(9);   // 0.15 s at 40 m/s² → full walk speed after 0.125 s
      var walk = s.HorizontalSpeed(e);
      s.Step(60);

      Assert.InRange(walk, 4.95f, 5.05f);
      Assert.InRange(s.HorizontalSpeed(e), 6.95f, 7.05f);
    }

    [Fact]
    public void Crouching_Ignores_Run() {
      EntityRef e = default;
      using var s = Recommended(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                t => Move(0, 1, run: true, crouch: true));
      s.Step(60);

      Assert.True(s.Character(e).Crouch.IsCrouching);
      Assert.InRange(s.HorizontalSpeed(e), 1.95f, 2.05f);
    }
  }
}
