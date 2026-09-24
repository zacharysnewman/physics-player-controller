namespace PPC.Tests {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Xunit.Abstractions;
  using Assert = Xunit.Assert;

  /// <summary>
  /// Behaviour lock for refactors: every player's position and state on every tick of the four-player
  /// scenario must match <c>Golden/scenario-trace.txt</c> (within 1 mm). Frame checksums would also flag
  /// harmless changes like removing a field, so positions are compared instead.
  /// Regenerate deliberately with <c>PPC_UPDATE_GOLDEN=1</c> when behaviour is meant to change.
  /// </summary>
  [Collection("Quantum")]
  public unsafe class GoldenTraceTests {
    readonly ITestOutputHelper _output;
    public GoldenTraceTests(ITestOutputHelper output) { _output = output; }

    static readonly long ToleranceRaw = FP.FromString("0.001").RawValue;

    static string GoldenPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Golden", "scenario-trace.txt"));

    internal static List<string> RecordTrace() {
      var (_, assets) = PPCTestWorld.DefaultAssets();
      HarnessKickSystem.Schedule.Clear();
      HarnessKickSystem.Explosions.Clear();
      HarnessMoverSystem.Movers.Clear();
      HarnessKickSystem.Explosions.Add((300, new FPVector3(0, 1, 6), (FP)8, (FP)10, FP._0_50));

      using var s = new HeadlessSession(Phase8HardeningTests.ScenarioWorld, Phase8HardeningTests.ScenarioInput,
        c => { c.AddSystem<HarnessMoverSystem>(); PPCSystems.AddTo(c); c.AddSystem<HarnessKickSystem>(); },
        playerCount: Phase8HardeningTests.ScenarioPlayers, seed: 7, extraAssets: assets);

      var lines = new List<string>();
      for (int tick = 0; tick < Phase8HardeningTests.ScenarioTicks; tick++) {
        s.Step(1);
        var rows = new SortedDictionary<int, string>();
        var filter = s.Frame.Filter<PPCPlayerLink, Transform3D>();
        while (filter.NextUnsafe(out var e, out var link, out var t)) {
          var c = s.Frame.Get<PPCCharacter>(e);
          var p = t->Position;
          rows[(int)link->Player] = $"{tick} {(int)link->Player} {p.X.RawValue} {p.Y.RawValue} {p.Z.RawValue} {c.State}";
        }
        lines.AddRange(rows.Values);
      }
      return lines;
    }

    [Fact]
    public void Scenario_Matches_Golden_Trace() {
      var actual = RecordTrace();

      if (Environment.GetEnvironmentVariable("PPC_UPDATE_GOLDEN") == "1") {
        Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath));
        File.WriteAllLines(GoldenPath, actual);
        _output.WriteLine($"Golden trace updated: {GoldenPath}");
        return;
      }

      Assert.True(File.Exists(GoldenPath), $"missing {GoldenPath}; run with PPC_UPDATE_GOLDEN=1");
      var expected = File.ReadAllLines(GoldenPath);
      Assert.Equal(expected.Length, actual.Count);

      long worst = 0;
      string worstLine = null;
      for (int i = 0; i < expected.Length; i++) {
        var e = expected[i].Split(' ');
        var a = actual[i].Split(' ');
        Assert.True(e[0] == a[0] && e[1] == a[1], $"row {i}: {expected[i]} vs {actual[i]}");
        Assert.True(e[5] == a[5], $"state differs at row {i}: {expected[i]} vs {actual[i]}");
        for (int k = 2; k <= 4; k++) {
          var diff = Math.Abs(long.Parse(e[k]) - long.Parse(a[k]));
          if (diff > worst) { worst = diff; worstLine = $"{expected[i]}  vs  {actual[i]}"; }
        }
      }
      _output.WriteLine($"max deviation: {FP.FromRaw(worst)} m {(worstLine != null ? "at " + worstLine : "")}");
      Assert.True(worst <= ToleranceRaw, $"position deviates by {FP.FromRaw(worst)} m: {worstLine}");
    }
  }
}
