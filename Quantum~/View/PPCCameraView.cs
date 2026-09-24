namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// First-person camera for the local player's character, the Quantum counterpart of the Unity
  /// package's CameraController. Put it on the character's entity view prefab. It only activates for a
  /// character linked (<see cref="PPCPlayerLink"/>) to a local player.
  /// <para>
  /// The simulation moves the character relative to the camera, so the camera's yaw and pitch must go
  /// into the game's input: read <see cref="Yaw"/> and <see cref="Pitch"/> from <see cref="Local"/> in
  /// your input poller, and feed mouse/stick deltas to <see cref="Look"/>.
  /// </para>
  /// </summary>
  public unsafe class PPCCameraView : QuantumEntityViewComponent {
    [Tooltip("Camera to drive. Defaults to Camera.main.")]
    public Camera Camera;
    [Tooltip("Degrees per unit of look input.")]
    public float Sensitivity = 0.1f;
    public bool InvertY;
    public float MinPitch = -80f;
    public float MaxPitch = 80f;
    [Tooltip("Eye height below the top of the capsule (m).")]
    public float EyeBelowTop = 0.15f;
    [Tooltip("How quickly the eye follows crouch height changes (1/s). The simulation's crouch is instant.")]
    public float HeightSmoothing = 10f;
    [Tooltip("Smooth the camera over steps (the simulation lifts the character onto a step, or snaps it down one, in a single tick).")]
    public bool SmoothSteps = true;
    [Tooltip("Slowest speed the camera catches up with a step (m/s); it also keeps up with the character's horizontal speed. Source: 150 units/s.")]
    public float StepSmoothSpeed = 3.8f;
    [Tooltip("The camera never lags more than this behind the character (m). Source: its step size, 18 units.")]
    public float MaxStepLag = 0.45f;
    public bool LockCursor = true;

    /// <summary>The camera of the (first) local player's character, if any.</summary>
    public static PPCCameraView Local { get; private set; }

    /// <summary>Camera yaw in degrees (world). Send as <c>PPCInput.LookYaw</c>.</summary>
    public float Yaw { get; private set; }
    /// <summary>Camera pitch in degrees, positive = looking up. Send as <c>PPCInput.LookPitch</c>.</summary>
    public float Pitch { get; private set; }
    public bool IsLocal { get; private set; }

    float _eyeHeight;
    int _lastFrame = -1;
    float _feetY;             // smoothed feet height for step smoothing
    bool _smoothStepsNow;     // grounded on static ground this frame
    float _catchUpSpeed;

    /// <summary>Applies a look delta (mouse counts or stick × time).</summary>
    public void Look(Vector2 delta) {
      Yaw = Mathf.Repeat(Yaw + delta.x * Sensitivity, 360f);
      Pitch = Mathf.Clamp(Pitch + (InvertY ? -delta.y : delta.y) * Sensitivity, MinPitch, MaxPitch);
    }

    public override void OnActivate(Frame frame) {
      IsLocal = frame.TryGet<PPCPlayerLink>(EntityRef, out var link) && Game.PlayerIsLocal(link.Player);
      if (!IsLocal) return;

      Local = this;
      if (Camera == null) Camera = Camera.main;
      if (frame.TryGet<PPCCharacter>(EntityRef, out var character)) {
        Yaw = character.Input.LookYaw.AsFloat;
        Pitch = character.Input.LookPitch.AsFloat;
      }
      _eyeHeight = TargetEyeHeight(frame);
      _feetY = FeetY(frame);
      if (LockCursor) {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
      }
    }

    public override void OnDeactivate() {
      if (Local == this) Local = null;
      IsLocal = false;
    }

    public override void OnUpdateView() {
      if (!IsLocal || PredictedFrame == null) return;
      var frame = PredictedFrame;

      // Turn with rotating platforms (Unity: PlatformYawOffset).
      if (frame.TryGet<PPCCharacter>(EntityRef, out var character) && _lastFrame >= 0 && frame.Number > _lastFrame) {
        var ticks = Mathf.Min(frame.Number - _lastFrame, 10);
        Yaw = Mathf.Repeat(Yaw + character.Platform.YawDelta.AsFloat * ticks, 360f);
      }
      _lastFrame = frame.Number;

      _eyeHeight = Mathf.Lerp(_eyeHeight, TargetEyeHeight(frame), 1f - Mathf.Exp(-HeightSmoothing * Time.deltaTime));

      // Step smoothing (Source: C_BasePlayer::SmoothViewOnStairs): only on the ground, and only on static
      // ground, so lifts and jumps aren't smoothed.
      _smoothStepsNow = SmoothSteps && character.Ground.IsGrounded && !character.Climb.IsClimbing &&
                        character.Platform.GroundVelocity == Photon.Deterministic.FPVector3.Zero;
      var velocity = character.TargetVelocity;
      _catchUpSpeed = Mathf.Max(StepSmoothSpeed, new Vector2(velocity.X.AsFloat, velocity.Z.AsFloat).magnitude);
    }

    public override void OnLateUpdateView() {
      if (!IsLocal || Camera == null) return;
      var position = EntityView.transform.position;
      var feet = PredictedFrame != null ? FeetY(PredictedFrame, position.y) : position.y;
      if (_smoothStepsNow) {
        _feetY = Mathf.MoveTowards(_feetY, feet, _catchUpSpeed * Time.deltaTime);
        _feetY = Mathf.Clamp(_feetY, feet - MaxStepLag, feet + MaxStepLag);
      } else {
        _feetY = feet;
      }
      Camera.transform.SetPositionAndRotation(
        position + Vector3.up * (_eyeHeight + _feetY - feet),
        Quaternion.Euler(-Pitch, Yaw, 0f));
    }

    float HalfHeight(Frame frame) =>
      frame.TryGet<PPCCharacter>(EntityRef, out var character)
        ? PPCConfig.Resolve(frame, character.Config).HalfHeight(character.Crouch.IsCrouching).AsFloat
        : 0f;

    // Feet height from the view's (interpolated) centre, so crouching, which moves the centre, isn't a "step".
    float FeetY(Frame frame, float centerY) => centerY - HalfHeight(frame);
    float FeetY(Frame frame) => FeetY(frame, EntityView.transform.position.y);

    float TargetEyeHeight(Frame frame) {
      if (!frame.TryGet<PPCCharacter>(EntityRef, out var character)) return 0f;
      return PPCConfig.Resolve(frame, character.Config).HalfHeight(character.Crouch.IsCrouching).AsFloat - EyeBelowTop;
    }
  }
}
