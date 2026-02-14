using UnityEngine;

[System.Serializable]
public struct WorkerGenome
{
    [Header("Traits (0..1 unless noted)")]
    [Range(0f, 1f)] public float moveChance;        // Speed: probability to take a step when it's time
    [Min(0f)]       public float senseRadius;       // Not used yet (mulch detection)
    [Range(0f, 1f)] public float acidAvoidance;     // Not used yet
    [Min(0f)]       public float transferAmount;    // Can map to deliverAmount already in WorkerAnt
    [Range(0f, 1f)] public float diggingExploration; // Not used yet
}
