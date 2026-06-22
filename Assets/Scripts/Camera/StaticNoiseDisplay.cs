using UnityEngine;

/// <summary>
/// Displays a shader graph material fullscreen on a target display whenever
/// no real camera is actively rendering to it.
///
/// Setup:
///   1. Add to an empty GameObject (one per display).
///   2. Set displayIndex (2 = Display 3, 3 = Display 4).
///   3. Assign your shader graph material to noiseMaterial in the Inspector.
/// </summary>
public class StaticNoiseDisplay : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("0-based display index. Display 3 = 2, Display 4 = 3.")]
    public int displayIndex = 2;

    [Header("Material")]
    [Tooltip("Assign your shader graph material here.")]
    public Material noiseMaterial;

    private Camera     _cam;
    private GameObject _quad;

    void Start()
    {
        if (displayIndex < Display.displays.Length)
            Display.displays[displayIndex].Activate();

        // ── Depth -99 orthographic camera ─────────────────────────────
        var camGO               = new GameObject($"StaticNoiseCam_{displayIndex}");
        camGO.transform.SetParent(transform, false);
        _cam                    = camGO.AddComponent<Camera>();
        _cam.targetDisplay      = displayIndex;
        _cam.depth              = -99;
        _cam.clearFlags         = CameraClearFlags.SolidColor;
        _cam.backgroundColor    = Color.black;
        _cam.orthographic       = true;
        _cam.orthographicSize   = 0.5f;
        _cam.nearClipPlane      = 0.01f;
        _cam.farClipPlane       = 1f;
        _cam.cullingMask        = 1 << 0; // Default layer only
        _cam.aspect             = (float)Screen.width / Mathf.Max(Screen.height, 1);

        // ── Fullscreen quad parented to camera ────────────────────────
        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quad.name  = "NoiseQuad";
        _quad.layer = 0;
        _quad.transform.SetParent(camGO.transform, false);
        _quad.transform.localPosition = new Vector3(0f, 0f, 0.5f);

        // Scale quad to fill the full display.
        // Ortho size = 0.5 → vertical half-extent = 0.5 → full height = 1.
        // Horizontal half-extent = 0.5 × aspect → full width = aspect.
        float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
        _quad.transform.localScale = new Vector3(aspect, 1f, 1f);

        Destroy(_quad.GetComponent<Collider>());

        var mr = _quad.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        if (noiseMaterial != null)
        {
            mr.material = noiseMaterial;
        }
        else
        {
            // Try to load StaticNoise.mat automatically
            #if UNITY_EDITOR
            noiseMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/StaticNoise.mat");
            #endif
            if (noiseMaterial == null)
                noiseMaterial = Resources.Load<Material>("StaticNoise");

            if (noiseMaterial != null)
            {
                mr.material = noiseMaterial;
            }
            else
            {
                mr.material = new Material(Shader.Find("Unlit/Color")) { color = Color.magenta };
                Debug.LogWarning($"[StaticNoiseDisplay] StaticNoise.mat not found. Assign it manually in the Inspector.");
            }
        }
    }

    void OnDestroy()
    {
        if (_quad != null) Destroy(_quad);
    }
}
