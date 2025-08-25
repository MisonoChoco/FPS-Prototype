using System;
using System.Collections.Generic;
using UnityEngine;
using Weapon; // Add using directive for the namespace

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    [Header("Weapon Slots")]
    [SerializeField] private Transform[] weaponSlots = new Transform[3];

    [SerializeField] private int activeSlotIndex = 0;

    [Header("Ammo Management")]
    [SerializeField] private Dictionary<WeaponModel, int> totalAmmo = new(); // Changed from Weapon.WeaponModel

    // Serialize ammo for inspector (since Dictionary isn't serializable)
    [Header("Ammo Amounts")]
    [SerializeField] private int totalRifleAmmo = 0;

    [SerializeField] private int totalPistolAmmo = 0;
    [SerializeField] private int totalShotgunAmmo = 0;

    [Header("Throwables")]
    [SerializeField] private ThrowableInventory throwableInventory;

    // Events
    public event Action<WeaponBase> OnWeaponSwitched;

    public event Action<WeaponBase> OnWeaponPickedUp;

    public event Action<WeaponBase> OnWeaponDropped;

    public event Action<WeaponModel, int> OnAmmoChanged; // Changed from Weapon.WeaponModel

    // Properties
    public WeaponBase CurrentWeapon => GetWeaponInSlot(activeSlotIndex);

    public Transform ActiveSlot => weaponSlots[activeSlotIndex];
    public int ActiveSlotIndex => activeSlotIndex;
    public bool HasWeaponInActiveSlot => CurrentWeapon != null;

    // Legacy properties for backward compatibility
    public List<GameObject> weaponSlots_Legacy => new List<GameObject>();

    public GameObject activeWeaponSlot => weaponSlots[activeSlotIndex].gameObject;

    // Properties for throwables (for HUDManager compatibility)
    public int lethalsCount => throwableInventory.lethalsCount;

    public int tacticalsCount => throwableInventory.tacticalsCount;
    public Throwable.ThrowableType equippedLethal => throwableInventory.equippedLethal;
    public Throwable.ThrowableType equippedTactical => throwableInventory.equippedTactical;

    #region Initialization

    private void Awake()
    {
        InitializeSingleton();
        InitializeSlotsByName();
        InitializeAmmoSystem();
        InitializeThrowables();
        ValidateSetup();
    }

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void InitializeSlotsByName()
    {
        // Use this if the references keep getting lost
        weaponSlots = new Transform[3];
        weaponSlots[0] = GameObject.Find("WeaponSlot1")?.transform;
        weaponSlots[1] = GameObject.Find("WeaponSlot2")?.transform;
        weaponSlots[2] = GameObject.Find("WeaponSlot3")?.transform;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null)
            {
                Debug.LogError($"Could not find WeaponSlot{i + 1} in scene!");
            }
        }
    }

    private void InitializeAmmoSystem()
    {
        // Initialize ammo dictionary with inspector values
        totalAmmo[WeaponModel.AK47] = totalRifleAmmo; // Changed from Weapon.WeaponModel
        totalAmmo[WeaponModel.HandgunM1911] = totalPistolAmmo; // Changed from Weapon.WeaponModel
        // Add more weapon models as needed
    }

    private void InitializeThrowables()
    {
        if (throwableInventory == null)
        {
            throwableInventory = new ThrowableInventory();
        }
    }

    private void ValidateSetup()
    {
        if (weaponSlots == null || weaponSlots.Length == 0)
        {
            Debug.LogError("WeaponManager: No weapon slots assigned!");
            return;
        }

        // Check each slot individually
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null)
            {
                Debug.LogError($"WeaponManager: Weapon slot {i} is null! Please assign it in the inspector.");
            }
        }

        // Ensure we have 3 slots for backward compatibility
        if (weaponSlots.Length != 3)
        {
            Debug.LogWarning("WeaponManager: Expected 3 weapon slots for full compatibility");
        }
    }

    private void Start()
    {
        SwitchToSlot(activeSlotIndex);
    }

    #endregion Initialization

    #region Update Loop

    private void Update()
    {
        HandleSlotVisibility();
        HandleInput();
    }

    private void HandleSlotVisibility()
    {
        // Add null checks to prevent MissingReferenceException
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != null && weaponSlots[i].gameObject != null)
            {
                weaponSlots[i].gameObject.SetActive(i == activeSlotIndex);
            }
            else
            {
                Debug.LogWarning($"WeaponManager: Weapon slot {i} is null or destroyed!");
            }
        }
    }

    private void HandleInput()
    {
        HandleWeaponSwitching();
        HandleThrowables();
    }

    private void HandleWeaponSwitching()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToSlot(2);

        // Mouse wheel switching
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.1f) SwitchToNextSlot();
        else if (scroll < -0.1f) SwitchToPreviousSlot();
    }

    private void HandleThrowables()
    {
        HandleLethalThrowables();
        HandleTacticalThrowables();
    }

    private void HandleLethalThrowables()
    {
        if (Input.GetKey(KeyCode.G))
        {
            throwableInventory.forceMultiplier += Time.deltaTime;
            if (throwableInventory.forceMultiplier > throwableInventory.forceMultiplierLimit)
            {
                throwableInventory.forceMultiplier = throwableInventory.forceMultiplierLimit;
            }
        }

        if (Input.GetKeyUp(KeyCode.G))
        {
            if (throwableInventory.lethalsCount > 0)
            {
                ThrowLethal();
            }
            throwableInventory.forceMultiplier = 0;
        }
    }

    private void HandleTacticalThrowables()
    {
        if (Input.GetKey(KeyCode.Q))
        {
            throwableInventory.forceMultiplier += Time.deltaTime;
            if (throwableInventory.forceMultiplier > throwableInventory.forceMultiplierLimit)
            {
                throwableInventory.forceMultiplier = throwableInventory.forceMultiplierLimit;
            }
        }

        if (Input.GetKeyUp(KeyCode.Q))
        {
            if (throwableInventory.tacticalsCount > 0)
            {
                ThrowTactical();
            }
            throwableInventory.forceMultiplier = 0;
        }
    }

    #endregion Update Loop

    #region Weapon Management (New & Legacy Support)

    // New method for WeaponBase
    public bool PickupWeapon(WeaponBase weapon)
    {
        if (weapon == null)
        {
            Debug.LogWarning("WeaponManager: Attempted to pickup null weapon");
            return false;
        }

        return AddWeaponToActiveSlot(weapon);
    }

    // Legacy method for old Weapon class - convert to WeaponBase
    public void PickupWeapon(GameObject weaponGameObject)
    {
        if (weaponGameObject == null) return;

        WeaponBase weaponBase = weaponGameObject.GetComponent<WeaponBase>();
        if (weaponBase != null)
        {
            PickupWeapon(weaponBase);
            return;
        }

        // If no WeaponBase found, log warning
        Debug.LogWarning($"WeaponManager: GameObject {weaponGameObject.name} doesn't have WeaponBase component");
    }

    private bool AddWeaponToActiveSlot(WeaponBase newWeapon)
    {
        // Drop current weapon if slot is occupied
        WeaponBase currentWeapon = GetWeaponInSlot(activeSlotIndex);
        if (currentWeapon != null)
        {
            DropWeaponFromSlot(activeSlotIndex, newWeapon.transform.position);
        }

        // Add new weapon to slot
        return AddWeaponToSlot(newWeapon, activeSlotIndex);
    }

    private bool AddWeaponToSlot(WeaponBase weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            Debug.LogError($"WeaponManager: Invalid slot index {slotIndex}");
            return false;
        }

        Transform slot = weaponSlots[slotIndex];
        if (slot == null)
        {
            Debug.LogError($"WeaponManager: Weapon slot {slotIndex} is null");
            return false;
        }

        // Setup weapon transform
        weapon.transform.SetParent(slot, false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;

        // Configure weapon state
        weapon.SetActiveWeapon(slotIndex == activeSlotIndex);

        // Disable outline for active weapon
        var outline = weapon.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        OnWeaponPickedUp?.Invoke(weapon);
        Debug.Log($"Weapon {weapon.weaponModel} added to slot {slotIndex}");

        return true;
    }

    public bool DropWeaponFromSlot(int slotIndex, Vector3 dropPosition)
    {
        WeaponBase weapon = GetWeaponInSlot(slotIndex);
        if (weapon == null) return false;

        return DropWeapon(weapon, dropPosition);
    }

    public bool DropCurrentWeapon(Vector3 dropPosition)
    {
        return DropWeaponFromSlot(activeSlotIndex, dropPosition);
    }

    private bool DropWeapon(WeaponBase weapon, Vector3 dropPosition)
    {
        if (weapon == null) return false;

        // Deactivate weapon
        weapon.SetActiveWeapon(false);

        // Reset transform
        weapon.transform.SetParent(null);
        weapon.transform.position = dropPosition;
        weapon.transform.rotation = Quaternion.identity;

        // Re-enable outline for pickup
        var outline = weapon.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
        }

        OnWeaponDropped?.Invoke(weapon);
        Debug.Log($"Dropped weapon {weapon.weaponModel}");

        return true;
    }

    public WeaponBase GetWeaponInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            Debug.LogWarning($"WeaponManager: Invalid slot index {slotIndex}");
            return null;
        }

        if (weaponSlots[slotIndex] == null)
        {
            Debug.LogWarning($"WeaponManager: Weapon slot {slotIndex} is null");
            return null;
        }

        return weaponSlots[slotIndex].GetComponentInChildren<WeaponBase>();
    }

    #endregion Weapon Management (New & Legacy Support)

    #region Slot Switching

    public void SwitchActiveSlot(int slotNumber)
    {
        SwitchToSlot(slotNumber);
    }

    public void SwitchToSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return;
        if (slotIndex == activeSlotIndex) return;

        // Deactivate current weapon
        WeaponBase currentWeapon = CurrentWeapon;
        if (currentWeapon != null)
        {
            currentWeapon.SetActiveWeapon(false);
        }

        // Switch slot
        activeSlotIndex = slotIndex;

        // Activate new weapon
        WeaponBase newWeapon = CurrentWeapon;
        if (newWeapon != null)
        {
            newWeapon.SetActiveWeapon(true);
        }

        OnWeaponSwitched?.Invoke(newWeapon);
        Debug.Log($"Switched to slot {slotIndex} - {(newWeapon?.weaponModel.ToString() ?? "Empty")}");
    }

    public void SwitchToNextSlot()
    {
        int nextSlot = (activeSlotIndex + 1) % weaponSlots.Length;
        SwitchToSlot(nextSlot);
    }

    public void SwitchToPreviousSlot()
    {
        int prevSlot = (activeSlotIndex - 1 + weaponSlots.Length) % weaponSlots.Length;
        SwitchToSlot(prevSlot);
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < weaponSlots.Length;
    }

    #endregion Slot Switching

    #region Ammo Management

    public void PickupAmmo(AmmoBox ammoBox)
    {
        if (ammoBox == null) return;

        switch (ammoBox.ammoType)
        {
            case AmmoBox.AmmoType.PistolAmmo:
                totalPistolAmmo += ammoBox.ammoAmount;
                totalAmmo[WeaponModel.HandgunM1911] = totalPistolAmmo; // Changed from Weapon.WeaponModel
                break;

            case AmmoBox.AmmoType.RifleAmmo:
                totalRifleAmmo += ammoBox.ammoAmount;
                totalAmmo[WeaponModel.AK47] = totalRifleAmmo; // Changed from Weapon.WeaponModel
                break;

            case AmmoBox.AmmoType.ShotgunAmmo:
                totalShotgunAmmo += ammoBox.ammoAmount;
                // Add shotgun weapon model when implemented
                break;
        }

        OnAmmoChanged?.Invoke(ConvertAmmoTypeToWeaponModel(ammoBox.ammoType), ammoBox.ammoAmount);
        Debug.Log($"Picked up {ammoBox.ammoAmount} {ammoBox.ammoType} ammo");
    }

    public void DecreaseTotalAmmo(int bulletsToDecrease, WeaponModel thisWeaponModel) // Changed parameter type
    {
        switch (thisWeaponModel)
        {
            case WeaponModel.AK47: // Changed from Weapon.WeaponModel
                totalRifleAmmo -= bulletsToDecrease;
                totalAmmo[WeaponModel.AK47] = totalRifleAmmo;
                break;

            case WeaponModel.HandgunM1911: // Changed from Weapon.WeaponModel
                totalPistolAmmo -= bulletsToDecrease;
                totalAmmo[WeaponModel.HandgunM1911] = totalPistolAmmo;
                break;
        }

        OnAmmoChanged?.Invoke(thisWeaponModel, GetAmmoCount(thisWeaponModel));
    }

    public int CheckAmmoLeftFor(WeaponModel thisWeaponModel) // Changed parameter type
    {
        return GetAmmoCount(thisWeaponModel);
    }

    public int GetAmmoCount(WeaponModel weaponModel) // Changed parameter type
    {
        return weaponModel switch
        {
            WeaponModel.AK47 => totalRifleAmmo, // Changed from Weapon.WeaponModel
            WeaponModel.HandgunM1911 => totalPistolAmmo, // Changed from Weapon.WeaponModel
            _ => 0
        };
    }

    private WeaponModel ConvertAmmoTypeToWeaponModel(AmmoBox.AmmoType ammoType) // Changed return type
    {
        return ammoType switch
        {
            AmmoBox.AmmoType.PistolAmmo => WeaponModel.HandgunM1911, // Changed from Weapon.WeaponModel
            AmmoBox.AmmoType.RifleAmmo => WeaponModel.AK47, // Changed from Weapon.WeaponModel
            AmmoBox.AmmoType.ShotgunAmmo => WeaponModel.AK47, // Placeholder until shotgun implemented
            _ => WeaponModel.HandgunM1911 // Changed from Weapon.WeaponModel
        };
    }

    #endregion Ammo Management

    #region Throwable System

    public void PickupThrowable(Throwable throwable)
    {
        if (throwable == null) return;

        switch (throwable.throwableType)
        {
            case Throwable.ThrowableType.Frag:
                if (throwableInventory.equippedLethal == throwable.throwableType || throwableInventory.equippedLethal == Throwable.ThrowableType.None)
                {
                    throwableInventory.equippedLethal = throwable.throwableType;

                    if (throwableInventory.lethalsCount < throwableInventory.maxLethal)
                    {
                        throwableInventory.lethalsCount += 1;
                        if (InteractionManager.Instance?.hoveredThrowable?.gameObject == throwable.gameObject)
                        {
                            InteractionManager.Instance.hoveredThrowable = null;
                        }
                        Destroy(throwable.gameObject);
                        HUDManager.Instance?.UpdateThrowablesUI();
                    }
                    else
                    {
                        Debug.Log("Lethal capacity exceeded");
                    }
                }
                break;

            case Throwable.ThrowableType.Smoke:
                if (throwableInventory.equippedTactical == throwable.throwableType || throwableInventory.equippedTactical == Throwable.ThrowableType.None)
                {
                    throwableInventory.equippedTactical = throwable.throwableType;

                    if (throwableInventory.tacticalsCount < throwableInventory.maxTactical)
                    {
                        throwableInventory.tacticalsCount += 1;
                        if (InteractionManager.Instance?.hoveredThrowable?.gameObject == throwable.gameObject)
                        {
                            InteractionManager.Instance.hoveredThrowable = null;
                        }
                        Destroy(throwable.gameObject);
                        HUDManager.Instance?.UpdateThrowablesUI();
                    }
                    else
                    {
                        Debug.Log("Tactical capacity exceeded");
                    }
                }
                break;
        }
    }

    private void ThrowLethal()
    {
        GameObject lethalPrefab = GetThrowablePrefab(throwableInventory.equippedLethal);
        if (lethalPrefab == null) return;

        GameObject throwable = Instantiate(lethalPrefab, throwableInventory.throwableSpawn.position, Camera.main.transform.rotation);
        Rigidbody rb = throwable.GetComponent<Rigidbody>();

        if (rb != null)
        {
            float throwForce = throwableInventory.throwForce * throwableInventory.forceMultiplier;
            rb.AddForce(Camera.main.transform.forward * throwForce, ForceMode.Impulse);
        }

        Throwable throwableComponent = throwable.GetComponent<Throwable>();
        if (throwableComponent != null)
        {
            throwableComponent.hasbeenThrown = true;
        }

        throwableInventory.lethalsCount -= 1;

        if (throwableInventory.lethalsCount <= 0)
        {
            throwableInventory.equippedLethal = Throwable.ThrowableType.None;
        }

        HUDManager.Instance?.UpdateThrowablesUI();
    }

    private void ThrowTactical()
    {
        GameObject tacticalPrefab = GetThrowablePrefab(throwableInventory.equippedTactical);
        if (tacticalPrefab == null) return;

        GameObject throwable = Instantiate(tacticalPrefab, throwableInventory.throwableSpawn.position, Camera.main.transform.rotation);
        Rigidbody rb = throwable.GetComponent<Rigidbody>();

        if (rb != null)
        {
            float throwForce = throwableInventory.throwForce * throwableInventory.forceMultiplier;
            rb.AddForce(Camera.main.transform.forward * throwForce, ForceMode.Impulse);
        }

        Throwable throwableComponent = throwable.GetComponent<Throwable>();
        if (throwableComponent != null)
        {
            throwableComponent.hasbeenThrown = true;
        }

        throwableInventory.tacticalsCount -= 1;

        if (throwableInventory.tacticalsCount <= 0)
        {
            throwableInventory.equippedTactical = Throwable.ThrowableType.None;
        }

        HUDManager.Instance?.UpdateThrowablesUI();
    }

    private GameObject GetThrowablePrefab(Throwable.ThrowableType throwableType)
    {
        return throwableType switch
        {
            Throwable.ThrowableType.Frag => throwableInventory.fragPrefab,
            Throwable.ThrowableType.Smoke => throwableInventory.smokePrefab,
            _ => null
        };
    }

    #endregion Throwable System

    #region Public API

    public WeaponInfo GetCurrentWeaponInfo()
    {
        return CurrentWeapon?.GetWeaponInfo();
    }

    public bool HasAnyWeapon()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (GetWeaponInSlot(i) != null) return true;
        }
        return false;
    }

    public ThrowableInventory GetThrowableInventory()
    {
        return throwableInventory;
    }

    #endregion Public API
}

// Throwable inventory data class matching your existing pattern
[System.Serializable]
public class ThrowableInventory
{
    [Header("Throwable General")]
    public float throwForce = 10f;

    public Transform throwableSpawn;
    public float forceMultiplier = 0;
    public float forceMultiplierLimit = 2f;

    [Header("Lethals")]
    public int lethalsCount = 0;

    public int maxLethal = 3;
    public GameObject fragPrefab;
    public Throwable.ThrowableType equippedLethal;

    [Header("Tacticals")]
    public int tacticalsCount = 0;

    public int maxTactical = 3;
    public GameObject smokePrefab;
    public Throwable.ThrowableType equippedTactical;
}