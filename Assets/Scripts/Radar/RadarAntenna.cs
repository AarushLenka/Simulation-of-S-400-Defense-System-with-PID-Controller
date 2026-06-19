using UnityEngine;
using System.Collections.Generic;   // for List<RadarContact>

public class RadarAntenna : MonoBehaviour
{
    public float range         = 400000f; // 400 km
    public float rotationSpeed = 36f;     // degrees/sec = 1 RPM = 6 deg/s, 6 RPM = 36
    public float coneAngle     = 6f;      // half-angle of radar beam
    public LayerMask targetMask;

    [Tooltip("Assign the child GameObject that holds the radar dish mesh. " +
             "Only this object will spin — the sweep logic stays on the parent.")]
    public Transform antennaModel;

    [HideInInspector] public List<RadarContact> contacts = new();

    // Current heading of the dish in world degrees — used by RadarDisplay for sweep line
    public float AntennaAngle { get; private set; }

    void Update()
    {
        if (antennaModel != null)
        {
            antennaModel.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            // Use world Y rotation angle for sweep — avoids issues with model's local axis orientation
            AntennaAngle = antennaModel.eulerAngles.y;
        }

        SweepForTargets();
    }

    void SweepForTargets()
    {
        contacts.Clear();

        // Build sweep direction from the antenna's world Y rotation angle
        // This avoids depending on the model's local axis orientation
        float rad = AntennaAngle * Mathf.Deg2Rad;
        Vector3 sweepForward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetMask);
        foreach (var hit in hits)
        {
            Vector3 toTarget = hit.transform.position - transform.position;
            Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            float angle = Vector3.Angle(sweepForward, toTargetFlat);
            if (angle <= coneAngle)
            {
                var tgt = hit.GetComponent<AerialTarget>();
                if (tgt != null)
                {
                    tgt.isDetected = true;
                    tgt.isTracked  = true;

                    var contact = new RadarContact {
                        target   = tgt,
                        position = hit.transform.position,
                        velocity = tgt.Rb.linearVelocity,
                        rcs      = tgt.config.rcs
                    };
                    contact.threatLevel = ThreatClassifier.Classify(contact);
                    tgt.threatLevel     = contact.threatLevel;
                    contacts.Add(contact);

                    float dist = toTarget.magnitude;
                    Debug.Log($"[RADAR] CONTACT: {tgt.name} | threat={contact.threatLevel} | dist={dist/1000f:F1}km | rcs={tgt.config.rcs} | speed={contact.velocity.magnitude:F0}m/s");
                }
            }
        }
    }
}
