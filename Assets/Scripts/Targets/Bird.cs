using UnityEngine;

/// <summary>
/// Bird flock using simple boid rules — stays near radar at low altitude.
/// </summary>
public class Bird : AerialTarget
{
    public float separationDist = 8f;
    [Tooltip("Altitude above terrain for the flock")]
    public float flockAltitude = 80f;

    private Bird[] _flock;

    protected override void InitializeTarget()
    {
        _flock = FindObjectsByType<Bird>(FindObjectsInactive.Exclude);
        currentSpeed = Random.Range(config.minSpeed, config.maxSpeed);
    }

    public override void UpdateMotion()
    {
        // ── Boid steering ─────────────────────────────────────────────
        Vector3 separation = Vector3.zero;
        Vector3 cohesion   = Vector3.zero;
        int     neighbours = 0;

        foreach (Bird b in _flock)
        {
            if (b == this || b == null) continue;
            float dist = Vector3.Distance(transform.position, b.transform.position);
            if (dist < separationDist)
                separation -= (b.transform.position - transform.position);
            cohesion += b.transform.position;
            neighbours++;
        }

        if (neighbours > 0) cohesion = (cohesion / neighbours) - transform.position;

        // ── Radar attraction — keeps flock near the radar ─────────────
        Vector3 radarPull = Vector3.zero;
        if (IsOutOfRange() && radarTarget != null)
            radarPull = (radarTarget.position - transform.position).normalized * 3f;

        Vector3 steer = (separation * 1.5f + cohesion * 0.5f + radarPull).normalized;
        steer.y = 0f; // altitude handled separately below

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
            steer * currentSpeed, Time.fixedDeltaTime * 2f);

        // ── Altitude: hover at terrain + flockAltitude ────────────────
        float groundY = GetTerrainYBelow();
        HoldAltitude(groundY + flockAltitude, 3f);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
    }
}
