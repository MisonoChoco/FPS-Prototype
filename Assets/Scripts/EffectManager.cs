using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("Blood Effects")]
    public GameObject bloodSprayPrefab;
    public AudioClip bloodImpactSound;
    [Range(0f, 1f)] public float bloodVolume = 0.8f;

    [Header("Bullet Hole Effects")]
    public GameObject bulletHolePrefab;
    public AudioClip bulletImpactSound;
    [Range(0f, 1f)] public float bulletVolume = 0.6f;

    [Header("Explosion Effects")]
    public GameObject explosionPrefab;
    public VisualEffect explosionVFX;
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;

    [Header("Settings")]
    public float effectLifetime = 10f;
    public bool parentEffectsToTarget = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void CreateBloodEffect(Vector3 position, Vector3 normal, GameObject target = null)
    {
        // Try GlobalReference first, fallback to our prefab
        GameObject prefabToUse = GlobalReference.Instance?.BloodSprayEffect ?? bloodSprayPrefab;

        if (prefabToUse != null)
        {
            CreateEffect(prefabToUse, position, normal, target);
            PlayAudio(bloodImpactSound, position, bloodVolume);
        }
    }

    public void CreateBulletHoleEffect(Vector3 position, Vector3 normal, GameObject target = null)
    {
        // Try GlobalReference first, fallback to our prefab
        GameObject prefabToUse = GlobalReference.Instance?.bulletImpactEffectPrefab ?? bulletHolePrefab;

        if (prefabToUse != null)
        {
            CreateEffect(prefabToUse, position, normal, target);
            PlayAudio(bulletImpactSound, position, bulletVolume);
        }
    }

    public void CreateExplosionEffect(Vector3 position, Vector3 normal, GameObject target = null)
    {
        // Create GameObject-based explosion effect
        if (explosionPrefab != null)
        {
            CreateEffect(explosionPrefab, position, normal, target);
        }

        // Create VFX-based explosion effect
        if (explosionVFX != null)
        {
            var vfx = Instantiate(explosionVFX, position, Quaternion.LookRotation(normal));

            // Don't parent VFX to moving objects for better performance
            if (parentEffectsToTarget && target != null &&
                (target.GetComponent<Rigidbody>()?.isKinematic ?? true))
            {
                vfx.transform.SetParent(target.transform);
            }

            StartCoroutine(DestroyAfterTime(vfx.gameObject, 5f));
        }

        PlayAudio(explosionSound, position, explosionVolume);
    }

    private void CreateEffect(GameObject prefab, Vector3 position, Vector3 normal, GameObject target)
    {
        if (prefab == null) return;

        var effect = Instantiate(prefab, position, Quaternion.LookRotation(normal));

        if (parentEffectsToTarget && target != null)
        {
            effect.transform.SetParent(target.transform);
        }

        StartCoroutine(DestroyAfterTime(effect, effectLifetime));
    }

    private void PlayAudio(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }

    private IEnumerator DestroyAfterTime(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        if (obj != null) Destroy(obj);
    }
}