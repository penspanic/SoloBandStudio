using UnityEngine;
using UnityEditor;
using SoloBandStudio.XR;
using SoloBandStudio.Instruments.Drum;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor utility to quickly set up a drumstick prefab with all required components.
    /// </summary>
    public static class DrumstickSetup
    {
        [MenuItem("SoloBandStudio/Setup Drumstick (Standard)", false, 100)]
        public static void SetupSelectedDrumstick()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Drumstick Setup",
                    "Please select a drumstick GameObject in the hierarchy or project.", "OK");
                return;
            }

            SetupDrumstick(selected);
            EditorUtility.DisplayDialog("Drumstick Setup",
                $"Drumstick '{selected.name}' has been set up successfully!\n\n" +
                "Components added:\n" +
                "- Rigidbody (kinematic, no gravity)\n" +
                "- CapsuleCollider (on root)\n" +
                "- ReturnableGrabbable\n" +
                "- DrumstickTip child object with trigger collider\n\n" +
                "You may need to adjust the tip position and collider size.", "OK");
        }

        [MenuItem("SoloBandStudio/Setup Drumstick (Sticky)", false, 101)]
        public static void SetupSelectedStickyDrumstick()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Sticky Drumstick Setup",
                    "Please select a drumstick GameObject in the hierarchy or project.", "OK");
                return;
            }

            SetupStickyDrumstick(selected);
            EditorUtility.DisplayDialog("Sticky Drumstick Setup",
                $"Sticky Drumstick '{selected.name}' has been set up successfully!\n\n" +
                "Components added:\n" +
                "- Rigidbody (kinematic, no gravity)\n" +
                "- CapsuleCollider (on root)\n" +
                "- StickyGrabbable (toggle grab)\n" +
                "- DrumstickTip child object with trigger collider\n\n" +
                "Grab once to attach, grab again to release!", "OK");
        }

        [MenuItem("SoloBandStudio/Convert to Sticky Grab", false, 102)]
        public static void ConvertToStickyGrab()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Convert to Sticky",
                    "Please select a drumstick GameObject with ReturnableGrabbable.", "OK");
                return;
            }

            ReturnableGrabbable oldGrab = selected.GetComponent<ReturnableGrabbable>();
            if (oldGrab == null)
            {
                EditorUtility.DisplayDialog("Convert to Sticky",
                    "Selected object doesn't have ReturnableGrabbable component.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(selected, "Convert to Sticky Grab");
            Undo.DestroyObjectImmediate(oldGrab);

            StickyGrabbable sticky = Undo.AddComponent<StickyGrabbable>(selected);
            sticky.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            sticky.throwOnDetach = false;

            EditorUtility.SetDirty(selected);
            EditorUtility.DisplayDialog("Convert to Sticky",
                $"Converted '{selected.name}' to StickyGrabbable!\n\n" +
                "Grab once to attach, grab again to release.", "OK");
        }

        [MenuItem("SoloBandStudio/Setup Drumstick (Standard)", true)]
        [MenuItem("SoloBandStudio/Setup Drumstick (Sticky)", true)]
        [MenuItem("SoloBandStudio/Convert to Sticky Grab", true)]
        public static bool SetupSelectedDrumstickValidate()
        {
            return Selection.activeGameObject != null;
        }

        public static void SetupStickyDrumstick(GameObject drumstick)
        {
            Undo.RegisterCompleteObjectUndo(drumstick, "Setup Sticky Drumstick");

            // 1. Add Rigidbody
            Rigidbody rb = drumstick.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(drumstick);
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 2. Add main collider for grabbing
            CapsuleCollider mainCol = drumstick.GetComponent<CapsuleCollider>();
            if (mainCol == null)
            {
                mainCol = Undo.AddComponent<CapsuleCollider>(drumstick);
            }
            mainCol.direction = 2;
            mainCol.height = 0.4f;
            mainCol.radius = 0.015f;
            mainCol.center = new Vector3(0, 0, 0.15f);
            mainCol.isTrigger = false;

            // 3. Remove old grabbable if exists, add StickyGrabbable
            ReturnableGrabbable oldGrab = drumstick.GetComponent<ReturnableGrabbable>();
            if (oldGrab != null)
            {
                Undo.DestroyObjectImmediate(oldGrab);
            }

            StickyGrabbable grabbable = drumstick.GetComponent<StickyGrabbable>();
            if (grabbable == null)
            {
                grabbable = Undo.AddComponent<StickyGrabbable>(drumstick);
            }
            grabbable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            grabbable.throwOnDetach = false;

            // 4. Create tip child object
            Transform existingTip = drumstick.transform.Find("DrumstickTip");
            GameObject tipObj;
            if (existingTip != null)
            {
                tipObj = existingTip.gameObject;
            }
            else
            {
                tipObj = new GameObject("DrumstickTip");
                Undo.RegisterCreatedObjectUndo(tipObj, "Create Drumstick Tip");
                tipObj.transform.SetParent(drumstick.transform);
            }

            tipObj.transform.localPosition = new Vector3(0, 0, 0.35f);
            tipObj.transform.localRotation = Quaternion.identity;
            tipObj.transform.localScale = Vector3.one;

            SphereCollider tipCol = tipObj.GetComponent<SphereCollider>();
            if (tipCol == null)
            {
                tipCol = Undo.AddComponent<SphereCollider>(tipObj);
            }
            tipCol.radius = 0.02f;
            tipCol.isTrigger = true;

            DrumstickTip tipScript = tipObj.GetComponent<DrumstickTip>();
            if (tipScript == null)
            {
                tipScript = Undo.AddComponent<DrumstickTip>(tipObj);
            }

            EditorUtility.SetDirty(drumstick);
            if (PrefabUtility.IsPartOfPrefabInstance(drumstick))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(drumstick);
            }

            Debug.Log($"[DrumstickSetup] Successfully set up sticky drumstick: {drumstick.name}");
        }

        public static void SetupDrumstick(GameObject drumstick)
        {
            Undo.RegisterCompleteObjectUndo(drumstick, "Setup Drumstick");

            // 1. Add Rigidbody
            Rigidbody rb = drumstick.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(drumstick);
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 2. Add main collider for grabbing (capsule along the stick)
            CapsuleCollider mainCol = drumstick.GetComponent<CapsuleCollider>();
            if (mainCol == null)
            {
                mainCol = Undo.AddComponent<CapsuleCollider>(drumstick);
            }
            // Default capsule settings - user should adjust
            mainCol.direction = 2; // Z-axis (along stick length)
            mainCol.height = 0.4f;
            mainCol.radius = 0.015f;
            mainCol.center = new Vector3(0, 0, 0.15f);
            mainCol.isTrigger = false;

            // 3. Add ReturnableGrabbable
            ReturnableGrabbable grabbable = drumstick.GetComponent<ReturnableGrabbable>();
            if (grabbable == null)
            {
                grabbable = Undo.AddComponent<ReturnableGrabbable>(drumstick);
            }

            // Configure XRGrabInteractable settings
            grabbable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
            grabbable.throwOnDetach = false;

            // 4. Create tip child object with trigger collider
            Transform existingTip = drumstick.transform.Find("DrumstickTip");
            GameObject tipObj;
            if (existingTip != null)
            {
                tipObj = existingTip.gameObject;
            }
            else
            {
                tipObj = new GameObject("DrumstickTip");
                Undo.RegisterCreatedObjectUndo(tipObj, "Create Drumstick Tip");
                tipObj.transform.SetParent(drumstick.transform);
            }

            // Position at the tip of the stick
            tipObj.transform.localPosition = new Vector3(0, 0, 0.35f);
            tipObj.transform.localRotation = Quaternion.identity;
            tipObj.transform.localScale = Vector3.one;

            // Add sphere collider as trigger
            SphereCollider tipCol = tipObj.GetComponent<SphereCollider>();
            if (tipCol == null)
            {
                tipCol = Undo.AddComponent<SphereCollider>(tipObj);
            }
            tipCol.radius = 0.02f;
            tipCol.isTrigger = true;

            // Add DrumstickTip component
            DrumstickTip tipScript = tipObj.GetComponent<DrumstickTip>();
            if (tipScript == null)
            {
                tipScript = Undo.AddComponent<DrumstickTip>(tipObj);
            }

            // Mark prefab as dirty
            EditorUtility.SetDirty(drumstick);
            if (PrefabUtility.IsPartOfPrefabInstance(drumstick))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(drumstick);
            }

            Debug.Log($"[DrumstickSetup] Successfully set up drumstick: {drumstick.name}");
        }
    }
}
