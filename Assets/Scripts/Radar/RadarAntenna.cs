public class RadarAntenna : MonoBehaviour
{
    public float range         = 400000f; // 400 km
    public float rotationSpeed = 36f;     // degrees/sec = 1 RPM = 6 deg/s, 6 RPM = 36
    public float coneAngle     = 6f;      // half-angle of radar beam
    public LayerMask targetMask;

    [HideInInspector] public List<RadarContact> contacts = new();

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        SweepForTargets();
    }

    void SweepForTargets()
    {
        contacts.Clear();
        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetMask);
        foreach (var hit in hits)
        {
            Vector3 toTarget = (hit.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle <= coneAngle)
            {
                var tgt = hit.GetComponent<AerialTarget>();
                if (tgt != null)
                {
                    tgt.isDetected = true;
                    contacts.Add(new RadarContact {
                        target   = tgt,
                        position = hit.transform.position,
                        velocity = tgt.rb.linearVelocity,
                        rcs      = tgt.config.rcs
                    });
                }
            }
        }
    }
}
