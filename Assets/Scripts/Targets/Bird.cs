using UnityEngine;

/// <summary>
/// REF §2.6 — Boids flocking: Separation (1.5x) + Cohesion (0.5x).
/// Stays below 400m altitude (1:40: IRL low-level ~10–100m → 0.25–2.5m,
/// use 15m for visibility at scale).
/// Single bird with no flock mates flies straight indefinitely (expected per spec).
/// Must spawn 5–8 birds clustered within 50m for flocking to emerge.
/// </summary>
public class Bird : AerialTarget
{
    [Tooltip("Minimum separation distance before steering away (metres)")]
    public float separationDist = 8f;

    [Tooltip("Flock altitude above terrain — keep below 400m world, ~10m at 1:40 scale")]
    public float flockAltitude = 10f;

    private Bird[] _flock;

    protected override void InitializeTarget()
    {
        // Collect all birds in scene — includes self, filtered in UpdateMotion
        _flock       = FindObjectsByType<Bird>(FindObjectsInactive.Exclude);
        currentSpeed = Random.Range(config.minSpeed, config.maxSpeed);
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        Vector3 separation = Vector3.zero;
        Vector3 cohesion   = Vector3.zero;
        int     count      = 0;

        foreach (Bird b in _flock)
        {
            if (b == null || b == this) continue;
            float dist = Vector3.Distance(transform.position, b.transform.position);

            // Separation: steer away from nearby birds
            if (dist < separationDist && dist > 0.001f)
                separation -= (b.transform.position - transform.position);

            // Cohesion: accumulate positions to find average
            cohesion += b.transform.position;
            count++;
        }

        // Finalise cohesion: vector toward average flock position
        if (count > 0)
            cohesion = (cohesion / count) - transform.position;

        // Blend per spec — separation 1.5x, cohesion 0.5x (§2.6)
        Vector3 steer = separation * 1.5f + cohesion * 0.5f;

        // Single bird or zero steer: fly straight (expected per spec §2.6)
        if (steer.sqrMagnitude > 0.001f)
            steer = steer.normalized;
        else
            steer = rb.linearVelocity.sqrMagnitude > 0.001f
                  ? rb.linearVelocity.normalized
                  : transform.forward;

        steer.y = 0f; // altitude handled by HoldAltitude

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
            steer * currentSpeed, Time.fixedDeltaTime * 2f);

        // Hold flock altitude above terrain (must stay below 400m per spec §2.6 & §3.3 rule 1)
        float groundY = GetTerrainYBelow();
        HoldAltitude(groundY + flockAltitude);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }
}
