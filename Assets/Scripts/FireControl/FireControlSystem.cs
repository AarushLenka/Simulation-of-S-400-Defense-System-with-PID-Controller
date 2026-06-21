using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FireControlSystem : MonoBehaviour
{
    public RadarAntenna radar;
    public Transform[]  launcherPositions;

    [Header("Missile Prefabs")]
    [Tooltip("9M96E — short-range, high-agility. Used against drones and cruise missiles.")]
    public GameObject missilePrefab9M96E;
    [Tooltip("48N6DM — long-range. Used against ballistic missiles, stealth fighters, and bombers.")]
    public GameObject missilePrefab48N6DM;

    [Header("Engagement")]
    public float engagementRange = 5000f;
    public float reloadTime      = 3f;

    [Tooltip("Assign the MissileCameraDisplay component here")]
    public MissileCameraDisplay missileCameraDisplay;

    // Speed read from each prefab at Start
    [HideInInspector] public float maxSpeed9M96E  = 45f;
    [HideInInspector] public float maxSpeed48N6DM = 45f;

    // Legacy single-prefab property so external code that reads missileMaxSpeed still compiles
    [HideInInspector] public float missileMaxSpeed => Mathf.Max(maxSpeed9M96E, maxSpeed48N6DM);

    private float lastLaunchTime;

    // Targets currently being engaged (at least one interceptor in the air)
    private HashSet<AerialTarget> engagedTargets = new HashSet<AerialTarget>();

    // How many interceptors are currently active per target
    private Dictionary<AerialTarget, int> activeMissilesPerTarget = new Dictionary<AerialTarget, int>();

    void Start()
    {
        // Auto-locate prefabs by path if Inspector slots are empty
        if (missilePrefab9M96E == null)
        {
            missilePrefab9M96E = UnityEngine.Resources.Load<GameObject>("Prefabs/Missiles/9M96E");
            #if UNITY_EDITOR
            if (missilePrefab9M96E == null)
                missilePrefab9M96E = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Missiles/9M96E.prefab");
            #endif
            if (missilePrefab9M96E == null) Debug.LogError("[FCS] 9M96E prefab not assigned and could not be found!");
        }
        if (missilePrefab48N6DM == null)
        {
            missilePrefab48N6DM = UnityEngine.Resources.Load<GameObject>("Prefabs/Missiles/48N6DM");
            #if UNITY_EDITOR
            if (missilePrefab48N6DM == null)
                missilePrefab48N6DM = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Missiles/48N6DM.prefab");
            #endif
            if (missilePrefab48N6DM == null) Debug.LogError("[FCS] 48N6DM prefab not assigned and could not be found!");
        }

        if (missilePrefab9M96E != null)
        {
            var mc = missilePrefab9M96E.GetComponent<MissileController>();
            if (mc != null) { maxSpeed9M96E = mc.maxSpeed; Debug.Log($"[FCS] 9M96E max speed: {maxSpeed9M96E} m/s"); }
        }
        if (missilePrefab48N6DM != null)
        {
            var mc = missilePrefab48N6DM.GetComponent<MissileController>();
            if (mc != null) { maxSpeed48N6DM = mc.maxSpeed; Debug.Log($"[FCS] 48N6DM max speed: {maxSpeed48N6DM} m/s"); }
        }
    }

    void Update()
    {
        // Clean up destroyed targets
        engagedTargets.RemoveWhere(t => t == null);
        var deadKeys = activeMissilesPerTarget.Keys.Where(k => k == null).ToList();
        foreach (var k in deadKeys) activeMissilesPerTarget.Remove(k);

        var candidates = GetCandidateContacts();

        foreach (var contact in candidates)
        {
            if (contact.threatLevel == ThreatLevel.None) continue;
            if (contact.target == null) continue;

            // Already has an interceptor in the air — don't fire again yet
            if (engagedTargets.Contains(contact.target)) continue;

            float dist = Vector3.Distance(transform.position, contact.position);
            if (dist > engagementRange)
            {
                Debug.Log($"[FCS] {contact.target.name} out of range ({dist/1000f:F1} km > {engagementRange/1000f:F0} km)");
                continue;
            }

            Vector3 toTarget       = (contact.position - transform.position).normalized;
            float targetRadialSpeed = Vector3.Dot(contact.velocity, toTarget);
            float selectedSpeed     = SelectPrefabForContact(contact) == missilePrefab9M96E
                                      ? maxSpeed9M96E : maxSpeed48N6DM;
            float closingSpeed      = selectedSpeed - targetRadialSpeed;
            if (closingSpeed < 5f)
            {
                Debug.LogWarning($"[FCS] {contact.target.name} uncatchable — closing speed {closingSpeed:F1} m/s");
                continue;
            }

            if (Time.time - lastLaunchTime < reloadTime) continue;

            // Always fire exactly 1 missile first; follow-up is handled in OnMissileTerminated
            GameObject prefab = SelectPrefabForContact(contact);
            if (prefab == null)
            {
                Debug.LogWarning($"[FCS] No prefab available for threat {contact.threatLevel} — skipping");
                continue;
            }
            Debug.Log($"[FCS] ENGAGE {contact.target.name} | threat={contact.threatLevel} | dist={dist/1000f:F1}km | missile={prefab.name}");
            LaunchOne(contact, prefab);
            engagedTargets.Add(contact.target);
            lastLaunchTime = Time.time;
        }
    }

    void LaunchOne(RadarContact contact, GameObject prefab)
    {
        Transform launcher = NearestLauncher(contact.position);
        StartCoroutine(LaunchAfterDelay(launcher, contact.target, prefab, 0f));
    }

    IEnumerator LaunchAfterDelay(Transform launcher, AerialTarget target, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target == null) { Debug.LogWarning("[FCS] Target destroyed before launch"); yield break; }

        GameObject m = Instantiate(prefab, launcher.position, Quaternion.LookRotation(Vector3.up));
        var mc = m.GetComponent<MissileController>();
        if (mc != null)
        {
            mc.Initialize(target);
            missileCameraDisplay?.RegisterMissile(mc);
            Debug.Log($"[FCS] {prefab.name} launched from {launcher.name} → {target.name}");
        }
        else
            Debug.LogError($"[FCS] MissileController missing on: {prefab.name}");

        ActiveMissileCount++;

        if (!activeMissilesPerTarget.ContainsKey(target))
            activeMissilesPerTarget[target] = 0;
        activeMissilesPerTarget[target]++;
    }

    //   Cruise missile: 17-20 m/s  → 9M96E
    //   Drone:          7-10 m/s   → 9M96E
    //   Stealth:        26-30 m/s, rcs=0.001 → 48N6DM  (identified by RCS, not speed)
    //   Bomber:         30-33 m/s  → 48N6DM
    //   Ballistic:      45-52 m/s  → 48N6DM
    // Stealth is identified by rcs <= 0.002 to avoid relying on speed during evasive manoeuvres.
    // Cruise cutoff at 22 m/s sits between cruise max (20) and stealth min (26).
    GameObject SelectPrefabForContact(RadarContact contact)
    {
        float spd = contact.velocity.magnitude;

        // Stealth fighter: tiny RCS — route to 48N6DM regardless of speed
        if (contact.rcs <= 0.002f && contact.threatLevel == ThreatLevel.Critical)
            return missilePrefab48N6DM;

        return contact.threatLevel switch {
            ThreatLevel.Medium                   => missilePrefab9M96E,   // drone
            ThreatLevel.Low                      => missilePrefab9M96E,
            ThreatLevel.High                     => missilePrefab48N6DM,  // bomber
            ThreatLevel.Critical when spd <= 22f => missilePrefab9M96E,   // cruise missile
            ThreatLevel.Critical                 => missilePrefab48N6DM,  // ballistic
            _                                    => missilePrefab48N6DM
        };
    }

    float GetSpeedForThreat(ThreatLevel level) =>
        (level == ThreatLevel.Medium || level == ThreatLevel.Low)
            ? maxSpeed9M96E
            : maxSpeed48N6DM;

    /// <summary>
    /// Called by MissileController when it terminates (hit or miss).
    /// If it missed and the target is still alive, fire one follow-up immediately.
    /// </summary>
    public void OnMissileTerminated(bool hit, AerialTarget target)
    {
        ActiveMissileCount = Mathf.Max(0, ActiveMissileCount - 1);
        Debug.Log($"[FCS] Interceptor terminated | hit={hit} | active remaining={ActiveMissileCount}");

        var hud = FindAnyObjectByType<HUDController>();
        if (hud != null)
        {
            if (hit) hud.RegisterKill();
            else     hud.RegisterMiss();
        }

        // Decrement per-target counter
        if (target != null && activeMissilesPerTarget.ContainsKey(target))
        {
            activeMissilesPerTarget[target] = Mathf.Max(0, activeMissilesPerTarget[target] - 1);

            // Missile missed and no other interceptors are chasing this target — re-engage
            if (!hit && activeMissilesPerTarget[target] == 0)
            {
                engagedTargets.Remove(target);
                Debug.Log($"[FCS] Miss on {target.name} — re-queuing for engagement");
            }
        }

        // If it was a hit, fully clear the target from tracking
        if (hit && target != null)
        {
            engagedTargets.Remove(target);
            activeMissilesPerTarget.Remove(target);
        }
    }

    // Legacy overload so existing callers with no target arg still compile
    public void OnMissileTerminated(bool hit) => OnMissileTerminated(hit, null);

    List<RadarContact> GetCandidateContacts()
    {
        if (radar.trackedContacts != null && radar.trackedContacts.Count > 0)
        {
            var list = new List<RadarContact>();
            foreach (var kvp in radar.trackedContacts)
            {
                if (kvp.Value.neutralized) continue;
                if (kvp.Key == null) continue;
                list.Add(kvp.Value.contact);
            }
            return list;
        }
        return radar.contacts;
    }

    Transform NearestLauncher(Vector3 pos) =>
        launcherPositions.OrderBy(l => Vector3.Distance(l.position, pos)).First();

    public int ActiveMissileCount { get; private set; }
}
