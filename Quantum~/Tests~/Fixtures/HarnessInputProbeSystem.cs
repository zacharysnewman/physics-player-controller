namespace Quantum {
  using Photon.Deterministic;

  /// <summary>Records player 0's input so tests can check scripted input reaches the simulation.</summary>
  public unsafe class HarnessInputProbeSystem : SystemMainThread {
    public static FP LastMoveX;

    public override void OnInit(Frame f) {
      LastMoveX = default;
    }

    public override void Update(Frame f) {
      LastMoveX = f.GetPlayerInput(0)->Move.X;
    }
  }
}
