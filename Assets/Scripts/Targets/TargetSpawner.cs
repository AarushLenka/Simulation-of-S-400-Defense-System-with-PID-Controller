using UnityEngine;
using UnityEngine.InputSystem;

public class TargetSpawner : MonoBehaviour
{
    [Header("Prefabs — assign all six in the Inspector")]
    public GameObject stealthFighterPrefab;
    public GameObject bomberPrefab;
    public GameObject uavPrefab;
    public GameObject cruiseMissilePrefab;
    public GameObject ballisticMissilePrefab;
    public GameObject birdPrefab;

    [Header("Spawn Geometry")]
    public Transform centerPoint;
    [Tooltip("Targets spawn on the edge of this radius (metres)")]
    public float spawnRadius = 12000f;

    [Header("Auto-Spawn")]
    public bool  autoSpawnEnabled  = false;
    public float autoSpawnInterval = 15f;
    private float _autoSpawnTimer;

    [Header("Bird Flock")]
    [Tooltip("Number of birds per flock")]
    public int   flockSize       = 8;
    [Tooltip("Radius within which flock members are scattered (metres)")]
    public float flockSpreadRadius = 8f;
    [Tooltip("Assign the TargetCameraDisplay component here")]
    public TargetCameraDisplay targetCameraDisplay;

    void Update()
    {
        HandleKeys();

        if (autoSpawnEnabled)
        {
            _autoSpawnTimer += Time.deltaTime;
            if (_autoSpawnTimer >= autoSpawnInterval)
            {
                _autoSpawnTimer = 0f;
                SpawnRandom();
            }
        }
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.bKey.wasPressedThisFrame) Spawn(ballisticMissilePrefab);
        if (kb.cKey.wasPressedThisFrame) Spawn(cruiseMissilePrefab);
        if (kb.fKey.wasPressedThisFrame) Spawn(stealthFighterPrefab);
        if (kb.rKey.wasPressedThisFrame) Spawn(bomberPrefab);
        if (kb.dKey.wasPressedThisFrame) for (int i = 0; i < 5; i++) Spawn(uavPrefab);
        if (kb.kKey.wasPressedThisFrame) SpawnFlock();
    }

    void SpawnRandom()
    {
        GameObject[] all = { stealthFighterPrefab, bomberPrefab, uavPrefab,
                             cruiseMissilePrefab, ballisticMissilePrefab, birdPrefab };
        Spawn(all[Random.Range(0, all.Length)]);
    }

    // ── Core spawn ────────────────────────────────────────────────────────────

    void Spawn(GameObject prefab)
    {
        if (prefab == null) { Debug.LogWarning("[SPAWNER] Prefab slot empty"); return; }

        float angle   = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 xzPos = centerPoint.position
                      + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;

        float spawnAltitude = GetSpawnAltitude(prefab, xzPos);
        Vector3 spawnPos    = new Vector3(xzPos.x, spawnAltitude, xzPos.z);

        Vector3 toRadar   = (centerPoint.position - spawnPos).normalized;
        Quaternion facing = toRadar != Vector3.zero
                          ? Quaternion.LookRotation(toRadar)
                          : Quaternion.identity;

        var go = Instantiate(prefab, spawnPos, facing);

        var target = go.GetComponent<AerialTarget>();
        if (target != null)
        {
            target.SetRadarTarget(centerPoint);
            targetCameraDisplay?.RegisterTarget(target);
        }

        Debug.Log($"[SPAWNER] {go.name} at Y={spawnAltitude:F0}m, dist={spawnRadius/1000f:F1}km");
    }

    // ── Flock spawn ───────────────────────────────────────────────────────────

    void SpawnFlock()
    {
        if (birdPrefab == null) { Debug.LogWarning("[SPAWNER] Bird prefab not assigned"); return; }

        // Pick one point on the spawn circle
        float   angle    = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 xzCenter = centerPoint.position
                         + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;

        float groundY      = SampleTerrainHeight(xzCenter);
        float spawnAltitude = groundY + 15f;

        // Shared heading toward radar
        Vector3    flockDir = (centerPoint.position - xzCenter).normalized;
        flockDir.y          = 0f;
        Quaternion facing   = Quaternion.LookRotation(flockDir);

        // V-formation slot offsets in leader-local space (forward = flockDir).
        // Pattern: leader at tip, pairs spread back and out on each wing.
        // spacing: lateral (right/left), depth (behind leader)
        float lat   = flockSpreadRadius * 0.5f;   // lateral gap between slots
        float depth = flockSpreadRadius * 0.55f;  // how far back each row sits

        // Slots: index 0 = leader, then alternating left/right pairs moving back
        Vector3[] slots = new Vector3[]
        {
            new Vector3(  0f,   0f,   0f),          // 0 — leader (tip)
            new Vector3(-lat,   0f, -depth),         // 1 — left  wing 1
            new Vector3( lat,   0f, -depth),         // 2 — right wing 1
            new Vector3(-lat*2, 0f, -depth*2),       // 3 — left  wing 2
            new Vector3( lat*2, 0f, -depth*2),       // 4 — right wing 2
            new Vector3(-lat*3, 0f, -depth*3),       // 5 — left  wing 3
            new Vector3( lat*3, 0f, -depth*3),       // 6 — right wing 3
            new Vector3(-lat*4, 0f, -depth*4),       // 7 — left  wing 4
        };

        int count = Mathf.Min(flockSize, slots.Length);

        // Spawn all birds at the leader position first (already clustered)
        var birds = new Bird[count];
        for (int i = 0; i < count; i++)
        {
            // Offset spawn position so they start roughly in their slot
            Vector3 slotWorld = xzCenter + facing * slots[i];
            slotWorld.y = spawnAltitude;

            var go     = Instantiate(birdPrefab, slotWorld, facing);
            var bird   = go.GetComponent<Bird>();
            var target = go.GetComponent<AerialTarget>();

            if (target != null)
            {
                target.SetRadarTarget(centerPoint);
                targetCameraDisplay?.RegisterTarget(target);
            }

            birds[i] = bird;
        }

        // Wire up formation — leader is birds[0]
        for (int i = 0; i < count; i++)
        {
            if (birds[i] == null) continue;
            birds[i].SetFormation(
                birds[0].transform,
                slots[i],
                flockDir);
        }

        Debug.Log($"[SPAWNER] V-flock of {count} birds spawned at dist={spawnRadius/1000f:F1}km");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    float GetSpawnAltitude(GameObject prefab, Vector3 xzPos)
    {
        float groundY = SampleTerrainHeight(xzPos);

        var cfg = prefab.GetComponent<AerialTarget>()?.config;
        if (cfg != null)
        {
            float agl = Random.Range(cfg.minAltitude, cfg.maxAltitude);
            return groundY + Mathf.Max(agl, 20f);
        }

        return groundY + 200f;
    }

    float SampleTerrainHeight(Vector3 worldPos)
    {
        if (Physics.Raycast(new Vector3(worldPos.x, 5000f, worldPos.z),
                            Vector3.down, out RaycastHit hit, 8000f, ~0,
                            QueryTriggerInteraction.Ignore))
            return hit.point.y;

        Terrain t = Terrain.activeTerrain;
        if (t != null) return t.SampleHeight(worldPos) + t.transform.position.y;

        return 0f;
    }
}
