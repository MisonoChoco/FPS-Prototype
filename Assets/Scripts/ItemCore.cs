using System;
using UnityEngine;

public class ItemCore : MonoBehaviour
{
    [Header("Item Configuration")]
    [SerializeField] private ItemType itemType;

    [SerializeField] private string itemName;

    [TextArea(2, 4)]
    [SerializeField] private string itemDescription;

    [Header("Stack Settings")]
    [SerializeField] private bool isStackable = false;

    [SerializeField] private int maxStackSize = 1;
    [SerializeField] private int currentStackCount = 1;

    [Header("Usage Settings")]
    [SerializeField] private int maxUsageCount = 1;

    [SerializeField] private int currentUsageCount = 1;

    [Header("Item Specific Values")]
    [SerializeField] private int armorAmount = 50;

    [SerializeField] private int healAmount = 0;

    // Properties
    public ItemType ItemType => itemType;

    public string ItemName => itemName;
    public string ItemDescription => itemDescription;
    public bool IsStackable => isStackable;
    public int MaxStackSize => maxStackSize;
    public int CurrentStackCount => currentStackCount;
    public int MaxUsageCount => maxUsageCount;
    public int CurrentUsageCount => currentUsageCount;
    public bool IsActiveItem { get; private set; } = false;

    // Events
    public event Action<ItemCore> OnItemUsed;

    public event Action<ItemCore> OnItemDepleted;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    public void SetActiveItem(bool active)
    {
        IsActiveItem = active;
        gameObject.SetActive(active);

        // Disable outline when active
        var outline = GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = !active;
        }

        // Disable animator when not active (like weapon system)
        if (animator != null)
        {
            animator.enabled = active;
        }
    }

    public bool CanStack()
    {
        return isStackable && currentStackCount < maxStackSize;
    }

    public bool TryAddToStack(int amount = 1)
    {
        if (!CanStack()) return false;

        int amountToAdd = Mathf.Min(amount, maxStackSize - currentStackCount);
        currentStackCount += amountToAdd;

        Debug.Log($"{itemName}: Stacked {amountToAdd}. Current stack: {currentStackCount}/{maxStackSize}");
        return true;
    }

    public void RemoveFromStack(int amount = 1)
    {
        currentStackCount = Mathf.Max(0, currentStackCount - amount);
    }

    public bool CanUse()
    {
        return currentUsageCount > 0 && currentStackCount > 0;
    }

    public void UseItem()
    {
        if (!CanUse())
        {
            Debug.LogWarning($"Cannot use {itemName}: No uses remaining or empty stack");
            return;
        }

        Debug.Log($"Using {itemName} (Type: {itemType})");

        // Play animation if available
        if (animator != null)
        {
            animator.SetTrigger("Use");
        }

        // Execute item-specific functionality
        switch (itemType)
        {
            case ItemType.ArmorPlate:
                UseArmorPlate();
                break;

            case ItemType.Medkit:
                UseMedkit();
                break;

            case ItemType.Bandage:
                UseBandage();
                break;

            default:
                Debug.LogWarning($"No use function defined for {itemType}");
                break;
        }

        OnItemUsed?.Invoke(this);

        // Decrease usage count
        currentUsageCount--;

        // If item is depleted, remove from stack
        if (currentUsageCount <= 0)
        {
            RemoveFromStack(1);

            // Reset usage count if there are more in stack
            if (currentStackCount > 0)
            {
                currentUsageCount = maxUsageCount;
            }
            else
            {
                OnItemDepleted?.Invoke(this);
            }
        }
    }

    private void UseArmorPlate()
    {
        Player player = UnityEngine.Object.FindFirstObjectByType<Player>();
        if (player != null)
        {
            player.AddArmor(armorAmount);
            Debug.Log($"Added {armorAmount} armor to player");
        }
    }

    private void UseMedkit()
    {
        Player player = UnityEngine.Object.FindFirstObjectByType<Player>();
        if (player != null)
        {
            player.Heal(healAmount);
            Debug.Log($"Healed player for {healAmount} HP");
        }
    }

    private void UseBandage()
    {
        Player player = UnityEngine.Object.FindFirstObjectByType<Player>();
        if (player != null)
        {
            player.Heal(healAmount);
            Debug.Log($"Healed player for {healAmount} HP");
        }
    }

    public ItemInfo GetItemInfo()
    {
        return new ItemInfo
        {
            itemType = itemType,
            itemName = itemName,
            itemDescription = itemDescription,
            currentStackCount = currentStackCount,
            maxStackSize = maxStackSize,
            currentUsageCount = currentUsageCount,
            maxUsageCount = maxUsageCount
        };
    }
}

[System.Serializable]
public enum ItemType
{
    None,
    ArmorPlate,
    Medkit,
    Bandage,
    Ammo,
    Tool
}

[System.Serializable]
public struct ItemInfo
{
    public ItemType itemType;
    public string itemName;
    public string itemDescription;
    public int currentStackCount;
    public int maxStackSize;
    public int currentUsageCount;
    public int maxUsageCount;

    public string GetDisplayText()
    {
        if (maxStackSize > 1)
        {
            return $"{itemName} ({currentStackCount}/{maxStackSize})";
        }
        return itemName;
    }
}