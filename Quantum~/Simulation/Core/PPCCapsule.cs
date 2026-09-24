namespace Quantum {
  using Photon.Deterministic;

  /// <summary>Capsule helpers. Quantum capsules are radius + extent, where half-height = extent + radius.</summary>
  public static class PPCCapsule {
    public static Shape3D Shape(PPCConfig config, bool crouching, FPVector3 offset = default) {
      var radius = config.Body.Radius;
      var extent = FPMath.Max(FP._0, config.HalfHeight(crouching) - radius);
      return Shape3D.CreateCapsule(radius, extent, offset);
    }
  }
}
