namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Configures the physics body and capsule from <see cref="PPCConfig"/> whenever a
  /// <see cref="PPCCharacter"/> is added, so an entity prototype only needs the component and a config.
  /// </summary>
  public unsafe class PPCSetupSystem : SystemSignalsOnly, ISignalOnComponentAdded<PPCCharacter> {
    public void OnAdded(Frame f, EntityRef entity, PPCCharacter* character) {
      var config = f.FindAsset(character->Config);
      if (config == null) {
        Log.Error($"PPCCharacter on {entity} has no PPCConfig");
        return;
      }
      Configure(f, entity, config);
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
      if (config.Body.Material.IsValid) {
        collider.Material = config.Body.Material;
      }
      f.Set(entity, collider);

      // Rotation is frozen and physics gravity is off: the controller owns both.
      var body = PhysicsBody3D.CreateDynamic(config.Body.Mass);
      body.RotationFreeze = RotationFreezeFlags.FreezeAll;
      body.GravityScale = FP._0;
      body.Drag = FP._0;
      body.AllowSleeping = false;
      f.Set(entity, body);
    }
  }
}
