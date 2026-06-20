using UnityEngine;

/// <summary>
/// REF §2.4 — Simultaneous terrain following (Y-axis) + waypoint navigation (XZ-axis).
/// Terrain follow: downward raycast → maintain terrainFollowHeight above ground.
/// Waypoint nav: steer toward waypoints[wpIndex], advance within 100m, then next.
/// Both run every tick independently on separate axes.
/// Falls back to radarTarget position when no waypoints are assigned.
/// </summary>
public class CruiseMissile : AerialTarget
{
    [Header("Waypoints")]
    [Tooltip("Ordered sequence of world-space waypoints defining the attack corridor. " +
             "If empty, flies directly toward the radar/S-400.")]
    public Transform[] waypoints;

    [Tooltip("Advance to next waypoint when within this distance (metres)")]
    public float waypointRadius = 100f;

    [Tooltip("Height above terrain to maintain — 1:40 scale: IRL 50m → ~1.25m, use 2m for visibility")]
    public float terrainFollowHeight = 2f;

    private int _wpIndex = 0;

    protected override void InitializeTarget()
    {
        currentSpeed = Random.Range(config.minSpeed, config.maxSpeed);
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        // ── Terrain following — owns Y axis completely ─────────────────
        // Raycast already handled in base GetTerrainYBelow; we set Y directly.
        float groundY   = GetTerrainYBelow();
        float desiredY  = groundY + terrainFollowHeight;

        // Teleport Y smoothly — terrain follower owns vertical, not physics
        Vector3 pos = transform.position;
        pos.y       = Mathf.Lerp(pos.y, desiredY, 10f * Time.fixedDeltaTime);
        transform.position = pos;

        // Zero vertical velocity so physics doesn't fight the Y assignment
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        // ── Waypoint navigation — owns XZ axis ─────────────────────────
        Vector3 navTarget = GetCurrentNavTarget();
        Vector3 toNav = navTarget - transform.position;
        toNav.y = 0f;

        if (toNav.sqrMagnitude > 0.01f)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                toNav.normalized * currentSpeed, 3f * Time.fixedDeltaTime);
        }

        // Advance waypoint when close enough
        if (waypoints != null && _wpIndex < waypoints.Length)
        {
            float dist2D = new Vector2(toNav.x, toNav.z).magnitude;
            if (dist2D < waypointRadius) _wpIndex++;
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }

    /// <summary>
    /// Returns the current navigation target position.
    /// Uses waypoints if available, falls back to radar position.
    /// </summary>
    Vector3 GetCurrentNavTarget()
    {
        if (waypoints != null && _wpIndex < waypoints.Length && waypoints[_wpIndex] != null)
            return waypoints[_wpIndex].position;

        if (radarTarget != null)
            return radarTarget.position;

        return transform.position + transform.forward * 100f;
    }
}
