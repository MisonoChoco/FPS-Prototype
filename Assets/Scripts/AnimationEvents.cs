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

    public void ReloadEnd()
    {
        PlayClip(weaponBase?.Data?.reloadEndSound);
    }

    public void MagOut()
    {
        PlayClip(weaponBase?.Data?.magOutSound);
    }

    public void MagIn()
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
}