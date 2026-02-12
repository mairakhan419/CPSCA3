using UnityEngine;
using Antymology.Terrain;
public enum AntState
{
    Wandering,
    Digging,
    Returning
}

public class WorkerAnt : MonoBehaviour
{
    [Header("Timing")]
    public float stepInterval = 0.5f;
    public float turnInterval = 2.0f;

    [Header("Turning")]
    [Range(0f, 1f)] public float turnChance = 0.35f;

    [Header("Smoothing")]
    public float moveSpeed = 3.0f;
    public float turnSpeed = 540f;

    [Header("Grounding")]
    public float surfaceOffset = 0.02f;
    public LayerMask groundMask = ~0; // ideally set Terrain layer in inspector

    [Header("Debug / State")]
    public Vector3Int CurrentBlock;     // block the ant is currently on (x,y,z)
    public float CurrentGroundY;        // detected ground surface height (world y)
    public AntState currentState = AntState.Wandering;
    private Transform queen;

    float stepTimer;
    float turnTimer;

    Vector3 targetPos;
    Quaternion targetRot;
    bool hasTarget;

    static readonly int[] CardinalAngles = { 0, 90, 180, 270 };

    void Start()
    {
        SetRandomCardinalRotation();
        targetRot = transform.rotation;

        SnapToSurfaceAtCurrentXZ();
        targetPos = transform.position;
        hasTarget = false;

        // Detect initial block
        UpdateCurrentBlockFromWorld();
        if (queen == null && WorldManager.Instance != null)
            queen = WorldManager.Instance.QueenTransform;

    }

    void Update()
    {
        if (currentState == AntState.Returning)
        {
            ReturnToQueen();
            return;
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        if (hasTarget)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

            if ((transform.position - targetPos).sqrMagnitude < 0.000001f)
            {
                transform.position = targetPos;
                hasTarget = false;

                // We arrived on a new tile: detect where we actually are
                UpdateCurrentBlockFromWorld();
                checkBlock();

            }
            return;
        }

        TurnLeftRightOrBack_Smooth();

        stepTimer += Time.deltaTime;
        if (stepTimer >= stepInterval)
        {
            stepTimer = 0f;
            StepForwardToSurface_SmoothTarget();
        }
    }

    // -------------------- Detection --------------------

    /// <summary>
    /// Detects the block beneath the ant and updates CurrentBlock (x,y,z) using real world state.
    /// Uses raycast first; if that fails, falls back to voxel scan.
    /// </summary>
    public void UpdateCurrentBlockFromWorld()
    {
        // int x = Mathf.RoundToInt(transform.position.x);
        // int z = Mathf.RoundToInt(transform.position.z);
        int x = Mathf.FloorToInt(transform.position.x);
        int z = Mathf.FloorToInt(transform.position.z);


        x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
        z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

        // 1) Physics raycast: detects actual mesh surface
        if (TryGetGroundByRaycast(x, z, out float hitY))
        {
            CurrentGroundY = hitY;

            // Convert surface Y -> block Y in your voxel scheme.
            // In your mesh, top face is at integer y for the solid block.
            // We can approximate by rounding the hitY down to nearest int.
            int yBlock = Mathf.FloorToInt(hitY - 0.001f); // important: subtract

            CurrentBlock = new Vector3Int(x, yBlock, z);

            return;
        }

        // 2) Fallback: voxel scan (data-based)
        if (TryGetTopSolidBlockY(x, z, out int yTop))
        {
            CurrentGroundY = yTop;
            CurrentBlock = new Vector3Int(x, yTop, z);
            return;
        }

        // If nothing found
        CurrentBlock = new Vector3Int(x, 0, z);
        CurrentGroundY = 0f;
    }

    bool TryGetGroundByRaycast(int x, int z, out float groundY)
    {
        // Cast from above down to terrain at the center of the tile
        Vector3 origin = new Vector3(x + 0.5f, WorldManager.Instance.WorldSizeY + 5f, z + 0.5f);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, WorldManager.Instance.WorldSizeY + 20f, groundMask))
        {
            Debug.Log("Raycast hit: " + hit.collider.name
    + " layer=" + hit.collider.gameObject.layer
    + " tag=" + hit.collider.tag);


            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    bool TryGetTopSolidBlockY(int x, int z, out int topY)
    {
        int y = WorldManager.Instance.WorldSizeY - 1;
        while (y > 0 && WorldManager.Instance.GetBlock(x, y, z) is AirBlock)
            y--;

        topY = y;
        return y > 0;
    }

    // -------------------- Movement --------------------

    void StepForwardToSurface_SmoothTarget()
    {
        Vector3 f = transform.forward;
        int stepX = 0, stepZ = 0;

        if (Mathf.Abs(f.x) > Mathf.Abs(f.z))
            stepX = f.x >= 0 ? 1 : -1;
        else
            stepZ = f.z >= 0 ? 1 : -1;

        int curX = Mathf.RoundToInt(transform.position.x);
        int curZ = Mathf.RoundToInt(transform.position.z);

        int nextX = Mathf.Clamp(curX + stepX, 1, WorldManager.Instance.WorldSizeX - 2);
        int nextZ = Mathf.Clamp(curZ + stepZ, 1, WorldManager.Instance.WorldSizeZ - 2);

        if (!TryGetTopSolidBlockY(nextX, nextZ, out int yTop))
            return;

            if (WorldManager.Instance.GetBlock(nextX, yTop, nextZ) is NestBlock)
            {
                TurnBack();
                hasTarget = false;
                return;
            }
            // -------------------------------------------

            float foot = GetFootOffset();
            float groundY = yTop + surfaceOffset;

            targetPos = new Vector3(nextX, groundY + foot, nextZ);
            hasTarget = true;
    }

    void SnapToSurfaceAtCurrentXZ()
    {
        int x = Mathf.RoundToInt(transform.position.x);
        int z = Mathf.RoundToInt(transform.position.z);

        x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
        z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

        if (!TryGetTopSolidBlockY(x, z, out int yTop))
            return;

        float foot = GetFootOffset();
        transform.position = new Vector3(x + 0.5f, yTop + surfaceOffset + foot, z + 0.5f);

    }

    // -------------------- Turning --------------------

    void SetRandomCardinalRotation()
    {
        int a = CardinalAngles[Random.Range(0, CardinalAngles.Length)];
        transform.rotation = Quaternion.Euler(0f, a, 0f);
    }

    void TurnLeftRightOrBack_Smooth()
    {
        turnTimer += Time.deltaTime;
        if (turnTimer < turnInterval) return;
        turnTimer = 0f;

        if (Random.value > turnChance) return;

        int[] delta = { -90, 90, 180 };
        int d = delta[Random.Range(0, delta.Length)];

        float newYaw = SnapYaw(transform.eulerAngles.y + d);
        targetRot = Quaternion.Euler(0f, newYaw, 0f);
    }

    float SnapYaw(float yaw)
    {
        yaw = Mathf.Repeat(yaw, 360f);
        return Mathf.Round(yaw / 90f) * 90f;
    }

    float GetFootOffset()
    {
        var col = GetComponent<Collider>();
        return col ? col.bounds.extents.y : 0f;
    }
    void checkBlock()
    {
        AbstractBlock block =
    WorldManager.Instance.GetBlock(
        CurrentBlock.x,
        CurrentBlock.y,
        CurrentBlock.z
    );
        Debug.Log("Block: " + block);
        if (block is MulchBlock)
        {
            WorldManager.Instance.SetBlock(
            CurrentBlock.x,
            CurrentBlock.y,
            CurrentBlock.z,
            new AirBlock()
        );
            currentState = AntState.Returning;

        }
    }
void ReturnToQueen()

{
    if (queen == null) return;
    // Move toward queen in XZ only
        Vector3 toQueen = queen.position - transform.position;
        // Debug.Log("QUeen: " + toQueen.x + " " + toQueen.z);
        toQueen.y = 0f;

    if (toQueen.magnitude < 1.0f)   // tweak radius as needed
    {
        Debug.Log("Reached queen");

        currentState = AntState.Wandering;
        return;
    }

    Vector3 dir = toQueen.normalized;

    // Face queen smoothly (XZ only)
    targetRot = Quaternion.LookRotation(dir);
    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

    // Step toward queen in XZ
    Vector3 next = transform.position + dir * (moveSpeed * Time.deltaTime);

    // Clamp to world bounds (optional but recommended)
    next.x = Mathf.Clamp(next.x, 1f, WorldManager.Instance.WorldSizeX - 2f);
    next.z = Mathf.Clamp(next.z, 1f, WorldManager.Instance.WorldSizeZ - 2f);

        // Ground the ant at the new XZ
        // int gx = Mathf.RoundToInt(next.x);
        // int gz = Mathf.RoundToInt(next.z);
    int gx = Mathf.FloorToInt(next.x);
    int gz = Mathf.FloorToInt(next.z);

    if (IsNestAtXZ(gx, gz))
        {
            // Can't go onto nest: turn away and stop returning
            TurnBack();
            currentState = AntState.Wandering;
            hasTarget = false;
            return;
        }

    if (TryGetTopSolidBlockY(gx, gz, out int yTop))
    {
        float foot = GetFootOffset();
        next.y = yTop + surfaceOffset + foot;
        transform.position = next;

        // Keep your "current block" updated while returning
        CurrentBlock = new Vector3Int(gx, yTop, gz);
        CurrentGroundY = yTop;
    }
    else
    {
        // If no ground found, don't move (prevents drifting into void)
        // Optionally: turn around or switch back to wandering
    }
}
bool IsNestBlockAt(int x, int y, int z)
{
    // Change ContainerBlock to your real nest block type if needed
    return WorldManager.Instance.GetBlock(x, y, z) is ContainerBlock;
}

bool IsNestAtXZ(int x, int z)
{
    if (!TryGetTopSolidBlockY(x, z, out int yTop))
        return false;

    return IsNestBlockAt(x, yTop, z);
}

void TurnBack()
{
    float newYaw = SnapYaw(transform.eulerAngles.y + 180f);
    targetRot = Quaternion.Euler(0f, newYaw, 0f);
}


}
