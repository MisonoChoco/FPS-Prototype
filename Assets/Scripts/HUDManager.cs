using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Weapon;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; set; }

    [Header("Ammo")]
    public TextMeshProUGUI MagazineAmmoUI;

    public TextMeshProUGUI TotalAmmoUI;
    public Image AmmoTypeUI;

    [Header("Weapon")]
    public Image activeWeaponUI;

    public Image unActiveWeaponUI;
    public Image unActiveWeaponUI2;
    public TextMeshProUGUI CurrentItemText;

    [Header("Throwables")]
    public Image lethalUI;

    public TextMeshProUGUI lethalAmountUI;
    public Image tacticalUI;
    public TextMeshProUGUI tacticalAmountUI;

    [Header("Hitmarker")]
    public GameObject hitmarkerPrefab;

    public Transform hitmarkerContainer;
    public int hitmarkerPoolSize = 30;
    public bool debugHitmarker = true; // Toggle debug logs
    private Queue<GameObject> hitmarkerPool;
    private List<GameObject> activeHitmarkers;
    private Dictionary<GameObject, Image[]> hitmarkerImageCache;

    public Sprite emptySlot;
    public Sprite greySlot;
    public GameObject Crosshair;

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

        InitializeHitmarkerPool();
    }

    private void Start()
    {
        // Delay subscription to ensure BulletImpactEvents is initialized
        StartCoroutine(SubscribeToEventsDelayed());
    }

    private IEnumerator SubscribeToEventsDelayed()
    {
        // Wait until BulletImpactEvents.Instance exists
        float timeout = 1f;
        float elapsed = 0f;

        while (BulletImpactEvents.Instance == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        // If still null, create it automatically
        if (BulletImpactEvents.Instance == null)
        {
            Debug.LogWarning("HUDManager: BulletImpactEvents not found, creating automatically...");

            GameObject eventsObj = new GameObject("BulletImpactEvents");
            eventsObj.AddComponent<BulletImpactEvents>();

            // Wait one frame for Awake to run
            yield return null;

            if (BulletImpactEvents.Instance == null)
            {
                Debug.LogError("HUDManager: Failed to create BulletImpactEvents!");
                yield break;
            }

            Debug.Log("HUDManager: BulletImpactEvents created successfully!");
        }

        // Subscribe to event
        BulletImpactEvents.Instance.OnEnemyHit += ShowHitmarker;

        if (debugHitmarker)
        {
            Debug.Log("HUDManager: Successfully subscribed to OnEnemyHit event!");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from event
        if (BulletImpactEvents.Instance != null)
        {
            BulletImpactEvents.Instance.OnEnemyHit -= ShowHitmarker;
        }
    }

    private void Update()
    {
        UpdateWeaponUI();
        UpdateThrowablesUIVisual();
    }

    #region Hitmarker System

    private GameObject CreateHitmarkerInstance(int index)
    {
        GameObject hitmarker = Instantiate(hitmarkerPrefab, hitmarkerContainer);
        hitmarker.name = $"Hitmarker_{index}";

        Animator animator = hitmarker.GetComponent<Animator>();
        CanvasGroup canvasGroup = hitmarker.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = hitmarker.AddComponent<CanvasGroup>();
        }

        if (animator != null)
        {
            animator.enabled = false;
            animator.enabled = true;
        }

        return hitmarker;
    }

    private void InitializeHitmarkerPool()
    {
        if (hitmarkerPrefab == null)
        {
            Debug.LogWarning("HUDManager: Hitmarker prefab not assigned!");
            return;
        }

        if (hitmarkerContainer == null)
        {
            hitmarkerContainer = transform;
            if (debugHitmarker)
            {
                Debug.Log("HUDManager: Hitmarker container not assigned, using HUDManager transform");
            }
        }

        hitmarkerPool = new Queue<GameObject>();
        activeHitmarkers = new List<GameObject>();

        for (int i = 0; i < hitmarkerPoolSize; i++)
        {
            GameObject hitmarker = CreateHitmarkerInstance(i);
            hitmarker.SetActive(false);
            hitmarkerPool.Enqueue(hitmarker);
        }

        if (debugHitmarker)
        {
            Debug.Log($"HUDManager: Initialized hitmarker pool with {hitmarkerPoolSize} objects");
        }
    }

    private void ShowHitmarker(Vector3 hitPosition, int damage, HitFeedbackType hitType)
    {
        hitPosition = new Vector3(0, 0, 0);

        if (debugHitmarker)
        {
            Debug.Log($"HUDManager: ShowHitmarker called! Position: {hitPosition}, Damage: {damage}, Type: {hitType}");
        }

        if (hitmarkerPool == null)
        {
            Debug.LogError("HUDManager: Hitmarker pool is null!");
            return;
        }

        GameObject hitmarker = GetHitmarkerFromPool();

        if (hitmarker == null)
        {
            Debug.LogError("HUDManager: Failed to get hitmarker from pool!");
            return;
        }

        float randomRotation = UnityEngine.Random.Range(-12f, 12f);
        hitmarker.transform.rotation = Quaternion.Euler(0f, 0f, randomRotation);

        CanvasGroup canvasGroup = hitmarker.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = hitmarker.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;

        hitmarker.SetActive(true);

        Animator animator = hitmarker.GetComponent<Animator>();
        if (animator != null)
        {
            // Clear any stale trigger from this hitmarker's previous use in the pool
            animator.ResetTrigger("ArmorBreak");
            animator.ResetTrigger("EnemyKill");
            animator.ResetTrigger("HeadshotHit");
            animator.ResetTrigger("HeadshotKill");
            animator.ResetTrigger("ArmorHit");
            animator.ResetTrigger("FleshHit");
            animator.ResetTrigger("HeadshotArmor");

            string triggerName = hitType switch
            {
                HitFeedbackType.ArmorBreak => "ArmorBreak",
                HitFeedbackType.Kill => "EnemyKill",
                HitFeedbackType.HeadshotHit => "HeadshotHit",
                HitFeedbackType.HeadshotKill => "HeadshotKill",
                HitFeedbackType.Armor => "ArmorHit",
                HitFeedbackType.HeadshotArmor => "HeadshotArmor",
                _ => "FleshHit"
            };

            animator.SetTrigger(triggerName);

            if (debugHitmarker)
            {
                Debug.Log($"HUDManager: Fired hitmarker trigger '{triggerName}'");
            }
        }
        else
        {
            Debug.LogWarning("HUDManager: Hitmarker doesn't have Animator component!");
        }

        activeHitmarkers.Add(hitmarker);
        StartCoroutine(ReturnHitmarkerToPool(hitmarker, 1f));
    }

    private GameObject GetHitmarkerFromPool()
    {
        if (hitmarkerPool.Count > 0)
        {
            return hitmarkerPool.Dequeue();
        }
        else
        {
            Debug.LogWarning("HUDManager: Hitmarker pool exhausted, creating new instance");
            return CreateHitmarkerInstance(hitmarkerPoolSize + UnityEngine.Random.Range(1000, 9999));
        }
    }

    private IEnumerator ReturnHitmarkerToPool(GameObject hitmarker, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (hitmarker != null)
        {
            hitmarker.SetActive(false);
            activeHitmarkers.Remove(hitmarker);
            hitmarkerPool.Enqueue(hitmarker);

            if (debugHitmarker)
            {
                Debug.Log($"HUDManager: Returned hitmarker to pool. Pool size: {hitmarkerPool.Count}");
            }
        }
    }

    #endregion Hitmarker System

    #region UI Updates

    private void UpdateWeaponUI()
    {
        WeaponBase activeWeapon = WeaponManager.Instance?.CurrentWeapon;
        WeaponBase unActiveWeapon = GetUnactiveWeapon();

        if (activeWeapon != null)
        {
            var weaponInfo = activeWeapon.GetWeaponInfo();

            MagazineAmmoUI.text = $"{weaponInfo.BulletsLeft}";
            TotalAmmoUI.text = $"{WeaponManager.Instance.CheckAmmoLeftFor(activeWeapon.Data.ammoType)}";
            CurrentItemText.text = activeWeapon.Data.weaponName;

            AmmoTypeUI.sprite = activeWeapon.Data.ammoIcon ?? emptySlot;
            activeWeaponUI.sprite = activeWeapon.Data.weaponIcon ?? emptySlot;

            if (unActiveWeapon != null)
                unActiveWeaponUI.sprite = unActiveWeapon.Data.weaponIcon ?? emptySlot;
            else
                unActiveWeaponUI.sprite = emptySlot;
        }
        else
        {
            MagazineAmmoUI.text = "";
            TotalAmmoUI.text = "";
            CurrentItemText.text = "";
            AmmoTypeUI.sprite = emptySlot;
            activeWeaponUI.sprite = emptySlot;
            unActiveWeaponUI.sprite = emptySlot;
        }

        unActiveWeaponUI2.sprite = emptySlot;
    }

    private void UpdateThrowablesUIVisual()
    {
        if (WeaponManager.Instance.lethalsCount <= 0)
        {
            lethalUI.sprite = greySlot;
        }

        if (WeaponManager.Instance.tacticalsCount <= 0)
        {
            tacticalUI.sprite = greySlot;
        }
    }

    private WeaponBase GetUnactiveWeapon()
    {
        for (int i = 0; i < 3; i++)
        {
            if (i != WeaponManager.Instance.ActiveSlotIndex)
            {
                WeaponBase weapon = WeaponManager.Instance.GetWeaponInSlot(i);
                if (weapon != null)
                {
                    return weapon;
                }
            }
        }
        return null;
    }

    public void UpdateThrowablesUI()
    {
        lethalAmountUI.text = $"{WeaponManager.Instance.lethalsCount}";
        tacticalAmountUI.text = $"{WeaponManager.Instance.tacticalsCount}";

        switch (WeaponManager.Instance.equippedLethal)
        {
            case Throwable.ThrowableType.Frag:
                var fragSprite = Resources.Load<Sprite>("Frag");
                if (fragSprite != null)
                {
                    lethalUI.sprite = fragSprite;
                }
                else
                {
                    var fragObj = Resources.Load<GameObject>("Frag");
                    if (fragObj != null)
                    {
                        var spriteRenderer = fragObj.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            lethalUI.sprite = spriteRenderer.sprite;
                        }
                    }
                }
                break;

            case Throwable.ThrowableType.None:
                lethalUI.sprite = greySlot;
                break;
        }

        switch (WeaponManager.Instance.equippedTactical)
        {
            case Throwable.ThrowableType.Smoke:
                var smokeSprite = Resources.Load<Sprite>("Smoke");
                if (smokeSprite != null)
                {
                    tacticalUI.sprite = smokeSprite;
                }
                else
                {
                    var smokeObj = Resources.Load<GameObject>("Smoke");
                    if (smokeObj != null)
                    {
                        var spriteRenderer = smokeObj.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            tacticalUI.sprite = spriteRenderer.sprite;
                        }
                    }
                }
                break;

            case Throwable.ThrowableType.None:
                tacticalUI.sprite = greySlot;
                break;
        }
    }

    #endregion UI Updates
}