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
    }

    public override void OnLateUpdateView() {
      if (!IsLocal || Camera == null) return;
      Camera.transform.SetPositionAndRotation(
        EntityView.transform.position + Vector3.up * _eyeHeight,
        Quaternion.Euler(-Pitch, Yaw, 0f));
    }

    float TargetEyeHeight(Frame frame) {
      if (!frame.TryGet<PPCCharacter>(EntityRef, out var character)) return 0f;
      var config = frame.FindAsset(character.Config);
      if (config == null) return 0f;
      return config.HalfHeight(character.Crouch.IsCrouching).AsFloat - EyeBelowTop;
    }
  }
}
