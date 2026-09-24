namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Jumping with a press buffer and coyote time, ported from the Unity package's PlayerJump. Timers
  /// run on simulation time (<c>f.DeltaTime</c>), so they're deterministic.
  /// <para>Fixes the Unity double jump: leaving the ground <i>by jumping</i> no longer starts coyote time.</para>
  /// </summary>
  public unsafe class PPCJumpSystem : PPCSystemBase {
    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var j = &c->Jump;
      var dt = f.DeltaTime;
      var grounded = c->Ground.IsGrounded;
      j->JumpedThisTick = false;

      if (j->BufferTimer > 0) j->BufferTimer -= dt;
      if (j->CoyoteTimer > 0) j->CoyoteTimer -= dt;

      if (!grounded && c->Ground.WasGrounded && !j->IsJumping) {
        j->CoyoteTimer = config.Jump.CoyoteTime;   // walked off an edge
      }
      if (grounded && j->IsJumping && filter.Body->Velocity.Y <= FP._0) {
        j->IsJumping = false;
      }

      if (c->Climb.IsClimbing) {
        return;   // jumping off a ladder is handled by PPCClimbSystem
      }

      var pressed = c->Input.Jump && !c->PreviousInput.Jump;
      if (pressed && j->BufferTimer <= 0) {
        j->BufferTimer = config.Jump.BufferTime;
      }

      if ((grounded || j->CoyoteTimer > 0) && j->BufferTimer > 0) {
        Perform(f, ref filter, config);
      }
    }

    static void Perform(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var velocity = config.Jump.Force / config.Body.Mass;

      // Keep any larger upward velocity already absorbed (e.g. from a launch pad).
      c->Vertical.AccumulatedY = FPMath.Max(c->Vertical.AccumulatedY, velocity + c->Vertical.PlatformY);
      c->Vertical.IsGrounded = false;

      c->Jump.BufferTimer = 0;
      c->Jump.CoyoteTimer = 0;
      c->Jump.IsJumping = true;
      c->Jump.JumpedThisTick = true;
      f.Events.PPCJumped(filter.Entity);
    }
  }
}
