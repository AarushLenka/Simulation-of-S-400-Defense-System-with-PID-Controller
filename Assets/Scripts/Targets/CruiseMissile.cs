using UnityEngine;

public class CruiseMissile : AerialTarget
{
    public Transform[] waypoints;
    public float terrainFollowHeight = 60f;
    private int wpIndex = 0;

    // Fallback target when no waypoints set — fly toward the radar/center
    private Transform fallbackTarget;

    protected override void InitializeTarget()
    {
        currentSpeed = Random.Range(config.minSpeed, config.maxSpeed);
        currentAltitude = Random.Range(config.minAltitude, config.maxAltitude);
        rb.linearVelocity = transform.forward * currentSpeed;

        // Use radar as fallback navigation target
        var radar = FindFirstObjectByType<RadarAntenna>();
        if (radar != null) fallbackTarget = radar.transform;
    }

    public override void UpdateMotion()
    {
        // Terrain following — stay at low altitude
        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, 600f, LayerMask.GetMask("Terrain")))
        {
            float desiredY = hit.point.y + terrainFollowHeight;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, desiredY, 3f * Time.fixedDeltaTime);
            transform.position = pos;
        }

        // Navigation — use waypoints if available, otherwise head for radar
        Vector3 navTarget = Vector3.zero;
        bool hasNav = false;

        if (waypoints != null && wpIndex < waypoints.Length)
        {
            navTarget = waypoints[wpIndex].position;
            hasNav = true;
            if (Vector3.Distance(transform.position, navTarget) < 80f) wpIndex++;
        }
        else if (fallbackTarget != null)
        {
            navTarget = fallbackTarget.position;
            hasNav = true;
        }

        if (hasNav)
        {
            Vector3 dir = (navTarget - transform.position).normalized;
            dir.y = 0f; // keep horizontal — terrain following handles altitude
            if (dir != Vector3.zero)
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, dir * currentSpeed, 2f * Time.fixedDeltaTime);
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }
}
