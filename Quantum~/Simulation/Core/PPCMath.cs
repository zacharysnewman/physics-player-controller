namespace Quantum {
  using Photon.Deterministic;

  public static class PPCMath {
    /// <summary>The vector with its vertical component removed.</summary>
    public static FPVector3 Flat(this FPVector3 v) => new FPVector3(v.X, 0, v.Z);
  }
}
