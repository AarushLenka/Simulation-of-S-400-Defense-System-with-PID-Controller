using UnityEngine;

public class PIDController
{
    public float Kp, Ki, Kd;

    private float integral;
    private float lastError;
    private float lastTime;
    private bool  firstTick = true;

    public PIDController(float kp, float ki, float kd)
    { Kp = kp; Ki = ki; Kd = kd; }

    public float Update(float error, float dt)
    {
        if (firstTick) { lastError = error; firstTick = false; }

        integral  += error * dt;
        integral   = Mathf.Clamp(integral, -50f, 50f); // Anti-windup
        float deriv = (error - lastError) / Mathf.Max(dt, 0.0001f);
        lastError  = error;

        return Kp * error + Ki * integral + Kd * deriv;
    }

    public void Reset()
    { integral = 0f; lastError = 0f; firstTick = true; }
}
