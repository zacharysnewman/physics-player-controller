namespace PPC.Tests {
  using System;
  using Photon.Deterministic;
  using Quantum;

  /// <summary>Session shortcuts for controller tests.</summary>
  public static unsafe class PPCTest {
    /// <summary>Starts a session with the controller systems, the default config (optionally tweaked) and a kick system.</summary>
    public static HeadlessSession Session(Action<Frame> setup, Func<int, Quantum.Input> input = null,
                                          Action<PPCConfig> tweak = null, int seed = 0) {
      var (_, assets) = PPCTestWorld.DefaultAssets(tweak);
      HarnessKickSystem.Schedule.Clear();
      return new HeadlessSession(setup,
        input: input == null ? null : (tick, player) => input(tick),
        configureSystems: c => { PPCSystems.AddTo(c); c.AddSystem<HarnessKickSystem>(); },
        seed: seed, extraAssets: assets);
    }

    public static PPCCharacter Character(this HeadlessSession s, EntityRef e) => s.Frame.Get<PPCCharacter>(e);
    public static FPVector3 Position(this HeadlessSession s, EntityRef e) => s.Frame.Get<Transform3D>(e).Position;
    public static FPVector3 Velocity(this HeadlessSession s, EntityRef e) => s.Frame.Get<PhysicsBody3D>(e).Velocity;
    public static float HorizontalSpeed(this HeadlessSession s, EntityRef e) {
      var v = s.Velocity(e);
      return new FPVector3(v.X, 0, v.Z).Magnitude.AsFloat;
    }

    /// <summary>Seconds → ticks at the harness rate.</summary>
    public static int Ticks(float seconds) => (int)Math.Round(seconds * HeadlessSession.UpdateFps);
  }
}
