using UnityEngine;
using UnityEngine.UIElements;
using Unity.XR.CoreUtils;
using SoloBandStudio.Common.SceneManagement;

namespace SoloBandStudio.UI.QuickMenu
{
    /// <summary>
    /// Miscellaneous menu view for QuickMenu.
    /// Contains utility functions like return to spawn, recenter view, etc.
    /// </summary>
    public class MiscMenuView : MonoBehaviour
    {
        // UI Elements
        private VisualElement root;
        private Button returnSpawnBtn;
        private Button recenterBtn;
        private Label spawnInfoLabel;

        // References
        private XROrigin xrOrigin;
        private ScenePortal scenePortal;

        /// <summary>
        /// Initialize with the instantiated UXML content.
        /// Called by QuickMenuController when tab is loaded.
        /// </summary>
        public void Initialize(VisualElement content)
        {
            root = content;

            // Find references
            xrOrigin = FindFirstObjectByType<XROrigin>();
            scenePortal = FindFirstObjectByType<ScenePortal>();

            QueryElements();
            SetupCallbacks();
            UpdateSpawnInfo();
        }

        private void QueryElements()
        {
            returnSpawnBtn = root.Q<Button>("return-spawn-btn");
            recenterBtn = root.Q<Button>("recenter-btn");
            spawnInfoLabel = root.Q<Label>("spawn-info-label");
        }

        private void SetupCallbacks()
        {
            returnSpawnBtn?.RegisterCallback<ClickEvent>(evt => OnReturnToSpawnClicked());
            recenterBtn?.RegisterCallback<ClickEvent>(evt => OnRecenterClicked());

            SetButtonLabelsPickingMode(returnSpawnBtn);
            SetButtonLabelsPickingMode(recenterBtn);
        }

        private void SetButtonLabelsPickingMode(Button button)
        {
            if (button == null) return;
            var label = button.Q<Label>();
            if (label != null)
            {
                label.pickingMode = PickingMode.Ignore;
            }
        }

        private void UpdateSpawnInfo()
        {
            if (spawnInfoLabel == null) return;

            if (scenePortal != null)
            {
                Vector3 pos = scenePortal.ArrivalPosition;
                spawnInfoLabel.text = $"Spawn: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})";
            }
            else
            {
                spawnInfoLabel.text = "No spawn point found";
            }
        }

        #region Button Handlers

        private void OnReturnToSpawnClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();

            if (xrOrigin == null)
            {
                xrOrigin = FindFirstObjectByType<XROrigin>();
                if (xrOrigin == null)
                {
                    Debug.LogWarning("[MiscMenuView] XROrigin not found");
                    UIAudioManager.Instance?.PlayError();
                    return;
                }
            }

            if (scenePortal == null)
            {
                scenePortal = FindFirstObjectByType<ScenePortal>();
            }

            if (scenePortal != null)
            {
                TeleportToPosition(scenePortal.ArrivalPosition, scenePortal.ArrivalRotation);
                Debug.Log($"[MiscMenuView] Teleported to spawn: {scenePortal.ArrivalPosition}");
            }
            else
            {
                // Fallback: teleport to origin
                TeleportToPosition(Vector3.zero, Quaternion.identity);
                Debug.Log("[MiscMenuView] No ScenePortal found, teleported to origin");
            }
        }

        private void OnRecenterClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();

            // Just reset tracking origin if available
            if (xrOrigin != null)
            {
                // Recenter by making camera look forward
                var camera = xrOrigin.Camera;
                if (camera != null)
                {
                    Vector3 cameraForward = camera.transform.forward;
                    cameraForward.y = 0;
                    cameraForward.Normalize();

                    float yRotation = Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
                    float rotationOffset = -yRotation + xrOrigin.transform.eulerAngles.y;

                    // Apply rotation offset so camera faces original XR Origin forward
                    xrOrigin.transform.rotation = Quaternion.Euler(0, rotationOffset, 0);

                    Debug.Log("[MiscMenuView] View recentered");
                }
            }
        }

        /// <summary>
        /// Teleport XR Origin to target position and rotation.
        /// Adjusts for camera offset (similar to SceneTransitionManager.RecenterXROrigin).
        /// </summary>
        private void TeleportToPosition(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (xrOrigin == null) return;

            Camera camera = xrOrigin.Camera;
            if (camera == null) return;

            // Get current camera forward direction (horizontal only)
            Vector3 cameraForward = camera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            // Get target forward direction
            Vector3 targetForward = targetRotation * Vector3.forward;
            targetForward.y = 0;
            targetForward.Normalize();

            // Calculate rotation offset between camera and target
            float rotationOffset = Vector3.SignedAngle(cameraForward, targetForward, Vector3.up);

            // Apply rotation to XR Origin
            xrOrigin.transform.rotation = Quaternion.Euler(0, xrOrigin.transform.eulerAngles.y + rotationOffset, 0);

            // Get camera's local offset from XR Origin (HMD tracking offset) after rotation
            Vector3 cameraLocalOffset = xrOrigin.transform.InverseTransformPoint(camera.transform.position);
            cameraLocalOffset.y = 0;

            // Calculate new XR Origin position so camera ends up at target
            Vector3 newOriginPosition = targetPosition - xrOrigin.transform.TransformVector(cameraLocalOffset);
            newOriginPosition.y = targetPosition.y;

            xrOrigin.transform.position = newOriginPosition;
        }

        #endregion
    }
}
