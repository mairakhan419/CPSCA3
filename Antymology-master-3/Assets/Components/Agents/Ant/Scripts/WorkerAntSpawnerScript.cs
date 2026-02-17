


using System.Collections.Generic;
using UnityEngine;
using Antymology.Terrain;

public class WorkerAntSpawnerScript : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject workerAntPrefab;

    [Header("Spawn Settings")]
    public float spawnRadius = 3f;

    [Header("Grounding")]
    public bool groundToSurface = true;

    [Header("References")]
    public Transform queen;

    void Awake()
    {
        queen = null;
        if (queen == null)
        {
            var q = GameObject.FindWithTag("Queen");
            if (q != null) queen = q.transform;
        }

    }

    public List<WorkerAntScript> SpawnGeneration(List<AntGenome> genomes)
    {
        // Find queen (you don't need to null it first)
        if (queen == null)
        {
            var q = GameObject.FindWithTag("Queen");
            if (q != null) queen = q.transform;
        }

        var ants = new List<WorkerAntScript>(genomes.Count);

        if (workerAntPrefab == null)
        {
            Debug.LogError("WorkerAntSpawner: workerAntPrefab not assigned.");
            return ants;
        }

        // Padding keeps spawns away from container walls
        const float wallPaddingMin = 2f;
        const float wallPaddingMaxX = 3f;
        const float wallPaddingMaxZ = 3f;

        float minX = wallPaddingMin;
        float maxX = WorldManager.Instance.WorldSizeX - wallPaddingMaxX;
        float minZ = wallPaddingMin;
        float maxZ = WorldManager.Instance.WorldSizeZ - wallPaddingMaxZ;

        for (int i = 0; i < genomes.Count; i++)
        {
            // Pick a random point anywhere in bounds
            Vector3 spawnPos = new Vector3(
                Random.Range(minX, maxX),
                0f,
                Random.Range(minZ, maxZ)
            );

            //  add a small jitter radius around that point
            // (keeps your spawnRadius feature meaningful without centering everything)
            if (spawnRadius > 0f)
            {
                spawnPos.x += Random.Range(-spawnRadius, spawnRadius);
                spawnPos.z += Random.Range(-spawnRadius, spawnRadius);
                spawnPos.x = Mathf.Clamp(spawnPos.x, minX, maxX);
                spawnPos.z = Mathf.Clamp(spawnPos.z, minZ, maxZ);
            }

            if (groundToSurface) spawnPos = GroundToSurface(spawnPos);

            GameObject go = Instantiate(workerAntPrefab, spawnPos, Quaternion.identity);

            var ant = go.GetComponent<WorkerAntScript>();
            if (ant == null)
            {
                Debug.LogError("WorkerAntSpawner: prefab missing WorkerAntScript.");
                Destroy(go);
                continue;
            }

            ant.Initialize(genomes[i], queen);
            ants.Add(ant);
        }

        return ants;
    }



    private Vector3 GroundToSurface(Vector3 pos)
    {
        int groundMask = LayerMask.GetMask("Ground");

        Vector3 rayStart = new Vector3(pos.x, WorldManager.Instance.WorldSizeY + 10f, pos.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10000f, groundMask))
        {
            float lift = 0.0f;
            Collider c = workerAntPrefab.GetComponentInChildren<Collider>();
            if (c != null) lift = c.bounds.extents.y;
            return hit.point + Vector3.up * (lift + 0.02f);
        }

        Debug.LogWarning($"GroundToSurface failed at xz=({pos.x:0.00},{pos.z:0.00}).");
        return pos;
    }

}
