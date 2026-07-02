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

    [Header("Generic Weapon Sounds")]
    public AudioClip emptyGunClick;

    public AudioClip enemyHitmarker;
    public AudioClip MagDrop;

    // Shell loading sounds — generic pool, not per-weapon
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

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    #region Weapon Sounds

    // WeaponBase passes weaponData.shootSound directly — no switch needed
    public void PlayShootingSound(AudioClip clip)
    {
        if (clip != null && weaponChannel != null)
            weaponChannel.PlayOneShot(clip);
    }

    // WeaponBase passes weaponData.reloadSound directly
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

    // WeaponBase passes weaponData.cycleSounds[] directly
    public void PlayCycleSound(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || weaponChannel == null) return;
        int randomIndex = Random.Range(0, clips.Length);
        if (clips[randomIndex] != null)
            weaponChannel.PlayOneShot(clips[randomIndex]);
    }

    // Generic pool — not per-weapon
    public void PlayShellLoadSound()
    {
        if (ShellLoadSounds == null || ShellLoadSounds.Length == 0 || weaponChannel == null) return;
        int randomIndex = Random.Range(0, ShellLoadSounds.Length);
        if (ShellLoadSounds[randomIndex] != null)
            weaponChannel.PlayOneShot(ShellLoadSounds[randomIndex]);
    }

    public void PlayEmptyGunSound()
    {
        if (emptyGunClick != null && weaponChannel != null)
            weaponChannel.PlayOneShot(emptyGunClick);
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

    #region Zombie Sounds

    //public void PlayZombieDeathSound()
    //{ if (zombieDeath != null) entityChannel.PlayOneShot(zombieDeath); }

    //public void PlayZombieWalkSound()
    //{ if (zombieWalk != null) entityChannel.PlayOneShot(zombieWalk); }

    //public void PlayZombieAttackSound()
    //{ if (zombieAttack != null) entityChannel.PlayOneShot(zombieAttack); }

    //public void PlayZombieChaseSound()
    //{ if (zombieChase != null) entityChannel.PlayOneShot(zombieChase); }

    //public void PlayZombieHurtSound()
    //{ if (zombieHurt != null) entityChannel.PlayOneShot(zombieHurt); }

    #endregion Zombie Sounds

    #region Player Sounds

    public void PlayPlayerHurtSound()
    { if (playerHurt != null) entityChannel.PlayOneShot(playerHurt); }

    public void PlayPlayerDieSound()
    { if (playerDie != null) entityChannel.PlayOneShot(playerDie); }

    public void PlayArmorBreakSound()
    { if (armorBreak != null) playerChannel.PlayOneShot(armorBreak); }

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