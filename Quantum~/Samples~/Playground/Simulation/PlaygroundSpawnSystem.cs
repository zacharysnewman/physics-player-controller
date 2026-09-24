namespace Quantum {
  using Photon.Deterministic;

  // Settings the Playground adds to the RuntimeConfig (set them on the QuantumRunnerLocalDebug or
  // your menu's RuntimeConfig).
  public partial class RuntimeConfig {
    public AssetRef<PPCConfig> PlaygroundCharacterConfig;
    public AssetRef<EntityView> PlaygroundCharacterView;
    public FPVector3 PlaygroundSpawnPoint;
  }

  /// <summary>Spawns one controller character per player that joins.</summary>
  public unsafe class PlaygroundSpawnSystem : SystemSignalsOnly, ISignalOnPlayerAdded {
    public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
      if (!firstTime) return;
      var config = f.RuntimeConfig;
      var spacing = new FPVector3(2, 0, 0) * (int)player;
      PPCSpawn.Character(f, config.PlaygroundCharacterConfig, config.PlaygroundSpawnPoint + spacing,
                         player: player, view: config.PlaygroundCharacterView);
    }
  }
}
