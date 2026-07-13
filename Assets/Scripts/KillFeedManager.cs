using UnityEngine;

public class KillFeedManager : MonoBehaviour
{
    public static KillFeedManager Instance { get; private set; }

    [SerializeField] private GameObject killFeedEntryPrefab;
    [SerializeField] private Transform container;
    [SerializeField] private int maxEntries = 5;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddKill(string enemyName)
    {
        // Remove oldest if over limit
        if (container.childCount >= maxEntries)
            Destroy(container.GetChild(0).gameObject);

        GameObject entry = Instantiate(killFeedEntryPrefab, container);
        entry.GetComponent<KillFeedEntry>().Show(enemyName);
    }
}