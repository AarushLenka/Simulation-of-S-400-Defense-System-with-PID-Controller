using UnityEngine;

public enum ThreatLevel { None, Low, Medium, High, Critical }

public static class ThreatClassifier
{
    public static ThreatLevel Classify(RadarContact c)
    {
        float spd = c.velocity.magnitude;
        float alt = c.position.y;

        // Birds — very low RCS + low altitude + slow (use RCS as primary signal)
        // rcs < 0.001 covers birds regardless of current speed
        if (c.rcs < 0.001f && alt < 500f)
            return ThreatLevel.None;

        // Ballistic missile — very high speed or very high altitude arc
        if (spd > 800f || alt > 50000f)
            return ThreatLevel.Critical;

        // Stealth fighter — very low RCS (even at low speed on first detection)
        if (c.rcs < 0.05f)
            return ThreatLevel.Critical;

        // Cruise missile — low altitude, moderate speed, low RCS
        if (alt < 500f && c.rcs < 1f)
            return ThreatLevel.Critical;

        // Strategic bomber — large RCS, high altitude
        if (c.rcs > 50f && alt > 3000f)
            return ThreatLevel.High;

        // UAV — small, slow, low altitude
        if (spd < 200f && c.rcs < 5f)
            return ThreatLevel.Medium;

        return ThreatLevel.Low;
    }
}
