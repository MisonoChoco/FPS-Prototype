using System;
using TMPro;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Item Slots")]
    [SerializeField] private Transform[] itemSlots = new Transform[5];

    [SerializeField] private int activeItemSlotIndex = -1; // -1 means no item active

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI currentItemText;

    // Events
    public event Action<ItemCore> OnItemSwitched;

    public event Action<ItemCore> OnItemPickedUp;

    public event Action<ItemCore> OnItemDropped;

    public event Action<ItemCore> OnItemUsed;

    // Properties
    public ItemCore CurrentItem => GetItemInSlot(activeItemSlotIndex);

    public int ActiveItemSlotIndex => activeItemSlotIndex;
    public bool HasItemInActiveSlot => CurrentItem != null;

    #region Initialization

    private void Awake()
    {
        InitializeSingleton();
        InitializeSlotsByName();
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
        itemSlots = new Transform[5];
        itemSlots[0] = GameObject.Find("ItemSlot1")?.transform;
        itemSlots[1] = GameObject.Find("ItemSlot2")?.transform;
        itemSlots[2] = GameObject.Find("ItemSlot3")?.transform;
        itemSlots[3] = GameObject.Find("ItemSlot4")?.transform;
        itemSlots[4] = GameObject.Find("ItemSlot5")?.transform;

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == null)
            {
                Debug.LogError($"Could not find ItemSlot{i + 1} in scene!");
            }
        }
    }

    private void ValidateSetup()
    {
        if (itemSlots == null || itemSlots.Length == 0)
        {
            Debug.LogError("PlayerInventory: No item slots assigned!");
            return;
        }

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == null)
            {
                Debug.LogError($"PlayerInventory: Item slot {i} is null!");
            }
        }

        if (currentItemText == null)
        {
            Debug.LogWarning("PlayerInventory: Current item text UI not assigned!");
        }
    }

    private void Start()
    {
        UpdateItemUI();
    }

    #endregion Initialization

    #region Update Loop

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        HandleItemSwitching();
        HandleItemUsage();
        HandleItemDrop();
    }

    private void HandleItemSwitching()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchToSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchToSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SwitchToSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SwitchToSlot(4);
    }

    private void HandleItemUsage()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            UseCurrentItem();
        }
    }

    private void HandleItemDrop()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            DropCurrentItem();
        }
    }

    #endregion Update Loop

    #region Item Management

    public bool PickupItem(ItemCore item)
    {
        if (item == null)
        {
            Debug.LogWarning("PlayerInventory: Attempted to pickup null item");
            return false;
        }

        // Check if we already have this item type and can stack it
        int existingSlotIndex = FindItemSlot(item.ItemType);
        if (existingSlotIndex != -1)
        {
            ItemCore existingItem = GetItemInSlot(existingSlotIndex);
            if (existingItem != null && existingItem.CanStack())
            {
                bool stacked = existingItem.TryAddToStack(item.CurrentStackCount);
                if (stacked)
                {
                    Debug.Log($"Stacked {item.ItemName} into existing slot {existingSlotIndex + 4}");
                    UpdateItemUI();
                    return true;
                }
            }
        }

        // Find empty slot and add item
        int emptySlot = FindEmptySlot();
        if (emptySlot != -1)
        {
            return AddItemToSlot(item, emptySlot);
        }

        Debug.LogWarning("PlayerInventory: No empty slots available");
        return false;
    }

    private bool AddItemToSlot(ItemCore item, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= itemSlots.Length)
        {
            Debug.LogError($"PlayerInventory: Invalid slot index {slotIndex}");
            return false;
        }

        Transform slot = itemSlots[slotIndex];
        if (slot == null)
        {
            Debug.LogError($"PlayerInventory: Item slot {slotIndex} is null");
            return false;
        }

        // Disable physics (like weapon pickup)
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Disable collider
        Collider col = item.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // Setup item transform (like weapon system)
        item.transform.SetParent(slot, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = Vector3.one;

        // Configure item state - IMPORTANT: Don't call SetActiveItem here
        // Item will be activated when switched to
        item.gameObject.SetActive(false); // Start inactive like weapons

        OnItemPickedUp?.Invoke(item);
        UpdateItemUI();

        Debug.Log($"Item {item.ItemName} added to slot {slotIndex + 4}");
        return true;
    }

    public void DropCurrentItem()
    {
        if (activeItemSlotIndex == -1 || CurrentItem == null) return;

        Vector3 dropPosition = Camera.main.transform.position + Camera.main.transform.forward * 2f;
        DropItemFromSlot(activeItemSlotIndex, dropPosition);
    }

    private void DropItemFromSlot(int slotIndex, Vector3 dropPosition)
    {
        ItemCore item = GetItemInSlot(slotIndex);
        if (item == null) return;

        // Deactivate item (like weapon drop)
        item.SetActiveItem(false);

        // Reset transform
        item.transform.SetParent(null);
        item.transform.position = dropPosition;
        item.transform.rotation = Quaternion.identity;

        // Re-enable physics
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Re-enable collider
        Collider col = item.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        // Re-enable outline
        var outline = item.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
        }

        OnItemDropped?.Invoke(item);

        // Clear active slot if dropping from active slot
        if (slotIndex == activeItemSlotIndex)
        {
            activeItemSlotIndex = -1;

            // Switch back to weapons
            if (WeaponManager.Instance != null)
            {
                WeaponManager.Instance.SwitchToSlot(0);
            }
        }

        UpdateItemUI();
        Debug.Log($"Dropped item {item.ItemName}");
    }

    public void UseCurrentItem()
    {
        if (CurrentItem == null) return;

        if (!CurrentItem.CanUse())
        {
            Debug.LogWarning($"Cannot use {CurrentItem.ItemName}: depleted");
            return;
        }

        CurrentItem.UseItem();
        OnItemUsed?.Invoke(CurrentItem);

        // Check if item is depleted
        if (CurrentItem.CurrentStackCount <= 0)
        {
            Debug.Log($"{CurrentItem.ItemName} depleted, removing from slot");
            RemoveItemFromSlot(activeItemSlotIndex);
        }

        UpdateItemUI();
    }

    private void RemoveItemFromSlot(int slotIndex)
    {
        ItemCore item = GetItemInSlot(slotIndex);
        if (item == null) return;

        Destroy(item.gameObject);

        if (slotIndex == activeItemSlotIndex)
        {
            activeItemSlotIndex = -1;

            // Switch back to weapons
            if (WeaponManager.Instance != null)
            {
                WeaponManager.Instance.SwitchToSlot(0);
            }
        }

        UpdateItemUI();
    }

    #endregion Item Management

    #region Slot Management

    public void SwitchToSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return;

        // If slot is empty, return
        ItemCore itemInSlot = GetItemInSlot(slotIndex);
        if (itemInSlot == null)
        {
            Debug.Log($"Item slot {slotIndex + 4} is empty");
            return;
        }

        // If switching to same slot, toggle off (like weapon system)
        if (slotIndex == activeItemSlotIndex)
        {
            DeactivateCurrentItem();
            return;
        }

        // Deactivate current item
        DeactivateCurrentItem();

        // Hide all weapon slots (like your weapon system)
        if (WeaponManager.Instance != null)
        {
            for (int i = 0; i < 3; i++)
            {
                var weapon = WeaponManager.Instance.GetWeaponInSlot(i);
                if (weapon != null)
                {
                    weapon.SetActiveWeapon(false);
                }
            }
        }

        // Activate new item
        activeItemSlotIndex = slotIndex;
        ItemCore newItem = CurrentItem;
        if (newItem != null)
        {
            newItem.SetActiveItem(true);
        }

        OnItemSwitched?.Invoke(newItem);
        UpdateItemUI();

        Debug.Log($"Switched to item slot {slotIndex + 4} - {(newItem?.ItemName ?? "Empty")}");
    }

    private void DeactivateCurrentItem()
    {
        if (activeItemSlotIndex != -1 && CurrentItem != null)
        {
            CurrentItem.SetActiveItem(false);
        }
        activeItemSlotIndex = -1;
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < itemSlots.Length;
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (GetItemInSlot(i) == null)
            {
                return i;
            }
        }
        return -1;
    }

    private int FindItemSlot(ItemType itemType)
    {
        for (int i = 0; i < itemSlots.Length; i++)
        {
            ItemCore item = GetItemInSlot(i);
            if (item != null && item.ItemType == itemType)
            {
                return i;
            }
        }
        return -1;
    }

    public ItemCore GetItemInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= itemSlots.Length) return null;
        if (itemSlots[slotIndex] == null) return null;

        // Use GetComponentInChildren to find item even if inactive
        return itemSlots[slotIndex].GetComponentInChildren<ItemCore>(true);
    }

    #endregion Slot Management

    #region UI Management

    private void UpdateItemUI()
    {
        if (currentItemText == null) return;

        if (CurrentItem != null && activeItemSlotIndex != -1)
        {
            ItemInfo info = CurrentItem.GetItemInfo();
            currentItemText.text = info.GetDisplayText();
            currentItemText.gameObject.SetActive(true);
        }
        else
        {
            currentItemText.text = "";
            currentItemText.gameObject.SetActive(false);
        }
    }

    #endregion UI Management

    #region Public API

    public bool HasItem(ItemType itemType)
    {
        return FindItemSlot(itemType) != -1;
    }

    public int GetItemCount(ItemType itemType)
    {
        int slotIndex = FindItemSlot(itemType);
        if (slotIndex != -1)
        {
            ItemCore item = GetItemInSlot(slotIndex);
            return item?.CurrentStackCount ?? 0;
        }
        return 0;
    }

    public bool IsItemSlotActive()
    {
        return activeItemSlotIndex != -1;
    }

    #endregion Public API
}