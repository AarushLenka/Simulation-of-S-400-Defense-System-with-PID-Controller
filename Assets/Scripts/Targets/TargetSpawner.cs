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
    public Transform centerPoint;           // Radar / S-400 GameObject
    [Tooltip("Targets spawn on the edge of this radius (metres)")]
    public float spawnRadius = 12000f;      // 12 km — within S-400 engagement envelope
    [Tooltip("Debug key T spawns this close")]
    public float debugSpawnRadius = 600f;

    [Header("Auto-Spawn")]
    public bool  autoSpawnEnabled  = false;
    public float autoSpawnInterval = 15f;
    private float _autoSpawnTimer;

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
        if (kb.kKey.wasPressedThisFrame) for (int i = 0; i < 8; i++) Spawn(birdPrefab);
        if (kb.tKey.wasPressedThisFrame) SpawnDebugClose(stealthFighterPrefab);
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

        // Random point on the spawn circle
        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 xzPos = centerPoint.position
                      + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;

        // Use the prefab's TargetConfig for correct altitude band
        float spawnAltitude = GetSpawnAltitude(prefab, xzPos);
        Vector3 spawnPos    = new Vector3(xzPos.x, spawnAltitude, xzPos.z);

        // Face the radar
        Vector3 toRadar     = (centerPoint.position - spawnPos).normalized;
        Quaternion facing   = toRadar != Vector3.zero
                            ? Quaternion.LookRotation(toRadar)
                            : Quaternion.identity;

        var go = Instantiate(prefab, spawnPos, facing);

        // Pass radar reference so targets can orbit/return
        var target = go.GetComponent<AerialTarget>();
        if (target != null) target.SetRadarTarget(centerPoint);

        Debug.Log($"[SPAWNER] {go.name} at Y={spawnAltitude:F0}m, dist={spawnRadius/1000f:F1}km");
    }

    void SpawnDebugClose(GameObject prefab)
    {
        if (prefab == null) return;

        float angle   = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 xzPos = centerPoint.position
                      + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * debugSpawnRadius;

        float spawnAltitude = GetSpawnAltitude(prefab, xzPos);
        Vector3 spawnPos    = new Vector3(xzPos.x, spawnAltitude, xzPos.z);

        Vector3 toRadar   = (centerPoint.position - spawnPos).normalized;
        Quaternion facing = Quaternion.LookRotation(toRadar);

        var go = Instantiate(prefab, spawnPos, facing);
        var target = go.GetComponent<AerialTarget>();
        if (target != null) target.SetRadarTarget(centerPoint);

        Debug.Log($"[SPAWNER] DEBUG {go.name} at {spawnPos}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the prefab's TargetConfig altitude range and returns a world Y spawn position.
    /// Config altitudes are treated as height ABOVE TERRAIN, not absolute world Y.
    /// </summary>
    float GetSpawnAltitude(GameObject prefab, Vector3 xzPos)
    {
        float groundY = SampleTerrainHeight(xzPos);

        var cfg = prefab.GetComponent<AerialTarget>()?.config;
        if (cfg != null)
        {
            // Config altitudes = above-terrain height, so add ground elevation
            float agl = Random.Range(cfg.minAltitude, cfg.maxAltitude);
            return groundY + Mathf.Max(agl, 20f); // always at least 20m above ground
        }

        return groundY + 200f;
    }

    /// <summary>Terrain world-Y at a given XZ position.</summary>
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
