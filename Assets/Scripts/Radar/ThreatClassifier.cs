using UnityEngine;

public enum ThreatLevel { None, Low, Medium, High, Critical }

/// <summary>
/// REF §3.3 — Pure function, no memory. First match wins.
/// All values at 1:40 scale. Actual config values (m/s):
///   Bird           rcs=0.0005  spd=0.2–0.5
///   UAV/Drone      rcs=0.01    spd=7–10
///   Cruise missile rcs=0.05    spd=17–20
///   Stealth fighter rcs=0.001  spd=26–30
///   Bomber         rcs=100     spd=30–33
///   Ballistic      rcs=1       spd=45–52
/// </summary>
public static class ThreatClassifier
{
    public static ThreatLevel Classify(RadarContact c)
    {
        float spd = c.velocity.magnitude;
        float alt = c.position.y;

        // ── Rule 1: Bird ───────────────────────────────────────────────
        // rcs=0.0005. Threshold set midway between bird (0.0005) and stealth (0.001).
        // No speed condition — physics spawn spikes can momentarily push bird velocity
        // into threat ranges, so RCS alone is the reliable discriminator.
        if (c.rcs < 0.0008f)
            return ThreatLevel.None;

        // ── Rule 2: Ballistic missile ─────────────────────────────────
        // rcs=1, spd=45-52. Speed > 40 is unambiguous at 1:40 scale.
        if (spd > 40f)
            return ThreatLevel.Critical;

        // ── Rule 3: Stealth fighter ───────────────────────────────────
        // rcs=0.001, spd=26-30. Tiny RCS + fast.
        if (c.rcs < 0.01f && spd > 20f)
            return ThreatLevel.Critical;

        // ── Rule 4: Cruise missile ────────────────────────────────────
        // rcs=0.05, spd=17-20. Medium RCS + medium speed.
        // Floor rcs > 0.01 separates it from stealth/drone.
        if (c.rcs > 0.01f && c.rcs < 0.5f && spd > 14f)
            return ThreatLevel.Critical;

        // ── Rule 5: Strategic bomber ──────────────────────────────────
        // rcs=100, spd=30-33. Enormous RCS.
        if (c.rcs > 50f)
            return ThreatLevel.High;

        // ── Rule 6: UAV / Drone ───────────────────────────────────────
        // rcs=0.01, spd=7-10. Small RCS + slow.
        if (c.rcs <= 0.01f && spd <= 20f)
            return ThreatLevel.Medium;

        // ── Rule 7: Fallback ──────────────────────────────────────────
        return ThreatLevel.Low;
    }
}
