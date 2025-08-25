using System;
using System.Collections;
using UnityEngine;
using Weapon;

#region ScriptableObject Weapon Data

[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapons/New WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Information")]
    public string weaponName = "Default Weapon";

    public WeaponModel weaponModel = WeaponModel.HandgunM1911;
    public GunType gunType = GunType.MagFed;

    [TextArea(2, 4)]
    public string description = "A standard weapon";

    public Sprite weaponIcon;

    [Header("Combat Stats")]
    public int damage = 25;

    public float fireRate = 600f; // Rounds per minute
    public float range = 100f;
    public int magazineSize = 30;
    public float reloadTime = 2f;

    [Header("Ballistics & Accuracy")]
    public float muzzleVelocity = 400f;

    public float hipSpread = 2f;
    public float adsSpread = 0.5f;
    public bool useGravity = false;
    public float dropOffStart = 50f; // Distance where damage starts dropping
    public float dropOffEnd = 100f; // Distance where damage reaches minimum
    public float minDamageMultiplier = 0.3f; // Minimum damage as percentage of base damage

    [Header("Fire Modes")]
    public ShootingMode[] availableShootingModes = { ShootingMode.Semi };

    public ShootingMode defaultShootingMode = ShootingMode.Semi;

    [Header("Burst Settings")]
    public int bulletsPerBurst = 3;

    public float burstDelay = 0.1f;

    [Header("Projectile Settings")]
    public ProjectileType projectileType = ProjectileType.Bullet;

    public int pelletsPerShot = 1; // For shotguns
    public float pelletSpread = 0f; // Additional spread for multiple pellets

    [Header("Audio Settings")]
    public AudioClip shootSound;

    public AudioClip reloadSound;
    public AudioClip emptySound;
    public AudioClip switchModeSound;

    [Range(0f, 1f)]
    public float audioVolume = 1f;

    [Header("Visual Effects")]
    public GameObject muzzleFlashPrefab;

    public GameObject shellCasingPrefab;
    public GameObject impactEffectPrefab;
    public Color muzzleLightColor = Color.yellow;
    public float muzzleLightIntensity = 2f;
    public float muzzleLightDuration = 0.02f;

    [Header("Animation Names")]
    public string shootAnimation = "RECOIL";

    public string shootADSAnimation = "RECOIL_ADS";
    public string reloadAnimation = "RELOAD";
    public string adsEnterAnimation = "enterADS";
    public string adsExitAnimation = "exitADS";
    public string idleAnimation = "Idle";

    [Header("Weapon Handling")]
    public float aimSpeed = 8f; // How fast to aim down sights

    public float weaponSway = 1f; // Weapon sway multiplier
    public float recoilAmount = 1f; // Recoil intensity multiplier
    public Vector3 recoilPattern = new Vector3(0, 1, -0.5f); // X=horizontal, Y=vertical, Z=rotational

    [Header("Ammo Configuration")]
    public AmmoType ammoType = AmmoType.Rifle556;

    public int maxAmmoReserve = 240; // Maximum reserve ammo
    public bool infiniteAmmo = false;

    [Header("Weapon Rarity & Economy")]
    public WeaponRarity rarity = WeaponRarity.Common;

    public int purchasePrice = 100;
    public int sellPrice = 50;
    public bool canBePurchased = true;
    public bool canBeDropped = true;

    [Header("Attachment Compatibility")]
    public AttachmentSlot[] supportedAttachments = new AttachmentSlot[0];

    // Validation
    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        damage = Mathf.Max(1, damage);
        fireRate = Mathf.Max(1f, fireRate);
        reloadTime = Mathf.Max(0.1f, reloadTime);
        range = Mathf.Max(1f, range);
        dropOffEnd = Mathf.Max(dropOffStart, dropOffEnd);

        // Ensure default shooting mode is available
        if (availableShootingModes.Length > 0)
        {
            bool hasDefaultMode = false;
            foreach (var mode in availableShootingModes)
            {
                if (mode == defaultShootingMode)
                {
                    hasDefaultMode = true;
                    break;
                }
            }
            if (!hasDefaultMode)
            {
                defaultShootingMode = availableShootingModes[0];
            }
        }
    }

    // Helper method for damage calculation
    public float GetDamageAtDistance(float distance)
    {
        if (distance <= dropOffStart)
            return damage;

        if (distance >= dropOffEnd)
            return damage * minDamageMultiplier;

        float t = (distance - dropOffStart) / (dropOffEnd - dropOffStart);
        float damageMultiplier = Mathf.Lerp(1f, minDamageMultiplier, t);
        return damage * damageMultiplier;
    }
}

#region Supporting Enums

public enum AmmoType
{
    Pistol9mm,
    Rifle556,
    Rifle762,
    Shotgun12Gauge,
    SniperRifle,
    Special
}

public enum WeaponRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum AttachmentSlot
{
    Scope,
    Barrel,
    Stock,
    Grip,
    Magazine,
    Laser,
    Flashlight
}

#endregion Supporting Enums

#endregion ScriptableObject Weapon Data