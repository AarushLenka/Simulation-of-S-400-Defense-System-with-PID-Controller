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

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;     // Custom gravity for ballistic only
        rb.drag = 0f;
        InitializeTarget();
    }

    protected abstract void InitializeTarget();
    public    abstract void UpdateMotion();  // Called from FixedUpdate

    protected virtual void FixedUpdate() => UpdateMotion();
}
