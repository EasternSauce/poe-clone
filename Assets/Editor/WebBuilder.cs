using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the Web player into Builds/WebGL (the poe-clone-web git clone) using the Web build profile.
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
        File.WriteAllText(index, File.ReadAllText(index).Replace("__BUILD_ID__", buildId));
        File.WriteAllText(Path.Combine(OutputPath, "version.json"), "{\"build\":\"" + buildId + "\"}");
        return buildId;
    }
}
