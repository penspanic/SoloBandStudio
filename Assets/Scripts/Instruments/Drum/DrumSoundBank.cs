using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Holds audio samples for each drum part.
    /// </summary>
    [CreateAssetMenu(fileName = "DrumSoundBank", menuName = "SoloBandStudio/Drum Sound Bank")]
    public class DrumSoundBank : ScriptableObject
    {
        [Serializable]
        public class DrumSound
        {
            public DrumPartType partType;
            public AudioClip[] samples;  // Multiple samples for variation
        }

        [SerializeField] private DrumSound[] sounds;

        private Dictionary<DrumPartType, AudioClip[]> soundMap;

        private void OnEnable()
        {
            BuildSoundMap();
        }

        private void BuildSoundMap()
        {
            soundMap = new Dictionary<DrumPartType, AudioClip[]>();
            if (sounds == null) return;

            foreach (var sound in sounds)
            {
                if (sound.samples != null && sound.samples.Length > 0)
                {
                    soundMap[sound.partType] = sound.samples;
                }
            }
        }

        /// <summary>
        /// Get a random sample for the specified drum part.
        /// </summary>
        public AudioClip GetSample(DrumPartType partType)
        {
            if (soundMap == null) BuildSoundMap();

            if (soundMap.TryGetValue(partType, out var samples) && samples.Length > 0)
            {
                return samples[UnityEngine.Random.Range(0, samples.Length)];
            }
            return null;
        }

        /// <summary>
        /// Get all samples for the specified drum part.
        /// </summary>
        public AudioClip[] GetSamples(DrumPartType partType)
        {
            if (soundMap == null) BuildSoundMap();

            if (soundMap.TryGetValue(partType, out var samples))
            {
                return samples;
            }
            return Array.Empty<AudioClip>();
        }

        /// <summary>
        /// Check if samples exist for a part type.
        /// </summary>
        public bool HasSamples(DrumPartType partType)
        {
            if (soundMap == null) BuildSoundMap();
            return soundMap.ContainsKey(partType);
        }
    }
}
