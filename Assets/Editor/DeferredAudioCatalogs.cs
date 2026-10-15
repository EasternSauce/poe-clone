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
            // Point authoring libraries at the permanent recordings too, avoiding duplicate
            // original/copy dependencies in player data and retiring inspiration references.
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
            Debug.Log("Deferred audio catalogs generated: " + original.soundGroups.Count + " sound groups and " + entries.Count + " ambience fields.");
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
            // Inspiration recordings must become permanent assets before runtime references exist.
            if (marker < 0 || path.StartsWith("Assets/assets_for_inspiration/", StringComparison.OrdinalIgnoreCase))
            {
                string directory = "Assets/Audio/Resources/DeferredAudio/" + AssetDatabase.AssetPathToGUID(path);
                Directory.CreateDirectory(directory);
                string destination = directory + "/" + Path.GetFileName(path);
                if (!File.Exists(destination) && !AssetDatabase.CopyAsset(path, destination))
                    throw new InvalidOperationException("Cannot copy audio to permanent Resources: " + path);
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
    }
}
