using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Linq;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Automatically generates songlist.json in StreamingAssets/Songs before build.
    /// This allows Android/Quest to know which MIDI files are available.
    /// </summary>
    public class SongListBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            GenerateSongList();
        }

        [MenuItem("Tools/SoloBandStudio/Generate Song List")]
        public static void GenerateSongList()
        {
            string songsPath = Path.Combine(Application.streamingAssetsPath, "Songs");

            // Create folder if needed
            if (!Directory.Exists(songsPath))
            {
                Directory.CreateDirectory(songsPath);
            }

            // Find all .mid files
            string[] midFiles = Directory.GetFiles(songsPath, "*.mid");
            string[] songNames = midFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToArray();

            // Create JSON
            var songList = new SongListData { songs = songNames };
            string json = JsonUtility.ToJson(songList, true);

            // Write to file
            string listPath = Path.Combine(songsPath, "songlist.json");
            File.WriteAllText(listPath, json);

            Debug.Log($"[SongListBuilder] Generated songlist.json with {songNames.Length} songs");

            // Refresh AssetDatabase
            AssetDatabase.Refresh();
        }

        [System.Serializable]
        private class SongListData
        {
            public string[] songs;
        }
    }
}
