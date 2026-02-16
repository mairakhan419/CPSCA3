using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Antymology.Terrain;   // <-- ADD THIS
using System.IO;
using System.Text;

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
    private int generationIndex = 0;// ---- Averages for current generation's genomes ----
private float avgMoveSpeed;
private float avgTurnChance;
private float avgPauseDuration;
private float avgSearchRadius;
private float avgTurnChanceTwoBlocks;
private float avgAvoidAcid;
private float avgAcidSenseRadius;
private float avgDigProbability;

public float AvgMoveSpeed => avgMoveSpeed;
public float AvgTurnChance => avgTurnChance;
public float AvgPauseDuration => avgPauseDuration;
public float AvgSearchRadius => avgSearchRadius;
public float AvgTurnChanceTwoBlocks => avgTurnChanceTwoBlocks;
public float AvgAvoidAcid => avgAvoidAcid;
public float AvgAcidSenseRadius => avgAcidSenseRadius;
    public float AvgDigProbability => avgDigProbability;



    private System.Random rng = new System.Random(1234);
    public QueenAntScript queen;
    public float queenStartHealth = 100f;
    [SerializeField] private string csvFileName = "evolution_log.csv";

    private bool hasSaved = false;

    public int CurrentGeneration => generationIndex;

    // Seconds remaining in the current generation (clamped)
    public float SecondsRemaining =>
        Mathf.Max(0f, generationEndTime - Time.time);

    // Normalized progress 0..1 (optional)
    public float GenerationT01 =>
        (evaluationSeconds <= 0f) ? 0f : Mathf.Clamp01(SecondsRemaining / evaluationSeconds);


    private System.Collections.IEnumerator Start()
    {
        if (queen == null)
        {
            GameObject q = GameObject.FindWithTag("Queen");

            if (q != null)
            {

                queen = q.GetComponent<QueenAntScript>();
            }
        }
        Debug.Log("Got Queen: " + queen);

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
        if (queen == null)
        {
            GameObject q = GameObject.FindWithTag("Queen");

            if (q != null)
            {

                queen = q.GetComponent<QueenAntScript>();
            }
        }
        if (Time.time >= generationEndTime)
        {
            // 1) Capture stats for the generation that just finished
            CaptureGenerationRecord();

            Debug.Log($"Generation {generationIndex} ended. Evaluating and breeding next generation...");

            // 2) Reset queen
            if (queen != null)
                queen.ResetForNewGeneration(queenStartHealth);

            // 3) Restore mulch
            if (WorldManager.Instance != null)
                WorldManager.Instance.RestoreRemovedMulch();

            // 4) Breed + spawn
            EvaluateAndBreedTopTwo();
            SpawnNewGeneration();
        }





    }
    private void CaptureGenerationRecord()
    {
        // rank
        var ranked = liveAnts
            .Where(a => a != null)
            .OrderByDescending(a => a.Fitness)
            .ToList();

        // If less than 2, still record what we can
        WorkerAntScript best1 = ranked.Count > 0 ? ranked[0] : null;
        WorkerAntScript best2 = ranked.Count > 1 ? ranked[1] : null;

        var rec = new GenerationRecord();
        rec.generation = generationIndex;

        rec.queenBlocksPlaced = (queen != null) ? queen.BlocksPlacedThisGeneration : 0;

        if (best1 != null)
        {
            rec.best1Fitness = best1.Fitness;
            var g = best1.Genome;
            rec.best1MoveSpeed = g.moveSpeed;
            rec.best1TurnChance = g.turnChance;
            rec.best1PauseDuration = g.pauseDuration;
            rec.best1SearchRadius = g.searchRadius;
            rec.best1TurnChanceTwoBlocks = g.turnChanceTwoBlocks;
            rec.best1AvoidAcid = g.avoidAcid;
            rec.best1AcidSenseRadius = g.acidSenseRadius;
            rec.best1DigProbability = g.digProbability;
        }

        if (best2 != null)
        {
            rec.best2Fitness = best2.Fitness;
            var g = best2.Genome;
            rec.best2MoveSpeed = g.moveSpeed;
            rec.best2TurnChance = g.turnChance;
            rec.best2PauseDuration = g.pauseDuration;
            rec.best2SearchRadius = g.searchRadius;
            rec.best2TurnChanceTwoBlocks = g.turnChanceTwoBlocks;
            rec.best2AvoidAcid = g.avoidAcid;
            rec.best2AcidSenseRadius = g.acidSenseRadius;
            rec.best2DigProbability = g.digProbability;
        }

        history.Add(rec);
    }


    private void SpawnNewGeneration()
    {
        // destroy previous ants
        foreach (var ant in liveAnts)
            if (ant != null)
            {
                Destroy(ant.gameObject);
            }

        liveAnts.Clear();

        // spawn new ants
        liveAnts.AddRange(spawner.SpawnGeneration(genomes));

        generationIndex++;
        generationEndTime = Time.time + evaluationSeconds;

        // ---- Compute averages from genomes ----
        // ---- Compute averages from genomes ----
// ---- Compute averages from genomes ----
        avgMoveSpeed = 0f;
        avgTurnChance = 0f;
        avgPauseDuration = 0f;
        avgSearchRadius = 0f;
        avgTurnChanceTwoBlocks = 0f;
        avgAvoidAcid = 0f;
        avgAcidSenseRadius = 0f;
        avgDigProbability = 0f;

        int n = genomes.Count;
        if (n > 0)
        {
            for (int i = 0; i < n; i++)
            {
                var g = genomes[i];
                avgMoveSpeed += g.moveSpeed;
                avgTurnChance += g.turnChance;
                avgPauseDuration += g.pauseDuration;
                avgSearchRadius += g.searchRadius;
                avgTurnChanceTwoBlocks += g.turnChanceTwoBlocks;
                avgAvoidAcid += g.avoidAcid;
                avgAcidSenseRadius += g.acidSenseRadius;
                avgDigProbability += g.digProbability;
            }

            float inv = 1f / n;
            avgMoveSpeed *= inv;
            avgTurnChance *= inv;
            avgPauseDuration *= inv;
            avgSearchRadius *= inv;
            avgTurnChanceTwoBlocks *= inv;
            avgAvoidAcid *= inv;
            avgAcidSenseRadius *= inv;
            avgDigProbability *= inv;
        }



        // ---- Log message ----
        Debug.Log(
            $"Generation {generationIndex} started. Ends at t={generationEndTime:0.00}\n" +
            $"Averages → MoveSpeed: {avgMoveSpeed:0.00} | " +
            $"SearchRadius: {avgSearchRadius:0.00} | " +
            $"DigProbability: {avgDigProbability:0.00}"
        );
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
        Debug.Log("Best 1 Search Radius " + best1.Genome.searchRadius + " Move Speed" + best1.Genome.moveSpeed + "Dig Proability " + best1.Genome.digProbability);
        Debug.Log("Best 2 Search Radius " + best2.Genome.searchRadius + " Move Speed" + best2.Genome.moveSpeed + "Dig Proability " + best2.Genome.digProbability);






        Debug.Log($"Gen {generationIndex} best fitness: {best1.Fitness:0.00}, second: {best2.Fitness:0.00}");

        // Make next generation from top 2
        var next = new List<AntGenome>(populationSize);

        // (Optional) elitism: keep exact best genomes
        // next.Add(best1.Genome);
        // next.Add(best2.Genome);

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

            // FIX: include digProbability
            digProbability = Pick() ? a.digProbability : b.digProbability,
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

        if (rng.NextDouble() < mutationRate) g.moveSpeed = Jitter(g.moveSpeed, 0.3f, 4f);
        if (rng.NextDouble() < mutationRate) g.turnChance = Jitter(g.turnChance, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.pauseDuration = Jitter(g.pauseDuration, 0f, 1f);

        if (rng.NextDouble() < mutationRate) g.searchRadius = Mathf.Clamp(g.searchRadius + rng.Next(-2, 3), 1, 12);

        if (rng.NextDouble() < mutationRate) g.turnChanceTwoBlocks = Jitter(g.turnChanceTwoBlocks, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.avoidAcid = Jitter(g.avoidAcid, 0f, 1f);
        if (rng.NextDouble() < mutationRate) g.acidSenseRadius = Mathf.Clamp(g.acidSenseRadius + rng.Next(-1, 2), 1, 8);

        // FIX: mutate digProbability (0..1)
        if (rng.NextDouble() < mutationRate) g.digProbability = Jitter(g.digProbability, 0f, 1f);
    }
    [System.Serializable]
    public class GenerationRecord
    {
        public int generation;
        public int queenBlocksPlaced;

        public float best1Fitness;
        public float best1MoveSpeed;
        public float best1TurnChance;
        public float best1PauseDuration;
        public int best1SearchRadius;
        public float best1TurnChanceTwoBlocks;
        public float best1AvoidAcid;
        public int best1AcidSenseRadius;
        public float best1DigProbability;

        public float best2Fitness;
        public float best2MoveSpeed;
        public float best2TurnChance;
        public float best2PauseDuration;
        public int best2SearchRadius;
        public float best2TurnChanceTwoBlocks;
        public float best2AvoidAcid;
        public int best2AcidSenseRadius;
        public float best2DigProbability;
    }

    private readonly List<GenerationRecord> history = new();



private void OnApplicationQuit()
{
    SaveCsvOnce();
}

// In the Unity Editor, OnApplicationQuit can be unreliable.
// OnDisable is usually called when you press Stop.
private void OnDisable()
{
    SaveCsvOnce();
}

private void SaveCsvOnce()
{
    if (hasSaved) return;
    hasSaved = true;

    if (history.Count == 0) return;

    string path = Path.Combine(Application.persistentDataPath, csvFileName);

    var sb = new StringBuilder(16 * 1024);

    sb.AppendLine(
        "generation,queenBlocksPlaced," +
        "best1Fitness,best1MoveSpeed,best1TurnChance,best1PauseDuration,best1SearchRadius,best1TurnChanceTwoBlocks,best1AvoidAcid,best1AcidSenseRadius,best1DigProbability," +
        "best2Fitness,best2MoveSpeed,best2TurnChance,best2PauseDuration,best2SearchRadius,best2TurnChanceTwoBlocks,best2AvoidAcid,best2AcidSenseRadius,best2DigProbability"
    );

    foreach (var r in history)
    {
        sb.Append(r.generation).Append(',')
          .Append(r.queenBlocksPlaced).Append(',')

          .Append(r.best1Fitness).Append(',')
          .Append(r.best1MoveSpeed).Append(',')
          .Append(r.best1TurnChance).Append(',')
          .Append(r.best1PauseDuration).Append(',')
          .Append(r.best1SearchRadius).Append(',')
          .Append(r.best1TurnChanceTwoBlocks).Append(',')
          .Append(r.best1AvoidAcid).Append(',')
          .Append(r.best1AcidSenseRadius).Append(',')
          .Append(r.best1DigProbability).Append(',')

          .Append(r.best2Fitness).Append(',')
          .Append(r.best2MoveSpeed).Append(',')
          .Append(r.best2TurnChance).Append(',')
          .Append(r.best2PauseDuration).Append(',')
          .Append(r.best2SearchRadius).Append(',')
          .Append(r.best2TurnChanceTwoBlocks).Append(',')
          .Append(r.best2AvoidAcid).Append(',')
          .Append(r.best2AcidSenseRadius).Append(',')
          .Append(r.best2DigProbability);

        sb.AppendLine();
    }

    File.WriteAllText(path, sb.ToString());
    Debug.Log($"Saved evolution CSV to: {path}");
}

}

