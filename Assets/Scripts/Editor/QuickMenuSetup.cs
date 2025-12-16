using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using SoloBandStudio.UI.QuickMenu;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor utility to create and setup QuickMenu in the scene.
    /// </summary>
    public static class QuickMenuSetup
    {
        private const string UXML_PATH = "Assets/UI/QuickMenu.uxml";
        private const string TOD_UXML_PATH = "Assets/UI/TODMenuView.uxml";
        private const string SETTINGS_UXML_PATH = "Assets/UI/SettingsMenuView.uxml";
        private const string PANEL_SETTINGS_PATH = "Assets/UI/QuickMenuPanelSettings.asset";

        [MenuItem("SoloBandStudio/Create Quick Menu", false, 300)]
        public static void CreateQuickMenu()
        {
            var existing = Object.FindFirstObjectByType<QuickMenuController>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Quick Menu Exists",
                    "A QuickMenu already exists in the scene.\n\nCreate another one?",
                    "Create", "Cancel"))
                {
                    Selection.activeGameObject = existing.gameObject;
                    return;
                }
            }

            // Create root object
            GameObject menuRoot = new GameObject("QuickMenu");
            Undo.RegisterCreatedObjectUndo(menuRoot, "Create Quick Menu");

            // Add FollowPlayerView
            menuRoot.AddComponent<FollowPlayerView>();

            // Create UI object
            GameObject uiObj = new GameObject("QuickMenuUI");
            uiObj.transform.SetParent(menuRoot.transform);
            uiObj.transform.localPosition = Vector3.zero;
            uiObj.transform.localRotation = Quaternion.identity;
            uiObj.transform.localScale = Vector3.one * 0.001f;

            // Add UIDocument
            var uiDocument = uiObj.AddComponent<UIDocument>();

            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (visualTree != null)
            {
                uiDocument.visualTreeAsset = visualTree;
            }

            // Set panel settings
            var panelSettings = CreateOrGetPanelSettings();
            if (panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
            }

            // Add QuickMenuController and setup references
            var controller = uiObj.AddComponent<QuickMenuController>();
            SetupControllerReferences(controller);

            Selection.activeGameObject = menuRoot;

            EditorUtility.DisplayDialog("Quick Menu Created",
                "QuickMenu has been created!\n\n" +
                "Setup:\n" +
                "1. The menu follows the player's camera\n" +
                "2. Press Tab (keyboard) or Menu/Y button (VR) to toggle\n" +
                "3. TOD controls are ready to use\n\n" +
                "Make sure you have a TODManager in the scene.",
                "OK");
        }

        private static PanelSettings CreateOrGetPanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_SETTINGS_PATH);
            if (existing != null) return existing;

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = 1f;

            AssetDatabase.CreateAsset(panelSettings, PANEL_SETTINGS_PATH);
            AssetDatabase.SaveAssets();

            return panelSettings;
        }

        private static void SetupControllerReferences(QuickMenuController controller)
        {
            SerializedObject so = new SerializedObject(controller);

            // Load tab UXML assets
            var todUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TOD_UXML_PATH);
            var settingsUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SETTINGS_UXML_PATH);

            var todProp = so.FindProperty("todMenuUxml");
            if (todProp != null) todProp.objectReferenceValue = todUxml;

            var settingsProp = so.FindProperty("settingsMenuUxml");
            if (settingsProp != null) settingsProp.objectReferenceValue = settingsUxml;

            so.ApplyModifiedProperties();
        }

        [MenuItem("SoloBandStudio/Create TODManager", false, 301)]
        public static void CreateTODManager()
        {
            var existing = Object.FindFirstObjectByType<Core.TODManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("TODManager Exists",
                    "A TODManager already exists in the scene.",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject todObj = new GameObject("TODManager");
            Undo.RegisterCreatedObjectUndo(todObj, "Create TODManager");

            todObj.AddComponent<Core.TODManager>();
            todObj.AddComponent<Core.DayNightCycle>();

            Selection.activeGameObject = todObj;

            EditorUtility.DisplayDialog("TODManager Created",
                "TODManager and DayNightCycle have been created!\n\n" +
                "The Directional Light will be found automatically.",
                "OK");
        }
    }
}
