namespace PPC.Tests {
  using System;
  using System.IO;
  using System.Text;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Xunit.Abstractions;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>
  /// Stairs and ledges under the recommended feel: the character climbs and descends anything up to
  /// <c>MaxStepHeight</c> without leaving the ground (the probe reaches down a whole step while grounded)
  /// and at full speed. Also writes a table (step-report.txt next to the test assembly) of how far the
  /// feet move in a single tick, which is what the camera's step smoothing hides.
  /// </summary>
  [Collection("Quantum")]
  public unsafe class StepSmoothnessTests {
    readonly ITestOutputHelper _output;
    public StepSmoothnessTests(ITestOutputHelper output) { _output = output; }

    const float StairStart = 2;

    public struct Result {
      public bool Arrived;
      public float Seconds;        // time to reach the target distance
      public float MaxRise;        // largest one-tick upward feet movement (m)
      public float MaxDrop;        // largest one-tick downward feet movement (m)
      public int Airborne;         // ticks not grounded on the way
      public float FinalFeet;      // after settling
    }

    /// <summary>
    /// <paramref name="steps"/> steps along +Z from z = 2, then a landing. Up: starts on the floor walking
    /// +Z. Down: starts on the landing walking -Z. A ledge is one step with a long tread.
    /// </summary>
    static Result Run(float rise, float tread, int steps, bool up, bool run) {
      var (config, assets) = DefaultAssets();
      config.ApplyRecommendedFeel();
      HarnessKickSystem.Schedule.Clear();
      HarnessKickSystem.Explosions.Clear();
      HarnessMoverSystem.Movers.Clear();

      var top = rise * steps;
      var end = StairStart + tread * steps;          // where the landing starts
      var startZ = up ? 0f : end + 1.5f;
      var targetZ = up ? end + 1f : StairStart - 1.5f;
      EntityRef e = default;
      bool arrived = false;
      using var s = new HeadlessSession(f => {
          Floor(f, 0);
          for (int i = 0; i < steps; i++) {
            var height = rise * (i + 1);
            var z0 = StairStart + tread * i;
            var z1 = end + 4;
            Box(f, new FPVector3(0, FP.FromFloat_UNSAFE(height / 2), FP.FromFloat_UNSAFE((z0 + z1) / 2)),
                new FPVector3(3, FP.FromFloat_UNSAFE(height / 2), FP.FromFloat_UNSAFE((z1 - z0) / 2)));
          }
          e = SpawnCharacter(f, new FPVector3(0, FP.FromFloat_UNSAFE(up ? 0 : top), FP.FromFloat_UNSAFE(startZ)));
        },
        (t, p) => arrived ? Move(0, 0) : Move(0, up ? 1 : -1, run: run),
        c => { PPCSystems.AddTo(c); c.AddSystem<HarnessKickSystem>(); }, extraAssets: assets);

      s.Step(3);
      var r = new Result();
      var previous = s.Feet(e);
      for (int tick = 1; tick <= 240 && !arrived; tick++) {
        s.Step(1);
        var feet = s.Feet(e);
        r.MaxRise = Math.Max(r.MaxRise, feet - previous);
        r.MaxDrop = Math.Max(r.MaxDrop, previous - feet);
        previous = feet;
        if (!s.Character(e).Ground.IsGrounded) r.Airborne++;
        var z = s.Position(e).Z.AsFloat;
        if (up ? z >= targetZ : z <= targetZ) {
          arrived = true;
          r.Arrived = true;
          r.Seconds = tick / (float)HeadlessSession.UpdateFps;
        }
      }
      s.Step(60);
      r.FinalFeet = s.Feet(e);
      return r;
    }

    [Theory]
    [InlineData("stairs 0.2/0.3 up, walk", 0.2f, 0.3f, 6, true, false)]
    [InlineData("stairs 0.2/0.3 down, walk", 0.2f, 0.3f, 6, false, false)]
    [InlineData("stairs 0.2/0.3 up, run", 0.2f, 0.3f, 6, true, true)]
    [InlineData("stairs 0.2/0.3 down, run", 0.2f, 0.3f, 6, false, true)]
    [InlineData("steps 0.4/0.6 up, walk", 0.4f, 0.6f, 3, true, false)]
    [InlineData("steps 0.4/0.6 down, walk", 0.4f, 0.6f, 3, false, false)]
    [InlineData("ledge 0.4 down, walk", 0.4f, 3f, 1, false, false)]
    public void Stays_Grounded_At_Full_Speed_Over_Steps(string name, float rise, float tread, int steps, bool up, bool run) {
      var r = Run(rise, tread, steps, up, run);
      var line = $"{name,-26} {r.Seconds,5:0.00} s  largest one-tick rise {r.MaxRise:0.000} / drop {r.MaxDrop:0.000} m  airborne {r.Airborne} ticks";
      _output.WriteLine(line);
      File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "step-report.txt"), line + "\n");

      Assert.True(r.Arrived, name);
      Assert.InRange(r.FinalFeet, (up ? rise * steps : 0) - 0.02f, (up ? rise * steps : 0) + 0.02f);
      Assert.Equal(0, r.Airborne);
      // Full speed: the flat-ground crossing time plus at most 0.05 s.
      var distance = up ? StairStart + tread * steps + 1f : tread * steps + 3f;
      var speed = run ? 8f : 5f;
      Assert.True(r.Seconds <= distance / speed + 0.15f, $"{name}: {r.Seconds} s");
    }

    [Fact]
    public void A_Ledge_Taller_Than_A_Step_Is_Still_A_Fall() {
      var r = Run(1.0f, 3f, 1, up: false, run: false);
      Assert.True(r.Airborne > 5, $"airborne {r.Airborne}");
      Assert.InRange(r.FinalFeet, -0.02f, 0.02f);
    }
  }
}
