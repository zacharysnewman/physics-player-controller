namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// Optional on-screen state and probe gizmos for a character (the Unity package's DebugVisualizer and
  /// state label). Add it to the entity view prefab only while developing; nothing depends on it.
  /// </summary>
  public unsafe class PPCDebugView : QuantumEntityViewComponent {
    public bool ShowLabel = true;
    public bool ShowGizmos = true;
    public Vector2 LabelPosition = new Vector2(10, 10);

    PPCCharacter _character;
    bool _hasCharacter;

    public override void OnUpdateView() {
      _hasCharacter = PredictedFrame != null && PredictedFrame.TryGet(EntityRef, out _character);
    }

    void OnGUI() {
      if (!ShowLabel || !_hasCharacter) return;
      var c = _character;
      GUI.Label(new Rect(LabelPosition.x, LabelPosition.y, 600, 20),
        $"State: {c.State}  Grounded: {c.Ground.IsGrounded}  Slope: {c.Ground.SlopeAngle.AsFloat:F0}°  " +
        $"Target: {c.TargetVelocity.ToUnityVector3():F2}  Platform: {c.Platform.BaseVelocity.ToUnityVector3():F2}");
    }

    void OnDrawGizmos() {
      if (!ShowGizmos || !_hasCharacter) return;
      var c = _character;
      var p = transform.position;
      Gizmos.color = c.Ground.IsGrounded ? Color.green : Color.red;
      Gizmos.DrawLine(p, p + c.Ground.Normal.ToUnityVector3());
      Gizmos.color = Color.cyan;
      Gizmos.DrawLine(p, p + c.TargetVelocity.ToUnityVector3() * 0.25f);
      if (c.Ground.IsTouchingWall) {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p, p + c.Ground.WallNormal.ToUnityVector3());
      }
    }
  }
}
