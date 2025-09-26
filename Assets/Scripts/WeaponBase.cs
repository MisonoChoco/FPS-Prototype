#region Weapon Base (References WeaponData) CORE LOGIC IMPORTANT

using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using Weapon;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] protected WeaponData weaponData; // Main data source

    [SerializeField] protected WeaponReferences references;

    [Header("Weapon Info")]
    public Weapon.WeaponModel weaponModel;

    public Weapon.ShootingMode[] availableShootingModes = { Weapon.ShootingMode.Semi };

    [Header("Cycle-Based Firing")]
    [SerializeField] private bool requiresCycling = false; // Enable for pump/bolt weapons

    [SerializeField] private float cycleTime = 0.8f;
    [SerializeField] private string cycleAnimation = "CYCLE";

    [Header("Shell Loading")]
    [SerializeField] private bool useShellReload = false; // Enable for shotguns

    [SerializeField] private float shellLoadTime = 0.8f;
    [SerializeField] private int maxShellsToLoad = 8; // Tube capacity
    [SerializeField] private string shellLoadAnimation = "SHELL_LOAD";

    public bool IsCycled { get; private set; } = true;
    private bool isCycling = false;
    private Coroutine cycleCoroutine;

    [Header("Camera Recoil Settings")]
    [SerializeField] private bool haveCameraRecoil = true;

    [SerializeField] private Transform cameraRecoilHolder;
    [SerializeField] private float recoilRotationSpeed = 6f;
    [SerializeField] private float recoilReturnSpeed = 25f;
    [SerializeField] private Vector3 hipFireRecoil = new Vector3(4f, 4f, 4f);
    [SerializeField] private Vector3 adsFireRecoil = new Vector3(2f, 2f, 2f);
    [SerializeField] private float hRecoil = 0.215f;
    [SerializeField] private float vRecoil = 0.221f;

    private Vector3 currentCameraRotation;
    private Vector3 cameraRot;

    [Header("Weapon Recoil Settings")]
    [SerializeField] private bool haveWeaponRecoil = true;

    [SerializeField] private Transform gunPositionHolder;
    [SerializeField] private float gunRecoilPositionSpeed = 8f;
    [SerializeField] private float gunPositionReturnSpeed = 10f;
    [SerializeField] private Vector3 recoilKickBackHip = new Vector3(0.015f, 0f, 0.05f);
    [SerializeField] private Vector3 recoilKickBackAds = new Vector3(-0.08f, 0.01f, 0.009f);
    [SerializeField] private float gunRecoilRotationSpeed = 8f;
    [SerializeField] private float gunRotationReturnSpeed = 38f;
    [SerializeField] private Vector3 recoilRotationHip = new Vector3(10f, 5f, 7f);
    [SerializeField] private Vector3 recoilRotationAds = new Vector3(10f, 4f, 6f);

    private Vector3 rotationRecoil;
    private Vector3 positionRecoil;
    private Vector3 weaponRot;

    [Header("Weapon Rotational Sway")]
    [SerializeField] private bool haveRotationalSway = true;

    [SerializeField] private float rotationSwayIntensity = 10f;
    [SerializeField] private float rotationSwaySmoothness = 2f;

    private Quaternion originRotation;
    private float mouseX;
    private float mouseY;

    [Header("Jump Sway")]
    [SerializeField] private bool haveJumpSway = true;

    [SerializeField] private float jumpIntensity = 5f;
    [SerializeField] private float weaponMaxClamp = 20f;
    [SerializeField] private float weaponMinClamp = 20f;
    [SerializeField] private float jumpSmooth = 15f;
    [SerializeField] private float landingIntensity = 5f;
    [SerializeField] private float landingSmooth = 15f;
    [SerializeField] private float recoverySpeed = 50f;

    private float impactForce = 0;

    [Header("Weapon Move Bobbing")]
    [SerializeField] private bool haveBobbing = true;

    [SerializeField] private float magnitude = 0.009f;
    [SerializeField] private float idleSpeed = 2f;
    [SerializeField] private float walkSpeedMultiplier = 4f;
    [SerializeField] private float walkSpeedMax = 6f;
    [SerializeField] private float aimReduction = 4f;

    private float sinY = 0f;
    private float sinX = 0f;
    private Vector3 lastPosition;

    [Header("Magazine Drop System")]
    [SerializeField] private GameObject magazineDropPrefab; // Separate magazine prefab

    [SerializeField] private Transform magazineDropPoint;   // Empty GameObject positioned where mag should drop
    [SerializeField] private Vector3 magazineDropForce = new Vector3(-2f, -1f, 1f);
    [SerializeField] private float magazineDropTorque = 5f;

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

    private GameObject leftArm;
    private GameObject rightArm;

    // Reference to player controller for movement state
    private PlayerController.PlayerController playerController;

    private void Start()
    {
        cameraRecoilHolder = GameObject.Find("WeaponRenderCamera")?.transform;
        gunPositionHolder = GameObject.Find("WeaponSpawner")?.transform;
        leftArm = transform.Find("LeftArm")?.gameObject;
        rightArm = transform.Find("RightArm")?.gameObject;
    }

    protected virtual void Awake()
    {
        Initialize();

        // Try to find the Player controller
        playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController.PlayerController>();
    }

    protected virtual void Initialize()
    {
        IsReloading = false;
        ReadyToShoot = true;
        isShooting = false;

        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        if (weaponData != null)
        {
            weaponModel = weaponData.weaponModel;
            availableShootingModes = weaponData.availableShootingModes;
            CurrentShootingMode = weaponData.defaultShootingMode;
            BulletsLeft = weaponData.magazineSize;
            burstBulletsLeft = weaponData.bulletsPerBurst;
        }
        else
        {
            Debug.LogError($"WeaponData is null on weapon {gameObject.name}! Please assign a WeaponData ScriptableObject.");
            BulletsLeft = 30;
            CurrentShootingMode = availableShootingModes[0];
            burstBulletsLeft = 3;
        }

        playerCamera = Camera.main;
        ValidateReferences();

        // Initialize weapon effects
        lastPosition = transform.position;
        if (haveRotationalSway) originRotation = transform.localRotation;

        // Set default camera recoil holder if not assigned
        if (cameraRecoilHolder == null && playerCamera != null)
            cameraRecoilHolder = playerCamera.transform;

        // Set default gun position holder if not assigned
        if (gunPositionHolder == null)
            gunPositionHolder = transform;

        // Store the ADJUSTED rotation as origin for sway
        if (haveRotationalSway) originRotation = transform.localRotation;
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

        // Handle weapon effects
        HandleWeaponRecoil();
        HandleCameraRecoil();
        WeaponRotationSway();
        WeaponBobbing();
        JumpSwayEffect();
    }

    protected virtual void HandleInput()
    {
        HandleAiming();
        HandleShooting();
        HandleReloading();
        HandleFireModeSwitch();

        // Update mouse input for sway
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
    }

    #region Weapon Effects System

    private void HandleWeaponRecoil()
    {
        if (!haveWeaponRecoil || gunPositionHolder == null) return;

        rotationRecoil = Vector3.Lerp(rotationRecoil, Vector3.zero, gunRotationReturnSpeed * Time.deltaTime);
        positionRecoil = Vector3.Lerp(positionRecoil, Vector3.zero, gunPositionReturnSpeed * Time.deltaTime);

        gunPositionHolder.localPosition = Vector3.Slerp(gunPositionHolder.localPosition, positionRecoil, gunRecoilPositionSpeed * Time.deltaTime);
        weaponRot = Vector3.Slerp(weaponRot, rotationRecoil, gunRecoilRotationSpeed * Time.deltaTime);
        gunPositionHolder.localRotation = Quaternion.Euler(weaponRot);
    }

    private void HandleCameraRecoil()
    {
        if (!haveCameraRecoil || cameraRecoilHolder == null) return;

        currentCameraRotation = Vector3.Lerp(currentCameraRotation, Vector3.zero, recoilReturnSpeed * Time.deltaTime);
        cameraRot = Vector3.Slerp(cameraRot, currentCameraRotation, recoilRotationSpeed * Time.deltaTime);
        cameraRecoilHolder.localRotation = Quaternion.Euler(cameraRot);
    }

    private void WeaponRotationSway()
    {
        if (!haveRotationalSway) return;

        // Get weapon-specific base rotation for sway calculations
        Quaternion baseRotation = originRotation;
        if (weaponData != null && weaponData.baseViewmodelRotation != Vector3.zero)
        {
            baseRotation = Quaternion.Euler(weaponData.baseViewmodelRotation);
        }

        Quaternion newAdjustedRotationX = Quaternion.AngleAxis(rotationSwayIntensity * mouseX * -1f, Vector3.up);
        Quaternion targetRotation = baseRotation * newAdjustedRotationX;
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, rotationSwaySmoothness * Time.deltaTime);
    }

    private void WeaponBobbing()
    {
        if (!haveBobbing) return;

        // Get weapon-specific base position
        Vector3 basePosition = weaponData ? weaponData.baseViewmodelPosition : Vector3.zero;

        // Skip bobbing if not grounded
        if (playerController != null)
        {
            if (!playerController.IsGrounded())
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, basePosition, Time.deltaTime);
                return;
            }
        }

        // Calculate delta time based on the player's movement speed
        float delta = Time.deltaTime * idleSpeed;
        float velocity = (lastPosition - transform.position).magnitude * walkSpeedMultiplier;
        delta += Mathf.Clamp(velocity, 0, walkSpeedMax);

        // Update the sinX and sinY values to create a bobbing effect
        sinX += delta / 2;
        sinY += delta;
        sinX %= Mathf.PI * 2;
        sinY %= Mathf.PI * 2;

        // Adjust the weapon's local position to create the bobbing effect
        float currentMagnitude = IsADS ? magnitude / aimReduction : magnitude;

        // Apply bobbing relative to base position instead of Vector3.zero
        transform.localPosition = basePosition + currentMagnitude * Mathf.Sin(sinY) * Vector3.up;
        transform.localPosition += currentMagnitude * Mathf.Sin(sinX) * Vector3.right;

        lastPosition = transform.position;
    }

    private void JumpSwayEffect()
    {
        if (!haveJumpSway || IsADS || playerController == null) return;

        switch (playerController.IsGrounded())
        {
            case false:
                // Adjust the weapon's rotation based on the player's jump velocity
                float yVelocity = playerController.GetVelocity().y;
                yVelocity = Mathf.Clamp(yVelocity, -weaponMinClamp, weaponMaxClamp);
                impactForce = -yVelocity * landingIntensity;

                if (IsADS)
                {
                    yVelocity = Mathf.Max(yVelocity, 0);
                }

                // Update the weapon's local rotation to simulate the jump sway effect
                transform.localRotation = Quaternion.Lerp(transform.localRotation,
                    Quaternion.Euler(0f, 0f, yVelocity * jumpIntensity),
                    Time.deltaTime * jumpSmooth);
                break;

            case true when impactForce >= 0:
                // If the player is grounded and has impact force, adjust the weapon's rotation accordingly
                transform.localRotation = Quaternion.Lerp(transform.localRotation,
                    Quaternion.Euler(0, 0, impactForce),
                    Time.deltaTime * landingSmooth);
                impactForce -= recoverySpeed * Time.deltaTime;
                break;

            case true:
                // If the player is grounded and there's no impact force, reset the weapon's rotation
                transform.localRotation = Quaternion.Lerp(transform.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * landingSmooth);
                break;
        }
    }

    #endregion Weapon Effects System

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

        // Check if weapon needs cycling
        if (inputPressed && requiresCycling && !IsCycled && !isCycling)
        {
            StartCycle();
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
        else playerController.AddRecoil(0f, 0f);
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
        HUDManager.Instance?.Crosshair?.SetActive(false);
        OnADSChanged?.Invoke(this, true);
    }

    protected virtual void ExitADS()
    {
        IsADS = false;
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

        // Apply recoil effects
        ApplyRecoilEffects();

        // Mark as needing cycle for pump/bolt weapons
        if (requiresCycling)
        {
            IsCycled = false;
        }

        // Handle shooting mode specific logic
        HandleShootingMode();

        OnWeaponFired?.Invoke(this);

        // Reset shot timing using weaponData fire rate
        if (allowReset)
        {
            float fireRate = weaponData ? weaponData.fireRate : 600f;
            float fireDelay = 60f / fireRate;
            Invoke(nameof(ResetShot), fireDelay);
            allowReset = false;
        }
    }

    protected virtual void StartCycle()
    {
        if (isCycling || IsCycled) return;

        if (cycleCoroutine != null)
            StopCoroutine(cycleCoroutine);

        cycleCoroutine = StartCoroutine(CycleCoroutine());
    }

    protected virtual IEnumerator CycleCoroutine()
    {
        isCycling = true;
        ReadyToShoot = false;

        // Enable animator and play cycle animation
        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = true;
            references.weaponAnimator.SetTrigger(cycleAnimation);
        }

        // Play cycling sound (pump/bolt sound)
        PlayCycleSound();

        yield return new WaitForSeconds(cycleTime);

        // Complete cycle
        IsCycled = true;
        isCycling = false;
        ReadyToShoot = true;

        // Disable animator
        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = false;
        }
    }

    protected virtual void ApplyRecoilEffects()
    {
        float hRecoilValue = UnityEngine.Random.Range(-hRecoil, hRecoil);

        if (IsADS)
        {
            // ADS recoil
            if (haveCameraRecoil)
            {
                currentCameraRotation += new Vector3(-adsFireRecoil.x,
                    UnityEngine.Random.Range(-adsFireRecoil.y, adsFireRecoil.y),
                    UnityEngine.Random.Range(-adsFireRecoil.z, adsFireRecoil.z));
            }

            if (haveWeaponRecoil)
            {
                rotationRecoil += new Vector3(-recoilRotationAds.x,
                    UnityEngine.Random.Range(-recoilRotationAds.y, recoilRotationAds.y),
                    UnityEngine.Random.Range(-recoilRotationAds.z, recoilRotationAds.z));
                positionRecoil += new Vector3(UnityEngine.Random.Range(-recoilKickBackAds.x, recoilKickBackAds.x),
                    UnityEngine.Random.Range(-recoilKickBackAds.y, recoilKickBackAds.y),
                    recoilKickBackAds.z);
            }

            // reduced recoil
            playerController.AddRecoil(hRecoil * 0.5f, vRecoil * 0.5f);
        }
        else
        {
            // Hip fire recoil
            if (haveCameraRecoil)
            {
                currentCameraRotation += new Vector3(-hipFireRecoil.x,
                    UnityEngine.Random.Range(-hipFireRecoil.y, hipFireRecoil.y),
                    UnityEngine.Random.Range(-hipFireRecoil.z, hipFireRecoil.z));
            }

            if (haveWeaponRecoil)
            {
                rotationRecoil += new Vector3(-recoilRotationHip.x,
                    UnityEngine.Random.Range(-recoilRotationHip.y, recoilRotationHip.y),
                    UnityEngine.Random.Range(-recoilRotationHip.z, recoilRotationHip.z));
                positionRecoil += new Vector3(UnityEngine.Random.Range(-recoilKickBackHip.x, recoilKickBackHip.x),
                    UnityEngine.Random.Range(-recoilKickBackHip.y, recoilKickBackHip.y),
                    recoilKickBackHip.z);
            }

            playerController.AddRecoil(hRecoil, vRecoil);
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

        ProjectileType projectileType = weaponData ? weaponData.projectileType : references.projectileType;
        int pelletsToFire = weaponData ? weaponData.pelletsPerShot : 1;

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
        if (references.muzzleFlash != null)
        {
            references.muzzleFlash.Play();
        }

        if (references.muzzleLight != null)
        {
            StartCoroutine(FlashMuzzleLight());
        }

        EjectShell();
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
        bool canReload = !IsReloading &&
                         BulletsLeft < magSize &&
                         HasAmmoAvailable();

        return canReload;
    }

    [SerializeField] private Transform magInGun; // assign in prefab inspector

    public void DropMagazine()
    {
        GameObject magPrefab = weaponData ? weaponData.magazineDropPrefab : magazineDropPrefab;
        if (magPrefab == null || magazineDropPoint == null) return;

        GameObject droppedMag = Instantiate(magPrefab, magazineDropPoint.position, magazineDropPoint.rotation);

        // Add physics
        var rb = droppedMag.GetComponent<Rigidbody>();
        if (rb == null) rb = droppedMag.AddComponent<Rigidbody>();

        // Apply realistic drop forces
        Vector3 worldDropForce = magazineDropPoint.TransformDirection(magazineDropForce);
        rb.AddForce(worldDropForce, ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * magazineDropTorque, ForceMode.Impulse);

        // Cleanup
        Destroy(droppedMag, 10f);
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

        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstBulletsLeft = weaponData ? weaponData.bulletsPerBurst : 3;
        }

        if (useShellReload)
        {
            // Shell-by-shell loading for shotguns
            yield return StartCoroutine(ShellReloadCoroutine());
        }
        else
        {
            // Magazine reload system
            yield return StartCoroutine(MagazineReloadCoroutine());
        }
    }

    protected virtual IEnumerator MagazineReloadCoroutine()
    {
        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = true;
        }

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

        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = false;
        }
    }

    protected virtual IEnumerator ShellReloadCoroutine()
    {
        // Shell loading logic I showed earlier
        OnReloadStarted?.Invoke(this);

        int maxCapacity = weaponData ? weaponData.magazineSize : maxShellsToLoad;

        while (BulletsLeft < maxCapacity)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CancelReload();
                yield break;
            }

            int availableAmmo = WeaponManager.Instance?.CheckAmmoLeftFor(weaponModel) ?? 0;
            if (availableAmmo <= 0) break;

            if (references.weaponAnimator != null)
            {
                references.weaponAnimator.enabled = true;
                references.weaponAnimator.SetTrigger(shellLoadAnimation);
            }

            SoundManager.Instance?.PlayShellLoadSound();

            yield return new WaitForSeconds(shellLoadTime);

            BulletsLeft++;
            WeaponManager.Instance?.DecreaseTotalAmmo(1, weaponModel);

            if (references.weaponAnimator != null)
            {
                references.weaponAnimator.enabled = false;
            }
        }

        ReadyToShoot = true;
        IsReloading = false;
        OnReloadCompleted?.Invoke(this);
    }

    protected virtual void CancelReload()
    {
        IsReloading = false;
        ReadyToShoot = true;

        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = true;

            // Force idle state
            references.weaponAnimator.SetTrigger("RELOAD"); // Stop current
            references.weaponAnimator.Play("Idle", -1, 0f); // Jump to idle at 0% progress
            references.weaponAnimator.Update(0f);

            references.weaponAnimator.enabled = false;
        }

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
    }

    private void ForceStopReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        IsReloading = false;
        ReadyToShoot = true;

        // Reset animator to default pose
        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = true;
            references.weaponAnimator.Rebind(); // This resets to bind pose
            references.weaponAnimator.Update(0f);
            references.weaponAnimator.enabled = false;
        }

        SoundManager.Instance?.StopReloadSound(weaponModel);
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

    #region Sounds

    protected virtual void PlayCycleSound()
    {
        // Play randomized shell loading/pumping sounds
        SoundManager.Instance?.PlayCycleSound(weaponModel);
    }

    protected virtual void PlayReloadSound()
    {
        SoundManager.Instance?.PlayReloadSound(weaponModel);
    }

    protected virtual void PlayShellLoadSound()
    {
        SoundManager.Instance?.PlayShellLoadSound();
    }

    protected virtual void PlayEmptySound()
    {
        SoundManager.Instance?.EmptyShooting?.Play();
    }

    #endregion Sounds

    #region Public API

    public virtual void SetActiveWeapon(bool active)
    {
        IsActiveWeapon = active;
        Animator animator = references.weaponAnimator;

        if (animator != null)
        {
            animator.enabled = true;
            Invoke(nameof(DisableAnimator), 0.1f);
        }

        if (leftArm != null) leftArm.SetActive(active);
        if (rightArm != null) rightArm.SetActive(active);

        if (!active)
        {
            // Stop cycling when unequipping
            if (cycleCoroutine != null)
            {
                StopCoroutine(cycleCoroutine);
                cycleCoroutine = null;
                isCycling = false;
            }

            // Force cleanup when weapon is deactivated
            if (IsReloading)
            {
                ForceStopReload();
            }

            // DEACTIVATION CODE
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
        else
        {
            // If weapon was unequipped mid-cycle, resume cycling
            if (requiresCycling && !IsCycled && !isCycling)
            {
                StartCycle();
            }
        }
    }

    private void DisableAnimator()
    {
        if (references.weaponAnimator != null)
        {
            references.weaponAnimator.enabled = false;
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

    public virtual float GetDamageAtDistance(float distance)
    {
        if (weaponData != null)
        {
            return weaponData.GetDamageAtDistance(distance);
        }
        return weaponData ? weaponData.damage : 25;
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

#region Weapon Configuration

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

    [Header("Recoil References")]
    public Transform viewmodelTransform;

    public Transform cameraTransform;
}

#endregion Weapon Configuration

#endregion