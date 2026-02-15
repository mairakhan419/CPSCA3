
using UnityEngine;
using Antymology.Terrain;

public class WorkerAntScript : MonoBehaviour
{
    [Header("References")]
    public Transform queen;              // assign in inspector OR find by tag
    public string foodTag = "Food";      // tag your food objects with "Food"

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float turnSpeed = 180f; // not used in wander, but you can keep it

    [Header("Voxel Food Sensing")]
    public int searchRadius = 6;

    private Vector3Int? targetMulchTile;

    public float retargetSeconds = 0.5f;
    private float nextRetargetTime;

    [Header("Step Up")]
    public float stepHeight = 10.0f;       // set to your voxel step height
    public float stepCheckDist = 4f;
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

    void Awake()
    {
        groundMask = LayerMask.GetMask("Ground");
        health = maxHealth;



        if (queen == null)
        {
            GameObject q = GameObject.FindWithTag("Queen");
            if (q != null) queen = q.transform;
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
        Debug.Log("HEalth: " + health);
        DrainHealthOverTime();
        if (health <= 0f)
        {
            Die();
            return;
        }
        // Safety check
        if (queen == null || WorldManager.Instance == null)
            return;
        // Acid avoidance (highest priority steering)
        if (Genome.avoidAcid > 0f)
        {
            Vector3 away = ComputeAcidAvoidanceVector();
            if (away != Vector3.zero)
            {
                // Blend current forward with away vector based on genome strength
                Vector3 desired = Vector3.Slerp(transform.forward, away, Genome.avoidAcid);
                FacePosition(transform.position + desired); // uses your FacePosition helper
            }
        }

        if (carryingFood)
        {
            FaceQueen();

            // still allow interactions (feeding queen)
            TryFeedQueen();

            // optional: stop retargeting mulch while carrying
            targetMulchTile = null;

            return; // skip wandering + pickup logic
        }
        // Retarget mulch periodically (keep your existing logic)
        if (!targetMulchTile.HasValue || Time.time >= nextRetargetTime)
        {
            targetMulchTile = FindNearestMulchTile(transform.position);
            nextRetargetTime = Time.time + retargetSeconds;
        }
        if (!carryingFood && targetMulchTile.HasValue)
        {
            // Use tile center (Vector3Int casts to corner-ish coords; +0.5 puts you in the middle)
            Vector3 targetPos = (Vector3)targetMulchTile.Value + new Vector3(0.5f, 0f, 0.5f);

            FacePosition(targetPos);
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
                int steps = Random.Range(-4, 5); // inclusive -4..4

                // optional: avoid 0-degree "turn"
                if (steps == 0) steps = (Random.value < 0.5f) ? -1 : 1;

                queuedTurnDegrees = steps * turnStepDegrees;
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
        // TryFeedQueen();
    }

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
        Debug.Log("Search Radius: " + searchRadius);

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

        TryStepUp();

        // If we're still pausing, don't move forward
        if (Time.time < pauseUntilTime)
            return;

        // Move forward
        Vector3 forwardMove = transform.forward * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + forwardMove);
    }

    private void TryPickupMulch()
    {
        if (carryingFood) return;
        if (!targetMulchTile.HasValue) return;

        Vector3 tileCenter = (Vector3)targetMulchTile.Value;
        float dist = Vector3.Distance(transform.position, tileCenter);

        if (dist < 1.0f)
        {
            Vector3Int t = targetMulchTile.Value;

            if (!WorldManager.Instance.TryClaimMulch(t))
                return;

            var b = WorldManager.Instance.GetBlock(t.x, t.y, t.z);
            if (b is MulchBlock)
            {
                carryingFood = true;
                Debug.Log($"Ant {name} picked up mulch at {t}");

                WorldManager.Instance.SetBlock(t.x, t.y, t.z, new AirBlock());
                Fitness += 2f;
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
            Debug.Log("Atempting Feeding queen now");

            carryingFood = false;

            DeliveredCount++;
            Fitness += 10f;
            Debug.Log("Ant " + name + " delivered food to queen! Total delivered: " + DeliveredCount);
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
            for (int dy = -2; dy <= 2; dy++)
                for (int dz = -searchRadius; dz <= searchRadius; dz++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    int z = cz + dz;

                    var b = WorldManager.Instance.GetBlock(x, y, z);
                    if (b is MulchBlock)
                    {
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 < bestDistSq)
                        {
                            bestDistSq = d2;
                            best = new Vector3Int(x, y, z);
                        }
                    }
                }

        return best;
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

        bool hitLower = Physics.Raycast(lowerOrigin, transform.forward, stepCheckDist, groundMask);
        bool hitUpper = Physics.Raycast(upperOrigin, transform.forward, stepCheckDist, groundMask);

        // 1-block step → climbable
        if (hitLower && !hitUpper)
        {
            Vector3 step =
                Vector3.up * stepUpAmount +
                transform.forward * stepForwardAmount;

            rb.MovePosition(rb.position + step);
            return;
        }

        // 2-block wall → NOT climbable
        if (hitLower && hitUpper)
        {
            //         Debug.Log(
            //     $"Ant {name} encountered a 2-block obstacle at {transform.position}--- TUrn Chance is: {turnChanceTwoBlocks}"
            // );

            if (Random.value > turnChanceTwoBlocks)
            {
                // Try a random turn to escape
                int steps = Random.Range(-4, 5); // inclusive -4..4
                if (steps == 0) steps = (Random.value < 0.5f) ? -1 : 1;
                transform.Rotate(0f, steps * turnStepDegrees, 0f);
            }
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

private void Die()
{
    // If you have an evolution manager tracking ants, notify it here.
    Destroy(gameObject);
}

}

