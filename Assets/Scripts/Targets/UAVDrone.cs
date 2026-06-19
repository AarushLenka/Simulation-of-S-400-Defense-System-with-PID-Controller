using UnityEngine;

public class UAVDrone : AerialTarget
{
    private enum State { Loiter, Dive }
    private State state = State.Loiter;

    public Vector3 loiterCenter;
    public float   loiterRadius = 200f;  // 1:40 scale (IRL ~8km loiter pattern)
    public float   diveTargetY  = 100f;
    private float  loiterAngle;

    protected override void InitializeTarget()
    {
        currentSpeed    = Random.Range(config.minSpeed, config.maxSpeed * 0.5f);
        currentAltitude = Random.Range(config.minAltitude, config.maxAltitude);
        loiterCenter    = transform.position;
    }

    public override void UpdateMotion()
    {
        switch (state)
        {
            case State.Loiter:
                // Circular holding pattern
                loiterAngle += (currentSpeed / loiterRadius) * Time.fixedDeltaTime;
                Vector3 offset = new Vector3(
                    Mathf.Cos(loiterAngle), 0, Mathf.Sin(loiterAngle)) * loiterRadius;
                Vector3 targetPos = loiterCenter + offset;
                targetPos.y = currentAltitude;
                rb.linearVelocity = (targetPos - transform.position).normalized * currentSpeed;

                // Commit to dive once tracked (simulates attack run or evasive descent)
                if (isTracked) state = State.Dive;
                break;

            case State.Dive:
                // Sharp descent, moderate speed increase
                currentSpeed = Mathf.MoveTowards(currentSpeed,
                    config.maxSpeed, 3f * Time.fixedDeltaTime);
                Vector3 diveDir = new Vector3(transform.forward.x, -0.6f,
                    transform.forward.z).normalized;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
                    diveDir * currentSpeed, 2f * Time.fixedDeltaTime);
                break;
        }
        currentAltitude = transform.position.y;
        transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
    }
}
