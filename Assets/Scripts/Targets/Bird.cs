public class Bird : AerialTarget
{
    public float separationDist = 5f;
    private Bird[] flock;

    protected override void InitializeTarget()
    {
        flock = FindObjectsByType<Bird>(FindObjectsSortMode.None);
    }

    public override void UpdateMotion()
    {
        Vector3 separation = Vector3.zero, cohesion = Vector3.zero;
        foreach (Bird b in flock)
        {
            if (b == this) continue;
            float dist = Vector3.Distance(transform.position, b.transform.position);
            if (dist < separationDist) separation -= (b.transform.position - transform.position);
            cohesion += b.transform.position;
        }
        if (flock.Length > 1) cohesion = (cohesion / (flock.Length - 1)) - transform.position;
        Vector3 steer = (separation * 1.5f + cohesion * 0.5f).normalized;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
            steer * config.maxSpeed, Time.fixedDeltaTime);
    }
}
