using UnityEngine;

public abstract class AerialTarget : MonoBehaviour
{
    [Header("Config")]
    public TargetConfig config;          // ScriptableObject with speed, RCS, etc.

    [Header("Runtime State")]
    public float currentSpeed;
    public float currentAltitude;
    public Vector3 velocity;
    public ThreatLevel threatLevel;
    public bool isDetected;
    public bool isTracked;

    protected Rigidbody rb;
    public Rigidbody Rb => rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;     // Custom gravity for ballistic only
        rb.linearDamping = 0f;
        InitializeTarget();
    }

    protected abstract void InitializeTarget();
    public    abstract void UpdateMotion();  // Called from FixedUpdate

    protected virtual void FixedUpdate() => UpdateMotion();
}
