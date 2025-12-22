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

    [Header("Weapon Shooting Sounds")]
    public AudioClip AK47Shot;

    public AudioClip M1911Shot;
    public AudioClip ShotgunShot;
    public AudioClip SniperShot;
    public AudioClip emptyGunClick;
    public AudioClip enemyHitmarker;

    [Header("Weapon Reload Sounds")]
    public AudioClip AK47Reload;

    public AudioClip M1911Reload;
    public AudioClip SniperReload;
    public AudioClip MagDrop;

    [Header("Shell Loading & Cycling Sounds")]
    public AudioClip[] ShellLoadSounds;

    public AudioClip[] PumpShotgunSounds;
    public AudioClip[] BoltActionSounds;

    [Header("Throwables")]
    public AudioClip grenadeSound;

    [Header("Zombie Sounds")]
    public AudioClip zombieDeath;

    public AudioClip zombieWalk;
    public AudioClip zombieAttack;
    public AudioClip zombieChase;
    public AudioClip zombieHurt;

    [Header("Player Sounds")]
    public AudioClip playerHurt;

    public AudioClip playerDie;
    public AudioClip armorBreak;

    [Header("Music")]
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
        AudioClip clip = null;

        switch (weapon)
        {
            case Weapon.WeaponModel.AK47:
                clip = AK47Shot;
                break;

            case Weapon.WeaponModel.HandgunM1911:
                clip = M1911Shot;
                break;

            case Weapon.WeaponModel.Shotgun:
                clip = ShotgunShot;
                break;

            case Weapon.WeaponModel.SniperRifle:
                clip = SniperShot;
                break;
        }

        if (clip != null && weaponChannel != null)
        {
            weaponChannel.PlayOneShot(clip);
        }
    }

    public void PlayEmptyGunSound()
    {
        if (emptyGunClick != null && weaponChannel != null)
        {
            weaponChannel.PlayOneShot(emptyGunClick);
        }
    }

    public void PlayReloadSound(Weapon.WeaponModel weapon)
    {
        AudioClip clip = null;

        switch (weapon)
        {
            case Weapon.WeaponModel.AK47:
                clip = AK47Reload;
                break;

            case Weapon.WeaponModel.HandgunM1911:
                clip = M1911Reload;
                break;

            case Weapon.WeaponModel.SniperRifle:
                clip = SniperReload;
                break;

            case Weapon.WeaponModel.Shotgun:
                // Shotgun uses shell loading, not magazine reload
                return;
        }

        if (clip != null && weaponChannel != null)
        {
            weaponChannel.PlayOneShot(clip);
        }
    }

    public void StopReloadSound(Weapon.WeaponModel weapon)
    {
        if (weaponChannel != null && weaponChannel.isPlaying)
        {
            weaponChannel.Stop();
        }
    }

    public void PlayShellLoadSound()
    {
        if (ShellLoadSounds != null && ShellLoadSounds.Length > 0 && weaponChannel != null)
        {
            int randomIndex = Random.Range(0, ShellLoadSounds.Length);
            weaponChannel.PlayOneShot(ShellLoadSounds[randomIndex]);
        }
    }

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

        if (cycleSounds != null && cycleSounds.Length > 0 && weaponChannel != null)
        {
            int randomIndex = Random.Range(0, cycleSounds.Length);
            weaponChannel.PlayOneShot(cycleSounds[randomIndex]);
        }
    }

    public void PlayEnemyHitmarker()
    {
        if (enemyHitmarker != null)
        {
            weaponChannel.PlayOneShot(enemyHitmarker);
        }
    }

    #endregion Weapon Sounds

    #region Throwables

    public void PlayGrenadeSound()
    {
        if (grenadeSound != null && throwablesChannel != null)
        {
            throwablesChannel.PlayOneShot(grenadeSound);
        }
    }

    #endregion Throwables

    #region Zombie Sounds

    public void PlayZombieDeathSound()
    {
        if (zombieDeath != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(zombieDeath);
        }
    }

    public void PlayZombieWalkSound()
    {
        if (zombieWalk != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(zombieWalk);
        }
    }

    public void PlayZombieAttackSound()
    {
        if (zombieAttack != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(zombieAttack);
        }
    }

    public void PlayZombieChaseSound()
    {
        if (zombieChase != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(zombieChase);
        }
    }

    public void PlayZombieHurtSound()
    {
        if (zombieHurt != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(zombieHurt);
        }
    }

    #endregion Zombie Sounds

    #region Player Sounds

    public void PlayPlayerHurtSound()
    {
        if (playerHurt != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(playerHurt);
        }
    }

    public void PlayPlayerDieSound()
    {
        if (playerDie != null && entityChannel != null)
        {
            entityChannel.PlayOneShot(playerDie);
        }
    }

    // NEW: Armor break sound
    public void PlayArmorBreakSound()
    {
        if (armorBreak != null && playerChannel != null)
        {
            playerChannel.PlayOneShot(armorBreak);
        }
    }

    #endregion Player Sounds

    #region Music

    public void PlayGameOverMusic()
    {
        if (gameOverMusic != null && musicChannel != null)
        {
            musicChannel.PlayOneShot(gameOverMusic);
        }
    }

    public void StopMusic()
    {
        if (musicChannel != null && musicChannel.isPlaying)
        {
            musicChannel.Stop();
        }
    }

    #endregion Music
}