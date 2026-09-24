namespace Quantum {
  /// <summary>Components every controller system works on.</summary>
  public unsafe struct PPCFilter {
    public EntityRef Entity;
    public Transform3D* Transform;
    public PhysicsBody3D* Body;
    public PhysicsCollider3D* Collider;
    public PPCCharacter* Character;
  }

  /// <summary>Base for controller systems: runs once per character with its config resolved.</summary>
  public abstract unsafe class PPCSystemBase : SystemMainThreadFilter<PPCFilter> {
    public sealed override void Update(Frame f, ref PPCFilter filter) {
      var config = f.FindAsset(filter.Character->Config);
      if (config == null) {
        return;
      }
      Update(f, ref filter, config);
    }

    protected abstract void Update(Frame f, ref PPCFilter filter, PPCConfig config);
  }
}
