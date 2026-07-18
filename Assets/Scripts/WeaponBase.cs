#region Weapon Base

using System;
using System.Collections;
using UnityEngine;
using Weapon;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] protected WeaponData weaponData;

    [Header("Scene References")]
    [SerializeField] protected Transform bulletSpawn;

    [SerializeField] protected Animator weaponAnimator;
    [SerializeField] protected ParticleSystem muzzleFlash;
    [SerializeField] protected GameObject muzzleLight;
    [SerializeField] protected Transform ejectionPort;
    [SerializeField] protected GameObject shellCasing;

    [Header("Gun Position")]
    [SerializeField] private Transform gunPositionHolder;

    [Header("Magazine Drop")]
    public Transform magazineDropPoint;

    public Vector3 magazineDropForce = new Vector3(-2f, -1f, 1f);
    public float magazineDropTorque = 5f;

    [Header("Weapon Info")]
    public Weapon.WeaponModel weaponModel;

    public Weapon.ShootingMode[] availableShootingModes = { Weapon.ShootingMode.Semi };

    // Cycling
    public bool IsCycled { get; private set; } = true;

    private bool isCycling = false;
    private Coroutine cycleCoroutine;

    // Inspection
    private bool isInspecting = false;

    private Coroutine inspectCoroutine;

    // Camera
    private FollowCamera.CameraFollow cameraFollow;

    // Weapon recoil
    private Vector3 rotationRecoil;

    private Vector3 positionRecoil;
    private Vector3 weaponRot;
    private float lastShotTime = -999f;

    // Sway
    private Quaternion originRotation;

    private float mouseX;
    private float mouseY;

    // Jump sway
    private float impactForce = 0;

    // Bobbing
    private float sinY = 0f;

    private float sinX = 0f;
    private Vector3 lastPosition;

    // ── State ────────────────────────────────────────────────────
    public bool IsActiveWeapon { get; set; }

    public bool IsADS { get; protected set; }
    public bool IsReloading { get; protected set; }
    public bool ReadyToShoot { get; protected set; } = true;
    public int BulletsLeft { get; protected set; }
    public Weapon.ShootingMode CurrentShootingMode { get; protected set; }

    // ── Spread ───────────────────────────────────────────────────
    private float currentSpread;

    private float accumulatedSpread;
    public float CurrentSpread => currentSpread;

    // ── Internal ─────────────────────────────────────────────────
    protected bool isShooting;

    protected bool allowReset = true;
    protected int burstBulletsLeft;
    protected Coroutine reloadCoroutine;
    protected Coroutine burstCoroutine;
    protected Camera playerCamera;

    // ── Events ───────────────────────────────────────────────────
    public event Action<WeaponBase> OnWeaponFired;

    public event Action<WeaponBase> OnReloadStarted;

    public event Action<WeaponBase> OnReloadCompleted;

    public event Action<WeaponBase, bool> OnADSChanged;

    private bool reloadEventSignaled = false;

    // ── Properties ───────────────────────────────────────────────
    public WeaponData Data => weaponData;

    [SerializeField] private GameObject leftArm;
    [SerializeField] private GameObject rightArm;
    private PlayerController.PlayerController playerController;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        gunPositionHolder = GameObject.Find("WeaponSpawner")?.transform;
        cameraFollow = UnityEngine.Object.FindAnyObjectByType<FollowCamera.CameraFollow>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "LeftArm") leftArm = t.gameObject;
            if (t.name == "RightArm") rightArm = t.gameObject;
        }
    }

    protected virtual void Awake()
    {
        Initialize();
        playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController.PlayerController>();
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

        if (weaponData == null)
        {
            Debug.LogError($"[WeaponBase] WeaponData is null on {gameObject.name}! Weapon disabled.");
            enabled = false;
            return;
        }

        weaponModel = weaponData.weaponModel;
        availableShootingModes = weaponData.availableShootingModes;
        CurrentShootingMode = weaponData.defaultShootingMode;
        BulletsLeft = weaponData.magazineSize;
        burstBulletsLeft = weaponData.bulletsPerBurst;
        currentSpread = weaponData.hipSpread;
        accumulatedSpread = weaponData.hipSpread;

        playerCamera = Camera.main;
        ValidateReferences();

        if (weaponAnimator != null)
            weaponAnimator.enabled = false;

        lastPosition = transform.position;
        if (weaponData.haveRotationalSway) originRotation = transform.localRotation;
        if (gunPositionHolder == null) gunPositionHolder = transform;
        if (weaponData.haveRotationalSway) originRotation = transform.localRotation;
    }

    protected virtual void ValidateReferences()
    {
        if (bulletSpawn == null)
            Debug.LogError($"[WeaponBase] bulletSpawn not assigned on {gameObject.name}!");

        if (weaponAnimator == null)
            weaponAnimator = GetComponent<Animator>();
    }

    protected virtual void Update()
    {
        if (!IsActiveWeapon) return;

        HandleInput();
        UpdateLayerMask();
        UpdateSpread();
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
        HandleInspection();
        HandleFireModeSwitch();

        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
    }

    #region Weapon Effects

    private void HandleWeaponRecoil()
    {
        if (!weaponData.haveWeaponRecoil || gunPositionHolder == null) return;

        rotationRecoil = Vector3.Lerp(rotationRecoil, Vector3.zero,
            weaponData.gunRotationReturnSpeed * Time.deltaTime);
        positionRecoil = Vector3.Lerp(positionRecoil, Vector3.zero,
            weaponData.gunPositionReturnSpeed * Time.deltaTime);

        gunPositionHolder.localPosition = Vector3.Slerp(gunPositionHolder.localPosition,
            positionRecoil, weaponData.gunRecoilPositionSpeed * Time.deltaTime);
        weaponRot = Vector3.Slerp(weaponRot, rotationRecoil,
            weaponData.gunRecoilRotationSpeed * Time.deltaTime);
        gunPositionHolder.localRotation = Quaternion.Euler(weaponRot);
    }

    private void HandleCameraRecoil()
    {
        if (!weaponData.haveCameraRecoil || cameraFollow == null) return;

        float fireInterval = 60f / weaponData.fireRate;
        bool currentlyFiring = (Time.time - lastShotTime) < fireInterval + 0.05f;
        cameraFollow.SetFiringState(currentlyFiring);
    }

    private void WeaponRotationSway()
    {
        if (!weaponData.haveRotationalSway) return;

        Quaternion baseRotation = weaponData.baseViewmodelRotation != Vector3.zero
            ? Quaternion.Euler(weaponData.baseViewmodelRotation)
            : originRotation;

        Quaternion swayRot = Quaternion.AngleAxis(weaponData.rotationSwayIntensity * mouseX * -1f, Vector3.up);
        transform.localRotation = Quaternion.Lerp(transform.localRotation,
            baseRotation * swayRot, weaponData.rotationSwaySmoothness * Time.deltaTime);
    }

    private void WeaponBobbing()
    {
        if (!weaponData.haveBobbing) return;

        Vector3 basePosition = weaponData.baseViewmodelPosition;

        if (playerController != null && !playerController.IsGrounded())
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, basePosition, Time.deltaTime);
            return;
        }

        float delta = Time.deltaTime * weaponData.idleSpeed;
        float velocity = (lastPosition - transform.position).magnitude * weaponData.walkSpeedMultiplier;
        delta += Mathf.Clamp(velocity, 0, weaponData.walkSpeedMax);

        sinX += delta / 2;
        sinY += delta;
        sinX %= Mathf.PI * 2;
        sinY %= Mathf.PI * 2;

        float mag = IsADS
            ? weaponData.bobbingMagnitude / weaponData.aimReduction
            : weaponData.bobbingMagnitude;

        transform.localPosition = basePosition + mag * Mathf.Sin(sinY) * Vector3.up;
        transform.localPosition += mag * Mathf.Sin(sinX) * Vector3.right;
        lastPosition = transform.position;
    }

    private void JumpSwayEffect()
    {
        if (!weaponData.haveJumpSway || IsADS || playerController == null) return;

        switch (playerController.IsGrounded())
        {
            case false:
                float yVelocity = playerController.GetVelocity().y;
                yVelocity = Mathf.Clamp(yVelocity, -weaponData.weaponMinClamp, weaponData.weaponMaxClamp);
                impactForce = -yVelocity * weaponData.landingIntensity;
                if (IsADS) yVelocity = Mathf.Max(yVelocity, 0);
                transform.localRotation = Quaternion.Lerp(transform.localRotation,
                    Quaternion.Euler(0f, 0f, yVelocity * weaponData.jumpIntensity),
                    Time.deltaTime * weaponData.jumpSmooth);
                break;

            case true when impactForce >= 0:
                transform.localRotation = Quaternion.Lerp(transform.localRotation,
                    Quaternion.Euler(0, 0, impactForce),
                    Time.deltaTime * weaponData.landingSmooth);
                impactForce -= weaponData.recoverySpeed * Time.deltaTime;
                break;

            case true:
                transform.localRotation = Quaternion.Lerp(transform.localRotation,
                    Quaternion.identity, Time.deltaTime * weaponData.landingSmooth);
                break;
        }
    }

    #endregion Weapon Effects

    protected virtual void HandleAiming()
    {
        if (Input.GetMouseButtonDown(1)) EnterADS();
        else if (Input.GetMouseButtonUp(1)) ExitADS();
    }

    protected virtual void HandleShooting()
    {
        bool inputPressed = GetShootingInput();

        if (inputPressed && BulletsLeft <= 0)
        {
            PlayEmptySound();
            return;
        }

        if (inputPressed && weaponData.requiresCycling && !IsCycled && !isCycling)
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
        else
        {
            playerController?.AddRecoil(0f, 0f);
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
            StartReload();
    }

    protected virtual void HandleFireModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.V) && availableShootingModes.Length > 1)
            CycleFireMode();
    }

    protected virtual void HandleInspection()
    {
        if (Input.GetKeyDown(KeyCode.I) && !isInspecting && weaponAnimator != null)
        {
            if (inspectCoroutine != null) StopCoroutine(inspectCoroutine);
            inspectCoroutine = StartCoroutine(InspectCoroutine());
        }
    }

    protected virtual IEnumerator InspectCoroutine()
    {
        isInspecting = true;
        weaponAnimator?.ResetTrigger("RELOAD");
        weaponAnimator?.ResetTrigger("INSPECT");
        weaponAnimator?.Play("Inspect", 0, 0f);
        yield return new WaitForSeconds(weaponData.inspectDuration);
        isInspecting = false;
    }

    protected virtual void UpdateLayerMask()
    {
        int targetLayer = IsActiveWeapon
            ? LayerMask.NameToLayer("WeaponRender")
            : LayerMask.NameToLayer("Default");

        foreach (Transform child in transform)
            child.gameObject.layer = targetLayer;

        var outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = !IsActiveWeapon;
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

        ApplyRecoilEffects();
        BuildSpread();

        CreateProjectile(CalculateShootDirection());
        PlayShootingEffects();

        if (weaponData.requiresCycling) IsCycled = false;

        HandleShootingMode();
        OnWeaponFired?.Invoke(this);

        if (allowReset)
        {
            Invoke(nameof(ResetShot), 60f / weaponData.fireRate);
            allowReset = false;
        }
    }

    protected virtual void StartCycle()
    {
        if (isCycling || IsCycled) return;
        if (cycleCoroutine != null) StopCoroutine(cycleCoroutine);
        cycleCoroutine = StartCoroutine(CycleCoroutine());
    }

    protected virtual IEnumerator CycleCoroutine()
    {
        isCycling = true;
        ReadyToShoot = false;

        weaponAnimator?.SetTrigger(weaponData.cycleAnimation);
        PlayCycleSound();

        yield return new WaitForSeconds(weaponData.cycleTime);

        IsCycled = true;
        isCycling = false;
        ReadyToShoot = true;
    }

    protected virtual void ApplyRecoilEffects()
    {
        if (weaponData.haveCameraRecoil && cameraFollow != null)
        {
            Vector3 kick = IsADS ? weaponData.adsFireRecoil : weaponData.hipFireRecoil;
            float yawKick = UnityEngine.Random.Range(-kick.y, kick.y) * weaponData.hRecoil;
            float pitchKick = kick.x * weaponData.vRecoil;
            float rollKick = UnityEngine.Random.Range(weaponData.recoilRollIntensity * 0.5f,
                              weaponData.recoilRollIntensity) * Mathf.Sign(yawKick);
            cameraFollow.ApplyRecoilKick(pitchKick, yawKick, rollKick,
                weaponData.recoilRotationSpeed, weaponData.recoilReturnSpeed);
        }

        if (weaponData.haveWeaponRecoil)
        {
            Vector3 rotRecoil = IsADS ? weaponData.recoilRotationAds : weaponData.recoilRotationHip;
            Vector3 posRecoil = IsADS ? weaponData.recoilKickBackAds : weaponData.recoilKickBackHip;

            rotationRecoil += new Vector3(
                -rotRecoil.x,
                UnityEngine.Random.Range(-rotRecoil.y, rotRecoil.y),
                UnityEngine.Random.Range(-rotRecoil.z, rotRecoil.z));
            positionRecoil += new Vector3(
                UnityEngine.Random.Range(-posRecoil.x, posRecoil.x),
                UnityEngine.Random.Range(-posRecoil.y, posRecoil.y),
                posRecoil.z);
        }

        lastShotTime = Time.time;
    }

    protected virtual bool CanShoot() =>
        ReadyToShoot && BulletsLeft > 0 && !IsReloading;

    protected virtual void CreateProjectile(Vector3 direction)
    {
        if (ProjectileFactory.Instance == null)
        {
            Debug.LogError("[WeaponBase] ProjectileFactory not found!");
            return;
        }

        for (int i = 0; i < weaponData.pelletsPerShot; i++)
        {
            Vector3 pelletDir = weaponData.pelletsPerShot > 1
                ? ApplySpread(direction, weaponData.pelletSpread)
                : direction;

            var projectile = ProjectileFactory.Instance.CreateProjectile(
                weaponData.projectileType,
                bulletSpawn.position,
                Quaternion.LookRotation(pelletDir));

            if (projectile != null)
                ConfigureProjectile(projectile, pelletDir);
        }
    }

    protected virtual void ConfigureProjectile(IProjectile projectile, Vector3 direction)
    {
        if (projectile is ProjectileBase pb)
        {
            pb.SetDamage(weaponData.damage);
            pb.Launch(direction, weaponData.muzzleVelocity);
        }
    }

    protected virtual Vector3 CalculateShootDirection()
    {
        Ray cameraRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Vector3 targetPoint = Physics.Raycast(cameraRay, out RaycastHit hit, weaponData.range)
            ? hit.point
            : cameraRay.origin + cameraRay.direction * weaponData.range;

        return ApplySpread((targetPoint - bulletSpawn.position).normalized, currentSpread);
    }

    protected virtual Vector3 ApplySpread(Vector3 direction, float spreadAmount)
    {
        float dev = spreadAmount * weaponData.bulletSpreadScale;
        return (direction + new Vector3(
            UnityEngine.Random.Range(-dev, dev),
            UnityEngine.Random.Range(-dev, dev),
            0f)).normalized;
    }

    protected virtual void HandleShootingMode()
    {
        if (CurrentShootingMode == Weapon.ShootingMode.Burst)
            HandleBurstMode();
    }

    protected virtual void HandleBurstMode()
    {
        burstBulletsLeft--;

        if (burstBulletsLeft > 0 && BulletsLeft > 0)
        {
            if (burstCoroutine != null) StopCoroutine(burstCoroutine);
            burstCoroutine = StartCoroutine(BurstFireCoroutine());
        }
        else
        {
            burstBulletsLeft = weaponData.bulletsPerBurst;
        }
    }

    protected virtual IEnumerator BurstFireCoroutine()
    {
        yield return new WaitForSeconds(weaponData.burstDelay);
        if (BulletsLeft > 0) FireWeapon();
    }

    protected virtual void PlayShootingEffects()
    {
        muzzleFlash?.Play();
        if (muzzleLight != null) StartCoroutine(FlashMuzzleLight());
        EjectShell();
        SoundManager.Instance?.PlayShootingSound(weaponData.shootSound);
    }

    protected virtual IEnumerator FlashMuzzleLight()
    {
        muzzleLight.SetActive(true);
        yield return new WaitForSeconds(weaponData.muzzleLightDuration);
        muzzleLight.SetActive(false);
    }

    protected virtual void EjectShell()
    {
        if (shellCasing == null || ejectionPort == null) return;

        GameObject shell = Instantiate(shellCasing, ejectionPort.position, ejectionPort.rotation);
        Rigidbody shellRb = shell.GetComponent<Rigidbody>();

        if (shellRb != null)
        {
            shellRb.AddForce(
                ejectionPort.right * UnityEngine.Random.Range(2f, 4f) +
                ejectionPort.up * UnityEngine.Random.Range(1f, 2f),
                ForceMode.Impulse);
            shellRb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }

        Destroy(shell, 5f);
    }

    protected virtual void ResetShot()
    {
        ReadyToShoot = true;
        allowReset = true;
    }

    #endregion Shooting

    #region Spread

    private void UpdateSpread()
    {
        float baseSpread = IsADS ? weaponData.adsSpread : weaponData.hipSpread;
        bool recoveryAllowed = (Time.time - lastShotTime) > weaponData.spreadRecoveryDelay;

        if (recoveryAllowed)
            accumulatedSpread = Mathf.MoveTowards(accumulatedSpread, baseSpread,
                weaponData.spreadRecoveryRate * Time.deltaTime);

        currentSpread = IsADS ? weaponData.adsSpread : accumulatedSpread;
    }

    private void BuildSpread()
    {
        accumulatedSpread = Mathf.Min(
            accumulatedSpread + weaponData.spreadPerShot,
            weaponData.hipSpread + weaponData.spreadMax);
    }

    #endregion Spread

    #region Reloading

    protected virtual bool CanReload() =>
        !IsReloading && BulletsLeft < weaponData.magazineSize && HasAmmoAvailable();

    [SerializeField] private Transform magInGun;

    public void DropMagazine()
    {
        if (weaponData.magazineDropPrefab == null || magazineDropPoint == null) return;

        GameObject mag = Instantiate(weaponData.magazineDropPrefab,
            magazineDropPoint.position, magazineDropPoint.rotation);

        var rb = mag.GetComponent<Rigidbody>() ?? mag.AddComponent<Rigidbody>();
        rb.AddForce(magazineDropPoint.TransformDirection(magazineDropForce), ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * magazineDropTorque, ForceMode.Impulse);
        Destroy(mag, 10f);
    }

    protected virtual bool HasAmmoAvailable() =>
        WeaponManager.Instance?.CheckAmmoLeftFor(weaponData.ammoType) > 0;

    protected virtual void StartReload()
    {
        if (reloadCoroutine != null) StopCoroutine(reloadCoroutine);
        reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    protected virtual IEnumerator ReloadCoroutine()
    {
        ReadyToShoot = false;
        IsReloading = true;

        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstBulletsLeft = weaponData.bulletsPerBurst;
        }

        if (weaponData.useShellReload)
            yield return StartCoroutine(ShellReloadCoroutine());
        else
            yield return StartCoroutine(MagazineReloadCoroutine());
    }

    protected virtual IEnumerator MagazineReloadCoroutine()
    {
        // Trigger animation — WeaponAnimationEvents.MagIn() / BoltPull() signals completion
        weaponAnimator?.SetTrigger(GetReloadAnimationName());
        OnReloadStarted?.Invoke(this);

        reloadEventSignaled = false;

        // Wait purely for animation event — no timer
        while (!reloadEventSignaled)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CancelReload();
                yield break;
            }
            yield return null;
        }

        CompleteReload();
    }

    protected virtual IEnumerator ShellReloadCoroutine()
    {
        OnReloadStarted?.Invoke(this);

        while (BulletsLeft < weaponData.magazineSize)
        {
            if (Input.GetMouseButtonDown(0)) { CancelReload(); yield break; }

            int available = WeaponManager.Instance?.CheckAmmoLeftFor(weaponData.ammoType) ?? 0;
            if (available <= 0) break;

            weaponAnimator?.SetTrigger(weaponData.shellLoadAnimation);
            SoundManager.Instance?.PlayShellLoadSound();

            yield return new WaitForSeconds(weaponData.shellLoadTime);

            BulletsLeft++;
            WeaponManager.Instance?.DecreaseTotalAmmo(1, weaponData.ammoType);
        }

        ReadyToShoot = true;
        IsReloading = false;
        OnReloadCompleted?.Invoke(this);
    }

    protected virtual void CancelReload()
    {
        IsReloading = false;
        ReadyToShoot = true;

        weaponAnimator?.ResetTrigger("RELOAD");
        weaponAnimator?.Play("Idle", -1, 0f);
        weaponAnimator?.Update(0f);

        SoundManager.Instance?.StopReloadSound();
    }

    protected virtual void CompleteReload()
    {
        ReadyToShoot = true;
        IsReloading = false;

        int needed = weaponData.magazineSize - BulletsLeft;
        int available = WeaponManager.Instance?.CheckAmmoLeftFor(weaponData.ammoType) ?? 0;
        int toReload = Mathf.Min(needed, available);

        BulletsLeft += toReload;
        WeaponManager.Instance?.DecreaseTotalAmmo(toReload, weaponData.ammoType);
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

        weaponAnimator?.Rebind();
        weaponAnimator?.Update(0f);

        SoundManager.Instance?.StopReloadSound();
    }

    #endregion Reloading

    #region Fire Mode

    protected virtual void CycleFireMode()
    {
        int currentIndex = Array.IndexOf(availableShootingModes, CurrentShootingMode);
        CurrentShootingMode = availableShootingModes[(currentIndex + 1) % availableShootingModes.Length];
        Debug.Log($"[WeaponBase] Fire mode: {CurrentShootingMode}");
    }

    #endregion Fire Mode

    #region Sounds

    protected virtual void PlayCycleSound() => SoundManager.Instance?.PlayCycleSound(weaponData.cycleSounds);

    protected virtual void PlayReloadSound() => SoundManager.Instance?.PlayReloadSound(weaponData.reloadSound);

    protected virtual void PlayShellLoadSound() => SoundManager.Instance?.PlayShellLoadSound();

    private float lastEmptySoundTime = -999f;
    private float emptySoundCooldown = 0.1f;

    protected virtual void PlayEmptySound()
    {
        if (Time.time - lastEmptySoundTime < emptySoundCooldown) return;
        lastEmptySoundTime = Time.time;
        SoundManager.Instance?.PlayShootingSound(weaponData.emptySound);
    }

    #endregion Sounds

    #region Public API

    public virtual void SetActiveWeapon(bool active, bool isFirstPickup = false)
    {
        IsActiveWeapon = active;

        if (leftArm != null) leftArm.SetActive(active);
        if (rightArm != null) rightArm.SetActive(active);

        if (!active)
        {
            if (weaponAnimator != null)
                weaponAnimator.enabled = false;

            rotationRecoil = Vector3.zero;
            positionRecoil = Vector3.zero;
            cameraFollow?.ResetRecoil();

            if (cycleCoroutine != null)
            {
                StopCoroutine(cycleCoroutine);
                cycleCoroutine = null;
                isCycling = false;
            }

            if (IsReloading) ForceStopReload();

            isShooting = false;
            if (IsADS) ExitADS();

            GetComponent<Outline>().enabled = false;
        }
        else
        {
            EnableAnimator();

            if (weaponAnimator != null)
            {
                // Reset common state triggers to avoid overlapping animations
                weaponAnimator.ResetTrigger("RELOAD");
                weaponAnimator.ResetTrigger("INSPECT");

                if (isFirstPickup)
                {
                    // Trigger your "First Equip" animation (e.g., pulling back charging handle on pickup)
                    weaponAnimator.SetTrigger("FIRSTEQUIP");
                }
                else
                {
                    // Trigger your standard "Switch / Draw" animation
                    weaponAnimator.SetTrigger("SWITCH");
                }
            }

            if (weaponData.requiresCycling && !IsCycled && !isCycling)
                StartCycle();
        }
    }

    public virtual WeaponInfo GetWeaponInfo()
    {
        return new WeaponInfo
        {
            Model = weaponModel,
            Damage = weaponData.damage,
            FireRate = weaponData.fireRate,
            Range = weaponData.range,
            BulletsLeft = BulletsLeft,
            MagSize = weaponData.magazineSize,
            CurrentFireMode = CurrentShootingMode,
            AvailableFireModes = availableShootingModes,
            IsReloading = IsReloading,
            IsADS = IsADS,
            WeaponName = weaponData.weaponName,
            AmmoType = weaponData.ammoType,
            Rarity = weaponData.rarity
        };
    }

    public virtual void RefillAmmo() => BulletsLeft = weaponData.magazineSize;

    public virtual float GetDamageAtDistance(float distance) => weaponData.GetDamageAtDistance(distance);

    public void SignalReloadComplete() => reloadEventSignaled = true;

    public void EnableAnimator()
    {
        if (weaponAnimator != null)
            weaponAnimator.enabled = true;
    }

    #endregion Public API

    #region Helpers

    private string GetReloadAnimationName()
    {
        if (BulletsLeft == 1 && !weaponData.isOpenBolt) return weaponData.lastBulletReloadAnimation;
        if (BulletsLeft > 1) return weaponData.tacticalReloadAnimation;
        return weaponData.reloadAnimation;
    }

    #endregion Helpers
}

#endregion