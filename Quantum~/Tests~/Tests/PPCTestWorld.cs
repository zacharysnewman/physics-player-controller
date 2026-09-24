namespace PPC.Tests {
  using System;
  using Photon.Deterministic;
  using Quantum;

  /// <summary>Assets and scene-building helpers shared by the controller tests.</summary>
  public static unsafe class PPCTestWorld {
    public const long ConfigGuid = 100;
    public const long FrictionlessGuid = 101;

    /// <summary>A config with the Unity defaults and a frictionless material, plus that material.</summary>
    public static (PPCConfig config, AssetObject[] assets) DefaultAssets(Action<PPCConfig> tweak = null) {
      var material = AssetObject.Create<PhysicsMaterial>();
      material.FrictionStatic = FP._0;
      material.FrictionDynamic = FP._0;
      material.FrictionCombineFunction = PhysicsCombineFunction.Min;
      material.Restitution = FP._0;
      material.RestitutionCombineFunction = PhysicsCombineFunction.Min;
      HeadlessSession.Identify(material, FrictionlessGuid, "Tests/Frictionless");

      var config = AssetObject.Create<PPCConfig>();
      config.Body.Material = new AssetRef<PhysicsMaterial>(material.Guid);
      tweak?.Invoke(config);
      HeadlessSession.Identify(config, ConfigGuid, "Tests/PPCConfig");
      return (config, new AssetObject[] { config, material });
    }

    public static AssetRef<PPCConfig> ConfigRef => new AssetRef<PPCConfig>(new AssetGuid(ConfigGuid));

    /// <summary>Spawns a character whose capsule bottom rests at <paramref name="feet"/>.</summary>
    public static EntityRef SpawnCharacter(Frame f, FPVector3 feet, PlayerRef? player = null) =>
      PPCSpawn.Character(f, ConfigRef, feet, player: player ?? default);

    /// <summary>Kinematic box whose top face is at <paramref name="topY"/>.</summary>
    public static EntityRef Box(Frame f, FPVector3 center, FPVector3 halfExtents) {
      var e = f.Create();
      f.Set(e, Transform3D.Create(center));
      f.Set(e, PhysicsCollider3D.Create(f, Shape3D.CreateBox(halfExtents)));
      f.Set(e, PhysicsBody3D.CreateKinematic());
      return e;
    }

    public static EntityRef Floor(Frame f, FP topY, FP halfSize = default) {
      if (halfSize == default) halfSize = 50;
      return Box(f, new FPVector3(0, topY - FP._0_50, 0), new FPVector3(halfSize, FP._0_50, halfSize));
    }

    public static Quantum.Input Move(FP x, FP y, bool run = false, bool jump = false, bool crouch = false, FP yaw = default, FP pitch = default) =>
      new Quantum.Input { Move = new FPVector2(x, y), Run = run, Jump = jump, Crouch = crouch, LookYaw = yaw, LookPitch = pitch };
  }
}
