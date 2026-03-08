// Assets/Editor/ClearDetailsUnderMesh.cs
using UnityEngine;
using UnityEditor;

public class ClearDetailsUnderMesh : EditorWindow
{
    [MenuItem("Tools/Clear Details Under Mesh")]
    public static void ShowWindow()
    {
        GetWindow<ClearDetailsUnderMesh>("Clear Details Under Mesh");
    }

    private Terrain terrain;
    private GameObject roadMesh;
    private float raycastHeightOffset = 10f;  // How high above mesh to start rays
    private float padding = 0.5f;             // Extra padding around road edges

    void OnGUI()
    {
        GUILayout.Label("Clear Terrain Details Under Mesh", EditorStyles.boldLabel);
        terrain  = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        roadMesh = EditorGUILayout.ObjectField("Road Mesh", roadMesh, typeof(GameObject), true) as GameObject;
        raycastHeightOffset = EditorGUILayout.FloatField("Raycast Height Offset", raycastHeightOffset);
        padding = EditorGUILayout.FloatField("Edge Padding (world units)", padding);

        EditorGUILayout.HelpBox(
            "Raycasts downward from each terrain detail cell. If the ray hits the road mesh, details are cleared.",
            MessageType.Info);

        if (GUILayout.Button("Clear Details Under Road"))
        {
            if (terrain != null && roadMesh != null) ClearDetails();
            else Debug.LogWarning("Assign both a Terrain and a Road Mesh!");
        }
    }

    void ClearDetails()
    {
        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        int detailW = td.detailWidth;
        int detailH = td.detailHeight;

        // Collect all colliders on the road mesh (including children)
        Collider[] roadColliders = roadMesh.GetComponentsInChildren<Collider>();
        if (roadColliders.Length == 0)
        {
            Debug.LogWarning("Road mesh has no Colliders! Add a MeshCollider to your road object.");
            return;
        }

        // Build a temporary layer mask — we only want to hit the road
        // Temporarily move road to its own layer if needed, or just check by collider
        int roadLayer = roadMesh.layer;

        // Load all detail maps
        int[][,] maps = new int[td.detailPrototypes.Length][,];
        for (int i = 0; i < td.detailPrototypes.Length; i++)
            maps[i] = td.GetDetailLayer(0, 0, detailW, detailH, i);

        int cleared = 0;

        for (int dz = 0; dz < detailH; dz++)
        {
            for (int dx = 0; dx < detailW; dx++)
            {
                // Convert detail grid coords to world position
                float normX = (float)dx / detailW;
                float normZ = (float)dz / detailH;

                Vector3 worldPos = terrainPos + new Vector3(
                    normX * td.size.x,
                    raycastHeightOffset + terrainPos.y + td.size.y,
                    normZ * td.size.z
                );

                // Cast ray straight down
                Ray ray = new Ray(worldPos, Vector3.down);
                bool hitRoad = false;

                RaycastHit[] hits = Physics.RaycastAll(ray, td.size.y + raycastHeightOffset + 50f);
                foreach (var hit in hits)
                {
                    // Check if we hit any collider belonging to the road mesh
                    foreach (var col in roadColliders)
                    {
                        if (hit.collider == col)
                        {
                            hitRoad = true;
                            break;
                        }
                    }
                    if (hitRoad) break;
                }

                // Also check with padding by doing extra rays in a small cross pattern
                if (!hitRoad && padding > 0f)
                {
                    Vector3[] offsets = {
                        new Vector3( padding, 0, 0),
                        new Vector3(-padding, 0, 0),
                        new Vector3(0, 0,  padding),
                        new Vector3(0, 0, -padding),
                    };
                    foreach (var offset in offsets)
                    {
                        Ray paddedRay = new Ray(worldPos + offset, Vector3.down);
                        RaycastHit[] paddedHits = Physics.RaycastAll(paddedRay, td.size.y + raycastHeightOffset + 50f);
                        foreach (var hit in paddedHits)
                        {
                            foreach (var col in roadColliders)
                            {
                                if (hit.collider == col) { hitRoad = true; break; }
                            }
                            if (hitRoad) break;
                        }
                        if (hitRoad) break;
                    }
                }

                if (hitRoad)
                {
                    for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
                        maps[layer][dz, dx] = 0;
                    cleared++;
                }
            }
        }

        // Write all detail layers back
        for (int i = 0; i < td.detailPrototypes.Length; i++)
            td.SetDetailLayer(0, 0, i, maps[i]);

        Debug.Log($"Done! Cleared details from {cleared} detail cells.");
    }
}