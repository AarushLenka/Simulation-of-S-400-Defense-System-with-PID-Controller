using UnityEngine;

/// <summary>
/// UAV loiters around the radar at config altitude.
/// When tracked dives toward radar, resets to loiter if out of range.
/// </summary>
public class UAVDrone : AerialTarget
{
    private enum State { Loiter, Dive }
    private State _state = State.Loiter;

    public float loiterRadius   = 1200f;
    private float _loiterAngle;
    private float _loiterAltitude;

    protected override void InitializeTarget()
    {
        currentSpeed    = Random.Range(config.minSpeed, config.maxSpeed * 0.6f);
        _loiterAltitude = Random.Range(config.minAltitude, config.maxAltitude);

        if (radarTarget != null)
        {
            _loiterAngle = Mathf.Atan2(
                transform.position.z - radarTarget.position.z,
                transform.position.x - radarTarget.position.x);
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
        // ── Hard out-of-range: snap back to loiter ────────────────────
        if (IsOutOfRange())
        {
            _state = State.Loiter;
            SteerTowardRadar(3f);
            HoldAltitude(_loiterAltitude);
            UpdateRotation();
            currentAltitude = transform.position.y;
            return;
        }

        switch (_state)
        {
            case State.Loiter:
            {
                if (isTracked) { _state = State.Dive; break; }

                _loiterAngle += (currentSpeed / loiterRadius) * Time.fixedDeltaTime;
                Vector3 center    = radarTarget != null ? radarTarget.position : transform.position;
                Vector3 orbitPos  = center
                    + new Vector3(Mathf.Cos(_loiterAngle), 0f, Mathf.Sin(_loiterAngle)) * loiterRadius;
                Vector3 dir       = (orbitPos - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                        dir.normalized * currentSpeed, 4f * Time.fixedDeltaTime);

                HoldAltitude(_loiterAltitude);
                break;
            }

            case State.Dive:
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 4f * Time.fixedDeltaTime);

                Vector3 diveTarget = radarTarget != null
                    ? radarTarget.position + Vector3.up * 30f
                    : transform.position + transform.forward * 200f;

                Vector3 diveDir = (diveTarget - transform.position).normalized;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    diveDir * currentSpeed, 3f * Time.fixedDeltaTime);

                // Once very close to radar, reset to loiter
                if (radarTarget != null &&
                    Vector3.Distance(transform.position, radarTarget.position) < 150f)
                {
                    currentSpeed = config.minSpeed;
                    _state       = State.Loiter;
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
