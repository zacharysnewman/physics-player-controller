namespace Quantum {
  using System;

  /// <summary>
  /// Lets a headless test build its scene inside the simulation (on the first frame), so
  /// entity creation goes through the normal deterministic path. Not part of the shipped package.
  /// </summary>
  public class HarnessBootstrapSystem : SystemMainThread {
    /// <summary>Set by the test before the session starts; invoked once from <see cref="OnInit"/>.
    /// Static, so tests using it must not run in parallel.</summary>
    public static Action<Frame> Setup;

    public override void OnInit(Frame f) {
      Setup?.Invoke(f);
    }

    public override void Update(Frame f) {
    }
  }
}
