using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; set; }

    [Header("Audio Channels")]
    public AudioSource ShootingChannel;

    public AudioSource EmptyShooting;
    public AudioSource CyclingChannel;
    public AudioSource throwablesChannel;
    public AudioSource zombieChannel;
    public AudioSource zombieChannel2;
    public AudioSource playerChannel;

    [Header("Weapon Shooting Sounds")]
    public AudioClip AK47Shot;

    public AudioClip M1911Shot;
    public AudioClip ShotgunShot;
    public AudioClip SniperShot;

    [Header("Weapon Reload Sounds (Magazine-Fed)")]
    public AudioSource Reloading_AK;

    public AudioSource Reloading_M1911;
    public AudioSource Reloading_Sniper;

    [Header("Shell Loading Sounds (Shotgun)")]
    public AudioClip[] ShellLoadSounds; // Array for randomized shell insertion sounds

    [Header("Cycle Sounds (Pump/Bolt Action)")]
    public AudioClip[] PumpShotgunSounds;  // Array for pump action cycling

    public AudioClip[] BoltActionSounds;   // Array for bolt action cycling

    [Header("Other Sounds")]
    public AudioClip grenadeSound;

    public AudioClip zombieDeath;
    public AudioClip zombieWalk;
    public AudioClip zombieAttack;
    public AudioClip zombieChase;
    public AudioClip zombieHurt;
    public AudioClip playerHurt;
    public AudioClip playerDie;
    public AudioClip gameOverMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    #region Weapon Sounds

    public void PlayShootingSound(Weapon.WeaponModel weapon)
    {
        switch (weapon)
        {
            case Weapon.WeaponModel.AK47:
                ShootingChannel.PlayOneShot(AK47Shot);
                break;

            case Weapon.WeaponModel.HandgunM1911:
                ShootingChannel.PlayOneShot(M1911Shot);
                break;

            case Weapon.WeaponModel.Shotgun:
                ShootingChannel.PlayOneShot(ShotgunShot);
                break;

            case Weapon.WeaponModel.SniperRifle:
                ShootingChannel.PlayOneShot(SniperShot);
                break;
        }
    }

    public void PlayReloadSound(Weapon.WeaponModel weapon)
    {
        switch (weapon)
        {
            case Weapon.WeaponModel.AK47:
                Reloading_AK.Play();
                break;

            case Weapon.WeaponModel.HandgunM1911:
                Reloading_M1911.Play();
                break;

            case Weapon.WeaponModel.SniperRifle:
                Reloading_Sniper.Play();
                break;

            case Weapon.WeaponModel.Shotgun:
                // Shotgun uses shell loading, not magazine reload
                break;
        }
    }

    public void StopReloadSound(Weapon.WeaponModel weapon)
    {
        switch (weapon)
        {
            case Weapon.WeaponModel.AK47:
                Reloading_AK.Stop();
                break;

            case Weapon.WeaponModel.HandgunM1911:
                Reloading_M1911.Stop();
                break;

            case Weapon.WeaponModel.SniperRifle:
                Reloading_Sniper.Stop();
                break;

            case Weapon.WeaponModel.Shotgun:
                // No magazine reload to stop for shotgun
                break;
        }
    }

    // NEW: Shell loading sound for shotguns
    public void PlayShellLoadSound()
    {
        if (ShellLoadSounds != null && ShellLoadSounds.Length > 0 && ShootingChannel != null)
        {
            int randomIndex = Random.Range(0, ShellLoadSounds.Length);
            ShootingChannel.PlayOneShot(ShellLoadSounds[randomIndex]);
        }
    }

    // NEW: Cycle sound for pump-action and bolt-action
    public void PlayCycleSound(Weapon.WeaponModel weapon)
    {
        AudioClip[] cycleSounds = null;

        switch (weapon)
        {
            case Weapon.WeaponModel.Shotgun:
                cycleSounds = PumpShotgunSounds;
                break;

            case Weapon.WeaponModel.SniperRifle:
                cycleSounds = BoltActionSounds;
                break;
        }

        if (cycleSounds != null && cycleSounds.Length > 0 && CyclingChannel != null)
        {
            int randomIndex = Random.Range(0, cycleSounds.Length);
            CyclingChannel.PlayOneShot(cycleSounds[randomIndex]);
        }
    }

    #endregion Weapon Sounds

    #region Other Sounds

    public void PlayGrenadeSound()
    {
        throwablesChannel.PlayOneShot(grenadeSound);
    }

    public void PlayZombieDeathSound()
    {
        zombieChannel.PlayOneShot(zombieDeath);
    }

    public void PlayZombieWalkSound()
    {
        zombieChannel.PlayOneShot(zombieWalk);
    }

    public void PlayZombieAttackSound()
    {
        zombieChannel2.PlayOneShot(zombieAttack);
    }

    public void PlayZombieChaseSound()
    {
        zombieChannel.PlayOneShot(zombieChase);
    }

    public void PlayZombieHurtSound()
    {
        zombieChannel2.PlayOneShot(zombieHurt);
    }

    public void PlayPlayerHurtSound()
    {
        playerChannel.PlayOneShot(playerHurt);
    }

    public void PlayPlayerDieSound()
    {
        playerChannel.PlayOneShot(playerDie);
    }

    public void PlayGameOverMusic()
    {
        playerChannel.PlayOneShot(gameOverMusic);
    }

    #endregion Other Sounds
}