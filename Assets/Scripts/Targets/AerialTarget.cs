using UnityEngine;

public abstract class AerialTarget : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Config")]
    public TargetConfig config;

    [Header("Runtime State")]
    public float       currentSpeed;
    public float       currentAltitude;
    public Vector3     velocity;
    public ThreatLevel threatLevel;
    public bool        isDetected;
    public bool        isTracked;

    [Header("Engagement")]
    [Tooltip("Target turns back toward radar beyond this distance (metres)")]
    public float engagementRadius = 14000f;

    [Header("Terrain Avoidance")]
    [Tooltip("Minimum metres above terrain enforced every physics tick")]
    public float minTerrainClearance = 15f;

    // ── Internal ──────────────────────────────────────────────────────────────
    protected Rigidbody rb;
    public    Rigidbody Rb => rb;

    protected Transform radarTarget { get; private set; }

    // Layer mask for terrain raycasts — excludes the target's own layer
    // Built once in Awake so we don't rebuild every tick
    private int _terrainMask;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity    = false;
        rb.linearDamping = 0f;

        // Exclude this object's own layer from terrain raycasts so the ray
        // never hits the target's own collider and reports a false ground height.
        _terrainMask = ~(1 << gameObject.layer);
    }

    /// <summary>Called by TargetSpawner right after Instantiate — before Start().</summary>
    public void SetRadarTarget(Transform t) => radarTarget = t;

    // Start runs one frame after Awake, so radarTarget is already set by the spawner.
    protected virtual void Start() => InitializeTarget();

    protected abstract void InitializeTarget();
    public    abstract void UpdateMotion();

    protected virtual void FixedUpdate()
    {
        UpdateMotion();
        EnforceTerrainFloor();
    }

    // ── Shared navigation helpers ─────────────────────────────────────────────

    protected bool IsOutOfRange()
    {
        if (radarTarget == null) return false;
        return Vector3.Distance(transform.position, radarTarget.position) > engagementRadius;
    }

    /// <summary>Hard-steers velocity toward radar. Early-returns so nothing else overrides it.</summary>
    protected void SteerTowardRadar(float turnRate = 4f)
    {
        if (radarTarget == null) return;
        Vector3 dir = (radarTarget.position - transform.position).normalized;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,
            dir * currentSpeed, Mathf.Clamp01(turnRate * Time.fixedDeltaTime));
    }

    // ── Altitude helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Drives the Rigidbody's vertical velocity to reach desiredWorldY.
    /// Never teleports position — avoids fighting the physics engine.
    /// </summary>
    protected void HoldAltitude(float desiredWorldY, float strength = 4f)
    {
        float error  = desiredWorldY - transform.position.y;
        // P-controller: vertical velocity proportional to altitude error, clamped
        float targetVY = Mathf.Clamp(error * strength, -60f, 60f);
        Vector3 vel  = rb.linearVelocity;
        vel.y        = Mathf.Lerp(vel.y, targetVY, 8f * Time.fixedDeltaTime);
        rb.linearVelocity = vel;
    }

    /// <summary>
    /// Terrain world-Y directly below the object.
    /// Uses a layer-masked raycast that ignores the target itself,
    /// with Terrain API fallback.
    /// </summary>
    protected float GetTerrainYBelow()
    {
        // Cast from well above — but use _terrainMask to skip own collider
        Vector3 rayOrigin = new Vector3(
            transform.position.x,
            transform.position.y + 500f,
            transform.position.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            2000f, _terrainMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        // Fallback: Unity Terrain API (works even outside terrain bounds)
        Terrain t = Terrain.activeTerrain;
        if (t != null)
            return t.SampleHeight(transform.position) + t.transform.position.y;

        return 0f;
    }

    // ── Terrain floor enforcement ─────────────────────────────────────────────

    private void EnforceTerrainFloor()
    {
        float floorY = GetTerrainYBelow() + minTerrainClearance;

        if (transform.position.y < floorY)
        {
            Vector3 pos = transform.position;
            pos.y = floorY;
            transform.position = pos;

            // Cancel downward velocity only
            if (rb.linearVelocity.y < 0f)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0f;
                rb.linearVelocity = vel;
            }
        }
    }
}
