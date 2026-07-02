using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

#region Interfaces

public interface IProjectile
{
    float Speed { get; }
    int Damage { get; }
    bool IsActive { get; }

    void Launch(Vector3 direction, float speed);

    void SetDamage(int damage);

    void Deactivate();

    event Action<IProjectile, Collision> OnImpact;
}

public interface IEffectProvider
{
    void CreateEffect(Vector3 position, Vector3 normal, GameObject target = null);
}

public interface IAudioProvider
{
    void PlaySound(Vector3 position);
}

#endregion Interfaces

#region Data Classes

[System.Serializable]
public class ProjectileConfig
{
    [Header("Basic Properties")]
    public float lifetime = 5f;

    [Header("Physics")]
    public bool useGravity = false;

    public float drag = 0f;
    public float mass = 1f;

    [Header("Penetration")]
    public bool canPenetrate = false;

    public int maxPenetrations = 1;
    public List<string> penetrableTags = new List<string>();
}

[System.Serializable]
public class CollisionEffects
{
    [Header("Visual Effects")]
    public VisualEffect impactVFX;

    public GameObject impactPrefab;

    [Header("Audio")]
    public AudioClip impactSound;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Settings")]
    public float effectLifetime = 5f;

    public bool parentToTarget = true;
}

public class ImpactData
{
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public GameObject Target { get; }
    public Collider HitCollider { get; }
    public float ImpactForce { get; }

    public ImpactData(ContactPoint contact, GameObject target, float force = 0f)
    {
        Point = contact.point;
        Normal = contact.normal;
        Target = target;
        HitCollider = contact.thisCollider;
        ImpactForce = force;
    }

    public ImpactData(Vector3 point, Vector3 normal, GameObject target,
        Collider collider = null, float force = 0f)
    {
        Point = point;
        Normal = normal;
        Target = target;
        HitCollider = collider;
        ImpactForce = force;
    }
}

#endregion Data Classes

#region Static Effect Helper

public static class EffectHelper
{
    public static void CreateBloodEffect(Vector3 position, Vector3 normal, GameObject target)
    {
        var prefab = GlobalReference.Instance?.BloodSprayEffect;
        if (prefab == null) return;
        var fx = UnityEngine.Object.Instantiate(prefab, position, Quaternion.LookRotation(normal));
        if (target != null) fx.transform.SetParent(target.transform);
        UnityEngine.Object.Destroy(fx, 5f);
    }

    public static void CreateBulletHoleEffect(Vector3 position, Vector3 normal, GameObject target)
    {
        var prefab = GlobalReference.Instance?.bulletImpactEffectPrefab;
        if (prefab == null) return;
        var fx = UnityEngine.Object.Instantiate(prefab, position, Quaternion.LookRotation(normal));
        UnityEngine.Object.Destroy(fx, 5f);
    }

    public static void PlayImpactAudio(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}

#endregion Static Effect Helper

#region Collision Handlers

public abstract class CollisionHandler
{
    protected IProjectile projectile;

    protected CollisionHandler(IProjectile projectile)
    {
        this.projectile = projectile;
    }

    public abstract bool CanHandle(string tag);

    public abstract void HandleCollision(GameObject target, ImpactData impactData);
}

public class EnemyCollisionHandler : CollisionHandler
{
    public EnemyCollisionHandler(IProjectile projectile) : base(projectile)
    {
    }

    public override bool CanHandle(string tag) => tag == "Enemy";

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        var enemy = target.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogWarning($"[EnemyCollisionHandler] {target.name} tagged Enemy but has no Enemy component.");
            return;
        }

        if (!enemy.isDead)
        {
            var damageInfo = new DamageInfo(
                projectile.Damage,
                impactData.Point,
                -impactData.Normal,
                (projectile as MonoBehaviour)?.gameObject,
                DamageType.Bullet);

            if (enemy is IDamageable damageable)
                damageable.TakeDamage(projectile.Damage, damageInfo);
            else
                enemy.TakeDamage(projectile.Damage);

            SoundManager.Instance?.PlayEnemyHitmarker();
            EffectHelper.CreateBloodEffect(impactData.Point, impactData.Normal, target);
        }

        if (enemy.isDead)
        {
            SoundManager.Instance?.PlayKillFeedback();
            target.GetComponent<CapsuleCollider>()?.gameObject.SetActive(false);
        }
    }
}

public class PlayerCollisionHandler : CollisionHandler
{
    public PlayerCollisionHandler(IProjectile projectile) : base(projectile)
    {
    }

    public override bool CanHandle(string tag) => tag == "Player";

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        // Player damage implementation pending
    }
}

public class EnvironmentCollisionHandler : CollisionHandler
{
    public EnvironmentCollisionHandler(IProjectile projectile) : base(projectile)
    {
    }

    public override bool CanHandle(string tag) =>
        tag == "Wall" || tag == "Target" || tag == "Environment";

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        EffectHelper.CreateBulletHoleEffect(impactData.Point, impactData.Normal, target);
    }
}

#endregion Collision Handlers

#region Abstract Projectile Base

public abstract class ProjectileBase : MonoBehaviour, IProjectile
{
    [Header("Configuration")]
    [SerializeField] protected ProjectileConfig config;

    protected Rigidbody projectileRigidbody;
    protected List<CollisionHandler> collisionHandlers;
    protected int penetrationCount = 0;

    // Damage is set externally by WeaponBase via SetDamage()
    // so it always reflects weaponData.damage — never a local inspector value
    private int _damage = 0;

    public float Speed { get; private set; }
    public int Damage => _damage;
    public bool IsActive { get; protected set; } = true;

    public event Action<IProjectile, Collision> OnImpact;

    protected virtual void Awake()
    {
        InitializeComponents();
        InitializeCollisionHandlers();
    }

    // Start() intentionally does NOT call Launch() —
    // Launch() is called explicitly by WeaponBase.ConfigureProjectile()
    // with the correct direction and speed from WeaponData.
    // Calling it here would override the velocity set by the weapon.
    protected virtual void Start()
    {
        StartCoroutine(DestroyAfterLifetime());
    }

    protected virtual void InitializeComponents()
    {
        projectileRigidbody = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        projectileRigidbody.useGravity = config.useGravity;
        projectileRigidbody.linearDamping = config.drag;
        projectileRigidbody.mass = config.mass;
    }

    protected virtual void InitializeCollisionHandlers()
    {
        collisionHandlers = new List<CollisionHandler>
        {
            new EnemyCollisionHandler(this),
            new PlayerCollisionHandler(this),
            new EnvironmentCollisionHandler(this)
        };
    }

    // Called by WeaponBase.ConfigureProjectile() — sets damage from weaponData
    public void SetDamage(int damage) => _damage = damage;

    // Called by WeaponBase.ConfigureProjectile() — sets direction and speed from weaponData
    public virtual void Launch(Vector3 direction, float speed)
    {
        Speed = speed;
        if (projectileRigidbody != null)
            projectileRigidbody.linearVelocity = direction.normalized * speed;
    }

    public virtual void ResetState()
    {
        IsActive = true;
        penetrationCount = 0;

        if (projectileRigidbody != null)
        {
            projectileRigidbody.linearVelocity = Vector3.zero;
            projectileRigidbody.angularVelocity = Vector3.zero;
        }

        StopAllCoroutines();
        StartCoroutine(DestroyAfterLifetime());
    }

    public virtual void Deactivate()
    {
        IsActive = false;

        if (projectileRigidbody != null)
        {
            projectileRigidbody.linearVelocity = Vector3.zero;
            projectileRigidbody.angularVelocity = Vector3.zero;
        }

        StopAllCoroutines();

        var poolable = GetComponent<PoolableProjectile>();
        if (poolable != null) poolable.ReturnToPool();
        else Destroy(gameObject);
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!IsActive) return;

        OnImpact?.Invoke(this, collision);

        GameObject target = collision.gameObject;
        ImpactData impactData = new ImpactData(collision.contacts[0], target);

        HandleCollision(target, impactData);
    }

    protected virtual void HandleCollision(GameObject target, ImpactData impactData)
    {
        foreach (var handler in collisionHandlers)
        {
            if (handler.CanHandle(target.tag))
            {
                handler.HandleCollision(target, impactData);
                break;
            }
        }

        HandlePenetrationOrDestroy(target);
    }

    protected virtual void HandlePenetrationOrDestroy(GameObject target)
    {
        if (config.canPenetrate && CanPenetrateTarget(target) &&
            penetrationCount < config.maxPenetrations)
        {
            penetrationCount++;
        }
        else
        {
            Deactivate();
        }
    }

    protected virtual bool CanPenetrateTarget(GameObject target) =>
        config.penetrableTags.Contains(target.tag);

    private IEnumerator DestroyAfterLifetime()
    {
        yield return new WaitForSeconds(config.lifetime);
        if (gameObject != null) Deactivate();
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}

#endregion Abstract Projectile Base

#region Concrete Projectile Implementations

public class Bullet : ProjectileBase
{
    protected override void InitializeCollisionHandlers()
    {
        collisionHandlers = new List<CollisionHandler>
        {
            new BulletEnemyHandler(this),
            new BulletPlayerHandler(this),
            new BulletEnvironmentHandler(this)
        };
    }
}

public class Grenade : ProjectileBase
{
    [Header("Grenade Specific")]
    [SerializeField] private float explosionRadius = 5f;

    [SerializeField] private float explosionForce = 500f;
    [SerializeField] private float fuseTime = 3f;
    [SerializeField] private LayerMask explosionLayers = ~0;

    protected override void Start()
    {
        base.Start();
        StartCoroutine(ExplodeAfterFuse());
    }

    private IEnumerator ExplodeAfterFuse()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    protected virtual void Explode()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius, explosionLayers);

        foreach (var hitCollider in hitColliders)
        {
            var damageable = hitCollider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                float damageMultiplier = 1f - (distance / explosionRadius);
                int explosionDamage = Mathf.RoundToInt(Damage * damageMultiplier);

                var damageInfo = new DamageInfo(
                    explosionDamage,
                    hitCollider.transform.position,
                    (hitCollider.transform.position - transform.position).normalized,
                    gameObject,
                    DamageType.Explosion);

                damageable.TakeDamage(explosionDamage, damageInfo);
            }

            hitCollider.GetComponent<Rigidbody>()?.AddExplosionForce(
                explosionForce, transform.position, explosionRadius);
        }

        Deactivate();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

#endregion Concrete Projectile Implementations

#region Specialized Collision Handlers

public class BulletEnemyHandler : EnemyCollisionHandler
{
    public BulletEnemyHandler(IProjectile projectile) : base(projectile)
    {
    }

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        base.HandleCollision(target, impactData);
        BulletImpactEvents.Instance?.InvokeEnemyHit(impactData.Point, projectile.Damage);
    }
}

public class BulletPlayerHandler : PlayerCollisionHandler
{
    public BulletPlayerHandler(IProjectile projectile) : base(projectile)
    {
    }

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        base.HandleCollision(target, impactData);
        BulletImpactEvents.Instance?.InvokePlayerHit(impactData.Point, projectile.Damage);
    }
}

public class BulletEnvironmentHandler : EnvironmentCollisionHandler
{
    public BulletEnvironmentHandler(IProjectile projectile) : base(projectile)
    {
    }

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        base.HandleCollision(target, impactData);

        if (target.CompareTag("Wall"))
            BulletImpactEvents.Instance?.InvokeWallHit(impactData.Point);
        else if (target.CompareTag("Target"))
            BulletImpactEvents.Instance?.InvokeTargetHit(impactData.Point);
    }
}

#endregion Specialized Collision Handlers

#region Events

public class BulletImpactEvents : MonoBehaviour
{
    public static BulletImpactEvents Instance { get; private set; }

    public event Action<Vector3, int> OnEnemyHit;

    public event Action<Vector3, int> OnPlayerHit;

    public event Action<Vector3> OnWallHit;

    public event Action<Vector3> OnTargetHit;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public void InvokeEnemyHit(Vector3 position, int damage) => OnEnemyHit?.Invoke(position, damage);

    public void InvokePlayerHit(Vector3 position, int damage) => OnPlayerHit?.Invoke(position, damage);

    public void InvokeWallHit(Vector3 position) => OnWallHit?.Invoke(position);

    public void InvokeTargetHit(Vector3 position) => OnTargetHit?.Invoke(position);
}

#endregion Events

public enum ProjectileType
{ Bullet, Grenade, Rocket }