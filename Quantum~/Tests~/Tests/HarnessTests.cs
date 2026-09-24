namespace PPC.Tests {
  using System.Linq;
  using Photon.Deterministic;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;

  // HarnessBootstrapSystem.Setup is static, so sessions must not run in parallel.
  [CollectionDefinition("Quantum", DisableParallelization = true)]
  public class QuantumCollection { }

  /// <summary>Checks the harness itself: sessions start, tick, and are deterministic.</summary>
  [Collection("Quantum")]
  public unsafe class HarnessTests {
    static void SpawnSmoke(Frame f) {
      var e = f.Create();
      f.Set(e, Transform3D.Create());
      f.Set(e, new PPCHarnessSmoke { Speed = FP._2 });
    }

    [Fact]
    public void Session_Starts_And_Steps_One_Tick_At_A_Time() {
      using var s = new HeadlessSession(SpawnSmoke, configureSystems: c => c.AddSystem<HarnessSmokeSystem>());
      int start = s.Frame.Number;

      s.Step(10);

      Assert.Equal(start + 10, s.Frame.Number);
    }

    [Fact]
    public void Smoke_System_Integrates_Velocity() {
      using var s = new HeadlessSession(SpawnSmoke, configureSystems: c => c.AddSystem<HarnessSmokeSystem>());

      s.Step(HeadlessSession.UpdateFps); // one simulated second

      var filter = s.Frame.Filter<Transform3D, PPCHarnessSmoke>();
      Assert.True(filter.NextUnsafe(out _, out var transform, out var smoke));
      // v = a·t = 2 m/s after 1 s
      Assert.Equal(FP._2.AsFloat, smoke->Velocity.Z.AsFloat, 2);
      Assert.True(transform->Position.Z > FP._0_50);
    }

    [Fact]
    public void Scripted_Input_Reaches_The_Simulation() {
      FP seen = default;
      using var s = new HeadlessSession(
        f => { },
        input: (tick, player) => new Quantum.Input { Move = new FPVector2(FP._1, FP._0) },
        configureSystems: c => c.AddSystem<HarnessInputProbeSystem>());

      s.Step(5);
      seen = HarnessInputProbeSystem.LastMoveX;

      Assert.Equal(FP._1, seen);
    }

    static ulong[] RunBoxPile(int seed, int ticks = 180) {
      using var s = new HeadlessSession(f => {
        // A pile of dynamic boxes with RNG-driven offsets falling onto a kinematic floor.
        var floor = f.Create();
        f.Set(floor, Transform3D.Create(new FPVector3(0, -1, 0)));
        f.Set(floor, PhysicsCollider3D.Create(f, Shape3D.CreateBox(new FPVector3(20, 1, 20))));
        f.Set(floor, PhysicsBody3D.CreateKinematic());

        for (int i = 0; i < 20; i++) {
          var box = f.Create();
          var pos = new FPVector3(f.RNG->Next(-FP._2, FP._2), 2 + i, f.RNG->Next(-FP._2, FP._2));
          f.Set(box, Transform3D.Create(pos, FPQuaternion.Euler(0, i * 17, 0)));
          f.Set(box, PhysicsCollider3D.Create(f, Shape3D.CreateBox(FPVector3.One * FP._0_50)));
          f.Set(box, PhysicsBody3D.CreateDynamic(FP._1));
        }
      }, seed: seed);

      s.Step(ticks);
      return s.Checksums.ToArray();
    }

    [Fact]
    public void Physics_Run_Is_Deterministic() {
      var a = RunBoxPile(seed: 1234);
      var b = RunBoxPile(seed: 1234);

      Assert.Equal(180, a.Length);
      Assert.Equal(a, b);
      Assert.True(a.Distinct().Count() > 1, "checksums never changed; the simulation isn't doing anything");
    }

    [Fact]
    public void Checksums_Detect_A_Different_Simulation() {
      // Control for the determinism test: if checksums ignored state, it would pass vacuously.
      var a = RunBoxPile(seed: 1234, ticks: 30);
      var b = RunBoxPile(seed: 4321, ticks: 30);

      Assert.NotEqual(a, b);
    }
  }
}
