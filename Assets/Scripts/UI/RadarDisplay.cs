using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class RadarDisplay : MonoBehaviour
{
    public RadarAntenna radar;
    public RawImage     radarImage;
    public int          texSize      = 512;
    public float        displayRange = 400000f;

    private Texture2D tex;
    private Color32[] clearPixels;

    // Green phosphor palette
    private static readonly Color32 ColBackground = new Color32(0,   18,  4, 220);
    private static readonly Color32 ColCritical   = new Color32(255,  50, 40, 255);
    private static readonly Color32 ColHigh       = new Color32(255, 160,  0, 255);
    private static readonly Color32 ColMedium     = new Color32(200, 255,  0, 255);
    private static readonly Color32 ColLow        = new Color32(0,  230, 80, 255);
    private static readonly Color32 ColNone       = new Color32(0,  140, 40, 180);
    private static readonly Color32 ColNeutralized= new Color32(80,  80, 80, 160); // grey

    void Start()
    {
        tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        clearPixels = Enumerable.Repeat(ColBackground, texSize * texSize).ToArray();

        if (radarImage != null)
            radarImage.texture = tex;
    }

    void Update()
    {
        if (tex == null) return;
        tex.SetPixels32(clearPixels);
        DrawRangeRings();
        DrawSweepLine();

        // Draw all persistent tracks (faded) then fresh sweep hits on top (bright)
        if (radar != null)
        {
            foreach (var kvp in radar.trackedContacts)
                DrawTrackedBlip(kvp.Value);
        }

        tex.Apply();
    }

    void DrawRangeRings()
    {
        int cx = texSize / 2, cy = texSize / 2;
        int[] radii = { texSize / 8, texSize / 4, texSize * 3 / 8 };
        Color32 ringCol = new Color32(0, 80, 30, 100);
        foreach (int r in radii)
        {
            for (int deg = 0; deg < 360; deg++)
            {
                float a = deg * Mathf.Deg2Rad;
                int x = cx + Mathf.RoundToInt(r * Mathf.Cos(a));
                int y = cy + Mathf.RoundToInt(r * Mathf.Sin(a));
                if (x >= 0 && x < texSize && y >= 0 && y < texSize)
                    tex.SetPixel(x, y, ringCol);
            }
        }
    }

    void DrawSweepLine()
    {
        if (radar == null) return;
        float angle = radar.AntennaAngle * Mathf.Deg2Rad;
        int cx = texSize / 2, cy = texSize / 2;
        int maxR = texSize / 2 - 2;
        for (int r = 0; r < maxR; r++)
        {
            int x = cx + Mathf.RoundToInt(r * Mathf.Sin(angle));
            int y = cy + Mathf.RoundToInt(r * Mathf.Cos(angle));
            if (x >= 0 && x < texSize && y >= 0 && y < texSize)
            {
                byte alpha = (byte)Mathf.Lerp(40, 220, (float)r / maxR);
                tex.SetPixel(x, y, new Color32(0, 255, 80, alpha));
            }
        }
    }

    void DrawTrackedBlip(TrackedContact track)
    {
        if (radar == null) return;

        Vector3 offset = track.contact.position - radar.transform.position;
        float nx = offset.x / displayRange;
        float ny = offset.z / displayRange;
        int px = Mathf.RoundToInt((nx * 0.5f + 0.5f) * texSize);
        int py = Mathf.RoundToInt((ny * 0.5f + 0.5f) * texSize);

        if (px < 0 || px >= texSize || py < 0 || py >= texSize) return;

        Color32 baseCol;
        if (track.neutralized)
        {
            baseCol = ColNeutralized;
        }
        else
        {
            baseCol = track.contact.threatLevel switch {
                ThreatLevel.Critical => ColCritical,
                ThreatLevel.High     => ColHigh,
                ThreatLevel.Medium   => ColMedium,
                ThreatLevel.Low      => ColLow,
                _                    => ColNone,
            };
        }

        // Fade blip based on how far the sweep has rotated since last detection.
        // After a full 360° the blip is at minimum brightness (30% alpha).
        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(radar.AntennaAngle, track.lastSeenAngle));
        float freshness  = 1f - Mathf.Clamp01(angleDelta / 360f);
        byte  alpha      = (byte)Mathf.Lerp(76, baseCol.a, freshness); // 76 = 30% of 255

        Color32 col = new Color32(baseCol.r, baseCol.g, baseCol.b, alpha);

        int blipSize = track.neutralized ? 2 : 4;
        for (int dx = -blipSize; dx <= blipSize; dx++)
        for (int dy = -blipSize; dy <= blipSize; dy++)
        {
            if (Mathf.Abs(dx) + Mathf.Abs(dy) <= blipSize)
            {
                int bx = px + dx, by = py + dy;
                if (bx >= 0 && bx < texSize && by >= 0 && by < texSize)
                    tex.SetPixel(bx, by, col);
            }
        }
    }

    public Texture2D GetRadarTexture() => tex;
}
