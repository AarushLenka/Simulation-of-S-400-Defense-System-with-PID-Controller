using UnityEngine;

/// <summary>
/// High-altitude fast jet. Approaches radar, orbits it, evades when tracked.
/// Always returns to engagement zone — out-of-range overrides all other states.
/// </summary>
public class StealthFighter : AerialTarget
{
    private enum State { Approach, Orbit, Evade, Dash }
    private State _state = State.Approach;

    private float   _evadeTimer;
    private float   _targetAltitude;
    private float   _orbitAngle;
    private Vector3 _dashDir;   // locked direction, never recalculated from transform.forward
    private float   _dashTimer;
    private const float OrbitRadius  = 3500f;
    private const float DashDuration = 3f;

    protected override void InitializeTarget()
    {
        currentSpeed    = config.minSpeed;
        _targetAltitude = Random.Range(config.minAltitude, config.maxAltitude);

        // Face radar from the start
        if (radarTarget != null)
        {
            Vector3 dir = (radarTarget.position - transform.position).normalized;
            rb.linearVelocity = dir * currentSpeed;
            transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            rb.linearVelocity = transform.forward * currentSpeed;
        }
    }

    public override void UpdateMotion()
    {
        // ── Out-of-range: hard override — nothing else runs ───────────
        if (IsOutOfRange())
        {
            _state = State.Approach;
            SteerTowardRadar(3f);
            HoldAltitude(_targetAltitude);
            UpdateRotation();
            currentSpeed    = config.minSpeed;
            currentAltitude = transform.position.y;
            return;
        }

        switch (_state)
        {
            // ── Approach ──────────────────────────────────────────────
            case State.Approach:
            {
                if (radarTarget == null) break;
                float dist = Vector3.Distance(transform.position, radarTarget.position);
                if (dist < OrbitRadius * 1.1f)
                {
                    _state      = State.Orbit;
                    _orbitAngle = Mathf.Atan2(
                        transform.position.z - radarTarget.position.z,
                        transform.position.x - radarTarget.position.x);
                    break;
                }
                SteerTowardRadar(2f);
                HoldAltitude(_targetAltitude);
                break;
            }

            // ── Orbit ─────────────────────────────────────────────────
            case State.Orbit:
            {
                if (isTracked) { _state = State.Evade; _evadeTimer = 3.5f; break; }

                _orbitAngle += (currentSpeed / OrbitRadius) * Time.fixedDeltaTime;
                if (radarTarget != null)
                {
                    Vector3 orbitPos = radarTarget.position
                        + new Vector3(Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle)) * OrbitRadius;
                    Vector3 dir = (orbitPos - transform.position);
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                            dir.normalized * currentSpeed, 4f * Time.fixedDeltaTime);
                }
                HoldAltitude(_targetAltitude);
                break;
            }

            // ── Evade ─────────────────────────────────────────────────
            case State.Evade:
            {
                _evadeTimer -= Time.fixedDeltaTime;
                // Evade perpendicular to radar direction, not transform.forward
                if (radarTarget != null)
                {
                    Vector3 toRadar = (radarTarget.position - transform.position).normalized;
                    Vector3 evadeDir = Vector3.Cross(toRadar, Vector3.up).normalized;
                    // Alternate left/right each evade cycle
                    evadeDir *= (Mathf.Sin(_evadeTimer * 2f) > 0f ? 1f : -1f);
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                        evadeDir * currentSpeed * 1.1f, 2f * Time.fixedDeltaTime);
                }
                HoldAltitude(_targetAltitude + Random.Range(-200f, 200f), 1f);
                if (_evadeTimer <= 0f)
                {
                    // Lock dash direction toward radar before entering dash
                    _dashDir   = radarTarget != null
                        ? (radarTarget.position - transform.position).normalized
                        : rb.linearVelocity.normalized;
                    _dashTimer = DashDuration;
                    _state     = State.Dash;
                }
                break;
            }

            // ── Dash ──────────────────────────────────────────────────
            case State.Dash:
            {
                _dashTimer -= Time.fixedDeltaTime;
                // Accelerate along the locked direction — never recalculates from transform.forward
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 12f * Time.fixedDeltaTime);
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    _dashDir * currentSpeed, 5f * Time.fixedDeltaTime);

                if (_dashTimer <= 0f)
                {
                    currentSpeed = config.minSpeed;
                    _state       = State.Orbit;
                }
                break;
            }
        }

        UpdateRotation();
        currentAltitude = transform.position.y;
        currentSpeed    = Mathf.Clamp(rb.linearVelocity.magnitude,
                                       config.minSpeed, config.maxSpeed);
    }

    private void UpdateRotation()
    {
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
    }
}
