namespace Quantum {
  using Photon.Deterministic;

  public partial struct PPCInput {
    /// <summary>Rotation of the camera about the vertical axis.</summary>
    public FPQuaternion YawRotation => FPQuaternion.Euler(0, LookYaw, 0);

    /// <summary>The camera's horizontal right vector.</summary>
    public FPVector3 CameraRight => YawRotation * FPVector3.Right;

    /// <summary>Move input as a horizontal, camera-relative direction (length ≤ 1).</summary>
    public FPVector3 MoveDirection {
      get {
        var yaw = YawRotation;
        return yaw * FPVector3.Forward * Move.Y + yaw * FPVector3.Right * Move.X;
      }
    }
  }
}
