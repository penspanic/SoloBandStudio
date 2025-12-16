using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Manages ambient environmental sounds.
    /// Plays base loops (wind) and random events (birds, water).
    /// Supports music ducking when LoopStation is playing.
    /// </summary>
    public class AmbientManager : MonoBehaviour
    {
        [Header("Base Loop")]
        [SerializeField] private AudioClip windLoop;
        [SerializeField] private float baseVolume = 0.3f;
        [SerializeField] private bool playOnStart = true;

        [Header("Random Events")]
        [SerializeField] private AudioClip[] birdSounds;
        [SerializeField] private AudioClip[] waterSounds;
        [SerializeField] private float eventVolume = 0.4f;
        [SerializeField] private float minEventInterval = 5f;
        [SerializeField] private float maxEventInterval = 15f;
        [SerializeField] private float birdChance = 0.7f; // vs water

        [Header("Music Ducking")]
        [SerializeField] private LoopStation loopStation;
        [SerializeField] private float duckingVolume = 0.1f;
        [SerializeField] private float duckingFadeTime = 1f;

        [Header("Spatial Settings")]
        [SerializeField] private bool useSpatialAudio = true;
        [SerializeField] private float minSpawnDistance = 5f;
        [SerializeField] private float maxSpawnDistance = 20f;
        [SerializeField] private float spawnHeight = 3f;

        // Audio sources
        private AudioSource baseLoopSource;
        private List<Transform> eventEmitters = new List<Transform>();
        private List<AudioSource> eventSources = new List<AudioSource>();
        private const int MaxEventSources = 3;

        // State
        private bool isPlaying;
        
        private bool isDucked;
        private float currentVolume;
        private float targetVolume;
        private Coroutine eventCoroutine;

        public bool IsPlaying => isPlaying;
        public bool IsDucked => isDucked;

        private void Awake()
        {
            // Create base loop audio source (2D, on this object)
            baseLoopSource = gameObject.AddComponent<AudioSource>();
            baseLoopSource.loop = true;
            baseLoopSource.playOnAwake = false;
            baseLoopSource.spatialBlend = 0f; // 2D for base ambient
            baseLoopSource.volume = 0f;

            // Create child objects for spatial event audio sources
            for (int i = 0; i < MaxEventSources; i++)
            {
                var emitterObj = new GameObject($"EventEmitter_{i}");
                emitterObj.transform.SetParent(transform);
                emitterObj.transform.localPosition = Vector3.zero;

                var source = emitterObj.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = useSpatialAudio ? 1f : 0f;
                source.minDistance = 1f;
                source.maxDistance = 400f;
                source.rolloffMode = AudioRolloffMode.Linear;

                eventEmitters.Add(emitterObj.transform);
                eventSources.Add(source);
            }

            currentVolume = baseVolume;
            targetVolume = baseVolume;
        }

        private void Start()
        {
            // Find LoopStation if not assigned
            if (loopStation == null)
            {
                loopStation = FindFirstObjectByType<LoopStation>();
            }

            // Subscribe to LoopStation events
            if (loopStation != null)
            {
                loopStation.OnPlayStarted += HandleMusicStarted;
                loopStation.OnPlayStopped += HandleMusicStopped;
            }

            if (playOnStart)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (loopStation != null)
            {
                loopStation.OnPlayStarted -= HandleMusicStarted;
                loopStation.OnPlayStopped -= HandleMusicStopped;
            }
        }

        private void Update()
        {
            // Smooth volume transitions
            if (!Mathf.Approximately(currentVolume, targetVolume))
            {
                currentVolume = Mathf.MoveTowards(currentVolume, targetVolume, Time.deltaTime / duckingFadeTime * baseVolume);
                baseLoopSource.volume = currentVolume;
            }
        }

        #region Playback Control

        /// <summary>
        /// Start playing ambient sounds.
        /// </summary>
        public void Play()
        {
            if (isPlaying) return;

            isPlaying = true;

            // Start base loop
            if (windLoop != null)
            {
                baseLoopSource.clip = windLoop;
                baseLoopSource.volume = currentVolume;
                baseLoopSource.Play();
            }

            // Start random events
            eventCoroutine = StartCoroutine(RandomEventLoop());

            Debug.Log("[AmbientManager] Started");
        }

        /// <summary>
        /// Stop all ambient sounds.
        /// </summary>
        public void Stop()
        {
            if (!isPlaying) return;

            isPlaying = false;

            // Stop base loop
            baseLoopSource.Stop();

            // Stop random events
            if (eventCoroutine != null)
            {
                StopCoroutine(eventCoroutine);
                eventCoroutine = null;
            }

            // Stop all event sources
            foreach (var source in eventSources)
            {
                source.Stop();
            }

            Debug.Log("[AmbientManager] Stopped");
        }

        /// <summary>
        /// Toggle ambient sounds on/off.
        /// </summary>
        public void Toggle()
        {
            if (isPlaying) Stop();
            else Play();
        }

        #endregion

        #region Music Ducking

        private void HandleMusicStarted()
        {
            Duck();
        }

        private void HandleMusicStopped()
        {
            Unduck();
        }

        /// <summary>
        /// Duck the ambient volume (when music is playing).
        /// </summary>
        public void Duck()
        {
            if (isDucked) return;

            isDucked = true;
            targetVolume = duckingVolume;

            Debug.Log("[AmbientManager] Ducking ambient sounds");
        }

        /// <summary>
        /// Restore ambient volume to normal.
        /// </summary>
        public void Unduck()
        {
            if (!isDucked) return;

            isDucked = false;
            targetVolume = baseVolume;

            Debug.Log("[AmbientManager] Restoring ambient sounds");
        }

        #endregion

        #region Random Events

        private IEnumerator RandomEventLoop()
        {
            while (isPlaying)
            {
                // Wait random interval
                float interval = UnityEngine.Random.Range(minEventInterval, maxEventInterval);
                yield return new WaitForSeconds(interval);

                if (!isPlaying) break;

                // Play random event
                PlayRandomEvent();
            }
        }

        private void PlayRandomEvent()
        {
            // Find available source and its emitter
            int availableIndex = -1;
            for (int i = 0; i < eventSources.Count; i++)
            {
                if (!eventSources[i].isPlaying)
                {
                    availableIndex = i;
                    break;
                }
            }

            if (availableIndex < 0) return; // All sources busy

            AudioSource source = eventSources[availableIndex];
            Transform emitter = eventEmitters[availableIndex];

            // Choose event type
            AudioClip clip = null;
            bool isBird = UnityEngine.Random.value < birdChance;

            if (isBird && birdSounds != null && birdSounds.Length > 0)
            {
                clip = birdSounds[UnityEngine.Random.Range(0, birdSounds.Length)];
            }
            else if (waterSounds != null && waterSounds.Length > 0)
            {
                clip = waterSounds[UnityEngine.Random.Range(0, waterSounds.Length)];
            }

            if (clip == null) return;

            // Set emitter position (random around listener)
            if (useSpatialAudio)
            {
                Vector3 listenerPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);

                Vector3 worldPos = new Vector3(
                    listenerPos.x + Mathf.Cos(angle) * distance,
                    spawnHeight,
                    listenerPos.z + Mathf.Sin(angle) * distance
                );

                // Move the child emitter, not the AmbientManager itself
                emitter.position = worldPos;
            }

            // Apply ducking to events too
            float volume = isDucked ? eventVolume * (duckingVolume / baseVolume) : eventVolume;

            // Play
            source.clip = clip;
            source.volume = volume;
            source.pitch = UnityEngine.Random.Range(0.9f, 1.1f); // Slight variation
            source.Play();
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set base volume.
        /// </summary>
        public void SetBaseVolume(float volume)
        {
            baseVolume = Mathf.Clamp01(volume);
            if (!isDucked)
            {
                targetVolume = baseVolume;
            }
        }

        /// <summary>
        /// Set event interval range.
        /// </summary>
        public void SetEventInterval(float min, float max)
        {
            minEventInterval = Mathf.Max(0.5f, min);
            maxEventInterval = Mathf.Max(minEventInterval, max);
        }

        /// <summary>
        /// Set ducking parameters.
        /// </summary>
        public void SetDuckingParams(float volume, float fadeTime)
        {
            duckingVolume = Mathf.Clamp01(volume);
            duckingFadeTime = Mathf.Max(0.1f, fadeTime);
        }

        #endregion

#if UNITY_EDITOR
        [ContextMenu("Play")]
        private void EditorPlay() => Play();

        [ContextMenu("Stop")]
        private void EditorStop() => Stop();

        [ContextMenu("Duck")]
        private void EditorDuck() => Duck();

        [ContextMenu("Unduck")]
        private void EditorUnduck() => Unduck();

        [ContextMenu("Play Random Event")]
        private void EditorPlayRandomEvent() => PlayRandomEvent();
#endif
    }
}
