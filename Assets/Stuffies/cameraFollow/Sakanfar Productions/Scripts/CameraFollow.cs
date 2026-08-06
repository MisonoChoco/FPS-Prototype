using UnityEngine;

namespace FollowCamera
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Mouse Look Settings")]
        [SerializeField] private float mouseSensitivityX = 100f;

        [SerializeField] private float mouseSensitivityY = 100f;
        [SerializeField] private bool invertMouseY = false;

        [Header("Camera Constraints")]
        [SerializeField] private float minVerticalAngle = -90f;

        [SerializeField] private float maxVerticalAngle = 90f;

        [Header("Smoothing")]
        [SerializeField] private float smoothTime = 0.1f;

        [SerializeField] private bool enableSmoothing = true;

        [Header("References")]
        [SerializeField] private Transform playerBody;

        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerController.PlayerController playerController;

        [Header("Zoom/Aim Settings")]
        [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;

        [SerializeField] private float normalFOV = 60f;
        [SerializeField] private float zoomedFOV = 30f;
        [SerializeField] private float runningFOV = 70f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float aimSensitivityMultiplier = 0.5f;

        [Header("Camera Shake Settings")]
        [SerializeField] private bool enableCameraShake = true;

        [SerializeField] private float walkShakeIntensity = 0.02f;
        [SerializeField] private float runShakeIntensity = 0.05f;
        [SerializeField] private float shakeFrequency = 10f;
        [SerializeField] private float landingShakeIntensity = 0.15f;
        [SerializeField] private float landingShakeDuration = 0.3f;
        [SerializeField] private float shakeReduction = 2f;

        // Mouse look — base rotation only, never touched by recoil
        private float xRotation = 0f;

        private float yRotation = 0f;
        private Vector2 currentMouseDelta;
        private Vector2 currentMouseDeltaVelocity;

        // Zoom
        private bool isAiming = false;

        private float targetFOV;
        private float currentFOV;

        // Camera shake
        private Vector3 cameraShakeOffset;

        private float shakeTimer;
        private float landingShakeTimer;
        private bool wasGrounded = true;

        // ── Recoil (fully separated from base rotation) ──────────────
        // The accumulated target — only decays when not firing
        private float recoilOffsetX = 0f;

        private float recoilOffsetY = 0f;

        // The smoothed visual that chases the offset target
        private float recoilVisualX = 0f;

        private float recoilVisualY = 0f;

        // Roll (still bakes directly into camera euler Z, recovers always)
        private float currentRoll = 0f;

        // Speed parameters set per-weapon via ApplyRecoilKick
        [SerializeField] private float recoilSnapSpeed = 20f;

        [SerializeField] private float recoilReturnSpeed = 5f;

        // Set by WeaponBase each frame — gates offset recovery
        private bool isFiring = false;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera == null)
                playerCamera = GetComponent<Camera>();

            if (playerBody == null)
            {
                if (transform.parent != null)
                    playerBody = transform.parent;
                else
                {
                    var pc = FindAnyObjectByType<PlayerController.PlayerController>();
                    if (pc != null) playerBody = pc.transform;
                }
            }

            if (playerController == null)
            {
                if (playerBody != null)
                    playerController = playerBody.GetComponent<PlayerController.PlayerController>();
                else
                    playerController = FindAnyObjectByType<PlayerController.PlayerController>();
            }

            if (playerBody != null)
                yRotation = playerBody.eulerAngles.y;
            else
                Debug.LogError("CameraFollow: No Player Body found!");

            if (playerCamera != null)
            {
                normalFOV = playerCamera.fieldOfView;
                currentFOV = normalFOV;
                targetFOV = normalFOV;
            }
        }

        private void Update()
        {
            HandleRecoilSmoothing();  // must run before HandleMouseLook
            HandleMouseLook();
            HandleZoom();
            HandleCameraShake();
            HandleCursorToggle();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;

            if (isAiming) { mouseX *= aimSensitivityMultiplier; mouseY *= aimSensitivityMultiplier; }
            if (invertMouseY) mouseY = -mouseY;

            if (enableSmoothing)
            {
                Vector2 targetMouseDelta = new Vector2(mouseX, mouseY);
                currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta,
                    ref currentMouseDeltaVelocity, smoothTime);
                mouseX = currentMouseDelta.x;
                mouseY = currentMouseDelta.y;
            }

            // Convert to camera-space deltas so both axes share the same sign convention:
            // positive pitchChange = camera tilting down, positive yawChange = camera turning right
            float pitchChange = -mouseY;
            float yawChange = mouseX;

            // While firing, route opposing input into the recoil offset (draining it)
            // rather than into the base rotation anchor.
            // Same-direction input (gasboost) passes through untouched and moves the anchor.
            if (isFiring)
            {
                pitchChange = AbsorbIntoOffset(pitchChange, ref recoilOffsetX);
                yawChange = AbsorbIntoOffset(yawChange, ref recoilOffsetY);
            }

            yRotation += yawChange;
            xRotation += pitchChange;
            xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

            if (playerBody != null)
                playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
            else
            {
                transform.rotation = Quaternion.Euler(xRotation + recoilVisualX, yRotation + recoilVisualY, currentRoll);
                return;
            }

            transform.localRotation = Quaternion.Euler(xRotation + recoilVisualX, recoilVisualY, currentRoll);

            if (enableCameraShake)
                transform.localPosition = cameraShakeOffset;
        }

        /// <summary>
        /// Routes player input against the recoil offset.
        /// Input opposing the offset drains it first; any remainder moves the anchor.
        /// Same-direction input (gasboost) is returned unchanged — it moves the anchor normally.
        /// </summary>
        private float AbsorbIntoOffset(float inputDelta, ref float offset)
        {
            if (offset == 0f || inputDelta == 0f) return inputDelta;

            // Opposing means: input wants to push camera in the direction that would reduce |offset|
            bool opposing = (offset > 0f) != (inputDelta > 0f);
            if (!opposing) return inputDelta;

            float absorb = Mathf.Min(Mathf.Abs(inputDelta), Mathf.Abs(offset));
            offset -= Mathf.Sign(offset) * absorb; // drain offset toward zero

            // Whatever wasn't absorbed becomes anchor movement (over-counter remainder)
            float remaining = Mathf.Abs(inputDelta) - absorb;
            return remaining > 0f ? Mathf.Sign(inputDelta) * remaining : 0f;
        }

        private void HandleRecoilSmoothing()
        {
            // Visual smoothly chases the accumulated offset target
            recoilVisualX = Mathf.Lerp(recoilVisualX, recoilOffsetX, recoilSnapSpeed * Time.deltaTime);
            recoilVisualY = Mathf.Lerp(recoilVisualY, recoilOffsetY, recoilSnapSpeed * Time.deltaTime);

            // Offset target ONLY decays when not firing
            // This is the entire snapback mechanism — while firing, offset accumulates freely
            if (!isFiring)
            {
                recoilOffsetX = Mathf.Lerp(recoilOffsetX, 0f, recoilReturnSpeed * Time.deltaTime);
                recoilOffsetY = Mathf.Lerp(recoilOffsetY, 0f, recoilReturnSpeed * Time.deltaTime);
            }

            // Roll always recovers (it's a cosmetic tilt, not an aim offset)
            currentRoll = Mathf.Lerp(currentRoll, 0f, 8f * Time.deltaTime);
        }

        private void HandleZoom()
        {
            isAiming = Input.GetKey(aimKey);
            bool isPlayerRunning = playerController != null && playerController.IsRunning();

            if (isAiming) targetFOV = zoomedFOV;
            else if (isPlayerRunning) targetFOV = runningFOV;
            else targetFOV = normalFOV;

            currentFOV = Mathf.Lerp(currentFOV, targetFOV, zoomSpeed * Time.deltaTime);
            if (playerCamera != null) playerCamera.fieldOfView = currentFOV;
        }

        private void HandleCameraShake()
        {
            if (!enableCameraShake || playerController == null)
            {
                cameraShakeOffset = Vector3.zero;
                return;
            }

            bool isGrounded = playerController.IsGrounded();
            bool isMoving = playerController.IsMoving();
            bool isRunning = playerController.IsRunning();

            if (isGrounded && !wasGrounded)
                landingShakeTimer = landingShakeDuration;
            wasGrounded = isGrounded;

            float shakeIntensity = 0f;

            if (landingShakeTimer > 0f)
            {
                landingShakeTimer -= Time.deltaTime;
                float landingShakeAmount = (landingShakeTimer / landingShakeDuration) * landingShakeIntensity;
                shakeIntensity = Mathf.Max(shakeIntensity, landingShakeAmount);
            }

            if (isMoving && isGrounded)
            {
                float movementShake = isRunning ? runShakeIntensity : walkShakeIntensity;
                shakeIntensity = Mathf.Max(shakeIntensity, movementShake);
            }

            if (isAiming) shakeIntensity *= aimSensitivityMultiplier;

            if (shakeIntensity > 0f)
            {
                shakeTimer += Time.deltaTime * shakeFrequency;
                cameraShakeOffset = new Vector3(
                    Mathf.Sin(shakeTimer) * shakeIntensity,
                    Mathf.Sin(shakeTimer * 1.3f) * shakeIntensity * 0.7f,
                    Mathf.Sin(shakeTimer * 0.8f) * shakeIntensity * 0.5f);
            }
            else
            {
                cameraShakeOffset = Vector3.Lerp(cameraShakeOffset, Vector3.zero,
                    Time.deltaTime * shakeReduction);
            }
        }

        private void HandleCursorToggle()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        // ── Recoil API ────────────────────────────────────────────────

        /// <summary>
        /// Called by WeaponBase each shot. Accumulates into the offset target.
        /// </summary>

        [SerializeField] private float recoilOffsetXMax = 90f; // max upward kick in degrees

        [SerializeField] private float recoilOffsetYMax = 15f; // max horizontal drift

        public void ApplyRecoilKick(float pitchKick, float yawKick, float rollKick,
    float snapSpeed, float returnSpeed)
        {
            recoilSnapSpeed = snapSpeed;
            recoilReturnSpeed = returnSpeed;

            recoilOffsetX = Mathf.Clamp(recoilOffsetX - pitchKick, -recoilOffsetXMax, recoilOffsetXMax);
            recoilOffsetY = Mathf.Clamp(recoilOffsetY + yawKick, -recoilOffsetYMax, recoilOffsetYMax);
            currentRoll += rollKick;
        }

        /// <summary>
        /// Called by WeaponBase every frame. Gates whether the offset is allowed to recover.
        /// </summary>
        public void SetFiringState(bool firing)
        {
            isFiring = firing;
        }

        /// <summary>
        /// Called when a weapon is unequipped. Clears stale offset so it doesn't bleed
        /// into the next weapon.
        /// </summary>
        public void ResetRecoil()
        {
            recoilOffsetX = 0f;
            recoilOffsetY = 0f;
            recoilVisualX = 0f;
            recoilVisualY = 0f;
            currentRoll = 0f;
        }

        // ── Other Public API ──────────────────────────────────────────

        public void SetMouseSensitivity(float sensitivityX, float sensitivityY)
        {
            mouseSensitivityX = sensitivityX;
            mouseSensitivityY = sensitivityY;
        }

        public void SetVerticalLimits(float minAngle, float maxAngle)
        {
            minVerticalAngle = minAngle;
            maxVerticalAngle = maxAngle;
        }

        public void ResetRotation()
        {
            xRotation = 0f;
            yRotation = 0f;
            transform.localRotation = Quaternion.identity;
            if (playerBody != null) playerBody.rotation = Quaternion.identity;
        }

        public void SetInvertMouse(bool invert) => invertMouseY = invert;

        public void SetSmoothing(bool enable, float time = 0.1f)
        {
            enableSmoothing = enable;
            smoothTime = time;
        }

        public Vector3 GetBaseAimDirection()
        {
            return Quaternion.Euler(xRotation, yRotation, 0f) * Vector3.forward;
        }

        public Vector3 GetBaseAimOrigin()
        {
            return playerCamera != null ? playerCamera.transform.position : transform.position;
        }
    }
}