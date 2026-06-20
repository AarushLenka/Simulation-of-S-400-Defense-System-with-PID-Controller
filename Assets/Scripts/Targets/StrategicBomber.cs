using UnityEngine;

/// <summary>
/// REF §2.2 — No state machine. Straight and level forever.
/// Never evades, never changes speed, never reacts to isTracked.
/// Only active behaviour: gentle altitude correction back to cruise altitude
/// if physics nudges it off level flight.
/// </summary>
public class StrategicBomber : AerialTarget
{
    private float _cruiseAltitude;

    protected override void InitializeTarget()
    {
        currentSpeed = Random.Range(config.minSpeed, config.maxSpeed);
        float groundAtRadar = radarTarget != null ? GetTerrainYBelow() : 0f;
        _cruiseAltitude = groundAtRadar + Random.Range(config.minAltitude, config.maxAltitude);

        // Straight inbound heading toward radar
        if (radarTarget != null)
        {
            Vector3 dir = (radarTarget.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                dir = dir.normalized;
                transform.rotation = Quaternion.LookRotation(dir);
                rb.linearVelocity  = dir * currentSpeed;
            }
        }
        else
        {
            rb.linearVelocity = transform.forward * currentSpeed;
        }
    }

    public override void UpdateMotion()
    {
        // XZ: maintain heading — lerp only horizontal components, never zero out Y
        Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horizontal.sqrMagnitude > 0.001f)
        {
            Vector3 vel = rb.linearVelocity;
            Vector3 xzTarget = horizontal.normalized * currentSpeed;
            vel.x = Mathf.Lerp(vel.x, xzTarget.x, 2f * Time.fixedDeltaTime);
            vel.z = Mathf.Lerp(vel.z, xzTarget.z, 2f * Time.fixedDeltaTime);
            rb.linearVelocity = vel;
        }

        // Y: gentle altitude correction — the only "active" behaviour per spec §2.2
        HoldAltitude(_cruiseAltitude, 1.5f);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        // Horizontal speed only — exclude HoldAltitude's vel.y to prevent feedback loop
        Vector3 hv = rb.linearVelocity; hv.y = 0f;
        currentSpeed = Mathf.Clamp(hv.magnitude, 0f, config.maxSpeed);
    }
}
