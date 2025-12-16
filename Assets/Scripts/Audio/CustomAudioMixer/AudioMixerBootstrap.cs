using UnityEngine;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Bootstrap component that ensures CustomAudioMixer is initialized before other components.
    /// Place this on a GameObject with very early execution order (e.g., -100).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioMixerBootstrap : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool createIfNotExists = true;
        [SerializeField] private float masterVolume = 0.7f;
        [SerializeField] private int maxVoices = 64;

        private void Awake()
        {
            // Check if mixer already exists
            if (CustomAudioMixer.Instance != null)
            {
                Debug.Log("[AudioMixerBootstrap] CustomAudioMixer already exists");
                return;
            }

            if (!createIfNotExists)
            {
                Debug.LogWarning("[AudioMixerBootstrap] CustomAudioMixer not found and createIfNotExists is false");
                return;
            }

            // Create the mixer
            CreateMixer();
        }

        private void CreateMixer()
        {
            // Create mixer GameObject
            GameObject mixerObj = new GameObject("CustomAudioMixer");

            // Add AudioSource (required for OnAudioFilterRead)
            var audioSource = mixerObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio for mixer output
            audioSource.volume = 1f;
            audioSource.loop = true;

            // We need to play silence to get OnAudioFilterRead called
            // Create a silent clip
            int sampleRate = AudioSettings.outputSampleRate;
            int channels = 2;
            int samples = sampleRate; // 1 second of silence
            AudioClip silentClip = AudioClip.Create("SilentMixer", samples, channels, sampleRate, false);
            float[] silence = new float[samples * channels];
            silentClip.SetData(silence, 0);

            audioSource.clip = silentClip;
            audioSource.Play();

            // Add the CustomAudioMixer component
            var mixer = mixerObj.AddComponent<CustomAudioMixer>();
            mixer.SetMasterVolume(masterVolume);

            Debug.Log($"[AudioMixerBootstrap] Created CustomAudioMixer (volume={masterVolume}, maxVoices={maxVoices})");
        }

#if UNITY_EDITOR
        [ContextMenu("Create Mixer Now")]
        private void EditorCreateMixer()
        {
            if (CustomAudioMixer.Instance != null)
            {
                Debug.Log("CustomAudioMixer already exists");
                return;
            }
            CreateMixer();
        }

        [ContextMenu("Log Cache Stats")]
        private void LogCacheStats()
        {
            var (clipCount, bytes) = SampleDataCache.Instance.GetStats();
            Debug.Log($"[SampleDataCache] {clipCount} clips, {bytes / 1024}KB");
        }
#endif
    }
}
