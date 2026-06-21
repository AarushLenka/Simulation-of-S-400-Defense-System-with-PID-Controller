using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a 1st-person view of every active interceptor missile on Display 4.
/// The display is split into N equal horizontal strips, one per active missile.
///
/// Setup:
///   1. Create an empty GameObject, add this component.
///   2. Set missileDisplay to 3 (Display 4) in Inspector.
///   3. Call RegisterMissile() from FireControlSystem after each launch.
///   4. Call UnregisterMissile() from MissileController on termination,
///      OR rely on the null-purge in LateUpdate.
/// </summary>
public class MissileCameraDisplay : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("Unity display index (0-based). Display 4 = index 3)")]
    public int missileDisplay = 3;

    [Header("Camera")]
    public float fieldOfView   = 80f;
    public float nearClipPlane = 0.3f;
    public float farClipPlane  = 8000f;

    // Live set of (missile, camera) pairs
    private readonly List<(MissileController missile, Camera cam)> _slots = new();

    void Awake()
    {
        if (missileDisplay < Display.displays.Length)
            Display.displays[missileDisplay].Activate();
    }

    void LateUpdate()
    {
        // Remove slots whose missile has been destroyed
        bool dirty = _slots.RemoveAll(s =>
        {
            if (s.missile != null) return false;
            if (s.cam != null) Destroy(s.cam.gameObject);
            return true;
        }) > 0;

        if (_slots.Count == 0) return;

        // Recompute viewport rects whenever the slot count changes
        if (dirty) RebuildViewports();

        // Attach each camera to its missile and look forward
        foreach (var (missile, cam) in _slots)
        {
            cam.transform.position = missile.transform.position;
            cam.transform.rotation = missile.transform.rotation;
        }
    }

    /// <summary>Called by FireControlSystem right after a missile is initialized.</summary>
    public void RegisterMissile(MissileController missile)
    {
        if (missile == null) return;

        var camGO = new GameObject($"MissileCamera_{missile.name}");
        camGO.transform.SetParent(transform, false);

        var cam = camGO.AddComponent<Camera>();
        cam.targetDisplay = missileDisplay;
        cam.depth         = 1;
        cam.fieldOfView   = fieldOfView;
        cam.nearClipPlane = nearClipPlane;
        cam.farClipPlane  = farClipPlane;
        cam.clearFlags    = CameraClearFlags.Skybox;

        _slots.Add((missile, cam));
        RebuildViewports();
    }

    /// <summary>
    /// Optional explicit unregister — safe to call from MissileController.Terminate().
    /// LateUpdate's null-purge is the fallback if this isn't called.
    /// </summary>
    public void UnregisterMissile(MissileController missile)
    {
        int idx = _slots.FindIndex(s => s.missile == missile);
        if (idx < 0) return;

        var cam = _slots[idx].cam;
        if (cam != null) Destroy(cam.gameObject);
        _slots.RemoveAt(idx);
        RebuildViewports();
    }

    // ── Viewport layout ──────────────────────────────────────────────────────

    /// <summary>
    /// Splits the display into N equal horizontal bands (top-to-bottom).
    /// Unity viewport rect: (0,0) = bottom-left, (1,1) = top-right.
    /// </summary>
    void RebuildViewports()
    {
        int n = _slots.Count;
        if (n == 0) return;

        float h = 1f / n;

        for (int i = 0; i < n; i++)
        {
            // i=0 → top strip.  Unity Y is bottom-up, so top strip starts at (1 - h*(i+1))
            float yBottom = 1f - h * (i + 1);
            _slots[i].cam.rect = new Rect(0f, yBottom, 1f, h);
        }
    }
}
