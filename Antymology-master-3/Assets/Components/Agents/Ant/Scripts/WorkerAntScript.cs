
using UnityEngine;
using Antymology.Terrain;

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

    private Rigidbody rb;
    private CapsuleCollider cap;

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


    void Awake()
    {
        queen = null;
        int layer = gameObject.layer;
        Physics.IgnoreLayerCollision(layer, layer, true);
        groundMask = LayerMask.GetMask("Ground");
        health = maxHealth;



        if (queen == null)
        {
            GameObject q = GameObject.FindWithTag("Queen");
            if (q != null)
            {
                queen = q.transform;
            }
        }


        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();

        if (rb == null)
            Debug.LogError("No Rigidbody on ant!");

        // start wandering immediately
        nextDecisionTime = Time.time + changeDirInterval;
    }

    void Update()
    {
        DrainHealthOverTime();
        if (health <= 0f)
        {
            Die();
            return;
        }


        // Safety check
        if (queen == null || WorldManager.Instance == null)
            return;
        // Prevent steering from overriding forced wall turns
        bool allowSteering = Time.time >= forcedTurnUntilTime;




        // Acid avoidance (highest priority steering)
        if (allowSteering)
        {
            // Acid avoidance
            if (Genome.avoidAcid > 0f)
            {
                Vector3 away = ComputeAcidAvoidanceVector();
                if (away != Vector3.zero)
                {
                    Vector3 desired = Vector3.Slerp(transform.forward, away, Genome.avoidAcid);
                    FacePosition(transform.position + desired);
                }
            }

            // Nest avoidance
            if (nestAvoidStrength > 0f)
            {
                Vector3 awayNest = ComputeNestAvoidanceVector();
                if (awayNest != Vector3.zero)
                {
                    Vector3 desired = Vector3.Slerp(transform.forward, awayNest, nestAvoidStrength);
                    FacePosition(transform.position + desired);
                }
            }
        }


if (carryingFood)
{
    Vector3 toQueen = (queen.position - transform.position);
    toQueen.y = 0f;

    Vector3 steer = toQueen.normalized;

    // add avoidance while carrying
    if (Genome.avoidAcid > 0f)
    {
        Vector3 awayAcid = ComputeAcidAvoidanceVector();
        if (awayAcid != Vector3.zero) steer += awayAcid * Genome.avoidAcid;
    }

    if (nestAvoidStrength > 0f)
    {
        Vector3 awayNest = ComputeNestAvoidanceVector();
        if (awayNest != Vector3.zero) steer += awayNest * nestAvoidStrength;
    }

    if (steer.sqrMagnitude > 0.0001f)
        FacePosition(transform.position + steer); // smooth turn, doesn't hard-snap

    TryFeedQueen();
    targetMulchTile = null;
    return;
}


        // Retarget mulch periodically (keep your existing logic)
        // Acquire / maintain mulch target
        if (!targetMulchTile.HasValue)
        {
            targetMulchTile = FindNearestMulchTile(transform.position);

            if (targetMulchTile.HasValue)
            {
                // initialize stuck tracking when we pick a new target
                stuckDeadline = Time.time + stuckSeconds;
                lastTargetDist = float.PositiveInfinity;
            }
        }
        else
        {
            // If the tile is gone, drop target
            Vector3Int t = targetMulchTile.Value;
            var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);

            if (!(b is MulchBlock))
            {
                targetMulchTile = null;
            }
        }



        if (!carryingFood && targetMulchTile.HasValue)
        {
            Vector3Int tt = targetMulchTile.Value;
            Vector3 targetPos = new Vector3(tt.x + 0.5f, tt.y + 0.5f, tt.z + 0.5f);


            FacePosition(targetPos);

            // ---- STUCK CHECK ----
            float d = Vector3.Distance(transform.position, targetPos);

            // if we made progress, extend deadline
            if (lastTargetDist - d > stuckMinProgress)
            {
                stuckDeadline = Time.time + stuckSeconds;
                lastTargetDist = d;
            }
            else if (Time.time > stuckDeadline)
            {
                // give up on unreachable target
                targetMulchTile = null;
                lastTargetDist = float.PositiveInfinity;
            }
        }




        // Random wandering decision: schedule a pause + a 45-degree step turn
        // Random wandering decision: schedule a pause, and sometimes a 45-degree step turn
        if (Time.time >= nextDecisionTime)
        {
            // stop for a bit
            pauseUntilTime = Time.time + pauseDuration;

            // decide if we actually turn this time
            // Debug.Log("TUrnCHance: " + turnChance);
            if (Random.value < turnChance)
            {

                // pick a random multiple of 45 degrees (-180..180)
                int stps = Random.Range(-4, 5); // inclusive -4..4

                // optional: avoid 0-degree "turn"
                if (stps == 0) stps = (Random.value < 0.5f) ? -1 : 1;

                queuedTurnDegrees = stps * turnStepDegrees;
                hasQueuedTurn = true;
            }
            else
            {
                hasQueuedTurn = false; // no turn this cycle
            }

            nextDecisionTime = Time.time + changeDirInterval;
        }



        // Apply the turn once when the pause ends
        if (hasQueuedTurn && Time.time >= pauseUntilTime)
        {
            transform.Rotate(0f, queuedTurnDegrees, 0f);
            hasQueuedTurn = false;
        }



        // Interactions (keep)
        TryPickupMulch();
        TryFeedQueen();
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

    void FixedUpdate()
    {
        if (rb == null || cap == null) return;

        // Reset nest brake flag every physics tick
        brakeForNestThisFixedTick = false;

        // -------------------------------------------------
        // 1) VERY CLOSE NEST CHECK (front brake + forced turn)
        // -------------------------------------------------
        Vector3 origin = transform.position + Vector3.up * 0.25f;

        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, nestBrakeDistance, groundMask))
        {
            var block = GetBlockFromHit(hit);

            if (block is NestBlock)
            {
                brakeForNestThisFixedTick = true;

                // Rotate away on cooldown so we don't spin every tick
                if (Time.time >= nextAllowedTwoBlockTurnTime)
                {
                    nextAllowedTwoBlockTurnTime = Time.time + twoBlockTurnCooldown;

                    int steps = Random.Range(-4, 5);
                    if (steps == 0)
                        steps = (Random.value < 0.5f) ? -1 : 1;

                    // Use Rigidbody rotation since movement is physics-based
                    rb.MoveRotation(
                        Quaternion.Euler(0f, steps * turnStepDegrees, 0f) * rb.rotation
                    );

                    // Brief pause so it doesn't immediately push back in
                    pauseUntilTime = Time.time + 0.10f;

                    // Prevent Update steering from overriding this turn
                    forcedTurnUntilTime = Time.time + 0.15f;
                }
            }
        }

        // -------------------------------------------------
        // 2) STEP UP / WALL HANDLING
        // -------------------------------------------------
        TryStepUp();

        // -------------------------------------------------
        // 3) IF NEST IS BLOCKING, DO NOT MOVE FORWARD
        // -------------------------------------------------
        if (brakeForNestThisFixedTick)
            return;

        // -------------------------------------------------
        // 4) RESPECT PAUSE WINDOWS
        // -------------------------------------------------
        if (Time.time < pauseUntilTime)
            return;

        // -------------------------------------------------
        // 5) NORMAL FORWARD MOVEMENT
        // -------------------------------------------------
        Vector3 forwardMove =
            transform.forward * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + forwardMove);
    }


    private void TryPickupMulch()
    {
        if (carryingFood) return;
        if (!targetMulchTile.HasValue) return;
        Vector3Int t = targetMulchTile.Value;
        Vector3 tileCenter = (Vector3)targetMulchTile.Value;
        float dist = Vector3.Distance(transform.position, tileCenter);

        if (dist < 1.0f)
        {
            // Vector3Int t = targetMulchTile.Value;

            if (!WorldManager.Instance.TryClaimMulch(t))
            {
                // Someone else has it -> don’t pile up here forever
                targetMulchTile = null;
                stuckDeadline = Time.time + stuckSeconds;
                lastTargetDist = float.PositiveInfinity;
                return;
            }


            var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
            if (b is MulchBlock)
            {
                if (UnityEngine.Random.value <= digProbability)
                {
                    carryingFood = true;

                    // Remove the mulch
                    // Remove the mulch
                    WorldManager.Instance.RecordRemovedMulch(t);

                    WorldManager.Instance.SetBlock(t.x, t.y, t.z, new AirBlock());


                    Fitness += 2f;

                    // Gain health for collecting mulch
                    health = Mathf.Min(maxHealth, health + healthGainOnPickup);
                    // Debug.Log($"Picked up mulch at {t}! Health: {health} --- Fitness: {Fitness}");
                }
            }



            WorldManager.Instance.ReleaseMulchClaim(t);
            targetMulchTile = null;
        }
    }

    void TryFeedQueen()
{
    if (!carryingFood || queen == null) return;

    float dist = Vector3.Distance(transform.position, queen.position);
        if (dist < 1.0f)
        {
            carryingFood = false;


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
            // Debug.Log($"Delivered food to queen! Total delivered: {DeliveredCount} --- Fitness: {Fitness} ");

    }
}


    private Vector3Int? FindNearestMulchTile(Vector3 worldPos)
    {
        int cx = Mathf.RoundToInt(worldPos.x);
        int cy = Mathf.RoundToInt(worldPos.y);
        int cz = Mathf.RoundToInt(worldPos.z);

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

        Debug.Log($"{label} hit {b?.GetType().Name ?? "null"} at tile {t}, hitPoint {hit.point}");
    }

    void TryStepUp()
    {
        if (rb == null || cap == null)
        {
            Debug.LogWarning("StepUp: Missing Rigidbody or CapsuleCollider");
            return;
        }

        Vector3 forwardOffset = transform.forward * (cap.radius + 0.02f);
        float feetY = cap.bounds.min.y + 0.05f;

        Vector3 lowerOrigin = new Vector3(transform.position.x, feetY, transform.position.z) + forwardOffset;
        Vector3 upperOrigin = lowerOrigin + Vector3.up * stepHeight;
        // Debug.DrawRay(lowerOrigin, transform.forward * stepCheckDist, Color.red);
        // Debug.DrawRay(upperOrigin, transform.forward * stepCheckDist, Color.blue);
        // bool hitLower = Physics.Raycast(lowerOrigin, transform.forward, stepCheckDist, groundMask);
        // bool hitUpper = Physics.Raycast(upperOrigin, transform.forward, stepCheckDist, groundMask);
        bool hitLower = Physics.Raycast(lowerOrigin, transform.forward, out RaycastHit lowerHit, stepCheckDist, groundMask);
        bool hitUpper = Physics.Raycast(upperOrigin, transform.forward, out RaycastHit upperHit, stepCheckDist, groundMask);



        // 1-block step → climbable
        if (hitLower && !hitUpper)
        {
            Vector3 step =
                Vector3.up * stepUpAmount +
                transform.forward * stepForwardAmount;

            rb.MovePosition(rb.position + step);
            return;
        }
        if (hitLower && hitUpper)
        {
            var lowerBlock = GetBlockFromHit(lowerHit);

            // Nest wall handling
            if (lowerBlock is NestBlock)
            {
                blockedByNestThisTick = true;
                // Still blocked by nest; don't try to step forward into it
                if (Time.time < nextAllowedTwoBlockTurnTime)
                    return;

                nextAllowedTwoBlockTurnTime = Time.time + twoBlockTurnCooldown;
                forcedTurnUntilTime = Time.time + 0.15f; // stop Update() from snapping rotation back

                int stps = Random.Range(-4, 5);
                if (stps == 0) stps = (Random.value < 0.5f) ? -1 : 1;

                // If you have rb available, prefer this:
                rb.MoveRotation(Quaternion.Euler(0f, stps * turnStepDegrees, 0f) * rb.rotation);
                // Otherwise:
                // transform.Rotate(0f, stps * turnStepDegrees, 0f);

                Debug.Log($"NEST TURN: {stps * turnStepDegrees} at t={Time.time}");
                return;
            }

            // Non-nest 2-block wall (your normal logic)...
            if (Time.time < nextAllowedTwoBlockTurnTime)
                return;

            nextAllowedTwoBlockTurnTime = Time.time + twoBlockTurnCooldown;

            int steps = Random.Range(-4, 5);
            if (steps == 0) steps = (Random.value < 0.5f) ? -1 : 1;

            rb.MoveRotation(Quaternion.Euler(0f, steps * turnStepDegrees, 0f) * rb.rotation);
            return;
        }







    }

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
                Mathf.RoundToInt(transform.position.x),
                Mathf.RoundToInt(transform.position.y),
                Mathf.RoundToInt(transform.position.z)
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


private void Die()
{
    // If you have an evolution manager tracking ants, notify it here.
    Destroy(gameObject);
}

}

