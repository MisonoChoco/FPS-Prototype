using UnityEngine;

public class ZombieAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [Header("Sounds")]
    [SerializeField] private AudioClip[] growlSounds;

    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip chaseSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip walkSound;

    public bool IsDead { get; private set; } = false;
    public bool IsChasing { get; private set; } = false;
    public bool IsWalking { get; private set; } = false;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 25f;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
    }

    private void OnEnable() => ZombieAudioManager.Instance?.Register(this);

    private void OnDisable() => ZombieAudioManager.Instance?.Unregister(this);

    // ── Called by ZombieAudioManager only ────────────────────────
    public void PlayGrowl()
    {
        if (growlSounds == null || growlSounds.Length == 0 || audioSource.isPlaying) return;
        audioSource.PlayOneShot(growlSounds[Random.Range(0, growlSounds.Length)]);
    }

    public void PlayChaseManaged()
    {
        if (chaseSound != null && !audioSource.isPlaying)
            audioSource.PlayOneShot(chaseSound);
    }

    public void PlayWalkManaged()
    {
        if (walkSound != null && !audioSource.isPlaying)
            audioSource.PlayOneShot(walkSound);
    }

    // ── State flags — set by Enemy, read by manager ───────────────
    public void RequestChase() => IsChasing = true;

    public void StopChase() => IsChasing = false;

    public void RequestWalk() => IsWalking = true;

    public void StopWalk() => IsWalking = false;

    // ── Always play directly — event-driven, not ambient ─────────
    public void PlayAttack() => PlayClip(attackSound);

    public void PlayHurt() => PlayClip(hurtSound);

    public void PlayDeath()
    { IsDead = true; PlayClip(deathSound); }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip);
    }
}