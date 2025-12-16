using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor tool to separate a mesh into individual islands using spatial clustering.
    /// Useful for separating combined piano key meshes into individual keys.
    /// </summary>
    public class MeshIslandSeparator : EditorWindow
    {
        private GameObject sourcePrefab;
        private string outputFolder = "Assets/ThirdParty/Touches Clavier piano/SeparatedKeys";
        private bool createPrefabs = true;
        private bool preserveMaterials = true;
        private float clusterThreshold = 0.005f; // 5mm gap threshold for piano keys
        private int maxIslandsPerMesh = 20; // Skip meshes with too many islands (likely fragmented geometry)

        [MenuItem("Tools/SoloBandStudio/Mesh Island Separator")]
        public static void ShowWindow()
        {
            GetWindow<MeshIslandSeparator>("Mesh Island Separator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Mesh Island Separator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool separates a mesh into individual parts using island detection.\n" +
                "Meshes with more than 'Max Islands Per Mesh' will be skipped.\n" +
                "For piano keys: Object_7 (7 white) and Object_8 (5 black) have proper islands.",
                MessageType.Info);

            GUILayout.Space(10);

            sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab/Object", sourcePrefab, typeof(GameObject), true);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            clusterThreshold = EditorGUILayout.FloatField("Cluster Threshold (m)", clusterThreshold);
            maxIslandsPerMesh = EditorGUILayout.IntField("Max Islands Per Mesh", maxIslandsPerMesh);
            createPrefabs = EditorGUILayout.Toggle("Create Prefabs", createPrefabs);
            preserveMaterials = EditorGUILayout.Toggle("Preserve Materials", preserveMaterials);

            GUILayout.Space(20);

            if (GUILayout.Button("Analyze Meshes"))
            {
                AnalyzeMeshes();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Separate All Meshes"))
            {
                SeparateAllMeshes();
            }
        }

        private void AnalyzeMeshes()
        {
            if (sourcePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a source prefab or GameObject.", "OK");
                return;
            }

            MeshFilter[] meshFilters = sourcePrefab.GetComponentsInChildren<MeshFilter>();

            Debug.Log($"=== Mesh Analysis for {sourcePrefab.name} ===");

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                Mesh mesh = mf.sharedMesh;
                var islands = FindMeshIslands(mesh);

                Debug.Log($"  {mf.gameObject.name}: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles, {islands.Count} islands");

                for (int i = 0; i < islands.Count; i++)
                {
                    var island = islands[i];
                    Debug.Log($"    Island {i}: {island.Count} triangles");
                }
            }
        }

        private void SeparateAllMeshes()
        {
            if (sourcePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a source prefab or GameObject.", "OK");
                return;
            }

            // Create output folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                CreateFolderRecursive(outputFolder);
            }

            MeshFilter[] meshFilters = sourcePrefab.GetComponentsInChildren<MeshFilter>();
            MeshRenderer[] meshRenderers = sourcePrefab.GetComponentsInChildren<MeshRenderer>();

            var rendererLookup = new Dictionary<MeshFilter, MeshRenderer>();
            foreach (var mf in meshFilters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) rendererLookup[mf] = mr;
            }

            int totalSeparated = 0;
            var allSeparatedObjects = new List<GameObject>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                Mesh originalMesh = mf.sharedMesh;
                Material material = null;

                if (preserveMaterials && rendererLookup.TryGetValue(mf, out var mr))
                {
                    material = mr.sharedMaterial;
                }

                var islands = FindMeshIslands(originalMesh);

                if (islands.Count <= 1)
                {
                    Debug.Log($"Skipping {mf.gameObject.name}: only {islands.Count} island(s)");
                    continue;
                }

                if (islands.Count > maxIslandsPerMesh)
                {
                    Debug.Log($"Skipping {mf.gameObject.name}: too many islands ({islands.Count} > {maxIslandsPerMesh}), likely fragmented geometry");
                    continue;
                }

                Debug.Log($"Separating {mf.gameObject.name} into {islands.Count} islands...");

                for (int i = 0; i < islands.Count; i++)
                {
                    var island = islands[i];
                    Mesh separatedMesh = ExtractIslandMesh(originalMesh, island);

                    if (separatedMesh == null || separatedMesh.vertexCount == 0) continue;

                    // Calculate center and recenter mesh
                    Vector3 center = CalculateMeshCenter(separatedMesh);
                    RecenterMesh(separatedMesh, center);

                    // Save mesh asset
                    string meshName = $"{mf.gameObject.name}_Key_{i:D2}";
                    string meshPath = $"{outputFolder}/{meshName}.asset";

                    AssetDatabase.CreateAsset(separatedMesh, meshPath);

                    if (createPrefabs)
                    {
                        // Create GameObject
                        GameObject keyObj = new GameObject(meshName);
                        MeshFilter newMf = keyObj.AddComponent<MeshFilter>();
                        MeshRenderer newMr = keyObj.AddComponent<MeshRenderer>();

                        newMf.sharedMesh = separatedMesh;
                        if (material != null) newMr.sharedMaterial = material;

                        // Position at original center
                        keyObj.transform.position = center;

                        allSeparatedObjects.Add(keyObj);
                    }

                    totalSeparated++;
                }
            }

            if (createPrefabs && allSeparatedObjects.Count > 0)
            {
                // Sort by X position (left to right for piano keys)
                allSeparatedObjects.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

                // Create parent container
                GameObject container = new GameObject("SeparatedPianoKeys");

                for (int i = 0; i < allSeparatedObjects.Count; i++)
                {
                    var obj = allSeparatedObjects[i];
                    obj.transform.SetParent(container.transform);
                    obj.name = $"Key_{i:D2}";
                }

                // Save as prefab
                string prefabPath = $"{outputFolder}/SeparatedPianoKeys.prefab";
                PrefabUtility.SaveAsPrefabAsset(container, prefabPath);

                // Clean up scene object
                DestroyImmediate(container);

                Debug.Log($"Created prefab at: {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete", $"Separated {totalSeparated} islands from meshes.", "OK");
        }

        private List<HashSet<int>> FindMeshIslands(Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            int triangleCount = triangles.Length / 3;

            // Union-Find data structure
            int[] parent = new int[triangleCount];
            int[] rank = new int[triangleCount];

            for (int i = 0; i < triangleCount; i++)
            {
                parent[i] = i;
                rank[i] = 0;
            }

            // Build vertex to triangle mapping
            var vertexToTriangles = new Dictionary<int, List<int>>();
            for (int t = 0; t < triangleCount; t++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int vertIndex = triangles[t * 3 + j];
                    if (!vertexToTriangles.ContainsKey(vertIndex))
                    {
                        vertexToTriangles[vertIndex] = new List<int>();
                    }
                    vertexToTriangles[vertIndex].Add(t);
                }
            }

            // Union triangles that share vertices
            foreach (var kvp in vertexToTriangles)
            {
                var tris = kvp.Value;
                for (int i = 1; i < tris.Count; i++)
                {
                    Union(parent, rank, tris[0], tris[i]);
                }
            }

            // Group triangles by their root
            var islands = new Dictionary<int, HashSet<int>>();
            for (int t = 0; t < triangleCount; t++)
            {
                int root = Find(parent, t);
                if (!islands.ContainsKey(root))
                {
                    islands[root] = new HashSet<int>();
                }
                islands[root].Add(t);
            }

            return islands.Values.ToList();
        }

        private int Find(int[] parent, int i)
        {
            if (parent[i] != i)
            {
                parent[i] = Find(parent, parent[i]); // Path compression
            }
            return parent[i];
        }

        private void Union(int[] parent, int[] rank, int x, int y)
        {
            int rootX = Find(parent, x);
            int rootY = Find(parent, y);

            if (rootX != rootY)
            {
                if (rank[rootX] < rank[rootY])
                {
                    parent[rootX] = rootY;
                }
                else if (rank[rootX] > rank[rootY])
                {
                    parent[rootY] = rootX;
                }
                else
                {
                    parent[rootY] = rootX;
                    rank[rootX]++;
                }
            }
        }

        private Mesh ExtractIslandMesh(Mesh originalMesh, HashSet<int> triangleIndices)
        {
            Vector3[] origVertices = originalMesh.vertices;
            Vector3[] origNormals = originalMesh.normals;
            Vector2[] origUVs = originalMesh.uv;
            int[] origTriangles = originalMesh.triangles;

            // Map old vertex indices to new ones
            var oldToNewVertex = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUVs = new List<Vector2>();
            var newTriangles = new List<int>();

            foreach (int triIndex in triangleIndices)
            {
                for (int j = 0; j < 3; j++)
                {
                    int oldVertIndex = origTriangles[triIndex * 3 + j];

                    if (!oldToNewVertex.ContainsKey(oldVertIndex))
                    {
                        oldToNewVertex[oldVertIndex] = newVertices.Count;
                        newVertices.Add(origVertices[oldVertIndex]);

                        if (origNormals != null && origNormals.Length > oldVertIndex)
                            newNormals.Add(origNormals[oldVertIndex]);

                        if (origUVs != null && origUVs.Length > oldVertIndex)
                            newUVs.Add(origUVs[oldVertIndex]);
                    }

                    newTriangles.Add(oldToNewVertex[oldVertIndex]);
                }
            }

            Mesh newMesh = new Mesh();
            newMesh.vertices = newVertices.ToArray();
            newMesh.triangles = newTriangles.ToArray();

            if (newNormals.Count == newVertices.Count)
                newMesh.normals = newNormals.ToArray();
            else
                newMesh.RecalculateNormals();

            if (newUVs.Count == newVertices.Count)
                newMesh.uv = newUVs.ToArray();

            newMesh.RecalculateBounds();

            return newMesh;
        }

        private Vector3 CalculateMeshCenter(Mesh mesh)
        {
            if (mesh.vertexCount == 0) return Vector3.zero;

            Bounds bounds = mesh.bounds;
            return bounds.center;
        }

        private void RecenterMesh(Mesh mesh, Vector3 center)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] -= center;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private void CreateFolderRecursive(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
