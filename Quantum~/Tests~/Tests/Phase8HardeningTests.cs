namespace PPC.Tests {
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Xunit.Abstractions;
  using Assert = Xunit.Assert;
  using static PPCTestWorld;

  /// <summary>Phase 8: every feature at once, several players, determinism and cost.</summary>
  [Collection("Quantum")]
  public unsafe class Phase8HardeningTests {
    readonly ITestOutputHelper _output;
    public Phase8HardeningTests(ITestOutputHelper output) { _output = output; }

    public const int ScenarioTicks = 600;
    public const int ScenarioPlayers = 4;

    /// <summary>Floor, ramp, step, low bar, ladder to a ledge, a moving platform and an explosion.</summary>
    internal static void ScenarioWorld(Frame f) {
      Floor(f, 0);
      Ramp(f, new FPVector3(-6, 0, 6), new FPVector3(2, FP._0_50, 5), pitch: -20);
      Box(f, new FPVector3(6, FP.FromString("0.15"), 8), new FPVector3(2, FP.FromString("0.15"), 4));   // step
      Box(f, new FPVector3(0, 2, 8), new FPVector3(2, FP._0_50, 1));                                    // low bar
      Box(f, new FPVector3(12, FP.FromString("1.5"), FP.FromString("3.1")), new FPVector3(2, FP.FromString("1.5"), 2)); // ledge
      Ladder(f, new FPVector3(12, FP.FromString("1.6"), 1), new FPVector3(FP._0_50, FP.FromString("1.6"), FP._0_10));
      var platform = Box(f, new FPVector3(-12, FP.FromString("0.25"), 0), new FPVector3(2, FP.FromString("0.25"), 2));
      HarnessMoverSystem.Movers[platform] = (new FPVector3(0, 0, FP._1), 30);

      // Players start near different features.
      SpawnCharacter(f, new FPVector3(-6, 0, 0), 0);    // ramp
      SpawnCharacter(f, new FPVector3(0, 0, 4), 1);     // low bar
      SpawnCharacter(f, new FPVector3(12, 0, 0), 2);    // ladder
      SpawnCharacter(f, new FPVector3(-12, FP._0_50, 0), 3); // platform
    }

    /// <summary>Different deterministic input scripts per player, exercising every action.</summary>
    internal static Quantum.Input ScenarioInput(int tick, int player) {
      var phase = (tick / 40 + player) % 6;
      var yaw = (FP)((tick * (player + 1)) % 360);
      return phase switch {
        0 => Move(0, 1, yaw: player == 2 ? FP._0 : yaw),
        1 => Move(1, 1, run: true, yaw: yaw),
        2 => Move(0, 1, jump: tick % 40 < 3),
        3 => Move(-1, 0, crouch: true),
        4 => Move(0, 1, pitch: -45),
        _ => Move(0, 0, jump: tick % 7 == 0),
      };
    }

    static (ulong[] checksums, string trace) RunScenario() {
      var (_, assets) = DefaultAssets();
      HarnessKickSystem.Schedule.Clear();
      HarnessKickSystem.Explosions.Clear();
      HarnessMoverSystem.Movers.Clear();
      HarnessKickSystem.Explosions.Add((300, new FPVector3(0, 1, 6), (FP)8, (FP)10, FP._0_50));

      using var s = new HeadlessSession(ScenarioWorld, ScenarioInput,
        c => { c.AddSystem<HarnessMoverSystem>(); PPCSystems.AddTo(c); c.AddSystem<HarnessKickSystem>(); },
        playerCount: ScenarioPlayers, seed: 7, extraAssets: assets);
      s.Step(ScenarioTicks);

      var trace = string.Join(";", Enumerable.Range(0, ScenarioPlayers).Select(p => {
        var filter = s.Frame.Filter<PPCPlayerLink, Transform3D>();
        while (filter.NextUnsafe(out var e, out var link, out var t)) {
          if ((int)link->Player == p) return $"{p}:{t->Position}";
        }
        return $"{p}:missing";
      }));
      return (s.Checksums.ToArray(), trace);
    }

    [Fact]
    public void Full_Scenario_With_Four_Players_Is_Deterministic() {
      var a = RunScenario();
      var b = RunScenario();

      Assert.Equal(ScenarioTicks, a.checksums.Length);
      Assert.Equal(a.checksums, b.checksums);
      Assert.True(a.checksums.Distinct().Count() > ScenarioTicks / 2, "state kept changing");

      // Record for the Debug-vs-Release comparison (Tools/cross-config-check.sh).
#if DEBUG
      const string config = "Debug";
#else
      const string config = "Release";
#endif
      var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".build");
      Directory.CreateDirectory(dir);
      File.WriteAllLines(Path.Combine(dir, $"scenario-checksums-{config}.txt"), a.checksums.Select(c => c.ToString()));
      _output.WriteLine($"final positions: {a.trace}");
    }

    [Fact]
    public void Sixteen_Characters_Cost() {
      const int players = 16, ticks = 300;
      var (_, assets) = DefaultAssets();
      HarnessMoverSystem.Movers.Clear();
      using var s = new HeadlessSession(f => {
          Floor(f, 0);
          for (int p = 0; p < players; p++) SpawnCharacter(f, new FPVector3((p % 4) * 3, 0, (p / 4) * 3), p);
        },
        (t, p) => ScenarioInput(t, p),
        c => { PPCSystems.AddTo(c); },
        playerCount: players, extraAssets: assets);

      s.Step(30);   // warm up
      var watch = Stopwatch.StartNew();
      s.Step(ticks);
      watch.Stop();

      var msPerTick = watch.Elapsed.TotalMilliseconds / ticks;
      _output.WriteLine($"16 characters: {msPerTick:F3} ms per tick (whole Quantum session, this machine)");
      // Loose ceiling: catches pathological regressions, not normal variance. 60 Hz = 16.7 ms.
      Assert.True(msPerTick < 16.7, $"{msPerTick:F2} ms per tick");
    }
  }
}
