namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Horizontal velocity layer, ported from the Unity package's PlayerMovement: camera-relative walk/run
  /// with acceleration, deceleration and faster reversal, worked out relative to the platform being
  /// stood on; automatic steps while grounded; and absorption of external horizontal forces, which then
  /// decay (friction on the ground, drag in the air).
  /// <para>
  /// Output is purely horizontal. Following slopes is the vertical layer's job (it keeps the combined
  /// velocity along the ground), which replaces the Unity version's partial slope alignment.
  /// </para>
  /// </summary>
  public unsafe class PPCMovementLayerSystem : PPCSystemBase {
    static readonly FP InputDeadzone = FP._0_10;
    static readonly FP ReverseDotThreshold = -FP._0_10;
    static readonly FP MinDirection = FP.FromString("0.01");
    static readonly FP MinStep = FP.FromString("0.01");

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var h = &c->Horizontal;
      var m = config.Movement;
      var dt = f.DeltaTime;
      var grounded = c->Ground.IsGrounded;

      if (c->Climb.IsClimbing) {
        return;   // the exclusive climb layer drives (and holds this layer; see PPCClimbSystem)
      }

      // External forces: whatever moved the body away from what we drove it towards last tick.
      var actual = filter.Body->Velocity.Flat();
      var externalDelta = actual - h->Contribution;
      if (externalDelta.Magnitude > m.ExternalAbsorbThreshold) {
        h->External += externalDelta;
      }
      h->External = grounded
        ? FPVector3.MoveTowards(h->External, FPVector3.Zero, m.GroundExternalFriction * dt)
        : h->External * FPMath.Exp(-m.AirExternalDrag * dt);

      var input = c->Input;
      var moveDirection = input.MoveDirection;

      if (grounded && moveDirection.Magnitude > MinDirection) {
        TryStep(f, ref filter, config, moveDirection);
      }

      var speed = (input.Run ? m.RunSpeed : m.WalkSpeed) * h->SpeedMultiplier;
      var playerTarget = moveDirection * speed;

      // Accelerate in the platform's frame so standing on a moving platform needs no input.
      var baseHorizontal = c->Platform.BaseVelocity.Flat();
      var relativeVelocity = h->Current - baseHorizontal;
      var relativeDelta = FPVector3.ClampMagnitude(playerTarget - relativeVelocity, m.MaxVelocityChange);

      var dot = relativeVelocity.Magnitude > MinDirection && playerTarget.Magnitude > MinDirection
        ? FPVector3.Dot(relativeVelocity.Normalized, playerTarget.Normalized)
        : FP._1;

      FP accelRate;
      if (input.Move.Magnitude > InputDeadzone) {
        accelRate = dot < ReverseDotThreshold ? m.ReverseDeceleration : m.Acceleration;
      } else {
        accelRate = m.Deceleration;
      }
      if (!grounded) {
        accelRate *= m.AirControl;
      }

      var change = FPVector3.MoveTowards(FPVector3.Zero, relativeDelta, accelRate * dt);
      h->Current = relativeVelocity + change + baseHorizontal;
      h->Contribution = h->Current + h->External;
    }

    /// <summary>
    /// Lifts the character onto a step just ahead (Unity: HandleStep). Uses the real capsule height;
    /// the Unity version assumed a 2 m capsule.
    /// </summary>
    static void TryStep(Frame f, ref PPCFilter filter, PPCConfig config, FPVector3 moveDirection) {
      var m = config.Movement;
      var c = filter.Character;
      var center = PPCProbe.CapsuleCenter(filter.Transform, filter.Collider);
      var halfHeight = config.HalfHeight(c->Crouch.IsCrouching);
      var flatDirection = moveDirection.Flat();
      if (flatDirection.Magnitude < MinDirection) {
        return;
      }
      var origin = center + flatDirection.Normalized * (config.Body.Radius + m.StepProbeDistance);
      if (!PPCProbe.Raycast(f, filter.Entity, origin, FPVector3.Down, halfHeight * 2, config.Probes.GroundLayerMask,
                            out var hit, out _)) {
        return;
      }
      var feetY = center.Y - halfHeight;
      var stepHeight = hit.Point.Y - feetY;
      if (stepHeight > MinStep && stepHeight <= m.MaxStepHeight &&
          PPCProbe.SlopeAngle(hit.Normal) <= config.Probes.MaxSlopeAngle) {
        filter.Transform->Position.Y += stepHeight;
      }
    }
  }
}
