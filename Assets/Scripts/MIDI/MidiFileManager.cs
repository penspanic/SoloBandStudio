using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SoloBandStudio.Audio;

namespace SoloBandStudio.MIDI
{
    /// <summary>
    /// Manages saving and loading MIDI files from local storage.
    /// </summary>
    public class MidiFileManager
    {
        private static MidiFileManager instance;
        public static MidiFileManager Instance => instance ??= new MidiFileManager();

        private string savePath;

        /// <summary>
        /// Directory where MIDI files are stored.
        /// </summary>
        public string SavePath
        {
            get
            {
                if (string.IsNullOrEmpty(savePath))
                {
                    InitializeSavePath();
                }
                return savePath;
            }
        }

        private MidiFileManager()
        {
            InitializeSavePath();
        }

        private void InitializeSavePath()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // Desktop: Documents/SoloBandStudio
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            savePath = Path.Combine(documents, "SoloBandStudio");
#else
            // Mobile/Quest: Application.persistentDataPath
            savePath = Path.Combine(Application.persistentDataPath, "Songs");
#endif
            // Ensure directory exists
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
                Debug.Log($"[MidiFileManager] Created save directory: {savePath}");
            }
        }

        #region File Operations

        /// <summary>
        /// Get list of all saved MIDI files.
        /// </summary>
        public string[] GetSavedFiles()
        {
            if (!Directory.Exists(SavePath))
            {
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(SavePath, "*.mid");

            // Return just filenames without path
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            }

            Array.Sort(files);
            return files;
        }

        /// <summary>
        /// Check if a file exists.
        /// </summary>
        public bool FileExists(string filename)
        {
            string fullPath = GetFullPath(filename);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Get full path for a filename.
        /// </summary>
        public string GetFullPath(string filename)
        {
            if (!filename.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".mid";
            }
            return Path.Combine(SavePath, filename);
        }

        /// <summary>
        /// Delete a MIDI file.
        /// </summary>
        public bool Delete(string filename)
        {
            string fullPath = GetFullPath(filename);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Debug.Log($"[MidiFileManager] Deleted: {filename}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rename a MIDI file.
        /// </summary>
        public bool Rename(string oldName, string newName)
        {
            string oldPath = GetFullPath(oldName);
            string newPath = GetFullPath(newName);

            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                File.Move(oldPath, newPath);
                Debug.Log($"[MidiFileManager] Renamed: {oldName} -> {newName}");
                return true;
            }

            return false;
        }

        #endregion

        #region Save/Load MidiFile

        /// <summary>
        /// Save MidiFile to disk.
        /// </summary>
        public void Save(string filename, MidiFile midi)
        {
            string fullPath = GetFullPath(filename);
            MidiWriter.WriteFile(midi, fullPath);
            Debug.Log($"[MidiFileManager] Saved: {fullPath}");
        }

        /// <summary>
        /// Load MidiFile from disk.
        /// </summary>
        public MidiFile Load(string filename)
        {
            string fullPath = GetFullPath(filename);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MidiFileManager] File not found: {fullPath}");
                return null;
            }

            try
            {
                var midi = MidiParser.ParseFile(fullPath);
                Debug.Log($"[MidiFileManager] Loaded: {filename} ({midi.Tracks.Count} tracks)");
                return midi;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Failed to load {filename}: {e.Message}");
                return null;
            }
        }

        #endregion

        #region High-Level Save/Load (with LoopTrackData conversion)

        /// <summary>
        /// Save tracks as MIDI file.
        /// </summary>
        public void SaveTracks(
            string filename,
            List<LoopTrackData> tracks,
            float bpm,
            int beatsPerBar,
            string songName = null)
        {
            if (string.IsNullOrEmpty(songName))
            {
                songName = filename;
            }

            var midi = MidiConverter.FromLoopTracks(tracks, bpm, beatsPerBar, songName);
            Save(filename, midi);
        }

        /// <summary>
        /// Load MIDI file and convert to tracks with structured metadata.
        /// </summary>
        public (List<LoopTrackData> tracks, SongMetadata metadata) LoadTracksWithMetadata(string filename)
        {
            var midi = Load(filename);

            if (midi == null)
            {
                return (null, SongMetadata.Default);
            }

            var tracks = MidiConverter.ToLoopTracks(midi);
            var metadata = MidiConverter.GetSongMetadata(midi);

            return (tracks, metadata);
        }

        /// <summary>
        /// Load MIDI file and convert to tracks.
        /// Returns tracks and metadata.
        /// </summary>
        [Obsolete("Use LoadTracksWithMetadata() for better type safety")]
        public (List<LoopTrackData> tracks, float bpm, int beatsPerBar, int totalBeats, string name) LoadTracks(string filename)
        {
            var (tracks, metadata) = LoadTracksWithMetadata(filename);
            return (tracks, metadata.BPM, metadata.BeatsPerBar, metadata.TotalBeats, metadata.Name);
        }

        /// <summary>
        /// Load MIDI from external path with structured metadata.
        /// </summary>
        public (List<LoopTrackData> tracks, SongMetadata metadata) LoadFromPathWithMetadata(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MidiFileManager] External file not found: {fullPath}");
                return (null, SongMetadata.Default);
            }

            try
            {
                var midi = MidiParser.ParseFile(fullPath);
                var tracks = MidiConverter.ToLoopTracks(midi);
                var metadata = MidiConverter.GetSongMetadata(midi);

                Debug.Log($"[MidiFileManager] Loaded external: {fullPath} - {metadata}");
                return (tracks, metadata);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Failed to load external file: {e.Message}");
                return (null, SongMetadata.Default);
            }
        }

        /// <summary>
        /// Load MIDI from external path (e.g., imported file).
        /// </summary>
        [Obsolete("Use LoadFromPathWithMetadata() for better type safety")]
        public (List<LoopTrackData> tracks, float bpm, int beatsPerBar, int totalBeats, string name) LoadFromPath(string fullPath)
        {
            var (tracks, metadata) = LoadFromPathWithMetadata(fullPath);
            return (tracks, metadata.BPM, metadata.BeatsPerBar, metadata.TotalBeats, metadata.Name);
        }

        #endregion

        #region Import from Resources (for bundled presets)

        /// <summary>
        /// Load MIDI from Resources folder (TextAsset).
        /// </summary>
        public (List<LoopTrackData> tracks, float bpm, int beatsPerBar, int totalBeats, string name) LoadFromResources(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);

            if (textAsset == null)
            {
                Debug.LogError($"[MidiFileManager] Resource not found: {resourcePath}");
                return (null, 120f, 4, 16, "");
            }

            try
            {
                var midi = MidiParser.Parse(textAsset.bytes);
                var tracks = MidiConverter.ToLoopTracks(midi);
                var metadata = MidiConverter.GetSongMetadata(midi);

                Debug.Log($"[MidiFileManager] Loaded from resources: {resourcePath}");
                return (tracks, metadata.BPM, metadata.BeatsPerBar, metadata.TotalBeats, midi.Name);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Failed to load resource: {e.Message}");
                return (null, 120f, 4, 16, "");
            }
        }

        #endregion

        #region StreamingAssets Support

        /// <summary>
        /// Path to StreamingAssets/Songs folder.
        /// </summary>
        public string StreamingAssetsPath => Path.Combine(Application.streamingAssetsPath, "Songs");

        // Cached song list for Android (since we can't enumerate files)
        private string[] cachedStreamingAssetsSongs;
        private bool songListLoaded = false;

        [Serializable]
        private class SongListData
        {
            public string[] songs;
        }

        /// <summary>
        /// Set the list of available songs in StreamingAssets.
        /// Required for Android/Quest since we can't enumerate files in APK.
        /// Call this at startup with the known list of songs.
        /// </summary>
        public void SetStreamingAssetsSongList(string[] songNames)
        {
            cachedStreamingAssetsSongs = songNames;
            songListLoaded = true;
            Debug.Log($"[MidiFileManager] Set StreamingAssets song list: {songNames.Length} songs");
        }

        /// <summary>
        /// Load song list from songlist.json (auto-generated at build time).
        /// </summary>
        public async System.Threading.Tasks.Task LoadSongListAsync()
        {
            if (songListLoaded) return;

            string listPath = Path.Combine(StreamingAssetsPath, "songlist.json");

            try
            {
                using (var request = UnityEngine.Networking.UnityWebRequest.Get(listPath))
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await System.Threading.Tasks.Task.Yield();
                    }

                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        string json = request.downloadHandler.text;
                        var data = JsonUtility.FromJson<SongListData>(json);
                        cachedStreamingAssetsSongs = data.songs ?? Array.Empty<string>();
                        songListLoaded = true;
                        Debug.Log($"[MidiFileManager] Loaded song list: {cachedStreamingAssetsSongs.Length} songs");
                    }
                    else
                    {
                        Debug.LogWarning($"[MidiFileManager] No songlist.json found: {request.error}");
                        cachedStreamingAssetsSongs = Array.Empty<string>();
                        songListLoaded = true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Failed to load song list: {e.Message}");
                cachedStreamingAssetsSongs = Array.Empty<string>();
                songListLoaded = true;
            }
        }

        /// <summary>
        /// Load song list synchronously (blocking).
        /// </summary>
        public void LoadSongListSync()
        {
            if (songListLoaded) return;

            string listPath = Path.Combine(StreamingAssetsPath, "songlist.json");

#if UNITY_ANDROID && !UNITY_EDITOR
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(listPath))
            {
                request.SendWebRequest();
                while (!request.isDone) { }

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var data = JsonUtility.FromJson<SongListData>(request.downloadHandler.text);
                    cachedStreamingAssetsSongs = data.songs ?? Array.Empty<string>();
                }
                else
                {
                    cachedStreamingAssetsSongs = Array.Empty<string>();
                }
            }
#else
            if (File.Exists(listPath))
            {
                string json = File.ReadAllText(listPath);
                var data = JsonUtility.FromJson<SongListData>(json);
                cachedStreamingAssetsSongs = data.songs ?? Array.Empty<string>();
            }
            else
            {
                cachedStreamingAssetsSongs = Array.Empty<string>();
            }
#endif
            songListLoaded = true;
            Debug.Log($"[MidiFileManager] Loaded song list (sync): {cachedStreamingAssetsSongs.Length} songs");
        }

        /// <summary>
        /// Get list of MIDI files in StreamingAssets/Songs folder.
        /// On Android, loads from songlist.json (auto-generated at build).
        /// On Editor/Standalone, enumerates the folder directly.
        /// </summary>
        public string[] GetStreamingAssetsSongs()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: load from songlist.json if not loaded
            if (!songListLoaded)
            {
                LoadSongListSync();
            }
            return cachedStreamingAssetsSongs ?? Array.Empty<string>();
#else
            // Editor/Standalone: enumerate folder
            string songsPath = StreamingAssetsPath;

            if (!Directory.Exists(songsPath))
            {
#if UNITY_EDITOR
                Directory.CreateDirectory(songsPath);
                Debug.Log($"[MidiFileManager] Created StreamingAssets/Songs folder: {songsPath}");
#endif
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(songsPath, "*.mid");

            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            }

            Array.Sort(files);
            return files;
#endif
        }

        /// <summary>
        /// Load MIDI from StreamingAssets/Songs folder with structured metadata.
        /// Works on all platforms including Android/Quest.
        /// </summary>
        public (List<LoopTrackData> tracks, SongMetadata metadata) LoadFromStreamingAssetsWithMetadata(string filename)
        {
            if (!filename.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".mid";
            }

            string fullPath = Path.Combine(StreamingAssetsPath, filename);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: use UnityWebRequest (synchronous wrapper)
            return LoadFromStreamingAssetsAndroidInternal(fullPath, filename);
#else
            // Editor/Standalone: direct file access
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MidiFileManager] StreamingAssets file not found: {fullPath}");
                return (null, SongMetadata.Default);
            }

            try
            {
                var midi = MidiParser.ParseFile(fullPath);
                var tracks = MidiConverter.ToLoopTracks(midi);
                var metadata = MidiConverter.GetSongMetadata(midi);

                Debug.Log($"[MidiFileManager] Loaded from StreamingAssets: {filename} - {metadata}");
                return (tracks, metadata);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Failed to load from StreamingAssets: {e.Message}");
                return (null, SongMetadata.Default);
            }
#endif
        }

        /// <summary>
        /// Load MIDI from StreamingAssets/Songs folder.
        /// Works on all platforms including Android/Quest.
        /// </summary>
        [Obsolete("Use LoadFromStreamingAssetsWithMetadata() for better type safety")]
        public (List<LoopTrackData> tracks, float bpm, int beatsPerBar, int totalBeats, string name) LoadFromStreamingAssets(string filename)
        {
            var (tracks, metadata) = LoadFromStreamingAssetsWithMetadata(filename);
            return (tracks, metadata.BPM, metadata.BeatsPerBar, metadata.TotalBeats, metadata.Name);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private (List<LoopTrackData> tracks, SongMetadata metadata) LoadFromStreamingAssetsAndroidInternal(string fullPath, string filename)
        {
            try
            {
                using (var request = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
                {
                    request.SendWebRequest();

                    // Wait synchronously (not ideal but simple)
                    while (!request.isDone) { }

                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[MidiFileManager] Failed to load {filename}: {request.error}");
                        return (null, SongMetadata.Default);
                    }

                    byte[] data = request.downloadHandler.data;
                    var midi = MidiParser.Parse(data);
                    var tracks = MidiConverter.ToLoopTracks(midi);
                    var metadata = MidiConverter.GetSongMetadata(midi);

                    Debug.Log($"[MidiFileManager] Loaded from StreamingAssets (Android): {filename} - {metadata}");
                    return (tracks, metadata);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Android load failed: {e.Message}");
                return (null, SongMetadata.Default);
            }
        }
#endif

        /// <summary>
        /// Load MIDI from StreamingAssets asynchronously (recommended for Android).
        /// </summary>
        public async System.Threading.Tasks.Task<(List<LoopTrackData> tracks, float bpm, int beatsPerBar, int totalBeats, string name)> LoadFromStreamingAssetsAsync(string filename)
        {
            if (!filename.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".mid";
            }

            string fullPath = Path.Combine(StreamingAssetsPath, filename);

            try
            {
                using (var request = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await System.Threading.Tasks.Task.Yield();
                    }

                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[MidiFileManager] Failed to load {filename}: {request.error}");
                        return (null, 120f, 4, 16, "");
                    }

                    byte[] data = request.downloadHandler.data;
                    var midi = MidiParser.Parse(data);
                    var tracks = MidiConverter.ToLoopTracks(midi);
                    var metadata = MidiConverter.GetSongMetadata(midi);

                    Debug.Log($"[MidiFileManager] Loaded async: {filename}");
                    return (tracks, metadata.BPM, metadata.BeatsPerBar, metadata.TotalBeats, midi.Name);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiFileManager] Async load failed: {e.Message}");
                return (null, 120f, 4, 16, "");
            }
        }

        #endregion

        /// <summary>
        /// Open save folder in file explorer.
        /// </summary>
        public void OpenSaveFolder()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", SavePath.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", SavePath);
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            System.Diagnostics.Process.Start("xdg-open", SavePath);
#else
            Debug.Log($"Save folder: {SavePath}");
#endif
        }
    }
}
