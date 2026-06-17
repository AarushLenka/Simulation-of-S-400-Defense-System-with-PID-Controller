using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RadarDisplay : MonoBehaviour
{
    public RadarAntenna radar;
    public RawImage     radarImage;
    public int          texSize = 512;
    public float        displayRange = 400000f;

    private Texture2D   tex;
    private Color32[]   clearPixels;

    void Start()
    {
        tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        clearPixels = Enumerable.Repeat(new Color32(0, 20, 0, 200),
                                        texSize * texSize).ToArray();
        radarImage.texture = tex;
    }

    void Update()
    {
        tex.SetPixels32(clearPixels);
        DrawSweepLine();
        foreach (var c in radar.contacts)
            DrawBlip(c);
        tex.Apply();
    }

    void DrawBlip(RadarContact c)
    {
        Vector3 offset = c.position - radar.transform.position;
        float nx = offset.x / displayRange;  // -1 to 1
        float ny = offset.z / displayRange;
        int px = Mathf.RoundToInt((nx * 0.5f + 0.5f) * texSize);
        int py = Mathf.RoundToInt((ny * 0.5f + 0.5f) * texSize);
        Color32 col = c.threatLevel switch {
            ThreatLevel.Critical => new Color32(255, 0,   0,   255),
            ThreatLevel.High     => new Color32(255, 165, 0,   255),
            ThreatLevel.Medium   => new Color32(255, 255, 0,   255),
            _                    => new Color32(0,   255, 0,   255),
        };
        for (int dx = -3; dx <= 3; dx++)
        for (int dy = -3; dy <= 3; dy++)
            tex.SetPixel(px + dx, py + dy, col);
    }

    void DrawSweepLine()
    {
        float angle = radar.transform.eulerAngles.y * Mathf.Deg2Rad;
        int cx = texSize / 2, cy = texSize / 2;
        for (int r = 0; r < texSize / 2; r++)
        {
            int x = cx + Mathf.RoundToInt(r * Mathf.Sin(angle));
            int y = cy + Mathf.RoundToInt(r * Mathf.Cos(angle));
            tex.SetPixel(x, y, new Color32(0, 255, 80, 180));
        }
    }
}
