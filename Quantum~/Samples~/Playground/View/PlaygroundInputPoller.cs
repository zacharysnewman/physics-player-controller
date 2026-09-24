namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;
  using UnityEngine.InputSystem;

  /// <summary>
  /// Polls keyboard/mouse and gamepad (Unity Input System) into the game's Quantum input, including the
  /// camera's yaw and pitch from <see cref="PPCCameraView"/>. Put one in the scene.
  /// </summary>
  public class PlaygroundInputPoller : MonoBehaviour {
    [Tooltip("Degrees per second at full stick deflection.")]
    public float GamepadLookSpeed = 180f;

    void OnEnable() {
      QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
    }

    void Update() {
      // Look every rendered frame for smooth camera motion; the latest yaw/pitch goes into each tick.
      var camera = PPCCameraView.Local;
      if (camera == null) return;
      var look = Vector2.zero;
      if (Mouse.current != null) look += Mouse.current.delta.ReadValue();
      if (Gamepad.current != null) look += Gamepad.current.rightStick.ReadValue() * (GamepadLookSpeed * Time.deltaTime / Mathf.Max(camera.Sensitivity, 1e-4f));
      camera.Look(look);
    }

    void PollInput(CallbackPollInput callback) {
      var input = new Quantum.Input();
      var move = Vector2.zero;
      bool jump = false, run = false, crouch = false;

      var keyboard = Keyboard.current;
      if (keyboard != null) {
        move.x += (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
        move.y += (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
        jump |= keyboard.spaceKey.isPressed;
        run |= keyboard.leftShiftKey.isPressed;
        crouch |= keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;
      }
      var gamepad = Gamepad.current;
      if (gamepad != null) {
        move += gamepad.leftStick.ReadValue();
        jump |= gamepad.buttonSouth.isPressed;
        run |= gamepad.leftStickButton.isPressed;
        crouch |= gamepad.buttonEast.isPressed;
      }

      input.Move = Vector2.ClampMagnitude(move, 1f).ToFPVector2();
      input.Jump = jump;
      input.Run = run;
      input.Crouch = crouch;

      var camera = PPCCameraView.Local;
      if (camera != null) {
        input.LookYaw = FP.FromFloat_UNSAFE(camera.Yaw);
        input.LookPitch = FP.FromFloat_UNSAFE(camera.Pitch);
      }
      callback.SetInput(input, DeterministicInputFlags.Repeatable);
    }
  }
}
