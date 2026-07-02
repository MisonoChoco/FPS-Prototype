using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieAudioManager : MonoBehaviour
{
    public static ZombieAudioManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxSimultaneousGrowls = 3;

    [SerializeField] private int maxSimultaneousChases = 2;
    [SerializeField] private int maxSimultaneousWalks = 3;
    [SerializeField] private float growlInterval = 3f;
    [SerializeField] private float chaseInterval = 4f;
    [SerializeField] private float walkInterval = 2.5f;
    [SerializeField] private float maxAudibleDistance = 25f;

    private List<ZombieAudio> registeredZombies = new();
    private Transform player;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        StartCoroutine(GrowlCycle());
        StartCoroutine(ChaseCycle());
        StartCoroutine(WalkCycle());
    }

    public void Register(ZombieAudio zombie)
    {
        if (!registeredZombies.Contains(zombie))
            registeredZombies.Add(zombie);
    }

    public void Unregister(ZombieAudio zombie) =>
        registeredZombies.Remove(zombie);

    private List<ZombieAudio> GetSortedByDistance()
    {
        if (player == null) return registeredZombies;
        var sorted = new List<ZombieAudio>(registeredZombies);
        sorted.Sort((a, b) =>
            Vector3.Distance(a.transform.position, player.position)
            .CompareTo(Vector3.Distance(b.transform.position, player.position)));
        return sorted;
    }

    private IEnumerator GrowlCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(growlInterval);
            PlayForClosest(
                z => !z.IsDead && !z.IsChasing,
                z => z.PlayGrowl(),
                maxSimultaneousGrowls);
        }
    }

    private IEnumerator ChaseCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(chaseInterval);
            PlayForClosest(
                z => !z.IsDead && z.IsChasing,
                z => z.PlayChaseManaged(),
                maxSimultaneousChases);
        }
    }

    private IEnumerator WalkCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(walkInterval);
            PlayForClosest(
                z => !z.IsDead && z.IsWalking && !z.IsChasing,
                z => z.PlayWalkManaged(),
                maxSimultaneousWalks);
        }
    }

    private void PlayForClosest(
        System.Func<ZombieAudio, bool> filter,
        System.Action<ZombieAudio> playAction,
        int maxCount)
    {
        if (player == null) return;

        int played = 0;
        foreach (var zombie in GetSortedByDistance())
        {
            if (zombie == null) continue;
            if (!filter(zombie)) continue;

            float dist = Vector3.Distance(zombie.transform.position, player.position);
            if (dist > maxAudibleDistance) break;

            if (played < maxCount)
            {
                playAction(zombie);
                played++;
            }
        }
    }
}