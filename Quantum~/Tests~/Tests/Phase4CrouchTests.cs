namespace PPC.Tests {
  using System;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Phase 4: crouching.</summary>
  [Collection("Quantum")]
  public unsafe class Phase4CrouchTests {
    static FP CapsuleHalfHeight(HeadlessSession s, EntityRef e) {
      var cap = s.Frame.Get<PhysicsCollider3D>(e).Shape.Capsule;
      return cap.Extent + cap.Radius;
    }

    [Fact]
    public void Crouching_On_The_Ground_Keeps_The_Feet_Planted() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, crouch: t >= 5));
      var changes = s.Count<EventPPCCrouchChanged>();
      s.Step(20);

      Assert.True(s.Character(e).Crouch.IsCrouching);
      Assert.Equal(FP._0_50, CapsuleHalfHeight(s, e));
      Assert.InRange(s.Feet(e), -0.02f, 0.03f);
      Assert.Equal(PPCState.Crouching, s.Character(e).State);
      Assert.Equal(1, changes());
    }

    [Fact]
    public void Releasing_Crouch_Stands_Back_Up() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, crouch: t >= 5 && t < 20));
      s.Step(40);

      Assert.False(s.Character(e).Crouch.IsCrouching);
      Assert.Equal(FP._1, CapsuleHalfHeight(s, e));
      Assert.InRange(s.Feet(e), -0.02f, 0.03f);
    }

    [Fact]
    public void Crouch_Walking_Is_Slower() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 1, crouch: true));
      s.Step(PPCTest.Ticks(1f));

      Assert.InRange(s.HorizontalSpeed(e), 1.9f, 2.1f);   // Crouch.Speed = 2
    }

    // A 3 m long bar whose underside is 1.5 m up, starting at z = 2.
    static void LowBar(Frame f) => Box(f, new FPVector3(0, 2, FP.FromString("3.5")), new FPVector3(3, FP._0_50, FP.FromString("1.5")));

    [Fact]
    public void Standing_Character_Cannot_Pass_Under_A_Low_Bar() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); LowBar(f); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 1));
      s.Step(PPCTest.Ticks(2f));

      Assert.True(s.Position(e).Z < FP.FromString("1.6"), "blocked by the bar");
    }

    [Fact]
    public void Stays_Crouched_Under_The_Bar_And_Stands_Once_Clear() {
      // Crouch-walk under, release crouch while under it, keep walking out the other side.
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); LowBar(f); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 1, crouch: t < PPCTest.Ticks(1.8f)));
      bool stayedCrouchedUnder = true;
      for (int i = 0; i < PPCTest.Ticks(3.5f); i++) {
        s.Step(1);
        var z = s.Position(e).Z;
        if (z > FP._2 + FP._0_50 && z < FP._2 + FP._2 && !s.Character(e).Crouch.IsCrouching) {
          stayedCrouchedUnder = false;
        }
      }

      Assert.True(s.Position(e).Z > FP._2 * 3, "made it through");
      Assert.True(stayedCrouchedUnder, "stood up under the bar");
      Assert.False(s.Character(e).Crouch.IsCrouching, "stood up after clearing it");
      Assert.InRange(s.Feet(e), -0.02f, 0.03f);
    }

    [Fact]
    public void Mid_Air_Crouch_Tucks_The_Legs_Up() {
      EntityRef e = default;
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: t >= 2 && t < 4, crouch: t >= 19));
      s.Step(19);
      var headBefore = s.Head(e);
      s.Step(1);
      var headAfter = s.Head(e);

      Assert.True(s.Character(e).Crouch.IsCrouching);
      // The head keeps moving with the jump; the tuck itself doesn't move it.
      var headStep = headAfter - headBefore;
      Assert.InRange(headStep, 0f, 0.2f);
    }

    [Fact]
    public void Crouch_Jump_Clears_A_Higher_Ledge() {
      // Normal jump apex: feet at 5 m. Tucking lifts the feet ~1 m more.
      float MaxFeet(bool crouch) {
        EntityRef e = default;
        using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                      input: t => Move(0, 0, jump: t >= 2 && t < 4, crouch: crouch && t >= 10));
        float max = 0;
        for (int i = 0; i < PPCTest.Ticks(1.2f); i++) { s.Step(1); max = Math.Max(max, s.Feet(e)); }
        return max;
      }

      Assert.True(MaxFeet(true) > MaxFeet(false) + 0.8f);
    }

    [Fact]
    public void Mid_Air_Boost_Adds_Height() {
      float Apex(FP boost) {
        EntityRef e = default;
        using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                      input: t => Move(0, 0, jump: t >= 2 && t < 4, crouch: t >= 10),
                                      tweak: c => c.Crouch.MidAirBoost = boost);
        float max = 0;
        for (int i = 0; i < PPCTest.Ticks(1.5f); i++) { s.Step(1); max = Math.Max(max, s.Feet(e)); }
        return max;
      }

      Assert.True(Apex(3) > Apex(0) + 1f);
    }

    [Fact]
    public void Landing_While_Crouched_Keeps_A_Correct_Capsule_Then_Stands() {
      // Unity bug: after a mid-air crouch the capsule centre stayed offset on landing.
      EntityRef e = default;
      int release = PPCTest.Ticks(3f);
      using var s = PPCTest.Session(f => { Floor(f, 0); e = SpawnCharacter(f, FPVector3.Zero); },
                                    input: t => Move(0, 0, jump: t >= 2 && t < 4, crouch: t >= 10 && t < release));
      s.Step(release - 5);
      Assert.True(s.Character(e).Crouch.IsCrouching);
      Assert.InRange(s.Feet(e), -0.02f, 0.03f);   // crouched, standing on the floor

      s.Step(20);
      Assert.False(s.Character(e).Crouch.IsCrouching);
      Assert.InRange(s.Feet(e), -0.02f, 0.03f);   // stood up from the feet
      Assert.Equal(FPVector3.Zero, s.Frame.Get<PhysicsCollider3D>(e).Shape.LocalTransform.Position);
    }
  }
}
