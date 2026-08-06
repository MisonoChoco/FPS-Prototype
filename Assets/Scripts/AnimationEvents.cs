using UnityEngine;

public class WeaponAnimationEvents : MonoBehaviour
{
    [SerializeField] private WeaponBase weaponBase;

    private void Awake()
    {
        if (weaponBase == null)
            weaponBase = GetComponentInParent<WeaponBase>();
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        SoundManager.Instance?.weaponChannel?.PlayOneShot(clip);
    }

    //reload done, ammo count immediately
    public void ReloadCompleted()
    {
        weaponBase?.SignalReloadComplete();
    }

    // ── Reload audio events ──────────────────────────────────────
    public void ReloadRaise()
    {
        PlayClip(weaponBase?.Data?.reloadRaiseSound);
    }

    public void ReloadRattle()
    {
        PlayClip(weaponBase?.Data?.reloadRattleSound);
    }

    public void ReloadEnd()
    {
        PlayClip(weaponBase?.Data?.reloadEndSound);
    }

    public void TacMagOut()
    {
        PlayClip(weaponBase?.Data?.magOutSound);
    }

    public void TacMagIn()
    {
        PlayClip(weaponBase?.Data?.magInSound);
    }

    public void EmptyMagOut()
    {
        PlayClip(weaponBase?.Data?.emptyMagOutSound);
    }

    public void EmptyMagIn()
    {
        PlayClip(weaponBase?.Data?.emptyMagInSound);
    }

    public void EmptyRaise()
    {
        PlayClip(weaponBase?.Data?.emptyReloadRaiseSound);
    }

    public void EmptyRattle()
    {
        PlayClip(weaponBase?.Data?.emptyReloadRattleSound);
    }

    public void EmptyEnd()
    {
        PlayClip(weaponBase?.Data?.emptyReloadEndSound);
    }

    public void EmptyMagHit()
    {
        PlayClip(weaponBase?.Data?.emptyMagHitSound);
    }

    public void MagHit()
    {
        PlayClip(weaponBase?.Data?.magHitSound);
    }

    public void BoltChamber()
    {
        PlayClip(weaponBase?.Data?.boltChamberSound);
    }

    // ── HK / bolt-action events ──────────────────────────────────

    public void BoltBack()
    {
        PlayClip(weaponBase?.Data?.boltBackSound);
    }

    public void BoltForward()
    {
        PlayClip(weaponBase?.Data?.boltForwardSound);
    }

    public void BoltEnd()
    {
        PlayClip(weaponBase?.Data?.boltEndSound);
    }

    public void EjectClick()
    {
        PlayClip(weaponBase?.Data?.ejectClickSound);
    }

    //public void EjectShell()
    //{
    //    weaponBase?.EjectShellCasing();
    //}

    // ────────────────────────────────────────

    public void OnEquipComplete()
    {
        weaponBase?.EquipCompleted();
    }

    public void OnSwitchUpComplete()
    {
        weaponBase?.EquipCompleted();
    }

    public void OnSwitchDownComplete()
    {
        weaponBase?.SwitchDownCompleted();
    }

    public void SwitchUpAudio()
    {
        PlayClip(weaponBase?.Data?.switchUpSound);
    }

    public void SwitchDownAudio()
    {
        PlayClip(weaponBase?.Data?.switchDownSound);
    }

    // ────────────────────────────────────────

    // Called at the moment shell enters chamber during RELOAD_LOOP
    public void OnReloadLoopComplete()
    {
        weaponBase?.SignalReloadLoopComplete();
    }

    // Called at the moment shell enters chamber during RELOAD_FINISH
    public void OnReloadFinishComplete()
    {
        weaponBase?.SignalReloadLoopComplete(); // same signal, finish loop
    }

    // Called when bolt/pump action completes after firing
    public void OnRechamberComplete()
    {
        weaponBase?.SignalRechamberComplete();
    }
}