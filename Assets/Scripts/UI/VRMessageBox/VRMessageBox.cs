using System;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.UI.QuickMenu;
using SoloBandStudio.Common.SceneManagement;

namespace SoloBandStudio.UI
{
    /// <summary>
    /// VR-friendly MessageBox that appears in front of the player.
    /// Simple structure similar to QuickMenu.
    /// </summary>
    public class VRMessageBox : MonoBehaviour
    {
        private static VRMessageBox _instance;

        public static VRMessageBox Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<VRMessageBox>();

                    if (_instance == null)
                    {
                        var prefab = Resources.Load<GameObject>("VRMessageBox");
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab);
                            go.name = "VRMessageBox";
                            DontDestroyOnLoad(go);
                            _instance = go.GetComponent<VRMessageBox>();
                        }
                        else
                        {
                            Debug.LogError("[VRMessageBox] Prefab not found in Resources!");
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Follow Player")]
        [SerializeField] private FollowPlayerView followPlayerView;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.2f;

        // UI Elements
        private VisualElement root;
        private VisualElement messageboxRoot;
        private Label titleLabel;
        private Label messageLabel;
        private Button confirmButton;
        private Button cancelButton;

        // State
        private bool isOpen;
        private bool isAnimating;
        private float animationProgress;

        // Callbacks
        private Action onConfirm;
        private Action onCancel;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeUI();
            InitializeFollowPlayerView();

            // Start closed
            SetOpen(false, instant: true);
        }

        private void Update()
        {
            UpdateAnimation();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void InitializeUI()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponentInChildren<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("[VRMessageBox] UIDocument not found!");
                return;
            }

            root = uiDocument.rootVisualElement;
            messageboxRoot = root.Q<VisualElement>("messagebox-root");
            titleLabel = root.Q<Label>("title-label");
            messageLabel = root.Q<Label>("message-label");
            confirmButton = root.Q<Button>("confirm-btn");
            cancelButton = root.Q<Button>("cancel-btn");

            // Setup button callbacks
            confirmButton?.RegisterCallback<ClickEvent>(evt =>
            {
                UIAudioManager.Instance?.PlayButtonClick();
                var callback = onConfirm;
                Close();
                callback?.Invoke();
            });

            cancelButton?.RegisterCallback<ClickEvent>(evt =>
            {
                UIAudioManager.Instance?.PlayButtonClick();
                var callback = onCancel;
                Close();
                callback?.Invoke();
            });
        }

        private void InitializeFollowPlayerView()
        {
            if (followPlayerView == null)
            {
                followPlayerView = GetComponent<FollowPlayerView>();
            }

            if (followPlayerView != null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    followPlayerView.SetTargetCamera(mainCamera.transform);
                }
            }
        }

        #region Public API

        public void Show(
            string title,
            string message,
            Action onConfirm,
            Action onCancel = null,
            string confirmText = "Confirm",
            string cancelText = "Cancel")
        {
            if (isOpen)
            {
                Close();
            }

            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            // Update UI
            if (titleLabel != null) titleLabel.text = title;
            if (messageLabel != null) messageLabel.text = message;
            if (confirmButton != null) confirmButton.text = confirmText;
            if (cancelButton != null) cancelButton.text = cancelText;

            // Snap to player position
            followPlayerView?.SnapToPosition();

            // Open
            SetOpen(true);
            UIAudioManager.Instance?.PlayPanelOpen();
        }

        public void Close()
        {
            if (!isOpen) return;

            SetOpen(false);
            UIAudioManager.Instance?.PlayPanelClose();

            onConfirm = null;
            onCancel = null;
        }

        #endregion

        #region Animation

        private void SetOpen(bool open, bool instant = false)
        {
            isOpen = open;

            if (instant)
            {
                animationProgress = open ? 1f : 0f;
                ApplyAnimationState(animationProgress);
                isAnimating = false;
            }
            else
            {
                isAnimating = true;
            }
        }

        private void UpdateAnimation()
        {
            if (!isAnimating || messageboxRoot == null) return;

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
            if (messageboxRoot == null) return;

            float scale = Mathf.Lerp(0.8f, 1f, progress);
            float translateY = Mathf.Lerp(20f, 0f, progress);

            messageboxRoot.style.opacity = progress;
            messageboxRoot.style.scale = new Scale(new Vector3(scale, scale, 1f));
            messageboxRoot.style.translate = new Translate(0, translateY);
            messageboxRoot.style.display = progress > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion
    }
}
