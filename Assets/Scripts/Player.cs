using System;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

#region Interfaces

public interface IDamageable
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsDead { get; }

    void TakeDamage(int damage, DamageInfo damageInfo = null);

    event Action<IDamageable, DamageInfo> OnDamaged;

    event Action<IDamageable> OnDeath;
}

public interface IHealable
{
    void Heal(int healAmount);

    event Action<int, int> OnHealed; // amount healed, new health
}

public interface IRespawnable
{
    void Respawn();

    void Respawn(Vector3 position);

    event Action OnRespawned;
}

public interface IControllable
{
    bool ControlsEnabled { get; }

    void EnableControls();

    void DisableControls();
}

#endregion Interfaces

#region Data Classes

[System.Serializable]
public class HealthConfig
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool canHealAboveMax = false;
    [SerializeField] private float healthRegenRate = 0f;
    [SerializeField] private float healthRegenDelay = 5f;

    public int MaxHealth => maxHealth;
    public bool CanHealAboveMax => canHealAboveMax;
    public float HealthRegenRate => healthRegenRate;
    public float HealthRegenDelay => healthRegenDelay;
}

[System.Serializable]
public class PlayerReferences
{
    [Header("UI References")]
    public GameObject bloodyVignette;

    public TextMeshProUGUI playerHpUI;
    public GameObject gameOverUI;

    [Header("Camera References")]
    public CinemachineCamera playerCamera;

    public CinemachineCamera deathCamera;

    [Header("Post Processing")]
    public Volume globalVolume;
}

[System.Serializable]
public class EffectSettings
{
    [Header("Visual Effects")]
    public float bloodyEffectDuration = 1f;

    public float gameOverDelay = 1f;
    public float deathSoundDelay = 2f;

    [Header("Camera Shake")]
    public float damageShakeIntensity = 0.5f;

    public float damageShakeDuration = 0.2f;
}

public class DamageInfo
{
    public int Amount { get; }
    public Vector3 HitPoint { get; }
    public Vector3 HitDirection { get; }
    public GameObject Source { get; }
    public DamageType Type { get; }

    public DamageInfo(int amount, Vector3 hitPoint = default, Vector3 hitDirection = default,
                     GameObject source = null, DamageType type = DamageType.Generic)
    {
        Amount = amount;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        Source = source;
        Type = type;
    }
}

public enum DamageType
{
    Generic,
    Bullet,
    Melee,
    Explosion,
    Environmental,
    Fall
}

#endregion Data Classes

#region Abstract Base Classes

public abstract class LivingEntity : MonoBehaviour, IDamageable
{
    [SerializeField] protected HealthConfig healthConfig;

    protected int currentHealth;
    protected bool isDead;

    // IDamageable implementation
    public int CurrentHealth => currentHealth;

    public int MaxHealth => healthConfig.MaxHealth;
    public bool IsDead => isDead;

    public event Action<IDamageable, DamageInfo> OnDamaged;

    public event Action<IDamageable> OnDeath;

    protected virtual void Awake()
    {
        InitializeHealth();
    }

    protected virtual void InitializeHealth()
    {
        currentHealth = healthConfig.MaxHealth;
        isDead = false;
    }

    public virtual void TakeDamage(int damage, DamageInfo damageInfo = null)
    {
        if (isDead || damage <= 0) return;

        damageInfo ??= new DamageInfo(damage);

        int actualDamage = CalculateDamage(damage, damageInfo);
        currentHealth = Mathf.Max(0, currentHealth - actualDamage);

        OnDamaged?.Invoke(this, damageInfo);
        OnDamageReceived(damageInfo);

        if (currentHealth <= 0 && !isDead)
        {
            Die();
        }
    }

    protected virtual int CalculateDamage(int baseDamage, DamageInfo damageInfo)
    {
        // Override in derived classes for damage reduction, armor, etc.
        return baseDamage;
    }

    protected virtual void OnDamageReceived(DamageInfo damageInfo)
    {
        // Override in derived classes for specific damage reactions
    }

    protected virtual void Die()
    {
        isDead = true;
        OnDeath?.Invoke(this);
        OnDeathInternal();
    }

    protected abstract void OnDeathInternal();
}

public abstract class ControllableEntity : LivingEntity, IControllable
{
    protected IPlayerController[] controllers;

    public bool ControlsEnabled { get; private set; } = true;

    protected override void Awake()
    {
        base.Awake();
        CacheControllers();
    }

    protected virtual void CacheControllers()
    {
        controllers = GetComponents<IPlayerController>();
    }

    public virtual void EnableControls()
    {
        ControlsEnabled = true;
        foreach (var controller in controllers)
        {
            controller?.EnableController();
        }
    }

    public virtual void DisableControls()
    {
        ControlsEnabled = false;
        foreach (var controller in controllers)
        {
            controller?.DisableController();
        }
    }
}

#endregion Abstract Base Classes

#region Controller Interface

public interface IPlayerController
{
    bool IsEnabled { get; }

    void EnableController();

    void DisableController();
}

// You would implement this on MouseMovement and PlayerMovement components
public abstract class PlayerControllerBase : MonoBehaviour, IPlayerController
{
    public bool IsEnabled { get; protected set; } = true;

    protected virtual void Awake()
    {
        enabled = IsEnabled;
    }

    public virtual void EnableController()
    {
        IsEnabled = true;
        enabled = true;
    }

    public virtual void DisableController()
    {
        IsEnabled = false;
        enabled = false;
    }
}

#endregion Controller Interface

#region Player Class

public class Player : ControllableEntity, IHealable, IRespawnable
{
    [Header("Player Configuration")]
    [SerializeField] private PlayerReferences references;

    [SerializeField] private EffectSettings effectSettings;

    // Cached components
    private Animator animator;

    private Blackout blackout;
    private Image bloodyVignetteImage;
    private DepthOfField depthOfField;

    // IHealable implementation
    public event Action<int, int> OnHealed;

    // IRespawnable implementation
    public event Action OnRespawned;

    // Player-specific events
    public static event Action<int, int> OnHealthChanged;

    public static event Action OnPlayerDeath;

    public static event Action<int> OnPlayerDamaged;

    private Vector3 respawnPosition;
    private Quaternion respawnRotation;

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        CacheComponents();
        StoreRespawnPoint();
    }

    private void Start()
    {
        InitializeReferences();
        InitializePostProcessing();
        UpdateHealthUI();
    }

    private void Update()
    {
        HandleDebugInput();
    }

    #endregion Unity Lifecycle

    #region Initialization

    private void CacheComponents()
    {
        animator = GetComponentInChildren<Animator>();
        blackout = GetComponent<Blackout>();

        if (references.bloodyVignette != null)
        {
            bloodyVignetteImage = references.bloodyVignette.GetComponentInChildren<Image>();
        }

        if (references.globalVolume?.profile.TryGet(out depthOfField) == true)
        {
            depthOfField.active = false;
        }
    }

    private void InitializeReferences()
    {
        if (references.playerCamera != null) references.playerCamera.Priority = 10;
        if (references.deathCamera != null) references.deathCamera.Priority = 5;
    }

    private void InitializePostProcessing()
    {
        if (depthOfField != null)
        {
            depthOfField.active = false;
        }
    }

    private void StoreRespawnPoint()
    {
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    #endregion Initialization

    #region Health System Override

    protected override void OnDamageReceived(DamageInfo damageInfo)
    {
        base.OnDamageReceived(damageInfo);

        Debug.Log($"Player took {damageInfo.Amount} {damageInfo.Type} damage. Health: {currentHealth}/{MaxHealth}");

        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnPlayerDamaged?.Invoke(damageInfo.Amount);

        StartCoroutine(PlayBloodyScreenEffect());
        PlayDamageSound();
    }

    protected override void OnDeathInternal()
    {
        Debug.Log("Player died");
        OnPlayerDeath?.Invoke();

        DisableControls();
        PlayDeathEffects();
        StartCoroutine(ShowGameOverSequence());
    }

    // IHealable implementation
    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return;

        int oldHealth = currentHealth;
        int maxPossibleHealth = healthConfig.CanHealAboveMax ? int.MaxValue : MaxHealth;
        currentHealth = Mathf.Min(maxPossibleHealth, currentHealth + healAmount);

        int actualHealAmount = currentHealth - oldHealth;
        if (actualHealAmount > 0)
        {
            UpdateHealthUI();
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            OnHealed?.Invoke(actualHealAmount, currentHealth);
        }
    }

    #endregion Health System Override

    #region Respawn System

    public void Respawn()
    {
        Respawn(respawnPosition);
    }

    public void Respawn(Vector3 position)
    {
        // Reset health and state
        InitializeHealth();

        // Reset position
        transform.position = position;
        transform.rotation = respawnRotation;

        // Re-enable controls
        EnableControls();

        // Reset UI
        if (references.playerHpUI != null) references.playerHpUI.gameObject.SetActive(true);
        if (references.gameOverUI != null) references.gameOverUI.SetActive(false);

        // Reset cameras
        SwitchToPlayerCamera();

        // Reset post-processing
        if (depthOfField != null) depthOfField.active = false;

        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnRespawned?.Invoke();

        Debug.Log("Player respawned");
    }

    public void SetRespawnPoint(Vector3 position, Quaternion rotation)
    {
        respawnPosition = position;
        respawnRotation = rotation;
    }

    #endregion Respawn System

    #region UI and Effects

    private void UpdateHealthUI()
    {
        if (references.playerHpUI != null)
        {
            references.playerHpUI.text = $"Health: {currentHealth}/{MaxHealth}";
        }
    }

    private IEnumerator PlayBloodyScreenEffect()
    {
        if (references.bloodyVignette == null || bloodyVignetteImage == null) yield break;

        references.bloodyVignette.SetActive(true);

        Color startColor = bloodyVignetteImage.color;
        startColor.a = 1f;
        bloodyVignetteImage.color = startColor;

        float elapsedTime = 0f;
        while (elapsedTime < effectSettings.bloodyEffectDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / effectSettings.bloodyEffectDuration);

            Color newColor = bloodyVignetteImage.color;
            newColor.a = alpha;
            bloodyVignetteImage.color = newColor;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        startColor.a = 0f;
        bloodyVignetteImage.color = startColor;
        references.bloodyVignette.SetActive(false);
    }

    private IEnumerator ShowGameOverSequence()
    {
        yield return new WaitForSeconds(effectSettings.gameOverDelay);

        if (references.gameOverUI != null)
        {
            references.gameOverUI.SetActive(true);
        }

        if (depthOfField != null)
        {
            depthOfField.active = true;
        }
    }

    #endregion UI and Effects

    #region Audio

    private void PlayDamageSound()
    {
        if (SoundManager.Instance?.playerChannel != null && SoundManager.Instance?.playerHurt != null)
        {
            SoundManager.Instance.playerChannel.PlayOneShot(SoundManager.Instance.playerHurt);
        }
    }

    private void PlayDeathEffects()
    {
        if (SoundManager.Instance?.playerChannel != null)
        {
            if (SoundManager.Instance.playerDie != null)
            {
                SoundManager.Instance.playerChannel.PlayOneShot(SoundManager.Instance.playerDie);
            }

            if (SoundManager.Instance.gameOverMusic != null)
            {
                SoundManager.Instance.playerChannel.clip = SoundManager.Instance.gameOverMusic;
                SoundManager.Instance.playerChannel.PlayDelayed(effectSettings.deathSoundDelay);
            }
        }

        if (animator != null) animator.enabled = true;
        if (references.playerHpUI != null) references.playerHpUI.gameObject.SetActive(false);
        if (blackout != null) blackout.StartFade();
    }

    #endregion Audio

    #region Camera Management

    private void HandleDebugInput()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.V))
        {
            SwitchToDeathCamera();
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            SwitchToPlayerCamera();
        }
#endif
    }

    private void SwitchToDeathCamera()
    {
        if (references.playerCamera != null) references.playerCamera.Priority = 5;
        if (references.deathCamera != null) references.deathCamera.Priority = 10;
    }

    private void SwitchToPlayerCamera()
    {
        if (references.playerCamera != null) references.playerCamera.Priority = 10;
        if (references.deathCamera != null) references.deathCamera.Priority = 5;
    }

    #endregion Camera Management

    #region Collision Handling

    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (other.CompareTag("Zombie"))
        {
            var zombieHand = other.GetComponent<ZombieHand>();
            if (zombieHand != null)
            {
                var damageInfo = new DamageInfo(
                    zombieHand.damage,
                    other.transform.position,
                    (transform.position - other.transform.position).normalized,
                    other.gameObject,
                    DamageType.Melee
                );
                TakeDamage(zombieHand.damage, damageInfo);
            }
        }
        //else if (other.CompareTag("HealthPack"))
        //{
        //    var healthPack = other.GetComponent<HealthPack>();
        //    if (healthPack != null)
        //    {
        //        Heal(healthPack.healAmount);
        //        Destroy(other.gameObject);
        //    }
        //}
    }

    #endregion Collision Handling

    #region Utility Methods

    public float GetHealthPercentage()
    {
        return MaxHealth > 0 ? (float)currentHealth / MaxHealth : 0f;
    }

    public bool IsHealthy()
    {
        return GetHealthPercentage() > 0.75f;
    }

    public bool IsInjured()
    {
        return GetHealthPercentage() < 0.5f;
    }

    public bool IsCritical()
    {
        return GetHealthPercentage() < 0.25f;
    }
}

#endregion Utility Methods


#endregion Player Class