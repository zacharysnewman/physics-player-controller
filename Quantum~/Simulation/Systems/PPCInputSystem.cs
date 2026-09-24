namespace Quantum {
  using Photon.Deterministic;

  /// <summary>Reads input for player-linked characters and keeps the previous tick's input for edge detection.</summary>
  public unsafe class PPCInputSystem : PPCSystemBase {
    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      c->PreviousInput = c->Input;

      if (f.Unsafe.TryGetPointer<PPCPlayerLink>(filter.Entity, out var link) && link->Player.IsValid) {
        c->Input = PPCInputBridge.Read(f, link->Player);
      }

      // Clamp so diagonal/analog input can't exceed full speed.
      if (c->Input.Move.SqrMagnitude > FP._1) {
        c->Input.Move = c->Input.Move.Normalized;
      }
    }
  }
}
