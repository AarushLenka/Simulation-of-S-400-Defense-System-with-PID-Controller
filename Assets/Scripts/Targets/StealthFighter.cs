using UnityEngine;

/// <summary>
/// REF §2.1 — Three states: Cruise → Evade → Dash (terminal, never returns).
/// Cruise: straight inbound at minSpeed.
/// Evade: triggered by isTracked, high-G 90° yaw break for 3 seconds.
///        Evade direction locked at state entry — never recalculated from transform.forward.
/// Dash: accelerates to maxSpeed on locked heading indefinitely.
/// </summary>
public class StealthFighter : AerialTarget
{
    private enum State { Cruise, Evade, Dash }
    private State   _state     = State.Cruise;
    private float   _evadeTimer;
    private float   _targetAltitude;
    private Vector3 _dashDir;    // locked at Dash entry
    private Vector3 _evadeDir;   // locked at Evade entry — never recalculated

    protected override void InitializeTarget()
    {
        currentSpeed = config.minSpeed;
        // Sample terrain at spawn position using the Terrain API directly —
        // GetTerrainYBelow() raycasts from transform.position which is correct here
        // because Start() runs after the object is placed at its spawn point.
        float groundHere = GetTerrainYBelow();
        _targetAltitude  = groundHere + Random.Range(config.minAltitude, config.maxAltitude);

        // Face radar on spawn
        if (radarTarget != null)
        {
            Vector3 toRadar = (radarTarget.position - transform.position);
            toRadar.y = 0f; // horizontal heading only — altitude held separately
            if (toRadar.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toRadar.normalized);
                rb.linearVelocity  = toRadar.normalized * currentSpeed;
            }
        }
        else
        {
            rb.linearVelocity = transform.forward * currentSpeed;
        }
    }

    public override void UpdateMotion()
    {
        switch (_state)
        {
            // ── Cruise: straight inbound at constant altitude ─────────
            case State.Cruise:
                if (radarTarget != null)
                {
                    Vector3 toRadar = radarTarget.position - transform.position;
                    toRadar.y = 0f;
                    if (toRadar.sqrMagnitude > 0.001f)
                    {
                        // Only steer XZ — HoldAltitude owns Y, do NOT overwrite full velocity
                        Vector3 vel = rb.linearVelocity;
                        Vector3 xzTarget = toRadar.normalized * currentSpeed;
                        vel.x = Mathf.Lerp(vel.x, xzTarget.x, 4f * Time.fixedDeltaTime);
                        vel.z = Mathf.Lerp(vel.z, xzTarget.z, 4f * Time.fixedDeltaTime);
                        rb.linearVelocity = vel;
                    }
                }
                HoldAltitude(_targetAltitude);

                if (isTracked)
                {
                    // Lock evade direction NOW at state entry — 90° yaw from current horizontal heading
                    Vector3 currentFlat = rb.linearVelocity;
                    currentFlat.y = 0f;
                    if (currentFlat.sqrMagnitude < 0.001f) currentFlat = transform.forward;
                    // Rotate 90° around world up — locked once, never touched again
                    _evadeDir   = Quaternion.Euler(0f, 90f, 0f) * currentFlat.normalized;
                    _state      = State.Evade;
                    _evadeTimer = 3f;
                }
                break;

            // ── Evade: hard turn onto locked heading, hold altitude ────
            case State.Evade:
                _evadeTimer -= Time.fixedDeltaTime;

                // Snap XZ velocity toward the locked evade direction aggressively
                // High lerp rate (12×dt ≈ 0.24/tick) gives a sharp, visible break
                {
                    Vector3 vel = rb.linearVelocity;
                    Vector3 xzEvade = _evadeDir * currentSpeed * 1.15f;
                    vel.x = Mathf.Lerp(vel.x, xzEvade.x, 12f * Time.fixedDeltaTime);
                    vel.z = Mathf.Lerp(vel.z, xzEvade.z, 12f * Time.fixedDeltaTime);
                    rb.linearVelocity = vel;
                }
                HoldAltitude(_targetAltitude);

                if (_evadeTimer <= 0f)
                {
                    // Lock dash direction from current flat velocity
                    Vector3 flatVel = rb.linearVelocity;
                    flatVel.y = 0f;
                    _dashDir = flatVel.sqrMagnitude > 0.01f
                        ? flatVel.normalized
                        : _evadeDir;
                    _state = State.Dash;
                }
                break;

            // ── Dash: supersonic sprint on locked HORIZONTAL heading ───
            case State.Dash:
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 5f * Time.fixedDeltaTime);
                // Drive only XZ from locked direction — HoldAltitude owns Y
                Vector3 dashVel = rb.linearVelocity;
                dashVel.x = Mathf.Lerp(dashVel.x, _dashDir.x * currentSpeed, 6f * Time.fixedDeltaTime);
                dashVel.z = Mathf.Lerp(dashVel.z, _dashDir.z * currentSpeed, 6f * Time.fixedDeltaTime);
                rb.linearVelocity = dashVel;
                HoldAltitude(_targetAltitude);
                break;
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;

        // currentSpeed = horizontal magnitude only — never include vel.y from HoldAltitude
        // otherwise the altitude controller inflates speed every tick (feedback loop)
        Vector3 hVel = rb.linearVelocity;
        hVel.y = 0f;
        currentSpeed = Mathf.Clamp(hVel.magnitude, 0f, config.maxSpeed);
    }
}
