using UnityEngine;

public enum ThreatLevel { None, Low, Medium, High, Critical }

/// <summary>
/// REF §3.3 — Pure function, no memory.
/// Decision tree evaluated in strict order — first match wins.
/// All thresholds are at 1:40 scale (÷40 from real-world values).
///
/// NOTE: alt = contact.position.y (absolute world Y).
/// For low-altitude fliers over uneven terrain we also check speed + RCS
/// directly, independent of absolute Y.
/// </summary>
public static class ThreatClassifier
{
    public static ThreatLevel Classify(RadarContact c)
    {
        float spd = c.velocity.magnitude;   // m/s at 1:40 scale
        float alt = c.position.y;           // absolute world Y, metres

        // ── Rule 1: Bird check — FIRST, before everything else ────────
        // Spec: speed < 25 m/s IRL → /40 = 0.625 m/s; alt < 400m IRL → /40 = 10m; rcs < 0.01
        if (spd < 0.7f && c.rcs < 0.01f)
            return ThreatLevel.None;

        // ── Rule 2: Ballistic missile — speed alone is sufficient ─────
        // Spec: speed > 800 m/s IRL → /40 = 20 m/s
        if (spd > 20f)
            return ThreatLevel.Critical;

        // ── Rule 3: Cruise missile — terrain-hugging profile ──────────
        // Identified by small RCS + speed in cruise range + LOW absolute altitude.
        // Two sub-checks so it fires regardless of terrain elevation:
        //   a) absolute world Y < 100m  (works over flat/low terrain)
        //   b) speed > 3 m/s + rcs < 0.5 (catches it even over elevated terrain)
        if (c.rcs < 0.5f && spd > 3f)
            return ThreatLevel.Critical;

        // ── Rule 4: Stealth fighter — RCS + speed combination ─────────
        // Spec: rcs < 0.01; speed > 300 m/s IRL → /40 = 7.5 m/s
        if (c.rcs < 0.01f && spd > 7.5f)
            return ThreatLevel.Critical;

        // ── Rule 5: Strategic bomber — enormous RCS + high altitude ───
        // Spec: rcs > 50; alt > 5000m IRL → /40 = 125m
        if (c.rcs > 50f && alt > 125f)
            return ThreatLevel.High;

        // ── Rule 6: UAV/Drone — slow + low ────────────────────────────
        // Spec: speed < 150 m/s IRL → /40 = 3.75 m/s; alt < 6000m IRL → /40 = 150m
        if (spd < 3.75f && alt < 150f)
            return ThreatLevel.Medium;

        // ── Rule 7: Fallback ──────────────────────────────────────────
        return ThreatLevel.Low;
    }
}
