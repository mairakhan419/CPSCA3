using UnityEngine;
using Antymology.Terrain;
using System.Collections.Generic;

public class QueenAntScript : MonoBehaviour
{
    [Header("Placement")]
    public float placeIntervalSeconds = 0.25f;
    public int forwardDistanceBlocks = 1;
    public int maxStackSearch = 50;

    [Header("Health")]
    public float maxHealth = 300f;
    public float health = 100f;

    float _nextPlaceTime;

    float NestCost => maxHealth / 3f;
    float BuildThreshold => maxHealth * (2f / 3f);
    private readonly List<Vector3Int> placedNestTiles = new();

    void Start()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
    }

    void Update()
    {
        // Debug.Log("Queen Health: " + health);
        if (Time.time >= _nextPlaceTime)
        {
            _nextPlaceTime = Time.time + placeIntervalSeconds;
            TryPlaceNestBlockInFront();
        }
    }

    // Workers will call this
    public float TryReceiveHealth(float amount)
    {
        if (amount <= 0f) return 0f;
        if (health >= maxHealth) return 0f;

        float accepted = Mathf.Min(amount, maxHealth - health);
        health += accepted;
        return accepted;
    }


    private void TryPlaceNestBlockInFront()
    {
        if (WorldManager.Instance == null) return;

        // Must have enough health to pay for the block
        if (health < BuildThreshold) return;



        Vector3 inFront = transform.position + transform.forward * forwardDistanceBlocks;

        int x = Mathf.FloorToInt(inFront.x);
        int z = Mathf.FloorToInt(inFront.z);

        x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
        z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

        int startY = Mathf.Clamp(Mathf.FloorToInt(transform.position.y), 1, WorldManager.Instance.WorldSizeY - 2);

        int y = FindFirstAirY(x, startY, z, maxStackSearch);
        if (y == -1) return;
        // Place nest
        Vector3Int pos = new Vector3Int(x, y, z);
        WorldManager.Instance.SetBlock(x, y, z, new NestBlock());
        placedNestTiles.Add(pos);
        // Pay health cost
        health -= NestCost;
        if (health < 0f) health = 0f;
    }

    private int FindFirstAirY(int x, int startY, int z, int searchUpLimit)
    {
        int y = startY;
        int tries = 0;

        while (tries < searchUpLimit && y < WorldManager.Instance.WorldSizeY - 1)
        {
            AbstractBlock b = WorldManager.Instance.GetBlock(x, y, z);
            if (b is AirBlock) return y;

            y++;
            tries++;
        }

        return -1;
    }

    public void ResetForNewGeneration(float resetHealth)
    {
        Debug.Log("RESETING QUEEN Health: " + health + " -> " + resetHealth);
        // Remove all nest blocks this queen placed
        if (WorldManager.Instance != null)
        {
            Debug.Log("Length of Placed Nest: " + placedNestTiles.Count);
            for (int i = 0; i < placedNestTiles.Count; i++)
            {
                Vector3Int p = placedNestTiles[i];

                // Only remove if it's still a NestBlock (avoid deleting something else)
                var b = WorldManager.Instance.GetBlock(p.x, p.y, p.z);
                if (b is NestBlock)
                {
                    Debug.Log("Clearing Block");
                    WorldManager.Instance.SetBlock(p.x, p.y, p.z, new AirBlock());

                }
            }
        }

        placedNestTiles.Clear();

        // Reset queen health + placement timer
        health = Mathf.Clamp(resetHealth, 0f, maxHealth);
        _nextPlaceTime = Time.time + placeIntervalSeconds;
    }

    public int BlocksPlacedThisGeneration => placedNestTiles.Count;



}
