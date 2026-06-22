using UnityEngine;

[CreateAssetMenu(menuName = "S400/TargetConfig")]
public class TargetConfig : ScriptableObject
{
    public string  targetTypeName;
    public float   minSpeed;
    public float   maxSpeed;
    public float   minAltitude;
    public float   maxAltitude;
    public float   rcs;             // Radar Cross Section (m²)
    public float   maxManeuverG;    // Max maneuvering force in G
    public ThreatLevel defaultThreat;
}
