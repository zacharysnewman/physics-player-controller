namespace PPC.Tests {
  using Quantum;
  using Xunit;
  using Assert = Xunit.Assert;

  /// <summary>Checks the package's systems register and run in a real session.</summary>
  [Collection("Quantum")]
  public unsafe class PackageTests {
    [Fact]
    public void AddTo_Registers_Package_Systems_In_Order() {
      var config = AssetObject.Create<SystemsConfig>();

      PPCSystems.AddTo(config);

      var types = config.Entries.ConvertAll(e => e.SystemType.Value);
      Assert.Contains(typeof(PPCSetupSystem), types);
      Assert.True(types.IndexOf(typeof(PPCInputSystem)) < types.IndexOf(typeof(PPCAggregateSystem)));
      Assert.Equal(typeof(PPCStateSystem), types[types.Count - 1]);
    }

    [Fact]
    public void Character_Without_Config_Is_Ignored_Safely() {
      EntityRef e = default;
      using var s = new HeadlessSession(f => {
        e = f.Create();
        f.Set(e, Transform3D.Create());
        f.Add(e, new PPCCharacter());
      }, configureSystems: PPCSystems.AddTo);

      s.Step(10);

      Assert.False(s.Frame.Has<PhysicsBody3D>(e));
    }
  }
}
