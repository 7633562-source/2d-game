using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Сборка плеера для headless-прогонов.
// Вызывается из командной строки:
//   Unity.exe -batchmode -projectPath <проект> -executeMethod HeadlessBuild.BuildTrialPlayer -logFile -
// Плеер собирается один раз, а параметры баланса передаются ему аргументами,
// поэтому перебор коэффициентов не требует пересборки.
public static class HeadlessBuild
{
    private const string SCENE_PATH = "Assets/Scenes/SampleScene.unity";

    [Serializable]
    private class BuildInfoDto
    {
        public string unityVersion;
        public string buildGuid;
        public string buildTimeUtc;
        public string outputPath;
    }

    public static void BuildTrialPlayer()
    {
        string folder = GetStringArg("-buildOut", DefaultBuildFolder());
        Directory.CreateDirectory(folder);

        string exePath = Path.Combine(folder, "BalanceTrial.exe");

        // Без этого плеер в batchmode может засыпать, потеряв фокус.
        PlayerSettings.runInBackground = true;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { SCENE_PATH },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        bool succeeded = summary.result == BuildResult.Succeeded;

        if (succeeded)
        {
            WriteBuildInfo(folder, exePath, summary);
            Debug.Log($"TRIAL_BUILD_OK {exePath}");
        }
        else
            Debug.LogError($"TRIAL_BUILD_FAILED result={summary.result} errors={summary.totalErrors}");

        EditorApplication.Exit(succeeded ? 0 : 1);
    }

    private static string DefaultBuildFolder()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, "Build", "Headless");
    }

    private static void WriteBuildInfo(string folder, string exePath, BuildSummary summary)
    {
        var dto = new BuildInfoDto
        {
            unityVersion = Application.unityVersion,
            buildGuid = ReadBuildGuid(summary),
            buildTimeUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            outputPath = exePath.Replace('\\', '/')
        };

        File.WriteAllText(Path.Combine(folder, "build-info.json"), JsonUtility.ToJson(dto, true));
    }

    private static string ReadBuildGuid(BuildSummary summary)
    {
        PropertyInfo guidProp = typeof(BuildSummary).GetProperty("guid");
        if (guidProp == null) return "";

        object value = guidProp.GetValue(summary, null);
        return value != null ? value.ToString() : "";
    }

    private static string GetStringArg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return args[index + 1];
    }
}
