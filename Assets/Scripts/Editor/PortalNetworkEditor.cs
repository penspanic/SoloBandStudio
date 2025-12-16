using UnityEngine;
using UnityEditor;
using SoloBandStudio.XR;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor utilities for setting up portal networks.
    /// </summary>
    public static class PortalNetworkEditor
    {
        [MenuItem("SoloBandStudio/Create Portal Network", false, 200)]
        public static void CreatePortalNetwork()
        {
            // Check if one already exists
            var existing = Object.FindFirstObjectByType<PortalNetwork>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Portal Network",
                    "A PortalNetwork already exists in the scene.\n\n" +
                    "Select it in the hierarchy to configure.", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new PortalNetwork
            GameObject networkObj = new GameObject("PortalNetwork");
            Undo.RegisterCreatedObjectUndo(networkObj, "Create Portal Network");

            var network = networkObj.AddComponent<PortalNetwork>();

            Selection.activeGameObject = networkObj;

            EditorUtility.DisplayDialog("Portal Network Created",
                "PortalNetwork has been created.\n\n" +
                "Now create portals using:\n" +
                "SoloBandStudio > Create Teleport Portal\n\n" +
                "The TeleportationProvider will be auto-detected from XR Origin.", "OK");
        }

        [MenuItem("SoloBandStudio/Create Teleport Portal", false, 201)]
        public static void CreateTeleportPortal()
        {
            // Ensure PortalNetwork exists
            var network = Object.FindFirstObjectByType<PortalNetwork>();
            if (network == null)
            {
                if (EditorUtility.DisplayDialog("No Portal Network",
                    "No PortalNetwork found in scene.\n\nCreate one first?", "Create", "Cancel"))
                {
                    CreatePortalNetwork();
                    network = Object.FindFirstObjectByType<PortalNetwork>();
                }
                else
                {
                    return;
                }
            }

            // Create portal
            GameObject portalObj = new GameObject("TeleportPortal");
            Undo.RegisterCreatedObjectUndo(portalObj, "Create Teleport Portal");

            // Position at scene view camera or origin
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                portalObj.transform.position = sceneView.camera.transform.position +
                                               sceneView.camera.transform.forward * 3f;
                portalObj.transform.position = new Vector3(
                    portalObj.transform.position.x,
                    0f, // Ground level
                    portalObj.transform.position.z
                );
            }

            // Add components
            var portal = portalObj.AddComponent<TeleportPortal>();

            // Add collider
            var collider = portalObj.AddComponent<CapsuleCollider>();
            collider.radius = 1f;
            collider.height = 3f;
            collider.center = new Vector3(0, 1.5f, 0);
            collider.isTrigger = true;

            // Create visual placeholder
            CreatePortalVisual(portalObj);

            Selection.activeGameObject = portalObj;

            // Count existing portals
            var allPortals = Object.FindObjectsByType<TeleportPortal>(FindObjectsSortMode.None);
            portalObj.name = $"TeleportPortal_{allPortals.Length}";

            Debug.Log($"[PortalNetworkEditor] Created portal: {portalObj.name}");
        }

        private static void CreatePortalVisual(GameObject parent)
        {
            // Create a simple cylinder as placeholder
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "PortalVisual";
            visual.transform.SetParent(parent.transform);
            visual.transform.localPosition = new Vector3(0, 1.5f, 0);
            visual.transform.localScale = new Vector3(2f, 0.05f, 2f);

            // Remove collider from visual
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            // Create material
            var renderer = visual.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0f, 1f, 1f, 0.5f);
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_AlphaClip", 0);
            mat.renderQueue = 3000;
            renderer.sharedMaterial = mat;

            // Create ring effect (flat cylinder as ring)
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PortalRing";
            ring.transform.SetParent(parent.transform);
            ring.transform.localPosition = new Vector3(0, 0.05f, 0);
            ring.transform.localScale = new Vector3(2.2f, 0.05f, 2.2f);

            Object.DestroyImmediate(ring.GetComponent<Collider>());

            var ringRenderer = ring.GetComponent<MeshRenderer>();
            var ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ringMat.color = new Color(0f, 0.8f, 1f, 1f);
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", new Color(0f, 0.5f, 1f, 1f) * 2f);
            ringRenderer.sharedMaterial = ringMat;
        }

        [MenuItem("SoloBandStudio/Connect Selected Portals", false, 202)]
        public static void ConnectSelectedPortals()
        {
            var selected = Selection.gameObjects;
            var portals = new System.Collections.Generic.List<TeleportPortal>();

            foreach (var obj in selected)
            {
                var portal = obj.GetComponent<TeleportPortal>();
                if (portal != null)
                {
                    portals.Add(portal);
                }
            }

            if (portals.Count < 2)
            {
                EditorUtility.DisplayDialog("Connect Portals",
                    "Select at least 2 portal GameObjects to connect them.", "OK");
                return;
            }

            // Connect in a chain: A -> B -> C -> A
            Undo.RecordObjects(portals.ToArray(), "Connect Portals");

            for (int i = 0; i < portals.Count; i++)
            {
                var current = portals[i];
                var next = portals[(i + 1) % portals.Count];

                SerializedObject so = new SerializedObject(current);
                so.FindProperty("destinationMode").enumValueIndex = 0; // Specific
                so.FindProperty("specificDestination").objectReferenceValue = next;
                so.ApplyModifiedProperties();
            }

            EditorUtility.DisplayDialog("Portals Connected",
                $"Connected {portals.Count} portals in a loop.\n\n" +
                string.Join(" -> ", portals.ConvertAll(p => p.gameObject.name)) +
                $" -> {portals[0].gameObject.name}", "OK");
        }

        [MenuItem("SoloBandStudio/Set All Portals to Random", false, 203)]
        public static void SetAllPortalsToRandom()
        {
            var portals = Object.FindObjectsByType<TeleportPortal>(FindObjectsSortMode.None);
            if (portals.Length == 0)
            {
                EditorUtility.DisplayDialog("No Portals", "No TeleportPortal found in scene.", "OK");
                return;
            }

            Undo.RecordObjects(portals, "Set Portals to Random");

            foreach (var portal in portals)
            {
                SerializedObject so = new SerializedObject(portal);
                so.FindProperty("destinationMode").enumValueIndex = 1; // Random
                so.ApplyModifiedProperties();
            }

            EditorUtility.DisplayDialog("Portals Updated",
                $"Set {portals.Length} portals to Random destination mode.", "OK");
        }
    }
}
