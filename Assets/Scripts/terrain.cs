using UnityEngine;

public class TerrainScan : MonoBehaviour
{
    void Start()
    {
        Terrain terrain = Terrain.activeTerrain;
        TerrainData td = terrain.terrainData;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        Vector3 minPos = Vector3.zero;
        Vector3 maxPos = Vector3.zero;

        int res = td.heightmapResolution;

        float[,] heights = td.GetHeights(0, 0, res, res);

        for (int x = 0; x < res; x++)
        {
            for (int z = 0; z < res; z++)
            {
                float h = heights[z, x] * td.size.y;

                if (h < minHeight)
                {
                    minHeight = h;

                    minPos = new Vector3(
                        (float)x / (res - 1) * td.size.x,
                        h,
                        (float)z / (res - 1) * td.size.z
                    );
                }

                if (h > maxHeight)
                {
                    maxHeight = h;

                    maxPos = new Vector3(
                        (float)x / (res - 1) * td.size.x,
                        h,
                        (float)z / (res - 1) * td.size.z
                    );
                }
            }
        }

        Debug.Log($"MIN: {minHeight:F2} at {minPos}");
        Debug.Log($"MAX: {maxHeight:F2} at {maxPos}");
    }
}