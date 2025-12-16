using System;
using System.Collections.Generic;
using SoloBandStudio.Core;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace SoloBandStudio.UI.QuickMenu
{
    /// <summary>
    /// Controls the quick menu UI Toolkit panel.
    /// Handles toggle input, animation, and tab switching.
    /// </summary>
    public class QuickMenuController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Tab UXML Assets")]
        [SerializeField] private VisualTreeAsset loopStationMenuUxml;
        [SerializeField] private VisualTreeAsset todMenuUxml;
        [SerializeField] private VisualTreeAsset settingsMenuUxml;
        [SerializeField] private VisualTreeAsset miscMenuUxml;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.25f;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleAction;

        [Header("Haptic Feedback")]
        [SerializeField] private bool enableHaptics = true;
        [SerializeField] private float hapticAmplitude = 0.2f;
        [SerializeField] private float hapticDuration = 0.05f;


        [Header("Follow")]
        [SerializeField] private FollowPlayerView followPlayerView;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        // UI Elements
        private VisualElement root;
        private VisualElement menuRoot;
        private VisualElement contentArea;
        private Button tabLoopStation;
        private Button tabTod;
        private Button tabSettings;
        private Button tabMisc;

        // State
        private bool isOpen;
        private bool isAnimating;
        private float animationProgress;
        private int currentTabIndex;

        // Input
        private InputAction menuButtonAction;

        // Tab views
        private LoopStationMenuView loopStationMenuView;
        private TODMenuView todMenuView;
        private MiscMenuView miscMenuView;

        // Events
        public event Action<bool> OnMenuToggled;
        public event Action<int> OnTabChanged;

        public bool IsOpen => isOpen;
        public int CurrentTabIndex => currentTabIndex;

        private void Start()
        {
            root = uiDocument.rootVisualElement;
            menuRoot = root.Q<VisualElement>("quick-menu-root");
            contentArea = root.Q<VisualElement>("content-area");
            tabLoopStation = root.Q<Button>("tab-loopstation");
            tabTod = root.Q<Button>("tab-tod");
            tabSettings = root.Q<Button>("tab-settings");
            tabMisc = root.Q<Button>("tab-misc");

            // Setup tab buttons
            tabLoopStation.RegisterCallback<ClickEvent>(evt => SwitchTab(0, isInitial: false));
            tabTod.RegisterCallback<ClickEvent>(evt => SwitchTab(1, isInitial: false));
            tabSettings.RegisterCallback<ClickEvent>(evt => SwitchTab(2, isInitial: false));
            tabMisc.RegisterCallback<ClickEvent>(evt => SwitchTab(3, isInitial: false));

            // Setup fallback input
            if (toggleAction == null)
            {
                menuButtonAction = new InputAction("MenuButton", InputActionType.Button);
                menuButtonAction.AddBinding("<XRController>{LeftHand}/menuButton");
                menuButtonAction.AddBinding("<XRController>{RightHand}/menuButton");
                menuButtonAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
                menuButtonAction.performed += OnTogglePerformed;
                menuButtonAction.Enable();
            }

            // Load initial tab
            SwitchTab(0, isInitial: true);

            // Start closed
            SetMenuState(false, instant: true);
        }

        private void OnEnable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.performed += OnTogglePerformed;
                toggleAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.performed -= OnTogglePerformed;
            }

            if (menuButtonAction != null)
            {
                menuButtonAction.performed -= OnTogglePerformed;
            }
        }

        private void OnDestroy()
        {
            menuButtonAction?.Dispose();
        }

        private void Update()
        {
            UpdateAnimation();

            // Keyboard toggle
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void OnTogglePerformed(InputAction.CallbackContext ctx)
        {
            Toggle();
        }

        #region Public API

        public void Toggle()
        {
            if (isAnimating) return;

            bool opening = !isOpen;

            SetMenuState(opening);
            PlayHapticFeedback();
            if (opening) UIAudioManager.EnsureInstance().PlayPanelOpen();
            else UIAudioManager.EnsureInstance().PlayPanelClose();
        }

        public void Open()
        {
            if (isOpen || isAnimating) return;
            SetMenuState(true);
            PlayHapticFeedback();
            UIAudioManager.EnsureInstance().PlayPanelOpen();
        }

        public void Close()
        {
            if (!isOpen || isAnimating) return;
            SetMenuState(false);
            UIAudioManager.EnsureInstance().PlayPanelClose();
        }

        public void SwitchTab(int index, bool isInitial)
        {
            if (index == currentTabIndex && contentArea.childCount > 0) return;

            // Update tab button styles
            tabLoopStation.EnableInClassList("active", index == 0);
            tabTod.EnableInClassList("active", index == 1);
            tabSettings.EnableInClassList("active", index == 2);
            tabMisc.EnableInClassList("active", index == 3);

            // Clear content
            contentArea.Clear();

            // Load tab content
            switch (index)
            {
                case 0:
                    LoadLoopStationTab();
                    break;
                case 1:
                    LoadTODTab();
                    break;
                case 2:
                    LoadSettingsTab();
                    break;
                case 3:
                    LoadMiscTab();
                    break;
            }

            currentTabIndex = index;
            if (!isInitial)
                UIAudioManager.EnsureInstance().PlayTabSwitch();
            OnTabChanged?.Invoke(currentTabIndex);
        }

        #endregion

        #region Tab Loading

        private void LoadLoopStationTab()
        {
            // Load UXML if not assigned
            if (loopStationMenuUxml == null)
            {
                loopStationMenuUxml = UnityEngine.Resources.Load<VisualTreeAsset>("UI/LoopStationMenuView");
                if (loopStationMenuUxml == null)
                {
                    #if UNITY_EDITOR
                    loopStationMenuUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/UI/LoopStationMenuView.uxml");
                    #endif
                }
            }

            if (loopStationMenuUxml == null)
            {
                Debug.LogError("[QuickMenu] LoopStationMenuView.uxml not found!");
                return;
            }

            var loopStationContent = loopStationMenuUxml.Instantiate();
            contentArea.Add(loopStationContent);

            if (loopStationMenuView == null)
            {
                loopStationMenuView = GetComponent<LoopStationMenuView>();
                if (loopStationMenuView == null)
                {
                    loopStationMenuView = gameObject.AddComponent<LoopStationMenuView>();
                }
            }
            loopStationMenuView.Initialize(loopStationContent);
        }

        private void LoadTODTab()
        {
            if (TODManager.Instance == null)
                return;

            // Load UXML if not assigned
            if (todMenuUxml == null)
            {
                todMenuUxml = UnityEngine.Resources.Load<VisualTreeAsset>("UI/TODMenuView");
                if (todMenuUxml == null)
                {
                    #if UNITY_EDITOR
                    todMenuUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/UI/TODMenuView.uxml");
                    #endif
                }
            }

            if (todMenuUxml == null)
            {
                Debug.LogError("[QuickMenu] TODMenuView.uxml not found!");
                return;
            }

            var todContent = todMenuUxml.Instantiate();
            contentArea.Add(todContent);

            if (todMenuView == null)
            {
                todMenuView = GetComponent<TODMenuView>();
                if (todMenuView == null)
                {
                    todMenuView = gameObject.AddComponent<TODMenuView>();
                }
            }
            todMenuView.Initialize(todContent);
        }

        private void LoadSettingsTab()
        {
            // Load UXML if not assigned
            if (settingsMenuUxml == null)
            {
                settingsMenuUxml = UnityEngine.Resources.Load<VisualTreeAsset>("UI/SettingsMenuView");
                if (settingsMenuUxml == null)
                {
                    #if UNITY_EDITOR
                    settingsMenuUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/UI/SettingsMenuView.uxml");
                    #endif
                }
            }

            if (settingsMenuUxml == null)
            {
                Debug.LogError("[QuickMenu] SettingsMenuView.uxml not found!");
                return;
            }

            var settingsContent = settingsMenuUxml.Instantiate();
            contentArea.Add(settingsContent);
        }

        private void LoadMiscTab()
        {
            // Load UXML if not assigned
            if (miscMenuUxml == null)
            {
                miscMenuUxml = UnityEngine.Resources.Load<VisualTreeAsset>("UI/MiscMenuView");
                if (miscMenuUxml == null)
                {
                    #if UNITY_EDITOR
                    miscMenuUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/UI/MiscMenuView.uxml");
                    #endif
                }
            }

            if (miscMenuUxml == null)
            {
                Debug.LogError("[QuickMenu] MiscMenuView.uxml not found!");
                return;
            }

            var miscContent = miscMenuUxml.Instantiate();
            contentArea.Add(miscContent);

            if (miscMenuView == null)
            {
                miscMenuView = GetComponent<MiscMenuView>();
                if (miscMenuView == null)
                {
                    miscMenuView = gameObject.AddComponent<MiscMenuView>();
                }
            }
            miscMenuView.Initialize(miscContent);
        }

        #endregion

        #region Animation

        private void SetMenuState(bool open, bool instant = false)
        {
            isOpen = open;

            // Snap to camera position when opening
            if (open && followPlayerView != null)
            {
                followPlayerView.SnapToPosition();
            }

            if (instant)
            {
                animationProgress = open ? 1f : 0f;
                ApplyAnimationState(animationProgress);
            }
            else
            {
                isAnimating = true;
            }

            OnMenuToggled?.Invoke(isOpen);
        }

        private void UpdateAnimation()
        {
            if (!isAnimating || menuRoot == null) return;

            float targetProgress = isOpen ? 1f : 0f;
            float direction = isOpen ? 1f : -1f;

            animationProgress += direction * Time.deltaTime / animationDuration;
            animationProgress = Mathf.Clamp01(animationProgress);

            ApplyAnimationState(animationProgress);

            if (Mathf.Approximately(animationProgress, targetProgress))
            {
                isAnimating = false;
            }
        }

        private void ApplyAnimationState(float progress)
        {
            if (menuRoot == null) return;

            // Scale and opacity
            float scale = Mathf.Lerp(0.8f, 1f, progress);
            float translateY = Mathf.Lerp(20f, 0f, progress);

            menuRoot.style.opacity = progress;
            menuRoot.style.scale = new Scale(new Vector3(scale, scale, 1f));
            menuRoot.style.translate = new Translate(0, translateY);
            menuRoot.style.display = progress > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Haptics & Audio

        private void PlayHapticFeedback()
        {
            if (!enableHaptics) return;

            var controllers = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(FindObjectsSortMode.None);
            foreach (var controller in controllers)
            {
                controller.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }
        }

        #endregion

        #region Debug

        private void Log(string message)
        {
            if (debugLog)
            {
                Debug.Log($"[QuickMenu] {message}");
            }
        }

        #endregion
    }
}
