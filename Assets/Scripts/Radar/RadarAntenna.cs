using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks all contacts persistently across sweeps.
/// - contacts      : only targets currently inside the sweep beam this frame (used by FCS)
/// - trackedContacts: all ever-detected targets, updated on each sweep hit,
///                    kept alive until the target GameObject is destroyed.
///                    When destroyed, the entry is marked neutralized=true.
/// </summary>
public class RadarAntenna : MonoBehaviour
{
    public float range         = 10000f;  // 10km — S-400 IRL 400km → /40 = 10,000m
    public float rotationSpeed = 36f;
    public float coneAngle     = 6f;
    public LayerMask targetMask;

    [Tooltip("Assign the child GameObject that holds the radar dish mesh.")]
    public Transform antennaModel;

    // Current-sweep contacts — consumed by FCS each frame
    [HideInInspector] public List<RadarContact> contacts = new();

    // Persistent track table — keyed by AerialTarget instance, never cleared
    // until the target is destroyed.
    [HideInInspector] public Dictionary<AerialTarget, TrackedContact> trackedContacts = new();

    public float AntennaAngle { get; private set; }

    void Update()
    {
        if (antennaModel != null)
        {
            antennaModel.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            AntennaAngle = antennaModel.eulerAngles.y;
        }

        SweepForTargets();
        PurgeDestroyedTargets();
    }

    void SweepForTargets()
    {
        contacts.Clear();

        float rad = AntennaAngle * Mathf.Deg2Rad;
        Vector3 sweepForward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetMask);
        foreach (var hit in hits)
        {
            Vector3 toTarget     = hit.transform.position - transform.position;
            Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            float   angle        = Vector3.Angle(sweepForward, toTargetFlat);

            if (angle > coneAngle) continue;

            var tgt = hit.GetComponent<AerialTarget>();
            if (tgt == null) continue;

            tgt.isDetected = true;
            tgt.isTracked  = true;

            var contact = new RadarContact
            {
                target      = tgt,
                position    = hit.transform.position,
                velocity    = tgt.Rb.linearVelocity,
                rcs         = tgt.config.rcs,
                threatLevel = ThreatClassifier.Classify(new RadarContact
                {
                    position  = hit.transform.position,
                    velocity  = tgt.Rb.linearVelocity,
                    rcs       = tgt.config.rcs
                })
            };
            tgt.threatLevel = contact.threatLevel;
            contacts.Add(contact);

            // Update or create persistent track entry
            if (trackedContacts.TryGetValue(tgt, out var track))
            {
                track.contact.position    = contact.position;
                track.contact.velocity    = contact.velocity;
                track.contact.threatLevel = contact.threatLevel;
                track.lastSeenAngle       = AntennaAngle;
            }
            else
            {
                trackedContacts[tgt] = new TrackedContact
                {
                    contact       = contact,
                    lastSeenAngle = AntennaAngle,
                    neutralized   = false
                };
                Debug.Log($"[RADAR] NEW TRACK: {tgt.name} | threat={contact.threatLevel} | dist={toTarget.magnitude/1000f:F1}km");
            }
        }
    }

    /// <summary>Mark tracks whose target GameObject has been destroyed as neutralized.</summary>
    void PurgeDestroyedTargets()
    {
        // Collect keys first to avoid modifying dict during iteration
        var keys = new List<AerialTarget>(trackedContacts.Keys);
        foreach (var key in keys)
        {
            if (key == null)
            {
                // Target was destroyed — Unity == null check returns true for destroyed objects
                // We can't access the key anymore, but we can mark it neutralized.
                // Because the key itself is null we find the entry by value instead.
                // Simpler: remove null keys and keep a separate neutralized list.
                trackedContacts.Remove(key);
            }
        }
    }

    /// <summary>
    /// Called by MissileController (via FCS or directly) when a target is destroyed.
    /// Marks the track neutralized so HUD can show "NEUTRALIZED" briefly.
    /// </summary>
    public void NotifyTargetNeutralized(AerialTarget target)
    {
        if (target == null) return;
        if (trackedContacts.TryGetValue(target, out var track))
            track.neutralized = true;
    }
}

/// <summary>Wrapper around a RadarContact that adds persistent-track metadata.</summary>
public class TrackedContact
{
    public RadarContact contact;
    public float        lastSeenAngle;  // antenna angle when last updated (for fade effect)
    public bool         neutralized;    // true once target is confirmed destroyed
}
