using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FireControlSystem : MonoBehaviour
{
    public RadarAntenna radar;
    public Transform[]  launcherPositions;
    public GameObject   missilePrefab;
    public float        engagementRange  = 150000f;
    public float        reloadTime       = 3f;
    public float        missileMaxSpeed  = 2000f;

    private float lastLaunchTime;
    private HashSet<AerialTarget> engagedTargets = new HashSet<AerialTarget>();

    void Update()
    {
        engagedTargets.RemoveWhere(t => t == null);

        foreach (var contact in radar.contacts)
        {
            if (contact.threatLevel == ThreatLevel.None) continue;
            if (contact.target == null) continue;
            if (engagedTargets.Contains(contact.target)) continue;

            float dist = Vector3.Distance(transform.position, contact.position);
            if (dist > engagementRange)
            {
                Debug.Log($"[FCS] {contact.target.name} out of range ({dist/1000f:F1} km > {engagementRange/1000f:F0} km)");
                continue;
            }

            Vector3 toTarget = (contact.position - transform.position).normalized;
            float targetRadialSpeed = Vector3.Dot(contact.velocity, toTarget);
            float closingSpeed = missileMaxSpeed - targetRadialSpeed;
            if (closingSpeed < 100f)
            {
                Debug.LogWarning($"[FCS] {contact.target.name} uncatchable — closing speed {closingSpeed:F0} m/s");
                continue;
            }

            if (Time.time - lastLaunchTime < reloadTime) continue;

            int count = MissilesRequired(contact.threatLevel);
            Debug.Log($"[FCS] ENGAGE {contact.target.name} | threat={contact.threatLevel} | dist={dist/1000f:F1}km | firing {count} interceptor(s)");
            LaunchSalvo(contact, count);
            engagedTargets.Add(contact.target);
            lastLaunchTime = Time.time;
        }
    }

    int MissilesRequired(ThreatLevel level) => level switch {
        ThreatLevel.Critical => 2,
        ThreatLevel.High     => 2,
        ThreatLevel.Medium   => 1,
        _                    => 1
    };

    void LaunchSalvo(RadarContact contact, int count)
    {
        Transform launcher = NearestLauncher(contact.position);
        for (int i = 0; i < count; i++)
            StartCoroutine(LaunchAfterDelay(launcher, contact.target, i * 0.5f));
    }

    IEnumerator LaunchAfterDelay(Transform launcher, AerialTarget target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target == null) { Debug.LogWarning("[FCS] Target destroyed before launch"); yield break; }

        Vector3 dir = (target.transform.position - launcher.position).normalized;
        if (dir == Vector3.zero) dir = Vector3.up;

        GameObject m = Instantiate(missilePrefab, launcher.position, Quaternion.LookRotation(dir));
        var mc = m.GetComponent<MissileController>();
        if (mc != null)
        {
            mc.Initialize(target);
            Debug.Log($"[FCS] Interceptor launched from {launcher.name} → {target.name}");
        }
        else
            Debug.LogError($"[FCS] MissileController missing on: {missilePrefab.name}");

        ActiveMissileCount++;
    }

    public void OnMissileTerminated(bool hit)
    {
        ActiveMissileCount = Mathf.Max(0, ActiveMissileCount - 1);
        Debug.Log($"[FCS] Interceptor terminated | hit={hit} | active remaining={ActiveMissileCount}");
        var hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
        {
            if (hit) hud.RegisterKill();
            else     hud.RegisterMiss();
        }
    }

    Transform NearestLauncher(Vector3 pos) =>
        launcherPositions.OrderBy(l => Vector3.Distance(l.position, pos)).First();

    public int ActiveMissileCount { get; private set; }
}
