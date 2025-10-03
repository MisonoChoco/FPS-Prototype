using UnityEngine;

public class DiscardedMag : MonoBehaviour
{
    [SerializeField] private AudioSource magDropSource;
    [SerializeField] private AudioClip[] audioClips;
    [SerializeField] private float playCooldown = 0.5f;

    private void OnCollisionEnter(Collision collision)
    {
        magDropSource = GetComponent<AudioSource>();

        if (playCooldown > 0)
        {
            playCooldown -= Time.deltaTime;
            int randomIndex = Random.Range(0, audioClips.Length);
            magDropSource.PlayOneShot(audioClips[randomIndex]);
        }
    }
}