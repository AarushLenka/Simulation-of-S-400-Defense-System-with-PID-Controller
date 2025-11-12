using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A basic radar system that detects objects with specific tags within a given range.
/// The list of tags to detect is provided by an external system.
/// </summary>
public class RadarSystem : MonoBehaviour
{
    [Header("Radar Settings")]
    [Tooltip("The detection range of the radar in meters.")]
    [SerializeField] private float scanRadius = 40000f; // Represents 40km for simulation purposes

    [Tooltip("The layer(s) that the radar should detect.")]
    [SerializeField] private LayerMask detectionLayer;
    
    [Tooltip("How often the radar scans for targets, in seconds.")]
    [SerializeField] public float scanInterval = 1.0f;

    // A list to hold the detected targets
    private List<Transform> detectedTargets = new List<Transform>();
    private List<string> targetTags = new List<string>();
    private float scanTimer;

    void Update()
    {
        // Use a timer to control the scan frequency
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            ScanForTargets();
            scanTimer = scanInterval;
        }
    }

    /// <summary>
    /// Sets the list of tags that the radar should search for.
    /// </summary>
    public void SetTargetTags(List<string> tags)
    {
        targetTags = tags;
    }

    /// <summary>
    /// Performs the radar scan to find targets.
    /// </summary>
    private void ScanForTargets()
    {
        detectedTargets.Clear();
        if (targetTags.Count == 0) return; // Don't scan if we have no tags to look for

        Collider[] hits = Physics.OverlapSphere(transform.position, scanRadius, detectionLayer);

        foreach (Collider hit in hits)
        {
            // Check if the detected object has one of the tags from our list
            foreach (string tag in targetTags)
            {
                if (hit.CompareTag(tag))
                {
                    detectedTargets.Add(hit.transform);
                    Debug.Log(hit.name + " is at " + hit.transform.position);
                    // Break to prevent adding the same object multiple times if we're only interested in its presence
                    break; 
                }
            }
        }
    }

    /// <summary>
    /// Returns the list of currently detected targets.
    /// </summary>
    public List<Transform> GetDetectedTargets()
    {
        return detectedTargets;
    }

    /// <summary>
    /// Draws a visual representation of the radar's range in the Unity editor for easier debugging.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}

