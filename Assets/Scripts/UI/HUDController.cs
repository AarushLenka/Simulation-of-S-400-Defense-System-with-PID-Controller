using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Drives the S-400 tactical HUD (UI Toolkit).
/// Attach to the same GameObject as UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HUDController : MonoBehaviour
{
    public FireControlSystem fireControl;
    public RadarAntenna      radar;
    public RadarDisplay      radarDisplay;   // optional — wires radar texture into HUD

    [HideInInspector] public int kills;
    [HideInInspector] public int misses;

    // ── UI elements ───────────────────────────────────────
    private Label         _activeMissiles;
    private Label         _kills;
    private Label         _misses;
    private Label         _contacts;
    private Label         _antennaAngle;
    private Label         _clock;
    private VisualElement _pauseOverlay;
    private VisualElement _radarScope;
    private ScrollView    _threatList;

    private readonly List<VisualElement> _threatRows = new();

    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _activeMissiles = root.Q<Label>("active-missiles-val");
        _kills          = root.Q<Label>("kills-val");
        _misses         = root.Q<Label>("misses-val");
        _contacts       = root.Q<Label>("contacts-val");
        _antennaAngle   = root.Q<Label>("antenna-angle-val");
        _clock          = root.Q<Label>("clock-label");
        _pauseOverlay   = root.Q<VisualElement>("pause-overlay");
        _radarScope     = root.Q<VisualElement>("radar-scope");
        _threatList     = root.Q<ScrollView>("threat-list");

        // Wire radar texture into the scope element
        if (_radarScope != null && radarDisplay != null)
        {
            // RadarDisplay writes to its Texture2D; blit it via a RenderTexture each frame
            // For now set background to the radar texture directly
            WireRadarTexture();
        }
    }

    void WireRadarTexture()
    {
        // RadarDisplay uses a Texture2D — create a RenderTexture wrapper
        // and update it each frame in LateUpdate
    }

    void Update()
    {
        // Clock
        float t = Time.time;
        int h = (int)(t / 3600) % 24;
        int m = (int)(t / 60) % 60;
        int s = (int)t % 60;
        if (_clock != null) _clock.text = $"{h:D2}:{m:D2}:{s:D2}";

        // Stats
        if (_activeMissiles != null)
            _activeMissiles.text = fireControl != null ? fireControl.ActiveMissileCount.ToString() : "—";
        if (_kills    != null) _kills.text    = kills.ToString();
        if (_misses   != null) _misses.text   = misses.ToString();
        if (_contacts != null)
            _contacts.text = radar != null ? radar.contacts.Count.ToString() : "—";
        if (_antennaAngle != null)
            _antennaAngle.text = radar != null ? $"{radar.AntennaAngle:000}°" : "—";

        // Push radar Texture2D into the scope background each frame
        if (_radarScope != null && radarDisplay != null)
        {
            var tex = radarDisplay.GetRadarTexture();
            if (tex != null)
            {
                // Re-assign each frame to force UI Toolkit to redraw the texture
                _radarScope.style.backgroundImage = new StyleBackground(Background.FromTexture2D(tex));
                _radarScope.MarkDirtyRepaint();
            }
        }

        UpdateThreatList();

        // Pause toggle
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Time.timeScale = Time.timeScale == 0f ? 1f : 0f;

        _pauseOverlay?.EnableInClassList("pause-overlay--visible", Time.timeScale == 0f);
    }

    void UpdateThreatList()
    {
        if (_threatList == null || radar == null) return;

        var contacts = radar.contacts;

        while (_threatRows.Count < contacts.Count)
        {
            var row = BuildThreatRow();
            _threatList.Add(row);
            _threatRows.Add(row);
        }

        for (int i = 0; i < _threatRows.Count; i++)
        {
            if (i < contacts.Count)
            {
                _threatRows[i].style.display = DisplayStyle.Flex;
                RefreshThreatRow(_threatRows[i], contacts[i]);
            }
            else
            {
                _threatRows[i].style.display = DisplayStyle.None;
            }
        }
    }

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
        string[] levels = { "critical", "high", "medium", "low", "none" };
        foreach (var l in levels) {
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
        var nameLabel = row.Q<Label>("t-name");
        if (nameLabel != null) nameLabel.text = tName;

        float d = radar != null ? Vector3.Distance(radar.transform.position, c.position) : 0f;
        var distLabel = row.Q<Label>("t-dist"); if (distLabel != null) distLabel.text = $"DIST {d/1000f:F1}KM";
        var spdLabel  = row.Q<Label>("t-spd");  if (spdLabel  != null) spdLabel.text  = $"SPD {c.velocity.magnitude:F0}M/S";
        var altLabel  = row.Q<Label>("t-alt");  if (altLabel  != null) altLabel.text  = $"ALT {c.position.y:F0}M";
    }

    public void RegisterKill() => kills++;
    public void RegisterMiss() => misses++;
}
