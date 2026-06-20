using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// Run via Tools > Match Terrain To Skybox.
/// Applies TerrainDemoScene visual assets to the SampleScene terrain:
///  - TerrainLit material
///  - Terrain layers (grass, moss, cliff, heather)
///  - MorningSun directional light
///  - GlobalVolume post-process
///  - SkyboxMountains prefab (if not already in scene)
///  - Fog settings matching the skybox atmosphere
/// </summary>
public static class MatchTerrainToSkybox
{
    [MenuItem("Tools/Match Terrain To Skybox")]
    public static void Apply()
    {
        int steps = 0;

        // ── 1. Terrain material ───────────────────────────────────────
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("[TerrainMatch] No Terrain found in scene."); return; }

        var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/TerrainDemoScene_URP/Terrain/Materials/TerrainLit.mat");
        if (terrainMat != null)
        {
            terrain.materialTemplate = terrainMat;
            EditorUtility.SetDirty(terrain);
            Debug.Log("[TerrainMatch] Applied TerrainLit material");
            steps++;
        }
        else Debug.LogWarning("[TerrainMatch] TerrainLit.mat not found");

        // ── 2. Terrain layers ─────────────────────────────────────────
        // Mountain-matching layer order:
        //   0 = Grass_A       (valley floors)
        //   1 = Grass_Moss_A  (mid slopes)
        //   2 = Cliff_Mossy_E (steep rocky faces — matches skybox rock colour)
        //   3 = Heather_A     (upper slopes / ridgelines)
        string[] layerPaths = {
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_Moss_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Cliff_Mossy_E.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Heather_A.terrainlayer",
        };

        var layers = layerPaths
            .Select(p => AssetDatabase.LoadAssetAtPath<TerrainLayer>(p))
            .Where(l => l != null)
            .ToArray();

        if (layers.Length > 0)
        {
            terrain.terrainData.terrainLayers = layers;
            EditorUtility.SetDirty(terrain.terrainData);
            Debug.Log($"[TerrainMatch] Applied {layers.Length} terrain layers");
            steps++;
        }
        else Debug.LogWarning("[TerrainMatch] No terrain layers found");

        // ── 3. Terrain height & detail settings ───────────────────────
        // Pixel error lower = higher quality mesh, draw distance increased
        terrain.heightmapPixelError = 5;
        terrain.drawInstanced       = true;
        terrain.basemapDistance     = 1500f;
        terrain.detailObjectDistance = 200f;
        terrain.treeDistance        = 2000f;
        terrain.treeBillboardDistance = 800f;
        EditorUtility.SetDirty(terrain);
        Debug.Log("[TerrainMatch] Updated terrain rendering settings");
        steps++;

        // ── 4. Replace/update MorningSun lighting ─────────────────────
        var existingSun = GameObject.Find("MorningSun");
        if (existingSun == null)
        {
            var morningSunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TerrainDemoScene_URP/Prefabs/Lighting/MorningSun.prefab");
            if (morningSunPrefab != null)
            {
                var sun = PrefabUtility.InstantiatePrefab(morningSunPrefab) as GameObject;
                sun.name = "MorningSun";
                Debug.Log("[TerrainMatch] Added MorningSun lighting prefab");
                steps++;
            }
            else Debug.LogWarning("[TerrainMatch] MorningSun.prefab not found");
        }
        else
        {
            Debug.Log("[TerrainMatch] MorningSun already in scene — skipping");
        }

        // ── 5. GlobalVolume post-process ──────────────────────────────
        var existingVol = GameObject.Find("GlobalVolume");
        if (existingVol == null)
        {
            var volPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TerrainDemoScene_URP/Prefabs/Volumes/GlobalVolume.prefab");
            if (volPrefab != null)
            {
                var vol = PrefabUtility.InstantiatePrefab(volPrefab) as GameObject;
                vol.name = "GlobalVolume";
                Debug.Log("[TerrainMatch] Added GlobalVolume post-process");
                steps++;
            }
            else Debug.LogWarning("[TerrainMatch] GlobalVolume.prefab not found");
        }
        else
        {
            Debug.Log("[TerrainMatch] GlobalVolume already in scene — skipping");
        }

        // ── 6. SkyboxMountains prefab ──────────────────────────────────
        var existingSkyMtns = GameObject.Find("SkyboxMountains");
        if (existingSkyMtns == null)
        {
            var mtPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TerrainDemoScene_URP/Prefabs/Skybox/SkyboxMountains.prefab");
            if (mtPrefab != null)
            {
                var mt = PrefabUtility.InstantiatePrefab(mtPrefab) as GameObject;
                mt.name = "SkyboxMountains";
                // Position at terrain centre, y=0
                var t = Object.FindFirstObjectByType<Terrain>();
                if (t != null)
                    mt.transform.position = new Vector3(
                        t.transform.position.x + t.terrainData.size.x * 0.5f,
                        0f,
                        t.transform.position.z + t.terrainData.size.z * 0.5f);
                Debug.Log("[TerrainMatch] Added SkyboxMountains prefab");
                steps++;
            }
            else Debug.LogWarning("[TerrainMatch] SkyboxMountains.prefab not found");
        }
        else
        {
            Debug.Log("[TerrainMatch] SkyboxMountains already in scene");
        }

        // ── 7. Fog — match the hazy mountain atmosphere ────────────────
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.ExponentialSquared;
        RenderSettings.fogDensity   = 0.0008f;
        RenderSettings.fogColor     = new Color(0.65f, 0.72f, 0.78f); // cool blue-grey haze

        // Ambient light — warm morning tone matching the skybox sun
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.52f, 0.60f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.48f, 0.42f);
        RenderSettings.ambientGroundColor  = new Color(0.18f, 0.16f, 0.12f);

        Debug.Log("[TerrainMatch] Applied fog + ambient lighting");
        steps++;

        // ── 8. Save ────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[TerrainMatch] Done — {steps} steps applied. Save with Ctrl+S.");
        EditorUtility.DisplayDialog("Match Terrain To Skybox",
            $"Done! {steps} improvements applied.\n\nSave the scene with Ctrl+S.", "OK");
    }
}
