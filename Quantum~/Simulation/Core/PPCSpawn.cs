namespace Quantum {
  using Photon.Deterministic;

  /// <summary>Creates controller characters from code (spawn systems, tests, bots).</summary>
  public static unsafe class PPCSpawn {
    /// <summary>
    /// Creates a character standing with its feet at <paramref name="feet"/>. <see cref="PPCSetupSystem"/>
    /// adds the physics body and capsule. Pass a player to drive it through <see cref="PPCInputBridge"/>,
    /// and a view to have Unity show it.
    /// </summary>
    public static EntityRef Character(Frame f, AssetRef<PPCConfig> config, FPVector3 feet, FP yawDegrees = default,
                                      PlayerRef player = default, AssetRef<EntityView> view = default) {
      var halfHeight = PPCConfig.Resolve(f, config).HalfHeight(false);

      var e = f.Create();
      f.Set(e, Transform3D.Create(feet + FPVector3.Up * halfHeight, FPQuaternion.Euler(0, yawDegrees, 0)));
      if (view.IsValid) {
        f.Set(e, new View { Current = view });
      }
      if (player.IsValid) {
        f.Set(e, new PPCPlayerLink { Player = player });
      }
      var character = new PPCCharacter { Config = config };
      character.Input.LookYaw = yawDegrees;
      f.Add(e, character);
      return e;
    }
  }
}
