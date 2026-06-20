using UnityEngine;

/// <summary>
/// Terrain-hugging cruise missile. Follows ground contour, circles the radar
/// once it arrives. Hard out-of-range return prevents runaway.
/// </summary>
public class CruiseMissile : AerialTarget
{
    [Tooltip("Target height above terrain — 1:40 scale: IRL 30–100m → 1–3m")]
    public float terrainFollowHeight = 3f;

    private float _orbitAngle;
    private bool  _orbiting;
    private const float OrbitRadius = 1200f;

    protected override void InitializeTarget()
    {
        currentSpeed = Random.Range(config.minSpeed, config.maxSpeed);

        if (radarTarget != null)
        {
            Vector3 dir = (radarTarget.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                rb.linearVelocity  = dir.normalized * currentSpeed;
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }
        else
        {
            rb.linearVelocity = transform.forward * currentSpeed;
        }
    }

    public override void UpdateMotion()
    {
        // ── Terrain following (always active) ─────────────────────────
        float groundY  = GetTerrainYBelow();
        float desiredY = groundY + terrainFollowHeight;
        Vector3 pos    = transform.position;
        pos.y          = Mathf.Lerp(pos.y, desiredY, 8f * Time.fixedDeltaTime);
        transform.position = pos;

        // Terrain follower owns Y — zero out vertical velocity
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        // ── Hard out-of-range return ──────────────────────────────────
        if (IsOutOfRange())
        {
            _orbiting = false;
            if (radarTarget != null)
            {
                Vector3 dir = (radarTarget.position - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                        dir.normalized * currentSpeed, 6f * Time.fixedDeltaTime);
            }
        }
        else if (!_orbiting && radarTarget != null)
        {
            // ── Approach ──────────────────────────────────────────────
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(radarTarget.position.x, 0f, radarTarget.position.z));

            if (dist < OrbitRadius * 1.1f)
            {
                _orbiting   = true;
                _orbitAngle = Mathf.Atan2(
                    transform.position.z - radarTarget.position.z,
                    transform.position.x - radarTarget.position.x);
            }
            else
            {
                Vector3 dir = (radarTarget.position - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                        dir.normalized * currentSpeed, 3f * Time.fixedDeltaTime);
            }
        }
        else if (_orbiting && radarTarget != null)
        {
            // ── Orbit at terrain level ────────────────────────────────
            _orbitAngle += (currentSpeed / OrbitRadius) * Time.fixedDeltaTime;
            Vector3 orbitPos = radarTarget.position
                + new Vector3(Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle)) * OrbitRadius;
            Vector3 dir = (orbitPos - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    dir.normalized * currentSpeed, 4f * Time.fixedDeltaTime);
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = Mathf.Clamp(rb.linearVelocity.magnitude,
                                       config.minSpeed, config.maxSpeed);
    }
}
