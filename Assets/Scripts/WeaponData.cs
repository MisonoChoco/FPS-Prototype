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

    [Header("Viewmodel")]
    public Vector3 baseViewmodelPosition = Vector3.zero;

    public Vector3 baseViewmodelRotation = Vector3.zero;
    public float inspectDuration = 12f;

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

    [Header("Shell Reload System")]
    public bool useShellReload = false;

    public float shellLoadTime = 0.8f;
    public int maxShellsToLoad = 8;
    public string shellLoadAnimation = "SHELL_LOAD";

    [Header("Weapon Visual Effects")]
    public bool haveWeaponRecoil = true;

    public bool haveCameraRecoil = true;
    public bool haveRotationalSway = true;
    public bool haveBobbing = true;
    public bool haveJumpSway = true;

    [Header("Camera Recoil Values")]
    public float recoilRotationSpeed = 6f;

    public float recoilReturnSpeed = 25f;
    public Vector3 hipFireRecoil = new Vector3(4f, 4f, 4f);
    public Vector3 adsFireRecoil = new Vector3(2f, 2f, 2f);
    public float hRecoil = 0.215f;
    public float vRecoil = 0.221f;

    [Header("Weapon Recoil Values")]
    public float gunRecoilPositionSpeed = 8f;

    public float gunPositionReturnSpeed = 10f;
    public Vector3 recoilKickBackHip = new Vector3(0.015f, 0f, 0.05f);
    public Vector3 recoilKickBackAds = new Vector3(-0.08f, 0.01f, 0.009f);
    public float gunRecoilRotationSpeed = 8f;
    public float gunRotationReturnSpeed = 38f;
    public Vector3 recoilRotationHip = new Vector3(10f, 5f, 7f);
    public Vector3 recoilRotationAds = new Vector3(10f, 4f, 6f);

    [Header("Rotational Sway Values")]
    public float rotationSwayIntensity = 10f;

    public float rotationSwaySmoothness = 2f;

    [Header("Jump Sway Values")]
    public float jumpIntensity = 5f;

    public float weaponMaxClamp = 20f;
    public float weaponMinClamp = 20f;
    public float jumpSmooth = 15f;
    public float landingIntensity = 5f;
    public float landingSmooth = 15f;
    public float recoverySpeed = 50f;

    [Header("Weapon Bobbing Values")]
    public float bobbingMagnitude = 0.009f;

    public float idleSpeed = 2f;
    public float walkSpeedMultiplier = 4f;
    public float walkSpeedMax = 6f;
    public float aimReduction = 4f;

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

    [Header("Cycle-Based Firing")]
    public bool requiresCycling = false;

    public float cycleTime = 0.8f;
    public string cycleAnimation = "CYCLE";
    public AudioClip[] cycleSounds; // Array of pump/bolt sounds for randomization

    [Header("Magazine Drop")]
    public GameObject magazineDropPrefab;

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