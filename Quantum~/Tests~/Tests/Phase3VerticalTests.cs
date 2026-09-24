namespace PPC.Tests {
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Phase 3: gravity, jumping (buffer, coyote), ceilings, launches, slopes.</summary>
  [Collection("Quantum")]
  public unsafe class Phase3VerticalTests {
    // Default config: jump velocity 10 m/s, gravity -10 → apex 5 m.

    [Fact]
    public void Falls_And_Lands_On_The_Floor() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, new FPVector3(0, 3, 0)); });
      var landed = s.Count<EventPPCLanded>();

      s.Step(5);
      Assert.Equal(PPCState.Falling, s.Character(e).State);
      s.Step(PPCTest.Ticks(1.5f));

      Assert.True(s.Character(e).Ground.IsGrounded);
      Assert.InRange((s.Position(e).Y - 1).AsFloat, -0.02f, 0.05f);
      Assert.Equal(PPCState.Idle, s.Character(e).State);
      Assert.Equal(1, landed());
    }

    [Fact]
    public void Jump_Reaches_Expected_Apex() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: t >= 5 && t < 8));
      var jumps = s.Count<EventPPCJumped>();

      var apex = s.MaxFeet(e, PPCTest.Ticks(2.5f));

      Assert.InRange(apex, 4.7f, 5.2f);   // v²/2g = 5 m
      Assert.Equal(1, jumps());
      Assert.True(s.Character(e).Ground.IsGrounded, "landed again");
    }

    [Fact]
    public void Holding_Jump_Does_Not_Bunny_Hop() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: t >= 5));
      var jumps = s.Count<EventPPCJumped>();

      s.Step(PPCTest.Ticks(5f));

      Assert.Equal(1, jumps());
    }

    [Fact]
    public void Buffered_Press_Just_Before_Landing_Jumps() {
      // Falls 1.25 m from rest: lands at t = 0.5 s. Press at 0.4 s (0.1 s early, buffer is 0.2 s).
      EntityRef e = default;
      int press = PPCTest.Ticks(0.4f);
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, new FPVector3(0, FP.FromString("1.25"), 0)); },
                                    input: t => Move(0, 0, jump: t >= press && t < press + 2));
      var jumps = s.Count<EventPPCJumped>();

      s.Step(PPCTest.Ticks(1f));

      Assert.Equal(1, jumps());
    }

    [Fact]
    public void Press_Too_Early_Is_Forgotten() {
      EntityRef e = default;
      int press = PPCTest.Ticks(0.15f);   // 0.35 s before landing
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, new FPVector3(0, FP.FromString("1.25"), 0)); },
                                    input: t => Move(0, 0, jump: t >= press && t < press + 2));
      var jumps = s.Count<EventPPCJumped>();

      s.Step(PPCTest.Ticks(1f));

      Assert.Equal(0, jumps());
    }

    static HeadlessSession LedgeRun(int jumpTick, out EntityRef e) {
      // Platform ends at z = 2; running at 5 m/s. Character's centre passes the edge around t ≈ 0.9 s.
      EntityRef c = default;
      var s = PPCTest.Session(f => {
        Box(f, new FPVector3(0, -FP._0_50, -8), new FPVector3(5, FP._0_50, 10));
        Floor(f, -20);
        c = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 1, jump: jumpTick >= 0 && t >= jumpTick && t < jumpTick + 2));
      e = c;
      return s;
    }

    static int TickWhenUngrounded(int jumpTick = -1) {
      using var s = LedgeRun(jumpTick, out var e);
      for (int i = 0; i < 300; i++) {
        s.Step(1);
        if (!s.Character(e).Ground.IsGrounded) return i + 1;
      }
      return -1;
    }

    [Fact]
    public void Coyote_Jump_Works_Just_After_Walking_Off() {
      int off = TickWhenUngrounded();
      Assert.True(off > 0);

      using var s = LedgeRun(off + PPCTest.Ticks(0.05f), out var e);
      var jumps = s.Count<EventPPCJumped>();
      s.Step(off + PPCTest.Ticks(0.2f));

      Assert.Equal(1, jumps());
      Assert.True(s.Velocity(e).Y > 5, "moving up after the coyote jump");
    }

    [Fact]
    public void Coyote_Time_Expires() {
      int off = TickWhenUngrounded();

      using var s = LedgeRun(off + PPCTest.Ticks(0.25f), out var e);
      var jumps = s.Count<EventPPCJumped>();
      s.Step(off + PPCTest.Ticks(0.5f));

      Assert.Equal(0, jumps());
    }

    [Fact]
    public void Second_Press_Right_After_A_Jump_Is_Not_A_Double_Jump() {
      // Unity bug: jumping started coyote time, so a quick second press jumped again.
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: (t >= 5 && t < 7) || (t >= 12 && t < 14)));
      var jumps = s.Count<EventPPCJumped>();

      var apex = s.MaxFeet(e, PPCTest.Ticks(2f));

      Assert.Equal(1, jumps());
      Assert.InRange(apex, 4.7f, 5.2f);
    }

    [Fact]
    public void Ceiling_Stops_The_Jump() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Box(f, new FPVector3(0, FP.FromString("3.5"), 0), new FPVector3(3, FP._0_50, 3)); // underside at 3 m
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, 0, jump: t >= 5 && t < 8));

      var apex = s.MaxFeet(e, PPCTest.Ticks(1.5f));
      s.Step(PPCTest.Ticks(1f));

      Assert.InRange(apex, 0.8f, 1.05f);   // head (2 m) reaches the 3 m ceiling
      Assert.True(s.Character(e).Ground.IsGrounded, "fell back down");
    }

    [Fact]
    public void Launch_Pad_Kick_While_Grounded_Launches() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); });
      s.Step(1);
      HarnessKickSystem.Schedule.Add((5, e, new FPVector3(0, 15, 0)));

      var apex = s.MaxFeet(e, PPCTest.Ticks(2.5f));

      Assert.InRange(apex, 10.5f, 11.5f);  // 15²/20 = 11.25 m
    }

    [Fact]
    public void Jump_During_Launch_Keeps_The_Larger_Velocity() {
      // Unity "ceiling bump" fix: a jump right after a launch must not clamp the launch down to 10 m/s.
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: t >= 8 && t < 10));
      s.Step(1);
      HarnessKickSystem.Schedule.Add((5, e, new FPVector3(0, 15, 0)));

      var apex = s.MaxFeet(e, PPCTest.Ticks(2.5f));

      Assert.True(apex > 10f, $"apex {apex}");
    }

    [Fact]
    public void Walks_Up_And_Back_Down_A_Ramp_Staying_Grounded() {
      // 20° ramp rising along +Z from z ≈ 4. Walk up for 1.6 s, then back down.
      EntityRef e = default;
      int turn = PPCTest.Ticks(1.6f);
      using var s = PPCTest.Session(f => {
        Floor(f, 0);
        Ramp(f, new FPVector3(0, 0, 6), new FPVector3(2, FP._0_50, 5), pitch: -20);
        e = SpawnCharacter(f, FPVector3.Zero);
      }, input: t => Move(0, t < turn ? FP._1 : -FP._1));

      int ungrounded = 0;
      float climbVy = 0;
      for (int i = 0; i < PPCTest.Ticks(3.2f); i++) {
        s.Step(1);
        var c = s.Character(e);
        if (!c.Ground.IsGrounded) ungrounded++;
        if (i == PPCTest.Ticks(1.4f)) climbVy = s.Velocity(e).Y.AsFloat;
      }

      Assert.InRange(climbVy, 1.7f, 1.95f);   // 5 m/s · tan 20° ≈ 1.82 m/s: moving along the surface
      Assert.Equal(0, ungrounded);           // no launch on the way up, no hops on the way down
      Assert.InRange((s.Position(e).Y - 1).AsFloat, -0.02f, 0.05f);
    }
  }
}
