using UnityEngine;

/// <summary>
/// All tuneable parameters for one interceptor type.
/// Assign to MissileController.config on each prefab.
/// </summary>
[CreateAssetMenu(menuName = "S400/InterceptorConfig")]
public class InterceptorConfig : ScriptableObject
{
    [Header("Identity")]
    public MissileType missileType = MissileType.Standard_48N6DM;

    [Header("Speed (1:40 scale)")]
    public float launchSpeed  = 20f;
    public float maxSpeed     = 45f;
    public float acceleration = 15f;

    [Header("Maneuverability")]
    [Tooltip("Max rotation rate deg/s")]
    public float maxTurnRate   = 180f;
    [Tooltip("Distance (m) at which guidance switches to PN terminal mode")]
    public float terminalRange = 80f;

    [Header("Proportional Navigation")]
    public float navConstant = 3f;

    [Header("Warhead")]
    public float fuzeRadius = 8f;

    [Header("Self-Destruct")]
    public float lifetime = 60f;

    [Header("Boost Phase")]
    [Tooltip("Seconds of boost before homing. 9M96E: short / direct. 48N6DM: vertical climb.")]
    public float boostDuration = 2f;

    [Header("Terrain / Obstacle Avoidance")]
    public float avoidanceLookAhead = 60f;
    [Range(0f, 1f)]
    public float avoidanceWeight    = 0.85f;

    [Header("Low-Altitude Mode (9M96E only)")]
    [Tooltip("When true the missile descends to attack altitude instead of loitering high")]
    public bool  descendToTarget    = true;
    [Tooltip("Minimum world-Y the missile will descend to when chasing low targets")]
    public float minFlightAltitude  = 5f;
}
