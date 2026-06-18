using UnityEngine;

public class MissileController : MonoBehaviour
{
    [Header("Outer Loop — Guidance (heading error → desired turn rate)")]
    public float Kp_pitch = 2.5f, Ki_pitch = 0.05f, Kd_pitch = 0.6f;
    public float Kp_yaw   = 2.5f, Ki_yaw   = 0.05f, Kd_yaw   = 0.6f;

    [Header("Inner Loop — Rate Stabilization (rate error → torque)")]
    public float Kp_pitchRate = 6.0f, Ki_pitchRate = 0.02f, Kd_pitchRate = 0.3f;
    public float Kp_yawRate   = 6.0f, Ki_yawRate   = 0.02f, Kd_yawRate   = 0.3f;
    public float maxTurnRate  = 25f;   // deg/s — clamps outer loop output

    [Header("Throttle Loop")]
    public float Kp_throttle = 2f, Ki_throttle = 0f, Kd_throttle = 0.5f;

    [Header("Terminal PN + LOS Filter")]
    public float navConstant   = 4f;     // Proportional navigation constant N'
    public float losFilterAlpha = 0.25f; // 0=no update(frozen) .. 1=no filtering(raw)

    [Header("Missile Params")]
    public float maxSpeed      = 2000f;  // m/s (Mach 5.9 for 48N6E2)
    public float maxAccel      = 300f;   // m/s²
    public float terminalRange = 5000f;  // Switch to PN at 5 km
    public float fuzeRadius    = 50f;    // Proximity detonation radius

    // Outer loop (angle error → desired rate)
    private PIDController pidPitchOuter, pidYawOuter;
    // Inner loop (rate error → torque)
    private PIDController pidPitchRate, pidYawRate;
    private PIDController pidThrottle;

    private Rigidbody rb;
    private AerialTarget target;
    private float currentSpeed;
    private bool  terminalPhase;

    // Inner-loop measured rates (previous-frame rotation, deg/s)
    private Vector3 lastEuler;

    // Proportional Nav + filtered LOS rate state
    private Vector3 lastLOS;
    private Vector3 filteredLOSrate;
    private bool    losInitialized;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Outer guidance loop — slower, larger Kp tolerated
        pidPitchOuter = new PIDController(Kp_pitch, Ki_pitch, Kd_pitch);
        pidYawOuter   = new PIDController(Kp_yaw,   Ki_yaw,   Kd_yaw);

        // Inner stabilization loop — damps actual rotation to commanded rate
        pidPitchRate  = new PIDController(Kp_pitchRate, Ki_pitchRate, Kd_pitchRate);
        pidYawRate    = new PIDController(Kp_yawRate,   Ki_yawRate,   Kd_yawRate);

        pidThrottle   = new PIDController(Kp_throttle, Ki_throttle, Kd_throttle);
        lastEuler     = transform.eulerAngles;
    }

    public void Initialize(AerialTarget t) => target = t;

    void FixedUpdate()
    {
        if (target == null) return;
        float dist = Vector3.Distance(transform.position, target.transform.position);

        if (dist < fuzeRadius)  { Detonate(); return; }
        terminalPhase = dist < terminalRange;

        if (terminalPhase) GuideProportionalNav();
        else               GuideCascadedPID();
    }

    void GuideCascadedPID()
    {
        float dt = Time.fixedDeltaTime;

        // ── OUTER LOOP: heading error → desired turn rate (deg/s) ──
        float tof   = Vector3.Distance(transform.position,
                        target.transform.position) / Mathf.Max(currentSpeed, 1f);
        Vector3 aim = target.transform.position + target.Rb.linearVelocity * tof;

        Vector3 toTarget = aim - transform.position;
        Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);

        // localDir.y/x are the heading errors (radians, small-angle approx)
        float desiredPitchRate = pidPitchOuter.Update(-localDir.y, dt) * Mathf.Rad2Deg;
        float desiredYawRate   = pidYawOuter.Update(localDir.x,   dt) * Mathf.Rad2Deg;
        desiredPitchRate = Mathf.Clamp(desiredPitchRate, -maxTurnRate, maxTurnRate);
        desiredYawRate   = Mathf.Clamp(desiredYawRate,   -maxTurnRate, maxTurnRate);

        // ── INNER LOOP: measured rate vs desired rate → actual rotation ──
        Vector3 euler = transform.eulerAngles;
        float measuredPitchRate = Mathf.DeltaAngle(lastEuler.x, euler.x) / dt;
        float measuredYawRate   = Mathf.DeltaAngle(lastEuler.y, euler.y) / dt;
        lastEuler = euler;

        float pitchRateError = desiredPitchRate - measuredPitchRate;
        float yawRateError   = desiredYawRate   - measuredYawRate;

        float pitchTorque = pidPitchRate.Update(pitchRateError, dt);
        float yawTorque   = pidYawRate.Update(yawRateError,     dt);

        // ── THROTTLE: maintain closing speed ──
        float closingSpeed = Vector3.Dot(rb.linearVelocity,
                               (target.transform.position - transform.position).normalized);
        float spdErr    = maxSpeed * 0.8f - closingSpeed;
        float throttle  = pidThrottle.Update(spdErr, dt);
        currentSpeed    = Mathf.MoveTowards(currentSpeed,
                              currentSpeed + throttle, maxAccel * dt);
        currentSpeed    = Mathf.Clamp(currentSpeed, 0, maxSpeed);

        // Apply inner-loop torque output as actual rotation this tick
        transform.Rotate(pitchTorque * dt, yawTorque * dt, 0, Space.Self);
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    void GuideProportionalNav()
    {
        Vector3 LOS = (target.transform.position - transform.position).normalized;
        if (!losInitialized)
        {
            lastLOS = LOS;
            filteredLOSrate = Vector3.zero;
            losInitialized = true;
        }

        // Raw LOS rate (noisy finite-difference derivative)
        Vector3 rawLOSrate = (LOS - lastLOS) / Time.fixedDeltaTime;
        lastLOS = LOS;

        // Low-pass filter: smooths jitter from target maneuvers / FP noise
        // without adding meaningful lag, since true LOS rate changes slowly
        // relative to the 50 Hz physics tick.
        filteredLOSrate = Vector3.Lerp(filteredLOSrate, rawLOSrate, losFilterAlpha);

        // PN command: a_c = N' * V_c * omega_filtered
        float closingVel = -Vector3.Dot(rb.linearVelocity, LOS);
        Vector3 accel   = navConstant * closingVel * filteredLOSrate;
        rb.AddForce(accel, ForceMode.Acceleration);
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeed);

        transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
    }

    void Detonate()
    {
        // Spawn explosion, damage target, return missile to pool
        Instantiate(Resources.Load("Effects/Explosion"), transform.position,
                    Quaternion.identity);
        Destroy(target.gameObject);
        Destroy(gameObject);
    }
}
