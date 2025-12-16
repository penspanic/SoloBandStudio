using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoloBandStudio.UI.Dialogs
{
    /// <summary>
    /// Base class for modal dialogs in UI Toolkit.
    /// </summary>
    public abstract class DialogBase
    {
        protected VisualElement root;
        protected VisualElement overlay;
        protected VisualElement dialogContainer;

        public bool IsVisible => overlay != null && !overlay.ClassListContains("hidden");

        public event Action OnClosed;

        /// <summary>
        /// Create and attach dialog to parent.
        /// </summary>
        public void Create(VisualElement parent)
        {
            if (parent == null)
            {
                Debug.LogError("[DialogBase] Parent is null");
                return;
            }

            root = parent;

            // Create overlay (blocks clicks behind dialog)
            overlay = new VisualElement();
            overlay.name = "dialog-overlay";
            overlay.AddToClassList("dialog-overlay");
            overlay.AddToClassList("hidden");

            // Close on overlay click
            overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == overlay)
                {
                    Hide();
                }
            });

            // Create dialog container
            dialogContainer = new VisualElement();
            dialogContainer.name = "dialog-container";
            dialogContainer.AddToClassList("dialog-container");

            // Build dialog content
            BuildContent(dialogContainer);

            overlay.Add(dialogContainer);
            root.Add(overlay);
        }

        /// <summary>
        /// Override to build dialog content.
        /// </summary>
        protected abstract void BuildContent(VisualElement container);

        /// <summary>
        /// Show the dialog.
        /// </summary>
        public virtual void Show()
        {
            overlay?.RemoveFromClassList("hidden");
            UIAudioManager.Instance?.PlayPanelOpen();
        }

        /// <summary>
        /// Hide the dialog.
        /// </summary>
        public virtual void Hide()
        {
            overlay?.AddToClassList("hidden");
            UIAudioManager.Instance?.PlayPanelClose();
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Remove dialog from DOM.
        /// </summary>
        public void Destroy()
        {
            overlay?.RemoveFromHierarchy();
            overlay = null;
            dialogContainer = null;
        }

        /// <summary>
        /// Create a styled button.
        /// </summary>
        protected Button CreateButton(string text, string className, Action onClick)
        {
            var btn = new Button(() =>
            {
                UIAudioManager.Instance?.PlayButtonClick();
                onClick?.Invoke();
            });
            btn.AddToClassList("dialog-btn");
            if (!string.IsNullOrEmpty(className))
            {
                btn.AddToClassList(className);
            }
            btn.text = text;
            return btn;
        }
    }
}
