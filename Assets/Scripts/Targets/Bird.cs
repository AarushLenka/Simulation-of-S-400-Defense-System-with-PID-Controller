using UnityEngine;

/// <summary>
/// Flock bird — flies in a V formation.
/// Each bird is assigned a slot offset (local space) relative to the flock leader.
/// It steers toward its world-space slot position each tick, holding the V shape.
/// Flies horizontally at fixed terrain-relative altitude. Despawns after lifetime.
/// </summary>
public class Bird : AerialTarget
{
    [Tooltip("Seconds before the flock despawns")]
    public float lifetime      = 30f;

    [Tooltip("Height above terrain to maintain")]
    public float flockAltitude = 15f;

    [Tooltip("How tightly each bird tracks its slot (higher = snappier formation)")]
    public float slotTracking  = 3f;

    // Set by TargetSpawner after Instantiate
    private Transform _leader;          // the lead bird (index 0)
    private Vector3   _slotOffset;      // local-space offset from leader
    private Vector3   _flockDir;        // shared heading
    private float     _elapsed;

    /// <summary>Called by TargetSpawner to assign this bird its V-slot.</summary>
    public void SetFormation(Transform leader, Vector3 localSlotOffset, Vector3 flockDir)
    {
        _leader     = leader;
        _slotOffset = localSlotOffset;
        _flockDir   = flockDir.normalized;
        _flockDir.y = 0f;
    }

    protected override void InitializeTarget()
    {
        currentSpeed      = Random.Range(config.minSpeed, config.maxSpeed);
        rb.linearVelocity = _flockDir * currentSpeed;
    }

    public override void UpdateMotion()
    {
        _elapsed += Time.fixedDeltaTime;
        if (_elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // ── Compute world-space slot position ────────────────────────
        // If this is the leader (_slotOffset == zero), just fly forward.
        Vector3 targetPos;
        if (_leader == null || _leader.gameObject == gameObject)
        {
            // Leader: fly straight in flock direction
            targetPos = transform.position + _flockDir * 10f;
        }
        else
        {
            // Follower: slot is leader's position + offset rotated by leader's heading
            Quaternion leaderRot = Quaternion.LookRotation(_flockDir, Vector3.up);
            targetPos = _leader.position + leaderRot * _slotOffset;
        }

        // ── Steer toward slot, horizontal only ───────────────────────
        Vector3 toSlot = targetPos - transform.position;
        toSlot.y = 0f;

        Vector3 desired = toSlot.sqrMagnitude > 0.01f ? toSlot.normalized : _flockDir;

        Vector3 currentHorizontal = new Vector3(
            rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        Vector3 newHorizontal = Vector3.Lerp(
            currentHorizontal,
            desired * currentSpeed,
            slotTracking * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);

        // ── Hold altitude ─────────────────────────────────────────────
        float groundY = GetTerrainYBelow();
        HoldAltitude(groundY + flockAltitude);

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

        currentAltitude = transform.position.y;
        currentSpeed    = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
    }
}
