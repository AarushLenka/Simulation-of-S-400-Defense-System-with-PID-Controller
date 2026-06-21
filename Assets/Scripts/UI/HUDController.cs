using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Drives the S-400 tactical HUD (UI Toolkit).
/// Threat list is now persistent — entries stay until the target is destroyed,
/// at which point they briefly show "NEUTRALIZED" before being cleaned up.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HUDController : MonoBehaviour
{
    public FireControlSystem fireControl;
    public RadarAntenna      radar;
    public RadarDisplay      radarDisplay;

    [HideInInspector] public int kills;
    [HideInInspector] public int misses;

    // How long (seconds) a NEUTRALIZED row stays visible before being removed
    public float neutralizedDisplayTime = 4f;

    // ── UI elements ───────────────────────────────────────────────────────────
    private Label         _activeMissiles;
    private Label         _kills;
    private Label         _misses;
    private Label         _contacts;
    private Label         _antennaAngle;
    private Label         _clock;
    private Label         _keybinds;
    private VisualElement _pauseOverlay;
    private VisualElement _radarScope;
    private ScrollView    _threatList;

    // Row pool: one row per tracked contact (keyed by AerialTarget)
    private readonly Dictionary<AerialTarget, VisualElement> _rowByTarget = new();
    // Neutralized rows waiting to be removed (target → time remaining)
    private readonly Dictionary<AerialTarget, float> _neutralizedTimers = new();

    // Null-key entries for targets already destroyed — stored by row reference
    private readonly List<(VisualElement row, float timer)> _expiring = new();

    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _activeMissiles = root.Q<Label>("active-missiles-val");
        _kills          = root.Q<Label>("kills-val");
        _misses         = root.Q<Label>("misses-val");
        _contacts       = root.Q<Label>("contacts-val");
        _antennaAngle   = root.Q<Label>("antenna-angle-val");
        _clock          = root.Q<Label>("clock-label");
        _keybinds       = root.Q<Label>("keybinds-label");
        _pauseOverlay   = root.Q<VisualElement>("pause-overlay");
        _radarScope     = root.Q<VisualElement>("radar-scope");
        _threatList     = root.Q<ScrollView>("threat-list");

        if (_keybinds != null)
            _keybinds.text = "[T] CYCLE TARGET CAM   [ESC] PAUSE";
    }

    void Update()
    {
        // ── Clock ─────────────────────────────────────────────────────────────
        float t = Time.time;
        int h = (int)(t / 3600) % 24;
        int m = (int)(t / 60)   % 60;
        int s = (int)t          % 60;
        if (_clock != null) _clock.text = $"{h:D2}:{m:D2}:{s:D2}";

        // ── Stats ─────────────────────────────────────────────────────────────
        if (_activeMissiles != null)
            _activeMissiles.text = fireControl != null ? fireControl.ActiveMissileCount.ToString() : "—";
        if (_kills    != null) _kills.text    = kills.ToString();
        if (_misses   != null) _misses.text   = misses.ToString();
        if (_contacts != null)
            _contacts.text = radar != null ? radar.contacts.Count.ToString() : "—";
        if (_antennaAngle != null)
            _antennaAngle.text = radar != null ? $"{radar.AntennaAngle:000}°" : "—";

        // ── Radar texture ─────────────────────────────────────────────────────
        if (_radarScope != null && radarDisplay != null)
        {
            var tex = radarDisplay.GetRadarTexture();
            if (tex != null)
            {
                _radarScope.style.backgroundImage = new StyleBackground(Background.FromTexture2D(tex));
                _radarScope.MarkDirtyRepaint();
            }
        }

        // ── Threat list (persistent) ──────────────────────────────────────────
        UpdateThreatList();

        // ── Pause toggle ──────────────────────────────────────────────────────
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Time.timeScale = Time.timeScale == 0f ? 1f : 0f;

        _pauseOverlay?.EnableInClassList("pause-overlay--visible", Time.timeScale == 0f);
    }

    void UpdateThreatList()
    {
        if (_threatList == null || radar == null) return;

        // 1. Add or refresh rows for every currently tracked contact
        foreach (var kvp in radar.trackedContacts)
        {
            AerialTarget  tgt   = kvp.Key;
            TrackedContact track = kvp.Value;

            if (tgt == null) continue;  // destroyed — handled below

            if (!_rowByTarget.TryGetValue(tgt, out var row))
            {
                row = BuildThreatRow();
                _threatList.Add(row);
                _rowByTarget[tgt] = row;
            }

            if (track.neutralized)
                SetRowNeutralized(row, tgt.name);
            else
                RefreshThreatRow(row, track.contact);
        }

        // 2. Tick down expiring (neutralized) rows and remove when timer runs out
        for (int i = _expiring.Count - 1; i >= 0; i--)
        {
            var (row, timer) = _expiring[i];
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                _threatList.Remove(row);
                _expiring.RemoveAt(i);
            }
            else
            {
                _expiring[i] = (row, timer);
            }
        }

        // 3. Detect newly neutralized targets: their key is still in _rowByTarget
        //    but no longer in radar.trackedContacts (purged) — or key == null (destroyed)
        var toExpire = new List<AerialTarget>();
        foreach (var kvp in _rowByTarget)
        {
            AerialTarget tgt = kvp.Key;
            // Target destroyed (Unity null check) and not already expiring
            if (tgt == null)
            {
                toExpire.Add(tgt);
            }
        }
        foreach (var tgt in toExpire)
        {
            var row = _rowByTarget[tgt];
            _rowByTarget.Remove(tgt);
            // Move to expiring list — row already shows NEUTRALIZED from step 1
            // (or we set it now for targets destroyed without FCS notification)
            SetRowNeutralized(row, "TARGET");
            _expiring.Add((row, neutralizedDisplayTime));
        }
    }

    // ── Row builders ──────────────────────────────────────────────────────────

    VisualElement BuildThreatRow()
    {
        var row   = new VisualElement(); row.AddToClassList("threat-row");
        var top   = new VisualElement(); top.AddToClassList("threat-row-top");
        var name  = new Label();         name.AddToClassList("threat-name");  name.name  = "t-name";
        var badge = new Label();         badge.AddToClassList("threat-badge"); badge.name = "t-badge";
        top.Add(name); top.Add(badge);

        var bot  = new VisualElement(); bot.AddToClassList("threat-row-bottom");
        var dist = new Label(); dist.AddToClassList("threat-detail"); dist.name = "t-dist";
        var spd  = new Label(); spd.AddToClassList("threat-detail");  spd.name  = "t-spd";
        var alt  = new Label(); alt.AddToClassList("threat-detail");  alt.name  = "t-alt";
        bot.Add(dist); bot.Add(spd); bot.Add(alt);

        row.Add(top); row.Add(bot);
        return row;
    }

    void RefreshThreatRow(VisualElement row, RadarContact c)
    {
        string[] levels = { "critical", "high", "medium", "low", "none", "neutralized" };
        foreach (var l in levels)
        {
            row.RemoveFromClassList($"threat-row--{l}");
            row.Q<Label>("t-badge")?.RemoveFromClassList($"threat-badge--{l}");
        }

        string cls = c.threatLevel switch {
            ThreatLevel.Critical => "critical",
            ThreatLevel.High     => "high",
            ThreatLevel.Medium   => "medium",
            ThreatLevel.Low      => "low",
            _                    => "none"
        };
        row.AddToClassList($"threat-row--{cls}");

        var badge = row.Q<Label>("t-badge");
        badge?.AddToClassList($"threat-badge--{cls}");
        if (badge != null) badge.text = c.threatLevel.ToString().ToUpper();

        string tName = c.target != null
            ? c.target.gameObject.name.Replace("(Clone)", "").Trim().ToUpper()
            : "UNKNOWN";
        row.Q<Label>("t-name")?.Apply(l => l.text = tName);

        float d = radar != null ? Vector3.Distance(radar.transform.position, c.position) : 0f;
        row.Q<Label>("t-dist")?.Apply(l => l.text = $"DIST {d/1000f:F1}KM");
        row.Q<Label>("t-spd") ?.Apply(l => l.text = $"SPD {c.velocity.magnitude:F0}M/S");
        row.Q<Label>("t-alt") ?.Apply(l => l.text = $"ALT {c.position.y:F0}M");
    }

    void SetRowNeutralized(VisualElement row, string targetName)
    {
        string[] levels = { "critical", "high", "medium", "low", "none", "neutralized" };
        foreach (var l in levels)
        {
            row.RemoveFromClassList($"threat-row--{l}");
            row.Q<Label>("t-badge")?.RemoveFromClassList($"threat-badge--{l}");
        }

        row.AddToClassList("threat-row--neutralized");
        var badge = row.Q<Label>("t-badge");
        badge?.AddToClassList("threat-badge--neutralized");
        if (badge != null) badge.text = "NEUTRALIZED";

        string display = targetName.Replace("(Clone)", "").Trim().ToUpper();
        row.Q<Label>("t-name")?.Apply(l => l.text = display);
        row.Q<Label>("t-dist")?.Apply(l => l.text = "");
        row.Q<Label>("t-spd") ?.Apply(l => l.text = "TARGET DESTROYED");
        row.Q<Label>("t-alt") ?.Apply(l => l.text = "");
    }

    public void RegisterKill() => kills++;
    public void RegisterMiss() => misses++;
}

// Small extension so we can write row.Q<Label>("x")?.Apply(l => l.text = "y")
// without a separate variable every time.
public static class VisualElementExt
{
    public static void Apply(this Label lbl, System.Action<Label> fn) => fn(lbl);
}
