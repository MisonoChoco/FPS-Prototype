using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Weapon; // Add using directive for the namespace

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; set; }

    [Header("Ammo")]
    public TextMeshProUGUI MagazineAmmoUI;

    public TextMeshProUGUI TotalAmmoUI;
    public Image AmmoTypeUI;

    [Header("Weapon")]
    public Image activeWeaponUI;

    public Image unActiveWeaponUI;
    public Image unActiveWeaponUI2;

    [Header("Throwables")]
    public Image lethalUI;

    public TextMeshProUGUI lethalAmountUI;

    public Image tacticalUI;
    public TextMeshProUGUI tacticalAmountUI;

    public Sprite emptySlot;
    public Sprite greySlot;

    public GameObject Crosshair;

    private void Awake()
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

    private void Update()
    {
        UpdateWeaponUI();
        UpdateThrowablesUIVisual();
    }

    private void UpdateWeaponUI()
    {
        WeaponBase activeWeapon = WeaponManager.Instance?.CurrentWeapon;
        WeaponBase unActiveWeapon = GetUnactiveWeapon();

        if (activeWeapon != null)
        {
            // Get weapon info from the new system
            var weaponInfo = activeWeapon.GetWeaponInfo();

            // Update ammo display
            MagazineAmmoUI.text = $"{weaponInfo.BulletsLeft}";
            TotalAmmoUI.text = $"{WeaponManager.Instance.CheckAmmoLeftFor(weaponInfo.Model)}";

            // Update weapon and ammo sprites
            WeaponModel model = weaponInfo.Model;
            AmmoTypeUI.sprite = GetAmmoSprite(model);
            activeWeaponUI.sprite = GetWeaponSprite(model);

            // Update inactive weapon UI
            if (unActiveWeapon != null)
            {
                unActiveWeaponUI.sprite = GetWeaponSprite(unActiveWeapon.weaponModel);
            }
            else
            {
                unActiveWeaponUI.sprite = emptySlot;
            }
        }
        else
        {
            // No active weapon - clear UI
            MagazineAmmoUI.text = "";
            TotalAmmoUI.text = "";

            AmmoTypeUI.sprite = emptySlot;
            activeWeaponUI.sprite = emptySlot;
            unActiveWeaponUI.sprite = emptySlot;
        }

        // Always clear the dummy slot
        unActiveWeaponUI2.sprite = emptySlot;
    }

    private void UpdateThrowablesUIVisual()
    {
        if (WeaponManager.Instance.lethalsCount <= 0)
        {
            lethalUI.sprite = greySlot;
        }

        if (WeaponManager.Instance.tacticalsCount <= 0)
        {
            tacticalUI.sprite = greySlot;
        }
    }

    private Sprite GetWeaponSprite(WeaponModel model) // Changed parameter type
    {
        switch (model)
        {
            case WeaponModel.HandgunM1911: // Changed from Weapon.WeaponModel
                return Resources.Load<Sprite>("M1911_Weapon");

            case WeaponModel.AK47: // Changed from Weapon.WeaponModel
                return Resources.Load<Sprite>("AK47_Weapon");

            case WeaponModel.Shotgun: // Add shotgun support
                return Resources.Load<Sprite>("Shotgun_Weapon");

            case WeaponModel.SniperRifle: // Add sniper support
                return Resources.Load<Sprite>("Sniper_Weapon");

            default:
                return emptySlot;
        }
    }

    private Sprite GetAmmoSprite(WeaponModel model) // Changed parameter type
    {
        switch (model)
        {
            case WeaponModel.HandgunM1911: // Changed from Weapon.WeaponModel
                return Resources.Load<Sprite>("Pistol_Ammo");

            case WeaponModel.AK47: // Changed from Weapon.WeaponModel
            case WeaponModel.SniperRifle:
                return Resources.Load<Sprite>("Rifle_Ammo");

            case WeaponModel.Shotgun: // Add shotgun support
                return Resources.Load<Sprite>("Shotgun_Ammo");

            default:
                return emptySlot;
        }
    }

    private WeaponBase GetUnactiveWeapon()
    {
        // Find the first weapon that's not in the active slot
        for (int i = 0; i < 3; i++) // Assuming 3 weapon slots
        {
            if (i != WeaponManager.Instance.ActiveSlotIndex)
            {
                WeaponBase weapon = WeaponManager.Instance.GetWeaponInSlot(i);
                if (weapon != null)
                {
                    return weapon;
                }
            }
        }
        return null;
    }

    public void UpdateThrowablesUI()
    {
        lethalAmountUI.text = $"{WeaponManager.Instance.lethalsCount}";
        tacticalAmountUI.text = $"{WeaponManager.Instance.tacticalsCount}";

        // Update lethal throwable sprite
        switch (WeaponManager.Instance.equippedLethal)
        {
            case Throwable.ThrowableType.Frag:
                var fragSprite = Resources.Load<Sprite>("Frag");
                if (fragSprite != null)
                {
                    lethalUI.sprite = fragSprite;
                }
                else
                {
                    // Fallback to loading GameObject and getting sprite
                    var fragObj = Resources.Load<GameObject>("Frag");
                    if (fragObj != null)
                    {
                        var spriteRenderer = fragObj.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            lethalUI.sprite = spriteRenderer.sprite;
                        }
                    }
                }
                break;

            case Throwable.ThrowableType.None:
                lethalUI.sprite = greySlot;
                break;
        }

        // Update tactical throwable sprite
        switch (WeaponManager.Instance.equippedTactical)
        {
            case Throwable.ThrowableType.Smoke:
                var smokeSprite = Resources.Load<Sprite>("Smoke");
                if (smokeSprite != null)
                {
                    tacticalUI.sprite = smokeSprite;
                }
                else
                {
                    // Fallback to loading GameObject and getting sprite
                    var smokeObj = Resources.Load<GameObject>("Smoke");
                    if (smokeObj != null)
                    {
                        var spriteRenderer = smokeObj.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            tacticalUI.sprite = spriteRenderer.sprite;
                        }
                    }
                }
                break;

            case Throwable.ThrowableType.None:
                tacticalUI.sprite = greySlot;
                break;
        }
    }
}