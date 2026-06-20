using UnityEngine;

/// <summary>
/// Ballistic missile: boosts along a fixed launch vector until apogee altitude
/// is reached (or boost duration expires), then falls under gravity toward radar.
/// </summary>
public class BallisticMissile : AerialTarget
{
    [Tooltip("Seconds of powered boost phase (cut short if apogee is reached first)")]
    public float boostDuration = 18f;
    [Tooltip("Altitude (metres) at which boost cuts off and reentry begins — 1:40 scale: IRL 50km → 1250m")]
    public float apogeeAltitude = 1250f;

    private float   _elapsed;
    private bool    _reentryStarted;
    private Vector3 _aimPoint;      // radar position, locked at launch
    private Vector3 _boostDir;      // fixed launch direction, never changes

    protected override void InitializeTarget()
    {
        rb.useGravity     = false;
        rb.linearDamping  = 0f;
        rb.angularDamping = 0f;
        currentSpeed      = config.minSpeed;

        // Lock aim point at launch
        _aimPoint = radarTarget != null
            ? radarTarget.position
            : transform.position + transform.forward * 5000f;

        // Fixed boost direction: forward + upward loft — locked once, never recalculated
        _boostDir = (transform.forward + Vector3.up * 1.8f).normalized;

        rb.linearVelocity = _boostDir * currentSpeed;
    }

    public override void UpdateMotion()
    {
        _elapsed += Time.fixedDeltaTime;

        bool apogeeReached = transform.position.y >= apogeeAltitude;
        bool boostExpired  = _elapsed >= boostDuration;

        if (!_reentryStarted && (apogeeReached || boostExpired))
        {
            // ── Begin reentry ─────────────────────────────────────────
            _reentryStarted  = true;
            rb.useGravity    = true;
            rb.linearDamping = 0.04f;

            // Kick velocity toward aim point so reentry arc leads to radar
            Vector3 toAim = (_aimPoint - transform.position).normalized;
            rb.linearVelocity = toAim * rb.linearVelocity.magnitude;
        }

        if (!_reentryStarted)
        {
            // ── Boost phase — accelerate along the fixed locked direction ──
            float accel = (config.maxSpeed - config.minSpeed) / boostDuration;
            rb.AddForce(_boostDir * accel, ForceMode.Acceleration);
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, config.maxSpeed);
        }
        else
        {
            // ── Reentry — gravity does most of the work, gentle aim correction ──
            if (transform.position.y > 300f)
            {
                Vector3 toAim = (_aimPoint - transform.position).normalized;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    toAim * rb.linearVelocity.magnitude, 0.8f * Time.fixedDeltaTime);
            }
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }
}
