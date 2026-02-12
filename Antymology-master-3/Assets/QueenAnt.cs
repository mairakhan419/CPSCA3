using UnityEngine;
using Antymology.Terrain; // so we can access WorldManager and blocks

public class QueenAnt : MonoBehaviour
{
    [Header("Placement")]
    public float placeIntervalSeconds = 0.25f;
    public int forwardDistanceBlocks = 1;
    public int maxStackSearch = 50; // how high we’re willing to stack

    private float _nextPlaceTime;

    void Update()
    {
        if (Time.time >= _nextPlaceTime)
        {
            _nextPlaceTime = Time.time + placeIntervalSeconds;
            PlaceNestBlockInFront();
        }
    }

    private void PlaceNestBlockInFront()
    {
        if (WorldManager.Instance == null)
            return;

        // 1) Pick the X/Z "in front" of the queen
        Vector3 inFront = transform.position + transform.forward * forwardDistanceBlocks;

        int x = Mathf.RoundToInt(inFront.x);
        int z = Mathf.RoundToInt(inFront.z);

        // keep it inside the world (avoid borders)
        x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
        z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

        // 2) Find first empty spot in that column, starting near queen height
        int startY = Mathf.Clamp(Mathf.RoundToInt(transform.position.y), 1, WorldManager.Instance.WorldSizeY - 2);

        int y = FindFirstAirY(x, startY, z, maxStackSearch);
        if (y == -1)
            return; // nowhere to place

        // 3) Place the block
        // Choose whatever block you want as "nest" (MulchBlock is a safe example)
        WorldManager.Instance.SetBlock(x, y, z, new NestBlock());
    }

    private int FindFirstAirY(int x, int startY, int z, int searchUpLimit)
    {
        int y = startY;

        // If we are inside solid, move up until we hit air
        int tries = 0;
        while (tries < searchUpLimit && y < WorldManager.Instance.WorldSizeY - 1)
        {
            AbstractBlock b = WorldManager.Instance.GetBlock(x, y, z);

            // "AirBlock" check without needing namespace headaches:
            if (b is AirBlock)
                return y;

            y++;
            tries++;
        }

        return -1;
    }
}
