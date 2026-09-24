namespace Quantum {
  /// <summary>
  /// Registers the controller's systems in a <see cref="SystemsConfig"/>, in the order they must run.
  /// Add the core Quantum systems (including <c>Core.PhysicsSystem3D</c>) before calling this: the
  /// controller reads the previous physics step's results and sets velocities for the next one.
  /// </summary>
  public static class PPCSystems {
    public static void AddTo(SystemsConfig config) {
      config.AddSystem<PPCSetupSystem>();
      config.AddSystem<PPCInputSystem>();
      config.AddSystem<PPCProbeSystem>();
      config.AddSystem<PPCPlatformSystem>();
      config.AddSystem<PPCCrouchSystem>();
      config.AddSystem<PPCJumpSystem>();
      config.AddSystem<PPCMovementLayerSystem>();
      config.AddSystem<PPCVerticalLayerSystem>();
      config.AddSystem<PPCAggregateSystem>();
      config.AddSystem<PPCStateSystem>();
    }
  }
}
