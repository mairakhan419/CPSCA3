using UnityEngine;
using Antymology.Terrain;

public class WorkerAntSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject workerAntPrefab;

    [Header("Spawn Settings")]
    public int numberToSpawn = 10;
    public float spawnRadius = 3f;

    [Header("Grounding")]
    public bool groundToSurface = true;

    [Header("Genome Random Ranges")]
    public Vector2 moveChanceRange = new Vector2(0.25f, 1.0f);
    public Vector2 senseRadiusRange = new Vector2(1.0f, 8.0f);
    public Vector2 acidAvoidanceRange = new Vector2(0.0f, 1.0f);
    public Vector2 transferAmountRange = new Vector2(5.0f, 20.0f);
    public Vector2 diggingExplorationRange = new Vector2(0.0f, 1.0f);

    void Start()
    {
        SpawnWorkers();
    }

    public void SpawnWorkers()
    {
        if (workerAntPrefab == null)
        {
            Debug.LogError("WorkerAntSpawner: workerAntPrefab not assigned.");
            return;
        }

        for (int i = 0; i < numberToSpawn; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f,
                Random.Range(-spawnRadius, spawnRadius)
            );

            Vector3 spawnPos = transform.position + offset;

            if (groundToSurface)
                spawnPos = GroundToSurface(spawnPos);

            GameObject go = Instantiate(workerAntPrefab, spawnPos, Quaternion.identity);

            // Assign unique randomized genome per ant
            WorkerAnt ant = go.GetComponent<WorkerAnt>();
            if (ant != null)
            {
                WorkerGenome g = new WorkerGenome
                {
                    moveChance = Random.Range(moveChanceRange.x, moveChanceRange.y),
                    senseRadius = Random.Range(senseRadiusRange.x, senseRadiusRange.y),
                    acidAvoidance = Random.Range(acidAvoidanceRange.x, acidAvoidanceRange.y),
                    transferAmount = Random.Range(transferAmountRange.x, transferAmountRange.y),
                    diggingExploration = Random.Range(diggingExplorationRange.x, diggingExplorationRange.y),
                };

                ant.SetGenome(g);
            }
        }
    }

    private Vector3 GroundToSurface(Vector3 pos)
    {
        Vector3 rayStart = new Vector3(pos.x, WorldManager.Instance.WorldSizeY + 10f, pos.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10000f))
        {
            float lift = 0.0f;

            Collider c = workerAntPrefab.GetComponentInChildren<Collider>();
            if (c != null)
                lift = c.bounds.extents.y;

            return hit.point + Vector3.up * (lift + 0.02f);
        }

        return pos;
    }
}
