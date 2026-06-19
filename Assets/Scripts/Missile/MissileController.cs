using UnityEngine;

/// <summary>
/// Simple, stable missile guidance.
/// Uses Pure Pursuit until terminal phase, then Proportional Navigation.
/// No cascaded PIDs — just direct velocity steering with a turn-rate limit.
/// </summary>
public class MissileController : MonoBehaviour
{
    [Header("Speed")]
    public float launchSpeed   = 20f;    // m/s initial (1:40 scale — IRL ~800 m/s)
    public float maxSpeed      = 45f;    // m/s top (1:40 scale — IRL ~1800 m/s 9M96E2)
    public float acceleration  = 8f;     // m/s²

    [Header("Maneuverability")]
    public float maxTurnRate   = 90f;    // deg/s — high agility needed to catch slow targets
    public float terminalRange = 80f;    // switch to PN inside this range

    [Header("Warhead")]
    public float fuzeRadius    = 8f;     // proximity detonation (1:40 scale — IRL ~320m)

    [Header("Proportional Nav")]
    public float navConstant   = 4f;     // N' for PN guidance

    // ── private state ──────────────────────────────────────────────
    private Rigidbody  rb;
    private AerialTarget target;
    private float      currentSpeed;
    private Vector3    lastLOS;
    private bool       losInit;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
    }

    public void Initialize(AerialTarget t)
    {
        target = t;
        currentSpeed = launchSpeed;
        rb.linearVelocity = transform.forward * currentSpeed;
        Debug.Log($"[MISSILE] Initialized → tracking {t.name} | launch speed={launchSpeed}m/s");
    }

    void FixedUpdate()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.transform.position);

        // Proximity fuze
        if (dist < fuzeRadius)
        {
            Debug.Log($"[MISSILE] DETONATION — proximity fuze triggered at {dist:F0}m from {target.name}");
            Detonate();
            return;
        }

        // Accelerate toward max speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.fixedDeltaTime);

        if (dist < terminalRange)
            SteerProportionalNav();
        else
            SteerPursuit();

        // Always drive forward at current speed
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    /// <summary>
    /// Pure Pursuit — point nose directly at predicted intercept point.
    /// Simple and stable at long range.
    /// </summary>
    void SteerPursuit()
    {
        // Lead target by one time-of-flight estimate
        float tof = Vector3.Distance(transform.position, target.transform.position)
                    / Mathf.Max(currentSpeed, 1f);
        Vector3 aimPoint = target.transform.position + target.Rb.linearVelocity * tof * 0.5f;
        Vector3 desired  = (aimPoint - transform.position).normalized;

        RotateToward(desired);
    }

    /// <summary>
    /// Proportional Navigation — commands acceleration proportional to LOS rate.
    /// More accurate in terminal phase.
    /// </summary>
    void SteerProportionalNav()
    {
        Vector3 toTarget = target.transform.position - transform.position;
        Vector3 LOS      = toTarget.normalized;

        if (!losInit) { lastLOS = LOS; losInit = true; }

        Vector3 LOSrate = (LOS - lastLOS) / Time.fixedDeltaTime;
        lastLOS = LOS;

        // PN acceleration command
        Vector3 accelCmd = navConstant * currentSpeed * LOSrate;

        // Convert to a desired direction
        Vector3 desired = (transform.forward + accelCmd * Time.fixedDeltaTime).normalized;
        RotateToward(desired);
    }

    /// <summary>
    /// Smoothly rotates the missile nose toward the desired direction,
    /// clamped by maxTurnRate.
    /// </summary>
    void RotateToward(Vector3 desiredDir)
    {
        if (desiredDir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(desiredDir);
        float maxDeg = maxTurnRate * Time.fixedDeltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxDeg);
    }

    void Detonate()
    {
        var explosion = Resources.Load<GameObject>("Effects/Explosion");
        if (explosion != null)
            Instantiate(explosion, transform.position, Quaternion.identity);

        bool hit = target != null;
        if (target != null) Destroy(target.gameObject);

        // Notify FCS
        var fcs = FindFirstObjectByType<FireControlSystem>();
        if (fcs != null) fcs.OnMissileTerminated(hit);

        Destroy(gameObject);
    }

    // Called when missile runs out of time/fuel without hitting
    void OnDestroy()
    {
        // If target still alive when we're destroyed (not via Detonate), register miss
        if (target != null)
        {
            var fcs = FindFirstObjectByType<FireControlSystem>();
            if (fcs != null) fcs.OnMissileTerminated(false);
        }
    }
}
