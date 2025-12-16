using System;
using UnityEngine.UIElements;

namespace SoloBandStudio.UI.Dialogs
{
    /// <summary>
    /// Dialog for saving files with a filename input.
    /// </summary>
    public class SaveDialog : DialogBase
    {
        private TextField filenameField;
        private Label errorLabel;

        public event Action<string> OnSaveConfirmed;

        public string DefaultFilename { get; set; } = "";

        protected override void BuildContent(VisualElement container)
        {
            // Title
            var title = new Label("Save Song");
            title.AddToClassList("dialog-title");
            container.Add(title);

            // Filename input
            filenameField = new TextField("Filename");
            filenameField.AddToClassList("dialog-input");
            container.Add(filenameField);

            // Error label (hidden by default)
            errorLabel = new Label();
            errorLabel.AddToClassList("dialog-error");
            errorLabel.AddToClassList("hidden");
            container.Add(errorLabel);

            // Buttons
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("dialog-buttons");

            var cancelBtn = CreateButton("Cancel", "cancel-btn", Hide);
            var saveBtn = CreateButton("Save", "confirm-btn", OnSaveClicked);

            buttonRow.Add(cancelBtn);
            buttonRow.Add(saveBtn);
            container.Add(buttonRow);
        }

        public override void Show()
        {
            // Set default filename
            if (filenameField != null)
            {
                filenameField.value = string.IsNullOrEmpty(DefaultFilename)
                    ? $"Song_{System.DateTime.Now:yyyyMMdd_HHmmss}"
                    : DefaultFilename;
            }

            // Hide error
            errorLabel?.AddToClassList("hidden");

            base.Show();

            // Focus the text field
            filenameField?.Focus();
        }

        private void OnSaveClicked()
        {
            if (filenameField == null) return;

            string filename = filenameField.value.Trim();

            // Validate
            if (string.IsNullOrEmpty(filename))
            {
                ShowError("Filename cannot be empty");
                return;
            }

            // Remove invalid characters
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                filename = filename.Replace(c.ToString(), "");
            }

            if (string.IsNullOrEmpty(filename))
            {
                ShowError("Invalid filename");
                return;
            }

            OnSaveConfirmed?.Invoke(filename);
            Hide();
        }

        private void ShowError(string message)
        {
            if (errorLabel == null) return;
            errorLabel.text = message;
            errorLabel.RemoveFromClassList("hidden");
        }
    }
}
