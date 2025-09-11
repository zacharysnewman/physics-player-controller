using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private PlayerInput playerInput;

        [Header("Settings")]
        [SerializeField] private float groundDebounceTime = 0.1f;
        [SerializeField] private float directionSmoothingSpeed = 5f;

        [Header("Debug")]
        [SerializeField] private bool showParameterDebug = true;

        private Animator animator;
        private bool lastIsGrounded;
        private float debounceTimer;
        private float currentDirectionX;
        private float currentDirectionY;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            if (playerController == null)
            {
                Debug.LogError("PlayerAnimatorController: PlayerController reference is required!");
                enabled = false;
                return;
            }

            // Auto-assign if not set
            if (playerRigidbody == null) playerRigidbody = playerController.GetComponent<Rigidbody>();
            if (playerInput == null) playerInput = playerController.GetPlayerInput();
        }

        private void Start()
        {
            // Subscribe to jump event after all Awake have run
            if (playerController.GetPlayerJump() != null)
            {
                playerController.GetPlayerJump().OnJump.AddListener(() => animator.SetTrigger("Jump"));
            }
            else
            {
                Debug.LogError("PlayerAnimatorController: PlayerJump component not found on PlayerController!");
            }
        }

        private void LateUpdate()
        {
            if (playerController == null || animator == null || playerRigidbody == null || playerInput == null) return;

            // Sync Animator parameters based on PlayerController state
            Vector3 horizontalVelocity = new Vector3(playerRigidbody.linearVelocity.x, 0, playerRigidbody.linearVelocity.z);
            animator.SetFloat("Speed", horizontalVelocity.magnitude);

            // Smooth movement direction from input (camera-relative)
            currentDirectionX = Mathf.Lerp(currentDirectionX, playerInput.MoveInput.x, Time.deltaTime * directionSmoothingSpeed);
            currentDirectionY = Mathf.Lerp(currentDirectionY, playerInput.MoveInput.y, Time.deltaTime * directionSmoothingSpeed);
            animator.SetFloat("DirectionX", currentDirectionX);
            animator.SetFloat("DirectionY", currentDirectionY);

            // Debounced IsGrounded
            bool currentIsGrounded = playerController.CurrentState != PlayerController.PlayerState.Jumping && playerController.CurrentState != PlayerController.PlayerState.Falling;
            if (currentIsGrounded != lastIsGrounded)
            {
                if (currentIsGrounded)
                {
                    // Immediately set to true
                    animator.SetBool("IsGrounded", true);
                    debounceTimer = 0;
                }
                else
                {
                    // Start debounce for setting to false
                    debounceTimer = groundDebounceTime;
                }
            }
            if (debounceTimer > 0)
            {
                debounceTimer -= Time.deltaTime;
                if (debounceTimer <= 0)
                {
                    animator.SetBool("IsGrounded", false);
                }
            }
            lastIsGrounded = currentIsGrounded;

            // Other bools
            animator.SetBool("IsFalling", playerController.IsFalling);
            animator.SetBool("IsCrouching", playerController.CurrentState == PlayerController.PlayerState.Crouching);
            animator.SetBool("IsRunning", playerController.CurrentState == PlayerController.PlayerState.Running);
            animator.SetBool("IsSliding", playerController.CurrentState == PlayerController.PlayerState.Sliding);
            animator.SetBool("IsClimbing", playerController.CurrentState == PlayerController.PlayerState.Climbing);




        }

        private void OnGUI()
        {
            if (showParameterDebug && animator != null)
            {
                GUI.Label(new Rect(10, 50, 550, 20), $"Anim - Speed: {animator.GetFloat("Speed"):F2}, DirX: {animator.GetFloat("DirectionX"):F2}, DirY: {animator.GetFloat("DirectionY"):F2}, IsGrounded: {animator.GetBool("IsGrounded")}, IsFalling: {animator.GetBool("IsFalling")}, IsCrouching: {animator.GetBool("IsCrouching")}, IsRunning: {animator.GetBool("IsRunning")}, IsClimbing: {animator.GetBool("IsClimbing")}");
            }
        }
    }
}