using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SoloBandStudio.Instruments.Drum;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor utility to set up the standard Drum prefab.
    /// Adds DrumPad components to DrumPart_* objects and connects them to DrumKit.
    /// </summary>
    public static class DrumSetup
    {
        // Mapping of object name suffix to DrumPartType
        // Note: TomLow -> TomMid (as requested, since we don't have TomLow sounds)
        private static readonly Dictionary<string, DrumPartType> PartMappings = new Dictionary<string, DrumPartType>
        {
            { "Kick", DrumPartType.Kick },
            { "Snare", DrumPartType.Snare },
            { "HiHat", DrumPartType.HiHatClosed },
            { "TomHigh", DrumPartType.TomHigh },
            { "TomMid", DrumPartType.TomMid },
            { "TomLow", DrumPartType.TomMid },  // Map TomLow to TomMid
            { "Crash", DrumPartType.Crash },
            { "Ride", DrumPartType.Ride },
        };

        [MenuItem("SoloBandStudio/Setup Standard Drum", false, 100)]
        public static void SetupSelectedDrum()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Drum Setup",
                    "Please select the Drum prefab root in the hierarchy.", "OK");
                return;
            }

            int padsFound = SetupDrum(selected);

            if (padsFound > 0)
            {
                EditorUtility.DisplayDialog("Drum Setup",
                    $"Drum '{selected.name}' setup complete!\n\n" +
                    $"• {padsFound} pads configured\n" +
                    $"• DrumPad components added\n" +
                    $"• DrumKit.drumPads list populated\n\n" +
                    "Don't forget to assign a DrumSoundBank in the Drum component!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Drum Setup",
                    "No pads found! Make sure you selected the correct prefab.\n\n" +
                    "Expected objects named 'DrumPart_*' in children.", "OK");
            }
        }

        [MenuItem("SoloBandStudio/Setup Standard Drum", true)]
        public static bool SetupSelectedDrumValidate()
        {
            return Selection.activeGameObject != null;
        }

        public static int SetupDrum(GameObject root)
        {
            Undo.RegisterCompleteObjectUndo(root, "Setup Standard Drum");

            List<DrumPad> createdPads = new List<DrumPad>();

            // Find all DrumPart_* objects
            var allTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                if (!t.name.StartsWith("DrumPart_")) continue;

                string partName = t.name.Substring("DrumPart_".Length);

                if (!PartMappings.TryGetValue(partName, out DrumPartType partType))
                {
                    Debug.LogWarning($"[DrumSetup] Unknown drum part: {partName}");
                    continue;
                }

                GameObject padObj = t.gameObject;

                // Add DrumPad component if not present
                DrumPad drumPad = padObj.GetComponent<DrumPad>();
                if (drumPad == null)
                {
                    drumPad = Undo.AddComponent<DrumPad>(padObj);
                }

                // Set partType via SerializedObject
                SerializedObject padSO = new SerializedObject(drumPad);
                SerializedProperty partTypeProp = padSO.FindProperty("partType");
                partTypeProp.enumValueIndex = GetEnumIndex(partType);
                padSO.ApplyModifiedProperties();

                createdPads.Add(drumPad);
                Debug.Log($"[DrumSetup] Added DrumPad to '{padObj.name}' -> {partType}");
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
                Debug.Log("[DrumSetup] Added Drum component");
            }

            // Add DrumKit component if not present
            DrumKit drumKit = root.GetComponent<DrumKit>();
            if (drumKit == null)
            {
                drumKit = Undo.AddComponent<DrumKit>(root);
                Debug.Log("[DrumSetup] Added DrumKit component");
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

            Debug.Log($"[DrumSetup] Setup complete! {createdPads.Count} pads configured.");
            return createdPads.Count;
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

        [MenuItem("SoloBandStudio/Show Drum Pad Info", false, 101)]
        public static void ShowPadInfo()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Pad Info",
                    "Please select a Drum prefab.", "OK");
                return;
            }

            string info = "Drum Pad Configuration Status:\n\n";
            var allTransforms = selected.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                if (!t.name.StartsWith("DrumPart_")) continue;

                string partName = t.name.Substring("DrumPart_".Length);
                DrumPad pad = t.GetComponent<DrumPad>();

                string status;
                if (pad != null)
                {
                    status = $"✓ Ready (Type: {pad.PartType})";
                }
                else
                {
                    status = "○ Needs setup";
                }

                string mappedTo = "";
                if (PartMappings.TryGetValue(partName, out DrumPartType targetType))
                {
                    if (partName == "TomLow")
                    {
                        mappedTo = $" → {targetType} (remapped)";
                    }
                    else
                    {
                        mappedTo = $" → {targetType}";
                    }
                }

                info += $"{t.name}{mappedTo}\n   {status}\n\n";
            }

            EditorUtility.DisplayDialog("Drum Pad Info", info, "OK");
        }
    }
}
