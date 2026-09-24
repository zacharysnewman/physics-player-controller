namespace Quantum {
  /// <summary>
  /// All controller systems in order, as one group: add a single <c>PPCSystemGroup</c> entry to your
  /// SystemsConfig asset (after Quantum's core systems) instead of listing each system.
  /// </summary>
  public class PPCSystemGroup : SystemGroup {
    public PPCSystemGroup() : base("PPC", PPCSystems.Create()) { }
  }
}
