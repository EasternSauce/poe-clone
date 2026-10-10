using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PoeClone.Audio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Ships music as one asset bundle per track in StreamingAssets/Music, so players download a
    /// track only when it first plays instead of all music before the game starts. Bundle files
    /// are named after their contents, so browsers keep unchanged tracks cached across deploys.
    /// </summary>
    public sealed class MusicBundles : IPostprocessBuildWithReport
    {
        public const string MusicFolder = "Assets/Audio/Music";
        private const string CacheRoot = "Library/MusicBundles";

        public int callbackOrder => 0;

        // Bundles can't be built while the player build runs, so the Build button builds them first.
        [InitializeOnLoadMethod]
        private static void RegisterBuildHandler()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(options =>
            {
                if (!Build(options.target))
                    throw new BuildFailedException("Music bundles failed to build.");
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
            });
        }

        private static string CacheDir(BuildTarget target) => Path.Combine(CacheRoot, target.ToString());

        // Bundle names must be lowercase; keep them URL-friendly too.
        private static string Slug(string track) =>
            new string(track.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

        /// <summary>Builds (incrementally) one bundle per music clip for the target platform.</summary>
        public static bool Build(BuildTarget target)
        {
            var builds = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => new AssetBundleBuild
                {
                    assetBundleName = Slug(Path.GetFileNameWithoutExtension(path)),
                    assetNames = new[] { path },
                    addressableNames = new[] { Path.GetFileNameWithoutExtension(path) },
                })
                .ToArray();
            string dir = CacheDir(target);
            Directory.CreateDirectory(dir);
            // Audio is already compressed, so chunk compression only keeps bundle reads cheap.
            var manifest = BuildPipeline.BuildAssetBundles(dir, builds, BuildAssetBundleOptions.ChunkBasedCompression, target);
            if (manifest == null)
            {
                Debug.LogError($"Music bundles: build failed for {target}");
                return false;
            }
            Debug.Log($"Music bundles: built {builds.Length} track(s) for {target}");
            return true;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            string output = report.summary.outputPath;
            string streamingAssets = target == BuildTarget.WebGL
                ? Path.Combine(output, "StreamingAssets")
                : Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data", "StreamingAssets");
            string dest = Path.Combine(streamingAssets, "Music");
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.CreateDirectory(dest);

            var tracks = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder })
                .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .Select(track =>
                {
                    string source = Path.Combine(CacheDir(target), Slug(track));
                    if (!File.Exists(source))
                        throw new BuildFailedException($"Music bundles: '{track}' was not built for {target}; build through PoeClone/Build Web or the Build button.");
                    string file = Slug(track) + "-" + Hash(source) + ".bundle";
                    File.Copy(source, Path.Combine(dest, file));
                    return new MusicCatalog.Entry { track = track, file = file };
                })
                .ToArray();
            File.WriteAllText(Path.Combine(dest, MusicCatalog.FileName),
                JsonUtility.ToJson(new MusicCatalog { tracks = tracks }, true));
        }

        private static string Hash(string path)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(md5.ComputeHash(stream), 0, 6).Replace("-", "").ToLowerInvariant();
        }
    }
}
