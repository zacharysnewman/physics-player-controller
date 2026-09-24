namespace Quantum {
  using System;
  using Photon.Deterministic;

  /// <summary>
  /// All tuning for a Physics Player Controller character. Sections mirror the Unity package's
  /// ScriptableObjects (PlayerMovementConfig, GroundCheckerConfig, PlayerJumpConfig, ...).
  /// A character with no config uses <see cref="Default"/>.
  /// <para>
  /// The defaults are the recommended feel (<see cref="ApplyRecommendedFeel"/>), derived from the Source
  /// engine's movement (Source SDK 2013 defaults, 1 unit = 1 inch, scaled to a 2 m character) and
  /// cross-checked against Halo 3. The Unity package's original numbers are one call (or right-click on
  /// the asset) away: <see cref="ApplyUnityParity"/>.
  /// </para>
  /// </summary>
  [Serializable]
#if QUANTUM_UNITY
  [UnityEngine.CreateAssetMenu(menuName = "Quantum/Physics Player Controller/Character Config", fileName = "PPCConfig")]
#endif
  public partial class PPCConfig : AssetObject {
    public BodySettings Body = new BodySettings();
    public MovementSettings Movement = new MovementSettings();
    public ProbeSettings Probes = new ProbeSettings();
    public JumpSettings Jump = new JumpSettings();
    public CrouchSettings Crouch = new CrouchSettings();
    public ClimbSettings Climb = new ClimbSettings();
    [Tooltip("Internal tuning. The defaults suit almost every game.")]
    public AdvancedSettings Advanced = new AdvancedSettings();

    /// <summary>
    /// Settings used by characters whose <c>PPCCharacter.Config</c> isn't set. Shared and read-only:
    /// don't modify it; create a config asset instead.
    /// </summary>
    public static readonly PPCConfig Default = new PPCConfig();

    /// <summary>The config behind <paramref name="config"/>, or <see cref="Default"/> if it isn't set.</summary>
    public static PPCConfig Resolve(Frame f, AssetRef<PPCConfig> config) =>
      config.IsValid && f.TryFindAsset(config, out PPCConfig found) ? found : Default;

    [Serializable]
    public class BodySettings {
      [Tooltip("Capsule radius (m).")]
      public FP Radius = FP._0_50;
      [Tooltip("Total capsule height when standing (m). The entity's position is the capsule centre.")]
      public FP StandingHeight = FP._2;
      [Tooltip("Body mass (kg). Affects how the character pushes and is pushed by other bodies.")]
      public FP Mass = FP._1;
      [Tooltip("Physics layer of the character's collider.")]
      public int Layer = 0;
      [Tooltip("Physics material for the character. Leave empty for a frictionless material (recommended: friction slows sliding along walls).")]
      public AssetRef<PhysicsMaterial> Material;
      [Tooltip("Multiplier on the physics gravity for the vertical layer. 2 × Quantum's default -10 = 20 m/s², Source's 800 units/s².")]
      public FP GravityScale = FP._2;
    }

    [Serializable]
    public class MovementSettings {
      [Tooltip("Walk speed (m/s). Source: Half-Life 2 run speed, 190 units/s ≈ 4.8 m/s.")]
      public FP WalkSpeed = 5;
      [Tooltip("Run speed (m/s). Source: HL2 sprint, 320 units/s ≈ 8.1 m/s; Halo 3 base speed ≈ 7.7 m/s.")]
      public FP RunSpeed = 8;
      [Tooltip("Acceleration towards the target velocity while there is move input (m/s²). Source: sv_accelerate 10 × speed.")]
      public FP Acceleration = 50;
      [Tooltip("Deceleration when there is no move input (m/s²). Source: sv_friction 4 with sv_stopspeed 100 stops from walk speed in ~0.4 s.")]
      public FP Deceleration = 12;
      [Tooltip("Acceleration used when input points against the current velocity (m/s²). Source: friction plus acceleration.")]
      public FP ReverseDeceleration = 60;
      [Tooltip("Scales acceleration while airborne. 1 = same as on the ground.")]
      public FP AirControl = FP.FromString("0.2");
      [Tooltip("Tallest step the character climbs automatically (m). Source: sv_stepsize 18 units ≈ 0.46 m.")]
      public FP MaxStepHeight = FP.FromString("0.45");
      [Tooltip("Exponential horizontal drag on external velocity while airborne (fraction lost per second). 0 = momentum is kept until landing, as in Source and Halo.")]
      public FP AirExternalDrag = 0;
      [Tooltip("Linear deceleration of external horizontal velocity while grounded (m/s²).")]
      public FP GroundExternalFriction = 15;
      [Tooltip("Standing on another character moves you with it, like a moving platform. Off: you can stand on heads, but they move out from under you.")]
      public bool CarriedByCharacters = false;
    }

    [Serializable]
    public class ProbeSettings {
      [Tooltip("How far below the feet the ground is detected while airborne (m). While grounded the probe reaches down Movement.MaxStepHeight, to stay on the ground going down steps.")]
      public FP GroundProbeMargin = FP.FromString("0.15");
      [Tooltip("How far above the head a ceiling is detected (m).")]
      public FP CeilingProbeMargin = FP._0_10;
      [Tooltip("Steepest walkable slope (degrees).")]
      public FP MaxSlopeAngle = 45;
      public int GroundLayerMask = -1;
      public int CeilingLayerMask = -1;
    }

    [Serializable]
    public class JumpSettings {
      [Tooltip("Jump apex above the take-off point (m), with this config's gravity.")]
      public FP Height = FP.FromString("1.25");
      [Tooltip("A press is remembered this long before landing (s).")]
      public FP BufferTime = FP.FromString("0.2");
      [Tooltip("Jumping is still allowed this long after walking off an edge (s).")]
      public FP CoyoteTime = FP.FromString("0.1");
    }

    [Serializable]
    public class CrouchSettings {
      public FP Height = FP._1;
      [Tooltip("Movement speed while crouched (m/s). Running has no effect while crouched. Source: ⅓ of run speed.")]
      public FP Speed = FP.FromString("1.6");
      [Tooltip("Upward velocity added when crouching in mid-air (m/s). 0 = off.")]
      public FP MidAirBoost = 0;
    }

    [Serializable]
    public class ClimbSettings {
      public FP Speed = 3;
      [Tooltip("Layers searched for ladder triggers (entities with a PPCLadder component).")]
      public int LayerMask = -1;
      [Tooltip("Looking down more than this (degrees) makes forward input climb down.")]
      public FP LookDownThreshold = 30;
      [Tooltip("Velocity applied when jumping off a ladder: Y = up, Z = away from the ladder (m/s).")]
      public FPVector3 JumpOffVelocity = new FPVector3(0, 4, 3);
      [Tooltip("How strongly the character is pulled onto the ladder's centre line (1/s). 0 = off.")]
      public FP SnapStrength = 10;
    }

    [Serializable]
    public class AdvancedSettings {
      [Tooltip("How far ahead of the capsule edge to look for steps (m).")]
      public FP StepProbeDistance = FP.FromString("0.01");
      [Tooltip("Velocity deviations smaller than this are treated as numerical noise, not external forces (m/s).")]
      public FP ExternalAbsorbThreshold = FP.FromString("0.01");
      [Tooltip("Radius of the ring of ground/ceiling rays, as a fraction of the capsule radius.")]
      public FP ProbeRingRadius = FP.FromString("0.9");
      [Tooltip("Wall ray length beyond the probe ring (m).")]
      public FP WallCheckDistance = FP.FromString("0.16");
      [Tooltip("Cap on how fast the camera turns with a rotating platform (deg/s).")]
      public FP MaxPlatformYawSpeed = 360;
    }

    /// <summary>The recommended feel: the defaults for speed, acceleration, air control, step and jump.</summary>
#if QUANTUM_UNITY
    [UnityEngine.ContextMenu("Apply Recommended Feel")]
#endif
    public void ApplyRecommendedFeel() => ApplyFeel(new BodySettings(), new MovementSettings(), new JumpSettings(), new CrouchSettings());

    /// <summary>
    /// The Unity package's original numbers: run 10 m/s, 10 m/s² acceleration, full air control, 0.5 m
    /// steps, 5 m jumps, gravity × 1, air drag 0.5 on pushes, crouch 2 m/s. (Behaviour differences listed
    /// in the changelog, such as crouch ignoring run, still apply.)
    /// </summary>
#if QUANTUM_UNITY
    [UnityEngine.ContextMenu("Apply Unity Parity")]
#endif
    public void ApplyUnityParity() => ApplyFeel(
      new BodySettings { GravityScale = FP._1 },
      new MovementSettings {
        WalkSpeed = 5, RunSpeed = 10, Acceleration = 10, Deceleration = 10, ReverseDeceleration = 20,
        AirControl = FP._1, MaxStepHeight = FP._0_50, AirExternalDrag = FP._0_50, GroundExternalFriction = 15,
      },
      new JumpSettings { Height = 5 },
      new CrouchSettings { Speed = 2 });

    void ApplyFeel(BodySettings body, MovementSettings movement, JumpSettings jump, CrouchSettings crouch) {
      Body.GravityScale = body.GravityScale;
      Movement.AirExternalDrag = movement.AirExternalDrag;
      Movement.GroundExternalFriction = movement.GroundExternalFriction;
      Crouch.Speed = crouch.Speed;
      Movement.WalkSpeed = movement.WalkSpeed;
      Movement.RunSpeed = movement.RunSpeed;
      Movement.Acceleration = movement.Acceleration;
      Movement.Deceleration = movement.Deceleration;
      Movement.ReverseDeceleration = movement.ReverseDeceleration;
      Movement.AirControl = movement.AirControl;
      Movement.MaxStepHeight = movement.MaxStepHeight;
      Jump.Height = jump.Height;
#if UNITY_EDITOR
      UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public FP HalfHeight(bool crouching) => (crouching ? Crouch.Height : Body.StandingHeight) / 2;

    /// <summary>How far the capsule centre moves when crouching or standing up.</summary>
    public FP CrouchHeightDelta => HalfHeight(false) - HalfHeight(true);

    /// <summary>Take-off speed that reaches <see cref="JumpSettings.Height"/> under this config's gravity.</summary>
    public unsafe FP JumpVelocity(Frame f) {
      var gravity = FPMath.Abs(f.PhysicsSceneSettings->Gravity.Y * Body.GravityScale);
      return FPMath.Sqrt(2 * gravity * Jump.Height);
    }
  }
}
