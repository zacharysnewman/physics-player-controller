using UnityEngine;
using UnityEngine.InputSystem;

namespace ZacharysNewman.PPC
{
    public class PlayerInputHandler
    {
        private PlayerControls playerControls;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool RunInput { get; private set; }
        public bool CrouchInput { get; private set; }
        public bool JumpInput { get; private set; }
        public bool InteractInput { get; private set; }

        private System.Action menuAction;
        private System.Action useAction;

        public PlayerInputHandler(PlayerControls controls, System.Action menu, System.Action use)
        {
            playerControls = controls;
            menuAction = menu;
            useAction = use;

            // Set up input callbacks
            playerControls.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            playerControls.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

            playerControls.Player.Look.performed += ctx => LookInput = ctx.ReadValue<Vector2>();
            playerControls.Player.Look.canceled += ctx => LookInput = Vector2.zero;

            playerControls.Player.Run.performed += ctx => RunInput = true;
            playerControls.Player.Run.canceled += ctx => RunInput = false;

            playerControls.Player.Crouch.performed += ctx => CrouchInput = true;
            playerControls.Player.Crouch.canceled += ctx => CrouchInput = false;

            playerControls.Player.Jump.performed += ctx => JumpInput = true;
            playerControls.Player.Jump.canceled += ctx => JumpInput = false;

            playerControls.Player.Interact.performed += ctx => InteractInput = true;
            playerControls.Player.Interact.canceled += ctx => InteractInput = false;

            playerControls.Player.Menu.performed += ctx => menuAction?.Invoke();
            playerControls.Player.Use.performed += ctx => useAction?.Invoke();
        }

        public void Enable()
        {
            playerControls.Player.Enable();
        }

        public void Disable()
        {
            playerControls.Player.Disable();
        }

        public void Dispose()
        {
            playerControls.Dispose();
        }
    }
}