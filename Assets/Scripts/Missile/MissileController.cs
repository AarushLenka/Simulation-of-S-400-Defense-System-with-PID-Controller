using UnityEngine;

/// <summary>
/// S-400 interceptor guidance — stable pure-pursuit with proportional navigation blend.
///
/// Launch: vertical cold-launch (spawned pointing up).
/// Guidance: rotates nose toward predicted intercept point each tick,
///           clamped by maxTurnRate. Velocity always follows the nose.
///           Switches to tighter PN in terminal phase for accuracy.
/// This avoids the PID torque instability that caused spinning.
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
    public float lifetime = 30f;       // seconds before fuel exhaustion

    // ── Private state ──────────────────────────────────────────────
    private Rigidbody    rb;
    private AerialTarget target;
    private float        _speed;
    private float        _elapsed;
    private Vector3      _lastLOS;
    private bool         _losInit;
    private bool         _terminated;

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

        // ── Guidance: compute desired aim direction ───────────────────
        Vector3 aimDir = dist < terminalRange
            ? ComputePN()
            : ComputePursuit();

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

        NotifyFCS(hit);
        Destroy(gameObject);
    }

    void Terminate(bool hit)
    {
        if (_terminated) return;
        _terminated = true;
        NotifyFCS(hit);
        Destroy(gameObject);
    }

    void NotifyFCS(bool hit)
    {
        var fcs = FindFirstObjectByType<FireControlSystem>();
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
