using UnityEngine;

public class BallisticMissile : AerialTarget
{
    public float boostDuration = 20f;   // seconds of powered flight
    private float elapsed;
    private bool reentryStarted;

    protected override void InitializeTarget()
    {
        rb.useGravity    = false;
        rb.linearDamping  = 0f;
        rb.angularDamping = 0f;
        currentSpeed = config.minSpeed;
        // Launch at steep upward angle
        rb.linearVelocity = (transform.forward + Vector3.up * 1.5f).normalized * currentSpeed;
    }

    public override void UpdateMotion()
    {
        elapsed += Time.fixedDeltaTime;

        if (elapsed < boostDuration)
        {
            // Boost — accelerate using config values, clamped to maxSpeed
            float accel = (config.maxSpeed - config.minSpeed) / boostDuration;
            Vector3 thrustDir = (transform.forward + Vector3.up * 0.5f).normalized;
            rb.AddForce(thrustDir * accel, ForceMode.Acceleration);
            if (rb.linearVelocity.magnitude > config.maxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * config.maxSpeed;
        }
        else
        {
            rb.useGravity = true;
            if (!reentryStarted && rb.linearVelocity.y < 0)
            {
                reentryStarted = true;
                rb.linearDamping = 0.1f;
            }
        }

        currentAltitude = transform.position.y;
        currentSpeed    = rb.linearVelocity.magnitude;
    }
}
