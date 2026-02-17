using System;
using UnityEngine;

[Serializable]
public struct AntGenome
{
    [Range(0.5f, 4f)] public float moveSpeed;
    [Range(0f, 1f)] public float turnChance;
    [Range(0f, 1f)] public float turnChanceTwoBlocks;

    [Range(1, 12)] public int searchRadius;
    [Range(0.0f, 1f)] public float pauseDuration;
    [Range(0f, 1f)] public float avoidAcid;   // 0 = doesn’t care, 1 = strongly avoids
    [Range(1, 8)]  public int acidSenseRadius;
    [Range(0f, 1f)] public float digProbability;

    // Remove "= 3f" here. Struct fields cannot have initializers in C# 9.
    public float changeDirInterval;

    public static AntGenome RandomGenome()
    {
        return new AntGenome
        {
            // moveSpeed = UnityEngine.Random.Range(1f, 6f),
            moveSpeed = UnityEngine.Random.Range(0.1f, 1.5f),

            turnChance = UnityEngine.Random.Range(0f, 1f),
            turnChanceTwoBlocks = UnityEngine.Random.Range(0f, 1f),
            searchRadius = UnityEngine.Random.Range(4, 10),
            pauseDuration = UnityEngine.Random.Range(0.0f, 0.5f),
            avoidAcid = UnityEngine.Random.Range(0f, 1f),
            acidSenseRadius = UnityEngine.Random.Range(0, 10),
            // digProbability = UnityEngine.Random.Range(0f, 1f), // Random value between 0 and 1
            digProbability  = UnityEngine.Random.Range(0f, 1f),

            // Set the default value here instead
            changeDirInterval = 3f
        };
    }
}
