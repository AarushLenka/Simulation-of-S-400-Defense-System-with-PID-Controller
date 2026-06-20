using UnityEngine;

/// <summary>
/// REF §2.3 — Two states: Loiter (circular at spawn point) → Dive (permanent).
/// Loiter: smooth trig-based circle around spawn position at fixed altitude.
/// Dive: triggered by isTracked. 60% down + 100% forward, accelerates to maxSpeed.
///       Once in Dive, never returns to Loiter.
/// </summary>
public class UAVDrone : AerialTarget
{
    private enum State { Loiter, Dive }
    private State  _state = State.Loiter;

    [Tooltip("Radius of the loiter circle (metres)")]
    public float loiterRadius = 1200f;

    private Vector3 _loiterCenter;   // spawn position — never changes
    private float   _loiterAngle;
    private float   _loiterAltitude;

    protected override void InitializeTarget()
    {
        currentSpeed    = Random.Range(config.minSpeed, config.maxSpeed * 0.6f);
        float groundAtRadar = radarTarget != null ? GetTerrainYBelow() : 0f;
        _loiterAltitude = groundAtRadar + Random.Range(config.minAltitude, config.maxAltitude);

        // Loiter centre is spawn position, not the radar (spec §2.3)
        _loiterCenter = transform.position;
        _loiterCenter.y = 0f; // we manage Y via HoldAltitude

        // Start angle at current position relative to loiter centre
        _loiterAngle = Mathf.Atan2(
            transform.position.z - _loiterCenter.z,
            transform.position.x - _loiterCenter.x);

        rb.linearVelocity = transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        switch (_state)
        {
            // ── Loiter: trig-based circle, smooth regardless of physics dt ──
            case State.Loiter:
                // Advance angle based on arc speed
                _loiterAngle += (currentSpeed / loiterRadius) * Time.fixedDeltaTime;

                Vector3 targetPos = _loiterCenter
                    + new Vector3(Mathf.Cos(_loiterAngle), 0f, Mathf.Sin(_loiterAngle)) * loiterRadius;

                Vector3 toTarget = (targetPos - transform.position);
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    rb.linearVelocity = toTarget.normalized * currentSpeed;

                HoldAltitude(_loiterAltitude);

                // Transition: permanent once triggered (spec §2.3)
                if (isTracked) _state = State.Dive;
                break;

            // ── Dive: 60% down + 100% forward, accelerate to maxSpeed ──────
            case State.Dive:
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 3f * Time.fixedDeltaTime);

                // Per spec: "60% downward component, 100% forward component, normalised"
                Vector3 forward2D = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (forward2D.sqrMagnitude < 0.01f)
                    forward2D = transform.forward;
                forward2D = forward2D.normalized;

                Vector3 diveDir = new Vector3(forward2D.x, -0.6f, forward2D.z).normalized;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    diveDir * currentSpeed, 2f * Time.fixedDeltaTime);
                // No return to Loiter — permanent per spec
                break;
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        // Horizontal speed only — exclude HoldAltitude's vel.y to prevent feedback loop
        Vector3 hv = rb.linearVelocity; hv.y = 0f;
        currentSpeed = Mathf.Clamp(hv.magnitude, 0f, config.maxSpeed);
    }
}
