using UnityEngine;
using UnityEngine.UIElements;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Generates and manages the piano keys sidebar.
    /// Syncs vertical position with grid scroll.
    /// </summary>
    public class PianoRollPianoKeys
    {
        private readonly PianoRollLayout layout;
        private readonly PianoRollData data;

        public PianoRollPianoKeys(PianoRollLayout layout, PianoRollData data)
        {
            this.layout = layout;
            this.data = data;
        }

        public void Generate()
        {
            if (layout.PianoContent == null) return;

            layout.PianoContent.Clear();

            // Set content height
            layout.PianoContent.style.height = data.GridHeight;
            layout.PianoContent.style.top = 0;

            // Generate from high to low (top to bottom)
            for (int note = data.MaxVisibleNote; note >= data.MinVisibleNote; note--)
            {
                var keyElement = new VisualElement();
                keyElement.AddToClassList("piano-key");

                bool isBlack = PianoRollData.IsBlackKey(note);
                bool isC = PianoRollData.IsC(note);

                keyElement.AddToClassList(isBlack ? "piano-key-black" : "piano-key-white");
                if (isC) keyElement.AddToClassList("piano-key-c");

                // Only show label for C notes and boundary notes
                if (isC || note == data.MinVisibleNote || note == data.MaxVisibleNote)
                {
                    var label = new Label(PianoRollData.GetNoteName(note));
                    label.AddToClassList("piano-key-label");
                    if (isC) label.AddToClassList("piano-key-label-c");
                    keyElement.Add(label);
                }

                layout.PianoContent.Add(keyElement);
            }
        }

        /// <summary>
        /// Sync piano keys position with grid scroll.
        /// Called directly from Update - no scheduling.
        /// </summary>
        public void SyncScroll(Vector2 scrollOffset)
        {
            if (layout.PianoContent == null) return;

            // Move content by negative scroll Y (translate instead of top for performance)
            layout.PianoContent.style.translate = new Translate(0, -scrollOffset.y);
        }
    }
}
