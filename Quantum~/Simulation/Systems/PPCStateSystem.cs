namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Derives <see cref="PPCCharacter.State"/>, ported from the Unity PlayerController state machine.
  /// Priority: Climbing, Crouching, airborne (Jumping/Falling), then Running/Walking/Idle.
  /// </summary>
  public unsafe class PPCStateSystem : PPCSystemBase {
    static readonly FP MovingSpeed = FP._0_10;

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var velocity = filter.Body->Velocity;

      PPCState state;
      if (c->Climb.IsClimbing) {
        state = PPCState.Climbing;
      } else if (c->Crouch.IsCrouching) {
        state = PPCState.Crouching;
      } else if (!c->Ground.IsGrounded) {
        state = velocity.Y > FP._0 || c->Jump.IsJumping ? PPCState.Jumping : PPCState.Falling;
      } else if (velocity.Flat().Magnitude > MovingSpeed) {
        state = c->Input.Run ? PPCState.Running : PPCState.Walking;
      } else {
        state = PPCState.Idle;
      }
      c->State = state;
    }
  }
}
