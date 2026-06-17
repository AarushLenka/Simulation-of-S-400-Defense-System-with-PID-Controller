using System.Collections;
using System.Linq;
using UnityEngine;

public class FireControlSystem : MonoBehaviour
{
    public RadarAntenna radar;
    public Transform[]  launcherPositions;  // S-400 TEL positions in scene
    public GameObject   missilePrefab;
    public float        engagementRange = 200000f; // 200 km
    public float        reloadTime      = 4f;      // seconds between salvos

    private float lastLaunchTime;

    void Update()
    {
        foreach (var contact in radar.contacts)
        {
            if (contact.threatLevel == ThreatLevel.None) continue; // Birds

            float dist = Vector3.Distance(
                transform.position, contact.position);

            if (dist < engagementRange &&
                Time.time - lastLaunchTime > reloadTime)
            {
                int count = MissilesRequired(contact.threatLevel);
                LaunchSalvo(contact, count);
                lastLaunchTime = Time.time;
            }
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
        {
            float delay = i * 0.5f;   // Stagger by 0.5 s
            StartCoroutine(LaunchAfterDelay(launcher, contact.target, delay));
        }
    }

    IEnumerator LaunchAfterDelay(Transform launcher,
                                 AerialTarget target, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject m = Instantiate(missilePrefab, launcher.position,
                        Quaternion.LookRotation(Vector3.up));
        m.GetComponent<MissileController>().Initialize(target);
    }

    Transform NearestLauncher(Vector3 pos) =>
        launcherPositions.OrderBy(l =>
            Vector3.Distance(l.position, pos)).First();

    // Tracked for HUDController.cs (Section 7.3) — increment on launch,
    // decrement from MissileController.Detonate() via a callback.
    public int ActiveMissileCount { get; private set; }
}
