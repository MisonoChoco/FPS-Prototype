using UnityEngine;
using Weapon; // Add this using directive to access the namespace

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; set; }

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 5f;

    [SerializeField] private LayerMask interactionLayers = -1;

    // Current hovered objects
    public WeaponBase hoveredWeapon = null; // Changed from Weapon to WeaponBase

    public AmmoBox hoveredAmmoBox = null;
    public Throwable hoveredThrowable = null;

    // Cache for performance
    private Camera playerCamera;

    private Ray interactionRay;
    private RaycastHit hitInfo;

    #region Initialization

    private void Awake()
    {
        InitializeSingleton();
        CacheComponents();
    }

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void CacheComponents()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Debug.LogError("InteractionManager: No main camera found!");
        }
    }

    #endregion Initialization

    #region Update Loop

    private void Update()
    {
        if (playerCamera == null) return;

        HandleInteractionRaycast();
        HandleInteractionInput();
    }

    private void HandleInteractionRaycast()
    {
        // Create ray from camera center
        interactionRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(interactionRay, out hitInfo, interactionRange, interactionLayers))
        {
            GameObject hitObject = hitInfo.transform.gameObject;

            HandleWeaponInteraction(hitObject);
            HandleAmmoInteraction(hitObject);
            HandleThrowableInteraction(hitObject);
        }
        else
        {
            ClearAllInteractions();
        }
    }

    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryInteractWithHoveredObject();
        }
    }

    #endregion Update Loop

    #region Weapon Interaction

    private void HandleWeaponInteraction(GameObject hitObject)
    {
        // Look for WeaponBase component
        WeaponBase weapon = hitObject.GetComponent<WeaponBase>();
        if (weapon != null && !weapon.IsActiveWeapon)
        {
            SetHoveredWeapon(weapon);
        }
        else
        {
            ClearHoveredWeapon();
        }
    }

    private void SetHoveredWeapon(WeaponBase weapon)
    {
        if (hoveredWeapon == weapon) return;

        // Clear previous weapon
        ClearHoveredWeapon();

        // Set new hovered weapon
        hoveredWeapon = weapon;

        var outline = weapon.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = Color.green;
        }
    }

    private void ClearHoveredWeapon()
    {
        if (hoveredWeapon != null)
        {
            var outline = hoveredWeapon.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
            hoveredWeapon = null;
        }
    }

    #endregion Weapon Interaction

    #region Ammo Interaction

    private void HandleAmmoInteraction(GameObject hitObject)
    {
        AmmoBox ammoBox = hitObject.GetComponent<AmmoBox>();
        if (ammoBox != null)
        {
            SetHoveredAmmoBox(ammoBox);
        }
        else
        {
            ClearHoveredAmmoBox();
        }
    }

    private void SetHoveredAmmoBox(AmmoBox ammoBox)
    {
        if (hoveredAmmoBox == ammoBox) return;

        // Clear previous ammo box
        ClearHoveredAmmoBox();

        // Set new hovered ammo box
        hoveredAmmoBox = ammoBox;

        var outline = ammoBox.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = Color.blue;
        }
    }

    private void ClearHoveredAmmoBox()
    {
        if (hoveredAmmoBox != null)
        {
            var outline = hoveredAmmoBox.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
            hoveredAmmoBox = null;
        }
    }

    #endregion Ammo Interaction

    #region Throwable Interaction

    private void HandleThrowableInteraction(GameObject hitObject)
    {
        Throwable throwable = hitObject.GetComponent<Throwable>();
        if (throwable != null)
        {
            SetHoveredThrowable(throwable);
        }
        else
        {
            ClearHoveredThrowable();
        }
    }

    private void SetHoveredThrowable(Throwable throwable)
    {
        if (hoveredThrowable == throwable) return;

        // Clear previous throwable
        ClearHoveredThrowable();

        // Set new hovered throwable
        hoveredThrowable = throwable;

        var outline = throwable.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = Color.yellow;
        }
    }

    private void ClearHoveredThrowable()
    {
        if (hoveredThrowable != null)
        {
            var outline = hoveredThrowable.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
            hoveredThrowable = null;
        }
    }

    #endregion Throwable Interaction

    #region Interaction Execution

    private void TryInteractWithHoveredObject()
    {
        // Try weapon interaction
        if (hoveredWeapon != null)
        {
            InteractWithWeapon();
            return;
        }

        // Try ammo interaction
        if (hoveredAmmoBox != null)
        {
            InteractWithAmmoBox();
            return;
        }

        // Try throwable interaction
        if (hoveredThrowable != null)
        {
            InteractWithThrowable();
            return;
        }
    }

    private void InteractWithWeapon()
    {
        if (WeaponManager.Instance != null)
        {
            bool success = WeaponManager.Instance.PickupWeapon(hoveredWeapon);
            if (success)
            {
                ClearHoveredWeapon();
            }
        }
    }

    private void InteractWithAmmoBox()
    {
        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.PickupAmmo(hoveredAmmoBox);
            Destroy(hoveredAmmoBox.gameObject);
            ClearHoveredAmmoBox();
        }
    }

    private void InteractWithThrowable()
    {
        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.PickupThrowable(hoveredThrowable);
            // WeaponManager will handle destroying the throwable
            ClearHoveredThrowable();
        }
    }

    #endregion Interaction Execution

    #region Utility Methods

    private void ClearAllInteractions()
    {
        ClearHoveredWeapon();
        ClearHoveredAmmoBox();
        ClearHoveredThrowable();
    }

    public bool HasAnyHoveredObject()
    {
        return hoveredWeapon != null ||
               hoveredAmmoBox != null ||
               hoveredThrowable != null;
    }

    public string GetHoveredObjectInfo()
    {
        if (hoveredWeapon != null)
        {
            return $"Press F to pickup {hoveredWeapon.weaponModel}";
        }

        if (hoveredAmmoBox != null)
        {
            return $"Press F to pickup {hoveredAmmoBox.ammoType} ({hoveredAmmoBox.ammoAmount})";
        }

        if (hoveredThrowable != null)
        {
            return $"Press F to pickup {hoveredThrowable.throwableType}";
        }

        return "";
    }

    #endregion Utility Methods

    #region Debug

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;

        // Draw interaction ray
        Gizmos.color = Color.red;
        Vector3 rayStart = playerCamera.transform.position;
        Vector3 rayEnd = rayStart + playerCamera.transform.forward * interactionRange;
        Gizmos.DrawLine(rayStart, rayEnd);

        // Draw hit point if we have one
        if (Physics.Raycast(playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)), out RaycastHit hit, interactionRange))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(hit.point, 0.1f);
        }
    }

    #endregion Debug
}