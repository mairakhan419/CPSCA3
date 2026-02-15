using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Antymology.Terrain;   // <-- ADD THIS
public class EvolutionManagerScript : MonoBehaviour
{
    [Header("References")]
    public WorkerAntSpawnerScript spawner;

    [Header("Evolution Timing")]
    public float evaluationSeconds = 20f;

    [Header("Population")]
    public int populationSize = 2;

    [Header("Mutation")]
    [Range(0f, 1f)] public float mutationRate = 0.15f;
    public float mutationStrength = 0.2f;

    private readonly List<WorkerAntScript> liveAnts = new();
    private List<AntGenome> genomes = new();
    private float generationEndTime;
    private int generationIndex = 0;

    private System.Random rng = new System.Random(1234);

 private System.Collections.IEnumerator Start()
{
    while (WorldManager.Instance == null)
        yield return null;

    // wait for chunk meshes/colliders to be generated
    yield return new WaitForEndOfFrame();
    yield return new WaitForEndOfFrame();

    if (spawner == null) spawner = FindFirstObjectByType<WorkerAntSpawnerScript>();

    genomes = new List<AntGenome>(populationSize);
    for (int i = 0; i < populationSize; i++)
        genomes.Add(AntGenome.RandomGenome());

    SpawnNewGeneration();
}

    void Update()
    {
        // Debug.Log("Gen Time: " + generationEndTime + "time: " +Time.time);
        if (Time.time >= generationEndTime)
        {
            Debug.Log($"Generation {generationIndex} ended. Evaluating and breeding next generation...");
            EvaluateAndBreedTopTwo();
            SpawnNewGeneration();
        }
    }

    private void SpawnNewGeneration()
    {
        // destroy previous ants
        foreach (var ant in liveAnts)
            if (ant != null)
            {
                Debug.Log("Destroy");
                Destroy(ant.gameObject);
            }
        ;
        liveAnts.Clear();

        // spawn new ants
        liveAnts.AddRange(spawner.SpawnGeneration(genomes));

        generationIndex++;
        generationEndTime = Time.time + evaluationSeconds;

        Debug.Log($"Generation {generationIndex} started. Ends at t={generationEndTime:0.00}");
    }

    private void EvaluateAndBreedTopTwo()
    {
        // Rank by Fitness (dead ants might be null)
        var ranked = liveAnts
            .Where(a => a != null)
            .OrderByDescending(a => a.Fitness)
            .ToList();

        if (ranked.Count < 2)
        {
            Debug.LogWarning("Not enough ants survived to select top 2. Re-randomizing.");
            genomes = new List<AntGenome>(populationSize);
            for (int i = 0; i < populationSize; i++)
                genomes.Add(AntGenome.RandomGenome());
            return;
        }

        var best1 = ranked[0];
        var best2 = ranked[1];

        Debug.Log($"Gen {generationIndex} best fitness: {best1.Fitness:0.00}, second: {best2.Fitness:0.00}");

        // Make next generation from top 2
        var next = new List<AntGenome>(populationSize);

        // (Optional) elitism: keep exact best genomes
        next.Add(best1.Genome);
        next.Add(best2.Genome);

        while (next.Count < populationSize)
        {
            AntGenome child = Crossover(best1.Genome, best2.Genome);
            Mutate(ref child);
            next.Add(child);
        }

        genomes = next;
    }

    private AntGenome Crossover(AntGenome a, AntGenome b)
    {
        // Uniform crossover: each gene chooses parent A or B
        bool Pick() => rng.NextDouble() < 0.5;

        return new AntGenome
        {
            moveSpeed = Pick() ? a.moveSpeed : b.moveSpeed,
            turnChance = Pick() ? a.turnChance : b.turnChance,
            pauseDuration = Pick() ? a.pauseDuration : b.pauseDuration,
            searchRadius = Pick() ? a.searchRadius : b.searchRadius,
            turnChanceTwoBlocks = Pick() ? a.turnChanceTwoBlocks : b.turnChanceTwoBlocks,
            avoidAcid = Pick() ? a.avoidAcid : b.avoidAcid,
            acidSenseRadius = Pick() ? a.acidSenseRadius : b.acidSenseRadius,
        };
    }

    private void Mutate(ref AntGenome g)
    {
        float Jitter(float x, float min, float max)
        {
            // small noise, scaled by mutationStrength
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0); // -1..1
            x += noise * mutationStrength;
            return Mathf.Clamp(x, min, max);
        }

        if (rng.NextDouble() < mutationRate) g.moveSpeed = Jitter(g.moveSpeed, 0.5f, 4f);
        if (rng.NextDouble() < mutationRate) g.turnChance = Jitter(g.turnChance, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.pauseDuration = Jitter(g.pauseDuration, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.searchRadius = Mathf.Clamp(g.searchRadius + rng.Next(-2, 3), 1, 12);
        if (rng.NextDouble() < mutationRate) g.turnChanceTwoBlocks = Jitter(g.turnChanceTwoBlocks, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.avoidAcid = Jitter(g.avoidAcid, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.acidSenseRadius = Mathf.Clamp(g.acidSenseRadius + rng.Next(-1, 2), 1, 8);
    }
}
