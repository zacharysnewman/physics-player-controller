namespace Quantum {
  using Photon.Deterministic;

  /// <summary>Capsule helpers. Quantum capsules are radius + extent, where half-height = extent + radius.</summary>
  public static class PPCCapsule {
    /// <param name="inset">Shrinks the capsule on all sides (e.g. so touching surfaces don't count as overlapping).</param>
    public static Shape3D Shape(PPCConfig config, bool crouching, FP inset = default, FPVector3 offset = default) {
      var radius = config.Body.Radius;
      var extent = FPMath.Max(FP._0, config.HalfHeight(crouching) - radius - inset);
      return Shape3D.CreateCapsule(radius - inset, extent, offset);
    }
  }
}
