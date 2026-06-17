using UnityEngine;

public class StrategicBomber : AerialTarget
{
    protected override void InitializeTarget()
    {
        currentSpeed    = Random.Range(config.minSpeed, config.maxSpeed);
        currentAltitude = Random.Range(config.minAltitude, config.maxAltitude);
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        // Straight and level — minor altitude correction only, no evasive response
        Vector3 vel = transform.forward * currentSpeed;
        vel.y = Mathf.Lerp(rb.linearVelocity.y, 0f, 0.5f * Time.fixedDeltaTime);
        rb.linearVelocity = vel;
        currentAltitude = transform.position.y;
    }
}
