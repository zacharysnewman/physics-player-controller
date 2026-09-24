namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Configures the physics body and capsule from <see cref="PPCConfig"/> whenever a
  /// <see cref="PPCCharacter"/> is added, so an entity prototype only needs the component (a config is
  /// optional: <see cref="PPCConfig.Default"/> is used without one).
  /// </summary>
  public unsafe class PPCSetupSystem : SystemSignalsOnly, ISignalOnComponentAdded<PPCCharacter> {
    public void OnAdded(Frame f, EntityRef entity, PPCCharacter* character) {
      Configure(f, entity, PPCConfig.Resolve(f, character->Config));
      character->Horizontal.SpeedMultiplier = FP._1;
      character->Ground.Normal = FPVector3.Up;
    }

    /// <summary>Adds or overwrites Transform3D, PhysicsCollider3D and PhysicsBody3D to match the config.</summary>
    public static void Configure(Frame f, EntityRef entity, PPCConfig config) {
      if (!f.Has<Transform3D>(entity)) {
        f.Set(entity, Transform3D.Create());
      }

      var collider = PhysicsCollider3D.Create(f, PPCCapsule.Shape(config, crouching: false));
      collider.Layer = config.Body.Layer;
      collider.Material = config.Body.Material.IsValid ? config.Body.Material : FrictionlessMaterial(f);
      f.Set(entity, collider);

      // Rotation is frozen and physics gravity is off: the controller owns both.
      var body = PhysicsBody3D.CreateDynamic(config.Body.Mass);
      body.RotationFreeze = RotationFreezeFlags.FreezeAll;
      body.GravityScale = FP._0;
      body.Drag = FP._0;
      body.AllowSleeping = false;
      f.Set(entity, body);
    }

    /// <summary>
    /// A frictionless material, created once per game as a dynamic asset (part of the deterministic
    /// frame state). Friction against walls would otherwise slow sliding along them.
    /// </summary>
    public static AssetRef<PhysicsMaterial> FrictionlessMaterial(Frame f) {
      var shared = f.Unsafe.GetOrAddSingletonPointer<PPCShared>();
      if (!shared->FrictionlessMaterial.IsValid) {
        var material = AssetObject.Create<PhysicsMaterial>();
        material.FrictionStatic = FP._0;
        material.FrictionDynamic = FP._0;
        material.FrictionCombineFunction = PhysicsCombineFunction.Min;
        material.Restitution = FP._0;
        material.RestitutionCombineFunction = PhysicsCombineFunction.Min;
        shared->FrictionlessMaterial = new AssetRef<PhysicsMaterial>(f.AddAsset(material));
      }
      return shared->FrictionlessMaterial;
    }
  }
}
