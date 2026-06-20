using UnityEngine;

/// <summary>
/// Attach to the Radar GameObject.
/// Destroys any AerialTarget or interceptor missile that physically collides with it
/// and logs a CRITICAL HIT message.
/// </summary>
public class RadarHitHandler : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    void HandleHit(GameObject attacker)
    {
        // Only react to targets or interceptor missiles
        bool isTarget  = attacker.GetComponent<AerialTarget>() != null;
        bool isMissile = attacker.GetComponent<MissileController>() != null;

        if (!isTarget && !isMissile) return;

        Debug.LogError($"[RADAR] *** CRITICAL HIT *** — {attacker.name} struck the radar installation!");

        // Notify HUD if available
        var hud = FindFirstObjectByType<HUDController>();
        if (hud != null) hud.RegisterMiss(); // counts as a failed interception

        Destroy(attacker);
    }
}
