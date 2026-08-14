using UnityEngine;
using Weapon;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float interactionRange = 5f;

    [SerializeField] private LayerMask interactionLayers = ~0;

    // Hovered objects
    public WeaponBase hoveredWeapon;

    public AmmoBox hoveredAmmoBox;
    public Throwable hoveredThrowable;
    public ItemCore hoveredItem;
    public AmmoTable hoveredAmmoTable;

    private Camera playerCamera;

    #region Unity

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        playerCamera = Camera.main;
    }

    private void Update()
    {
        if (playerCamera == null) return;
        UpdateHovered();
        if (Input.GetKeyDown(KeyCode.F)) TryInteract();
    }

    #endregion Unity

    #region Raycast

    private void UpdateHovered()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionLayers))
        {
            GameObject obj = hit.transform.gameObject;
            SetHovered(obj.GetComponent<WeaponBase>(), ref hoveredWeapon, Color.green, w => !w.IsActiveWeapon);
            SetHovered(obj.GetComponent<AmmoBox>(), ref hoveredAmmoBox, Color.blue, _ => true);
            SetHovered(obj.GetComponent<Throwable>(), ref hoveredThrowable, Color.yellow, _ => true);
            SetHovered(obj.GetComponent<ItemCore>(), ref hoveredItem, Color.white, it => !it.IsActiveItem);
            SetHovered(obj.GetComponent<AmmoTable>(), ref hoveredAmmoTable, Color.cyan, _ => true);
        }
        else
        {
            ClearAll();
        }
    }

    private void SetHovered<T>(T candidate, ref T current, Color outlineColor,
        System.Func<T, bool> condition) where T : MonoBehaviour
    {
        if (candidate != null && condition(candidate))
        {
            if (current == candidate) return;
            ClearOutline(current);
            current = candidate;
            ApplyOutline(current, outlineColor);
        }
        else
        {
            ClearOutline(current);
            current = null;
        }
    }

    private void ApplyOutline<T>(T obj, Color color) where T : MonoBehaviour
    {
        if (obj == null) return;
        var outline = obj.GetComponent<Outline>();
        if (outline != null) { outline.enabled = true; outline.OutlineColor = color; }
    }

    private void ClearOutline<T>(T obj) where T : MonoBehaviour
    {
        if (obj == null) return;
        var outline = obj.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    private void ClearAll()
    {
        ClearOutline(hoveredWeapon); hoveredWeapon = null;
        ClearOutline(hoveredAmmoBox); hoveredAmmoBox = null;
        ClearOutline(hoveredThrowable); hoveredThrowable = null;
        ClearOutline(hoveredItem); hoveredItem = null;
        ClearOutline(hoveredAmmoTable); hoveredAmmoTable = null;
    }

    #endregion Raycast

    #region Interaction

    private void TryInteract()
    {
        if (hoveredWeapon != null) { InteractWeapon(); return; }
        if (hoveredAmmoBox != null) { InteractAmmo(); return; }
        if (hoveredThrowable != null) { InteractThrowable(); return; }
        if (hoveredItem != null) { InteractItem(); return; }
        if (hoveredAmmoTable != null) { InteractAmmoTable(); return; }
    }

    private void InteractWeapon()
    {
        if (WeaponManager.Instance?.PickupWeapon(hoveredWeapon) == true)
        {
            ClearOutline(hoveredWeapon);
            hoveredWeapon = null;
        }
    }

    private void InteractAmmo()
    {
        if (WeaponManager.Instance == null) return;
        WeaponManager.Instance.PickupAmmo(hoveredAmmoBox);
        Destroy(hoveredAmmoBox.gameObject);
        hoveredAmmoBox = null;
    }

    private void InteractThrowable()
    {
        WeaponManager.Instance?.PickupThrowable(hoveredThrowable);
        ClearOutline(hoveredThrowable);
        hoveredThrowable = null;
    }

    private void InteractItem()
    {
        if (PlayerInventory.Instance?.PickupItem(hoveredItem) == true)
        {
            Destroy(hoveredItem.gameObject);
            hoveredItem = null;
        }
    }

    private void InteractAmmoTable()
    {
        hoveredAmmoTable.Interact();
        ClearOutline(hoveredAmmoTable);
        hoveredAmmoTable = null;
    }

    #endregion Interaction

    #region Public API

    public bool HasHoveredObject() =>
        hoveredWeapon != null ||
        hoveredAmmoBox != null ||
        hoveredThrowable != null ||
        hoveredItem != null ||
        hoveredAmmoTable != null;

    public string GetHoveredInfo()
    {
        if (hoveredWeapon != null) return $"[F] Pick up {hoveredWeapon.Data.weaponModel}";
        if (hoveredAmmoBox != null) return $"[F] Pick up {hoveredAmmoBox.ammoType} x{hoveredAmmoBox.ammoAmount}";
        if (hoveredThrowable != null) return $"[F] Pick up {hoveredThrowable.throwableType}";
        if (hoveredItem != null) return $"[F] Pick up {hoveredItem.ItemName}";
        if (hoveredAmmoTable != null) return "[F] Open ammo table";
        return string.Empty;
    }

    #endregion Public API

    #region Debug

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(playerCamera.transform.position,
            playerCamera.transform.forward * interactionRange);
    }

    #endregion Debug
}