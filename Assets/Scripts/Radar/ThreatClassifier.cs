public enum ThreatLevel { None, Low, Medium, High, Critical }

public class ThreatClassifier : MonoBehaviour
{
    public static ThreatLevel Classify(RadarContact c)
    {
        float spd = c.velocity.magnitude;
        float alt = c.position.y;

        // Birds — fast exclusion
        if (spd < 25f && alt < 400f && c.rcs < 0.01f)
            return ThreatLevel.None;

        // Ballistic — very high speed + high arc
        if (spd > 800f)
            return ThreatLevel.Critical;

        // Cruise — low altitude, moderate speed
        if (alt < 200f && spd > 150f && c.rcs < 0.5f)
            return ThreatLevel.Critical;

        // Stealth fighter — low RCS, high speed
        if (c.rcs < 0.01f && spd > 300f)
            return ThreatLevel.Critical;

        // Bomber — large RCS, high alt
        if (c.rcs > 50f && alt > 5000f)
            return ThreatLevel.High;

        // UAV
        if (spd < 150f && alt < 6000f)
            return ThreatLevel.Medium;

        return ThreatLevel.Low;
    }
}
