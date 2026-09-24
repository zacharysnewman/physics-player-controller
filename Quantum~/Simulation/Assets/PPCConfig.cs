namespace Quantum {
  using System;
  using Photon.Deterministic;

  /// <summary>
  /// All tuning for a Physics Player Controller character. Sections mirror the Unity package's
  /// ScriptableObjects (PlayerMovementConfig, GroundCheckerConfig, PlayerJumpConfig, ...) with the
  /// same defaults, converted to fixed point.
  /// </summary>
  [Serializable]
  public partial class PPCConfig : AssetObject {
    public BodySettings Body = new BodySettings();
    public MovementSettings Movement = new MovementSettings();
    public ProbeSettings Probes = new ProbeSettings();
    public JumpSettings Jump = new JumpSettings();
    public CrouchSettings Crouch = new CrouchSettings();
    public ClimbSettings Climb = new ClimbSettings();
    public PlatformSettings Platforms = new PlatformSettings();

    [Serializable]
    public class BodySettings {
      [Tooltip("Capsule radius (m).")]
      public FP Radius = FP._0_50;
      [Tooltip("Total capsule height when standing (m). The entity's position is the capsule centre.")]
      public FP StandingHeight = FP._2;
      [Tooltip("Body mass (kg). Jump velocity is Jump.Force / Mass.")]
      public FP Mass = FP._1;
      [Tooltip("Physics layer of the character's collider.")]
      public int Layer = 0;
      [Tooltip("Physics material for the character. Leave empty for the simulation default; a frictionless material is recommended so floor friction doesn't fight the controller.")]
      public AssetRef<PhysicsMaterial> Material;
      [Tooltip("Multiplier on the physics gravity for the vertical layer.")]
      public FP GravityScale = FP._1;
    }

    [Serializable]
    public class MovementSettings {
      public FP WalkSpeed = 5;
      public FP RunSpeed = 10;
      [Tooltip("Acceleration towards the target velocity while there is move input (m/s²).")]
      public FP Acceleration = 10;
      [Tooltip("Deceleration when there is no move input (m/s²).")]
      public FP Deceleration = 10;
      [Tooltip("Acceleration used when input points against the current velocity (m/s²).")]
      public FP ReverseDeceleration = 20;
      [Tooltip("Largest velocity change considered per tick (m/s).")]
      public FP MaxVelocityChange = 10;
      [Tooltip("Scales acceleration while airborne. 1 = same as on the ground (the Unity package's behaviour).")]
      public FP AirControl = FP._1;
      [Tooltip("Tallest step the character climbs automatically (m).")]
      public FP MaxStepHeight = FP._0_50;
      [Tooltip("How far ahead of the capsule edge to look for steps (m).")]
      public FP StepProbeDistance = FP.FromString("0.01");
      [Tooltip("Exponential horizontal drag on external velocity while airborne (fraction lost per second).")]
      public FP AirExternalDrag = FP._0_50;
      [Tooltip("Linear deceleration of external horizontal velocity while grounded (m/s²).")]
      public FP GroundExternalFriction = 15;
      [Tooltip("Velocity deviations smaller than this are treated as numerical noise, not external forces (m/s).")]
      public FP ExternalAbsorbThreshold = FP.FromString("0.01");
    }

    [Serializable]
    public class ProbeSettings {
      [Tooltip("Ground ray length measured from the capsule centre (m). Default = half height + 0.15.")]
      public FP GroundCheckDistance = FP.FromString("1.15");
      public int GroundLayerMask = -1;
      [Tooltip("Radius of the ring of ground/ceiling rays, as a fraction of the capsule radius.")]
      public FP RadiusMultiplier = FP.FromString("0.9");
      [Tooltip("Ceiling ray length measured from the capsule centre (m). Default = half height + 0.1.")]
      public FP CeilingCheckDistance = FP.FromString("1.1");
      public int CeilingLayerMask = -1;
      [Tooltip("Steepest walkable slope (degrees).")]
      public FP MaxSlopeAngle = 45;
      [Tooltip("Wall ray length beyond the ring radius (m).")]
      public FP WallCheckDistance = FP.FromString("0.16");
    }

    [Serializable]
    public class JumpSettings {
      [Tooltip("Jump impulse (N·s). Velocity = Force / Body.Mass.")]
      public FP Force = 10;
      [Tooltip("A press is remembered this long before landing (s).")]
      public FP BufferTime = FP.FromString("0.2");
      [Tooltip("Jumping is still allowed this long after walking off an edge (s).")]
      public FP CoyoteTime = FP.FromString("0.1");
    }

    [Serializable]
    public class CrouchSettings {
      public FP Height = FP._1;
      public FP Speed = 2;
      [Tooltip("Upward velocity added when crouching in mid-air (m/s). 0 = off.")]
      public FP MidAirBoost = 0;
    }

    [Serializable]
    public class ClimbSettings {
      public FP Speed = 3;
      [Tooltip("Layers searched for ladder triggers (entities with a PPCLadder component).")]
      public int LayerMask = -1;
      [Tooltip("Looking down more than this (degrees) makes forward input climb down. The Unity version flipped at exactly level.")]
      public FP LookDownThreshold = 30;
      [Tooltip("Velocity applied when jumping off a ladder: Y = up, Z = away from the ladder (m/s).")]
      public FPVector3 JumpOffVelocity = new FPVector3(0, 4, 3);
      [Tooltip("How strongly the character is pulled onto the ladder's centre line (1/s). 0 = off.")]
      public FP SnapStrength = 10;
    }

    [Serializable]
    public class PlatformSettings {
      [Tooltip("Multiplier on the platform velocity the character inherits.")]
      public FP VelocityMultiplier = FP._1;
      [Tooltip("Cap on how fast the character's yaw follows a rotating platform (deg/s).")]
      public FP MaxRotationSpeed = 360;
    }

    public FP HalfHeight(bool crouching) => (crouching ? Crouch.Height : Body.StandingHeight) / 2;
  }
}
