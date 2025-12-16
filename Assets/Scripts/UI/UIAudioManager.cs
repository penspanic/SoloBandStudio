using UnityEngine;

namespace SoloBandStudio.UI
{
    /// <summary>
    /// Singleton audio manager for UI sounds.
    /// Shared across all UI components.
    /// </summary>
    public class UIAudioManager : MonoBehaviour
    {
        public static UIAudioManager Instance { get; private set; }

        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;

        [Header("Common UI Sounds")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip buttonHover;
        [SerializeField] private AudioClip panelOpen;
        [SerializeField] private AudioClip panelClose;
        [SerializeField] private AudioClip tabSwitch;
        [SerializeField] private AudioClip sliderChange;
        [SerializeField] private AudioClip error;
        [SerializeField] private AudioClip success;

        [Header("Settings")]
        [SerializeField] private float defaultVolume = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Play(AudioClip clip, float volume = -1f)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volume < 0 ? defaultVolume : volume);
        }

        public void PlayButtonClick() => Play(buttonClick);
        public void PlayButtonHover() => Play(buttonHover);
        public void PlayPanelOpen() => Play(panelOpen);
        public void PlayPanelClose() => Play(panelClose);
        public void PlayTabSwitch() => Play(tabSwitch);
        public void PlaySliderChange() => Play(sliderChange);
        public void PlayError() => Play(error);
        public void PlaySuccess() => Play(success);

        /// <summary>
        /// Ensure instance exists (creates one if needed).
        /// </summary>
        public static UIAudioManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<UIAudioManager>();
            if (existing != null) return existing;

            var go = new GameObject("UIAudioManager");
            return go.AddComponent<UIAudioManager>();
        }
    }
}
