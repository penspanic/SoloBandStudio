using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;

namespace SoloBandStudio.Instruments.Keyboard
{
    public enum KeyboardLayoutType
    {
        Standard88Keys,  // A0 to C8 (MIDI 21-108)
        Custom           // Use startMidiNote and endMidiNote
    }

    /// <summary>
    /// Manages the keyboard visuals and key creation.
    /// Supports standard 88-key layout or custom MIDI note ranges.
    /// Used for Piano, Bass, and other keyboard-based instruments.
    /// </summary>
    public class KeyboardLayout : MonoBehaviour
    {
        [Header("Keyboard Layout")]
        [SerializeField] private KeyboardLayoutType layout = KeyboardLayoutType.Standard88Keys;

        [Header("Custom Range (only used when Layout = Custom)")]
        [SerializeField] private int startMidiNote = 21;  // A0
        [SerializeField] private int endMidiNote = 108;   // C8

        [Header("Scale")]
        [SerializeField] private float keyboardScale = 1f;

        [Header("Prefab Mode")]
        [SerializeField] private bool usePrefabMeshes = true;
        [SerializeField] private Mesh[] whiteKeyMeshes = new Mesh[7]; // C, D, E, F, G, A, B
        [SerializeField] private Mesh[] blackKeyMeshes = new Mesh[5]; // C#, D#, F#, G#, A#

        [Header("Key Spacing")]
        [SerializeField] private float whiteKeySpacing = 0.024f;
        [SerializeField] private float keyPressDepth = 0.005f;

        [Header("Black Key Offset")]
        [SerializeField] private float blackKeyYOffset = 0.01f;
        [SerializeField] private float blackKeyZOffset = 0.03f;

        [Header("Materials")]
        [SerializeField] private Material whiteKeyMaterial;
        [SerializeField] private Material blackKeyMaterial;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material whiteKeyPressedMaterial;
        [SerializeField] private Material blackKeyPressedMaterial;

        [Header("Black Key X Adjustment")]
        [SerializeField] private float blackKeyXOffset = 0.2f;

        [Header("Black Key Collider")]
        [Tooltip("Scale multiplier for black key colliders. Increase X to make them easier to hit.")]
        [SerializeField] private Vector3 blackKeyColliderScale = new Vector3(1.5f, 1f, 1f);

        private List<KeyboardKey> allKeys = new List<KeyboardKey>();
        private Dictionary<int, KeyboardKey> keysByMidiNote = new Dictionary<int, KeyboardKey>();

        // Events for Keyboard to subscribe to
        public event Action<int, float> OnKeyPressed;  // (midiNote, velocity)
        public event Action<int> OnKeyReleased;        // (midiNote)

        /// <summary>
        /// When true, keys stay visually pressed until manually reset (for quiz mode).
        /// </summary>
        public bool ToggleMode { get; set; } = false;

        // Standard 88-key piano range
        private const int STANDARD_START = 21;  // A0
        private const int STANDARD_END = 108;   // C8

        // Which notes are black keys (within an octave, 0-11)
        private static readonly bool[] isBlackKey = { false, true, false, true, false, false, true, false, true, false, true, false };
        // C, C#, D, D#, E, F, F#, G, G#, A, A#, B

        // White key index within octave for each note (0-11) -> (0-6 for white keys, -1 for black)
        private static readonly int[] whiteKeyIndex = { 0, -1, 1, -1, 2, 3, -1, 4, -1, 5, -1, 6 };

        // Black key index within octave (0-4 for C#, D#, F#, G#, A#)
        private static readonly int[] blackKeyIndex = { -1, 0, -1, 1, -1, -1, 2, -1, 3, -1, 4, -1 };

        // Black key X offset relative to previous white key
        private static readonly float[] blackKeyRelativeOffset = { 0f, 0.5f, 0f, 0.5f, 0f, 0f, 0.5f, 0f, 0.5f, 0f, 0.5f, 0f };

        private void Start()
        {
            if (allKeys.Count == 0)
            {
                GenerateKeyboard();
            }
        }

        /// <summary>
        /// Initialize and generate the keyboard.
        /// </summary>
        public void Initialize()
        {
            GenerateKeyboard();
            Debug.Log($"[KeyboardLayout] Initialized with {allKeys.Count} keys (Layout: {layout})");
        }

        private void GenerateKeyboard()
        {
            // Clear existing keys
            foreach (var key in allKeys)
            {
                if (key != null && key.gameObject != null)
                {
                    DestroyImmediate(key.gameObject);
                }
            }
            allKeys.Clear();
            keysByMidiNote.Clear();

            // Determine range
            int rangeStart, rangeEnd;
            if (layout == KeyboardLayoutType.Standard88Keys)
            {
                rangeStart = STANDARD_START;
                rangeEnd = STANDARD_END;
            }
            else
            {
                rangeStart = Mathf.Clamp(startMidiNote, 0, 127);
                rangeEnd = Mathf.Clamp(endMidiNote, rangeStart, 127);
            }

            float scaledSpacing = whiteKeySpacing * keyboardScale;
            float scaledPressDepth = keyPressDepth * keyboardScale;

            // First pass: count white keys and create them
            int whiteKeyCount = 0;
            List<(int midiNote, int whiteKeyPosition)> blackKeysToCreate = new List<(int, int)>();

            for (int midiNote = rangeStart; midiNote <= rangeEnd; midiNote++)
            {
                int noteInOctave = midiNote % 12;
                int octave = (midiNote / 12) - 1; // MIDI octave convention

                if (!isBlackKey[noteInOctave])
                {
                    // White key
                    float xPos = whiteKeyCount * scaledSpacing;
                    Note note = MidiNoteToNote(noteInOctave);
                    int meshIndex = whiteKeyIndex[noteInOctave];

                    CreateKey(note, octave, midiNote, false, meshIndex, new Vector3(xPos, 0, 0), scaledPressDepth);
                    whiteKeyCount++;
                }
                else
                {
                    // Remember black key position (relative to current white key count)
                    blackKeysToCreate.Add((midiNote, whiteKeyCount));
                }
            }

            // Second pass: create black keys
            foreach (var (midiNote, whiteKeyPos) in blackKeysToCreate)
            {
                int noteInOctave = midiNote % 12;
                int octave = (midiNote / 12) - 1;

                // Black key position: slightly before the next white key
                float xPos = (whiteKeyPos - 1 + blackKeyRelativeOffset[noteInOctave] + blackKeyXOffset) * scaledSpacing;
                Vector3 position = new Vector3(xPos, blackKeyYOffset * keyboardScale, blackKeyZOffset * keyboardScale);

                Note note = MidiNoteToNote(noteInOctave);
                int meshIndex = blackKeyIndex[noteInOctave];

                CreateKey(note, octave, midiNote, true, meshIndex, position, scaledPressDepth);
            }
        }

        private Note MidiNoteToNote(int noteInOctave)
        {
            return noteInOctave switch
            {
                0 => Note.C,
                1 => Note.CSharp,
                2 => Note.D,
                3 => Note.DSharp,
                4 => Note.E,
                5 => Note.F,
                6 => Note.FSharp,
                7 => Note.G,
                8 => Note.GSharp,
                9 => Note.A,
                10 => Note.ASharp,
                11 => Note.B,
                _ => Note.C
            };
        }

        private KeyboardKey CreateKey(Note note, int octave, int midiNote, bool isBlack, int meshIndex, Vector3 position, float pressDepth)
        {
            GameObject keyObj;

            if (usePrefabMeshes && meshIndex >= 0)
            {
                keyObj = CreatePrefabKey(isBlack, meshIndex);
            }
            else
            {
                keyObj = CreateDynamicKey(isBlack);
            }

            if (keyObj == null)
            {
                Debug.LogError($"[KeyboardLayout] Failed to create key for {note}{octave} (MIDI {midiNote})");
                return null;
            }

            keyObj.name = $"Key_{note}{octave}_M{midiNote}";
            keyObj.transform.SetParent(transform);
            keyObj.transform.localPosition = position;
            keyObj.transform.localScale = Vector3.one * keyboardScale;
            keyObj.transform.localRotation = Quaternion.identity;

            // Set material
            MeshRenderer renderer = keyObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = isBlack ? blackKeyMaterial : whiteKeyMaterial;
                if (mat != null) renderer.sharedMaterial = mat;
            }

            // Add required components for interaction
            EnsureComponents(keyObj, isBlack);

            // Add KeyboardKey component
            KeyboardKey key = keyObj.AddComponent<KeyboardKey>();
            key.Initialize(note, octave, isBlack, pressDepth, this);
            key.SetHighlightMaterial(highlightMaterial);
            key.SetPressedMaterial(isBlack ? blackKeyPressedMaterial : whiteKeyPressedMaterial);

            allKeys.Add(key);
            keysByMidiNote[midiNote] = key;

            return key;
        }

        private GameObject CreatePrefabKey(bool isBlack, int keyIndex)
        {
            Mesh[] meshArray = isBlack ? blackKeyMeshes : whiteKeyMeshes;

            if (meshArray == null || keyIndex >= meshArray.Length || meshArray[keyIndex] == null)
            {
                Debug.LogWarning($"[KeyboardLayout] Missing mesh for {(isBlack ? "black" : "white")} key {keyIndex}, falling back to cube");
                return CreateDynamicKey(isBlack);
            }

            GameObject keyObj = new GameObject();
            MeshFilter mf = keyObj.AddComponent<MeshFilter>();
            MeshRenderer mr = keyObj.AddComponent<MeshRenderer>();

            mf.sharedMesh = meshArray[keyIndex];

            return keyObj;
        }

        private GameObject CreateDynamicKey(bool isBlack)
        {
            GameObject keyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Scale cube to approximate key size
            if (isBlack)
            {
                keyObj.transform.localScale = new Vector3(0.013f, 0.015f, 0.09f);
            }
            else
            {
                keyObj.transform.localScale = new Vector3(0.023f, 0.02f, 0.15f);
            }

            return keyObj;
        }

        private void EnsureComponents(GameObject keyObj, bool isBlack)
        {
            // Add BoxCollider if not present
            BoxCollider collider = keyObj.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = keyObj.AddComponent<BoxCollider>();
                collider.isTrigger = false;
            }

            // Scale black key collider for easier interaction
            if (isBlack)
            {
                collider.size = Vector3.Scale(collider.size, blackKeyColliderScale);
            }

            // Add Rigidbody if not present
            if (keyObj.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = keyObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        /// <summary>
        /// Called by KeyboardKey when pressed by user input.
        /// </summary>
        internal void NotifyKeyPressed(int midiNote, float velocity)
        {
            OnKeyPressed?.Invoke(midiNote, velocity);
        }

        /// <summary>
        /// Called by KeyboardKey when released by user input.
        /// </summary>
        internal void NotifyKeyReleased(int midiNote)
        {
            OnKeyReleased?.Invoke(midiNote);
        }

        /// <summary>
        /// Get key by MIDI note.
        /// </summary>
        public KeyboardKey GetKey(int midiNote)
        {
            keysByMidiNote.TryGetValue(midiNote, out KeyboardKey key);
            return key;
        }

        /// <summary>
        /// Get the world position of a key by MIDI note.
        /// Returns the keyboard's position if key not found.
        /// </summary>
        public Vector3 GetKeyPosition(int midiNote)
        {
            if (keysByMidiNote.TryGetValue(midiNote, out KeyboardKey key) && key != null)
            {
                return key.transform.position;
            }
            return transform.position;
        }

        /// <summary>
        /// Set visual state of a specific key (for scheduled playback).
        /// </summary>
        public void SetKeyVisualState(int midiNote, bool pressed)
        {
            if (keysByMidiNote.TryGetValue(midiNote, out KeyboardKey key))
            {
                key.SetVisualState(pressed);
            }
        }

        /// <summary>
        /// Reset all key visuals to default state.
        /// </summary>
        public void ResetAllKeyVisuals()
        {
            foreach (var key in allKeys)
            {
                key?.SetVisualState(false);
            }
        }

        /// <summary>
        /// Get total number of keys.
        /// </summary>
        public int KeyCount => allKeys.Count;

        /// <summary>
        /// Get all keys.
        /// </summary>
        public IReadOnlyList<KeyboardKey> AllKeys => allKeys.AsReadOnly();

#if UNITY_EDITOR
        [ContextMenu("Regenerate Keyboard")]
        private void EditorRegenerate()
        {
            // Clear existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            allKeys.Clear();
            keysByMidiNote.Clear();

            GenerateKeyboard();
            Debug.Log($"[KeyboardLayout] Regenerated {allKeys.Count} keys");
        }

        [ContextMenu("Set 88-Key Standard")]
        private void EditorSet88Keys()
        {
            layout = KeyboardLayoutType.Standard88Keys;
            EditorRegenerate();
        }

        [ContextMenu("Set 61-Key (C2-C7)")]
        private void EditorSet61Keys()
        {
            layout = KeyboardLayoutType.Custom;
            startMidiNote = 36;  // C2
            endMidiNote = 96;    // C7
            EditorRegenerate();
        }

        [ContextMenu("Set 49-Key (C3-C7)")]
        private void EditorSet49Keys()
        {
            layout = KeyboardLayoutType.Custom;
            startMidiNote = 48;  // C3
            endMidiNote = 96;    // C7
            EditorRegenerate();
        }

        [ContextMenu("Set 25-Key (C3-C5)")]
        private void EditorSet25Keys()
        {
            layout = KeyboardLayoutType.Custom;
            startMidiNote = 48;  // C3
            endMidiNote = 72;    // C5
            EditorRegenerate();
        }
#endif
    }
}
