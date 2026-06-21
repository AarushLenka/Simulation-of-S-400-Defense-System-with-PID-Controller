using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maintains a single 3rd-person tracking camera that follows the oldest living target.
/// Renders to targetDisplay (default: Display 3, index 2).
///
/// Setup:
///   1. Create an empty GameObject, add this component.
///   2. Set targetDisplay to 2 (Display 3) in Inspector.
///   3. Call RegisterTarget() from TargetSpawner after each spawn.
/// </summary>
public class TargetCameraDisplay : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("Unity display index (0-based). Display 3 = index 2)")]
    public int targetDisplay = 2;

    [Header("3rd-Person Offset")]
    public Vector3 followOffset   = new Vector3(0f, 2f, -6f);   // behind and above
    public float   followSmoothing = 5f;

    // Ordered list of live targets — front is the primary (oldest surviving).
    private readonly List<AerialTarget> _targets = new();
    private Camera _cam;

    void Awake()
    {
        // Create the camera as a child so it stays alive independent of targets
        var camGO = new GameObject("TargetTrackingCamera");
        camGO.transform.SetParent(transform, false);
        _cam = camGO.AddComponent<Camera>();
        _cam.targetDisplay = targetDisplay;
        _cam.depth         = 1;             // render on top of nothing; main cam is depth 0
        _cam.fieldOfView   = 60f;
        _cam.nearClipPlane = 0.5f;
        _cam.farClipPlane  = 20000f;

        // Activate the display (no-op on displays already active)
        if (targetDisplay < Display.displays.Length)
            Display.displays[targetDisplay].Activate();

        _cam.gameObject.SetActive(false);   // hidden until first target registered
    }

    void LateUpdate()
    {
        // Prune destroyed targets
        _targets.RemoveAll(t => t == null);

        if (_targets.Count == 0)
        {
            _cam.gameObject.SetActive(false);
            return;
        }

        _cam.gameObject.SetActive(true);

        // Follow oldest (front of list) target
        AerialTarget primary = _targets[0];
        Vector3 desiredPos = primary.transform.TransformPoint(followOffset);
        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, desiredPos,
            followSmoothing * Time.deltaTime);

        _cam.transform.LookAt(
            primary.transform.position + Vector3.up * 2f, Vector3.up);
    }

    /// <summary>Called by TargetSpawner for every newly spawned target.</summary>
    public void RegisterTarget(AerialTarget target)
    {
        if (target != null && !_targets.Contains(target))
            _targets.Add(target);
    }
}
