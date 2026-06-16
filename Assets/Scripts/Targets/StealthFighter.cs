public class StealthFighter : AerialTarget
{
    private enum State { Cruise, Evade, Dash }
    private State state = State.Cruise;
    private float evadeTimer;
    private Vector3 evadeDirection;

    protected override void InitializeTarget()
    {
        currentSpeed  = config.minSpeed;
        currentAltitude = Random.Range(5000f, 15000f);
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
                // 9g evasive spiral
                evadeTimer -= Time.fixedDeltaTime;
                evadeDirection = Quaternion.Euler(0, 90, 45) * transform.forward;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    evadeDirection * currentSpeed * 1.2f, 0.05f);
                if (evadeTimer <= 0) state = State.Dash;
                break;
            case State.Dash:
                // Supersonic sprint
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 50f * Time.fixedDeltaTime);
                rb.linearVelocity = transform.forward * currentSpeed;
                break;
        }
        transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
    }
} 
