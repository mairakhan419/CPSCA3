// using UnityEngine;
// using Antymology.Terrain;

// public class WorkerAntSpawner : MonoBehaviour
// {
//     [Header("Prefabs")]
//     public GameObject workerAntPrefab;

//     [Header("Spawn Settings")]
//     public int numberToSpawn = 10;
//     public float spawnRadius = 3f;

//     [Header("Grounding")]
//     public bool groundToSurface = true;

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

//             if (groundToSurface)
//                 spawnPos = GroundToSurface(spawnPos);

//             Instantiate(workerAntPrefab, spawnPos, Quaternion.identity);
//         }
//     }

// private Vector3 GroundToSurface(Vector3 pos)
// {
//     // Raycast straight down onto the chunk mesh colliders.
//     // Start high above the world so we definitely hit something.
//     Vector3 rayStart = new Vector3(pos.x, WorldManager.Instance.WorldSizeY + 10f, pos.z);

//     if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10000f))
//     {
//         float lift = 0.0f;

//         // Use the worker prefab's collider height to place it on top of the hit surface
//         Collider c = workerAntPrefab.GetComponentInChildren<Collider>();
//         if (c != null)
//             lift = c.bounds.extents.y; // half height

//         return hit.point + Vector3.up * (lift + 0.02f); // tiny extra so it doesn't clip
//     }

//     // Fallback: if no hit, just return original
//     return pos;
// }

// }


using UnityEngine;

public class WorkerAntSpawner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
