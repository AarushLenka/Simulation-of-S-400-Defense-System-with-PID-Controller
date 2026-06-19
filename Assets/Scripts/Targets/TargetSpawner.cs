using UnityEngine;
using UnityEngine.InputSystem;   // requires the Input System package

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
    public Transform centerPoint;       // usually the Radar/S-400 GameObject
    public float     spawnRadius = 50000f; // 50 km out
    public float     debugSpawnRadius = 500f; // close spawn for testing visibility

    [Header("Auto-Spawn (optional, for unattended testing)")]
    public bool  autoSpawnEnabled = false;
    public float autoSpawnInterval = 15f;
    private float autoSpawnTimer;

    void Update()
    {
        HandleCheatKeys();

        if (autoSpawnEnabled)
        {
            autoSpawnTimer += Time.deltaTime;
            if (autoSpawnTimer >= autoSpawnInterval)
            {
                autoSpawnTimer = 0f;
                SpawnRandom();
            }
        }
    }

    void HandleCheatKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;  // no keyboard device this frame

        if (kb.bKey.wasPressedThisFrame) Spawn(ballisticMissilePrefab);
        if (kb.cKey.wasPressedThisFrame) Spawn(cruiseMissilePrefab);
        if (kb.fKey.wasPressedThisFrame) Spawn(stealthFighterPrefab);
        if (kb.rKey.wasPressedThisFrame) Spawn(bomberPrefab);
        if (kb.dKey.wasPressedThisFrame) for (int i = 0; i < 5; i++) Spawn(uavPrefab);
        if (kb.kKey.wasPressedThisFrame) for (int i = 0; i < 8; i++) Spawn(birdPrefab);

        // T = debug spawn right next to radar so you can see it
        if (kb.tKey.wasPressedThisFrame) SpawnDebugClose(stealthFighterPrefab);
    }

    void SpawnRandom()
    {
        GameObject[] all = { stealthFighterPrefab, bomberPrefab, uavPrefab,
                            cruiseMissilePrefab, ballisticMissilePrefab, birdPrefab };
        Spawn(all[Random.Range(0, all.Length)]);
    }

    void Spawn(GameObject prefab)
    {
        if (prefab == null) { Debug.LogWarning("[SPAWNER] Prefab slot is empty"); return; }

        Vector2 randCircle = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPos = centerPoint.position +
            new Vector3(randCircle.x, Random.Range(100f, 400f), randCircle.y);

        Quaternion facing = Quaternion.LookRotation(
            (centerPoint.position - spawnPos).normalized);

        var go = Instantiate(prefab, spawnPos, facing);
        Debug.Log($"[SPAWNER] Spawned {go.name} at {spawnPos} | dist={Vector3.Distance(centerPoint.position, spawnPos)/1000f:F1}km");
    }

    void SpawnDebugClose(GameObject prefab)
    {
        if (prefab == null) { Debug.LogWarning("[SPAWNER] Prefab slot is empty"); return; }

        Vector3 spawnPos = centerPoint.position + new Vector3(debugSpawnRadius, 200f, 0f);
        Quaternion facing = Quaternion.LookRotation((centerPoint.position - spawnPos).normalized);

        var obj = Instantiate(prefab, spawnPos, facing);
        Debug.Log($"[SPAWNER] DEBUG spawn: {obj.name} at {spawnPos} | Layer: {LayerMask.LayerToName(obj.layer)}");
    }
}
