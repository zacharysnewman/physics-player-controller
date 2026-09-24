namespace Quantum {
  public partial struct PPCCharacter {
    /// <summary>Jump went down this tick.</summary>
    public bool JumpPressed => Input.Jump && !PreviousInput.Jump;

    /// <summary>Crouch went down this tick.</summary>
    public bool CrouchPressed => Input.Crouch && !PreviousInput.Crouch;
  }
}
