namespace Quantum {
  /// <summary>
  /// Derives <see cref="PPCCharacter.State"/> from the controller's other components.
  /// Phase 0 stub: the inputs it reads (ground probes, jump, crouch, climb) arrive in Phases 1–6.
  /// </summary>
  public unsafe class PPCStateSystem : SystemMainThreadFilter<PPCStateSystem.Filter> {
    public struct Filter {
      public EntityRef Entity;
      public PPCCharacter* Character;
    }

    public override void Update(Frame f, ref Filter filter) {
      filter.Character->State = PPCState.Idle;
    }
  }
}
