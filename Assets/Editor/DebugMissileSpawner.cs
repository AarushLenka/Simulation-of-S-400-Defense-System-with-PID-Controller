using UnityEngine;
using UnityEditor;

public static class DebugMissileSpawner
{
    [MenuItem("Tools/Debug/Spawn Missiles at Launchers")]
    public static void Spawn()
    {
        // Remove old ones first
        var old = GameObject.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include);
        foreach (var go in old)
            if (go != null && go.name.StartsWith("DEBUG_Missile_"))
                Object.DestroyImmediate(go);

        var fcs = Object.FindFirstObjectByType<FireControlSystem>();
        if (fcs == null) { Debug.LogError("[DEBUG] FireControlSystem not found"); return; }
        if (fcs.missilePrefab == null) { Debug.LogError("[DEBUG] missilePrefab not assigned"); return; }

        int n = 0;
        foreach (var launcher in fcs.launcherPositions)
        {
            if (launcher == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fcs.missilePrefab);
            go.transform.position = launcher.position;
            go.transform.rotation = Quaternion.LookRotation(Vector3.up);
            go.name = $"DEBUG_Missile_{launcher.name}";
            foreach (var m in go.GetComponents<MonoBehaviour>()) m.enabled = false;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            n++;
        }
        Debug.Log($"[DEBUG] Spawned {n} static missiles at launcher positions.");
    }

    [MenuItem("Tools/Debug/Remove Debug Missiles")]
    public static void Remove()
    {
        var all = GameObject.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int n = 0;
        foreach (var go in all)
            if (go != null && go.name.StartsWith("DEBUG_Missile_"))
            { Object.DestroyImmediate(go); n++; }
        Debug.Log($"[DEBUG] Removed {n} debug missiles.");
    }
}
