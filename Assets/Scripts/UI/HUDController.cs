using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class HUDController : MonoBehaviour
{
    public FireControlSystem fireControl;

    public TMP_Text activeMissilesText;
    public TMP_Text killCountText;
    public TMP_Text missCountText;
    public TMP_Text pauseText;

    [HideInInspector] public int kills;
    [HideInInspector] public int misses;

    void Update()
    {
        activeMissilesText.text = $"Active Missiles: {fireControl.ActiveMissileCount}";
        killCountText.text      = $"Kills: {kills}";
        missCountText.text      = $"Misses: {misses}";
        pauseText.gameObject.SetActive(Time.timeScale == 0f);

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            Time.timeScale = (Time.timeScale == 0f) ? 1f : 0f;
    }

    // Call this from MissileController.Detonate() when a target is destroyed
    public void RegisterKill()  => kills++;
    // Call this from MissileController when fuel/timeout runs out without a hit
    public void RegisterMiss() => misses++;
}
