using UnityEngine;

/// <summary>
/// REF §2.5 — Three physical phases: Boost → Coast → Reentry.
/// Boost: thrust ON, gravity OFF, steep climb for boostDuration seconds.
/// Coast: thrust OFF, gravity ON, natural ballistic arc.
/// Reentry: triggered when vertical velocity goes negative (descending).
///          Adds linearDamping to simulate atmospheric drag. Speed increases.
/// </summary>
public class BallisticMissile : AerialTarget
{
    [Tooltip("Duration of powered boost phase (seconds)")]
    public float boostDuration = 18f;

    [Tooltip("Apogee altitude cutoff — boost ends early if this is reached (1:40: IRL 50km → 1250m)")]
    public float apogeeAltitude = 1250f;

    private enum Phase { Boost, Coast, Reentry }
    private Phase   _phase   = Phase.Boost;
    private float   _elapsed;
    private Vector3 _boostDir; // locked at launch, never recalculated

    protected override void InitializeTarget()
    {
        rb.useGravity     = false;  // gravity OFF during boost (spec §2.5)
        rb.linearDamping  = 0f;
        rb.angularDamping = 0f;
        currentSpeed      = config.minSpeed;

        // Lock boost direction once — steep upward and forward
        // Facing radar ensures forward component points toward target
        if (radarTarget != null)
        {
            Vector3 toRadar = (radarTarget.position - transform.position);
            toRadar.y = 0f;
            if (toRadar.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toRadar.normalized);
        }

        _boostDir = (transform.forward + Vector3.up * 2f).normalized;
        rb.linearVelocity = _boostDir * currentSpeed;
    }

    public override void UpdateMotion()
    {
        _elapsed += Time.fixedDeltaTime;

        switch (_phase)
        {
            // ── Boost: force applied along locked direction, gravity OFF ──
            case Phase.Boost:
            {
                float accel = (config.maxSpeed - config.minSpeed) / boostDuration;
                rb.AddForce(_boostDir * accel, ForceMode.Acceleration);
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, config.maxSpeed);

                bool timeUp    = _elapsed >= boostDuration;
                bool apogeeHit = transform.position.y >= apogeeAltitude;
                if (timeUp || apogeeHit)
                {
                    // Transition to Coast: switch gravity on, let physics take over
                    rb.useGravity   = true;
                    rb.linearDamping = 0f;
                    _phase          = Phase.Coast;
                }
                break;
            }

            // ── Coast: no thrust, gravity ON, natural arc ─────────────────
            case Phase.Coast:
            {
                // No intervention — physics drives the arc
                // Transition to Reentry when vertical velocity turns negative (spec §2.5)
                if (rb.linearVelocity.y < 0f)
                {
                    rb.linearDamping = 0.08f; // atmospheric drag during reentry
                    _phase           = Phase.Reentry;
                }
                break;
            }

            // ── Reentry: drag ON, speed increases as gravity accelerates descent ─
            case Phase.Reentry:
            {
                // Physics (gravity + drag) drives everything — no thrust, no steering
                // Speed will increase dramatically as intended (spec §2.5)
                break;
            }
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }
}
