namespace Quantum {
  // Maps the game's input onto the controller's (see PPCInputBridge in the package).
  public static unsafe partial class PPCInputBridge {
    static partial void ReadPlayerInput(Frame f, PlayerRef player, ref PPCInput input) {
      var i = f.GetPlayerInput(player);
      input.Move = i->Move;
      input.LookYaw = i->LookYaw;
      input.LookPitch = i->LookPitch;
      input.Jump = i->Jump.IsDown;
      input.Run = i->Run.IsDown;
      input.Crouch = i->Crouch.IsDown;
    }
  }
}
