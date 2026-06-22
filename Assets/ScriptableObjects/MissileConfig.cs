using UnityEngine;

[CreateAssetMenu(menuName = "S400/MissileConfig")]
public class MissileConfig : ScriptableObject
{
    public string  missileModel;    // e.g. "48N6E2", "40N6E"
    public float   maxSpeed;        // m/s
    public float   maxAccel;        // m/s²
    public float   maxRange;        // m
    public float   maxAltitude;     // m
    public float   fuzeRadius;      // proximity detonation (m)
    public float   navConstant;     // PN constant N'
    // PID Gains (overwritten at runtime by MATLAB bridge)
    public float   Kp_pitch, Ki_pitch, Kd_pitch;
    public float   Kp_yaw,   Ki_yaw,   Kd_yaw;
    public float   Kp_throttle, Ki_throttle, Kd_throttle;
}
