using System.Collections.Generic;
using UnityEngine;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Caches AudioClip data as float arrays for direct buffer manipulation.
    /// Thread-safe for reading cached data from audio thread.
    /// </summary>
    public class SampleDataCache
    {
        private static SampleDataCache instance;
        public static SampleDataCache Instance => instance ??= new SampleDataCache();

        private readonly Dictionary<int, CachedSample> cache = new Dictionary<int, CachedSample>();
        private readonly object cacheLock = new object();

        public struct CachedSample
        {
            public float[] Data;
            public int Channels;
            public int SampleRate;
            public int SampleCount; // Samples per channel
        }

        /// <summary>
        /// Preload an AudioClip into the cache.
        /// Must be called from main thread.
        /// </summary>
        public void Preload(AudioClip clip)
        {
            if (clip == null) return;

            int clipId = clip.GetInstanceID();

            lock (cacheLock)
            {
                if (cache.ContainsKey(clipId)) return;
            }

            // Extract sample data (main thread only)
            int totalSamples = clip.samples * clip.channels;
            float[] data = new float[totalSamples];
            clip.GetData(data, 0);

            var cached = new CachedSample
            {
                Data = data,
                Channels = clip.channels,
                SampleRate = clip.frequency,
                SampleCount = clip.samples
            };

            lock (cacheLock)
            {
                cache[clipId] = cached;
            }
        }

        /// <summary>
        /// Preload multiple AudioClips.
        /// </summary>
        public void PreloadAll(IEnumerable<AudioClip> clips)
        {
            foreach (var clip in clips)
            {
                Preload(clip);
            }
        }

        /// <summary>
        /// Get cached sample data. Thread-safe.
        /// </summary>
        public bool TryGetSample(AudioClip clip, out CachedSample sample)
        {
            if (clip == null)
            {
                sample = default;
                return false;
            }

            int clipId = clip.GetInstanceID();
            lock (cacheLock)
            {
                return cache.TryGetValue(clipId, out sample);
            }
        }

        /// <summary>
        /// Get cached sample data by clip ID. Thread-safe.
        /// </summary>
        public bool TryGetSample(int clipId, out CachedSample sample)
        {
            lock (cacheLock)
            {
                return cache.TryGetValue(clipId, out sample);
            }
        }

        /// <summary>
        /// Check if a clip is cached.
        /// </summary>
        public bool IsCached(AudioClip clip)
        {
            if (clip == null) return false;
            lock (cacheLock)
            {
                return cache.ContainsKey(clip.GetInstanceID());
            }
        }

        /// <summary>
        /// Clear all cached data.
        /// </summary>
        public void Clear()
        {
            lock (cacheLock)
            {
                cache.Clear();
            }
            Debug.Log("[SampleDataCache] Cleared");
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        public (int clipCount, long totalBytes) GetStats()
        {
            lock (cacheLock)
            {
                long bytes = 0;
                foreach (var kvp in cache)
                {
                    bytes += kvp.Value.Data.Length * sizeof(float);
                }
                return (cache.Count, bytes);
            }
        }
    }
}
