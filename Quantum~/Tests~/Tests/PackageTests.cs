namespace PPC.Tests {
  using System.Linq;
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;

  /// <summary>Checks the package's own simulation code (Quantum~/Simulation) runs in a real session.</summary>
  [Collection("Quantum")]
  public unsafe class PackageTests {
    [Fact]
    public void AddTo_Registers_Package_Systems() {
      var config = AssetObject.Create<SystemsConfig>();

      PPCSystems.AddTo(config);

      Assert.Contains(config.Entries, e => e.SystemType.Value == typeof(PPCStateSystem));
    }

    [Fact]
    public void Character_Entity_Ticks_Through_Package_Systems() {
      EntityRef character = default;
      using var s = new HeadlessSession(
        f => {
          character = f.Create();
          f.Set(character, Transform3D.Create());
          f.Set(character, new PPCCharacter { State = PPCState.Falling });
        },
        configureSystems: PPCSystems.AddTo);

      s.Step(10);

      Assert.True(s.Frame.Unsafe.TryGetPointer<PPCCharacter>(character, out var c));
      Assert.Equal(PPCState.Idle, c->State); // the stub system ran
    }
  }
}
