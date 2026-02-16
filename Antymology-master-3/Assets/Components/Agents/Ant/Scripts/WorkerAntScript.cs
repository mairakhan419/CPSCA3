
using UnityEngine;
using Antymology.Terrain;
using System.Collections.Generic;

public class WorkerAntScript : MonoBehaviour
{
    [Header("References")]
    public Transform queen;              // assign in inspector OR find by tag
    public string foodTag = "Food";      // tag your food objects with "Food"

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float digProbability = 0f;

    public float turnSpeed = 180f; // not used in wander, but you can keep it

    [Header("Voxel Food Sensing")]
    public int searchRadius = 6;

    private Vector3Int? targetMulchTile;

    public float retargetSeconds = 0.5f;
    private float nextRetargetTime;

    [Header("Step Up")]
    public float stepHeight = 10.0f;       // set to your voxel step height
    public float stepCheckDist = 1f;
    public float stepUpAmount = 1f;        // how much to lift per step attempt
    public float stepForwardAmount = 0.10f; // small forward nudge
    public LayerMask groundMask = ~6;

    [Header("Random Wander")]
    public float changeDirInterval = 3f;   // how often we pick a new direction
    public float pauseDuration = 0.25f;    // how long we stop before turning
    public int turnStepDegrees = 45;       // keep at 45 for your request

    private float nextDecisionTime;
    private float pauseUntilTime;
    private int queuedTurnDegrees;
    private bool hasQueuedTurn;

    // private Rigidbody rb;
    // private CapsuleCollider cap;
// /
    private bool carryingFood;
    public float Fitness { get; private set; }
    public int DeliveredCount { get; private set; }
    [Range(0f, 1f)]
    public float turnChance = 0.35f; // 35% chance to turn each decision
    public float turnChanceTwoBlocks;

    [Header("Genome (runtime)")]
    public AntGenome Genome { get; private set; }


    [Header("Health")]
    public float maxHealth = 100f;
    public float healthDrainPerSecond = 1f;
    public float health { get; private set; }

    public float healthGainOnPickup = 50f;
    public float healthGivenToQueen = 10f;
    [Header("2-block turn control")]
    public float twoBlockTurnCooldown = 0.35f;
    private float nextAllowedTwoBlockTurnTime = 0f;
    private bool wasBlockedByTwoBlock = false;
    [Header("Target Filtering")]
    public int maxUpStepsToTarget = 1;   // 1 = only target at same height or 1 block up
    public int maxDownStepsToTarget = 3; // optional
    [Header("Stuck handling")]
    public float stuckSeconds = 1.5f;
    public float stuckMinProgress = 0.25f; // must reduce distance by this much to count as progress

    private float stuckDeadline;
    private float lastTargetDist = float.PositiveInfinity;
    private float forcedTurnUntilTime = 0f;

    private bool blockedByNestThisTick = false;

    [Header("Nest Avoidance")]
    public float nestAvoidStrength = 1.0f;     // how strongly we steer away (0..1 recommended)
    public int nestSenseRadius = 2;            // in blocks
    public float nestBrakeDistance = 0.8f;     // if nest is this close in front, don't move forward this tick

    private bool brakeForNestThisFixedTick = false;

    [Header("Grid Teleport Movement")]
    public float moveInterval = 1f;   // how often we hop to a new tile
    public int maxTeleportBlocks = 3;    // your "as long as it's shorter than 3 blocks"
    public bool allowDiagonal = false;   // optional
    private float nextMoveTime;
    private Vector3Int gridDir = new Vector3Int(1, 0, 0); // current grid heading

    // private Vector3 TileCenter(Vector3Int t) => new Vector3(t.x, t.y-0.5f, t.z);
    private Vector3 TileCenter(Vector3Int t) => new Vector3(t.x, t.y - 0.5f, t.z);

    public bool simpleForwardOnly = true;
    [Header("Post-delivery cooldown")]
    public int postDeliveryForwardTicks = 30;   // how many grid moves to go straight after delivering
    private int postDeliveryTicksLeft = 0;

    private bool InPostDeliveryCooldown => postDeliveryTicksLeft > 0;

[Header("Visuals")]
public Renderer antRenderer;   // assign in inspector (or auto-find)
    public Color targetMulchColor = Color.magenta; // purple
    private int blockedStreak = 0;
private int blockedTotal = 0;
private float nextBlockedLogTime = 0f;

private int blockedSinceMove = 0;
private float nextStuckLogTime = 0f;

private readonly Dictionary<(int x, int z), int> topYCache = new();
private int GetTopYCached(int x, int z)
{
    var key = (x, z);
    if (topYCache.TryGetValue(key, out int y)) return y;

    y = FindTopSolidY(x, z);
    topYCache[key] = y;
    return y;
}

    void Awake()
    {
        queen = null;
        int layer = gameObject.layer;
        // // Physics.IgnoreLayerCollision(layer, layer, true);
        // groundMask = LayerMask.GetMask("Ground");
        health = maxHealth;



        if (queen == null)
        {
            GameObject q = GameObject.FindWithTag("Queen");
            if (q != null)
            {
                queen = q.transform;
            }
        }


        // rb = GetComponent<Rigidbody>();
        // cap = GetComponent<CapsuleCollider>();

        // if (rb == null)
        //     Debug.LogError("No Rigidbody on ant!");

        // start wandering immediately
        nextMoveTime = Time.time + moveInterval + Random.Range(0f, moveInterval);
        nextDecisionTime = Time.time + changeDirInterval + Random.Range(0f, changeDirInterval);
        nextRetargetTime = Time.time + Random.Range(0f, retargetSeconds);



    }

    void Update()
    {
        // Debug.Log("Carrying food: " + carryingFood);
        DrainHealthOverTime();
        if (health <= 0f) { Die(); return; }

        if (queen == null || WorldManager.Instance == null)
            return;

        // bool allowSteering = Time.time >= forcedTurnUntilTime;

        // 1) steering (acid/nest) - keep as-is
        // if (allowSteering)
        // {
        //     if (Genome.avoidAcid > 0f)
        //     {
        //         Vector3 away = ComputeAcidAvoidanceVector();
        //         if (away != Vector3.zero)
        //             FacePosition(transform.position + Vector3.Slerp(transform.forward, away, Genome.avoidAcid));
        //     }

        //     if (nestAvoidStrength > 0f)
        //     {
        //         Vector3 awayNest = ComputeNestAvoidanceVector();
        //         if (awayNest != Vector3.zero)
        //             FacePosition(transform.position + Vector3.Slerp(transform.forward, awayNest, nestAvoidStrength));
        //     }
        // }

        bool allowSteering = Time.time >= forcedTurnUntilTime;

        if (!simpleForwardOnly && allowSteering)
        {
            if (Genome.avoidAcid > 0f)
            {
                Vector3 away = ComputeAcidAvoidanceVector();
                if (away != Vector3.zero)
                    FacePosition(transform.position + Vector3.Slerp(transform.forward, away, Genome.avoidAcid));
            }

            if (nestAvoidStrength > 0f)
            {
                Vector3 awayNest = ComputeNestAvoidanceVector();
                if (awayNest != Vector3.zero)
                    FacePosition(transform.position + Vector3.Slerp(transform.forward, awayNest, nestAvoidStrength));
            }
        }


        // 2) mode behavior
        if (carryingFood)
        {
            // face target if we have one
            if (!simpleForwardOnly && targetMulchTile.HasValue && Time.time >= forcedTurnUntilTime)
            {
                Vector3Int tt = targetMulchTile.Value;
                Vector3 targetPos = new Vector3(tt.x + 0.5f, tt.y + 0.5f, tt.z + 0.5f);
                FacePosition(targetPos);
            }


            // while carrying, ignore mulch targeting
            targetMulchTile = null;

            TryFeedQueen();
        }
        else
        {

            // NEW: after delivering, go forward-only for a bit and do NOT target mulch
            if (InPostDeliveryCooldown)
            {
                targetMulchTile = null;
                // Don't dig / interact either
            }
            else
            {
                // acquire / maintain mulch target
                if (!targetMulchTile.HasValue)
                {
                    if (Time.time >= nextRetargetTime)
                    {
                        nextRetargetTime = Time.time + retargetSeconds;

                        targetMulchTile = FindNearestMulchTile(transform.position);
                        if (targetMulchTile.HasValue)
                        {
                            stuckDeadline = Time.time + stuckSeconds;
                            lastTargetDist = float.PositiveInfinity;
                        }
                    }
                }

                else
                {
                    Vector3Int t = targetMulchTile.Value;
                    var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
                    if (b is not MulchBlock) targetMulchTile = null;
                }

                // face target if we have one
                if (!simpleForwardOnly && targetMulchTile.HasValue)
                {
                    Vector3Int tt = targetMulchTile.Value;
                    Vector3 targetPos = new Vector3(tt.x + 0.5f, tt.y + 0.5f, tt.z + 0.5f);
                    FacePosition(targetPos);
                }

                TryDigGrassUnderfoot();
                TryInteractWithBlock();
            }
        }


        // 3) wander turning decisions - keep your turn code here (unchanged)
        // (your nextDecisionTime / pause / rotate code)

        // 4) ONE place where movement happens (always)
        if (Time.time >= nextMoveTime)
        {
            nextMoveTime = Time.time + moveInterval;
            GridMoveTick();

            // NEW: count down “forward only” moves
            if (postDeliveryTicksLeft > 0)
                postDeliveryTicksLeft--;
        }


    }

        // void OnCollisionEnter(Collision c)
    // {
    //     if (c.collider.CompareTag("Ant"))
    //     {
    //         Debug.Log("Collided with another ant, ignoring collision to prevent blocking.");
    //         Physics.IgnoreCollision(cap, c.collider, true);
    //     }
    // }

    public void Initialize(AntGenome genome, Transform queenRef)
    {
        topYCache.Clear();
        blockedStreak = 0;
        health = maxHealth;

        Genome = genome;

        // Apply genes to behaviour knobs you already have
        moveSpeed = genome.moveSpeed;
        turnChance = genome.turnChance;
        // changeDirInterval = genome.changeDirInterval;
        turnChanceTwoBlocks = genome.turnChanceTwoBlocks;
        pauseDuration = genome.pauseDuration;
        searchRadius = genome.searchRadius;
        digProbability = genome.digProbability;

        queen = queenRef;

        // reset per-eval stats
        Fitness = 0f;
        DeliveredCount = 0;
        carryingFood = false;

        // restart wandering timers so genes take effect immediately
        nextDecisionTime = Time.time + changeDirInterval;
        pauseUntilTime = 0f;
        hasQueuedTurn = false;
    }

    // void FixedUpdate()
    // {
    //     if (rb == null || cap == null) return;

    //     // Reset nest brake flag every physics tick
    //     brakeForNestThisFixedTick = false;

    //     // -------------------------------------------------
    //     // 1) VERY CLOSE NEST CHECK (front brake + forced turn)
    //     // -------------------------------------------------
    //     Vector3 origin = transform.position + Vector3.up * 0.25f;

    //     if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, nestBrakeDistance, groundMask))
    //     {
    //         var block = GetBlockFromHit(hit);

    //         if (block is NestBlock)
    //         {
    //             brakeForNestThisFixedTick = true;

    //             // Rotate away on cooldown so we don't spin every tick
    //             if (Time.time >= nextAllowedTwoBlockTurnTime)
    //             {
    //                 nextAllowedTwoBlockTurnTime = Time.time + twoBlockTurnCooldown;

    //                 int steps = Random.Range(-4, 5);
    //                 if (steps == 0)
    //                     steps = (Random.value < 0.5f) ? -1 : 1;

    //                 // Use Rigidbody rotation since movement is physics-based
    //                 rb.MoveRotation(
    //                     Quaternion.Euler(0f, steps * turnStepDegrees, 0f) * rb.rotation
    //                 );

    //                 // Brief pause so it doesn't immediately push back in
    //                 pauseUntilTime = Time.time + 0.10f;

    //                 // Prevent Update steering from overriding this turn
    //                 forcedTurnUntilTime = Time.time + 0.15f;
    //             }
    //         }
    //     }

    //     // -------------------------------------------------
    //     // 2) STEP UP / WALL HANDLING
    //     // -------------------------------------------------
    //     TryStepUp();

    //     // -------------------------------------------------
    //     // 3) IF NEST IS BLOCKING, DO NOT MOVE FORWARD
    //     // -------------------------------------------------
    //     if (brakeForNestThisFixedTick)
    //         return;

    //     // -------------------------------------------------
    //     // 4) RESPECT PAUSE WINDOWS
    //     // -------------------------------------------------
    //     if (Time.time < pauseUntilTime)
    //         return;

    //     // -------------------------------------------------
    //     // 5) NORMAL FORWARD MOVEMENT
    //     // -------------------------------------------------
    //     Vector3 forwardMove =
    //         transform.forward * moveSpeed * Time.fixedDeltaTime;

    //     rb.MovePosition(rb.position + forwardMove);
    // }

    private void TryInteractWithBlock()
    {
        if (carryingFood) return;
        if (!targetMulchTile.HasValue) return;

        Vector3Int t = targetMulchTile.Value;
        Vector3 tileCenter = new Vector3(t.x + 0.5f, t.y + 0.5f, t.z + 0.5f);

        if (Vector3.Distance(transform.position, tileCenter) < 1.0f)
        {
            var block = WorldManager.Instance.GetBlock(t.x, t.y, t.z);

            // Only interact with mulch targets here
            if (block is MulchBlock)
            {
                if (!WorldManager.Instance.TryClaimMulch(t))
                {
                    targetMulchTile = null;
                    stuckDeadline = Time.time + stuckSeconds;
                    lastTargetDist = float.PositiveInfinity;
                    return;
                }

                // IMPORTANT: No digProbability for mulch
                carryingFood = true;
health = Mathf.Min(maxHealth, health + healthGainOnPickup);
                // Prefer a WorldManager method that records + removes (see WorldManager section)
                WorldManager.Instance.RemoveMulchBlock(t);

                targetMulchTile = null;
            }
            else
            {
                // Target is no longer mulch (got removed/changed)
                targetMulchTile = null;
            }

            stuckDeadline = Time.time + stuckSeconds;
            lastTargetDist = float.PositiveInfinity;
        }
    }
    private void TryDigGrassUnderfoot()
    {
        if (WorldManager.Instance == null) return;

        // Typically the block under the ant’s feet is y - 1
        Vector3Int t = CurrentTile();
        Vector3Int under = new Vector3Int(t.x, t.y - 1, t.z);

        var b = WorldManager.Instance.GetBlock(under.x, under.y, under.z);
        if (b is not GrassBlock) return;

        if (UnityEngine.Random.value <= digProbability)
        {
            WorldManager.Instance.RemoveGrassBlock(under);
            // Optional: reward fitness, up to you
            // Fitness += 0.5f;
        }
    }





    // private void TryPickupMulch()
    // {
    //     if (carryingFood) return;
    //     if (!targetMulchTile.HasValue) return;
    //     Vector3Int t = targetMulchTile.Value;
    //     Vector3 tileCenter = (Vector3)targetMulchTile.Value;
    //     float dist = Vector3.Distance(transform.position, tileCenter);

    //     if (dist < 1.0f)
    //     {
    //         // Vector3Int t = targetMulchTile.Value;

    //         if (!WorldManager.Instance.TryClaimMulch(t))
    //         {
    //             // Someone else has it -> don’t pile up here forever
    //             targetMulchTile = null;
    //             stuckDeadline = Time.time + stuckSeconds;
    //             lastTargetDist = float.PositiveInfinity;
    //             return;
    //         }


    //         var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
    //         if (b is MulchBlock)
    //         {
    //             if (UnityEngine.Random.value <= digProbability)
    //             {
    //                 carryingFood = true;

    //                 // Remove the mulch
    //                 // Remove the mulch
    //                 WorldManager.Instance.RecordRemovedMulch(t);

    //                 WorldManager.Instance.SetBlock(t.x, t.y, t.z, new AirBlock());


    //                 Fitness += 2f;

    //                 // Gain health for collecting mulch
    //                 health = Mathf.Min(maxHealth, health + healthGainOnPickup);
    //                 // Debug.Log($"Picked up mulch at {t}! Health: {health} --- Fitness: {Fitness}");
    //             }
    //         }



    //         WorldManager.Instance.ReleaseMulchClaim(t);
    //         targetMulchTile = null;
    //     }
    // }

    void TryFeedQueen()
{
    if (!carryingFood || queen == null) return;

        // float dist = Vector3.Distance(transform.position, queen.position);
        //     if (dist < 1.0f)
        Vector3Int antTile = CurrentTile();
        Vector3Int queenTile = WorldToTile(queen.position);

        int manhattan =
            Mathf.Abs(antTile.x - queenTile.x) +
            Mathf.Abs(antTile.z - queenTile.z);

        if (manhattan <= 3)
        {
            carryingFood = false;


            postDeliveryTicksLeft = postDeliveryForwardTicks;

            // Clear any target so we don't immediately resume targeting
            targetMulchTile = null;

            // Optional: prevent any steering snaps right after delivery
            forcedTurnUntilTime = Time.time + moveInterval * 0.25f;

            var queenScript = queen.GetComponent<QueenAntScript>();
            if (queenScript != null)
            {
                float amt = Mathf.Min(healthGivenToQueen, health);
                float accepted = queenScript.TryReceiveHealth(amt);
                health -= accepted; // subtract exactly what she took
                if (health < 0f) health = 0f;
            }

            DeliveredCount++;
            Fitness += 10f;
            // forwardOnlyUntilTime = Time.time + forwardAfterDeliverySeconds;
            // Debug.Log($"Delivered food to queen! Total delivered: {DeliveredCount} --- Fitness: {Fitness} ");

        }
}


    private Vector3Int? FindNearestMulchTile(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.x);
        int cy = Mathf.FloorToInt(worldPos.y);
        int cz = Mathf.FloorToInt(worldPos.z);

        float bestDistSq = float.PositiveInfinity;
        Vector3Int? best = null;

        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        for (int dz = -searchRadius; dz <= searchRadius; dz++)
        for (int dy = -2; dy <= 2; dy++)
        {
            int x = cx + dx;
            int y = cy + dy;
            int z = cz + dz;

            // height filter (key part)
            int deltaY = y - cy;
            if (deltaY > maxUpStepsToTarget) continue;
            if (-deltaY > maxDownStepsToTarget) continue;

            var b = WorldManager.Instance.GetBlock(x, y, z);
            if (b is not MulchBlock) continue;

            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 < bestDistSq)
            {
                bestDistSq = d2;
                best = new Vector3Int(x, y, z);
            }
        }

        return best;
    }

    // Put these helpers anywhere in WorkerAntScript (class scope)
    private Vector3Int WorldToTile(Vector3 p)
    {
        // Hit points will be on the surface; nudge slightly inward so we pick the block we struck.
        // (If your chunk/world origin is offset, adjust here.)
        return new Vector3Int(
            Mathf.FloorToInt(p.x),
            Mathf.FloorToInt(p.y),
            Mathf.FloorToInt(p.z)
        );
    }
    private AbstractBlock GetBlockFromHit(RaycastHit hit)
    {
        if (WorldManager.Instance == null) return null;

        // Nudge inside the hit surface so we choose the block we struck
        Vector3 inside = hit.point - hit.normal * 0.01f;

        Vector3Int t = new Vector3Int(
            Mathf.FloorToInt(inside.x),
            Mathf.FloorToInt(inside.y),
            Mathf.FloorToInt(inside.z)
        );

        return WorldManager.Instance.GetBlock(t.x, t.y, t.z);
    }

    private void LogHitBlock(string label, RaycastHit hit)
    {
        if (WorldManager.Instance == null) return;

        // Move a tiny bit back along the ray direction so we land inside the collider/block we hit
        Vector3 inside = hit.point - hit.normal * 0.01f;

        Vector3Int t = WorldToTile(inside);
        var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);

    }

    // void TryStepUp()
    // {
    //     // if (rb == null || cap == null)
    //     // {
    //     //     Debug.LogWarning("StepUp: Missing Rigidbody or CapsuleCollider");
    //     //     return;
    //     // }

    //     Vector3 forwardOffset = transform.forward * (cap.radius + 0.02f);
    //     float feetY = cap.bounds.min.y + 0.05f;

    //     Vector3 lowerOrigin = new Vector3(transform.position.x, feetY, transform.position.z) + forwardOffset;
    //     Vector3 upperOrigin = lowerOrigin + Vector3.up * stepHeight;
    //     // Debug.DrawRay(lowerOrigin, transform.forward * stepCheckDist, Color.red);
    //     // Debug.DrawRay(upperOrigin, transform.forward * stepCheckDist, Color.blue);
    //     // bool hitLower = Physics.Raycast(lowerOrigin, transform.forward, stepCheckDist, groundMask);
    //     // bool hitUpper = Physics.Raycast(upperOrigin, transform.forward, stepCheckDist, groundMask);
    //     bool hitLower = Physics.Raycast(lowerOrigin, transform.forward, out RaycastHit lowerHit, stepCheckDist, groundMask);
    //     bool hitUpper = Physics.Raycast(upperOrigin, transform.forward, out RaycastHit upperHit, stepCheckDist, groundMask);



    //     // 1-block step → climbable
    //     if (hitLower && !hitUpper)
    //     {
    //         Vector3 step =
    //             Vector3.up * stepUpAmount +
    //             transform.forward * stepForwardAmount;

    //         rb.MovePosition(rb.position + step);
    //         return;
    //     }
    //     if (hitLower && hitUpper)
    //     {
    //         var lowerBlock = GetBlockFromHit(lowerHit);

    //         // Nest wall handling
    //         if (lowerBlock is NestBlock)
    //         {
    //             blockedByNestThisTick = true;
    //             // Still blocked by nest; don't try to step forward into it
    //             if (Time.time < nextAllowedTwoBlockTurnTime)
    //                 return;

    //             nextAllowedTwoBlockTurnTime = Time.time + twoBlockTurnCooldown;
    //             forcedTurnUntilTime = Time.time + 0.15f; // stop Update() from snapping rotation back

    //             int stps = Random.Range(-4, 5);
    //             if (stps == 0) stps = (Random.value < 0.5f) ? -1 : 1;

    //             // If you have rb available, prefer this:
    //             rb.MoveRotation(Quaternion.Euler(0f, stps * turnStepDegrees, 0f) * rb.rotation);
    //             // Otherwise:
    //             // transform.Rotate(0f, stps * turnStepDegrees, 0f);

    //             Debug.Log($"NEST TURN: {stps * turnStepDegrees} at t={Time.time}");
    //             return;
    //         }

    //         // Non-nest 2-block wall (your normal logic)...
    //         if (Time.time < nextAllowedTwoBlockTurnTime)
    //             return;

    //         nextAllowedTwoBlockTurnTime = Time.time + twoBlockTurnCooldown;

    //         int steps = Random.Range(-4, 5);
    //         if (steps == 0) steps = (Random.value < 0.5f) ? -1 : 1;

    //         rb.MoveRotation(Quaternion.Euler(0f, steps * turnStepDegrees, 0f) * rb.rotation);
    //         return;
    //     }







    // }

    void FaceQueen()
    {
        if (queen == null) return;

        Vector3 toQueen = queen.position - transform.position;
        toQueen.y = 0f; // keep flat turning only
        if (toQueen.sqrMagnitude < 0.0001f) return;

        // Snap turn (simple and effective)
        transform.rotation = Quaternion.LookRotation(toQueen.normalized);

        // If you want smooth turn instead, use:
        // Quaternion target = Quaternion.LookRotation(toQueen.normalized);
        // transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }
    void FacePosition(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(to.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }




private bool IsAcidAt(Vector3Int t)
{
    var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
    return b is AcidicBlock;
}

    // Returns a steering vector AWAY from nearby acid, or Vector3.zero if none found
    private Vector3 ComputeAcidAvoidanceVector()
    {
        int r = Mathf.Max(1, Genome.acidSenseRadius);
        Vector3Int c = CurrentTile();

        Vector3 away = Vector3.zero;
        int count = 0;

        // sample same level + a bit around (you can widen dy if needed)
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector3Int t = new Vector3Int(c.x + dx, c.y + dy, c.z + dz);
                    if (!IsAcidAt(t)) continue;

                    // push away from acid tile (closer acid pushes more)
                    Vector3 diff = (Vector3)c - (Vector3)t;
                    float d2 = diff.sqrMagnitude + 0.001f;
                    away += diff / d2;

                    count++;
                }

        if (count == 0) return Vector3.zero;

        away.y = 0f;
        return away.normalized;
    }

        private Vector3Int CurrentTile()
        {
            return new Vector3Int(
                Mathf.FloorToInt(transform.position.x),
                Mathf.FloorToInt(transform.position.y),
                Mathf.FloorToInt(transform.position.z)
            );
        }

        private bool IsStandingOnAcid()
        {
            Vector3Int t = CurrentTile();
            var b = WorldManager.Instance.GetBlock(t.x, t.y - 1, t.z);
            // If your ants' pivot is already at ground level, remove the -1.
            return b is AcidicBlock;
        }

private void DrainHealthOverTime()
{
    float mult = IsStandingOnAcid() ? 2f : 1f; // assignment rule

    health -= healthDrainPerSecond * mult * Time.deltaTime;
    if (health < 0f) health = 0f;
}
    private bool IsNestAt(Vector3Int t)
    {
        var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
        return b is NestBlock;
    }


    // Steering vector away from nearby nest blocks
    private Vector3 ComputeNestAvoidanceVector()
    {
        int r = Mathf.Max(1, nestSenseRadius);
        Vector3Int c = CurrentTile();

        Vector3 away = Vector3.zero;
        int count = 0;

        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector3Int t = new Vector3Int(c.x + dx, c.y + dy, c.z + dz);
                    if (!IsNestAt(t)) continue;

                    Vector3 diff = (Vector3)c - (Vector3)t;
                    float d2 = diff.sqrMagnitude + 0.001f;

                    away += diff / d2;
                    count++;
                }

        if (count == 0) return Vector3.zero;

        away.y = 0f;
        return away.normalized;
    }

    private Vector3Int CurrentTileFloor()
    {
        // Floor works better for voxel grids than Round for consistent centers.
        return new Vector3Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y),
            Mathf.FloorToInt(transform.position.z)
        );
    }


    private bool IsAir(Vector3Int t)
    {
        var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
        return b is AirBlock;
    }


    private bool IsSolid(Vector3Int t)
    {
        var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
        return b is not AirBlock;
    }


    private bool IsWalkable(Vector3Int dest)
    {
        if (!IsAir(dest)) return false;

        Vector3Int below = new Vector3Int(dest.x, dest.y - 1, dest.z);
        if (!IsSolid(below)) return false;

        // // Optional: avoid stepping into nest tiles (or near them)
        // var underBlock = WorldManager.Instance.GetBlock(below.x, below.y, below.z);
        // if (underBlock is NestBlock) return false;

        return true;
    }


    // Checks the vertical constraint you already use for targeting (step up/down limits)
    private bool RespectsStepLimits(Vector3Int from, Vector3Int to)
    {
        int dy = to.y - from.y;
        if (dy > maxUpStepsToTarget) return false;
        if (-dy > maxDownStepsToTarget) return false;
        return true;
    }

    // private void GridMoveTick()
    // {
    //     if (WorldManager.Instance == null) return;

    //     // respect pause window from your wander logic
    //     if (Time.time < pauseUntilTime) return;

    //     Vector3Int cur = CurrentTileFloor();

    //     // Decide where we want to go
    //     Vector3Int desired;

    //     if (carryingFood && queen != null)
    //     {
    //         desired = StepToward(cur, WorldToTile(queen.position));
    //     }
    //     else if (targetMulchTile.HasValue)
    //     {
    //         desired = StepToward(cur, targetMulchTile.Value);
    //     }
    //     else
    //     {
    //         desired = StepForward(cur);
    //     }

    //     // Teleport
    //     transform.position = TileCenter(desired);
    // }

    // Takes up to maxTeleportBlocks in one hop, but will fall back to shorter hops if blocked.
    private void GridMoveTick()
    {
        Vector3Int cur = CurrentTile();
        if (!carryingFood &&
    !targetMulchTile.HasValue &&
    // !InPostDeliveryCooldown &&
    Time.time >= forcedTurnUntilTime)
        {

            TryRandomTurn();
        }
        Vector3Int dir;

        if (carryingFood)
        {
            // Debug.Log(1);
            dir = DirTowardQueen(cur);
        }
        else if (InPostDeliveryCooldown)
        {
            // Debug.Log(2);

            dir = ForwardToGridDir();
        }
        else if (targetMulchTile.HasValue)
        {

            Vector3Int mulch = targetMulchTile.Value;
            Vector3Int goalStand = new Vector3Int(mulch.x, mulch.y + 1, mulch.z); // stand above mulch
            dir = DirTowardTile(cur, goalStand);
        }
        else
        {
            // Debug.Log(4);

            dir = ForwardToGridDir();
        }

        if (dir == Vector3Int.zero)
        {
            // We are aligned in XZ with the target column.
            // Try to interact; if we can't, clear target or force a turn.
            if (targetMulchTile.HasValue)
            {
                // If you're close enough, TryInteractWithBlock() will pick it up.
                TryInteractWithBlock();

                // If still targeting and we didn't pick it up, we’re “stuck on column”.
                // Clear it so we resume wandering instead of freezing.
                if (targetMulchTile.HasValue)
                    targetMulchTile = null;

                // Force a turn so we don't immediately re-lock
                HandleBlocked("alignedColumnNoInteract");
                return;
            }

            // Otherwise, just continue forward
            dir = ForwardToGridDir();
        }




        int cx = cur.x;
        int cz = cur.z;

        int nx = cx + dir.x;
        int nz = cz + dir.z;

        // 1. Bounds check
        if (nx < 0 || nx >= WorldManager.Instance.WorldSizeX ||
            nz < 0 || nz >= WorldManager.Instance.WorldSizeZ)
        {
            HandleBlocked("bounds");
            return;
        }

        int curTopY = GetTopYCached(cx, cz);
        int nextTopY = GetTopYCached(nx, nz);


        // 2. Fall off world check
        if (curTopY < 0 || nextTopY < 0)
        {
            HandleBlocked("noGround");
            return;
        }

        // 3. Height/Obstacle Check
        // If the step is too high, or too deep of a drop
        int heightDiff = nextTopY - curTopY;
        // Debug.Log("Height Diff"+ heightDiff);
        // if (heightDiff > maxUpStepsToTarget || -heightDiff > maxDownStepsToTarget)
        if (heightDiff > maxUpStepsToTarget)
        {
            // OBSTACLE DETECTED!
             HandleBlocked($"heightDiff={heightDiff}");
            return;
        }

        // 4. Container check
        if (IsContainer(nx, nextTopY, nz) || IsContainer(nx, nextTopY + 1, nz))
        {
            HandleBlocked("container");
            return;
        }

        // 5. Success! Move the ant.
        Vector3Int standTile = new Vector3Int(nx, nextTopY + 1, nz);
        transform.position = TileCenter(standTile);

        // Reset stuck trackers because we successfully advanced
        blockedStreak = 0;
        blockedSinceMove = 0;
    }

private void TryRandomTurn()
{
        // Debug.Log("Trying Random Turn");
    if (Random.value <= turnChance)
        {
            RandomTurn90();

            // Prevent steering from instantly snapping back
            forcedTurnUntilTime = Time.time + (moveInterval * 0.5f);
        }
}

private void RandomTurn90()
{
    // 50/50 left or right
    float angle = (Random.value < 0.5f) ? -90f : 90f;

    transform.Rotate(0f, angle, 0f);
}


    private Vector3Int ForwardToGridDir()
    {
        Vector3 f = transform.forward;

        if (Mathf.Abs(f.x) >= Mathf.Abs(f.z))
            return new Vector3Int(f.x >= 0f ? 1 : -1, 0, 0);
        else
            return new Vector3Int(0, 0, f.z >= 0f ? 1 : -1);
    }


    private Vector3Int DirTowardQueen(Vector3Int cur)
{
    if (queen == null) return ForwardToGridDir();

    Vector3Int q = WorldToTile(queen.position);
    int dx = q.x - cur.x;
    int dz = q.z - cur.z;

    // If same tile, keep moving forward (or pick a random direction)
    if (dx == 0 && dz == 0)
    {
        return (Random.value < 0.5f)
            ? new Vector3Int(1, 0, 0)
            : new Vector3Int(0, 0, 1);
    }

    if (Mathf.Abs(dx) >= Mathf.Abs(dz))
        return new Vector3Int(dx > 0 ? 1 : -1, 0, 0);
    else
        return new Vector3Int(0, 0, dz > 0 ? 1 : -1);
}





    private Vector3Int StepToward(Vector3Int cur, Vector3Int goal)
    {
        Vector3Int best = cur;

        // try stride 3,2,1 (your requirement)
        for (int stride = maxTeleportBlocks; stride >= 1; stride--)
        {
            Vector3Int candidate = cur;

            int dx = goal.x - cur.x;
            int dz = goal.z - cur.z;

            int stepX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int stepZ = dz == 0 ? 0 : (dz > 0 ? 1 : -1);

            if (!allowDiagonal)
            {
                // pick the dominant axis
                if (Mathf.Abs(dx) >= Mathf.Abs(dz))
                    candidate.x += stepX * stride;
                else
                    candidate.z += stepZ * stride;
            }
            else
            {
                candidate.x += stepX * stride;
                candidate.z += stepZ * stride;
            }

            // handle y by sampling nearby y offsets (0, +1, -1, etc) within your limits
            // choose first walkable that respects step limits
            for (int dy = -maxDownStepsToTarget; dy <= maxUpStepsToTarget; dy++)
            {
                Vector3Int c2 = new Vector3Int(candidate.x, cur.y + dy, candidate.z);

                if (!RespectsStepLimits(cur, c2)) continue;
                if (!IsWalkable(c2)) continue;

                return c2;
            }
        }

        // If we couldn't move toward goal, try turning via your existing queued turns
        return StepForward(cur);
    }

    private Vector3Int StepForward(Vector3Int cur)
    {
        // Convert forward to a grid direction
        Vector3 f = transform.forward;
        Vector3Int dir;

        if (Mathf.Abs(f.x) >= Mathf.Abs(f.z))
            dir = new Vector3Int(f.x >= 0 ? 1 : -1, 0, 0);
        else
            dir = new Vector3Int(0, 0, f.z >= 0 ? 1 : -1);

        // stride 3,2,1 forward
        for (int stride = maxTeleportBlocks; stride >= 1; stride--)
        {
            Vector3Int candidate = cur + new Vector3Int(dir.x * stride, 0, dir.z * stride);

            for (int dy = -maxDownStepsToTarget; dy <= maxUpStepsToTarget; dy++)
            {
                Vector3Int c2 = new Vector3Int(candidate.x, cur.y + dy, candidate.z);

                if (!RespectsStepLimits(cur, c2)) continue;
                if (!IsWalkable(c2)) continue;

                return c2;
            }
        }

        // blocked: stay in place (your wander turn logic will rotate eventually)
        return cur;
    }
    private int FindTopSolidY(int x, int z)
    {
        // Scan from top down to find the highest non-air block
        for (int y = WorldManager.Instance.WorldSizeY - 1; y >= 0; y--)
        {
            var b = WorldManager.Instance.GetBlock(x, y, z);
            if (b is not AirBlock)
                return y;
        }
        return -1; // nothing found
    }


    private bool IsContainer(int x, int y, int z)
    {
        return WorldManager.Instance.GetBlock(x, y, z) is ContainerBlock;
    }

    private int GroundYAt(int x, int z)
    {
        return FindTopSolidY(x, z);
    }

    // Helper to handle turning and suppressing the Queen-facing logic
// private void HandleBlocked()
// {
//     blockedStreak++;
//     blockedTotal++;

//     // Log only when the ant is clearly stuck, and throttle logs
//     if (blockedStreak >= 6 && Time.time >= nextBlockedLogTime)
//     {
//         nextBlockedLogTime = Time.time + 1.0f; // log at most once per second per ant

//         Vector3Int cur = CurrentTile();
//         Debug.LogWarning(
//             $"[ANT STUCK] name={name} id={GetInstanceID()} " +
//             $"blockedStreak={blockedStreak} blockedTotal={blockedTotal} " +
//             $"pos={transform.position} tile={cur} " +
//             $"carrying={carryingFood} postCooldown={InPostDeliveryCooldown} " +
//             $"target={(targetMulchTile.HasValue ? targetMulchTile.Value.ToString() : "none")}"
//         );
//     }

//     // Cache recovery
//     if (blockedStreak >= 3)
//     {
//         topYCache.Clear();
//         blockedStreak = 0;
//     }

//     RandomTurn90();
//     forcedTurnUntilTime = Time.time + (moveInterval * 1.5f);
// }

private void HandleBlocked(string reason = "")
{
    blockedStreak++;
    blockedSinceMove++;

    if (blockedSinceMove >= 8 && Time.time >= nextStuckLogTime)
    {
        nextStuckLogTime = Time.time + 1.0f; // throttle per ant

        Vector3Int cur = CurrentTile();
        Debug.LogWarning(
            $"[ANT STUCK] {reason} name={name} id={GetInstanceID()} " +
            $"blockedSinceMove={blockedSinceMove} cacheStreak={blockedStreak} " +
            $"tile={cur} pos={transform.position} " +
            $"carrying={carryingFood} cooldown={InPostDeliveryCooldown} " +
            $"target={(targetMulchTile.HasValue ? targetMulchTile.Value.ToString() : "none")}"
        );
    }

    // cache heal (does NOT reset blockedSinceMove)
    if (blockedStreak >= 3)
    {
        topYCache.Clear();
        blockedStreak = 0;
    }

    RandomTurn90();
    forcedTurnUntilTime = Time.time + (moveInterval * 1.5f);
}



    private Vector3Int DirTowardTile(Vector3Int cur, Vector3Int goal)
    {
        int dx = goal.x - cur.x;
        int dz = goal.z - cur.z;
        int dy = goal.y - cur.y;
        // Debug.Log("DirTowardTile: dx=" + dx + " dz=" + dz + " dy=" + dy);

        if (dx == 0 && dz == 0) return Vector3Int.zero;

        if (Mathf.Abs(dx) >= Mathf.Abs(dz))
            return new Vector3Int(dx > 0 ? 1 : -1, 0, 0);
        else
            return new Vector3Int(0, 0, dz > 0 ? 1 : -1);
    }


private void Die()
    {
        // Debug.Log("Dead");
        // If you have an evolution manager tracking ants, notify it here.
        Destroy(gameObject);
    }

}

