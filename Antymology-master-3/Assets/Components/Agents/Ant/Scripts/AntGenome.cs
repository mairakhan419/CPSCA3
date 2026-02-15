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


    // FIX 1: Remove "= 3f" here. Struct fields cannot have initializers in C# 9.
    public float changeDirInterval;

    public static AntGenome RandomGenome()
    {
        return new AntGenome
        {
            moveSpeed = UnityEngine.Random.Range(0.8f, 3.2f),
            turnChance = UnityEngine.Random.Range(0.05f, 0.06f),
            turnChanceTwoBlocks = UnityEngine.Random.Range(0.01f, 0.02f),
            searchRadius = UnityEngine.Random.Range(9, 10),
            pauseDuration = UnityEngine.Random.Range(0.0f, 0.5f),
            avoidAcid = UnityEngine.Random.Range(0f, 0f),
            acidSenseRadius = UnityEngine.Random.Range(0, 0),

            // FIX 2: Set the default value here instead
            changeDirInterval = 3f
        };
    }
}
