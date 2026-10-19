using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>Gameplay audio belongs in Assets/Audio, including Resources-loaded clips.</summary>
    public sealed class AudioAssetValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Validate();

        [MenuItem("PoeClone/Audio/Validate Gameplay Audio Locations")]
        public static string Validate()
        {
            bool InAudio(string path) => path.StartsWith("Assets/Audio/", StringComparison.OrdinalIgnoreCase);
            bool RuntimeAsset(string path) => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/assets_for_inspiration/", StringComparison.OrdinalIgnoreCase) &&
                !path.Split('/').Any(part => part.Equals("Editor", StringComparison.OrdinalIgnoreCase));

            // Include prefabs and settings as well as scenes, so dynamically spawned content is checked.
            var roots = AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(RuntimeAsset).ToArray();
            var referenced = AssetDatabase.GetDependencies(roots, true)
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AudioClip));
            var resources = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Split('/').Any(part => part.Equals("Resources", StringComparison.OrdinalIgnoreCase)));
            var invalid = referenced.Concat(resources).Where(path => !InAudio(path))
                .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (invalid.Length > 0)
                throw new BuildFailedException("Move gameplay audio into Assets/Audio:\n" + string.Join("\n", invalid));
            var deferred = referenced.Concat(resources).Distinct()
                .Where(path => InAudio(path) && !path.StartsWith("Assets/Audio/Music/", StringComparison.OrdinalIgnoreCase))
                .Where(path =>
                {
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) return true;
                    bool Delayed(AudioImporterSampleSettings sample) => !sample.preloadAudioData || sample.loadType != AudioClipLoadType.DecompressOnLoad;
                    return importer.loadInBackground || Delayed(importer.defaultSampleSettings) ||
                        new[] { "WebGL", "Standalone", "Android", "iOS" }.Any(platform =>
                            importer.ContainsSampleSettingsOverride(platform) && Delayed(importer.GetOverrideSampleSettings(platform)));
                }).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (deferred.Length > 0)
                throw new BuildFailedException("Sound effects must preload and decode synchronously:\n" + string.Join("\n", deferred));
            return "All gameplay audio is under Assets/Audio; sound effects preload and decode before gameplay.";
        }
    }
}
