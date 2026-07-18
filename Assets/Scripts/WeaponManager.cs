using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Weapon;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    [Header("Weapon Slots")]
    [SerializeField] private Transform[] weaponSlots = new Transform[3];

    [SerializeField] private int activeSlotIndex = 0;

    // Ammo tracked by AmmoType — adding any new weapon using an existing AmmoType
    // just works, zero code changes needed
    [Header("Ammo Amounts (by AmmoType)")]
    [SerializeField] private int ammo_Pistol9mm = 0;

    [SerializeField] private int ammo_Rifle556 = 0;
    [SerializeField] private int ammo_Rifle762 = 0;
    [SerializeField] private int ammo_Shotgun12Gauge = 0;
    [SerializeField] private int ammo_SniperRifle = 0;
    [SerializeField] private int ammo_Special = 0;

    private Dictionary<AmmoType, int> totalAmmo = new();

    [Header("Throwables")]
    [SerializeField] private ThrowableInventory throwableInventory;

    // Events
    public event Action<WeaponBase> OnWeaponSwitched;

    public event Action<WeaponBase> OnWeaponPickedUp;

    public event Action<WeaponBase> OnWeaponDropped;

    public event Action<AmmoType, int> OnAmmoChanged;

    // Properties
    public WeaponBase CurrentWeapon => GetWeaponInSlot(activeSlotIndex);

    public Transform ActiveSlot => weaponSlots[activeSlotIndex];
    public int ActiveSlotIndex => activeSlotIndex;
    public bool HasWeaponInActiveSlot => CurrentWeapon != null;

    public GameObject activeWeaponSlot => weaponSlots[activeSlotIndex].gameObject;

    // Throwable properties
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void InitializeSlotsByName()
    {
        weaponSlots = new Transform[3];
        weaponSlots[0] = GameObject.Find("WeaponSlot1")?.transform;
        weaponSlots[1] = GameObject.Find("WeaponSlot2")?.transform;
        weaponSlots[2] = GameObject.Find("WeaponSlot3")?.transform;

        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] == null)
                Debug.LogError($"Could not find WeaponSlot{i + 1} in scene!");
    }

    private void InitializeAmmoSystem()
    {
        // Matches the AmmoType enum exactly — no per-weapon hardcoding
        totalAmmo[AmmoType.Parabellum9mm] = ammo_Pistol9mm;
        totalAmmo[AmmoType.NATO556] = ammo_Rifle556;
        totalAmmo[AmmoType.Soviet762] = ammo_Rifle762;
        totalAmmo[AmmoType.Gauge12] = ammo_Shotgun12Gauge;
        totalAmmo[AmmoType.Winchester308] = ammo_SniperRifle;
        totalAmmo[AmmoType.ActionExpress50] = ammo_Special;
    }

    private void InitializeThrowables()
    {
        if (throwableInventory == null)
            throwableInventory = new ThrowableInventory();
    }

    private void ValidateSetup()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] == null)
                Debug.LogError($"WeaponManager: Weapon slot {i} is null!");

        if (weaponSlots.Length != 3)
            Debug.LogWarning("WeaponManager: Expected 3 weapon slots.");
    }

    private void Start() => SwitchToSlot(activeSlotIndex);

    #endregion Initialization

    #region Update Loop

    private void Update()
    {
        HandleSlotVisibility();
        HandleInput();
    }

    private void HandleSlotVisibility()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != null)
                weaponSlots[i].gameObject.SetActive(i == activeSlotIndex);
            else
                Debug.LogWarning($"WeaponManager: Weapon slot {i} is null or destroyed!");
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
            throwableInventory.forceMultiplier = Mathf.Min(
                throwableInventory.forceMultiplier + Time.deltaTime,
                throwableInventory.forceMultiplierLimit);

        if (Input.GetKeyUp(KeyCode.G))
        {
            if (throwableInventory.lethalsCount > 0) ThrowLethal();
            throwableInventory.forceMultiplier = 0;
        }
    }

    private void HandleTacticalThrowables()
    {
        if (Input.GetKey(KeyCode.Q))
            throwableInventory.forceMultiplier = Mathf.Min(
                throwableInventory.forceMultiplier + Time.deltaTime,
                throwableInventory.forceMultiplierLimit);

        if (Input.GetKeyUp(KeyCode.Q))
        {
            if (throwableInventory.tacticalsCount > 0) ThrowTactical();
            throwableInventory.forceMultiplier = 0;
        }
    }

    #endregion Update Loop

    #region Weapon Management

    public bool PickupWeapon(WeaponBase weapon)
    {
        if (weapon == null) { Debug.LogWarning("WeaponManager: Null weapon pickup"); return false; }
        return AddWeaponToActiveSlot(weapon);
    }

    public void PickupWeapon(GameObject weaponGameObject)
    {
        if (weaponGameObject == null) return;
        WeaponBase weaponBase = weaponGameObject.GetComponent<WeaponBase>();
        if (weaponBase != null) PickupWeapon(weaponBase);
        else Debug.LogWarning($"WeaponManager: {weaponGameObject.name} has no WeaponBase");
    }

    private bool AddWeaponToActiveSlot(WeaponBase newWeapon)
    {
        WeaponBase current = GetWeaponInSlot(activeSlotIndex);
        if (current != null) DropWeaponFromSlot(activeSlotIndex, newWeapon.transform.position);
        return AddWeaponToSlot(newWeapon, activeSlotIndex);
    }

    private bool AddWeaponToSlot(WeaponBase weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
        { Debug.LogError($"WeaponManager: Invalid slot {slotIndex}"); return false; }

        Transform slot = weaponSlots[slotIndex];
        if (slot == null) { Debug.LogError($"WeaponManager: Slot {slotIndex} is null"); return false; }

        weapon.transform.SetParent(slot, false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        bool isCurrentActiveSlot = (slotIndex == activeSlotIndex);
        weapon.SetActiveWeapon(isCurrentActiveSlot, isFirstPickup: isCurrentActiveSlot);

        var outline = weapon.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        OnWeaponPickedUp?.Invoke(weapon);
        return true;
    }

    public bool DropWeaponFromSlot(int slotIndex, Vector3 dropPosition)
    {
        WeaponBase weapon = GetWeaponInSlot(slotIndex);
        return weapon != null && DropWeapon(weapon, dropPosition);
    }

    public bool DropCurrentWeapon(Vector3 dropPosition) =>
        DropWeaponFromSlot(activeSlotIndex, dropPosition);

    private bool DropWeapon(WeaponBase weapon, Vector3 dropPosition)
    {
        if (weapon == null) return false;
        weapon.SetActiveWeapon(false);
        weapon.transform.SetParent(null);
        weapon.transform.position = dropPosition;
        weapon.transform.rotation = Quaternion.identity;

        var outline = weapon.GetComponent<Outline>();
        if (outline != null) outline.enabled = true;

        OnWeaponDropped?.Invoke(weapon);
        return true;
    }

    public WeaponBase GetWeaponInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return null;
        if (weaponSlots[slotIndex] == null) return null;
        return weaponSlots[slotIndex].GetComponentInChildren<WeaponBase>();
    }

    #endregion Weapon Management

    #region Slot Switching

    public void SwitchToSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex) || slotIndex == activeSlotIndex) return;

        CurrentWeapon?.SetActiveWeapon(false);
        activeSlotIndex = slotIndex;

        WeaponBase newWeapon = CurrentWeapon;
        if (newWeapon != null)
        {
            newWeapon.SetActiveWeapon(true, isFirstPickup: false);
            StartCoroutine(EnableWeaponAnimatorDelayed(newWeapon));
        }

        OnWeaponSwitched?.Invoke(newWeapon);
    }

    private IEnumerator EnableWeaponAnimatorDelayed(WeaponBase weapon)
    {
        yield return new WaitForSecondsRealtime(0.05f);
        if (weapon != null && weapon.IsActiveWeapon)
            weapon.EnableAnimator();
    }

    public void SwitchToNextSlot() => SwitchToSlot((activeSlotIndex + 1) % weaponSlots.Length);

    public void SwitchToPreviousSlot() => SwitchToSlot((activeSlotIndex - 1 + weaponSlots.Length) % weaponSlots.Length);

    public void SwitchActiveSlot(int slotNumber) => SwitchToSlot(slotNumber);

    private bool IsValidSlotIndex(int i) => i >= 0 && i < weaponSlots.Length;

    #endregion Slot Switching

    #region Ammo Management

    // All ammo methods now use AmmoType — WeaponBase passes weaponData.ammoType
    public void DecreaseTotalAmmo(int amount, AmmoType ammoType)
    {
        if (!totalAmmo.ContainsKey(ammoType)) return;
        totalAmmo[ammoType] = Mathf.Max(0, totalAmmo[ammoType] - amount);
        SyncInspectorAmmoFields();
        OnAmmoChanged?.Invoke(ammoType, totalAmmo[ammoType]);
    }

    public int CheckAmmoLeftFor(AmmoType ammoType)
    {
        return totalAmmo.TryGetValue(ammoType, out int count) ? count : 0;
    }

    public bool PickupAmmo(AmmoBox ammoBox)
    {
        if (ammoBox == null) return false;

        if (!totalAmmo.ContainsKey(ammoBox.ammoType))
            totalAmmo[ammoBox.ammoType] = 0;

        totalAmmo[ammoBox.ammoType] += ammoBox.ammoAmount;
        SyncInspectorAmmoFields();
        OnAmmoChanged?.Invoke(ammoBox.ammoType, totalAmmo[ammoBox.ammoType]);
        return true;
    }

    // Keeps inspector fields in sync so you can see live values in editor
    private void SyncInspectorAmmoFields()
    {
        ammo_Pistol9mm = totalAmmo.GetValueOrDefault(AmmoType.Parabellum9mm);
        ammo_Rifle556 = totalAmmo.GetValueOrDefault(AmmoType.NATO556);
        ammo_Rifle762 = totalAmmo.GetValueOrDefault(AmmoType.Soviet762);
        ammo_Shotgun12Gauge = totalAmmo.GetValueOrDefault(AmmoType.Gauge12);
        ammo_SniperRifle = totalAmmo.GetValueOrDefault(AmmoType.Winchester308);
        ammo_Special = totalAmmo.GetValueOrDefault(AmmoType.ActionExpress50);
    }

    #endregion Ammo Management

    #region Throwable System

    public void PickupThrowable(Throwable throwable)
    {
        if (throwable == null) return;

        switch (throwable.throwableType)
        {
            case Throwable.ThrowableType.Frag:
                if (throwableInventory.equippedLethal == throwable.throwableType ||
                    throwableInventory.equippedLethal == Throwable.ThrowableType.None)
                {
                    throwableInventory.equippedLethal = throwable.throwableType;
                    if (throwableInventory.lethalsCount < throwableInventory.maxLethal)
                    {
                        throwableInventory.lethalsCount++;
                        if (InteractionManager.Instance?.hoveredThrowable?.gameObject == throwable.gameObject)
                            InteractionManager.Instance.hoveredThrowable = null;
                        Destroy(throwable.gameObject);
                        HUDManager.Instance?.UpdateThrowablesUI();
                    }
                }
                break;

            case Throwable.ThrowableType.Smoke:
                if (throwableInventory.equippedTactical == throwable.throwableType ||
                    throwableInventory.equippedTactical == Throwable.ThrowableType.None)
                {
                    throwableInventory.equippedTactical = throwable.throwableType;
                    if (throwableInventory.tacticalsCount < throwableInventory.maxTactical)
                    {
                        throwableInventory.tacticalsCount++;
                        if (InteractionManager.Instance?.hoveredThrowable?.gameObject == throwable.gameObject)
                            InteractionManager.Instance.hoveredThrowable = null;
                        Destroy(throwable.gameObject);
                        HUDManager.Instance?.UpdateThrowablesUI();
                    }
                }
                break;
        }
    }

    private void ThrowLethal()
    {
        GameObject prefab = GetThrowablePrefab(throwableInventory.equippedLethal);
        if (prefab == null) return;
        SpawnThrowable(prefab);
        throwableInventory.lethalsCount--;
        if (throwableInventory.lethalsCount <= 0)
            throwableInventory.equippedLethal = Throwable.ThrowableType.None;
        HUDManager.Instance?.UpdateThrowablesUI();
    }

    private void ThrowTactical()
    {
        GameObject prefab = GetThrowablePrefab(throwableInventory.equippedTactical);
        if (prefab == null) return;
        SpawnThrowable(prefab);
        throwableInventory.tacticalsCount--;
        if (throwableInventory.tacticalsCount <= 0)
            throwableInventory.equippedTactical = Throwable.ThrowableType.None;
        HUDManager.Instance?.UpdateThrowablesUI();
    }

    private void SpawnThrowable(GameObject prefab)
    {
        GameObject throwable = Instantiate(prefab,
            throwableInventory.throwableSpawn.position,
            Camera.main.transform.rotation);

        Rigidbody rb = throwable.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(Camera.main.transform.forward *
                throwableInventory.throwForce * throwableInventory.forceMultiplier,
                ForceMode.Impulse);

        var tc = throwable.GetComponent<Throwable>();
        if (tc != null) tc.hasbeenThrown = true;
    }

    private GameObject GetThrowablePrefab(Throwable.ThrowableType type) => type switch
    {
        Throwable.ThrowableType.Frag => throwableInventory.fragPrefab,
        Throwable.ThrowableType.Smoke => throwableInventory.smokePrefab,
        _ => null
    };

    #endregion Throwable System

    #region Public API

    public WeaponInfo GetCurrentWeaponInfo() => CurrentWeapon?.GetWeaponInfo();

    public bool HasAnyWeapon()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
            if (GetWeaponInSlot(i) != null) return true;
        return false;
    }

    public ThrowableInventory GetThrowableInventory() => throwableInventory;

    #endregion Public API
}

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