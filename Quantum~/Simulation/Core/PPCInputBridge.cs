namespace Quantum {
  /// <summary>
  /// Connects the game's input to the controller. Quantum allows one <c>input</c> definition per game,
  /// so the package can't define it; implement <see cref="ReadPlayerInput"/> in your game code instead
  /// (it compiles into the same Quantum.Simulation assembly):
  /// <code>
  /// namespace Quantum {
  ///   public static unsafe partial class PPCInputBridge {
  ///     static partial void ReadPlayerInput(Frame f, PlayerRef player, ref PPCInput input) {
  ///       var i = f.GetPlayerInput(player);
  ///       input.Move = i->Move;
  ///       input.LookYaw = i->LookYaw;
  ///       input.LookPitch = i->LookPitch;
  ///       input.Jump = i->Jump.IsDown;
  ///       input.Run = i->Run.IsDown;
  ///       input.Crouch = i->Crouch.IsDown;
  ///     }
  ///   }
  /// }
  /// </code>
  /// Characters without a <see cref="PPCPlayerLink"/> (e.g. bots) keep whatever is written to
  /// <see cref="PPCCharacter.Input"/>.
  /// </summary>
  public static unsafe partial class PPCInputBridge {
    static partial void ReadPlayerInput(Frame f, PlayerRef player, ref PPCInput input);

    public static PPCInput Read(Frame f, PlayerRef player) {
      var input = default(PPCInput);
      ReadPlayerInput(f, player, ref input);
      return input;
    }
  }
}
