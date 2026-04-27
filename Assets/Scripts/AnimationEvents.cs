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

    // ── Reload audio events ──────────────────────────────────────

    public void MagOut()
    {
        PlayClip(weaponBase?.Data?.magOutSound);
    }

    public void MagIn()
    {
        PlayClip(weaponBase?.Data?.magInSound);
        // Tactical reload completes here — mag is seated, bullet count updates
        weaponBase?.SignalReloadComplete();
    }

    public void BoltPull()
    {
        PlayClip(weaponBase?.Data?.boltPullSound);
        // Empty reload and last bullet reload complete here — round is chambered
        weaponBase?.SignalReloadComplete();
    }

    // ── HK / bolt-action events ──────────────────────────────────

    public void BoltBack()
    {
        PlayClip(weaponBase?.Data?.boltBackSound);
    }

    public void BoltForward()
    {
        PlayClip(weaponBase?.Data?.boltForwardSound);
        // For HK/bolt-action, completion is on bolt going forward, not pull
        weaponBase?.SignalReloadComplete();
    }
}