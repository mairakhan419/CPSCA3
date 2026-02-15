// using UnityEngine;
// using Antymology.Terrain;

// public class QueenAnt : MonoBehaviour
// {
//     [Header("Placement")]
//     public float placeIntervalSeconds = 0.25f;
//     public int forwardDistanceBlocks = 1;
//     public int maxStackSearch = 50;

//     [Header("Health")]
//     public float maxHealth = 300f;
//     public float health = 100f;

//     float _nextPlaceTime;

//     float NestCost => maxHealth / 3f;
//     float BuildThreshold => maxHealth * (2f / 3f);

//     void Start()
//     {
//         health = Mathf.Clamp(health, 0f, maxHealth);
//     }

//     void Update()
//     {
//         // Debug.Log("Queen Health: " + health);
//         if (Time.time >= _nextPlaceTime)
//         {
//             _nextPlaceTime = Time.time + placeIntervalSeconds;
//             TryPlaceNestBlockInFront();
//         }
//     }

//     // Workers will call this
//     public bool TryReceiveHealth(float amount)
//     {
//         if (amount <= 0f) return false;
//         if (health >= maxHealth) return false;

//         float accepted = Mathf.Min(amount, maxHealth - health);
//         health += accepted;
//         return accepted > 0f;
//     }

//     private void TryPlaceNestBlockInFront()
//     {
//         if (WorldManager.Instance == null) return;

//         // Must have enough health to pay for the block
//         if (health < BuildThreshold) return;



//         Vector3 inFront = transform.position + transform.forward * forwardDistanceBlocks;

//         int x = Mathf.FloorToInt(inFront.x);
//         int z = Mathf.FloorToInt(inFront.z);

//         x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
//         z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

//         int startY = Mathf.Clamp(Mathf.FloorToInt(transform.position.y), 1, WorldManager.Instance.WorldSizeY - 2);

//         int y = FindFirstAirY(x, startY, z, maxStackSearch);
//         if (y == -1) return;
//         // Place nest
//         WorldManager.Instance.SetBlock(x, y, z, new NestBlock());
//         Debug.Log("Added Block");
//         // Pay health cost
//         health -= NestCost;
//         if (health < 0f) health = 0f;
//     }

//     private int FindFirstAirY(int x, int startY, int z, int searchUpLimit)
//     {
//         int y = startY;
//         int tries = 0;

//         while (tries < searchUpLimit && y < WorldManager.Instance.WorldSizeY - 1)
//         {
//             AbstractBlock b = WorldManager.Instance.GetBlock(x, y, z);
//             if (b is AirBlock) return y;

//             y++;
//             tries++;
//         }

//         return -1;
//     }
// }

using UnityEngine;

public class QueenAnt : MonoBehaviour
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
