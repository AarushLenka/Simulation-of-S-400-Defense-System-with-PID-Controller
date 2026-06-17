using UnityEngine;

public class CruiseMissile : AerialTarget
{
    public Transform[] waypoints;
    public float terrainFollowHeight = 50f;
    private int wpIndex = 0;

    protected override void InitializeTarget()
    {
        currentSpeed    = Random.Range(config.minSpeed, config.maxSpeed);
        currentAltitude = Random.Range(config.minAltitude, config.maxAltitude);
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        // Terrain following
        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, 500f, LayerMask.GetMask("Terrain")))
        {
            float desiredY = hit.point.y + terrainFollowHeight;
            Vector3 pos    = transform.position;
            pos.y          = Mathf.Lerp(pos.y, desiredY, 2f * Time.fixedDeltaTime);
            transform.position = pos;
        }
        // Waypoint navigation
        if (wpIndex < waypoints.Length)
        {
            Vector3 dir = (waypoints[wpIndex].position - transform.position).normalized;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                dir * currentSpeed, 3f * Time.fixedDeltaTime);
            if (Vector3.Distance(transform.position, waypoints[wpIndex].position) < 100f)
                wpIndex++;
        }
        transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
    }
}
