using UnityEngine;
using UnityEditor;

public static class DebugMissileSpawner
{
    [MenuItem("Tools/Debug/Spawn Missiles at Launchers")]
    public static void Spawn()
    {
        // Remove old ones first
        var old = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in old)
            if (go != null && go.name.StartsWith("DEBUG_Missile_"))
                Object.DestroyImmediate(go);

        var fcs = Object.FindAnyObjectByType<FireControlSystem>();
        if (fcs == null) { Debug.LogError("[DEBUG] FireControlSystem not found"); return; }

        // Use whichever prefab is assigned — prefer 48N6DM, fall back to 9M96E
        var prefab = fcs.missilePrefab48N6DM ?? fcs.missilePrefab9M96E;
        if (prefab == null) { Debug.LogError("[DEBUG] No missile prefab assigned on FireControlSystem"); return; }

        int n = 0;
        foreach (var launcher in fcs.launcherPositions)
        {
            if (launcher == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = launcher.position;
            go.transform.rotation = Quaternion.LookRotation(Vector3.up);
            go.name = $"DEBUG_Missile_{launcher.name}";
            foreach (var m in go.GetComponents<MonoBehaviour>()) m.enabled = false;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            n++;
        }
        Debug.Log($"[DEBUG] Spawned {n} static missiles ({prefab.name}) at launcher positions.");
    }

    [MenuItem("Tools/Debug/Remove Debug Missiles")]
    public static void Remove()
    {
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        int n = 0;
        foreach (var go in all)
            if (go != null && go.name.StartsWith("DEBUG_Missile_"))
            { Object.DestroyImmediate(go); n++; }
        Debug.Log($"[DEBUG] Removed {n} debug missiles.");
    }
}
