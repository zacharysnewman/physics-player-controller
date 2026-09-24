namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Phase 6: ladder climbing.</summary>
  [Collection("Quantum")]
  public unsafe class Phase6ClimbTests {
    // Ladder: trigger 1 m wide, 0.2 m deep, from the floor to 3.2 m, centred on z = 1 and facing +Z.
    // A 3 m ledge starts right behind it (z ≥ 1.1). The character starts at z = 0 facing +Z.
    static void World(Frame f) {
      Floor(f, 0);
      Box(f, new FPVector3(0, FP.FromString("1.5"), FP.FromString("3.1")), new FPVector3(3, FP.FromString("1.5"), 2));
      Ladder(f, new FPVector3(0, FP.FromString("1.6"), 1), new FPVector3(FP._0_50, FP.FromString("1.6"), FP._0_10));
    }

    static HeadlessSession Session(out EntityRef e, System.Func<int, Quantum.Input> input) {
      EntityRef c = default;
      var s = PPCTest.Session(f => { World(f); c = SpawnCharacter(f, FPVector3.Zero); }, input);
      e = c;
      return s;
    }

    [Fact]
    public void Walking_Into_A_Ladder_Grabs_It_And_Holds_Height() {
      using var s = Session(out var e, t => t < 20 ? Move(0, 1) : Move(0, 0));
      var started = s.Count<EventPPCClimbStarted>();
      s.Step(30);
      var y = s.Position(e).Y;
      s.Step(60);

      Assert.Equal(1, started());
      Assert.True(s.Character(e).Climb.IsClimbing);
      Assert.Equal(PPCState.Climbing, s.Character(e).State);
      Assert.InRange((s.Position(e).Y - y).AsFloat, -0.02f, 0.02f);   // no gravity on the ladder
    }

    [Fact]
    public void Forward_Climbs_Up_At_Climb_Speed_And_Snaps_To_The_Face() {
      using var s = Session(out var e, t => Move(0, 1));
      s.Step(30);
      s.Step(30);

      Assert.True(s.Character(e).Climb.IsClimbing);
      Assert.InRange(s.Velocity(e).Y.AsFloat, 2.9f, 3.1f);
      Assert.InRange(s.Position(e).Z.AsFloat, 0.45f, 0.55f);   // 1 - (0.1 + 0.5 - 0.1)
    }

    [Fact]
    public void Looking_Down_Past_The_Threshold_Climbs_Down() {
      // Climb up for 0.8 s, then look down 45° and keep pushing forward.
      using var s = Session(out var e, t => t < 48 ? Move(0, 1) : Move(0, 1, pitch: -45));
      s.Step(58);

      Assert.True(s.Character(e).Climb.IsClimbing);
      Assert.InRange(s.Velocity(e).Y.AsFloat, -3.1f, -2.9f);
    }

    [Fact]
    public void Glancing_Down_Slightly_Still_Climbs_Up() {
      // Unity flipped at exactly level; a 10° glance down reversed the climb.
      using var s = Session(out var e, t => Move(0, 1, pitch: t < 20 ? 0 : -10));
      s.Step(50);

      Assert.InRange(s.Velocity(e).Y.AsFloat, 2.9f, 3.1f);
    }

    [Fact]
    public void Strafing_Moves_Along_The_Ladder() {
      using var s = Session(out var e, t => t < 25 ? Move(0, 1) : Move(1, 0));
      s.Step(32);   // the ladder is 1 m wide: strafing slides off its side after ~0.3 s

      Assert.True(s.Character(e).Climb.IsClimbing);
      Assert.InRange(s.Velocity(e).X.AsFloat, 2.9f, 3.1f);
    }

    [Fact]
    public void Jumping_Off_Launches_Away_And_Does_Not_Regrab() {
      using var s = Session(out var e, t => Move(0, t < 40 ? FP._1 : FP._0, jump: t >= 40 && t < 42));
      var ended = s.Count<EventPPCClimbEnded>();
      var started = s.Count<EventPPCClimbStarted>();
      s.Step(43);

      Assert.False(s.Character(e).Climb.IsClimbing);
      Assert.Equal(1, ended());
      var v = s.Velocity(e);
      Assert.True(v.Z < -2, $"away from the ladder: {v}");   // JumpOffVelocity.Z = 3 away (-Z here)
      Assert.True(v.Y > 3, $"upwards: {v}");                  // JumpOffVelocity.Y = 4

      s.Step(60);
      Assert.Equal(1, started());
    }

    [Fact]
    public void Climbing_Down_To_The_Floor_Lets_Go() {
      // Climb up 0.5 s, then climb down (back input) until the feet touch the floor.
      using var s = Session(out var e, t => t < 50 ? Move(0, 1) : Move(0, -1));
      var ended = s.Count<EventPPCClimbEnded>();
      s.Step(50 + PPCTest.Ticks(1.5f));

      Assert.Equal(1, ended());
      Assert.False(s.Character(e).Climb.IsClimbing);
      Assert.True(s.Character(e).Ground.IsGrounded);
    }

    [Fact]
    public void Climbing_Over_The_Top_Lands_On_The_Ledge() {
      using var s = Session(out var e, t => Move(0, 1));
      s.Step(PPCTest.Ticks(3f));

      Assert.False(s.Character(e).Climb.IsClimbing);
      Assert.True(s.Character(e).Ground.IsGrounded);
      Assert.InRange(s.Feet(e), 2.97f, 3.05f);
      Assert.True(s.Position(e).Z > FP._2, "walked onto the ledge");
    }
  }
}
