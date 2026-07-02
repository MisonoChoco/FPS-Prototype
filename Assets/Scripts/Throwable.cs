using System;
using UnityEngine;

public class Throwable : MonoBehaviour
{
    [SerializeField] private float delay = 3f;
    [SerializeField] private float dmgRadius = 20f;
    [SerializeField] private float explosionForce = 1200f;

    private float countdown;

    private bool hasExploded = false;
    public bool hasbeenThrown = false;

    public enum ThrowableType
    {
        None,
        Frag,
        Smoke
    }

    public ThrowableType throwableType;

    private void Start()
    {
        countdown = delay;
    }

    private void Update()
    {
        if (hasbeenThrown)
        {
            countdown -= Time.deltaTime;
            if (countdown <= 0f && !hasExploded)
            {
                Explode();
                hasExploded = true;
            }
        }
    }

    private void Explode()
    {
        GetThrowableEffect();
        Destroy(gameObject);
    }

    private void GetThrowableEffect()
    {
        switch (throwableType)
        {
            case ThrowableType.Frag:
                GrenadeEffect();
                break;

            case ThrowableType.Smoke:
                SmokeGrenadeEffect();
                break;
        }
    }

    private void SmokeGrenadeEffect()
    {
        GameObject smokeEffect = GlobalReference.Instance.SmokeExplosionEffect;
        Instantiate(smokeEffect, transform.position, transform.rotation);

        SoundManager.Instance.PlaySmokeSound();

        Collider[] colliders = Physics.OverlapSphere(transform.position, dmgRadius);
        foreach (Collider objectInRange in colliders)
        {
            Rigidbody rb = objectInRange.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // smoking here...
            }
        }
    }

    private void GrenadeEffect()
    {
        GameObject fragEffect = GlobalReference.Instance.FragExplosionEffect;
        Instantiate(fragEffect, transform.position, transform.rotation);

        SoundManager.Instance.PlayGrenadeSound();

        Collider[] colliders = Physics.OverlapSphere(transform.position, dmgRadius);
        foreach (Collider objectInRange in colliders)
        {
            Rigidbody rb = objectInRange.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddExplosionForce(explosionForce, transform.position, dmgRadius);

            Enemy enemy = objectInRange.GetComponent<Enemy>();
            if (enemy != null)
                enemy.TakeDamage(100);
        }
    }
}