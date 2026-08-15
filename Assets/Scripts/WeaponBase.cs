#region Weapon Base

using System;
using System.Collections;
using UnityEngine;
using Weapon;

public abstract class WeaponBase : MonoBehaviour
{
    #region Fields

    // ── Configuration & Scene References ────────────────────────
    [Header("Configuration")]
    [SerializeField] protected WeaponData weaponData;

    [Header("Scene References")]
    [SerializeField] protected Transform bulletSpawn;

    [SerializeField] protected Animator weaponAnimator;
    [SerializeField] protected ParticleSystem muzzleFlash;
    [SerializeField] protected GameObject muzzleLight;
    [SerializeField] protected Transform ejectionPort;
    [SerializeField] protected GameObject shellCasing;
    [SerializeField] private Transform magInGun;
    [SerializeField] private GameObject leftArm;
    [SerializeField] private GameObject rightArm;

    [Header("Gun Position")]
    [SerializeField] private Transform gunPositionHolder;

    [Header("Magazine Drop")]
    public Transform magazineDropPoint;

    public Vector3 magazineDropForce = new Vector3(-2f, -1f, 1f);
    public float magazineDropTorque = 5f;

    [Header("Last Magazine Drop")]
    public Transform lastMagazineDropPoint;

    public Vector3 lastMagazineDropForce = new Vector3(-2f, -1f, 1f);
    public float lastMagazineDropTorque = 5f;

    // ── Cycling (bolt/chamber) state ─────────────────────────────
    public bool IsCychambered { get; private set; } = true;

    private bool isRechambering = false;
    private Coroutine rechamberCoroutine;
    private Coroutine postFireCoroutine;
    private bool rechamberEventSignaled = false;

    // ── Inspection state ──────────────────────────────────────────
    private bool isInspecting = false;

    private Coroutine inspectCoroutine;

    // ── Camera references ─────────────────────────────────────────
    private FollowCamera.CameraFollow cameraFollow;

    protected Camera playerCamera;

    // ── Weapon recoil (viewmodel) state ────────────────────────────
    private Vector3 rotationRecoilVelocity = Vector3.zero;

    private Vector3 positionRecoil;
    private Vector3 weaponRot;
    private float lastShotTime = -999f;

    // ── Rotational sway state ───────────────────────────────────────
    private Quaternion originRotation;

    private float mouseX;
    private float mouseY;

    // ── Jump sway state ──────────────────────────────────────────────
    private float impactForce = 0;

    // ── Bobbing state ─────────────────────────────────────────────────
    private float sinY = 0f;

    private float sinX = 0f;
    private Vector3 lastPosition;

    // ── Core weapon state ───────────────────────────────────────────────
    public bool IsActiveWeapon { get; set; }

    public bool IsADS { get; protected set; }
    public bool IsReloading { get; protected set; }
    public bool ReadyToShoot { get; protected set; } = true;
    public int BulletsLeft { get; protected set; }

    private bool isSwitchingDown = false;
    public bool IsSwitchingDown => isSwitchingDown;

    public bool IsEquipping { get; private set; } = false;
    public Weapon.ShootingMode CurrentShootingMode { get; protected set; }

    // ── Spread state ───────────────────────────────────────────────────
    private float currentSpread;

    private float accumulatedSpread;
    public float CurrentSpread => currentSpread;

    // ── Internal flags / coroutine handles ───────────────────────────────
    protected bool isShooting;

    private bool reloadQueued = false;
    protected bool allowReset = true;
    protected int burstBulletsLeft;
    protected Coroutine reloadCoroutine;
    protected Coroutine burstCoroutine;
    private bool reloadEventSignaled = false;
    private bool reloadLoopEventSignaled = false;
    private float lastEmptySoundTime = -999f;
    private float emptySoundCooldown = 0.1f;

    // ── Events ─────────────────────────────────────────────────────────────
    public event Action<WeaponBase> OnWeaponFired;

    public event Action<WeaponBase> OnReloadStarted;

    public event Action<WeaponBase> OnReloadCompleted;

    public event Action<WeaponBase, bool> OnADSChanged;

    // ── External systems ──────────────────────────────────────────────────
    private PlayerController.PlayerController playerController;

    // ── Public properties ─────────────────────────────────────────────────
    public WeaponData Data => weaponData;

    #endregion Fields

    #region Unity Lifecycle

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

    #endregion Unity Lifecycle

    #region Initialization

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

    #endregion Initialization

    #region Input Handling

    protected virtual void HandleInput()
    {
        // Only hard-block input during switch-down animation
        // During equip (switch-up/first equip), player can switch away freely
        if (IsEquipping && isSwitchingDown) return;

        HandleAiming();
        HandleShooting();   // shooting still blocked via ReadyToShoot = false
        HandleReloading();
        HandleInspection();
        HandleFireModeSwitch();

        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
    }

    protected virtual void HandleReloading()
    {
        if (!Input.GetKeyDown(KeyCode.R)) return;

        if (CanReload())
        {
            StartReload();
        }
        else if (weaponData.RequiresCycling && isRechambering)
        {
            reloadQueued = true; // fire automatically once the bolt finishes cycling
        }
    }

    protected virtual void HandleFireModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.V) && weaponData.availableShootingModes.Length > 1)
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
        weaponAnimator?.ResetTrigger(weaponData.inspectAnimation);
        weaponAnimator?.Play(weaponData.inspectAnimation, 0, 0f);
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

    #endregion Input Handling

    #region Aiming

    protected virtual void HandleAiming()
    {
        if (Input.GetMouseButtonDown(1)) EnterADS();
        else if (Input.GetMouseButtonUp(1)) ExitADS();
    }

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

    protected virtual bool CanShoot() =>
        ReadyToShoot && BulletsLeft > 0 && !IsReloading &&
        (!weaponData.RequiresCycling || IsCychambered);

    protected virtual void FireWeapon()
    {
        if (!CanShoot()) return;

        BulletsLeft--;
        ReadyToShoot = false;

        weaponAnimator?.SetTrigger("SHOOT");

        ApplyRecoilEffects();
        BuildSpread();

        CreateProjectile(CalculateShootDirection());
        PlayShootingEffects();

        if (weaponData.RequiresCycling)
        {
            IsCychambered = false;
            if (BulletsLeft > 0)
                StartPostFireEndlag();
        }
        else
        {
            // Only non-cycling weapons use the fire rate reset
            if (allowReset)
            {
                Invoke(nameof(ResetShot), 60f / weaponData.fireRate);
                allowReset = false;
            }
        }

        HandleShootingMode();
        OnWeaponFired?.Invoke(this);
    }

    protected virtual void ResetShot()
    {
        // Only called for non-cycling weapons
        ReadyToShoot = true;
        allowReset = true;
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
                weaponData.recoilRotationSpeed, weaponData.recoilReturnSpeed,
                weaponData.rollShakeStiffness, weaponData.rollShakeDamping);
        }

        if (weaponData.haveWeaponRecoil)
        {
            Vector3 rotRecoil = IsADS ? weaponData.recoilRotationAds : weaponData.recoilRotationHip;
            Vector3 posRecoil = IsADS ? weaponData.recoilKickBackAds : weaponData.recoilKickBackHip;

            // Kick the spring's velocity — this creates the punchy, multi-wobble settle
            rotationRecoilVelocity += new Vector3(
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

    protected virtual void PlayShootingEffects()
    {
        muzzleFlash?.Play();
        if (muzzleLight != null) StartCoroutine(FlashMuzzleLight());

        // For cycling weapons
        if (!weaponData.RequiresCycling)
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

    #endregion Shooting

    #region Cycling / Rechambering

    private void StartRechamber()
    {
        if (isRechambering || IsCychambered) return;
        if (rechamberCoroutine != null) StopCoroutine(rechamberCoroutine);
        rechamberCoroutine = StartCoroutine(RechamberCoroutine());
    }

    private void StartPostFireEndlag()
    {
        if (postFireCoroutine != null) StopCoroutine(postFireCoroutine);
        postFireCoroutine = StartCoroutine(PostFireEndlagCoroutine());
    }

    private IEnumerator PostFireEndlagCoroutine()
    {
        yield return new WaitForSeconds(weaponData.postFireEndlag);
        postFireCoroutine = null;
        StartRechamber();
    }

    private IEnumerator RechamberCoroutine()
    {
        isRechambering = true;
        ReadyToShoot = false;
        rechamberEventSignaled = false;

        weaponAnimator?.ResetTrigger(weaponData.rechamberAnimation);
        weaponAnimator?.SetTrigger(weaponData.rechamberAnimation);
        SoundManager.Instance?.PlayCycleSound(weaponData.rechamberSounds);

        yield return new WaitUntil(() => rechamberEventSignaled);

        IsCychambered = true;
        isRechambering = false;
        ReadyToShoot = true;
        rechamberCoroutine = null;

        if (reloadQueued && CanReload())
        {
            reloadQueued = false;
            StartReload();
        }
    }

    // Cancels an in-progress rechamber WITHOUT marking the round as chambered.
    private void CancelRechamber()
    {
        reloadQueued = false;
        if (postFireCoroutine != null)
        {
            StopCoroutine(postFireCoroutine);
            postFireCoroutine = null;
        }
        if (rechamberCoroutine != null)
        {
            StopCoroutine(rechamberCoroutine);
            rechamberCoroutine = null;
        }
        isRechambering = false;

        if (weaponData.RequiresCycling)
        {
            weaponAnimator?.ResetTrigger(weaponData.rechamberAnimation);
        }
    }

    // Called by WeaponAnimationEvents
    public void SignalRechamberComplete()
    {
        rechamberEventSignaled = true;
    }

    #endregion Cycling / Rechambering

    #region Fire Mode

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

    protected virtual void CycleFireMode()
    {
        int currentIndex = Array.IndexOf(weaponData.availableShootingModes, CurrentShootingMode);
        CurrentShootingMode = weaponData.availableShootingModes[(currentIndex + 1) % weaponData.availableShootingModes.Length];
        Debug.Log($"[WeaponBase] Fire mode: {CurrentShootingMode}");
    }

    #endregion Fire Mode

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
    !IsReloading && !isRechambering && BulletsLeft < weaponData.magazineSize && HasAmmoAvailable();

    protected virtual bool HasAmmoAvailable() =>
        WeaponManager.Instance?.CheckAmmoLeftFor(weaponData.ammoType) > 0;

    protected virtual void StartReload()
    {
        CancelRechamber();
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
        bool reloadIncludesChambering = weaponData.RequiresCycling && BulletsLeft == 0;

        weaponAnimator?.SetTrigger(GetReloadAnimationName());
        OnReloadStarted?.Invoke(this);

        reloadEventSignaled = false;
        while (!reloadEventSignaled)
            yield return null;

        CompleteReload(reloadIncludesChambering);
    }

    protected virtual IEnumerator ShellReloadCoroutine()
    {
        OnReloadStarted?.Invoke(this);

        while (BulletsLeft < weaponData.magazineSize)
        {
            int available = WeaponManager.Instance?.CheckAmmoLeftFor(weaponData.ammoType) ?? 0;
            if (available <= 0) break;

            // How many shells this loop inserts
            int shellsThisLoop = Mathf.Min(
                weaponData.reloadLoopAmount,
                weaponData.magazineSize - BulletsLeft,
                available);

            // Check if this is the last loop — play finish anim instead
            bool isLastLoop = (BulletsLeft + shellsThisLoop >= weaponData.magazineSize) ||
                              (available - shellsThisLoop <= 0);

            string animTrigger = isLastLoop
                ? weaponData.reloadFinishAnimation
                : weaponData.reloadLoopAnimation;

            reloadLoopEventSignaled = false;
            weaponAnimator?.SetTrigger(animTrigger);
            SoundManager.Instance?.PlayShellLoadSound();

            // Wait for animation event — fallback to shellLoadTime
            float elapsed = 0f;
            while (!reloadLoopEventSignaled && elapsed < weaponData.shellLoadTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Add shells after animation completes
            for (int i = 0; i < shellsThisLoop; i++)
            {
                BulletsLeft++;
                WeaponManager.Instance?.DecreaseTotalAmmo(1, weaponData.ammoType);
            }

            if (isLastLoop) break;
        }

        // After shell reload done — rechamber if needed
        if (weaponData.RequiresCycling && !IsCychambered)
            yield return StartCoroutine(RechamberCoroutine());

        ReadyToShoot = true;
        IsReloading = false;
        OnReloadCompleted?.Invoke(this);
    }

    protected virtual void CompleteReload(bool chamberedDuringReload = false)
    {
        ReadyToShoot = true;
        IsReloading = false;

        int needed = weaponData.magazineSize - BulletsLeft;
        int available = WeaponManager.Instance?.CheckAmmoLeftFor(weaponData.ammoType) ?? 0;
        int toReload = Mathf.Min(needed, available);

        BulletsLeft += toReload;
        WeaponManager.Instance?.DecreaseTotalAmmo(toReload, weaponData.ammoType);

        if (weaponData.RequiresCycling && !IsCychambered)
        {
            if (chamberedDuringReload)
            {
                // Empty-reload clip already visually chambers a round (bolt back → mag out → mag in → bolt forward)
                IsCychambered = true;
            }
            else
            {
                ReadyToShoot = false;
                StartRechamber();
            }
        }

        OnReloadCompleted?.Invoke(this);
    }

    protected virtual void CancelReload()
    {
        IsReloading = false;
        StopReloadVisualsAndResolveReadiness();
        SoundManager.Instance?.StopReloadSound();
    }

    private void ForceStopReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        IsReloading = false;
        StopReloadVisualsAndResolveReadiness();
        SoundManager.Instance?.StopReloadSound();
    }

    private void StopReloadVisualsAndResolveReadiness()
    {
        if (weaponAnimator != null && weaponAnimator.isActiveAndEnabled)
        {
            ResetReloadTriggers();
            weaponAnimator.Play("Idle", -1, 0f);
            weaponAnimator.Update(0f);
        }

        if (weaponData.RequiresCycling && !IsCychambered)
        {
            ReadyToShoot = false;
            StartRechamber();
        }
        else
        {
            ReadyToShoot = true;
        }
    }

    private void ResetReloadTriggers()
    {
        weaponAnimator.ResetTrigger(weaponData.reloadAnimation);
        weaponAnimator.ResetTrigger(weaponData.tacticalReloadAnimation);
        weaponAnimator.ResetTrigger(weaponData.lastBulletReloadAnimation);
        weaponAnimator?.ResetTrigger(weaponData.inspectAnimation);
    }

    // Called by WeaponAnimationEvents
    public void SignalReloadComplete() => reloadEventSignaled = true;

    public void SignalReloadLoopComplete()
    {
        reloadLoopEventSignaled = true;
    }

    #endregion Reloading

    #region Magazine Drop

    public void DropMagazine()
    {
        if (weaponData.magazineDropPrefab == null || magazineDropPoint == null) return;

        GameObject mag = Instantiate(weaponData.magazineDropPrefab,
            magazineDropPoint.position, magazineDropPoint.rotation);

        var rb = mag.GetComponent<Rigidbody>() ?? mag.AddComponent<Rigidbody>();
        rb.AddForce(magazineDropPoint.TransformDirection(magazineDropForce), ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * magazineDropTorque, ForceMode.Impulse);
        Destroy(mag, 20f);
    }

    public void DropMagazineLastReload()
    {
        if (weaponData.magazineDropPrefab == null || lastMagazineDropPoint == null) return;

        GameObject mag = Instantiate(weaponData.magazineDropPrefab,
            lastMagazineDropPoint.position, lastMagazineDropPoint.rotation);

        var rb = mag.GetComponent<Rigidbody>() ?? mag.AddComponent<Rigidbody>();
        rb.AddForce(lastMagazineDropPoint.TransformDirection(lastMagazineDropForce), ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * lastMagazineDropTorque, ForceMode.Impulse);
        Destroy(mag, 20f);
    }

    #endregion Magazine Drop

    #region Weapon Switching

    public void StartSwitchDown()
    {
        if (IsReloading) ForceStopReload();
        CancelRechamber();
        if (IsADS) ExitADS();

        isShooting = false;
        IsEquipping = true;
        isSwitchingDown = true;
        ReadyToShoot = false;

        if (weaponAnimator != null && weaponAnimator.isActiveAndEnabled)
        {
            ResetReloadTriggers();
            weaponAnimator.SetTrigger(weaponData.switchDownAnimation);
        }
    }

    public void CancelSwitchDown()
    {
        isSwitchingDown = false;
        IsEquipping = false;

        if (weaponData.RequiresCycling && !IsCychambered)
        {
            ReadyToShoot = false;
            StartRechamber();
        }
        else
        {
            ReadyToShoot = true;
        }

        if (weaponAnimator != null && weaponAnimator.isActiveAndEnabled)
        {
            weaponAnimator.ResetTrigger(weaponData.switchDownAnimation);
            weaponAnimator.Play("Idle", -1, 0f);
        }
    }

    public void EquipCompleted()
    {
        IsEquipping = false;
        isSwitchingDown = false;
        ReadyToShoot = true;
    }

    public void SwitchDownCompleted()
    {
        isSwitchingDown = false;
        WeaponManager.Instance?.ExecutePendingSwitch();
    }

    public virtual void SetActiveWeapon(bool active, bool isFirstPickup = false)
    {
        IsActiveWeapon = active;

        if (weaponAnimator != null)
            weaponAnimator.enabled = active;

        if (leftArm != null) leftArm.SetActive(active);
        if (rightArm != null) rightArm.SetActive(active);

        if (!active)
        {
            rotationRecoilVelocity = Vector3.zero; // was: rotationRecoil = Vector3.zero;
            weaponRot = Vector3.zero;
            positionRecoil = Vector3.zero;
            cameraFollow?.ResetRecoil();

            CancelRechamber();

            if (IsReloading) ForceStopReload();

            isShooting = false;
            IsEquipping = false;
            ReadyToShoot = false;
            if (IsADS) ExitADS();

            GetComponent<Outline>().enabled = false;
        }
        else
        {
            if (weaponAnimator != null)
            {
                ResetReloadTriggers();
                weaponAnimator.ResetTrigger(weaponData.inspectAnimation);
                weaponAnimator.ResetTrigger(weaponData.switchDownAnimation);
                weaponAnimator.ResetTrigger(weaponData.switchUpAnimation);
                weaponAnimator.ResetTrigger(weaponData.firstEquipAnimation);

                if (weaponData.RequiresCycling)
                {
                    weaponAnimator.ResetTrigger(weaponData.rechamberAnimation);
                }
            }

            IsEquipping = true;
            ReadyToShoot = false;

            if (isFirstPickup)
                weaponAnimator?.SetTrigger(weaponData.firstEquipAnimation);
            else
                weaponAnimator?.SetTrigger(weaponData.switchUpAnimation);

            if (weaponData.RequiresCycling && !IsCychambered && !isRechambering)
                StartRechamber();
        }
    }

    public void EnableAnimator()
    {
        if (weaponAnimator != null)
            weaponAnimator.enabled = true;
    }

    #endregion Weapon Switching

    #region Weapon Effects (Recoil / Sway / Bobbing)

    private void HandleWeaponRecoil()
    {
        if (!weaponData.haveWeaponRecoil || gunPositionHolder == null) return;

        // Position: simple punch-and-return
        positionRecoil = Vector3.Lerp(positionRecoil, Vector3.zero,
            weaponData.gunPositionReturnSpeed * Time.deltaTime);
        gunPositionHolder.localPosition = Vector3.Slerp(gunPositionHolder.localPosition,
            positionRecoil, weaponData.gunRecoilPositionSpeed * Time.deltaTime);

        // Rotation: damped spring — kicks and wobbles before settling
        Vector3 rotAccel = -weaponData.gunRecoilShakeStiffness * weaponRot
                            - weaponData.gunRecoilShakeDamping * rotationRecoilVelocity;
        rotationRecoilVelocity += rotAccel * Time.deltaTime;
        weaponRot += rotationRecoilVelocity * Time.deltaTime;

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

    #endregion Weapon Effects (Recoil / Sway / Bobbing)

    #region Sounds

    protected virtual void PlayCycleSound() => SoundManager.Instance?.PlayCycleSound(weaponData.rechamberSounds);

    protected virtual void PlayReloadSound() => SoundManager.Instance?.PlayReloadSound(weaponData.reloadSound);

    protected virtual void PlayShellLoadSound() => SoundManager.Instance?.PlayShellLoadSound();

    protected virtual void PlayEmptySound()
    {
        if (Time.time - lastEmptySoundTime < emptySoundCooldown) return;
        lastEmptySoundTime = Time.time;
        SoundManager.Instance?.PlayShootingSound(weaponData.emptySound);
    }

    #endregion Sounds

    #region Public API

    public virtual WeaponInfo GetWeaponInfo()
    {
        return new WeaponInfo
        {
            Model = weaponData.weaponModel,
            Damage = weaponData.damage,
            FireRate = weaponData.fireRate,
            Range = weaponData.range,
            BulletsLeft = BulletsLeft,
            MagSize = weaponData.magazineSize,
            CurrentFireMode = CurrentShootingMode,
            AvailableFireModes = weaponData.availableShootingModes,
            IsReloading = IsReloading,
            IsADS = IsADS,
            WeaponName = weaponData.weaponName,
            AmmoType = weaponData.ammoType,
            Rarity = weaponData.rarity
        };
    }

    public virtual void RefillAmmo() => BulletsLeft = weaponData.magazineSize;

    public virtual float GetDamageAtDistance(float distance) => weaponData.GetDamageAtDistance(distance);

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