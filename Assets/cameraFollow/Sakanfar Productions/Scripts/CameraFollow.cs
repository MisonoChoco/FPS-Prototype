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

        // Mouse look
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

        // Recoil
        private float currentRoll = 0f;

        private float recoilReturnSpeed = 5f;

        private float recoilTargetX = 0f;
        private float recoilTargetY = 0f;
        private float recoilCurrentX = 0f;
        private float recoilCurrentY = 0f;
        [SerializeField] private float recoilSnapSpeed = 20f; // tweak in inspector

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
            HandleRecoilSmoothing();
            HandleMouseLook();
            HandleZoom();
            HandleCameraShake();
            HandleCursorToggle();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;

            if (isAiming)
            {
                mouseX *= aimSensitivityMultiplier;
                mouseY *= aimSensitivityMultiplier;
            }

            if (invertMouseY) mouseY = -mouseY;

            if (enableSmoothing)
            {
                Vector2 targetMouseDelta = new Vector2(mouseX, mouseY);
                currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, smoothTime);
                mouseX = currentMouseDelta.x;
                mouseY = currentMouseDelta.y;
            }

            yRotation += mouseX;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

            if (playerBody != null)
                playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
            else
            {
                transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
                return;
            }

            currentRoll = Mathf.Lerp(currentRoll, 0f, 8f * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, currentRoll);

            if (enableCameraShake)
                transform.localPosition = cameraShakeOffset;
        }

        private void HandleRecoilSmoothing()
        {
            float prevX = recoilCurrentX;
            float prevY = recoilCurrentY;

            // Snap to target fast — driven by recoilRotationSpeed
            recoilCurrentX = Mathf.Lerp(recoilCurrentX, recoilTargetX, recoilSnapSpeed * Time.deltaTime);
            recoilCurrentY = Mathf.Lerp(recoilCurrentY, recoilTargetY, recoilSnapSpeed * Time.deltaTime);

            // Decay target back to zero separately — driven by recoilReturnSpeed
            recoilTargetX = Mathf.Lerp(recoilTargetX, 0f, recoilReturnSpeed * Time.deltaTime);
            recoilTargetY = Mathf.Lerp(recoilTargetY, 0f, recoilReturnSpeed * Time.deltaTime);

            xRotation += recoilCurrentX - prevX;
            yRotation += recoilCurrentY - prevY;
            xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);
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
                cameraShakeOffset = Vector3.Lerp(cameraShakeOffset, Vector3.zero, Time.deltaTime * shakeReduction);
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

        // ── Recoil API ──────────────────────────────────────────────
        public void ApplyRecoilKick(float pitchKick, float yawKick, float rollKick, float snapSpeed, float returnSpeed)
        {
            recoilSnapSpeed = snapSpeed;
            recoilReturnSpeed = returnSpeed;
            recoilTargetX -= pitchKick;
            recoilTargetY += yawKick;
            currentRoll += rollKick;
        }

        // ── Other Public API ─────────────────────────────────────────
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
    }
}