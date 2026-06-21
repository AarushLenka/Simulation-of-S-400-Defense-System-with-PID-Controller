using UnityEngine;

/// <summary>
/// S-400 interceptor guidance — stable pure-pursuit with proportional navigation blend.
///
/// Launch: vertical cold-launch (spawned pointing up).
/// Guidance: rotates nose toward predicted intercept point each tick,
///           clamped by maxTurnRate. Velocity always follows the nose.
///           Switches to tighter PN in terminal phase for accuracy.
/// Terrain/obstacle avoidance: multi-ray forward scan steers away from
///           terrain and Radar objects before collision can occur.
/// </summary>
public class MissileController : MonoBehaviour
{
    [Header("Speed — 1:40 scale")]
    public float launchSpeed  = 20f;   // m/s  (IRL ~800 m/s)
    public float maxSpeed     = 45f;   // m/s  (IRL ~1800 m/s)
    public float acceleration = 15f;   // m/s²

    [Header("Maneuverability")]
    [Tooltip("Max rotation rate in degrees/second")]
    public float maxTurnRate   = 180f; // deg/s — high enough to pitch over after vertical launch
    public float terminalRange = 80f;  // switch to tighter PN inside this distance

    [Header("Proportional Navigation")]
    public float navConstant = 3f;     // N' — PN gain

    [Header("Warhead — 1:40 scale")]
    public float fuzeRadius = 8f;      // proximity detonation radius

    [Header("Self-destruct")]
    public float lifetime = 60f;       // seconds before fuel exhaustion

    [Header("Boost Phase")]
    [Tooltip("Seconds to fly straight up before homing begins")]
    public float boostDuration = 2f;

    [Header("Terrain / Obstacle Avoidance")]
    [Tooltip("How far ahead to cast avoidance rays (metres)")]
    public float avoidanceLookAhead = 60f;
    [Tooltip("Half-angle of the avoidance ray fan (degrees)")]
    public float avoidanceFanAngle  = 30f;
    [Tooltip("Strength of the avoidance steering (0 = off, 1 = full override)")]
    [Range(0f, 1f)]
    public float avoidanceWeight    = 0.85f;
    [Tooltip("Layers that count as obstacles (terrain + anything tagged Radar)")]
    public LayerMask avoidanceMask  = ~0;   // all layers by default; tune in Inspector

    // ── Private state ──────────────────────────────────────────────
    private Rigidbody    rb;
    private AerialTarget target;
    private float        _speed;
    private float        _elapsed;
    private Vector3      _lastLOS;
    private bool         _losInit;
    private bool         _terminated;

    // Avoidance ray directions relative to forward (built once in Awake)
    private static readonly Vector3[] _avoidRayDirs = new Vector3[]
    {
        Vector3.forward,
        new Vector3( 0.5f,  0f, 1f),   // right 27°
        new Vector3(-0.5f,  0f, 1f),   // left  27°
        new Vector3( 0f,  0.5f, 1f),   // up    27°
        new Vector3( 0f, -0.5f, 1f),   // down  27°
        new Vector3( 0.7f, 0.7f, 1f),  // up-right
        new Vector3(-0.7f, 0.7f, 1f),  // up-left
    };

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity     = false;
        rb.linearDamping  = 0f;
        rb.angularDamping = 0f;
        // Disable angular physics — we drive rotation directly via RotateTowards
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void Initialize(AerialTarget t)
    {
        target = t;
        _speed = launchSpeed;
        rb.linearVelocity = Vector3.up * _speed;
        Debug.Log($"[MISSILE] Launched → tracking {t.name}");
    }

    void FixedUpdate()
    {
        if (_terminated) return;

        _elapsed += Time.fixedDeltaTime;

        if (_elapsed > lifetime)
        {
            Debug.Log("[MISSILE] Fuel exhausted");
            Terminate(false);
            return;
        }

        if (target == null)
        {
            Terminate(false);
            return;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);

        // Proximity fuze
        if (dist < fuzeRadius)
        {
            Debug.Log($"[MISSILE] DETONATION at {dist:F1}m");
            Detonate();
            return;
        }

        // ── Accelerate ────────────────────────────────────────────────
        _speed = Mathf.MoveTowards(_speed, maxSpeed, acceleration * Time.fixedDeltaTime);

        // ── Boost phase: fly straight up, no guidance ─────────────────
        if (_elapsed < boostDuration)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(Vector3.up),
                maxTurnRate * Time.fixedDeltaTime);
            rb.linearVelocity = transform.forward * _speed;
            return;
        }

        // ── Guidance: compute desired aim direction ───────────────────
        // In terminal phase only use PN; avoidance would deflect the kill shot.
        Vector3 aimDir = dist < terminalRange
            ? ComputePN()
            : ComputePursuit();

        // ── Terrain / obstacle avoidance (mid-course only) ───────────
        // Skip avoidance in terminal phase — the target matters more than obstacles.
        if (dist >= terminalRange)
        {
            Vector3 avoidDir = ComputeAvoidance();
            if (avoidDir != Vector3.zero)
                aimDir = Vector3.Slerp(aimDir, avoidDir, avoidanceWeight).normalized;
        }

        // ── Steer: rotate nose toward aim, clamped by maxTurnRate ─────
        if (aimDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(aimDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, maxTurnRate * Time.fixedDeltaTime);
        }

        // ── Velocity always follows nose — no separate physics forces ─
        rb.linearVelocity = transform.forward * _speed;
    }

    /// <summary>
    /// Casts a fan of rays in the missile's forward hemisphere.
    /// If any ray hits terrain or a Radar object, returns a steering direction
    /// that blends away from all hit normals with an upward bias.
    /// Returns Vector3.zero when no obstacles are detected.
    /// </summary>
    Vector3 ComputeAvoidance()
    {
        // Dynamic look-ahead: at minimum 1.5× the distance needed to stop turning
        float dynamicAhead = Mathf.Max(avoidanceLookAhead, _speed * 1.5f);

        Vector3 avoidAccum  = Vector3.zero;
        int     hitCount    = 0;

        foreach (Vector3 localDir in _avoidRayDirs)
        {
            // Transform ray direction from missile-local to world space
            Vector3 worldDir = transform.TransformDirection(localDir.normalized);

            if (Physics.Raycast(transform.position, worldDir, out RaycastHit hit,
                                dynamicAhead, avoidanceMask, QueryTriggerInteraction.Ignore))
            {
                // Only react to terrain and objects tagged "Radar"
                bool isTerrain = hit.collider is TerrainCollider ||
                                hit.collider.gameObject.layer ==
                                LayerMask.NameToLayer("Terrain");
                bool isRadar   = hit.collider.CompareTag("Radar");

                if (!isTerrain && !isRadar) continue;

                // Urgency: closer obstacle → stronger push
                float urgency = 1f - Mathf.Clamp01(hit.distance / dynamicAhead);

                // Reflection off hit normal + upward bias to climb over obstacles
                Vector3 reflected  = Vector3.Reflect(worldDir, hit.normal);
                Vector3 avoidLocal = (reflected + Vector3.up * 0.5f).normalized;
                avoidAccum += avoidLocal * urgency;
                hitCount++;

                Debug.DrawRay(transform.position, worldDir * hit.distance, Color.red,
                            Time.fixedDeltaTime);
            }
            else
            {
                Debug.DrawRay(transform.position, worldDir * dynamicAhead, Color.green,
                              Time.fixedDeltaTime);
            }
        }

        if (hitCount == 0) return Vector3.zero;

        // Blend the avoidance direction with current forward so the missile
        // doesn't spin 180°; normalise after accumulation.
        return (avoidAccum / hitCount + transform.forward * 0.3f).normalized;
    }

    /// <summary>
    /// Pure pursuit with lead: aim at predicted target position one TOF ahead.
    /// Stable at all ranges. Used until terminal phase.
    /// </summary>
    Vector3 ComputePursuit()
    {
        float tof = Vector3.Distance(transform.position, target.transform.position)
                    / Mathf.Max(_speed, 1f);
        // Lead by half TOF — avoids overshooting on slow targets
        Vector3 aimPoint = target.transform.position + target.Rb.linearVelocity * tof * 0.5f;
        return (aimPoint - transform.position).normalized;
    }

    /// <summary>
    /// Proportional Navigation: steers to zero the LOS rate.
    /// More accurate in terminal phase when target may be manoeuvring.
    /// </summary>
    Vector3 ComputePN()
    {
        Vector3 toTarget = target.transform.position - transform.position;
        Vector3 LOS = toTarget.normalized;

        if (!_losInit) { _lastLOS = LOS; _losInit = true; return LOS; }

        Vector3 LOSrate = (LOS - _lastLOS) / Time.fixedDeltaTime;
        _lastLOS = LOS;

        float closingVel = Vector3.Dot(
            rb.linearVelocity - target.Rb.linearVelocity, -LOS);

        Vector3 accel = navConstant * Mathf.Max(closingVel, 1f) * LOSrate;
        return (transform.forward + accel * Time.fixedDeltaTime).normalized;
    }

    void Detonate()
    {
        if (_terminated) return;
        _terminated = true;

        var explosion = Resources.Load<GameObject>("Effects/Explosion");
        if (explosion != null)
            Instantiate(explosion, transform.position, Quaternion.identity);

        bool hit = target != null;
        if (target != null) Destroy(target.gameObject);

        // Remove this missile's camera slot immediately
        var mcd = FindAnyObjectByType<MissileCameraDisplay>();
        mcd?.UnregisterMissile(this);

        NotifyFCS(hit);
        Destroy(gameObject);
    }

    void Terminate(bool hit)
    {
        if (_terminated) return;
        _terminated = true;
        // Remove this missile's camera slot immediately
        var mcd = FindAnyObjectByType<MissileCameraDisplay>();
        mcd?.UnregisterMissile(this);
        NotifyFCS(hit);
        Destroy(gameObject);
    }

    void NotifyFCS(bool hit)
    {
        var fcs = FindAnyObjectByType<FireControlSystem>();
        if (fcs != null) fcs.OnMissileTerminated(hit);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<TerrainCollider>() != null)
        {
            Debug.Log("[MISSILE] Terrain impact");
            Detonate();
        }
    }

    void OnDestroy()
    {
        if (!_terminated && target != null) NotifyFCS(false);
    }
}
