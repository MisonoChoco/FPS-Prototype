#region Weapon Base Class (Core Logic - References WeaponData)

using System;
using System.Collections;
using UnityEngine;
using Weapon;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] protected WeaponData weaponData; // Main data source

    [SerializeField] protected WeaponReferences references;

    [Header("Weapon Info (Set from WeaponData)")]
    public Weapon.WeaponModel weaponModel;

    public Weapon.ShootingMode[] availableShootingModes = { Weapon.ShootingMode.Semi };

    // State
    public bool IsActiveWeapon { get; set; }

    public bool IsADS { get; protected set; }
    public bool IsReloading { get; protected set; }
    public bool ReadyToShoot { get; protected set; } = true;
    public int BulletsLeft { get; protected set; }
    public Weapon.ShootingMode CurrentShootingMode { get; protected set; }

    // Internal state
    protected bool isShooting;

    protected bool allowReset = true;
    protected int burstBulletsLeft;
    protected Coroutine reloadCoroutine;
    protected Coroutine burstCoroutine;
    protected Camera playerCamera;

    // Events
    public event Action<WeaponBase> OnWeaponFired;

    public event Action<WeaponBase> OnReloadStarted;

    public event Action<WeaponBase> OnReloadCompleted;

    public event Action<WeaponBase, bool> OnADSChanged;

    // Properties to access WeaponData
    public WeaponData Data => weaponData;

    protected virtual void Awake()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        if (weaponData != null)
        {
            // Set weapon info from ScriptableObject data
            weaponModel = weaponData.weaponModel;
            availableShootingModes = weaponData.availableShootingModes;
            CurrentShootingMode = weaponData.defaultShootingMode;
            BulletsLeft = weaponData.magazineSize;
            burstBulletsLeft = weaponData.bulletsPerBurst;
        }
        else
        {
            Debug.LogError($"WeaponData is null on weapon {gameObject.name}! Please assign a WeaponData ScriptableObject.");
            // Fallback values
            BulletsLeft = 30;
            CurrentShootingMode = availableShootingModes[0];
            burstBulletsLeft = 3;
        }

        playerCamera = Camera.main;
        ValidateReferences();
    }

    protected virtual void ValidateReferences()
    {
        if (references.bulletSpawn == null)
        {
            Debug.LogError($"Bullet spawn not set on weapon {gameObject.name}!");
        }

        if (references.weaponAnimator == null)
        {
            references.weaponAnimator = GetComponent<Animator>();
        }
    }

    protected virtual void Update()
    {
        if (!IsActiveWeapon) return;

        HandleInput();
        UpdateLayerMask();
    }

    protected virtual void HandleInput()
    {
        HandleAiming();
        HandleShooting();
        HandleReloading();
        HandleFireModeSwitch();
    }

    protected virtual void HandleAiming()
    {
        if (Input.GetMouseButtonDown(1))
        {
            EnterADS();
        }
        else if (Input.GetMouseButtonUp(1))
        {
            ExitADS();
        }
    }

    protected virtual void HandleShooting()
    {
        bool inputPressed = GetShootingInput();

        if (inputPressed && BulletsLeft <= 0)
        {
            PlayEmptySound();
            return;
        }

        if (inputPressed && ReadyToShoot && BulletsLeft > 0 && !IsReloading)
        {
            isShooting = true;
            FireWeapon();
        }
        else if (!inputPressed)
        {
            isShooting = false;
        }
    }

    protected virtual bool GetShootingInput()
    {
        return CurrentShootingMode switch
        {
            Weapon.ShootingMode.Auto => Input.GetKey(KeyCode.Mouse0),
            Weapon.ShootingMode.Semi => Input.GetKeyDown(KeyCode.Mouse0),
            Weapon.ShootingMode.Burst => Input.GetKeyDown(KeyCode.Mouse0),
            _ => false
        };
    }

    protected virtual void HandleReloading()
    {
        if (Input.GetKeyDown(KeyCode.R) && CanReload())
        {
            StartReload();
        }

        // Auto reload when empty (optional)
        if (ReadyToShoot && !isShooting && !IsReloading && BulletsLeft <= 0 && HasAmmoAvailable())
        {
            // StartReload();
        }
    }

    protected virtual void HandleFireModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.V) && availableShootingModes.Length > 1)
        {
            CycleFireMode();
        }
    }

    protected virtual void UpdateLayerMask()
    {
        int targetLayer = IsActiveWeapon ? LayerMask.NameToLayer("WeaponRender") : LayerMask.NameToLayer("Default");

        foreach (Transform child in transform)
        {
            child.gameObject.layer = targetLayer;
        }

        var outline = GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = !IsActiveWeapon;
        }
    }

    #region Aiming

    protected virtual void EnterADS()
    {
        IsADS = true;
        string adsAnimation = weaponData ? weaponData.adsEnterAnimation : "enterADS";
        references.weaponAnimator?.SetTrigger(adsAnimation);
        HUDManager.Instance?.Crosshair?.SetActive(false);
        OnADSChanged?.Invoke(this, true);
    }

    protected virtual void ExitADS()
    {
        IsADS = false;
        string adsAnimation = weaponData ? weaponData.adsExitAnimation : "exitADS";
        references.weaponAnimator?.SetTrigger(adsAnimation);
        HUDManager.Instance?.Crosshair?.SetActive(true);
        OnADSChanged?.Invoke(this, false);
    }

    #endregion Aiming

    #region Shooting

    protected virtual void FireWeapon()
    {
        if (!CanShoot()) return;

        BulletsLeft--;
        ReadyToShoot = false;

        // Create and launch projectile
        Vector3 shootDirection = CalculateShootDirection();
        CreateProjectile(shootDirection);

        // Visual and audio effects
        PlayShootingEffects();

        // Handle shooting mode specific logic
        HandleShootingMode();

        OnWeaponFired?.Invoke(this);

        // Reset shot timing using weaponData fire rate
        if (allowReset)
        {
            float fireRate = weaponData ? weaponData.fireRate : 600f;
            float fireDelay = 60f / fireRate; // Convert RPM to seconds
            Invoke(nameof(ResetShot), fireDelay);
            allowReset = false;
        }
    }

    protected virtual bool CanShoot()
    {
        return ReadyToShoot && BulletsLeft > 0 && !IsReloading;
    }

    protected virtual void CreateProjectile(Vector3 direction)
    {
        if (ProjectileFactory.Instance == null)
        {
            Debug.LogError("ProjectileFactory instance not found!");
            return;
        }

        // Get projectile settings from weaponData
        ProjectileType projectileType = weaponData ? weaponData.projectileType : references.projectileType;
        int pelletsToFire = weaponData ? weaponData.pelletsPerShot : 1;

        // Fire multiple pellets for shotguns
        for (int i = 0; i < pelletsToFire; i++)
        {
            Vector3 pelletDirection = direction;

            if (pelletsToFire > 1 && weaponData)
            {
                pelletDirection = ApplySpread(direction, weaponData.pelletSpread);
            }

            var projectile = ProjectileFactory.Instance.CreateProjectile(
                projectileType,
                references.bulletSpawn.position,
                Quaternion.LookRotation(pelletDirection)
            );

            if (projectile != null)
            {
                ConfigureProjectile(projectile, pelletDirection);
            }
        }
    }

    protected virtual void ConfigureProjectile(IProjectile projectile, Vector3 direction)
    {
        if (projectile is ProjectileBase projectileBase)
        {
            float muzzleVelocity = weaponData ? weaponData.muzzleVelocity : 400f;
            projectileBase.Launch(direction, muzzleVelocity);
        }
    }

    protected virtual Vector3 CalculateShootDirection()
    {
        // Raycast from camera center
        float range = weaponData ? weaponData.range : 100f;
        Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint;
        if (Physics.Raycast(cameraRay, out RaycastHit hit, range))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = cameraRay.GetPoint(range);
        }

        Vector3 shootDirection = (targetPoint - references.bulletSpawn.position).normalized;

        // Apply spread using weaponData
        float spreadAmount = IsADS ?
            (weaponData ? weaponData.adsSpread : 0.5f) :
            (weaponData ? weaponData.hipSpread : 2f);

        shootDirection = ApplySpread(shootDirection, spreadAmount);

        return shootDirection;
    }

    protected virtual Vector3 ApplySpread(Vector3 direction, float spreadAmount)
    {
        float spreadX = UnityEngine.Random.Range(-spreadAmount, spreadAmount);
        float spreadY = UnityEngine.Random.Range(-spreadAmount, spreadAmount);

        Vector3 spread = new Vector3(spreadX, spreadY, 0f);
        return (direction + spread * 0.1f).normalized;
    }

    protected virtual void HandleShootingMode()
    {
        switch (CurrentShootingMode)
        {
            case Weapon.ShootingMode.Burst:
                HandleBurstMode();
                break;

            case Weapon.ShootingMode.Semi:
            case Weapon.ShootingMode.Auto:
                break;
        }
    }

    protected virtual void HandleBurstMode()
    {
        burstBulletsLeft--;

        if (burstBulletsLeft > 0 && BulletsLeft > 0)
        {
            if (burstCoroutine != null)
                StopCoroutine(burstCoroutine);

            burstCoroutine = StartCoroutine(BurstFireCoroutine());
        }
        else
        {
            burstBulletsLeft = weaponData ? weaponData.bulletsPerBurst : 3;
        }
    }

    protected virtual IEnumerator BurstFireCoroutine()
    {
        float burstDelay = weaponData ? weaponData.burstDelay : 0.1f;
        yield return new WaitForSeconds(burstDelay);

        if (BulletsLeft > 0)
        {
            FireWeapon();
        }
    }

    protected virtual void PlayShootingEffects()
    {
        // Muzzle flash
        if (references.muzzleFlash != null)
        {
            references.muzzleFlash.Play();
        }

        // Muzzle light
        if (references.muzzleLight != null)
        {
            StartCoroutine(FlashMuzzleLight());
        }

        // Shell ejection
        EjectShell();

        // Animation using weaponData animation names
        string recoilTrigger = IsADS ?
            (weaponData ? weaponData.shootADSAnimation : "RECOIL_ADS") :
            (weaponData ? weaponData.shootAnimation : "RECOIL");
        references.weaponAnimator?.SetTrigger(recoilTrigger);

        // Audio
        SoundManager.Instance?.PlayShootingSound(weaponModel);
    }

    protected virtual IEnumerator FlashMuzzleLight()
    {
        if (references.muzzleLight != null)
        {
            references.muzzleLight.SetActive(true);
            float duration = weaponData ? weaponData.muzzleLightDuration : 0.02f;
            yield return new WaitForSeconds(duration);
            references.muzzleLight.SetActive(false);
        }
    }

    protected virtual void EjectShell()
    {
        if (references.shellCasing != null && references.ejectionPort != null)
        {
            GameObject shell = Instantiate(references.shellCasing, references.ejectionPort.position, references.ejectionPort.rotation);

            Rigidbody shellRb = shell.GetComponent<Rigidbody>();
            if (shellRb != null)
            {
                Vector3 ejectionForce = references.ejectionPort.right * UnityEngine.Random.Range(2f, 4f) +
                                       references.ejectionPort.up * UnityEngine.Random.Range(1f, 2f);
                shellRb.AddForce(ejectionForce, ForceMode.Impulse);
                shellRb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            Destroy(shell, 5f);
        }
    }

    protected virtual void PlayEmptySound()
    {
        SoundManager.Instance?.EmptyShooting?.Play();
    }

    protected virtual void ResetShot()
    {
        ReadyToShoot = true;
        allowReset = true;
    }

    #endregion Shooting

    #region Reloading

    protected virtual bool CanReload()
    {
        int magSize = weaponData ? weaponData.magazineSize : 30;
        return !IsReloading &&
               BulletsLeft < magSize &&
               HasAmmoAvailable();
    }

    protected virtual bool HasAmmoAvailable()
    {
        return WeaponManager.Instance?.CheckAmmoLeftFor(weaponModel) > 0;
    }

    protected virtual void StartReload()
    {
        if (reloadCoroutine != null)
            StopCoroutine(reloadCoroutine);

        reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    protected virtual IEnumerator ReloadCoroutine()
    {
        ReadyToShoot = false;
        IsReloading = true;

        // Stop any burst firing
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstBulletsLeft = weaponData ? weaponData.bulletsPerBurst : 3;
        }

        SoundManager.Instance?.PlayReloadSound(weaponModel);
        string reloadAnimation = weaponData ? weaponData.reloadAnimation : "RELOAD";
        references.weaponAnimator?.SetTrigger(reloadAnimation);
        OnReloadStarted?.Invoke(this);

        float reloadTime = weaponData ? weaponData.reloadTime : 2f;
        float elapsed = 0f;
        bool cancelled = false;

        while (elapsed < reloadTime && !cancelled)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CancelReload();
                cancelled = true;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!cancelled)
        {
            CompleteReload();
        }
    }

    protected virtual void CancelReload()
    {
        IsReloading = false;
        ReadyToShoot = true;

        string reloadAnimation = weaponData ? weaponData.reloadAnimation : "RELOAD";
        string idleAnimation = weaponData ? weaponData.idleAnimation : "Idle";
        references.weaponAnimator?.ResetTrigger(reloadAnimation);
        references.weaponAnimator?.CrossFade(idleAnimation, 0.1f);
        SoundManager.Instance?.StopReloadSound(weaponModel);

        Debug.Log("Reload cancelled");
    }

    protected virtual void CompleteReload()
    {
        ReadyToShoot = true;
        IsReloading = false;

        int magSize = weaponData ? weaponData.magazineSize : 30;
        int bulletsNeeded = magSize - BulletsLeft;
        int availableAmmo = WeaponManager.Instance?.CheckAmmoLeftFor(weaponModel) ?? 0;
        int bulletsToReload = Mathf.Min(bulletsNeeded, availableAmmo);

        BulletsLeft += bulletsToReload;
        WeaponManager.Instance?.DecreaseTotalAmmo(bulletsToReload, weaponModel);

        OnReloadCompleted?.Invoke(this);
        Debug.Log($"Reload completed. Bullets: {BulletsLeft}/{magSize}");
    }

    #endregion Reloading

    #region Fire Mode

    protected virtual void CycleFireMode()
    {
        int currentIndex = Array.IndexOf(availableShootingModes, CurrentShootingMode);
        int nextIndex = (currentIndex + 1) % availableShootingModes.Length;
        CurrentShootingMode = availableShootingModes[nextIndex];

        Debug.Log($"Fire mode changed to: {CurrentShootingMode}");
    }

    #endregion Fire Mode

    #region Public API

    public virtual void SetActiveWeapon(bool active)
    {
        IsActiveWeapon = active;
        Animator animator = references.weaponAnimator;
        if (animator != null)
        {
            animator.enabled = active;
        }

        if (!active)
        {
            isShooting = false;

            if (IsADS)
            {
                ExitADS();
            }

            GetComponent<Outline>().enabled = false;
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
    }

    public virtual WeaponInfo GetWeaponInfo()
    {
        return new WeaponInfo
        {
            Model = weaponModel,
            Damage = weaponData ? weaponData.damage : 25,
            FireRate = weaponData ? weaponData.fireRate : 600f,
            Range = weaponData ? weaponData.range : 100f,
            BulletsLeft = BulletsLeft,
            MagSize = weaponData ? weaponData.magazineSize : 30,
            CurrentFireMode = CurrentShootingMode,
            AvailableFireModes = availableShootingModes,
            IsReloading = IsReloading,
            IsADS = IsADS,
            WeaponName = weaponData ? weaponData.weaponName : "Unknown Weapon",
            AmmoType = weaponData ? weaponData.ammoType : AmmoType.Rifle556,
            Rarity = weaponData ? weaponData.rarity : WeaponRarity.Common
        };
    }

    public virtual void RefillAmmo()
    {
        int magSize = weaponData ? weaponData.magazineSize : 30;
        BulletsLeft = magSize;
    }

    // Get damage at specific distance using weaponData
    public virtual float GetDamageAtDistance(float distance)
    {
        if (weaponData != null)
        {
            return weaponData.GetDamageAtDistance(distance);
        }
        return weaponData ? weaponData.damage : 25; // Fallback
    }

    #endregion Public API
}

#region Support Classes

namespace Weapon
{
    [System.Serializable]
    public class WeaponInfo
    {
        public WeaponModel Model;
        public string WeaponName;
        public int Damage;
        public float FireRate;
        public float Range;
        public int BulletsLeft;
        public int MagSize;
        public ShootingMode CurrentFireMode;
        public ShootingMode[] AvailableFireModes;
        public bool IsReloading;
        public bool IsADS;
        public AmmoType AmmoType;
        public WeaponRarity Rarity;
    }

    public enum WeaponModel
    {
        HandgunM1911,
        AK47,
        M4A1,
        Shotgun,
        SniperRifle
    }

    public enum ShootingMode
    {
        Semi,
        Burst,
        Auto
    }

    public enum GunType
    {
        MagFed,
        RoundFed,
        Knife
    }
}

#endregion Support Classes

#region Weapon Configuration (Legacy - for backward compatibility)

[System.Serializable]
public class WeaponStats
{
    [Header("Basic Properties")]
    public int damage = 25;

    public float fireRate = 600f;
    public float range = 100f;
    public float reloadTime = 2f;
    public int magSize = 30;

    [Header("Ballistics")]
    public float muzzleVelocity = 400f;

    public float hipSpread = 2f;
    public float adsSpread = 0.5f;
    public bool useGravity = false;

    [Header("Burst Mode")]
    public int bulletsPerBurst = 3;

    public float burstDelay = 0.1f;
}

[System.Serializable]
public class WeaponReferences
{
    [Header("Projectile")]
    public ProjectileType projectileType = ProjectileType.Bullet;

    public Transform bulletSpawn;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;

    public GameObject muzzleLight;
    public Transform ejectionPort;
    public GameObject shellCasing;

    [Header("Animation")]
    public Animator weaponAnimator;
}

#endregion Weapon Configuration (Legacy - for backward compatibility)

#endregion