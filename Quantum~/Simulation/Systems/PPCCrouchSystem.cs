namespace Quantum {
  using Photon.Deterministic;

  /// <summary>
  /// Hold-to-crouch, ported from the Unity package's PlayerCrouch: pressing crouches, and releasing
  /// stands back up as soon as there's room (so walking out from under something stands you up).
  /// <list type="bullet">
  /// <item>On the ground the capsule shrinks with the feet planted; in mid-air it shrinks towards the
  /// head (legs tucked), optionally with an upward boost.</item>
  /// <item>The capsule stays centred on the entity; the entity moves by the height difference instead.
  /// That fixes the Unity bug where landing after a mid-air crouch left the capsule off-centre.</item>
  /// <item>Standing up checks the standing capsule for overlaps (Unity used the ceiling rays), and in
  /// mid-air tries extending down (feet) first, then up.</item>
  /// </list>
  /// </summary>
  public unsafe class PPCCrouchSystem : PPCSystemBase {
    static readonly FP Skin = FP.FromString("0.02");

    protected override void Update(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      if (c->Climb.IsClimbing) {
        if (c->Crouch.IsCrouching) TryStand(f, ref filter, config);
        return;
      }

      if (c->CrouchPressed && !c->Crouch.IsCrouching) {
        Crouch(f, ref filter, config);
      } else if (!c->Input.Crouch && c->Crouch.IsCrouching) {
        TryStand(f, ref filter, config);
      }
    }

    static void Crouch(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var delta = config.CrouchHeightDelta;
      var grounded = c->Ground.IsGrounded;

      // Grounded: keep the feet where they are. Airborne: keep the head, pull the feet up.
      filter.Transform->Position.Y += grounded ? -delta : delta;
      SetShape(f, ref filter, config, crouching: true);

      if (!grounded && config.Crouch.MidAirBoost > 0) {
        c->Vertical.AccumulatedY += config.Crouch.MidAirBoost;
      }
      c->Horizontal.SpeedMultiplier = config.Crouch.Speed / config.Movement.WalkSpeed;
      f.Events.PPCCrouchChanged(filter.Entity, true);
    }

    static void TryStand(Frame f, ref PPCFilter filter, PPCConfig config) {
      var c = filter.Character;
      var delta = config.CrouchHeightDelta;
      var position = filter.Transform->Position;

      // Grounded: grow upwards from the feet. Airborne: grow down to the feet first, else upwards.
      var up = position + FPVector3.Up * delta;
      var down = position - FPVector3.Up * delta;
      FPVector3 standAt;
      if (c->Ground.IsGrounded) {
        if (!Fits(f, ref filter, config, up)) return;
        standAt = up;
      } else if (Fits(f, ref filter, config, down)) {
        standAt = down;
      } else if (Fits(f, ref filter, config, up)) {
        standAt = up;
      } else {
        return;
      }

      filter.Transform->Position = standAt;
      SetShape(f, ref filter, config, crouching: false);
      c->Horizontal.SpeedMultiplier = FP._1;
      f.Events.PPCCrouchChanged(filter.Entity, false);
    }

    /// <summary>Is there room for the standing capsule at <paramref name="center"/>?</summary>
    static bool Fits(Frame f, ref PPCFilter filter, PPCConfig config, FPVector3 center) {
      // Slightly slimmer than the real capsule so touching the floor or a wall doesn't count.
      var shape = PPCCapsule.Shape(config, crouching: false, inset: Skin);
      return !PPCProbe.OverlapsOther(f, filter.Entity, center, shape, config.Probes.CeilingLayerMask);
    }

    static void SetShape(Frame f, ref PPCFilter filter, PPCConfig config, bool crouching) {
      filter.Character->Crouch.IsCrouching = crouching;
      filter.Collider->Shape = PPCCapsule.Shape(config, crouching);
      filter.Body->ResetCenterOfMass(f, filter.Entity);   // centre of mass first, then inertia
      filter.Body->ResetInertia(f, filter.Entity);
    }
  }
}
