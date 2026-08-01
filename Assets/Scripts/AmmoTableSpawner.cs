using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmmoTable : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private List<GameObject> ammoBoxPrefabs = new();
    [SerializeField] private float spawnHeight = 1.5f;
    [SerializeField] private float spacing = 0.5f;
    [SerializeField] private float spawnDelay = 0.1f; // seconds between each box
    [SerializeField] private bool oneTimeUse = false;
    [SerializeField] private int columns = 6;    // grid columns

    private bool hasBeenUsed = false;

    public void Interact()
    {
        if (oneTimeUse && hasBeenUsed) return;
        hasBeenUsed = true;
        StartCoroutine(SpawnSequence());
    }

    private IEnumerator SpawnSequence()
    {
        for (int i = 0; i < ammoBoxPrefabs.Count; i++)
        {
            if (ammoBoxPrefabs[i] == null) continue;

            // Calculate grid position
            int col = i % columns;
            int row = i / columns;
            Vector3 offset = new Vector3(
                col * spacing - (columns - 1) * spacing * 0.5f,  // center the grid
                spawnHeight,
                row * spacing);

            Vector3 spawnPos = transform.position + offset;

            GameObject box = Instantiate(ammoBoxPrefabs[i], spawnPos, Quaternion.identity);

            if (box.GetComponent<Rigidbody>() == null)
                box.AddComponent<Rigidbody>();

            yield return new WaitForSeconds(spawnDelay);
        }
    }
}