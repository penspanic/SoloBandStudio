using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SoloBandStudio.UI.Dialogs
{
    /// <summary>
    /// Generic selection dialog for choosing from a list of items.
    /// Can be configured with or without delete buttons.
    /// </summary>
    public class SelectionDialog : DialogBase
    {
        private ScrollView itemListView;
        private Label titleLabel;

        // Configuration
        public string Title { get; set; } = "Select Item";
        public string EmptyText { get; set; } = "No items available";
        public bool ShowDeleteButton { get; set; } = false;

        // Events
        public event Action<int, string> OnItemSelected;
        public event Action<int, string> OnItemDeleted;

        /// <summary>
        /// Function to get the list of items. Returns (display name, value) pairs.
        /// </summary>
        public Func<List<(string displayName, string value)>> GetItemsFunc { get; set; }

        protected override void BuildContent(VisualElement container)
        {
            // Title
            titleLabel = new Label(Title);
            titleLabel.AddToClassList("dialog-title");
            container.Add(titleLabel);

            // Item list scroll view
            itemListView = new ScrollView();
            itemListView.AddToClassList("dialog-file-list");
            container.Add(itemListView);

            // Buttons
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("dialog-buttons");

            var cancelBtn = CreateButton("Cancel", "cancel-btn", Hide);
            buttonRow.Add(cancelBtn);
            container.Add(buttonRow);
        }

        public override void Show()
        {
            if (titleLabel != null)
            {
                titleLabel.text = Title;
            }
            RefreshItemList();
            base.Show();
        }

        public void RefreshItemList()
        {
            if (itemListView == null) return;

            itemListView.Clear();

            var items = GetItemsFunc?.Invoke() ?? new List<(string, string)>();

            if (items.Count == 0)
            {
                var empty = new Label(EmptyText);
                empty.AddToClassList("dialog-empty-text");
                itemListView.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var (displayName, value) = items[i];
                var item = CreateListItem(i, displayName, value);
                itemListView.Add(item);
            }
        }

        private VisualElement CreateListItem(int index, string displayName, string value)
        {
            var item = new VisualElement();
            item.AddToClassList("dialog-file-item");

            // Clickable button for selection
            var nameBtn = new Button(() => SelectItem(index, value));
            nameBtn.AddToClassList("dialog-file-name");
            nameBtn.text = displayName;
            item.Add(nameBtn);

            // Optional delete button
            if (ShowDeleteButton)
            {
                var deleteBtn = new Button(() => DeleteItem(index, value));
                deleteBtn.AddToClassList("dialog-file-delete");
                deleteBtn.text = "X";
                item.Add(deleteBtn);
            }

            return item;
        }

        private void SelectItem(int index, string value)
        {
            OnItemSelected?.Invoke(index, value);
            Hide();
        }

        private void DeleteItem(int index, string value)
        {
            OnItemDeleted?.Invoke(index, value);
            RefreshItemList();
        }
    }
}
