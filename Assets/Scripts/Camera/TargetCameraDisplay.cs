using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maintains a single 3rd-person camera on Display 3 that tracks one target at a time.
/// All spawned targets are registered; press T to cycle through them.
/// When the active target is destroyed the next one is selected automatically.
/// </summary>
public class TargetCameraDisplay : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("Unity display index (0-based). Display 3 = index 2)")]
    public int targetDisplay = 2;

    [Header("3rd-Person Offset")]
    public Vector3 followOffset    = new Vector3(0f, 2f, -6f);
    public float   followSmoothing = 5f;

    // All registered targets in spawn order
    private readonly List<AerialTarget> _targets = new();
    private int    _activeIndex = 0;
    private Camera _cam;

    void Awake()
    {
        var camGO = new GameObject("TargetTrackingCamera");
        camGO.transform.SetParent(transform, false);
        _cam = camGO.AddComponent<Camera>();
        _cam.targetDisplay = targetDisplay;
        _cam.depth         = 1;
        _cam.fieldOfView   = 60f;
        _cam.nearClipPlane = 0.5f;
        _cam.farClipPlane  = 20000f;

        if (targetDisplay < Display.displays.Length)
            Display.displays[targetDisplay].Activate();

        _cam.gameObject.SetActive(false);
    }

    void Update()
    {
        // Prune destroyed targets, keeping index valid
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            if (_targets[i] == null)
            {
                _targets.RemoveAt(i);
                // If removed target was before or at active index, adjust so we
                // don't skip the next target in line
                if (i <= _activeIndex)
                    _activeIndex = Mathf.Max(0, _activeIndex - 1);
            }
        }

        if (_targets.Count == 0)
        {
            _cam.gameObject.SetActive(false);
            return;
        }

        _cam.gameObject.SetActive(true);
        _activeIndex = Mathf.Clamp(_activeIndex, 0, _targets.Count - 1);

        // T key cycles to next target
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            _activeIndex = (_activeIndex + 1) % _targets.Count;
    }

    void LateUpdate()
    {
        if (_targets.Count == 0) return;

        AerialTarget active = _targets[_activeIndex];
        if (active == null) return;

        Vector3 desiredPos = active.transform.TransformPoint(followOffset);
        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, desiredPos,
            followSmoothing * Time.deltaTime);

        _cam.transform.LookAt(active.transform.position + Vector3.up * 2f, Vector3.up);
    }

    /// <summary>Called by TargetSpawner for every newly spawned target.</summary>
    public void RegisterTarget(AerialTarget target)
    {
        if (target != null && !_targets.Contains(target))
            _targets.Add(target);
    }
}
