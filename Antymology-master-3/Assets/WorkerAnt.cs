using UnityEngine;
using Antymology.Terrain;
public enum AntState
{
    Wandering,
    Digging,
    Returning
}
public enum FitnessEvent
{
    ConsumedMulch,
    AliveTick,
    AcidTick,
    DeliveredHealthToQueen
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
    public LayerMask groundMask;


    [Header("Debug / State")]
    public Vector3Int CurrentBlock;     // block the ant is currently on (x,y,z)
    public float CurrentGroundY;        // detected ground surface height (world y)
    public AntState currentState = AntState.Wandering;
    private Transform queen;
    Vector3Int reservedTile;
    bool hasReservation;

    float stepTimer;
    float turnTimer;

    Vector3 targetPos;
    Quaternion targetRot;
    bool hasTarget;

    // static readonly int[] CardinalAngles = { 0, 90, 180, 270 };
    static readonly int[] OctileAngles = { 0, 45, 90, 135, 180, 225, 270, 315 };

    [Header("Health")]
    public float maxHealth = 100f;
    public float health = 100f;

// Every X seconds, lose Y health
    public float healthTickSeconds = 1f;   // 3 minutes
    public float healthLossPerTick = 10f;   // set to 100 to die after 1 tick, or smaller to die gradually

    float healthTimer;
    [Header("Fitness")]
    public int fitness = 0;
[Header("Health Transfer")]
public float deliverAmount = 10f;     // how much the ant tries to give the queen per delivery
public float queenDeliverRadius = 1.5f; // must be close to queen
[Header("Mulch Healing")]
public float mulchHealAmount = 10f;   // gain this when consuming mulch

float carriedHealthFromMulch = 0f;    // how much “energy” this ant is carrying to queen

    // Evaluation timestep (seconds). 0.2 = 5 ticks/sec, 1.0 = 1 tick/sec
    public float evalTickSeconds = 1.0f;

    float evalTimer;

    [Header("Genome (per ant)")]
    public WorkerGenome genome;
    [SerializeField] private bool genomeAssigned = false;

    void Start()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        healthTimer = 0f;

        groundMask = LayerMask.GetMask("Ground");
        SetRandomOctileRotation();

        targetRot = transform.rotation;

        SnapToSurfaceAtCurrentXZ();
        targetPos = transform.position;
        hasTarget = false;

        // Detect initial block
        UpdateCurrentBlockFromWorld();
        if (queen == null && WorldManager.Instance != null)
            queen = WorldManager.Instance.QueenTransform;
        // NEW: If we are very close to the queen, look away from her immediately
        if (queen != null && Vector3.Distance(transform.position, queen.position) < 2.0f)
        {
            Vector3 away = transform.position - queen.position;
            if (away.sqrMagnitude > 0.01f)
            {
                float angle = Quaternion.LookRotation(away).eulerAngles.y;
                targetRot = Quaternion.Euler(0, SnapYaw(angle), 0);
                transform.rotation = targetRot;
            }
        }
        else
        {
            SetRandomOctileRotation(); // Your old logic for ants spawned elsewhere
        }
        stepTimer = Random.Range(0f, stepInterval);
        reservedTile = CurrentBlock;
        hasReservation = WorldManager.Instance.TryReserveTile(reservedTile);

    }

void Update()
{
    UpdateHealthOverTime();
    UpdateFitnessTick();

    // Always rotate smoothly toward whatever targetRot currently is
    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

    // If we're currently walking toward a chosen target tile, keep moving toward it
    if (hasTarget)
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

        if ((transform.position - targetPos).sqrMagnitude < 0.000001f)
        {
            transform.position = targetPos;
            hasTarget = false;

            UpdateCurrentBlockFromWorld();

            // Only do mulch checks while wandering (prevents consuming mulch while returning)
            if (currentState == AntState.Wandering)
            {
                checkBlock();
            }
            else if (currentState == AntState.Returning)
            {
                // If we stepped close enough to the queen, deliver and stop returning
                CheckReachedQueen();
            }
        }
        return;
    }

    // Not currently moving: decide facing behavior
    if (currentState == AntState.Returning)
        FaceQueen_Smooth();
    else
        TurnLeftRightOrBack_Smooth();

    // Step gating (THIS is the built-in idle time)
    stepTimer += Time.deltaTime;
    if (stepTimer >= stepInterval)
    {
        stepTimer = 0f;

        // SPEED TRAIT: probability to actually take a step this tick
        if (Random.value > genome.moveChance)
            return;

        if (currentState == AntState.Returning)
            StepTowardQueen_SmoothTarget();
        else
            StepForwardToSurface_SmoothTarget();
    }

}
void FaceQueen_Smooth()
{
    if (queen == null) return;

    Vector3 toQueen = queen.position - transform.position;
    toQueen.y = 0f;

    if (toQueen.sqrMagnitude < 0.0001f) return;

    targetRot = Quaternion.LookRotation(toQueen.normalized);
}

void CheckReachedQueen()
{
    if (queen == null) return;

    Vector3 toQueen = queen.position - transform.position;
    toQueen.y = 0f;

    if (toQueen.magnitude < 1.0f) // same threshold you used before
    {
        TryDeliverHealthToQueen();
        currentState = AntState.Wandering;
    }
}

void StepTowardQueen_SmoothTarget()
{
    if (queen == null) return;

    Vector3 toQueen = queen.position - transform.position;
    toQueen.y = 0f;

    // Already close enough? Deliver without stepping.
    if (toQueen.magnitude < 1.0f)
    {
        TryDeliverHealthToQueen();
        currentState = AntState.Wandering;
        return;
    }

    // Choose a cardinal step that reduces distance (grid-like)
    int stepX = 0, stepZ = 0;
    if (Mathf.Abs(toQueen.x) > Mathf.Abs(toQueen.z))
        stepX = toQueen.x >= 0 ? 1 : -1;
    else
        stepZ = toQueen.z >= 0 ? 1 : -1;

    int curX = Mathf.FloorToInt(transform.position.x);
    int curZ = Mathf.FloorToInt(transform.position.z);

    int nextX = Mathf.Clamp(curX + stepX, 1, WorldManager.Instance.WorldSizeX - 2);
    int nextZ = Mathf.Clamp(curZ + stepZ, 1, WorldManager.Instance.WorldSizeZ - 2);

    // Don't step onto the nest
    if (IsNestAtXZ(nextX, nextZ))
    {
        TurnBack();
        currentState = AntState.Wandering;
        return;
    }

    if (!TryGetTopSolidBlockY(nextX, nextZ, out int yTop))
        return;

    Vector3Int nextTile = new Vector3Int(nextX, yTop, nextZ);

        // Respect tile reservation like wandering does
        // if (!WorldManager.Instance.TryReserveTile(nextTile))
        if (!WorldManager.Instance.TryReserveTile(nextTile) && currentState == AntState.Wandering)

    {
        TurnBack();
        return;
    }

    if (hasReservation)
        WorldManager.Instance.ReleaseTile(reservedTile);

    reservedTile = nextTile;
    hasReservation = true;

    float foot = GetFootOffset();
    float groundY = yTop + surfaceOffset;

    // Set target position (movement will happen via hasTarget MoveTowards)
    DoMove(new Vector3(nextX, groundY + foot, nextZ));

    // Face the step direction
    Vector3 stepDir = new Vector3(stepX, 0f, stepZ);
    if (stepDir.sqrMagnitude > 0.0001f)
        targetRot = Quaternion.LookRotation(stepDir);
}

    // -------------------- Detection --------------------

    /// <summary>
    /// Detects the block beneath the ant and updates CurrentBlock (x,y,z) using real world state.
    /// Uses raycast first; if that fails, falls back to voxel scan.
    /// </summary>
    public void UpdateCurrentBlockFromWorld()
    {
        int x = Mathf.FloorToInt(transform.position.x);
        int z = Mathf.FloorToInt(transform.position.z);

        x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
        z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

        // Raycast just to confirm we're over ground (optional)
        if (TryGetGroundByRaycast(x, z, out float hitY))
            CurrentGroundY = hitY;

        // Always use voxel data to decide which block we're standing on
        if (TryGetTopSolidBlockY(x, z, out int yTop))
        {
            CurrentBlock = new Vector3Int(x, yTop, z);
            return;
        }

        CurrentBlock = new Vector3Int(x, 0, z);
        CurrentGroundY = 0f;
    }

    bool TryGetGroundByRaycast(int x, int z, out float groundY)
    {
        // Cast from above down to terrain at the center of the tile
        Vector3 origin = new Vector3(x + 0.5f, WorldManager.Instance.WorldSizeY + 5f, z + 0.5f);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, WorldManager.Instance.WorldSizeY + 20f, groundMask))
        {
            // Debug.Log("Raycast hit: " + hit.collider.name
            // + " layer=" + hit.collider.gameObject.layer
            // + " tag=" + hit.collider.tag);


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
        // 1. Calculate Step Direction
        Vector3 f = transform.forward;
        // Simple logic to snap forward vector to grid direction
        int stepX = (f.x > 0.35f) ? 1 : (f.x < -0.35f) ? -1 : 0;
        int stepZ = (f.z > 0.35f) ? 1 : (f.z < -0.35f) ? -1 : 0;

        if (stepX == 0 && stepZ == 0) return;

        int curX = Mathf.FloorToInt(transform.position.x);
        int curZ = Mathf.FloorToInt(transform.position.z);

        int nextX = Mathf.Clamp(curX + stepX, 1, WorldManager.Instance.WorldSizeX - 2);
        int nextZ = Mathf.Clamp(curZ + stepZ, 1, WorldManager.Instance.WorldSizeZ - 2);

        // 2. Check Ground (Don't walk into void)
        if (!TryGetTopSolidBlockY(nextX, nextZ, out int yTop))
        {
            TurnRandom();
            return;
        }

        // 3. NEST LOGIC: Allow exiting the nest, forbid re-entering
        bool amIOnNest = IsNestAtXZ(curX, curZ);
        if (IsNestAtXZ(nextX, nextZ) && !amIOnNest)
        {
            TurnRandom();
            hasTarget = false;
            return;
        }

        // 4. RESERVATION & TRAFFIC LOGIC
        Vector3Int nextTile = new Vector3Int(nextX, yTop, nextZ);
        float distToQueen = (queen != null) ? Vector3.Distance(transform.position, queen.position) : 0f;
        bool inSafeZone = distToQueen < 6.0f; // Slightly increased radius

        if (!inSafeZone)
        {
            // Try to reserve the tile
            if (!WorldManager.Instance.TryReserveTile(nextTile) && currentState == AntState.Wandering)
            {
                // --- FIX 2: QUEUEING LOGIC ---
                // If the path is blocked, usually we should just WAIT for the ant in front to move.
                // Turning immediately creates a clog.

                // 80% chance to just wait (return) and try again next tick
                if (Random.value < 0.8f) return;

                // 20% chance to give up and turn (prevents getting stuck forever)
                TurnRandom();
                hasTarget = false;
                return;
            }
        }

        // 5. Manage Reservation
        if (hasReservation)
            WorldManager.Instance.ReleaseTile(reservedTile);

        // Only lock the tile if we are outside the ghosting area
        if (!inSafeZone)
        {
            reservedTile = nextTile;
            hasReservation = true;
        }
        else
        {
            hasReservation = false;
        }

        // 6. Move
        float foot = GetFootOffset();
        float groundY = yTop + surfaceOffset;
        targetPos = new Vector3(nextX, groundY + foot, nextZ);

        DoMove(targetPos);
    }
    void DoMove(Vector3 pos){

        targetPos = pos;

        hasTarget = true;

    }



    void SnapToSurfaceAtCurrentXZ()
    {
        int x = Mathf.FloorToInt(transform.position.x);
        int z = Mathf.FloorToInt(transform.position.z);


        x = Mathf.Clamp(x, 1, WorldManager.Instance.WorldSizeX - 2);
        z = Mathf.Clamp(z, 1, WorldManager.Instance.WorldSizeZ - 2);

        if (!TryGetTopSolidBlockY(x, z, out int yTop))
            return;

        float foot = GetFootOffset();
        transform.position = new Vector3(x + 0.5f, yTop + surfaceOffset + foot, z + 0.5f);

    }

    // -------------------- Turning --------------------

        void SetRandomOctileRotation()
    {
        int a = OctileAngles[Random.Range(0, OctileAngles.Length)];
        transform.rotation = Quaternion.Euler(0f, a, 0f);
    }


    void TurnLeftRightOrBack_Smooth()
    {
        turnTimer += Time.deltaTime;
        if (turnTimer < turnInterval) return;
        turnTimer = 0f;

        if (Random.value > turnChance) return;

        // int[] delta = { -90, 90, 180 };
        int[] delta = { -45, 45, -90, 90, -135, 135, 180 };

        int d = delta[Random.Range(0, delta.Length)];

        float newYaw = SnapYaw(transform.eulerAngles.y + d);
        targetRot = Quaternion.Euler(0f, newYaw, 0f);
    }

    float SnapYaw(float yaw)
{
    yaw = Mathf.Repeat(yaw, 360f);
    return Mathf.Round(yaw / 45f) * 45f;
}



    float GetFootOffset()
    {
        var col = GetComponent<Collider>();
        return col ? col.bounds.extents.y : 0f;
    }
    void checkBlock()
    {

        int x = Mathf.FloorToInt(transform.position.x);
        int z = Mathf.FloorToInt(transform.position.z);

        TryGetTopSolidBlockY(x, z, out int yTopFromScan);
        var blockFromScan = WorldManager.Instance.GetBlock(x, yTopFromScan, z);
        var blockFromCurrent = WorldManager.Instance.GetBlock(CurrentBlock.x, CurrentBlock.y, CurrentBlock.z);

        // Debug.Log($"pos={transform.position} scan=({x},{yTopFromScan},{z}) {blockFromScan} current={CurrentBlock} {blockFromCurrent}");

        AbstractBlock block =
    WorldManager.Instance.GetBlock(
        CurrentBlock.x,
        CurrentBlock.y,
        CurrentBlock.z
    );
        // Debug.Log("Block: " + block);
        if (block is MulchBlock)
        {
            // Only one ant is allowed to claim this mulch tile

            // Debug.Log($"{name} trying to claim mulch at {CurrentBlock}");

            if (!WorldManager.Instance.TryClaimMulch(CurrentBlock))
            {
                Debug.Log($"{name} FAILED to claim mulch at {CurrentBlock}");
                return;

            }
            AddFitness(FitnessEvent.ConsumedMulch);
                        // Gain health from eating mulch
            health = Mathf.Min(maxHealth, health + mulchHealAmount);

            // Mark this amount as “carried” so we can donate half to queen later
            carriedHealthFromMulch += mulchHealAmount;


            // Debug.Log($"{name} SUCCESSFULLY claimed mulch at {CurrentBlock}");


            WorldManager.Instance.SetBlock(CurrentBlock.x, CurrentBlock.y, CurrentBlock.z, new AirBlock());
            currentState = AntState.Returning;

            // Optional: release the claim immediately since the block is gone
            WorldManager.Instance.ReleaseMulchClaim(CurrentBlock);
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
            // Debug.Log("Reached queen");

            TryDeliverHealthToQueen();
            currentState = AntState.Wandering;
            return;
        }

        Vector3 dir = toQueen.normalized;

        // Face queen smoothly (XZ only)
        targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        // Step toward queen in XZ
        Vector3 next = transform.position + dir * (moveSpeed * Time.deltaTime);
        // Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

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
        if (y < 0 || y >= WorldManager.Instance.WorldSizeY) return false;

        AbstractBlock b = WorldManager.Instance.GetBlock(x, y, z);

        // Put ALL blocks you want to forbid here:
        return b is NestBlock || b is ContainerBlock;
    }

    bool IsNestAtXZ(int x, int z)
    {
        if (!TryGetTopSolidBlockY(x, z, out int yTop))
            return false;

        return IsNestBlockAt(x, yTop, z) || IsNestBlockAt(x, yTop + 1, z);
    }

    void TurnBack()
    {
        float newYaw = SnapYaw(transform.eulerAngles.y + 90f);
        targetRot = Quaternion.Euler(0f, newYaw, 0f);
    }
void UpdateHealthOverTime()
{
                // Debug.Log("Check Fitness: " + fitness);

    if (health <= 0f) return;

    healthTimer += Time.deltaTime;
    if (healthTimer >= healthTickSeconds)
    {
        healthTimer -= healthTickSeconds;

        health -= healthLossPerTick;
        if (health <= 0f)
        {
                Debug.Log("Check Fitness: " + fitness);
            Die();
        }
    }
}
void Die()
{
    // If you implemented tile reservations, release them here:
    if (hasReservation) WorldManager.Instance.ReleaseTile(reservedTile);

    Destroy(gameObject);
}
void UpdateFitnessTick()
{
    evalTimer += Time.deltaTime;
    if (evalTimer < evalTickSeconds) return;
    evalTimer -= evalTickSeconds;
    AddFitness(FitnessEvent.AliveTick);
    // +1 every timestep alive

    // -2 every timestep on acid (if standing on AcidicBlock)
    if (IsStandingOnAcid())
        fitness -= 2;
}

    bool IsStandingOnAcid()
    {
        // CurrentBlock should already be the top solid block under the ant
        AbstractBlock b = WorldManager.Instance.GetBlock(CurrentBlock.x, CurrentBlock.y, CurrentBlock.z);
        return b is AcidicBlock;
    }
    void AddFitness(FitnessEvent reason)
    {
        switch (reason)
        {
            case FitnessEvent.ConsumedMulch:
                fitness += 2;
                break;

            case FitnessEvent.AliveTick:
                fitness += 1;
                break;

            case FitnessEvent.AcidTick:
                fitness -= 5;
                break;

            case FitnessEvent.DeliveredHealthToQueen:
                fitness += 3;
                break;
        }
        // Debug.Log("Reason: " + reason + " -- " + fitness + " == Health: " + health);

    }
    bool TryDeliverHealthToQueen()
    {
        if (WorldManager.Instance == null) return false;
        if (WorldManager.Instance.Queen == null) return false;

        QueenAnt q = WorldManager.Instance.Queen;

        // close enough?
        if ((q.transform.position - transform.position).sqrMagnitude > queenDeliverRadius * queenDeliverRadius)
            return false;

        // only donate if we actually have carried mulch-health
        if (carriedHealthFromMulch <= 0f) return false;

        float toGive = carriedHealthFromMulch * 0.5f;

        // queen may not accept all if full
        float before = q.health;
        bool accepted = q.TryReceiveHealth(toGive);
        if (!accepted) return false;

        float acceptedAmount = q.health - before;
        if (acceptedAmount <= 0f) return false;

        // Reduce carried amount by what queen actually accepted *2?
        // We intended to give half of carried. If queen only accepts part, remove that part from carried.
        carriedHealthFromMulch -= acceptedAmount;

        // Important: this should be zero-sum from the ant’s health too.
        // Since you already added the mulch heal to ant health, donating should reduce ant health.
        health -= acceptedAmount;
        if (health < 0f) health = 0f;

        AddFitness(FitnessEvent.DeliveredHealthToQueen);
        return true;
    }

    // Replace your existing TurnBack() with this:
    void TurnRandom()
    {
        // Pick a random direction (left, right, or all the way back)
        // This breaks the "infinite loop" of trying the same blocked path
        float[] candidates = { -90f, 90f, 180f, -45f, 45f };
        float randomAdd = candidates[Random.Range(0, candidates.Length)];

        float newYaw = SnapYaw(transform.eulerAngles.y + randomAdd);
        targetRot = Quaternion.Euler(0f, newYaw, 0f);
    }
public void SetGenome(WorkerGenome g)
{
    genome = g;
    genomeAssigned = true;

    // Optional: directly map one trait to existing variable today
    deliverAmount = genome.transferAmount;

    // Optional debug:
    // Debug.Log($"{name} genome: moveChance={genome.moveChance:F2}, sense={genome.senseRadius:F1}, acidAvoid={genome.acidAvoidance:F2}, transfer={genome.transferAmount:F1}, digExplore={genome.diggingExploration:F2}");
}

void OnDestroy()
{
    if (hasReservation)
        WorldManager.Instance.ReleaseTile(reservedTile);
}


}
