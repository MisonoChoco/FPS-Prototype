using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; set; }

    [Header("Audio Channels")]
    public AudioSource weaponChannel;

    public AudioSource entityChannel;
    public AudioSource throwablesChannel;
    public AudioSource musicChannel;
    public AudioSource playerChannel;

    [Header("Weapon Shot Pool")]
    [SerializeField] private int weaponShotPoolSize = 7;

    private AudioSource[] weaponShotPool;
    private int currentShotIndex = 0;

    [Header("Generic Weapon Sounds")]
    public AudioClip emptyGunClick;

    public AudioClip enemyHitmarker;
    public AudioClip MagDrop;

    [Header("Shell Loading")]
    public AudioClip[] ShellLoadSounds;

    [Header("Player Sounds")]
    public AudioClip playerHurt;

    public AudioClip playerDie;
    public AudioClip armorBreak;
    public AudioClip killFeedbackSound;

    [Header("Music")]
    public AudioClip gameOverMusic;

    [Header("Throwables")]
    public AudioClip grenadeSound;

    public AudioClip smokeSound;

    [Header("Armor")]
    public AudioClip armorHitSound;

    public AudioClip armorBreakSound;
    public AudioClip selfArmorBreak;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitWeaponShotPool();
    }

    private void InitWeaponShotPool()
    {
        weaponShotPool = new AudioSource[weaponShotPoolSize];
        for (int i = 0; i < weaponShotPoolSize; i++)
        {
            weaponShotPool[i] = gameObject.AddComponent<AudioSource>();
            weaponShotPool[i].playOnAwake = false;
            weaponShotPool[i].volume = weaponChannel != null ? weaponChannel.volume : 1f;
            weaponShotPool[i].outputAudioMixerGroup = weaponChannel?.outputAudioMixerGroup;
        }
    }

    #region Weapon Sounds

    public void PlayShootingSound(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource source = weaponShotPool[currentShotIndex];
        currentShotIndex = (currentShotIndex + 1) % weaponShotPoolSize;

        source.clip = clip;
        source.Play();
    }

    public void PlayReloadSound(AudioClip clip)
    {
        if (clip != null && weaponChannel != null)
            weaponChannel.PlayOneShot(clip);
    }

    public void StopReloadSound()
    {
        if (weaponChannel != null && weaponChannel.isPlaying)
            weaponChannel.Stop();
    }

    public void PlayCycleSound(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || weaponChannel == null) return;
        int randomIndex = Random.Range(0, clips.Length);
        if (clips[randomIndex] != null)
            weaponChannel.PlayOneShot(clips[randomIndex]);
    }

    public void PlayShellLoadSound()
    {
        if (ShellLoadSounds == null || ShellLoadSounds.Length == 0 || weaponChannel == null) return;
        int randomIndex = Random.Range(0, ShellLoadSounds.Length);
        if (ShellLoadSounds[randomIndex] != null)
            weaponChannel.PlayOneShot(ShellLoadSounds[randomIndex]);
    }

    public void PlayKillFeedback()
    {
        if (killFeedbackSound != null && playerChannel != null)
            playerChannel.PlayOneShot(killFeedbackSound);
    }

    public void PlayEnemyHitmarker()
    {
        if (enemyHitmarker != null && playerChannel != null)
            playerChannel.PlayOneShot(enemyHitmarker);
    }

    public void PlayEmptyGunSound()
    {
        if (emptyGunClick != null && weaponChannel != null)
            weaponChannel.PlayOneShot(emptyGunClick);
    }

    #endregion Weapon Sounds

    #region Throwables

    public void PlayGrenadeSound()
    {
        if (grenadeSound != null && throwablesChannel != null)
            throwablesChannel.PlayOneShot(grenadeSound);
    }

    public void PlaySmokeSound()
    {
        if (smokeSound != null && throwablesChannel != null)
            throwablesChannel.PlayOneShot(smokeSound);
    }

    #endregion Throwables

    #region Player Sounds

    public void PlayPlayerHurtSound()
    { if (playerHurt != null) entityChannel.PlayOneShot(playerHurt); }

    public void PlayPlayerDieSound()
    { if (playerDie != null) entityChannel.PlayOneShot(playerDie); }

    public void PlaySelfArmorBreakSound()
    { if (armorBreak != null) playerChannel.PlayOneShot(selfArmorBreak); }

    public void PlayArmorHitSound()
    {
        if (armorHitSound != null && playerChannel != null)
            playerChannel.PlayOneShot(armorHitSound);
    }

    public void PlayArmorBreakSound()
    {
        if (armorBreakSound != null && playerChannel != null)
            playerChannel.PlayOneShot(armorBreakSound);
    }

    #endregion Player Sounds

    #region Music

    public void PlayGameOverMusic()
    {
        if (gameOverMusic != null && musicChannel != null)
            musicChannel.PlayOneShot(gameOverMusic);
    }

    public void StopMusic()
    {
        if (musicChannel != null && musicChannel.isPlaying)
            musicChannel.Stop();
    }

    #endregion Music
}