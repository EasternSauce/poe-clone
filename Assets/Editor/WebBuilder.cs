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

        Debug.Log($"Build Web: succeeded in {summary.totalTime}, {summary.totalSize / (1024 * 1024)} MB at {OutputPath}");
        return true;
    }
}
