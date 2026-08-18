using UnityEngine;

namespace PlayerController
{
    public enum MovementState
    {
        Idle,
        Walk,
        Sprint,
        TacSprint,
        Crouch,
        Slide,
        Prone
    }

    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 3f;

        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float tacSprintMultiplier = 1.5f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float proneSpeed = 1f; // placeholder continuous speed — step-based creep movement TBD later

        [Header("Speed Ramping")]
        [SerializeField] private float groundAcceleration = 20f;

        [SerializeField] private float groundDeceleration = 25f;

        [Header("Jump / Gravity")]
        [SerializeField] private float jumpHeight = 2f;

        [SerializeField] private float gravity = -9.81f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;

        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = 1;

        [Header("Movement Smoothing (input direction)")]
        [SerializeField] private float accelerationTime = 0.1f;

        [SerializeField] private float decelerationTime = 0.1f;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 2f;

        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private Vector3 standingCenter = Vector3.zero;
        [SerializeField] private Vector3 crouchCenter = new Vector3(0f, -1f, 0f);
        [SerializeField] private float crouchTransitionSpeed = 8f;
        [SerializeField] private float cameraCrouchOffset = -0.6f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Prone")]
        [SerializeField] private KeyCode proneKey = KeyCode.C;

        [SerializeField] private float proneHeight = 0.5f;
        [SerializeField] private Vector3 proneCenter = new Vector3(0f, -1.5f, 0f);
        [SerializeField] private float cameraProneOffset = -1.2f;

        [Header("Sprint")]
        [SerializeField] private float sprintForwardThreshold = 0.5f;

        [Header("Tac Sprint")]
        [SerializeField] private float tacSprintMaxDuration = 4f;

        [SerializeField] private float tacSprintRegenDuration = 8f;
        [SerializeField] private float tacSprintRegenPenaltyWhileSprinting = 0.25f;
        [SerializeField, Range(0f, 1f)] private float tacSprintActivationThreshold = 0.5f;

        [Header("Slide")]
        [SerializeField] private float slideFriction = 4f;

        [SerializeField] private float slideCancelPunishment = 3f; // instant speed cut applied when jump-cancelling a slide
        [SerializeField] private float slideEndSpeed = 2f;
        [SerializeField] private float slideSteerInfluence = 0.15f;

        [Header("Input Settings")]
        [SerializeField] private KeyCode runKey = KeyCode.LeftShift;

        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        // Components
        private CharacterController controller;

        private FollowCamera.CameraFollow cameraFollow;

        // Movement variables
        private Vector3 velocity;

        private bool isGrounded;
        private Vector2 currentInputVector;
        private Vector2 smoothInputVelocity;

        // Sprint / Tac Sprint
        private bool sprintActive = false;

        private bool tacSprintActive = false;
        private float tacSprintStamina;

        // Speed ramp
        private float currentSpeed = 0f;

        public MovementState State { get; private set; } = MovementState.Idle;

        // Collider/camera blend
        private float currentControllerHeight;

        private Vector3 currentControllerCenter;
        private float currentCameraOffset = 0f;

        // Stance toggles
        private bool crouchToggled = false;

        private bool proneToggled = false;
        private bool crouchPressedThisFrame;
        private bool pronePressedThisFrame;

        // Slide runtime
        private Vector3 slideDirection;

        private float slideSpeed;

        #region Unity Lifecycle

        private void Start()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            controller.center = standingCenter;
            currentControllerHeight = standingHeight;
            currentControllerCenter = standingCenter;
            tacSprintStamina = tacSprintMaxDuration;

            if (groundCheck == null)
            {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.SetParent(transform);
                groundCheckObj.transform.localPosition = new Vector3(0, -controller.height / 2, 0);
                groundCheck = groundCheckObj.transform;
            }

            if (cameraTransform == null)
                cameraTransform = GetComponentInChildren<Camera>()?.transform;

            if (cameraTransform != null)
                cameraFollow = cameraTransform.GetComponent<FollowCamera.CameraFollow>();
        }

        private void Update()
        {
            HandleGroundCheck();
            HandleInput();
            UpdateMovementState();
            HandleMovement();
            HandleCrouchAndCameraBlend();
            HandleGravityAndJump();
            HandleCursor();

            controller.Move(velocity * Time.deltaTime);
        }

        #endregion Unity Lifecycle

        #region Ground Check

        private void HandleGroundCheck()
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            if (isGrounded && velocity.y < 0)
                velocity.y = -2f;
        }

        #endregion Ground Check

        #region Input

        private void HandleInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector2 targetInputVector = new Vector2(horizontal, vertical).normalized;
            float smoothTime = targetInputVector.magnitude > 0 ? accelerationTime : decelerationTime;
            currentInputVector = Vector2.SmoothDamp(currentInputVector, targetInputVector, ref smoothInputVelocity, smoothTime);

            crouchPressedThisFrame = Input.GetKeyDown(crouchKey);
            pronePressedThisFrame = Input.GetKeyDown(proneKey);

            bool forwardInput = currentInputVector.y >= sprintForwardThreshold;

            if (!forwardInput)
            {
                sprintActive = false;
                tacSprintActive = false;
            }

            if (!proneToggled && Input.GetKeyDown(runKey)) // sprint input does nothing while prone
            {
                if (!sprintActive && forwardInput)
                {
                    sprintActive = true;
                }
                else if (sprintActive && !tacSprintActive &&
                         tacSprintStamina >= tacSprintMaxDuration * tacSprintActivationThreshold)
                {
                    tacSprintActive = true;
                }
            }

            UpdateTacSprintStamina();
        }

        private void UpdateTacSprintStamina()
        {
            if (tacSprintActive)
            {
                tacSprintStamina -= Time.deltaTime;
                if (tacSprintStamina <= 0f)
                {
                    tacSprintStamina = 0f;
                    tacSprintActive = false;
                }
            }
            else
            {
                float regenRate = tacSprintMaxDuration / tacSprintRegenDuration;
                if (sprintActive) regenRate *= tacSprintRegenPenaltyWhileSprinting;
                tacSprintStamina = Mathf.Min(tacSprintMaxDuration, tacSprintStamina + regenRate * Time.deltaTime);
            }
        }

        private bool HasMoveInput() => currentInputVector.magnitude > 0.1f;

        public bool IsTacSprinting() => State == MovementState.TacSprint;

        #endregion Input

        #region State Machine

        private void UpdateMovementState()
        {
            if (State == MovementState.Slide)
            {
                UpdateSlide(); // cancel now happens via jump — see HandleGravityAndJump
                return;
            }

            if (!isGrounded)
                return;

            // Prone toggle — only reachable from Walk/Idle/Crouch, never Sprint/TacSprint/Slide
            if (pronePressedThisFrame)
            {
                if (State == MovementState.Prone)
                {
                    if (CanReachHeight(crouchHeight))
                    {
                        proneToggled = false;
                        crouchToggled = true; // rise to crouch, not straight to standing
                    }
                }
                else if (State == MovementState.Walk || State == MovementState.Idle || State == MovementState.Crouch)
                {
                    proneToggled = true;
                    crouchToggled = false;
                }
            }

            if (proneToggled)
            {
                State = MovementState.Prone;
                sprintActive = false;
                tacSprintActive = false;
                return;
            }

            bool wasSprintingHard = (State == MovementState.Sprint || State == MovementState.TacSprint)
                                     && currentSpeed >= sprintSpeed - 0.1f;

            if (crouchPressedThisFrame)
            {
                if (wasSprintingHard)
                {
                    StartSlide();
                    return;
                }
                crouchToggled = !crouchToggled;
            }

            bool wantsCrouch = crouchToggled || !CanReachHeight(standingHeight);

            if (wantsCrouch)
            {
                State = MovementState.Crouch;
                sprintActive = false;
                tacSprintActive = false;
            }
            else if (!HasMoveInput())
            {
                State = MovementState.Idle;
            }
            else if (tacSprintActive)
            {
                State = MovementState.TacSprint;
            }
            else if (sprintActive)
            {
                State = MovementState.Sprint;
            }
            else
            {
                State = MovementState.Walk;
            }
        }

        private float GetTargetSpeedForState(MovementState state) => state switch
        {
            MovementState.Idle => 0f,
            MovementState.Walk => walkSpeed,
            MovementState.Sprint => sprintSpeed,
            MovementState.TacSprint => sprintSpeed * tacSprintMultiplier,
            MovementState.Crouch => crouchSpeed,
            MovementState.Prone => proneSpeed,
            _ => walkSpeed
        };

        #endregion State Machine

        #region Movement

        private void HandleMovement()
        {
            if (State == MovementState.Slide)
                return;

            Vector3 moveDirection = transform.right * currentInputVector.x + transform.forward * currentInputVector.y;

            float targetSpeed = HasMoveInput() ? GetTargetSpeedForState(State) : 0f;
            float rate = targetSpeed > currentSpeed ? groundAcceleration : groundDeceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

            velocity.x = moveDirection.x * currentSpeed;
            velocity.z = moveDirection.z * currentSpeed;
        }

        #endregion Movement

        #region Slide

        private void StartSlide()
        {
            State = MovementState.Slide;
            Vector3 moveDirection = transform.right * currentInputVector.x + transform.forward * currentInputVector.y;
            slideDirection = moveDirection.sqrMagnitude > 0.01f ? moveDirection.normalized : transform.forward;
            slideSpeed = currentSpeed;
        }

        private void UpdateSlide()
        {
            if (!isGrounded)
            {
                State = HasMoveInput() ? MovementState.Walk : MovementState.Idle;
                currentSpeed = slideSpeed;
                return;
            }

            slideSpeed = Mathf.MoveTowards(slideSpeed, 0f, slideFriction * Time.deltaTime);

            Vector3 inputDirection = transform.right * currentInputVector.x + transform.forward * currentInputVector.y;
            if (inputDirection.sqrMagnitude > 0.01f)
                slideDirection = Vector3.Slerp(slideDirection, inputDirection.normalized, slideSteerInfluence).normalized;

            velocity.x = slideDirection.x * slideSpeed;
            velocity.z = slideDirection.z * slideSpeed;

            if (slideSpeed <= slideEndSpeed)
                EndSlide();
        }

        private void EndSlide()
        {
            currentSpeed = slideSpeed;
            crouchToggled = true; // slide always settles into crouch — jump-cancel exits through a separate path
            State = MovementState.Crouch;
        }

        // Called from HandleGravityAndJump when Space is pressed mid-slide
        private void CancelSlideViaJump()
        {
            slideSpeed = Mathf.Max(0f, slideSpeed - slideCancelPunishment); // sharp instant cut, not gradual friction
            currentSpeed = slideSpeed;

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            crouchToggled = false;

            State = HasMoveInput() ? MovementState.Walk : MovementState.Idle;
        }

        #endregion Slide

        #region Crouch / Prone / Camera Blend

        private bool CanReachHeight(float targetHeight)
        {
            if (targetHeight <= currentControllerHeight + 0.01f)
                return true; // going down or staying — always fine

            float clearance = targetHeight - currentControllerHeight;
            Vector3 origin = transform.position + currentControllerCenter + Vector3.up * (currentControllerHeight / 2f);
            return !Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up, out _, clearance, obstructionMask);
        }

        private void HandleCrouchAndCameraBlend()
        {
            float targetHeight;
            Vector3 targetCenter;
            float targetCameraOffset;

            if (State == MovementState.Prone)
            {
                targetHeight = proneHeight;
                targetCenter = proneCenter;
                targetCameraOffset = cameraProneOffset;
            }
            else if (State == MovementState.Crouch || State == MovementState.Slide || crouchToggled || !CanReachHeight(standingHeight))
            {
                targetHeight = crouchHeight;
                targetCenter = crouchCenter;
                targetCameraOffset = cameraCrouchOffset;
            }
            else
            {
                targetHeight = standingHeight;
                targetCenter = standingCenter;
                targetCameraOffset = 0f;
            }

            currentControllerHeight = Mathf.Lerp(currentControllerHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            currentControllerCenter = Vector3.Lerp(currentControllerCenter, targetCenter, crouchTransitionSpeed * Time.deltaTime);
            controller.height = currentControllerHeight;
            controller.center = currentControllerCenter;

            currentCameraOffset = Mathf.Lerp(currentCameraOffset, targetCameraOffset, crouchTransitionSpeed * Time.deltaTime);
            cameraFollow?.SetCrouchOffset(currentCameraOffset);
        }

        #endregion Crouch / Prone / Camera Blend

        #region Gravity / Jump

        private void HandleGravityAndJump()
        {
            if (Input.GetKeyDown(jumpKey))
            {
                if (State == MovementState.Slide)
                {
                    CancelSlideViaJump();
                }
                else if (isGrounded && CanReachHeight(standingHeight))
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    crouchToggled = false;
                    proneToggled = false;
                    State = HasMoveInput() ? MovementState.Walk : MovementState.Idle;
                }
            }

            velocity.y += gravity * Time.deltaTime;
        }

        #endregion Gravity / Jump

        #region Public API

        private void HandleCursor()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0))
                Cursor.lockState = CursorLockMode.Locked;
        }

        public bool IsGrounded() => isGrounded;

        public bool IsRunning() => (State == MovementState.Sprint || State == MovementState.TacSprint) && HasMoveInput();

        public bool IsMoving() => HasMoveInput() || State == MovementState.Slide;

        public bool IsCrouching() => State == MovementState.Crouch;

        public bool IsProne() => State == MovementState.Prone;

        public bool IsSliding() => State == MovementState.Slide;

        public float GetCurrentSpeed() => State == MovementState.Slide ? slideSpeed : currentSpeed;

        public Vector3 GetVelocity() => velocity;

        public void SetMovementSpeeds(float newWalkSpeed, float newSprintSpeed)
        {
            walkSpeed = newWalkSpeed;
            sprintSpeed = newSprintSpeed;
        }

        public void SetJumpHeight(float newJumpHeight) => jumpHeight = newJumpHeight;

        #endregion Public API

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }
        }

        #endregion Gizmos
    }
}