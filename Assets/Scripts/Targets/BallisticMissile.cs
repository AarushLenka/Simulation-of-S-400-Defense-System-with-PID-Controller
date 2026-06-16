public class BallisticMissile : AerialTarget
{
    public float boostThrust  = 15000f; // N
    public float boostDuration = 60f;   // seconds
    private float elapsed;
    private bool reentryStarted;

    protected override void InitializeTarget()
    {
        rb.useGravity = false;
        rb.linearVelocity = (transform.forward + Vector3.up * 2f).normalized * 300f;
    }

    public override void UpdateMotion()
    {
        elapsed += Time.fixedDeltaTime;

        if (elapsed < boostDuration)
        {
            // Boost phase — thrust upward + forward
            Vector3 thrust = (transform.forward + Vector3.up).normalized * boostThrust;
            rb.AddForce(thrust * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }
        else
        {
            // Coast + reentry — real gravity
            rb.useGravity = true;
            if (!reentryStarted && rb.linearVelocity.y < 0)
            {
                reentryStarted = true;
                // Add atmospheric drag approximation at reentry
                rb.drag = 0.05f;
            }
        }
        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }
}
