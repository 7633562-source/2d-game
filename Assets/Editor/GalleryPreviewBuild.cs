using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// A second player, for the art scenes only.
//
// Why not the trial player: Build/Headless/BalanceTrial.exe is single-scene by
// contract (the harness boots SampleScene and nothing else), so the ART-1
// catalogue gets its own small preview build:
//
//   Unity.exe -batchmode -nographics -projectPath <project>
//       -executeMethod GalleryPreviewBuild.Build -quit
//   Build/Preview/GalleryPreview.exe -batchmode -scene TreeApple -capture 1 ...
//
// SampleScene is not in this build, so no trial number can move.
public static class GalleryPreviewBuild
{
    private const string GalleryPath = "Assets/Scenes/Gallery.unity";
    private const string TreeApplePath = "Assets/Scenes/TreeApple.unity";
    // One species per scene (Yury 18.09): every other species scene is a clone of
    // TreeApple.unity written by Tools/species-scenes.py, which rewrites only the
    // three fields that name the species. That tool is the authority on the set -
    // its COVERAGE line reads PlantTree.ParseKind and fails if a kind has no scene -
    // and the build below fails if a path in this list is not on disk.
    private static readonly string[] SpeciesPaths =
    {
        "Assets/Scenes/TreeOak.unity",
        "Assets/Scenes/TreePine.unity",
        "Assets/Scenes/TreeWillow.unity",
        "Assets/Scenes/TreeBush.unity",
        "Assets/Scenes/TreePoplar.unity",
        "Assets/Scenes/TreeLilac.unity",
        "Assets/Scenes/TreeHazel.unity",
        "Assets/Scenes/TreeCedar.unity",
        "Assets/Scenes/TreePlum.unity",
    };
    // BALANCE-ARENA-1: the impact scene is editor-playable AND bootable in this
    // player, so "the scene runs" can be proved from a built player, not only from
    // the trial harness. It records into Trials/Runs from its own component.
    private const string BalanceArenaPath = "Assets/Scenes/BalanceArena.unity";

    public static void Build()
    {
        string folder = Arg("-buildOut", "Build/Preview");
        Directory.CreateDirectory(folder);
        string exePath = Path.Combine(folder, "GalleryPreview.exe");
        PlayerSettings.runInBackground = true;

        List<string> scenes = new List<string> { GalleryPath, TreeApplePath };
        scenes.AddRange(SpeciesPaths);
        scenes.Add(BalanceArenaPath);

        // A scene named here but absent on disk would silently cost a species its
        // scene: fail loudly instead, before the player is written.
        List<string> missing = scenes.FindAll(p => !File.Exists(p));
        if (missing.Count > 0)
        {
            Debug.LogError("PREVIEW_BUILD_FAILED missing scene(s): " + string.Join(", ", missing));
            EditorApplication.Exit(2);
            return;
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log((ok ? "PREVIEW_BUILD_OK " : "PREVIEW_BUILD_FAILED ") + exePath
            + " scenes=" + scenes.Count
            + " errors=" + report.summary.totalErrors + " bytes=" + report.summary.totalSize);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static string Arg(string key, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return fallback;
    }
}
