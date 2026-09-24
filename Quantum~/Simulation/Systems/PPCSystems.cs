namespace Quantum {
  using System.Collections.Generic;

  /// <summary>
  /// The controller's systems, in the order they must run. Add them after Quantum's core systems
  /// (including <c>Core.PhysicsSystem3D</c>): the controller reads the previous physics step's results
  /// and sets velocities for the next one. In a SystemsConfig asset, the simplest is one
  /// <see cref="PPCSystemGroup"/> entry. From code, <see cref="AddTo"/>.
  /// </summary>
  public static class PPCSystems {
    public static void AddTo(SystemsConfig config) {
      foreach (var system in Create()) {
        config.AddSystem(system.GetType());
      }
    }

    public static SystemBase[] Create() => new SystemBase[] {
      new PPCSetupSystem(),          // signals: builds body + capsule when PPCCharacter is added
      new PPCInputSystem(),          // input bridge, previous input
      new PPCProbeSystem(),          // ground / ceiling / walls
      new PPCPlatformSystem(),       // base velocity of the ground entity
      new PPCCrouchSystem(),         // capsule size (before the jump uses it)
      new PPCJumpSystem(),           // buffer, coyote, jump impulse
      new PPCClimbSystem(),          // exclusive ladder layer
      new PPCMovementLayerSystem(),  // horizontal layer
      new PPCVerticalLayerSystem(),  // vertical layer
      new PPCAggregateSystem(),      // drive the body
      new PPCStateSystem(),          // state for animation / gameplay
    };
  }
}
