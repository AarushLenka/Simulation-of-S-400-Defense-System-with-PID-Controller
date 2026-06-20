using UnityEngine;

public enum ThreatLevel { None, Low, Medium, High, Critical }

/// <summary>
/// REF §3.3 — Pure function, no memory.
/// Decision tree evaluated in strict order — first match wins.
/// All thresholds are at 1:40 scale (÷40 from real-world values).
/// </summary>
public static class ThreatClassifier
{
    public static ThreatLevel Classify(RadarContact c)
    {
        float spd = c.velocity.magnitude;   // m/s at 1:40 scale
        float alt = c.position.y;           // metres at 1:40 scale

        // ── Rule 1: Bird check — FIRST, before everything else ────────
        // Spec: speed < 25 m/s IRL → /40 = 0.625 m/s; alt < 400m IRL → /40 = 10m; rcs < 0.01
        // Use slightly relaxed thresholds (0.7 m/s, 12m) to account for flock dynamics
        if (spd < 0.7f && alt < 12f && c.rcs < 0.01f)
            return ThreatLevel.None;

        // ── Rule 2: Ballistic missile — speed alone is sufficient ─────
        // Spec: speed > 800 m/s IRL → /40 = 20 m/s
        if (spd > 20f)
            return ThreatLevel.Critical;

        // ── Rule 3: Cruise missile — low altitude + speed + small RCS ─
        // Spec: alt < 200m IRL → /40 = 5m; speed > 150 m/s IRL → /40 = 3.75 m/s
        if (alt < 5f && spd > 3.75f && c.rcs < 0.5f)
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
