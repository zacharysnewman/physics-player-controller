namespace PPC.Tests {
  using System;
  using Photon.Deterministic;
  using Quantum;

  /// <summary>Assets and scene-building helpers shared by the controller tests.</summary>
  public static unsafe class PPCTestWorld {
    public const long ConfigGuid = 100;
    public const long FrictionlessGuid = 101;

    /// <summary>
    /// A config with the package defaults (no material set, so the automatic frictionless one is used),
    /// plus a separate frictionless material asset for tests that set one explicitly.
    /// </summary>
    public static (PPCConfig config, AssetObject[] assets) DefaultAssets(Action<PPCConfig> tweak = null) {
      var material = AssetObject.Create<PhysicsMaterial>();
      material.FrictionStatic = FP._0;
      material.FrictionDynamic = FP._0;
      material.FrictionCombineFunction = PhysicsCombineFunction.Min;
      material.Restitution = FP._0;
      material.RestitutionCombineFunction = PhysicsCombineFunction.Min;
      HeadlessSession.Identify(material, FrictionlessGuid, "Tests/Frictionless");

      var config = AssetObject.Create<PPCConfig>();
      tweak?.Invoke(config);
      HeadlessSession.Identify(config, ConfigGuid, "Tests/PPCConfig");
      return (config, new AssetObject[] { config, material });
    }

    public static AssetRef<PPCConfig> ConfigRef => new AssetRef<PPCConfig>(new AssetGuid(ConfigGuid));
    public static AssetRef<PhysicsMaterial> FrictionlessRef => new AssetRef<PhysicsMaterial>(new AssetGuid(FrictionlessGuid));

    /// <summary>Spawns a character (driven by player 0 unless told otherwise) whose feet rest at <paramref name="feet"/>.</summary>
    public static EntityRef SpawnCharacter(Frame f, FPVector3 feet, PlayerRef? player = null) =>
      PPCSpawn.Character(f, ConfigRef, feet, player: player ?? (PlayerRef)0);

    /// <summary>Kinematic box whose top face is at <paramref name="topY"/>.</summary>
    public static EntityRef Box(Frame f, FPVector3 center, FPVector3 halfExtents, FPQuaternion? rotation = null) {
      var e = f.Create();
      f.Set(e, Transform3D.Create(center, rotation ?? FPQuaternion.Identity));
      f.Set(e, PhysicsCollider3D.Create(f, Shape3D.CreateBox(halfExtents)));
      f.Set(e, PhysicsBody3D.CreateKinematic());
      return e;
    }

    /// <summary>A kinematic box tilted by <paramref name="pitch"/> (about X) and <paramref name="roll"/> (about Z), in degrees.</summary>
    public static EntityRef Ramp(Frame f, FPVector3 center, FPVector3 halfExtents, FP pitch, FP roll = default) =>
      Box(f, center, halfExtents, FPQuaternion.Euler(pitch, 0, roll));

    /// <summary>A ladder: trigger box with a <see cref="PPCLadder"/>, facing its local +Z.</summary>
    public static EntityRef Ladder(Frame f, FPVector3 center, FPVector3 halfExtents) {
      var e = f.Create();
      f.Set(e, Transform3D.Create(center));
      var trigger = PhysicsCollider3D.Create(f, Shape3D.CreateBox(halfExtents));
      trigger.IsTrigger = true;
      f.Set(e, trigger);
      f.Set(e, new PPCLadder());
      return e;
    }

    /// <summary>Box with a collider but no body (moved by script, like an animated platform).</summary>
    public static EntityRef ScriptedBox(Frame f, FPVector3 center, FPVector3 halfExtents) {
      var e = f.Create();
      f.Set(e, Transform3D.Create(center));
      f.Set(e, PhysicsCollider3D.Create(f, Shape3D.CreateBox(halfExtents)));
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
