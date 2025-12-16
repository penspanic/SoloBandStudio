using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SoloBandStudio.Instruments.Drum;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor utility to set up the Alesis SamplePad Pro prefab as a drum instrument.
    /// Adds DrumPad components to the actual pad mesh objects and connects them to DrumKit.
    /// </summary>
    public static class SamplePadSetup
    {
        // Mapping of parent pad names to DrumPartType
        // The actual DrumPad will be added to the FIRST CHILD (the pad surface mesh)
        private static readonly (string parentName, DrumPartType partType)[] PadMappings = new[]
        {
            ("pad 01_3", DrumPartType.Snare),
            ("pad 02_2", DrumPartType.TomHigh),
            ("pad 03_1", DrumPartType.TomMid),
            ("pad 04_6", DrumPartType.Kick),
            ("pad 05_5", DrumPartType.HiHatClosed),
            ("pad 06_4", DrumPartType.Crash),
            ("Pad 07_7", DrumPartType.Ride),
        };

        [MenuItem("SoloBandStudio/Setup SamplePad Drum", false, 110)]
        public static void SetupSelectedSamplePad()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("SamplePad Setup",
                    "Please select the SamplePad prefab root in the hierarchy.", "OK");
                return;
            }

            int padsFound = SetupSamplePad(selected);

            if (padsFound > 0)
            {
                EditorUtility.DisplayDialog("SamplePad Setup",
                    $"SamplePad '{selected.name}' setup complete!\n\n" +
                    $"• {padsFound} pads configured\n" +
                    $"• DrumPad components added to pad meshes\n" +
                    $"• DrumKit.drumPads list populated\n\n" +
                    "Don't forget to assign a DrumSoundBank in the Drum component!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("SamplePad Setup",
                    "No pads found! Make sure you selected the correct prefab.\n\n" +
                    "Expected pad names: pad 01_3, pad 02_2, etc.", "OK");
            }
        }

        [MenuItem("SoloBandStudio/Setup SamplePad Drum", true)]
        public static bool SetupSelectedSamplePadValidate()
        {
            return Selection.activeGameObject != null;
        }

        public static int SetupSamplePad(GameObject root)
        {
            Undo.RegisterCompleteObjectUndo(root, "Setup SamplePad Drum");

            List<DrumPad> createdPads = new List<DrumPad>();

            // Find and setup all pads
            foreach (var mapping in PadMappings)
            {
                Transform parentTransform = FindChildRecursive(root.transform, mapping.parentName);
                if (parentTransform == null)
                {
                    Debug.LogWarning($"[SamplePadSetup] Parent pad not found: '{mapping.parentName}'");
                    continue;
                }

                // Get the first child (the actual pad mesh)
                if (parentTransform.childCount == 0)
                {
                    Debug.LogWarning($"[SamplePadSetup] Pad '{mapping.parentName}' has no children!");
                    continue;
                }

                Transform padMeshTransform = parentTransform.GetChild(0);
                GameObject padMeshObj = padMeshTransform.gameObject;

                // Add DrumPad component if not present
                DrumPad drumPad = padMeshObj.GetComponent<DrumPad>();
                if (drumPad == null)
                {
                    drumPad = Undo.AddComponent<DrumPad>(padMeshObj);
                }

                // Set partType via SerializedObject
                SerializedObject padSO = new SerializedObject(drumPad);
                SerializedProperty partTypeProp = padSO.FindProperty("partType");
                partTypeProp.enumValueIndex = GetEnumIndex(mapping.partType);
                padSO.ApplyModifiedProperties();

                createdPads.Add(drumPad);
                Debug.Log($"[SamplePadSetup] Added DrumPad to '{padMeshObj.name}' ({mapping.parentName}) -> {mapping.partType}");
            }

            if (createdPads.Count == 0)
            {
                return 0;
            }

            // Add Drum component if not present
            Drum drum = root.GetComponent<Drum>();
            if (drum == null)
            {
                drum = Undo.AddComponent<Drum>(root);
                Debug.Log("[SamplePadSetup] Added Drum component");
            }

            // Add DrumKit component if not present
            DrumKit drumKit = root.GetComponent<DrumKit>();
            if (drumKit == null)
            {
                drumKit = Undo.AddComponent<DrumKit>(root);
                Debug.Log("[SamplePadSetup] Added DrumKit component");
            }

            // Set up DrumKit with the pads list
            SerializedObject kitSO = new SerializedObject(drumKit);
            SerializedProperty padsProp = kitSO.FindProperty("drumPads");
            padsProp.ClearArray();
            for (int i = 0; i < createdPads.Count; i++)
            {
                padsProp.InsertArrayElementAtIndex(i);
                padsProp.GetArrayElementAtIndex(i).objectReferenceValue = createdPads[i];
            }
            kitSO.ApplyModifiedProperties();

            // Set the drumKit reference in Drum
            SerializedObject drumSO = new SerializedObject(drum);
            SerializedProperty drumKitProp = drumSO.FindProperty("drumKit");
            drumKitProp.objectReferenceValue = drumKit;

            // Set default instrument name
            SerializedProperty nameProp = drumSO.FindProperty("instrumentName");
            if (string.IsNullOrEmpty(nameProp.stringValue) || nameProp.stringValue == "Drum Kit")
            {
                nameProp.stringValue = "Sample Pad";
            }
            drumSO.ApplyModifiedProperties();

            // Mark as dirty
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(drum);
            EditorUtility.SetDirty(drumKit);
            foreach (var pad in createdPads)
            {
                EditorUtility.SetDirty(pad);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            }

            Debug.Log($"[SamplePadSetup] Setup complete! {createdPads.Count} pads configured.");
            return createdPads.Count;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static int GetEnumIndex(DrumPartType partType)
        {
            var values = System.Enum.GetValues(typeof(DrumPartType));
            for (int i = 0; i < values.Length; i++)
            {
                if ((DrumPartType)values.GetValue(i) == partType)
                    return i;
            }
            return 0;
        }

        [MenuItem("SoloBandStudio/Show SamplePad Pad Info", false, 111)]
        public static void ShowPadInfo()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Pad Info",
                    "Please select a SamplePad prefab.", "OK");
                return;
            }

            string info = "Pad Configuration Status:\n\n";
            foreach (var mapping in PadMappings)
            {
                Transform parent = FindChildRecursive(selected.transform, mapping.parentName);
                string status;

                if (parent == null)
                {
                    status = "✗ Parent not found";
                }
                else if (parent.childCount == 0)
                {
                    status = "✗ No child mesh";
                }
                else
                {
                    Transform child = parent.GetChild(0);
                    DrumPad pad = child.GetComponent<DrumPad>();
                    if (pad != null)
                    {
                        status = $"✓ Ready ({child.name})";
                    }
                    else
                    {
                        status = $"○ Needs setup ({child.name})";
                    }
                }

                info += $"{mapping.parentName} → {mapping.partType}\n   {status}\n\n";
            }

            EditorUtility.DisplayDialog("SamplePad Info", info, "OK");
        }
    }
}
