namespace Quantum {
  using System.Collections.Generic;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// Drives an Animator from the character's simulation state, with the same parameter names as the
  /// Unity package's PlayerAnimatorController, so existing animator controllers keep working.
  /// Parameters the animator doesn't have are skipped.
  /// </summary>
  public unsafe class PPCAnimatorView : QuantumEntityViewComponent {
    public Animator Animator;
    [Tooltip("How quickly DirectionX/DirectionY follow the input (1/s).")]
    public float DirectionSmoothing = 10f;

    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int DirectionX = Animator.StringToHash("DirectionX");
    static readonly int DirectionY = Animator.StringToHash("DirectionY");
    static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    static readonly int IsFalling = Animator.StringToHash("IsFalling");
    static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
    static readonly int IsRunning = Animator.StringToHash("IsRunning");
    static readonly int IsSliding = Animator.StringToHash("IsSliding");
    static readonly int IsClimbing = Animator.StringToHash("IsClimbing");
    static readonly int Jump = Animator.StringToHash("Jump");

    readonly HashSet<int> _parameters = new HashSet<int>();
    Vector2 _direction;

    public override void OnActivate(Frame frame) {
      if (Animator == null) Animator = GetComponentInChildren<Animator>();
      _parameters.Clear();
      if (Animator != null) {
        foreach (var p in Animator.parameters) _parameters.Add(p.nameHash);
      }
      QuantumEvent.Subscribe<EventPPCJumped>(this, OnJumped);
    }

    public override void OnDeactivate() {
      QuantumEvent.UnsubscribeListener(this);
    }

    public override void OnUpdateView() {
      if (Animator == null || PredictedFrame == null) return;
      var frame = PredictedFrame;
      if (!frame.TryGet<PPCCharacter>(EntityRef, out var c) || !frame.TryGet<PhysicsBody3D>(EntityRef, out var body)) return;

      var relative = body.Velocity - c.Platform.BaseVelocity;
      var horizontalSpeed = new FPVector3(relative.X, 0, relative.Z).Magnitude.AsFloat;
      var move = new Vector2(c.Input.Move.X.AsFloat, c.Input.Move.Y.AsFloat);
      _direction = Vector2.Lerp(_direction, move, 1f - Mathf.Exp(-DirectionSmoothing * Time.deltaTime));

      SetFloat(Speed, horizontalSpeed * move.magnitude);
      SetFloat(DirectionX, _direction.x);
      SetFloat(DirectionY, _direction.y);
      SetBool(IsGrounded, c.Ground.IsGrounded);
      SetBool(IsFalling, c.State == PPCState.Falling);
      SetBool(IsCrouching, c.State == PPCState.Crouching);
      SetBool(IsRunning, c.State == PPCState.Running);
      SetBool(IsSliding, c.State == PPCState.Sliding);
      SetBool(IsClimbing, c.State == PPCState.Climbing);
    }

    void OnJumped(EventPPCJumped e) {
      if (e.Entity == EntityRef && _parameters.Contains(Jump)) Animator.SetTrigger(Jump);
    }

    void SetFloat(int id, float value) { if (_parameters.Contains(id)) Animator.SetFloat(id, value); }
    void SetBool(int id, bool value) { if (_parameters.Contains(id)) Animator.SetBool(id, value); }
  }
}
