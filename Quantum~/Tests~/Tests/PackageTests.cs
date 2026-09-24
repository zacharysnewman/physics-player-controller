namespace PPC.Tests {
  using System.Linq;
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
    public void System_Group_Moves_The_Character_Exactly_Like_Individual_Systems() {
      // Frame checksums include the system list itself (the group is one extra system), so compare
      // the character's trajectory instead.
      string Run(bool useGroup) {
        var (_, assets) = PPCTestWorld.DefaultAssets();
        EntityRef e = default;
        using var s = new HeadlessSession(f => {
          PPCTestWorld.Floor(f, 0);
          e = PPCTestWorld.SpawnCharacter(f, Photon.Deterministic.FPVector3.Zero);
        },
        input: (t, p) => PPCTestWorld.Move(0, 1, jump: t == 30, crouch: t > 60),
        configureSystems: c => { if (useGroup) c.AddSystem<PPCSystemGroup>(); else PPCSystems.AddTo(c); },
        extraAssets: assets);
        var trace = new System.Text.StringBuilder();
        for (int i = 0; i < 120; i++) {
          s.Step(1);
          var c = s.Frame.Get<PPCCharacter>(e);
          trace.Append(s.Frame.Get<Transform3D>(e).Position).Append(c.State).Append(';');
        }
        return trace.ToString();
      }

      Assert.Equal(Run(false), Run(true));
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
