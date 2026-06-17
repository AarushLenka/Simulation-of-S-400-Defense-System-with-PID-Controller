using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ThreatPanel : MonoBehaviour
{
    public RadarAntenna radar;
    public Transform    contentParent;   // 'Content' object inside a ScrollRect
    public GameObject   rowPrefab;        // a single TMP_Text row prefab

    private List<TMP_Text> activeRows = new();

    void Update()
    {
        // Grow the pool of rows if there are more contacts than rows
        while (activeRows.Count < radar.contacts.Count)
        {
            var row = Instantiate(rowPrefab, contentParent);
            activeRows.Add(row.GetComponent<TMP_Text>());
        }

        for (int i = 0; i < activeRows.Count; i++)
        {
            if (i < radar.contacts.Count)
            {
                var c = radar.contacts[i];
                float dist = Vector3.Distance(radar.transform.position, c.position);
                activeRows[i].gameObject.SetActive(true);
                activeRows[i].text =
                    $"{c.target.name,-18} | {c.threatLevel,-9} | " +
                    $"{dist / 1000f:F1} km | {c.velocity.magnitude:F0} m/s";
                activeRows[i].color = ColorForThreat(c.threatLevel);
            }
            else
            {
                activeRows[i].gameObject.SetActive(false);  // hide unused rows
            }
        }
    }

    Color ColorForThreat(ThreatLevel level) => level switch
    {
        ThreatLevel.Critical => Color.red,
        ThreatLevel.High     => new Color(1f, 0.55f, 0f), // orange
        ThreatLevel.Medium   => Color.yellow,
        ThreatLevel.Low      => Color.cyan,
        _                    => Color.green,             // None — birds
    };
}
