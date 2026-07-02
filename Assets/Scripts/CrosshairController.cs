using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("Arms")]
    [SerializeField] private RectTransform centerDot;

    [SerializeField] private RectTransform armTop;
    [SerializeField] private RectTransform armBottom;
    [SerializeField] private RectTransform armLeft;
    [SerializeField] private RectTransform armRight;

    [Header("Appearance")]
    [SerializeField] private float armLength = 8f;

    [SerializeField] private float armThickness = 2f;
    [SerializeField] private float dotSize = 3f;
    [SerializeField] private float baseGap = 6f;

    [Header("Spread")]
    [SerializeField] private bool showSpreadIndicator = true;

    [SerializeField] private float spreadSmoothSpeed = 12f;

    [Header("Raycast")]
    [SerializeField] private float aimRayLength = 2000f;

    [SerializeField] private LayerMask aimMask = ~0;

    private RectTransform crosshairRoot;
    private FollowCamera.CameraFollow cameraFollow;
    private Camera playerCamera;
    private Canvas canvas;
    private float visualGap;

    private void Awake()
    {
        crosshairRoot = GetComponent<RectTransform>();
    }

    private void Start()
    {
        cameraFollow = FindAnyObjectByType<FollowCamera.CameraFollow>();
        playerCamera = Camera.main;
        canvas = GetComponentInParent<Canvas>();
        visualGap = baseGap;

        ApplyArmSizes();
    }

    private void LateUpdate()
    {
        if (cameraFollow == null || playerCamera == null || canvas == null) return;

        PositionOnTrueAim();
        UpdateSpread();
    }

    private void PositionOnTrueAim()
    {
        Vector3 origin = cameraFollow.GetBaseAimOrigin();
        Vector3 direction = cameraFollow.GetBaseAimDirection();

        Vector3 worldAimPoint = Physics.Raycast(origin, direction, out RaycastHit hit, aimRayLength, aimMask)
            ? hit.point
            : origin + direction * aimRayLength;

        Vector3 screenPos = playerCamera.WorldToScreenPoint(worldAimPoint);
        if (screenPos.z < 0f) return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : playerCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform,
            new Vector2(screenPos.x, screenPos.y),
            uiCamera,
            out Vector2 localPoint);

        crosshairRoot.localPosition = localPoint;
    }

    private void UpdateSpread()
    {
        float targetGap = baseGap;

        if (showSpreadIndicator)
        {
            WeaponBase weapon = WeaponManager.Instance?.CurrentWeapon;
            if (weapon?.Data != null)
            {
                Vector3 origin = cameraFollow.GetBaseAimOrigin();
                Vector3 direction = cameraFollow.GetBaseAimDirection();
                float distance = Physics.Raycast(origin, direction, out RaycastHit hit, aimRayLength, aimMask)
                    ? hit.distance
                    : aimRayLength;

                float bulletDeviation = weapon.CurrentSpread * weapon.Data.bulletSpreadScale;
                Vector3 worldEdge = origin + direction * distance
                                      + playerCamera.transform.right * bulletDeviation * distance;

                Vector3 screenCenter = playerCamera.WorldToScreenPoint(origin + direction * distance);
                Vector3 screenEdge = playerCamera.WorldToScreenPoint(worldEdge);

                targetGap = Mathf.Abs(screenEdge.x - screenCenter.x);
            }
        }

        visualGap = Mathf.Lerp(visualGap, targetGap, spreadSmoothSpeed * Time.deltaTime);
        ApplyArmPositions();
    }

    private void ApplyArmSizes()
    {
        armTop.sizeDelta = new Vector2(armThickness, armLength);
        armBottom.sizeDelta = new Vector2(armThickness, armLength);
        armLeft.sizeDelta = new Vector2(armThickness, armLength);
        armRight.sizeDelta = new Vector2(armThickness, armLength);

        armTop.localEulerAngles = Vector3.zero;
        armBottom.localEulerAngles = Vector3.zero;
        armLeft.localEulerAngles = new Vector3(0f, 0f, 90f);
        armRight.localEulerAngles = new Vector3(0f, 0f, 90f);

        centerDot.sizeDelta = new Vector2(dotSize, dotSize);
    }

    private void ApplyArmPositions()
    {
        float g = visualGap;
        armTop.anchoredPosition = new Vector2(0, g);
        armBottom.anchoredPosition = new Vector2(0, -g);
        armLeft.anchoredPosition = new Vector2(-g, 0);
        armRight.anchoredPosition = new Vector2(g, 0);
    }

    public void SetSpreadIndicator(bool enabled)
    {
        showSpreadIndicator = enabled;
        armTop.gameObject.SetActive(enabled);
        armBottom.gameObject.SetActive(enabled);
        armLeft.gameObject.SetActive(enabled);
        armRight.gameObject.SetActive(enabled);
    }

    public void SetVisible(bool visible) => crosshairRoot.gameObject.SetActive(visible);
}