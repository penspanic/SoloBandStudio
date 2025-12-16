using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoloBandStudio.UI.Dialogs
{
    /// <summary>
    /// A simple confirmation dialog with title, message, and confirm/cancel buttons.
    /// Uses UXML template for structure.
    /// </summary>
    public class ConfirmationDialog
    {
        private VisualElement root;
        private VisualElement overlay;
        private Label titleLabel;
        private Label messageLabel;
        private Button confirmButton;
        private Button cancelButton;

        public bool IsVisible => overlay != null && !overlay.ClassListContains("hidden");

        // Events
        public event Action OnConfirm;
        public event Action OnCancel;
        public event Action OnClosed;

        /// <summary>
        /// Initialize the dialog from an existing UXML structure in the root.
        /// </summary>
        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;

            overlay = root.Q<VisualElement>("dialog-overlay");
            titleLabel = root.Q<Label>("title-label");
            messageLabel = root.Q<Label>("message-label");
            confirmButton = root.Q<Button>("confirm-btn");
            cancelButton = root.Q<Button>("cancel-btn");

            if (overlay == null)
            {
                Debug.LogError("[ConfirmationDialog] dialog-overlay not found in UXML");
                return;
            }

            // Setup button callbacks
            confirmButton?.RegisterCallback<ClickEvent>(evt =>
            {
                UIAudioManager.Instance?.PlayButtonClick();
                Hide();
                OnConfirm?.Invoke();
            });

            cancelButton?.RegisterCallback<ClickEvent>(evt =>
            {
                UIAudioManager.Instance?.PlayButtonClick();
                Hide();
                OnCancel?.Invoke();
            });

            // Close on overlay click
            overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == overlay)
                {
                    Hide();
                    OnCancel?.Invoke();
                }
            });
        }

        /// <summary>
        /// Show the dialog with specified content.
        /// </summary>
        public void Show(string title, string message, string confirmText = "확인", string cancelText = "취소")
        {
            if (titleLabel != null)
                titleLabel.text = title;

            if (messageLabel != null)
                messageLabel.text = message;

            if (confirmButton != null)
                confirmButton.text = confirmText;

            if (cancelButton != null)
                cancelButton.text = cancelText;

            Show();
        }

        /// <summary>
        /// Show the dialog.
        /// </summary>
        public void Show()
        {
            overlay?.RemoveFromClassList("hidden");
            UIAudioManager.Instance?.PlayPanelOpen();
        }

        /// <summary>
        /// Hide the dialog.
        /// </summary>
        public void Hide()
        {
            overlay?.AddToClassList("hidden");
            UIAudioManager.Instance?.PlayPanelClose();
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Clear all event listeners.
        /// </summary>
        public void ClearListeners()
        {
            OnConfirm = null;
            OnCancel = null;
            OnClosed = null;
        }
    }
}
