using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A serializable class to hold a key-value pair for a tag and its corresponding RCS threat value.
/// </summary>
[System.Serializable]
public class RcsData
{
    public string tag;
    public float rcsValue;
}

/// <summary>
/// Evaluates targets detected by the RadarSystem to determine the highest threat.
/// It calculates a threat score based on distance, speed, and Radar Cross Section (simulated by tags).
/// </summary>
public class ThreatAssessmentSystem : MonoBehaviour
{
    [Header("System References")]
    [Tooltip("The radar system that provides the list of targets.")]
    [SerializeField] private RadarSystem radarSystem;

    [Header("Threat Scoring Weights")]
    [Tooltip("How much distance affects the threat score. Higher value = closer targets are much more threatening.")]
    [SerializeField] private float distanceWeight = 1000f;
    [Tooltip("How much speed affects the threat score.")]
    [SerializeField] private float speedWeight = 0.5f;
    [Tooltip("How much the target's type (RCS) affects the score.")]
    [SerializeField] private float rcsWeight = 50f;

    [Header("Engagement Rules")]
    [Tooltip("Minimum threat score required to engage a target. Prevents engaging low-threat targets like birds.")]
    [SerializeField] private float engagementThreshold = 50f;

    [Header("RCS Threat Values")]
    [Tooltip("Define RCS threat values for different target tags. The 'tag' must match exactly.")]
    public RcsData[] rcsTagValues;

    [Header("Missile Settings")]
    [Tooltip("The missile prefab to instantiate when engaging a target.")]
    [SerializeField] private GameObject missilePrefab;
    [Tooltip("Height offset for missile launch (meters above launcher).")]
    [SerializeField] private float launchHeight = 5f;
    [Tooltip("Cooldown time between missile launches (seconds).")]
    [SerializeField] private float launchCooldown = 2f;

    private Transform commandCenter;
    private Transform highestThreatTarget = null;
    private float highestThreatScore = -1f;
    private HashSet<Transform> engagedTargets = new HashSet<Transform>();
    private float lastLaunchTime = -999f;

    void Start()
    {
        // If the radar system isn't assigned in the inspector, try to find it on the same GameObject
        if (radarSystem == null)
        {
            radarSystem = GetComponent<RadarSystem>();
        }
        // The command center is this object's position, used for distance calculations
        commandCenter = transform;

        // Automatically configure the radar with the tags from our RCS list
        UpdateRadarTargetTags();
    }

    void Update()
    {
        EvaluateThreats(radarSystem.GetDetectedTargets());
    }

    /// <summary>
    /// Extracts all tags from the RcsData array and sends them to the RadarSystem.
    /// </summary>
    private void UpdateRadarTargetTags()
    {
        if (radarSystem == null)
        {
            Debug.LogError("ThreatAssessmentSystem: RadarSystem reference is not set!");
            return;
        }

        List<string> tagsToDetect = new List<string>();
        foreach (RcsData data in rcsTagValues)
        {
            if (!string.IsNullOrEmpty(data.tag))
            {
                tagsToDetect.Add(data.tag);
            }
        }

        // Send the generated list of tags to the radar system
        radarSystem.SetTargetTags(tagsToDetect);
    }

    /// <summary>
    /// Analyzes a list of targets and identifies the one with the highest threat score.
    /// </summary>
    private void EvaluateThreats(List<Transform> targets)
    {
        float maxScore = -1f;
        Transform currentHighestThreat = null;

        foreach (Transform target in targets)
        {
            // --- Calculate Threat Factors ---
            float distance = Vector3.Distance(commandCenter.position, target.position);
            if (distance == 0) distance = 0.01f;

            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;

            float rcsThreatValue = GetRcsThreatValue(target.tag);

            // --- Calculate Final Score ---
            float distanceScore = distanceWeight / distance;
            float speedScore = speed * speedWeight;
            float rcsScore = rcsThreatValue * rcsWeight;
            float score = distanceScore + speedScore + rcsScore;

            //Debug.Log($"Target: {target.name} | Dist: {distance:F1}m | Speed: {speed:F1}m/s | RCS: {rcsThreatValue} | Score: {score:F2} (D:{distanceScore:F2} S:{speedScore:F2} R:{rcsScore:F2})");

            if (score > maxScore)
            {
                maxScore = score;
                currentHighestThreat = target;
            }
        }

        // Only engage if the threat exceeds the threshold
        if (currentHighestThreat != null && maxScore >= engagementThreshold)
        {
            // Check if we haven't already engaged this target and cooldown has passed
            bool canLaunch = Time.time - lastLaunchTime >= launchCooldown;
            bool notYetEngaged = !engagedTargets.Contains(currentHighestThreat);
            
            if (notYetEngaged && canLaunch)
            {
                highestThreatTarget = currentHighestThreat;
                highestThreatScore = maxScore;
                Debug.LogWarning($"!!! ENGAGING THREAT: {highestThreatTarget.name} | Score: {highestThreatScore:F2} !!!");
                LogMissileResponse(highestThreatTarget);
                engagedTargets.Add(currentHighestThreat);
                lastLaunchTime = Time.time;
            }
            else if (!canLaunch)
            {
                Debug.Log($"Launch cooldown active. {launchCooldown - (Time.time - lastLaunchTime):F1}s remaining.");
            }
            else if (!notYetEngaged)
            {
                Debug.Log($"Target {currentHighestThreat.name} already engaged.");
            }
        }
        else
        {
            // Target doesn't meet engagement criteria
            if (currentHighestThreat != null && maxScore < engagementThreshold)
            {
                Debug.Log($"Target {currentHighestThreat.name} detected but score ({maxScore:F2}) below engagement threshold ({engagementThreshold:F2}). No action taken.");
            }
            highestThreatTarget = null;
        }
        
        // Clean up destroyed targets from engaged list
        engagedTargets.RemoveWhere(t => t == null);
    }

    /// <summary>
    /// Returns a threat modifier by looking up the target's tag in the public rcsTagValues array.
    /// </summary>
    private float GetRcsThreatValue(string tag)
    {
        foreach (RcsData data in rcsTagValues)
        {
            if (data.tag == tag)
            {
                return data.rcsValue;
            }
        }
        return 0.3f; // Default value for an unidentified target.
    }

    /// <summary>
    /// Launches a missile to engage the target.
    /// </summary>
    private void LogMissileResponse(Transform target)
    {
        float distanceToTarget = Vector3.Distance(commandCenter.position, target.position);
        string missileType;

        if (distanceToTarget > 20000f)
            missileType = "40N6 (Long Range)";
        else if (distanceToTarget > 5000f)
            missileType = "48N6DM (Medium Range)";
        else
            missileType = "9M96E2 (Short Range)";

        Debug.Log($"<color=green>ACTION: Engaging {target.name} with {missileType}. Target is at {distanceToTarget:F0}m.</color>");

        // Launch the missile
        if (missilePrefab != null)
        {
            LaunchMissile(target);
        }
        else
        {
            Debug.LogWarning("ThreatAssessmentSystem: No missile prefab assigned!");
        }
    }

    /// <summary>
    /// Instantiates and launches a missile towards the target.
    /// </summary>
    private void LaunchMissile(Transform target)
    {
        // Calculate launch position (5 meters up from command center)
        Vector3 launchPosition = commandCenter.position + Vector3.up * launchHeight;

        // Instantiate the missile
        GameObject missile = Instantiate(missilePrefab, launchPosition, Quaternion.identity);

        // Get the HomingMissile component and assign the target
        HomingMissile homingScript = missile.GetComponent<HomingMissile>();
        if (homingScript != null)
        {
            homingScript.target = target;
            Debug.Log($"<color=cyan>Missile launched at {launchPosition} targeting {target.name}</color>");
        }
        else
        {
            Debug.LogError("ThreatAssessmentSystem: Missile prefab doesn't have a HomingMissile component!");
            Destroy(missile);
        }
    }
}

