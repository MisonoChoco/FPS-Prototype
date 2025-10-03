using UnityEngine;

/// <summary>
/// Attach this to any GameObject to test if events are firing
/// </summary>
public class EventChainDebugger : MonoBehaviour
{
    private void Start()
    {
        // Wait a frame to ensure all singletons are initialized
        Invoke(nameof(SubscribeToEvents), 0.1f);
    }

    private void SubscribeToEvents()
    {
        if (BulletImpactEvents.Instance == null)
        {
            Debug.LogError("❌ BulletImpactEvents.Instance is NULL!");
            return;
        }

        Debug.Log("✅ BulletImpactEvents.Instance found, subscribing to events...");

        BulletImpactEvents.Instance.OnEnemyHit += TestEnemyHit;
        BulletImpactEvents.Instance.OnPlayerHit += TestPlayerHit;
        BulletImpactEvents.Instance.OnWallHit += TestWallHit;
        BulletImpactEvents.Instance.OnTargetHit += TestTargetHit;

        Debug.Log("✅ Successfully subscribed to all events!");
    }

    private void TestEnemyHit(Vector3 position, int damage)
    {
        Debug.Log($"🎯 ENEMY HIT EVENT FIRED! Position: {position}, Damage: {damage}");
    }

    private void TestPlayerHit(Vector3 position, int damage)
    {
        Debug.Log($"🎯 PLAYER HIT EVENT FIRED! Position: {position}, Damage: {damage}");
    }

    private void TestWallHit(Vector3 position)
    {
        Debug.Log($"🎯 WALL HIT EVENT FIRED! Position: {position}");
    }

    private void TestTargetHit(Vector3 position)
    {
        Debug.Log($"🎯 TARGET HIT EVENT FIRED! Position: {position}");
    }

    private void OnDestroy()
    {
        if (BulletImpactEvents.Instance != null)
        {
            BulletImpactEvents.Instance.OnEnemyHit -= TestEnemyHit;
            BulletImpactEvents.Instance.OnPlayerHit -= TestPlayerHit;
            BulletImpactEvents.Instance.OnWallHit -= TestWallHit;
            BulletImpactEvents.Instance.OnTargetHit -= TestTargetHit;
        }
    }
}