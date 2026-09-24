namespace PPC.Tests {
  using System;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Standing on another character's head: solid, but not a moving platform (unless opted in).</summary>
  [Collection("Quantum")]
  public unsafe class CharacterStackingTests {
    // Player 0 stands on the floor; player 1 stands on player 0's head. Player 0 walks sideways.
    static HeadlessSession Stack(bool carried, out EntityRef bottom, out EntityRef top) {
      var (_, assets) = DefaultAssets(c => c.Movement.CarriedByCharacters = carried);
      EntityRef b = default, t = default;
      var s = new HeadlessSession(f => {
        Floor(f, 0);
        b = SpawnCharacter(f, FPVector3.Zero, 0);
        t = SpawnCharacter(f, new FPVector3(0, 2, 0), 1);
      },
      input: (tick, player) => player == 0 && tick >= 30 ? Move(1, 0) : Move(0, 0),
      configureSystems: PPCSystems.AddTo, playerCount: 2, extraAssets: assets);
      bottom = b;
      top = t;
      return s;
    }

    [Fact]
    public void Standing_On_A_Head_Is_Grounded() {
      using var s = Stack(false, out var bottom, out var top);
      s.Step(25);

      Assert.True(s.Character(top).Ground.IsGrounded);
      Assert.Equal(bottom, s.Character(top).Ground.Entity);
      Assert.InRange(s.Feet(top), 1.97f, 2.05f);
    }

    [Fact]
    public void The_Character_Below_Walks_Out_From_Under_You() {
      using var s = Stack(false, out var bottom, out var top);
      s.Step(30 + 30);   // bottom walks for 0.5 s

      Assert.True(s.Position(bottom).X > FP._0_50, "bottom moved");
      Assert.InRange(s.Position(top).X.AsFloat, -0.05f, 0.05f);   // top wasn't carried
    }

    [Fact]
    public void Opting_In_Carries_You_Along() {
      using var s = Stack(true, out var bottom, out var top);
      s.Step(30 + 30);

      Assert.True(s.Position(top).X > FP._0_50, "carried with the character below");
    }
  }
}
