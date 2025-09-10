using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#region Unified Factory with Pooling

public class ProjectileFactory : MonoBehaviour
{
    [Header("Projectile Configuration")]
    [SerializeField] private List<ProjectilePoolData> projectileConfigs;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Singleton
    public static ProjectileFactory Instance { get; private set; }

    // Pooling system
    private Dictionary<ProjectileType, Queue<GameObject>> availablePools;

    private Dictionary<ProjectileType, List<GameObject>> activePools;
    private Dictionary<ProjectileType, GameObject> prefabLookup;
    private Dictionary<ProjectileType, ProjectilePoolData> configLookup;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingleton();
        InitializePoolingSystem();
        StartCleanupRoutine();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    #endregion Unity Lifecycle

    #region Initialization

    private void SetupSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("ProjectileFactory initialized as singleton");
        }
        else
        {
            DebugLog("Duplicate ProjectileFactory found, destroying...");
            Destroy(gameObject);
        }
    }

    private void InitializePoolingSystem()
    {
        availablePools = new Dictionary<ProjectileType, Queue<GameObject>>();
        activePools = new Dictionary<ProjectileType, List<GameObject>>();
        prefabLookup = new Dictionary<ProjectileType, GameObject>();
        configLookup = new Dictionary<ProjectileType, ProjectilePoolData>();

        foreach (var config in projectileConfigs)
        {
            if (config.prefab == null)
            {
                Debug.LogError($"Prefab is null for ProjectileType: {config.type}");
                continue;
            }

            // Setup lookups
            prefabLookup[config.type] = config.prefab;
            configLookup[config.type] = config;

            // Initialize pools
            availablePools[config.type] = new Queue<GameObject>();
            activePools[config.type] = new List<GameObject>();

            // Pre-instantiate objects
            CreateInitialPool(config);

            DebugLog($"Initialized pool for {config.type}: {config.initialPoolSize} objects");
        }
    }

    private void CreateInitialPool(ProjectilePoolData config)
    {
        var pool = availablePools[config.type];

        for (int i = 0; i < config.initialPoolSize; i++)
        {
            GameObject obj = CreatePooledObject(config.prefab, config.type);
            pool.Enqueue(obj);
        }
    }

    private GameObject CreatePooledObject(GameObject prefab, ProjectileType type)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.name = $"{type}_{obj.GetInstanceID()}";
        obj.SetActive(false);

        // Ensure the object can be returned to pool
        var poolable = obj.GetComponent<PoolableProjectile>();
        if (poolable == null)
        {
            poolable = obj.AddComponent<PoolableProjectile>();
        }
        poolable.Initialize(type, this);

        return obj;
    }

    #endregion Initialization

    #region Factory Methods

    /// <summary>
    /// Creates a projectile at the specified position and rotation
    /// </summary>
    public IProjectile CreateProjectile(ProjectileType type, Vector3 position, Quaternion rotation)
    {
        GameObject obj = GetPooledObject(type);
        if (obj == null) return null;

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        var projectile = obj.GetComponent<IProjectile>();
        if (projectile != null)
        {
            // Reset projectile state
            if (projectile is ProjectileBase baseProjectile)
            {
                baseProjectile.ResetState();
            }
        }

        DebugLog($"Created {type} projectile at {position}");
        return projectile;
    }

    /// <summary>
    /// Creates a projectile with specific launch parameters
    /// </summary>
    public IProjectile CreateAndLaunchProjectile(ProjectileType type, Vector3 position, Quaternion rotation, Vector3 direction, float speed = -1f)
    {
        var projectile = CreateProjectile(type, position, rotation);
        if (projectile != null)
        {
            float launchSpeed = speed > 0 ? speed : projectile.Speed;
            projectile.Launch(direction, launchSpeed);
        }
        return projectile;
    }

    /// <summary>
    /// Generic method to create specific projectile types
    /// </summary>
    public T CreateProjectile<T>(Vector3 position, Quaternion rotation) where T : ProjectileBase
    {
        ProjectileType type = GetProjectileTypeFromClass<T>();
        var projectile = CreateProjectile(type, position, rotation);
        return projectile as T;
    }

    #endregion Factory Methods

    #region Pooling System

    private GameObject GetPooledObject(ProjectileType type)
    {
        if (!availablePools.ContainsKey(type))
        {
            Debug.LogError($"No pool configured for ProjectileType: {type}");
            return null;
        }

        var availablePool = availablePools[type];
        var activePool = activePools[type];
        var config = configLookup[type];

        GameObject obj = null;

        // Try to get from available pool
        while (availablePool.Count > 0)
        {
            obj = availablePool.Dequeue();
            if (obj != null) break;
        }

        // If no available objects, create new one if allowed
        if (obj == null)
        {
            if (config.allowExpansion && activePool.Count < config.maxPoolSize)
            {
                obj = CreatePooledObject(prefabLookup[type], type);
                DebugLog($"Pool expanded for {type}. Active count: {activePool.Count + 1}");
            }
            else
            {
                Debug.LogWarning($"Pool limit reached for {type}. Max: {config.maxPoolSize}");
                return null;
            }
        }

        // Move to active pool
        activePool.Add(obj);
        return obj;
    }

    /// <summary>
    /// Returns a projectile to the pool (called by PoolableProjectile component)
    /// </summary>
    public void ReturnToPool(GameObject obj, ProjectileType type)
    {
        if (!activePools.ContainsKey(type)) return;

        var activePool = activePools[type];
        var availablePool = availablePools[type];

        // Remove from active pool
        activePool.Remove(obj);

        // Reset object state
        obj.SetActive(false);
        obj.transform.SetParent(transform);

        // Add to available pool
        availablePool.Enqueue(obj);

        DebugLog($"Returned {type} to pool. Available: {availablePool.Count}, Active: {activePool.Count}");
    }

    #endregion Pooling System

    #region Utility Methods

    private ProjectileType GetProjectileTypeFromClass<T>() where T : ProjectileBase
    {
        if (typeof(T) == typeof(Bullet)) return ProjectileType.Bullet;
        if (typeof(T) == typeof(Grenade)) return ProjectileType.Grenade;
        // Add more mappings as needed

        Debug.LogError($"No ProjectileType mapping found for class: {typeof(T)}");
        return ProjectileType.Bullet; // Default fallback
    }

    /// <summary>
    /// Get current pool statistics
    /// </summary>
    public PoolStats GetPoolStats(ProjectileType type)
    {
        if (!availablePools.ContainsKey(type)) return null;

        return new PoolStats
        {
            Type = type,
            Available = availablePools[type].Count,
            Active = activePools[type].Count,
            MaxSize = configLookup[type].maxPoolSize
        };
    }

    /// <summary>
    /// Get all pool statistics
    /// </summary>
    public List<PoolStats> GetAllPoolStats()
    {
        var stats = new List<PoolStats>();
        foreach (var type in availablePools.Keys)
        {
            stats.Add(GetPoolStats(type));
        }
        return stats;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ProjectileFactory] {message}");
        }
    }

    #endregion Utility Methods

    #region Cleanup System

    private void StartCleanupRoutine()
    {
        foreach (var config in projectileConfigs)
        {
            if (config.autoCleanup)
            {
                InvokeRepeating(nameof(CleanupUnusedObjects), config.cleanupInterval, config.cleanupInterval);
            }
        }
    }

    private void CleanupUnusedObjects()
    {
        foreach (var config in projectileConfigs)
        {
            if (!config.autoCleanup) continue;

            var availablePool = availablePools[config.type];
            var targetSize = Mathf.Max(config.initialPoolSize, availablePools[config.type].Count / 2);

            while (availablePool.Count > targetSize)
            {
                var obj = availablePool.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }
    }

    #endregion Cleanup System
}

#endregion Unified Factory with Pooling

#region Supporting Classes

/// <summary>
/// Component that handles returning projectiles to the pool
/// </summary>
public class PoolableProjectile : MonoBehaviour
{
    private ProjectileType projectileType;
    private ProjectileFactory factory;

    public void Initialize(ProjectileType type, ProjectileFactory factory)
    {
        this.projectileType = type;
        this.factory = factory;
    }

    public void ReturnToPool()
    {
        if (factory != null)
        {
            factory.ReturnToPool(gameObject, projectileType);
        }
    }
}

/// <summary>
/// Pool statistics for debugging and monitoring
/// </summary>
public class PoolStats
{
    public ProjectileType Type;
    public int Available;
    public int Active;
    public int MaxSize;

    public override string ToString()
    {
        return $"{Type}: {Active}/{MaxSize} active, {Available} available";
    }
}

#endregion Supporting Classes

[System.Serializable]
public class ProjectilePoolData
{
    [Header("Prefab Configuration")]
    public ProjectileType type;

    public GameObject prefab;

    [Header("Pool Settings")]
    public int initialPoolSize = 20;

    public int maxPoolSize = 100;
    public bool allowExpansion = true;

    [Header("Auto Cleanup")]
    public bool autoCleanup = true;

    public float cleanupInterval = 30f; // Clean unused objects every 30 seconds
}