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

    /// <summary>Counts events of type <typeparamref name="T"/> raised from now on.</summary>
    public static Func<int> Count<T>(this HeadlessSession s) where T : EventBase {
      int n = 0;
      s.Events.Subscribe(s, (T _) => n++);
      return () => n;
    }

    /// <summary>Highest capsule-bottom height reached over <paramref name="ticks"/> ticks.</summary>
    public static float MaxFeet(this HeadlessSession s, EntityRef e, int ticks, FP halfHeight = default) {
      if (halfHeight == default) halfHeight = FP._1;
      float max = float.MinValue;
      for (int i = 0; i < ticks; i++) {
        s.Step(1);
        max = Math.Max(max, (s.Position(e).Y - halfHeight).AsFloat);
      }
      return max;
    }

    /// <summary>Height of the capsule bottom (default config: half height 1 standing, 0.5 crouched).</summary>
    public static float Feet(this HeadlessSession s, EntityRef e) =>
      (s.Position(e).Y - (s.Character(e).Crouch.IsCrouching ? FP._0_50 : FP._1)).AsFloat;

    /// <summary>Top of the capsule.</summary>
    public static float Head(this HeadlessSession s, EntityRef e) =>
      (s.Position(e).Y + (s.Character(e).Crouch.IsCrouching ? FP._0_50 : FP._1)).AsFloat;

    /// <summary>Seconds → ticks at the harness rate.</summary>
    public static int Ticks(float seconds) => (int)Math.Round(seconds * HeadlessSession.UpdateFps);
  }
}
