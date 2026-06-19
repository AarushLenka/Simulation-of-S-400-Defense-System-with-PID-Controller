using UnityEngine;

public class StealthFighter : AerialTarget
{
    private enum State { Cruise, Evade, Dash }
    private State state = State.Cruise;
    private float evadeTimer;
    private Vector3 evadeDirection;

    protected override void InitializeTarget()
    {
        currentSpeed    = config.minSpeed;
        currentAltitude = Random.Range(config.minAltitude, config.maxAltitude);
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        switch (state) {
            case State.Cruise:
                // Straight flight toward target area
                rb.linearVelocity = transform.forward * currentSpeed;
                if (isTracked) { state = State.Evade; evadeTimer = 3f; }
                break;
            case State.Evade:
                // Evasive spiral — gentler to stay in scene
                evadeTimer -= Time.fixedDeltaTime;
                evadeDirection = Quaternion.Euler(0, 60, 20) * transform.forward;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    evadeDirection * currentSpeed * 1.1f, 0.03f);
                if (evadeTimer <= 0) state = State.Dash;
                break;
            case State.Dash:
                // Speed up using config max
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 5f * Time.fixedDeltaTime);
                rb.linearVelocity = transform.forward * currentSpeed;
                break;
        }
        transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
    }
}
