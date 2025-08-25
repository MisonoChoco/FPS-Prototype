using UnityEngine;

/// <summary>
/// Handles weapon pickup interactions - works with your existing InteractionManager
/// </summary>
[RequireComponent(typeof(WeaponBase))]
public class WeaponPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private bool autoSetupOutline = true;

    [SerializeField] private AudioClip pickupSound;

    [Header("Visual Feedback")]
    [SerializeField] private bool enableBobbing = true;

    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private float rotationSpeed = 30f;

    // Components
    private WeaponBase weaponComponent;

    private Outline outlineComponent;
    private Rigidbody weaponRigidbody;

    // State
    private Vector3 originalPosition;

    private float bobTimer = 0f;
    private bool isPickupEnabled = true;

    // Properties
    public bool IsPickupEnabled => isPickupEnabled && weaponComponent != null;

    public WeaponBase WeaponComponent => weaponComponent;

    #region Initialization

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        weaponComponent = GetComponent<WeaponBase>();
        if (weaponComponent == null)
        {
            Debug.LogError($"WeaponPickup: No WeaponBase component found on {gameObject.name}");
            enabled = false;
            return;
        }

        weaponRigidbody = GetComponent<Rigidbody>();

        // Setup outline component if needed
        if (autoSetupOutline)
        {
            outlineComponent = GetComponent<Outline>();
            if (outlineComponent == null)
            {
                outlineComponent = gameObject.AddComponent<Outline>();
                outlineComponent.OutlineColor = Color.white;
                outlineComponent.OutlineWidth = 2f;
            }
            outlineComponent.enabled = false; // InteractionManager will handle this
        }
    }

    private void Start()
    {
        SetupForPickup();
    }

    private void SetupForPickup()
    {
        // Ensure weapon is not active when used as pickup
        if (weaponComponent != null)
        {
            weaponComponent.SetActiveWeapon(false);
        }

        // Store original position for bobbing
        originalPosition = transform.position;

        // Add random offset to bob timer to avoid synchronization
        bobTimer = Random.Range(0f, Mathf.PI * 2f);

        // Set layer to default for pickup (InteractionManager raycast)
        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    #endregion Initialization

    #region Update Loop

    private void Update()
    {
        if (!isPickupEnabled) return;

        if (enableBobbing)
        {
            UpdateBobbing();
        }
    }

    private void UpdateBobbing()
    {
        bobTimer += Time.deltaTime * bobSpeed;

        // Vertical bobbing
        Vector3 newPosition = originalPosition;
        newPosition.y += Mathf.Sin(bobTimer) * bobHeight;
        transform.position = newPosition;

        // Rotation
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    #endregion Update Loop

    #region Pickup Logic

    public bool TryPickup()
    {
        if (!CanPickup()) return false;

        // Your InteractionManager will call WeaponManager.PickupWeapon
        bool success = WeaponManager.Instance.PickupWeapon(weaponComponent);

        if (success)
        {
            OnPickupSuccess();
        }
        else
        {
            OnPickupFailed();
        }

        return success;
    }

    private bool CanPickup()
    {
        return isPickupEnabled &&
               weaponComponent != null &&
               WeaponManager.Instance != null;
    }

    private void OnPickupSuccess()
    {
        // Play pickup effects
        PlayPickupEffects();

        // Clear from InteractionManager if this was the hovered weapon
        if (InteractionManager.Instance != null)
        {
            // The InteractionManager will handle clearing hovered weapon
        }

        // Disable this pickup component
        isPickupEnabled = false;
        enabled = false;

        Debug.Log($"Picked up weapon: {weaponComponent.weaponModel}");
    }

    private void OnPickupFailed()
    {
        Debug.Log("Failed to pickup weapon - inventory might be full");
    }

    private void PlayPickupEffects()
    {
        // Play pickup sound
        if (pickupSound != null && SoundManager.Instance?.ShootingChannel != null)
        {
            SoundManager.Instance.ShootingChannel.PlayOneShot(pickupSound);
        }
    }

    #endregion Pickup Logic

    #region Utility

    /// <summary>
    /// Called when the weapon is dropped to re-enable pickup functionality
    /// </summary>
    public void EnablePickup(Vector3 position)
    {
        transform.position = position;
        originalPosition = position;
        isPickupEnabled = true;
        enabled = true;

        // Reset visual state
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false; // InteractionManager controls this
        }

        // Enable physics
        if (weaponRigidbody != null)
        {
            weaponRigidbody.isKinematic = false;
        }

        // Set to default layer for pickup
        gameObject.layer = LayerMask.NameToLayer("Default");

        Debug.Log($"Weapon {weaponComponent.weaponModel} enabled for pickup");
    }

    /// <summary>
    /// Get display-friendly weapon name
    /// </summary>
    public string GetWeaponDisplayName()
    {
        if (weaponComponent == null) return "Unknown Weapon";

        return weaponComponent.weaponModel switch
        {
            Weapon.WeaponModel.HandgunM1911 => "M1911 Pistol",
            Weapon.WeaponModel.AK47 => "AK-47 Rifle",
            _ => weaponComponent.weaponModel.ToString()
        };
    }

    #endregion Utility

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Draw pickup bounds
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
        }

        // Draw bobbing range
        if (enableBobbing)
        {
            Gizmos.color = Color.yellow;
            Vector3 pos = Application.isPlaying ? originalPosition : transform.position;
            Gizmos.DrawLine(pos + Vector3.up * bobHeight, pos - Vector3.up * bobHeight);
        }
    }

    #endregion Gizmos
}