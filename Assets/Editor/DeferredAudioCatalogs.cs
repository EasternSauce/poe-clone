using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PoeClone.Audio;

namespace PoeClone.EditorTools
{
    /// <summary>Keeps authoring references out of runtime audio catalogs.</summary>
    public sealed class DeferredAudioCatalogs : IPreprocessBuildWithReport
    {
        [Serializable] private sealed class Source { public string file, pack; }
        [Serializable] private sealed class Recording { public string file, sha256, category; public Source[] sources; }
        [Serializable] private sealed class Manifest
        {
            public int version, sourceFiles;
            public string importedAt;
            public Recording[] sounds;
        }

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Generate();

        [MenuItem("PoeClone/Audio/Generate Deferred Catalogs")]
        public static void Generate()
        {
            var original = AssetDatabase.LoadAssetAtPath<SoundBoardSettings>("Assets/Resources/SoundBoardSettings.asset");
            var ambient = AssetDatabase.LoadAssetAtPath<AmbientSoundLibrary>("Assets/Resources/AmbientSoundLibrary.asset");
            if (original == null || ambient == null) throw new InvalidOperationException("Audio authoring libraries are missing.");
            var board = UnityEngine.Object.Instantiate(original);
            foreach (var group in board.soundGroups)
            {
                group.clipPaths = group.clips.Where(c => c != null).Select(ResourcePath).ToArray();
                group.clips = Array.Empty<AudioClip>();
            }
            foreach (var effect in board.effects)
            {
                if (string.IsNullOrEmpty(effect.soundGroupId) && (effect.selected != null || effect.defaults.Length > 0))
                {
                    string id = "deferred." + effect.id;
                    board.soundGroups.Add(new SoundBoardSettings.SoundGroup { id = id,
                        clipPaths = (effect.selected != null ? new[] { effect.selected } : effect.defaults).Where(c => c != null).Select(ResourcePath).ToArray() });
                    effect.soundGroupId = id;
                }
                effect.selected = null;
                effect.defaults = Array.Empty<AudioClip>();
            }
            Write("Assets/Resources/DeferredSoundBoard.json", JsonUtility.ToJson(board, true));
            UnityEngine.Object.DestroyImmediate(board);
            var entries = new List<AmbientSoundLibrary.Entry>();
            foreach (var field in typeof(AmbientSoundLibrary).GetFields())
            {
                AudioClip[] clips;
                if (field.FieldType == typeof(AudioClip)) clips = new[] { (AudioClip)field.GetValue(ambient) };
                else if (field.FieldType == typeof(AudioClip[])) clips = (AudioClip[])field.GetValue(ambient);
                else continue;
                entries.Add(new AmbientSoundLibrary.Entry { name = field.Name,
                    paths = clips.Where(c => c != null).Select(ResourcePath).ToArray() });
            }
            Write("Assets/Resources/DeferredAmbience.json", JsonUtility.ToJson(new AmbientSoundLibrary.Catalog { entries = entries.ToArray() }, true));
            // Keep authoring and runtime catalogs on the same permanent recordings.
            foreach (var group in original.soundGroups)
                group.clips = Array.ConvertAll(group.clips, PermanentClip);
            foreach (var effect in original.effects)
            {
                effect.selected = PermanentClip(effect.selected);
                effect.defaults = Array.ConvertAll(effect.defaults, PermanentClip);
            }
            foreach (var field in typeof(AmbientSoundLibrary).GetFields())
            {
                if (field.FieldType == typeof(AudioClip)) field.SetValue(ambient, PermanentClip((AudioClip)field.GetValue(ambient)));
                else if (field.FieldType == typeof(AudioClip[])) field.SetValue(ambient, Array.ConvertAll((AudioClip[])field.GetValue(ambient), PermanentClip));
            }
            EditorUtility.SetDirty(original);
            EditorUtility.SetDirty(ambient);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AudioClip PermanentClip(AudioClip clip) => clip != null ? Resources.Load<AudioClip>(ResourcePath(clip)) : null;

        private static void Write(string path, string contents)
        {
            if (!File.Exists(path) || File.ReadAllText(path) != contents) File.WriteAllText(path, contents);
        }

        private static string ResourcePath(AudioClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            int marker = path.LastIndexOf("/Resources/", StringComparison.Ordinal);
            if (!path.StartsWith("Assets/Audio/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Gameplay audio must be under Assets/Audio: " + path);
            if (marker < 0)
            {
                string directory = "Assets/Audio/Resources/DeferredAudio/" + AssetDatabase.AssetPathToGUID(path);
                Directory.CreateDirectory(directory);
                string destination = directory + "/" + Path.GetFileName(path);
                string error = AssetDatabase.MoveAsset(path, destination);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException("Cannot move audio to permanent Resources: " + error);
                UpdateProvenance(path, destination);
                path = destination;
                marker = path.LastIndexOf("/Resources/", StringComparison.Ordinal);
            }
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null && (importer.defaultSampleSettings.preloadAudioData || !importer.loadInBackground))
            {
                var sample = importer.defaultSampleSettings;
                sample.preloadAudioData = false;
                importer.defaultSampleSettings = sample;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
            return Path.ChangeExtension(path.Substring(marker + "/Resources/".Length), null);
        }

        private static void UpdateProvenance(string oldPath, string newPath)
        {
            const string directory = "Assets/Audio/SoundLibrary/";
            const string manifestPath = directory + "manifest.json";
            if (!File.Exists(manifestPath)) return;
            string Relative(string path) => path.StartsWith(directory, StringComparison.Ordinal)
                ? path.Substring(directory.Length) : "../" + path.Substring("Assets/Audio/".Length);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            string previous = Relative(oldPath);
            bool changed = false;
            foreach (var recording in manifest.sounds)
                if (recording.file == previous)
                {
                    recording.file = Relative(newPath);
                    changed = true;
                }
            if (changed) Write(manifestPath, JsonUtility.ToJson(manifest, true) + "\n");
        }
    }
}
