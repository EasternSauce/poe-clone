using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the Web player into Builds/WebGL using the Web build profile.
// Command line: Unity.exe -batchmode -quit -projectPath . -executeMethod WebBuilder.BuildFromCommandLine
public static class WebBuilder
{
    private const string ProfilePath = "Assets/Settings/Build Profiles/Web - Desktop - Release.asset";
    private const string OutputPath = "Builds/WebGL";

    [MenuItem("PoeClone/Build Web")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        bool ok = Build();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Build()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError($"Build Web: no build profile at {ProfilePath}");
            return false;
        }

        // URP gathers and strips shaders using the Editor's active target, even when
        // BuildPlayer is given a different profile. Require WebGL before preprocessing.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL ||
            BuildProfile.GetActiveBuildProfile() != profile)
        {
            Debug.LogError("Build Web: activate the Web - Desktop - Release profile and wait for compilation before building.");
            return false;
        }

        if (!PoeClone.EditorTools.MusicBundles.Build(BuildTarget.WebGL))
            return false;

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = OutputPath,
            options = BuildOptions.None,
        });

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Build Web: {summary.result} with {summary.totalErrors} error(s)");
            return false;
        }

        string buildId = StampBuild();
        Debug.Log($"Build Web: succeeded in {summary.totalTime}, {summary.totalSize / (1024 * 1024)} MB at {OutputPath} (build {buildId})");
        return true;
    }

    // Gives the published page this build's id (the patch notes version plus the time) and writes
    // it to version.json beside it: open pages poll that file and reload when it changes, and the
    // id on the build files' URLs keeps browsers from mixing in cached files of the old build.
    private static string StampBuild()
    {
        string version = "dev";
        string notes = Path.Combine("Assets", "Resources", "PatchNotes.txt");
        if (File.Exists(notes))
        {
            string first = File.ReadAllLines(notes)[0];
            if (first.StartsWith("version:"))
                version = first.Substring("version:".Length).Trim();
        }
        string buildId = version + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        string index = Path.Combine(OutputPath, "index.html");
        string chunks = BuildDownloadChunks();
        File.WriteAllText(index, File.ReadAllText(index).Replace("__BUILD_ID__", buildId)
            .Replace("\"__DOWNLOAD_CHUNKS__\"", chunks)
            .Replace("// __INLINE_DOWNLOADER__", File.ReadAllText("Assets/WebGLTemplates/FullWindow/chunk-downloads.js")));
        File.WriteAllText(Path.Combine(OutputPath, "version.json"), "{\"build\":\"" + buildId + "\"}");
        return buildId;
    }

    // Small content-addressed files avoid slow single CDN transfers and survive unrelated
    // deployments in the browser cache. Keep the normal files for diagnostics/fallback.
    private static string BuildDownloadChunks()
    {
        string directory = Path.Combine(OutputPath, "Build", "chunks");
        Directory.CreateDirectory(directory);
        var keep = new HashSet<string>();
        var entries = new List<string>();
        foreach (string path in Directory.GetFiles(Path.Combine(OutputPath, "Build")))
        {
            string name = Path.GetFileName(path);
            if (!name.Contains(".data") && !name.Contains(".wasm") && !name.Contains(".framework.js") && !name.EndsWith(".loader.js")) continue;
            byte[] bytes = File.ReadAllBytes(path);
            if (name.Contains(".data"))
            {
                entries.Add("\"Build/" + name + "\":" + BuildAssetChunks(bytes, directory, keep));
                continue;
            }
            var parts = new List<string>();
            const int chunkSize = 512 * 1024;
            using (var sha = SHA256.Create())
            for (int offset = 0; offset < bytes.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, bytes.Length - offset);
                string hash = BitConverter.ToString(sha.ComputeHash(bytes, offset, count)).Replace("-", "").ToLowerInvariant();
                string chunkPath = Path.Combine(directory, hash + ".bin");
                keep.Add(chunkPath);
                using (var output = File.Create(chunkPath)) output.Write(bytes, offset, count);
                parts.Add("\"" + hash + "\"");
            }
            entries.Add("\"Build/" + name + "\":{\"size\":" + bytes.Length + ",\"hashes\":[" + string.Join(",", parts) + "]}");
        }
        foreach (string old in Directory.GetFiles(directory, "*.bin"))
            if (!keep.Contains(old)) File.Delete(old);
        return "{" + string.Join(",", entries) + "}";
    }

    // Cut the uncompressed UnityWebData archive at its contained file boundaries.
    // Cutting one gzip stream instead made every later piece change when a tiny
    // resource (including patch notes or boot.config) changed size.
    private static string BuildAssetChunks(byte[] source, string directory, HashSet<string> keep)
    {
        byte[] bytes = source;
        if (source.Length > 2 && source[0] == 0x1f && source[1] == 0x8b)
        {
            using (var input = new GZipStream(new MemoryStream(source), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                bytes = output.ToArray();
            }
        }
        var boundaries = new SortedSet<int> { 0, bytes.Length };
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(16)) != "UnityWebData1.0\0")
                throw new InvalidDataException("Unknown Unity web asset archive");
            int headerEnd = checked((int)reader.ReadUInt32());
            if (headerEnd < 20 || headerEnd > bytes.Length) throw new InvalidDataException("Invalid asset header");
            boundaries.Add(headerEnd);
            while (reader.BaseStream.Position < headerEnd)
            {
                int offset = checked((int)reader.ReadUInt32()), length = checked((int)reader.ReadUInt32());
                int nameLength = checked((int)reader.ReadUInt32());
                if (offset < headerEnd || length > bytes.Length - offset || nameLength > headerEnd - reader.BaseStream.Position)
                    throw new InvalidDataException("Invalid asset archive entry");
                reader.BaseStream.Position += nameLength;
                boundaries.Add(offset);
                boundaries.Add(checked(offset + length));
            }
        }
        var hashes = new List<string>();
        var parts = new List<string>();
        long transferSize = 0;
        int start = 0;
        using (var sha = SHA256.Create())
        {
            foreach (int end in boundaries)
            {
                while (start < end)
                {
                    int length = Math.Min(512 * 1024, end - start);
                    byte[] compressed;
                    using (var output = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                            gzip.Write(bytes, start, length);
                        compressed = output.ToArray();
                    }
                    string hash = BitConverter.ToString(sha.ComputeHash(compressed)).Replace("-", "").ToLowerInvariant();
                    string path = Path.Combine(directory, hash + ".bin");
                    File.WriteAllBytes(path, compressed);
                    keep.Add(path);
                    hashes.Add("\"" + hash + "\"");
                    parts.Add("{\"offset\":" + start + ",\"length\":" + length + ",\"size\":" + compressed.Length + "}");
                    transferSize += compressed.Length;
                    start += length;
                }
            }
            string fallbackHash = BitConverter.ToString(sha.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
            return "{\"encoding\":\"gzip-pieces\",\"size\":" + bytes.Length + ",\"transferSize\":" + transferSize +
                ",\"fallbackSize\":" + source.Length + ",\"fallbackHash\":\"" + fallbackHash +
                "\",\"hashes\":[" + string.Join(",", hashes) + "],\"parts\":[" + string.Join(",", parts) + "]}";
        }
    }
}
