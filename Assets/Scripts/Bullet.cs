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
    public int damage = 25;

    public float speed = 100f;
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

    [Range(0f, 1f)] public float volume = 1f;

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

    public ImpactData(Vector3 point, Vector3 normal, GameObject target, Collider collider = null, float force = 0f)
    {
        Point = point;
        Normal = normal;
        Target = target;
        HitCollider = collider;
        ImpactForce = force;
    }
}

#endregion Data Classes

#region Effect Classes

public abstract class ImpactEffect : MonoBehaviour, IEffectProvider
{
    [SerializeField] protected CollisionEffects effectData;

    public abstract void CreateEffect(Vector3 position, Vector3 normal, GameObject target = null);

    protected virtual void PlayAudio(Vector3 position)
    {
        if (effectData.impactSound != null)
        {
            AudioSource.PlayClipAtPoint(effectData.impactSound, position, effectData.volume);
        }
    }

    protected virtual GameObject InstantiateEffect(GameObject prefab, Vector3 position, Vector3 normal, GameObject target)
    {
        if (prefab == null) return null;

        var effect = Instantiate(prefab, position, Quaternion.LookRotation(normal));

        if (effectData.parentToTarget && target != null)
        {
            effect.transform.SetParent(target.transform);
        }

        StartCoroutine(DestroyAfterTime(effect, effectData.effectLifetime));
        return effect;
    }

    private IEnumerator DestroyAfterTime(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        if (obj != null) Destroy(obj);
    }
}

public class BloodEffect : ImpactEffect
{
    public override void CreateEffect(Vector3 position, Vector3 normal, GameObject target = null)
    {
        if (GlobalReference.Instance?.BloodSprayEffect == null) return;

        InstantiateEffect(GlobalReference.Instance.BloodSprayEffect, position, normal, target);
        PlayAudio(position);
    }
}

public class BulletHoleEffect : ImpactEffect
{
    public override void CreateEffect(Vector3 position, Vector3 normal, GameObject target = null)
    {
        if (GlobalReference.Instance?.bulletImpactEffectPrefab == null) return;

        InstantiateEffect(GlobalReference.Instance.bulletImpactEffectPrefab, position, normal, target);
        PlayAudio(position);
    }
}

public class ExplosionEffect : ImpactEffect
{
    [SerializeField] private VisualEffect explosionVFX;

    public override void CreateEffect(Vector3 position, Vector3 normal, GameObject target = null)
    {
        if (explosionVFX != null)
        {
            var vfx = Instantiate(explosionVFX, position, Quaternion.LookRotation(normal));

            // Don't parent VFX to moving objects
            if (effectData.parentToTarget && target != null &&
                (target.GetComponent<Rigidbody>()?.isKinematic ?? true))
            {
                vfx.transform.SetParent(target.transform);
            }

            StartCoroutine(DestroyVFXAfterPlay(vfx.gameObject));
        }

        PlayAudio(position);
    }

    private IEnumerator DestroyVFXAfterPlay(GameObject vfxObject)
    {
        yield return new WaitForSeconds(3f);
        if (vfxObject != null) Destroy(vfxObject);
    }
}

#endregion Effect Classes

#region Collision Handlers

public abstract class CollisionHandler
{
    protected IProjectile projectile;

    public CollisionHandler(IProjectile projectile)
    {
        this.projectile = projectile;
    }

    public abstract bool CanHandle(string tag);

    public abstract void HandleCollision(GameObject target, ImpactData impactData);
}

public class EnemyCollisionHandler : CollisionHandler
{
    private readonly BloodEffect bloodEffect;
    private readonly ExplosionEffect explosionEffect;

    public EnemyCollisionHandler(IProjectile projectile) : base(projectile)
    {
        // Initialize effects - in a real implementation, you'd inject these dependencies
        bloodEffect = new GameObject("BloodEffect").AddComponent<BloodEffect>();
        explosionEffect = new GameObject("ExplosionEffect").AddComponent<ExplosionEffect>();
    }

    public override bool CanHandle(string tag)
    {
        return tag == "Enemy";
    }

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        var enemy = target.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogWarning($"Enemy tagged object {target.name} doesn't have Enemy component!");
            return;
        }

        if (!enemy.isDead)
        {
            var damageInfo = new DamageInfo(
                projectile.Damage,
                impactData.Point,
                -impactData.Normal,
                (projectile as MonoBehaviour)?.gameObject,
                DamageType.Bullet
            );

            if (enemy is IDamageable damageable)
            {
                damageable.TakeDamage(projectile.Damage, damageInfo);
            }
            else
            {
                enemy.TakeDamage(projectile.Damage);
            }

            Debug.Log($"Hit enemy {target.name} for {projectile.Damage} damage!");

            // Create effects
            bloodEffect.CreateEffect(impactData.Point, impactData.Normal, target);
            explosionEffect.CreateEffect(impactData.Point, impactData.Normal, target);
        }

        // Disable collider if enemy is dead
        if (enemy.isDead)
        {
            var collider = target.GetComponent<CapsuleCollider>();
            if (collider != null) collider.enabled = false;
        }
    }
}

public class PlayerCollisionHandler : CollisionHandler
{
    private readonly BloodEffect bloodEffect;

    public PlayerCollisionHandler(IProjectile projectile) : base(projectile)
    {
        bloodEffect = new GameObject("BloodEffect").AddComponent<BloodEffect>();
    }

    public override bool CanHandle(string tag)
    {
        return tag == "Player";
    }

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        //var player = target.GetComponent<Player>();
        //if (player != null && !player.IsDead)
        //{
        //    var damageInfo = new DamageInfo(
        //        projectile.Damage,
        //        impactData.Point,
        //        -impactData.Normal,
        //        (projectile as MonoBehaviour)?.gameObject,
        //        DamageType.Bullet
        //    );

        //    player.TakeDamage(projectile.Damage, damageInfo);
        //    bloodEffect.CreateEffect(impactData.Point, impactData.Normal, target);

        //    Debug.Log($"Hit player for {projectile.Damage} damage!");
        //}
    }
}

public class EnvironmentCollisionHandler : CollisionHandler
{
    private readonly BulletHoleEffect bulletHoleEffect;

    public EnvironmentCollisionHandler(IProjectile projectile) : base(projectile)
    {
        bulletHoleEffect = new GameObject("BulletHoleEffect").AddComponent<BulletHoleEffect>();
    }

    public override bool CanHandle(string tag)
    {
        return tag == "Wall" || tag == "Target" || tag == "Environment";
    }

    public override void HandleCollision(GameObject target, ImpactData impactData)
    {
        bulletHoleEffect.CreateEffect(impactData.Point, impactData.Normal, target);
        Debug.Log($"Hit {target.tag}: {target.name}");
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

    // IProjectile implementation
    public float Speed => config.speed;

    public int Damage => config.damage;
    public bool IsActive { get; protected set; } = true;

    public event Action<IProjectile, Collision> OnImpact;

    protected virtual void Awake()
    {
        InitializeComponents();
        InitializeCollisionHandlers();
    }

    protected virtual void Start()
    {
        Launch(transform.forward, config.speed);
        StartCoroutine(DestroyAfterLifetime());
    }

    protected virtual void InitializeComponents()
    {
        projectileRigidbody = GetComponent<Rigidbody>();
        if (projectileRigidbody == null)
        {
            projectileRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        ConfigureRigidbody();
    }

    protected virtual void ConfigureRigidbody()
    {
        projectileRigidbody.useGravity = config.useGravity;
        projectileRigidbody.linearDamping = config.drag; // Updated from linearDamping
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

    public virtual void Launch(Vector3 direction, float speed)
    {
        if (projectileRigidbody != null)
        {
            projectileRigidbody.linearVelocity = direction.normalized * speed; // Updated from linearVelocity
        }
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
        if (poolable != null)
        {
            poolable.ReturnToPool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!IsActive) return;

        OnImpact?.Invoke(this, collision);

        GameObject hitObject = collision.gameObject;
        ImpactData impactData = new ImpactData(collision.contacts[0], hitObject);

        HandleCollision(hitObject, impactData);
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
        if (config.canPenetrate && CanPenetrateTarget(target) && penetrationCount < config.maxPenetrations)
        {
            penetrationCount++;
            Debug.Log($"Projectile penetrated {target.name}. Penetrations: {penetrationCount}/{config.maxPenetrations}");
        }
        else
        {
            Deactivate();
        }
    }

    protected virtual bool CanPenetrateTarget(GameObject target)
    {
        return config.penetrableTags.Contains(target.tag);
    }

    private IEnumerator DestroyAfterLifetime()
    {
        yield return new WaitForSeconds(config.lifetime);

        if (gameObject != null)
        {
            Debug.Log("Projectile destroyed due to lifetime expiry");
            Deactivate();
        }
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
    [Header("Bullet Specific")]
    [SerializeField] private Volume globalVolume;

    private DepthOfField depthOfField;

    protected override void Awake()
    {
        base.Awake();

        if (globalVolume?.profile.TryGet(out depthOfField) == true)
        {
            // Cache depth of field if needed for special effects
        }
    }

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
    [SerializeField] private LayerMask explosionLayers = -1;

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
                int explosionDamage = Mathf.RoundToInt(config.damage * damageMultiplier);

                var damageInfo = new DamageInfo(
                    explosionDamage,
                    hitCollider.transform.position,
                    (hitCollider.transform.position - transform.position).normalized,
                    gameObject,
                    DamageType.Explosion
                );

                damageable.TakeDamage(explosionDamage, damageInfo);
            }

            var rigidbody = hitCollider.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }
        }

        // Create explosion effect
        var explosionEffect = GetComponent<ExplosionEffect>();
        explosionEffect?.CreateEffect(transform.position, Vector3.up);

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

        // Additional bullet-specific logic - Invoke events properly
        BulletImpactEvents.Instance.InvokeEnemyHit(impactData.Point, projectile.Damage);
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

        // Additional bullet-specific logic
        BulletImpactEvents.Instance.InvokePlayerHit(impactData.Point, projectile.Damage);
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

        // Additional bullet-specific logic
        if (target.CompareTag("Wall"))
        {
            BulletImpactEvents.Instance.InvokeWallHit(impactData.Point);
        }
        else if (target.CompareTag("Target"))
        {
            BulletImpactEvents.Instance.InvokeTargetHit(impactData.Point);
        }
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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InvokeEnemyHit(Vector3 position, int damage)
    {
        OnEnemyHit?.Invoke(position, damage);
    }

    public void InvokePlayerHit(Vector3 position, int damage)
    {
        OnPlayerHit?.Invoke(position, damage);
    }

    public void InvokeWallHit(Vector3 position)
    {
        OnWallHit?.Invoke(position);
    }

    public void InvokeTargetHit(Vector3 position)
    {
        OnTargetHit?.Invoke(position);
    }
}

#endregion Events

public enum ProjectileType
{
    Bullet,
    Grenade,
    Rocket
}