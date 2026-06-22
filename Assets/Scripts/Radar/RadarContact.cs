using UnityEngine;

[System.Serializable]
public class RadarContact
{
    public AerialTarget target;
    public Vector3      position;
    public Vector3      velocity;
    public float        rcs;
    public ThreatLevel  threatLevel;  // filled in by ThreatClassifier.Classify()
}
