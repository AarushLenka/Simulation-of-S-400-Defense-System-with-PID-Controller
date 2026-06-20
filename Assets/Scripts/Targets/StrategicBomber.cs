using UnityEngine;

/// <summary>
/// Large, slow, high-altitude bomber. Approaches radar in a wide orbit,
/// holds altitude band. Hard out-of-range return prevents runaway.
/// </summary>
public class StrategicBomber : AerialTarget
{
    private float _targetAltitude;
    private float _orbitAngle;
    private const float OrbitRadius = 5000f;
    private bool  _orbiting;

    protected override void InitializeTarget()
    {
        currentSpeed    = Random.Range(config.minSpeed, config.maxSpeed);
        _targetAltitude = Random.Range(config.minAltitude, config.maxAltitude);

        if (radarTarget != null)
        {
            Vector3 dir = (radarTarget.position - transform.position).normalized;
            rb.linearVelocity  = dir * currentSpeed;
            transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            rb.linearVelocity = transform.forward * currentSpeed;
        }
    }

    public override void UpdateMotion()
    {
        // ── Hard out-of-range return ──────────────────────────────────
        if (IsOutOfRange())
        {
            _orbiting = false;
            SteerTowardRadar(2f);
            HoldAltitude(_targetAltitude, 1.5f);
            if (rb.linearVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
            currentAltitude = transform.position.y;
            return;
        }

        // ── Approach ──────────────────────────────────────────────────
        if (!_orbiting && radarTarget != null)
        {
            float dist = Vector3.Distance(transform.position, radarTarget.position);
            if (dist < OrbitRadius * 1.1f)
            {
                _orbiting   = true;
                _orbitAngle = Mathf.Atan2(
                    transform.position.z - radarTarget.position.z,
                    transform.position.x - radarTarget.position.x);
            }
            else
            {
                SteerTowardRadar(1.5f);
            }
        }

        // ── Orbit ─────────────────────────────────────────────────────
        if (_orbiting && radarTarget != null)
        {
            _orbitAngle += (currentSpeed / OrbitRadius) * Time.fixedDeltaTime;
            Vector3 orbitPos = radarTarget.position
                + new Vector3(Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle)) * OrbitRadius;
            Vector3 dir = (orbitPos - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    dir.normalized * currentSpeed, 2f * Time.fixedDeltaTime);
        }

        HoldAltitude(_targetAltitude, 1.5f);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
    }
}
