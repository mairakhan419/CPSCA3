// using UnityEngine;
// using Antymology.Terrain;

// public class WorkerAntSpawnerScript : MonoBehaviour
// {
//     [Header("Prefabs")]
//     public GameObject workerAntPrefab;

//     [Header("Spawn Settings")]
//     public int numberToSpawn = 1;
//     public float spawnRadius = 3f;

//     [Header("Grounding")]
//     public bool groundToSurface = true;

//     [Header("References")]
//     public Transform queen; // assign or find by tag

//     void Awake()
//     {
//         if (queen == null)
//         {
//             var q = GameObject.FindWithTag("Queen");
//             if (q != null) queen = q.transform;
//         }
//     }

//     void Start()
//     {
//         SpawnWorkers();
//     }

//     public void SpawnWorkers()
//     {
//         if (workerAntPrefab == null)
//         {
//             Debug.LogError("WorkerAntSpawner: workerAntPrefab not assigned.");
//             return;
//         }

//         for (int i = 0; i < numberToSpawn; i++)
//         {
//             Vector3 offset = new Vector3(
//                 Random.Range(-spawnRadius, spawnRadius),
//                 0f,
//                 Random.Range(-spawnRadius, spawnRadius)
//             );

//             Vector3 spawnPos = transform.position + offset;
//             if (groundToSurface) spawnPos = GroundToSurface(spawnPos);

//             GameObject go = Instantiate(workerAntPrefab, spawnPos, Quaternion.identity);

//             var ant = go.GetComponent<WorkerAntScript>();
//             if (ant == null)
//             {
//                 Debug.LogError("WorkerAntSpawner: prefab missing WorkerAntScript.");
//                 Destroy(go);
//                 continue;
//             }

//             AntGenome g = AntGenome.RandomGenome();
//             ant.Initialize(g, queen);
//         }
//     }

//     private Vector3 GroundToSurface(Vector3 pos)
//     {
//         Vector3 rayStart = new Vector3(pos.x, WorldManager.Instance.WorldSizeY + 10f, pos.z);

//         if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10000f))
//         {
//             float lift = 0.0f;
//             Collider c = workerAntPrefab.GetComponentInChildren<Collider>();
//             if (c != null) lift = c.bounds.extents.y;
//             return hit.point + Vector3.up * (lift + 0.02f);
//         }

//         return pos;
//     }
// }


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
        queen = null;
        if (queen == null)
        {
            var q = GameObject.FindWithTag("Queen");
            if (q != null) queen = q.transform;
        }

    var ants = new List<WorkerAntScript>(genomes.Count);

    // SAME center logic as WorldManager used
    Vector3 centerXZ = new Vector3(WorldManager.Instance.WorldSizeX / 2f, 0f, WorldManager.Instance.WorldSizeZ / 2f);
    Vector3 spawnCenter = groundToSurface ? GroundToSurface(centerXZ) : centerXZ;

        for (int i = 0; i < genomes.Count; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f,
                Random.Range(-spawnRadius, spawnRadius)
            );

            Vector3 spawnPos = spawnCenter + offset;

            // keep them inside bounds so they don’t spawn outside the container walls
            spawnPos.x = Mathf.Clamp(spawnPos.x, 2f, WorldManager.Instance.WorldSizeX - 3f);
            spawnPos.z = Mathf.Clamp(spawnPos.z, 2f, WorldManager.Instance.WorldSizeZ - 3f);

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
