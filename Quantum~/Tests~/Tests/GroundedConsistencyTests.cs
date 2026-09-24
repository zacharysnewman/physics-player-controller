namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>
  /// Review item 12: every system agrees on "grounded". Taking off by jumping (or a launch) ends
  /// grounding for everyone on that tick, not a couple of ticks later when the probe (which reaches
  /// 0.15 m below the feet) loses the floor.
  /// </summary>
  [Collection("Quantum")]
  public unsafe class GroundedConsistencyTests {
    [Fact]
    public void State_Is_Jumping_From_The_Takeoff_Tick() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: t == 10));
      var jumps = s.Count<EventPPCJumped>();
      s.Step(10);
      Assert.Equal(0, jumps());

      s.Step(1);   // tick 10: the jump happens
      Assert.Equal(1, jumps());
      Assert.False(s.Character(e).Ground.IsGrounded);
      Assert.Equal(PPCState.Jumping, s.Character(e).State);
    }

    [Fact]
    public void Jumping_Off_A_Moving_Platform_Stops_Its_Carry_At_Once() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        var platform = Box(f, new FPVector3(0, -FP._0_50, 0), new FPVector3(20, FP._0_50, 20));
        HarnessMoverSystem.Movers[platform] = (new FPVector3(3, 0, 0), FP._0);
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 0, jump: t == 60));
      s.Step(61);   // takeoff tick: the jump inherits the platform's motion
      Assert.InRange(s.Character(e).Platform.BaseVelocity.X.AsFloat, 2.9f, 3.1f);

      s.Step(1);    // the next tick: airborne, so no more carry
      Assert.Equal(FPVector3.Zero, s.Character(e).Platform.BaseVelocity);
    }
  }
}
