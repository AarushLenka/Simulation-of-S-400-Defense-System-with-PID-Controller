using UnityEngine;

/// <summary>
/// Two states: Cruise → Dash.
/// Cruise: flies straight toward radar at minSpeed. Does NOT evade on radar detection.
/// Dash:   accelerates to maxSpeed on locked heading after surviving missile evasion.
/// Missile evasion: when an interceptor enters missileEvadeRadius, performs a
/// smooth banking turn perpendicular to the missile's approach. Speed is maintained
/// so there is no sudden snap — just a gradual heading change like a real aircraft.
/// </summary>
public class StealthFighter : AerialTarget
{
    private enum State { Cruise, Dash }
    private State   _state = State.Cruise;
    private float   _targetAltitude;
    private Vector3 _dashDir;

    [Header("Missile Evasion")]
    [Tooltip("Distance at which the fighter reacts to an incoming interceptor (metres)")]
    public float missileEvadeRadius   = 300f;
    [Tooltip("How aggressively it banks away — lower = smoother, higher = sharper (deg/s)")]
    public float evasionTurnRate      = 60f;   // degrees per second — realistic fighter turn
    [Tooltip("Seconds before it can react to another missile")]
    public float missileEvadeCooldown = 3f;

    private float   _cooldownTimer = 0f;
    private bool    _evading       = false;
    private float   _evadeTimer    = 0f;
    private Vector3 _evadeDir      = Vector3.zero;   // target heading during evasion

    protected override void InitializeTarget()
    {
        currentSpeed    = config.minSpeed;
        float groundHere = GetTerrainYBelow();
        _targetAltitude  = groundHere + Random.Range(config.minAltitude, config.maxAltitude);

        if (radarTarget != null)
        {
            Vector3 toRadar = (radarTarget.position - transform.position);
            toRadar.y = 0f;
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
        _cooldownTimer -= Time.fixedDeltaTime;

        // ── Missile proximity check ───────────────────────────────────
        if (_cooldownTimer <= 0f)
        {
            MissileController threat = FindNearestIncomingMissile();
            if (threat != null)
            {
                StartEvasion(threat);
                _cooldownTimer = missileEvadeCooldown;
            }
        }

        // ── Heading ───────────────────────────────────────────────────
        Vector3 desiredFlat;

        if (_evading)
        {
            _evadeTimer -= Time.fixedDeltaTime;
            if (_evadeTimer <= 0f)
            {
                _evading = false;
                // Transition to Dash on heading we've turned onto
                Vector3 flatVel = rb.linearVelocity; flatVel.y = 0f;
                _dashDir = flatVel.sqrMagnitude > 0.01f ? flatVel.normalized : transform.forward;
                _state   = State.Dash;
            }
            desiredFlat = _evadeDir;
        }
        else
        {
            switch (_state)
            {
                case State.Cruise:
                    // Fly toward radar
                    if (radarTarget != null)
                    {
                        Vector3 toRadar = radarTarget.position - transform.position;
                        toRadar.y = 0f;
                        desiredFlat = toRadar.sqrMagnitude > 0.001f
                                    ? toRadar.normalized : transform.forward;
                    }
                    else desiredFlat = transform.forward;
                    break;

                case State.Dash:
                    currentSpeed = Mathf.MoveTowards(currentSpeed,
                                   config.maxSpeed, 5f * Time.fixedDeltaTime);
                    desiredFlat  = _dashDir;
                    break;

                default:
                    desiredFlat = transform.forward;
                    break;
            }
        }

        // ── Smooth banking turn — rotate current velocity toward desired ──
        // Clamp to evasionTurnRate so the aircraft sweeps through a curve,
        // not a snap. This is the key fix for the 180-on-the-spot bug.
        Vector3 currentFlat = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (currentFlat.sqrMagnitude < 0.001f) currentFlat = transform.forward;
        currentFlat.Normalize();

        float maxDeg    = evasionTurnRate * Time.fixedDeltaTime;
        Vector3 newFlat = Vector3.RotateTowards(currentFlat, desiredFlat, maxDeg * Mathf.Deg2Rad, 0f);

        rb.linearVelocity = new Vector3(
            newFlat.x * currentSpeed,
            rb.linearVelocity.y,
            newFlat.z * currentSpeed);

        HoldAltitude(_targetAltitude);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        Vector3 hVel    = rb.linearVelocity; hVel.y = 0f;
        currentSpeed    = Mathf.Clamp(hVel.magnitude, 0f, config.maxSpeed);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    MissileController FindNearestIncomingMissile()
    {
        float             bestDist = missileEvadeRadius;
        MissileController best     = null;
        var missiles = FindObjectsByType<MissileController>(FindObjectsInactive.Exclude);
        foreach (var m in missiles)
        {
            float d = Vector3.Distance(transform.position, m.transform.position);
            if (d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    void StartEvasion(MissileController missile)
    {
        // Break perpendicular to the missile's flight path, on the side
        // that maximises lateral distance from the missile.
        Vector3 missileFlat = missile.transform.forward; missileFlat.y = 0f;
        if (missileFlat.sqrMagnitude < 0.001f) missileFlat = Vector3.forward;
        missileFlat.Normalize();

        Vector3 fromMissile = transform.position - missile.transform.position;
        fromMissile.y = 0f;
        fromMissile.Normalize();

        Vector3 perpRight = new Vector3(-missileFlat.z, 0f,  missileFlat.x);
        Vector3 perpLeft  = new Vector3( missileFlat.z, 0f, -missileFlat.x);

        _evadeDir   = Vector3.Dot(perpRight, fromMissile) >= 0f ? perpRight : perpLeft;
        _evading    = true;
        _evadeTimer = missileEvadeCooldown * 0.8f;   // break for 80% of cooldown window

        Debug.Log($"[STEALTH] Evasion — banking {(_evadeDir == perpRight ? "right" : "left")} from missile");
    }
}
