namespace Quantum {
  /// <summary>
  /// Registers the controller's systems in a <see cref="SystemsConfig"/>, in the order they must run.
  /// Add the core Quantum systems (including <c>Core.PhysicsSystem3D</c>) before calling this.
  /// </summary>
  public static class PPCSystems {
    public static void AddTo(SystemsConfig config) {
      config.AddSystem<PPCStateSystem>();
    }
  }
}
