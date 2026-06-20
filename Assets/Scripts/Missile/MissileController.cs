using UnityEngine;

/// <summary>
/// REF §6 — S-400 interceptor guidance.
/// 
/// Launch: spawned pointing straight up (cold-launch vertical ejection).
///         Immediately begins pitchover toward target via PN + PID.
///
/// Guidance chain (spec §6):
///   Radar → Threat Assessment → Target Assignment
///   → PN Guidance (strategist: computes desired LOS rate correction)
///   → PID Controller (pilot: applies AddTorque to achieve desired turn rate)
///   → Rigidbody → Missile Motion
///
/// PN is always active — no mode switching.
/// PID drives AddTorque, not direct velocity assignment.
/// Speed: thrust via AddForce along transform.forward.
/// </summary>
public class MissileController : MonoBehaviour
{
    [Header("Speed — 1:40 scale")]
    public float launchSpeed  = 20f;   // m/s  (IRL ~800 m/s)
    public float maxSpeed     = 45f;   // m/s  (IRL ~1800 m/s 9M96E2)
    public float thrustForce  = 120f;  // N/kg (gives ~3 s to max speed)

    [Header("Proportional Navigation")]
    [Tooltip("Navigation constant N'. Typical value 3–5.")]
    public float navConstant  = 4f;

    [Header("PID — attitude control")]
    public float kP = 15f;
    public float kI = 0.5f;
    public float kD = 2f;

    [Header("Warhead — 1:40 scale")]
    [Tooltip("Proximity fuze radius — IRL ~320m → /40 = 8m")]
    public float fuzeRadius   = 8f;

    [Header("Self-destruct")]
    [Tooltip("Destroy missile after this many seconds if no hit (fuel exhaustion)")]
    public float lifetime     = 30f;

    // ── Private state ──────────────────────────────────────────────
    private Rigidbody    rb;
    private AerialTarget target;
    private float        _currentSpeed;
    private float        _elapsed;

    // PID state
    private Vector3 _pidIntegral;
    private Vector3 _pidLastError;

    // PN state
    private Vector3 _lastLOS;
    private bool    _losInit;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity    = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.1f;
    }

    /// <summary>Called by FCS immediately after instantiation.</summary>
    public void Initialize(AerialTarget t)
    {
        target        = t;
        _currentSpeed = launchSpeed;
        // Missile starts pointing straight up (vertical launch per spec §5.4 / §6)
        // Initial velocity is straight up — PN/PID pitchover starts immediately
        rb.linearVelocity = Vector3.up * _currentSpeed;
        Debug.Log($"[MISSILE] Vertical launch → tracking {t.name}");
    }

    void FixedUpdate()
    {
        _elapsed += Time.fixedDeltaTime;

        // Self-destruct on fuel exhaustion
        if (_elapsed > lifetime)
        {
            Debug.Log("[MISSILE] Fuel exhausted — self-destructing");
            SelfDestruct();
            return;
        }

        if (target == null)
        {
            SelfDestruct();
            return;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);

        // Proximity fuze
        if (dist < fuzeRadius)
        {
            Debug.Log($"[MISSILE] DETONATION at {dist:F1}m from {target.name}");
            Detonate();
            return;
        }

        // ── Thrust: accelerate along current forward direction ────────
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed,
            (thrustForce / Mathf.Max(rb.mass, 0.001f)) * Time.fixedDeltaTime);
        rb.AddForce(transform.forward * thrustForce, ForceMode.Acceleration);
        // Clamp speed
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        // ── PN Guidance → desired turn direction ──────────────────────
        Vector3 desiredDir = ComputePNDirection();

        // ── PID → torque to rotate toward desired direction ───────────
        ApplyPIDTorque(desiredDir);
    }

    /// <summary>
    /// Proportional Navigation — always active (spec §6).
    /// Returns the desired heading unit vector.
    /// </summary>
    Vector3 ComputePNDirection()
    {
        Vector3 toTarget = target.transform.position - transform.position;
        Vector3 LOS      = toTarget.normalized;

        if (!_losInit) { _lastLOS = LOS; _losInit = true; return LOS; }

        // LOS rate (change in line-of-sight direction per second)
        Vector3 LOSrate = (LOS - _lastLOS) / Time.fixedDeltaTime;
        _lastLOS = LOS;

        // PN acceleration command: a_cmd = N * Vc * omega
        // where Vc = closing velocity, omega = LOS rate
        float closingVel = Vector3.Dot(rb.linearVelocity - target.Rb.linearVelocity, -LOS);
        Vector3 accelCmd = navConstant * Mathf.Abs(closingVel) * LOSrate;

        // Desired direction = current forward + PN correction
        Vector3 desired = (transform.forward + accelCmd * Time.fixedDeltaTime).normalized;
        return desired;
    }

    /// <summary>
    /// PID attitude controller — applies AddTorque to steer nose toward desiredDir.
    /// This is the "pilot" that physically executes the PN command (spec §6).
    /// </summary>
    void ApplyPIDTorque(Vector3 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.001f) return;

        // Angular error: cross product gives rotation axis * sin(angle)
        Vector3 error = Vector3.Cross(transform.forward, desiredDir);

        _pidIntegral  += error * Time.fixedDeltaTime;
        Vector3 derivative = (error - _pidLastError) / Time.fixedDeltaTime;
        _pidLastError  = error;

        Vector3 torque = kP * error + kI * _pidIntegral + kD * derivative;
        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    void Detonate()
    {
        var explosion = Resources.Load<GameObject>("Effects/Explosion");
        if (explosion != null)
            Instantiate(explosion, transform.position, Quaternion.identity);

        bool hit = target != null;
        if (target != null) Destroy(target.gameObject);

        NotifyFCS(hit);
        Destroy(gameObject);
    }

    void SelfDestruct()
    {
        NotifyFCS(false);
        Destroy(gameObject);
    }

    void NotifyFCS(bool hit)
    {
        var fcs = FindFirstObjectByType<FireControlSystem>();
        if (fcs != null) fcs.OnMissileTerminated(hit);
    }

    void OnDestroy()
    {
        // Guard: if destroyed externally (not via Detonate/SelfDestruct), still notify FCS
        if (target != null) NotifyFCS(false);
    }
}
